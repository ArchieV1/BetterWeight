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
    public static class PatchTools
    {
        //because this is static it currently cannot access the class with your settings so you'll have to fix that somehow
        static int efficiency = 80;
        static int numberOfDPToRoundTo = 2;
        static bool roundToNearest5 = true;

        /// <summary>
        /// If it's:
        /// [(A building AND needs materials) OR (A building AND needs stuffMaterials)] AND mass = 1
        /// </summary>
        /// <param name="thing">The thing to be checked if it needs patching</param>
        /// <returns>true if it should be patched</returns>
        public static bool ShouldPatch(ThingDef thing)
        {
            // If its a building that costs either materials or StuffMaterials
            if (((thing.category == ThingCategory.Building && thing.costList != null) ||
                 (thing.category == ThingCategory.Building && thing.costStuffCount != 0))
                && thing.BaseMass == 1)
            {
                //Log.Message(thing.category.ToString());
                //Log.Message(ThingCategory.Building.ToString());

                return true;
            }
            else
            {
                return false;
            }
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
        /// Round the mass based on the settings above
        /// </summary>
        /// <param name="initMass"></param>
        /// <returns></returns>
        public static float RoundMass(float initMass)
        {
            float newMass = new float();

            if (roundToNearest5)
            {
                newMass = (float) Math.Round(initMass * 5, numberOfDPToRoundTo) / 5;
            }
            else
            {
                newMass = (float) Math.Round(initMass, numberOfDPToRoundTo);
            }

            return newMass;
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
            
            //quich check to make sure thing isn't null
            if (req.Thing == null) return true;
            if (req.Thing.def == null) return true;
            
            if (PatchTools.ShouldPatch(req.Thing.def))
            {
                for (var index = 0; index < req.StatBases.Count; index++) //iterate through all stats in request
                {
                    var stat = req.StatBases[index]; //get current stat
                    if (stat.stat.label == "mass") //check if it is the mass
                    {
                        var mass = PatchTools.RoundMass(PatchTools.CalculateMass(req.Thing.def));
                        Log.Message("Changed mass for " + req.Def.defName + " to " + mass);
                        req.StatBases[index].value = mass; //set mass of item here
                    }
                }
            }

            return true; //returns true so function runs with modifed StatReq
        }

        public class BetterWeight : ModBase
        {
            public override string ModIdentifier => "ArchieV.BetterWeight";


            public SettingHandle<int> efficiency;
            private SettingHandle<int> numberOfDPToRoundTo;
            private SettingHandle<bool> roundToNearest5;

            //private List<ThingCategory> shouldPatch = new List<ThingCategory> { ThingCategory.Building };

            public override void DefsLoaded()
            {
                efficiency = Settings.GetHandle<int>(
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
                }
                catch (Exception e)
                {
                    Log.Error(e.ToString());
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
        }
    }
}