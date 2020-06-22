using System;
using System.Xml;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Diagnostics;
using RimWorld;
using System.Linq;
using Verse;
using System.Xml.Linq;
using System.Reflection;
using UnityEngine;
using HugsLib;
using HugsLib.Settings;

namespace ArchieVBetterWeight
{
    //[StaticConstructorOnStartup]
    //This class exists so that I can make the other class not static
    public static class StartupClass
    {
        static StartupClass() //Constructor
        {
            Log.Error("ArchieVBetterWeight");
            //DirectoryInfo directoryInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
            //ModContentPack content = new ModContentPack(directoryInfo, "123456", "north", 3, "BetterWeight");
            //ModContentPack = HugsLib.Settings.;

            BetterWeight betterWeight = new BetterWeight();
        }
    }

    public class BetterWeight : ModBase
    {
        public override string ModIdentifier => "ArchieV.BetterWeight";

        //private List<ThingCategory> shouldPatch = new List<ThingCategory> { ThingCategory.Building };

        private HugsLib.Settings.SettingHandle<int> efficiency;
        private HugsLib.Settings.SettingHandle<int> numberOfDPToRoundTo;
        private HugsLib.Settings.SettingHandle<bool> roundToNearest5;
        private HugsLib.Settings.SettingHandle<bool> needToRestart;

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
                        if ((float)Math.Round(CalculateMass(def)) != 1.00f)
                        {
                            patches += CreatePatch(def, (float)Math.Round(CalculateMass(def)));
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

            return 0.00f;
        }


        /// <summary>
        /// Check if thing is a building
        /// </summary>
        /// <param name="thing"></param>
        /// <returns>true if it is a building</returns>
        public bool IsItABuilding(ThingDef thing)
        {
            //Log.Warning("START IsItABuilding");
            if (thing.costList == null || thing.costStuffCount == 0) { /*Log.Message("END IsItABuilding");*/ return false; }
            else { /*Log.Message("END IsItABuilding");*/  return true; }
        }

        /// <summary>
        /// Calculate mass recusively
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

        /// <summary>
        /// Creates dictionary of all buildings with their newly calculated masses
        /// </summary>
        /// <returns>Dictionary of things with masses</returns>
        public Dictionary<ThingDef, float> CalculateMassForAllBuildings()
        {
            //Log.Warning("Start CalculateMassForAllBuildings");
            List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
            Dictionary<ThingDef, float> buildingsWithMass = new Dictionary<ThingDef, float>();

            foreach (ThingDef thing in allDefs)
            {
                float mass = new float();

                //If it's a building with non default mass
                if (IsItABuilding(thing) && !ItHasMass(thing))
                {
                    mass = CalculateMass(thing);
                    //Round to nearest 0.05
                    mass = (float) Math.Round(mass);
                    buildingsWithMass.Add(thing, mass);
                }
            }
            //Log.Message("END CalculateMassForAllBuildings");
            return buildingsWithMass;
        }

        /// <summary>
        /// Returns if object has a mass other than the base mass
        /// </summary>
        /// <param name="thing"></param>
        /// <returns>true if it has it's own mass (Not 1.00)</returns>
        public bool ItHasMass(ThingDef thing)
        {
            //Log.Warning("START ItHasMass");
            if (thing.BaseMass == 1.00f) { /*Log.Message("END ItHasMass");*/ return false; }
            else { /*Log.Message("END ItHasMass");*/ return true; }
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
        /// Creates the patches document and patches for all ThingDefs given in the dictionary
        /// </summary>
        /// <param name="allBuildings"></param>
        public void CreatePatches(Dictionary<ThingDef, float> allBuildings)
        {
            //Log.Warning("START CreatePatches");
            string path = Directory.GetCurrentDirectory();
            path = Directory.GetParent(path).ToString();

            //Log.Error(path);
            path += "\\Patches";

            foreach (FileInfo toDelete in new DirectoryInfo(path).GetFiles())
            {
                toDelete.Delete();
            }

            path += "\\BetterWeightsPatch.xml";
            StreamWriter file = new StreamWriter(path, false);
            
            string patches = "";
            patches += "<?xml version=\"1.0\" encoding=\"utf - 8\" ?>\n";
            patches += "<Patch>\n";

            foreach (KeyValuePair<ThingDef, float> entry in allBuildings)
            {
                if (ShouldPatch(entry.Key))
                {
                    //Log.Message(entry.Key.defName + entry.Value.ToString());
                    patches += CreatePatch(entry.Key, entry.Value);
                }
            }


            patches += "</Patch>";

            file.Write(patches);
            file.Close();
            //Log.Message("END CreatePatches");
        }

        /// <summary>
        /// If it's:
        /// [(A building AND needs materials) OR(A building AND needs stuffMaterials)] AND mass = 1
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

        /// <summary>
        /// GUI for settings
        /// </summary>
        /// <param name="inRect"></param>
        //public override void DoSettingsWindowContents(Rect inRect)
        //{
        //    Listing_Standard listingStandard = new Listing_Standard();
        //    listingStandard.Begin(inRect);
        //    //listingStandard.CheckboxLabeled("exampleBoolExplanation", ref settings.exampleBool, "exampleBoolToolTip");
        //    listingStandard.Label("Efficiency");
        //    settings.efficiency = listingStandard.Slider(settings.efficiency, 0.05f, 300f);

        //    listingStandard.Label("Categories");
        //    listingStandard.End();
        //    base.DoSettingsWindowContents(inRect);
        //}

        /// <summary>
        /// Override SettingsCategory to show up in the list of settings.
        /// Using .Translate() is optional, but does allow for localisation.
        /// </summary>
        /// <returns>The (translated) mod name.</returns>
        //public override string SettingsCategory()
        //{
        //    return "BetterWeight";
        //}
    }

    //public class BetterWeightSettings : ModSettings
    //{
    //    /// <summary>
    //    /// The settings that exist in BetterWeights
    //    /// </summary>
        
    //    //0.55 is probably the way to go based on: (weight of a battery)/(weight of its cost)
    //    //But 1.00 gives the nicest weights IMO
    //    public float efficiency = 0.75f;

    //    //Array of types to be patched
    //    public ThingCategory[] shouldPatch = { ThingCategory.Building };

    //    /// <summary>
    //    /// Writes setting to file
    //    /// </summary>
    //    public override void ExposeData()
    //    {
    //        Scribe_Values.Look(ref efficiency, "efficiency", 0.75f);
    //        //Scribe_Collections.Look(ref shouldPatch, "shouldPatch", LookMode.Reference);
    //        base.ExposeData();
    //    }
    //}

}

