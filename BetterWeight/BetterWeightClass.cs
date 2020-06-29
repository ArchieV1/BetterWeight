using System;
using System.IO;
using System.Collections.Generic;
using Verse;
using HugsLib;
using HugsLib.Settings;
using HarmonyLib;
using System.Reflection;
using ArchieVBetterWeight;
using RimWorld;

namespace ArchieVBetterWeight
{
    public class PatchTools
    {
        /// <summary>
        /// Round the mass based on the settings above
        /// </summary>
        /// <param name="initMass"></param>
        /// <returns></returns>
        public static float RoundMass(float initMass)
        {
            float newMass = new float();

            if (BetterWeight.roundToNearest5)
            {
                newMass = (float)Math.Round(initMass * 5, BetterWeight.numberOfDPToRoundTo) / 5;
            }
            else
            {
                newMass = (float)Math.Round(initMass, BetterWeight.numberOfDPToRoundTo);
            }

            return newMass;
        }

        /// <summary>
        /// If it's:
        /// [(A building AND needs materials) OR (A building AND needs stuffMaterials)] AND mass = 1
        /// </summary>
        /// <param name="thing">The thing to be checked if it needs patching</param>
        /// <returns>true if it should be patched</returns>
        public static bool ShouldPatch(ThingDef thing)
        {
            if (BetterWeight.listToPatch.Contains(thing)) { return true; }
            else { return false; }
        }

        /// <summary>
        /// Calculate mass recusively. NO ROUNDING IS DONE HERE
        /// </summary>
        /// <param name="thing"></param>
        /// <returns>The mass of the passed value</returns>
        public static float CalculateMass(ThingDef thing)
        {
            //Log.Warning("Start CalculateMass");
            float mass = 0.00f;
            try

            {
                if (thing.costList != null)
                {
                    foreach (ThingDefCountClass part in thing.costList)
                    {
                        mass += part.thingDef.BaseMass * part.count * BetterWeight.defaultEfficiency / 100f;
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
                    mass += thing.costStuffCount * (BetterWeight.defaultEfficiency / 100f);
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

    }

    public static class StartupClass
    {
        static StartupClass() //Constructor
        {
            Log.Message("ArchieVBetterWeight");
            Harmony harmony = new Harmony("uk.ArchieV.projects.modding.Rimworld.BetterWeight");
            //Harmony.DEBUG = true;

            harmony.PatchAll();

            //BetterWeight betterWeight = new BetterWeight();
        }
    }

    [HarmonyPatch(typeof(StatWorker))]
    [HarmonyPatch(nameof(StatWorker.GetValueUnfinalized))]
    static class StatWorker_GetValueUnfinalized_Patch
    {
        //runs before GetValueUnfinalized
        static bool Prefix(float __result, StatRequest req, bool applyPostProcess)
        {
            //todo reimplement check to see if its 1 or 0. You should probs put it in ShouldPatch() tho

            // Quick check to make sure thing isn't null
            if (req.Thing == null) { return true; }
            if (req.Thing.def == null) { return true; }
            if (req.StatBases == null) { return true; }

            if (PatchTools.ShouldPatch(req.Thing.def))
            {
                bool addMass = true;
                for (var index = 0; index < req.StatBases.Count; index++) //iterate through all stats in request
                {
                    var stat = req.StatBases[index]; //get current stat
                    if (stat.stat.label == "mass") //check if it is the mass
                    {
                        var mass = PatchTools.RoundMass(PatchTools.CalculateMass(req.Thing.def));
                        //Log.Error("Changed mass for " + req.Def.defName + " to " + mass, true);
                        req.StatBases[index].value = mass; //set mass of item here    
                        addMass = false;
                    }
                }
                if ((addMass && req.Thing.def.costList != null)
                    ||
                    (addMass && req.Thing.def.costStuffCount != 0))
                {
                    if (req.Thing.def.costList == null)
                    {
                        return true;
                    }
                    if (req.Thing.def.costList.Count == 0)
                    {
                        return true;
                    }

                    StatModifier statModifier = new StatModifier
                    {
                        stat = StatDefOf.Mass,
                        value = PatchTools.CalculateMass(req.Thing.def)
                    };

                    req.StatBases.Add(statModifier);

                    Log.Message("Added mass for " + req.Thing.def.defName);
                }
            }

            return true; //returns true so function runs with modifed StatReq
        }
        
    }

    public class BetterWeight : ModBase
    {
        public override string ModIdentifier => "ArchieV.BetterWeight";


        public static SettingHandle<int> defaultEfficiency;
        public static SettingHandle<int> numberOfDPToRoundTo;
        public static SettingHandle<bool> roundToNearest5;
        public static Dictionary<ThingDef, float> thingDefEffeciency = new Dictionary<ThingDef, float>();
        public static List<ThingDef> listToPatch = new List<ThingDef>();


        //private List<ThingCategory> shouldPatch = new List<ThingCategory> { ThingCategory.Building };

        public override void DefsLoaded()
        {
            defaultEfficiency = Settings.GetHandle<int>(
                "BetterWeight_efficiency",
                "Efficiency",
                "What percentage of the weight goes into the building and what percentage is waste\n" +
                "Range = 1% - 300%",
                75,
                Validators.IntRangeValidator(1, 300));

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

            thingDefEffeciency = Settings.GetHandle<Dictionary<ThingDef, float>> (
                "BetterWeight_thingDefEfficiency",
                "Thing / Efficiency",
                "The name of the thing / The efficiency of that thing",
                BetterWeight.thingDefEffeciency);

            listToPatch = Settings.GetHandle<List<ThingDef>>(
                "BetterWeight_ListToPatch",
                "To Patch",
                "The list of things to be assigned a new calculated mass,",
                null);

            // Set default list to patch
            if (listToPatch == null)
            {
                listToPatch = generateDefaultListToPatch();
            }

        }


        /// <summary>
        /// Constructor
        /// </summary>
        public BetterWeight()
        {
            try
            {
                Log.Message(DateTime.Now.ToString("h:mm:ss tt") + " Loading BetterWeight...");

                DefsLoaded();

                thingDefEffeciency = generateDefaultDictionary();

                Log.Message(DateTime.Now.ToString("h:mm:ss tt") + " Finished loading BetterWeight");
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                Log.Error("Failed to load BetterWeight.");
                Log.Error("Please leave a bug report at https://github.com/ArchieV1/BetterWeight");
            }
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

        public Dictionary<ThingDef, float> generateDefaultDictionary()
        {
            List<ThingDef> things = (List<ThingDef>) DefDatabase<ThingDef>.AllDefs;
            Dictionary<ThingDef, float> dictionary = new Dictionary<ThingDef, float>();

            foreach (ThingDef thing in things)
            {
                if (thing.category == ThingCategory.Building && thing.BaseMass == 1)
                {
                    dictionary.Add(thing, BetterWeight.defaultEfficiency);
                }
            }
            return dictionary;
        }

        public List<ThingDef> generateDefaultListToPatch()
        {
            List<ThingDef> things = (List<ThingDef>) DefDatabase<ThingDef>.AllDefs;
            List<ThingDef> toPatch = new List<ThingDef>();

            foreach (ThingDef thing in things)
            {
                if (thing.category == ThingCategory.Building && thing.BaseMass == 1)
                {
                    toPatch.Add(thing);
                }
            }
            return toPatch;
        }
    }

    public class ThingDefFloatList : SettingHandleConvertible
    {
        public Dictionary<ThingDef, float> thingDefEffeciency = new Dictionary<ThingDef, float>();

        public override void FromString(string settingValue)
        {
            string[] thing = settingValue.Split('|');

            thingDefEffeciency.Add(ThingDef.Named(thing[0]), (float) Math.Round(float.Parse(thing[1]), 2));
        }

        public override string ToString()
        {
            string list = "";

            foreach(KeyValuePair<ThingDef, float> pair in thingDefEffeciency)
            {
                list += pair.Key.defName + " | " + pair.Value.ToString() + "\n";
            }

            return list;
        }
    }

    public class ThingDefList : SettingHandleConvertible
    {
        public List<ThingDef> things = new List<ThingDef>();

        public override bool ShouldBeSaved
        {
            get { return things.Count > 0; }
        }

        public override void FromString(string settingValue)
        {
            // Add ThingDef with passed name
            things.Add(ThingDef.Named(settingValue));
        }

        public override string ToString()
        {
            string list = "";

            foreach (ThingDef thing in things)
            {
                list += thing.defName + "\n";
            }
            return list;
        }
    }
}