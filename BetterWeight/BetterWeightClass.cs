using System;
using System.IO;
using System.Collections.Generic;
using Verse;
using HugsLib;
using HugsLib.Settings;
using HarmonyLib;
using System.Reflection;
using RimWorld;

namespace ArchieVBetterWeight
{
    public static class StartupClass
    {
        static StartupClass() //Constructor
        {
            Log.Message("ArchieVBetterWeight");
            Harmony harmony = new Harmony("uk.ArchieV.projects.modding.Rimworld.BetterWeight");
            //Harmony.DEBUG = true;

            //HarmonyPatchs harmonyPatches = new HarmonyPatchs();

            harmony.PatchAll();

            //BetterWeight betterWeight = new BetterWeight();
        }
    }

    public class BetterWeight : ModBase
    {
        public override string ModIdentifier => "ArchieV.BetterWeight";

        //private List<ThingCategory> shouldPatch = new List<ThingCategory> { ThingCategory.Building };

        private SettingHandle<int> efficiency;
        private SettingHandle<int> numberOfDPToRoundTo;
        private SettingHandle<bool> roundToNearest5;
        private SettingHandle<bool> needToRestart;

        public override void DefsLoaded()
        {
            efficiency = Settings.GetHandle<int>(
                "BetterWeight_efficiency",
                "Efficiency",
                "What percentage of the weight goes into the building and what percentage is waste\n" +
                "Range = 1% - 300%",
                75,
                Validators.IntRangeValidator(1, 500));

            numberOfDPToRoundTo = Settings.GetHandle<int>(
                "BetterWeight_numberOfDPToRoundTo",
                "Number of decimal points to round to",
                "How many decimal points to round the calculated value to\n" +
                "Range = 0 - 2 decimal places",
                0,
                Validators.IntRangeValidator(0, 2));

            roundToNearest5 = Settings.GetHandle<bool>(
                "BetterWeight_roundToNearest5",
                "Round to the nearest 5",
                "Should calculated masses be rounded to the nearest 5 of the number of DP specified?\n" +
                "Eg:\n" +
                "1.24 to 2DP and with this true will round to 1.25\n" +
                "234.37 to 0DP with this true will round to 235",
                false);

            needToRestart = Settings.GetHandle<bool>(
                "BetterWeight_needToRestart",
                "Does the game need a restart",
                "After:\n" +
                "Changing settings\n" +
                "Loading with betterweight for the first time\n" +
                "Changing the modlist to add something that adds buildings\n" +
                "You MUST restart your game for changes to take effect (Otherwise the new buildings will have a mass of 1.00kg",
                true);
            needToRestart.NeverVisible = true;
            needToRestart.CanBeReset = false;

        }


        /// <summary>
        /// Constructor
        /// </summary>
        public BetterWeight()
        {
            try
            {
                Log.Message(DateTime.Now.ToString("h:mm:ss tt") + "Loading BetterWeight...");

                DefsLoaded();

                if (CreatePatchesForBuildable())
                {
                    Log.Message(DateTime.Now.ToString("h:mm:ss tt") + "Successfully loaded BetterWeight");
                    Log.Message("If you have changed the modlist/just installed BetterWeight you need to restart for the changes to take effect");
                }
                else
                {
                    Log.Error(DateTime.Now.ToString("h:mm:ss tt") + "Failed to load BetterWeight");
                    Log.Error("Please create a bug report at http://github.com/archiev1/BetterWeight");
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }

        }

        /// <summary>
        /// Creates all of the patches and the XML file of patches
        /// </summary>
        /// <returns>True if success</returns>
        public Boolean CreatePatchesForBuildable()
        {
            try
            {
                // Create all fo the indivisual patches
                List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefsListForReading;

                String patches = "";

                foreach (ThingDef def in allDefs)
                {
                    // Replace with a better "should patch"
                    if (ShouldPatch(def))
                    {
                        // If it has equaled 1 again it will log a non important error.
                        // May as well remove it though
                        if (RoundMass(CalculateMass(def)) != 1.00f && RoundMass(CalculateMass(def)) != 0.00f)
                        {
                            patches += CreatePatch(def, (RoundMass(CalculateMass(def))));
                            //Log.Message(def.defName);
                        }
                    }
                }

                if (CreatePatchDoc(patches))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Log.Message(e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Creates the XML patch doc
        /// </summary>
        /// <param name="patch">The formatted XML patches</param>
        /// <returns>true if it works</returns>
        public Boolean CreatePatchDoc(String patch)
        {
            try
            {
                // Create the XML file and remove existing ones
                string path = Directory.GetCurrentDirectory();
                path = Directory.GetParent(path).ToString();
                path += "\\Patches";

                foreach (FileInfo toDelete in new DirectoryInfo(path).GetFiles())
                {
                    toDelete.Delete();
                }

                path += "\\BetterWeightsPatch.xml";
                StreamWriter file = new StreamWriter(path, false);

                string patches = "<?xml version=\"1.0\" encoding=\"utf - 8\" ?>\n<Patch>\n";

                patches += patch;

                // End of the XML
                patches += "</Patch>";

                file.Write(patches);
                file.Close();

                return true;
            }
            catch (Exception e)
            {
                // Delete the file it didnt end so it can't cause issues later
                string path = Directory.GetCurrentDirectory();
                foreach (FileInfo toDelete in new DirectoryInfo(path).GetFiles())
                {
                    toDelete.Delete();
                }

                Log.Error(e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Round the mass based on the settings above
        /// </summary>
        /// <param name="initMass"></param>
        /// <returns></returns>
        public float RoundMass(float initMass)
        {
            //numberOfDPToRoundTo
            //roundToNearest5

            float newMass = new float();

            if (roundToNearest5)
            {
                newMass = (float)Math.Round(initMass * 5, numberOfDPToRoundTo) / 5;
            }
            else
            {
                newMass = (float)Math.Round(initMass, numberOfDPToRoundTo);
            }

            return newMass;
        }

        /// <summary>
        /// Calculate mass recusively. NO ROUNDING IS DONE HERE
        /// </summary>
        /// <param name="thing"></param>
        /// <returns>The mass of the passed value</returns>
        public float CalculateMass(ThingDef thing)
        {
            //Log.Warning("Start CalculateMass");
            float mass = 0.00f;

            try
            {
                if (thing.costList != null)
                {
                    foreach (ThingDefCountClass part in thing.costList)
                    {
                        mass += part.thingDef.BaseMass * part.count * efficiency / 100;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }
            // If has costStuffCount eg, can be made of wood/steel/granite
            // Wayyyyy too hard to calculate it properly so just assume mass of material = 1
            // This assumptions means wooden doors weigh more than their parts total but stone weigh a bit less
            try
            {
                if (thing.costStuffCount != 0)
                {
                    mass += thing.costStuffCount * efficiency / 100;
                }
            }
            catch (Exception e)
            {
                Log.Message(e.ToString());
            }
            //Log.Message("END CalculateMass");
            //Log.Error(thing.defName + thing.costStuffCount);
            return mass;
        }

        public void LogAllBuildingWeights(Dictionary<ThingDef, float> allBuildings)
        {
            //Log.Warning("START logAllBuildingsWeights");
            foreach (KeyValuePair<ThingDef, float> pair in allBuildings)
            {
                Log.Message(
                    pair.Key.defName + "\n" +
                    pair.Key.BaseMass + "BaseMass" + "\n" +
                    pair.Value.ToString() + "NewMass");
            }
            //Log.Message("END ItHasMass");
        }

        /// <summary>
        /// Creates the path for the specific ThingDef and new mass
        /// </summary>
        /// <param name="thing"></param>
        /// <param name="newMass"></param>
        /// <returns></returns>
        public string CreatePatch(ThingDef thing, float newMass)
        {
            //Log.Warning("START CreatePatche");
            string final = "";
            string operation = "";
            if (thing.BaseMass == 1.00f)
            {
                operation = "PatchOperationAdd";
            }
            else
            {
                operation = "PatchOperationReplace";
            }
            //Writes the patch for the given ThingDef
            final += "<Operation Class=\"" + operation + "\">\n";
            final += "<xpath>/Defs/ThingDef[defName = \"" + thing.defName + "\"]/statBases</xpath>\n";
            final += "<value>\n";
            final += "    <Mass> " + newMass + " </Mass>\n";
            final += "</value>\n";
            final += "</Operation>\n";

            //Log.Message("END CreatePatche");
            return final;
        }

        /// <summary>
        /// If it's:
        /// [(A building AND needs materials) OR (A building AND needs stuffMaterials)] AND mass = 1
        /// </summary>
        /// <param name="thing">The thing to be checked if it needs patching</param>
        /// <returns>true if it should be patched</returns>
        public bool ShouldPatch(ThingDef thing)
        {
            // If its a building that costs either materials or StuffMaterials
            if (((thing.category == ThingCategory.Building && thing.costList != null) || (thing.category == ThingCategory.Building && thing.costStuffCount != 0))
                && thing.BaseMass == 1)
            {
                //Log.Message(thing.category.ToString());
                //Log.Message(ThingCategory.Building.ToString());

                return true;
            }
            else { return false; }
        }
    }

    [HarmonyPatch(typeof(Verse.ThingDef))] // Edits thingdef
    //[HarmonyPatch(MethodType.Getter)] // Edits a getter
    [HarmonyPatch("BaseMass", MethodType.Getter)] // Edits the get BaseMass getter
    class HarmonyPatchs
    {
        /// <summary>
        /// This seems to only be debug
        /// </summary>
        /// <param name="__result"></param>
        [HarmonyPostfix]
        static void GetBaseMassPatch(ref float __result)
        {
            __result = 62;
            Log.Error("Ran code", true);
            //return 62f; //return false to skip execution of the original.
        }
    }

    /// <summary>
    /// This one works. But only when getting mass
    /// </summary>
    [HarmonyPatch(typeof(StatExtension))]
    [HarmonyPatch(nameof(StatExtension.GetStatValueAbstract), new Type[] { typeof(BuildableDef), typeof(StatDef), typeof(ThingDef)} )]
    static class GetStatValueAbstract3
    {
        //[HarmonyPrefix]
        //static bool getbasevalueforpatch(this AbilityDef def, StatDef stat)
        //{
        //    Log.Error(def.defName + stat.ToString());
        //    //return stat.Worker.GetValueAbstract(def);
        //    return true; //let origional run
        //}
        [HarmonyPostfix]
        public static void GetStatValueAbstract2(ref float __result)
        {
            //Log.Error("StatValue " + __result.ToString());
        }

        [HarmonyPrefix]
        public static bool GetStatValueAbstract5(this BuildableDef def, StatDef stat, ThingDef stuff = null)
        {
            //Log.Message(def.defName + stat.defName);
            if (!stat.defName.EqualsIgnoreCase("StuffPower_Insulation_Heat") && !stat.defName.EqualsIgnoreCase("StuffPower_Insulation_Cold") &&
                !stat.defName.EqualsIgnoreCase("Medicalpotency") && !stat.defName.EqualsIgnoreCase("beauty") && !stat.defName.EqualsIgnoreCase("nutrition") &&
                !stat.defName.EqualsIgnoreCase("ComfyTemperatureMin") && !stat.defName.EqualsIgnoreCase("ComfyTemperatureMax") &&
                !stat.defName.EqualsIgnoreCase("worktomake") && !stat.defName.EqualsIgnoreCase("worktobuild") && !stat.defName.EqualsIgnoreCase("marketvalue") &&
                !stat.defName.EqualsIgnoreCase("StuffPower_Armor_Sharp") && !stat.defName.EqualsIgnoreCase("StuffPower_Armor_Blunt"))
            {
                Log.Error(stat.defName, true);
            }
            //return stat.Worker.GetValueAbstract(def, stuff);
            // Its called "Mass" and its when you make a caravan
            return true;
        }
    }

    static class GetStatValue3
    {
        [HarmonyPatch(typeof(StatExtension))]
        [HarmonyPatch(nameof(StatExtension.GetStatValue))]
        public static float GetStatValue(this Thing thing, StatDef stat, bool applyPostProcess = true)
        {
            if (!thing.def.defName.EqualsIgnoreCase("StuffPower_Insulation_Heat") && !thing.def.defName.EqualsIgnoreCase("StuffPower_Insulation_Cold"))
            {
                Log.Error(thing.def.defName, true);
            }
            return stat.Worker.GetValue(thing, applyPostProcess);
        }

        
    }

    /// <summary>
    /// Literally no clue when this is called
    /// </summary>
    [HarmonyPatch(typeof(StatWorker))]
    [HarmonyPatch(nameof(StatWorker.GetValueAbstract), new Type[] { typeof(AbilityDef) })]
    static class patch4
    {
        [HarmonyPrefix]
        public static bool GetValueAbstractPatch(AbilityDef def)
        {
            Log.Error(def.defName, true);
            //return StatWorker.GetValue(StatRequest.For(def), true);
            return true;
        }

        [HarmonyPostfix]
        public static void GetValueAbstractPatc2h(AbilityDef def)
        {
            Log.Error(def.defName, true);
            //return StatWorker.GetValue(StatRequest.For(def), true);
        }
    }

    //[HarmonyPatch(typeof(StatExtension))]
    //[HarmonyPatch(nameof(StatExtension.GetStatValue), new Type[] { typeof(BuildableDef), typeof(ThingDef) })]
    //static class GetValueAbstract2
    //{
    //    [HarmonyPrefix]
    //    static bool prefixOne(BuildableDef def, ThingDef stuffDef = null)
    //    {
    //        Log.Error(def.defName + stuffDef.defName);
    //        return true;
    //    }
    //}
}

