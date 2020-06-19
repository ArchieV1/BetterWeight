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

namespace ArchieVBetterWeight
{
    [StaticConstructorOnStartup]
    //This class exists so that I can make the other class not static
    public static class StartupClass
    {
        static StartupClass() //Constructor
        {
            Log.Error("ArchieVBetterWeight");
            DirectoryInfo directoryInfo = new DirectoryInfo(Directory.GetCurrentDirectory());
            ModContentPack content = new ModContentPack(directoryInfo, "123456", "north", 3, "BetterWeight");

            BetterWeight betterWeight = new BetterWeight(content);
        }
    }

    public class BetterWeight : Mod
    {
        /// <summary>
        /// Reference to settings
        /// </summary>
        BetterWeightSettings settings;

        /// <summary>
        /// Constructor
        /// </summary>
        public BetterWeight(ModContentPack content) : base(content)
        {
            Log.Message("Loading BetterWeight...");
            this.settings = GetSettings<BetterWeightSettings>();

            Dictionary<ThingDef, float> dict = CalculateMassForAllBuildings();
            //LogAllBuildingWeights(dict);
            CreatePatches(dict);

            Log.Message("Successfully loaded BetterWeight" +
                "If you have changed the modlist/just installed BetterWeight you need to restart for the changes to take effect");
        }

        /// <summary>
        /// Check if thing is a building
        /// </summary>
        /// <param name="thing"></param>
        /// <returns>true if it is a building</returns>
        public bool IsItABuilding(ThingDef thing)
        {
            //Log.Warning("START IsItABuilding");
            if (thing.costList == null) { /*Log.Message("END IsItABuilding");*/ return false; }
            else { /*Log.Message("END IsItABuilding");*/  return true; }
        }

        /// <summary>
        /// Calculate mass recusively
        /// </summary>
        /// <param name="thing"></param>
        /// <returns>The mass of the passed value</returns>
        public float CalculateMass(ThingDef thing)
        {
            Log.Warning("Start CalculateMass");
            float mass = 0.00f;

            if (IsItABuilding(thing))
            {
                foreach (ThingDefCountClass part in thing.costList)
                {
                    //If the part to build "thing" has a mass
                    if (part.thingDef.BaseMass != 1.00f)
                    {
                        mass += part.thingDef.BaseMass * part.count * settings.efficiency;
                    }
                    else
                    {
                        mass += part.count * CalculateMass(part.thingDef) * settings.efficiency;
                    }
                }
                //mass += thing.costStuffCount * 
            }
            Log.Message("END CalculateMass");
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
            Log.Warning("START CreatePatche");
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

            Log.Message("END CreatePatche");
            return final;
        }

        /// <summary>
        /// Creates the patches document and patches for all ThingDefs given in the dictionary
        /// </summary>
        /// <param name="allBuildings"></param>
        public void CreatePatches(Dictionary<ThingDef, float> allBuildings)
        {
            Log.Warning("START CreatePatches");
            string path = Directory.GetCurrentDirectory();
            path = Directory.GetParent(path).ToString();

            Log.Error(path);
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
                    Log.Message(entry.Key.defName + entry.Value.ToString());
                    patches += CreatePatch(entry.Key, entry.Value);
                }
            }


            patches += "</Patch>";

            file.Write(patches);
            file.Close();
            Log.Message("END CreatePatches");
        }

        /// <summary>
        /// Calculates if this object should be patched
        /// </summary>
        /// <param name="thing"></param>
        /// <returns>true if it should be patched</returns>
        public bool ShouldPatch(ThingDef thing)
        {
            if (settings.shouldPatch.Contains(thing.category)) { return true; }
            else { return false; }
        }

        /// <summary>
        /// GUI for settings
        /// </summary>
        /// <param name="inRect"></param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            //listingStandard.CheckboxLabeled("exampleBoolExplanation", ref settings.exampleBool, "exampleBoolToolTip");
            listingStandard.Label("Efficiency");
            settings.efficiency = listingStandard.Slider(settings.efficiency, 100f, 300f);
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        /// <summary>
        /// Override SettingsCategory to show up in the list of settings.
        /// Using .Translate() is optional, but does allow for localisation.
        /// </summary>
        /// <returns>The (translated) mod name.</returns>
        public override string SettingsCategory()
        {
            return "MyExampleModName".Translate();
        }
    }

    public class BetterWeightSettings : ModSettings
    {
        /// <summary>
        /// The settings that exist in BetterWeights
        /// </summary>
        
        //0.55 is probably the way to go based on: (weight of a battery)/(weight of its cost)
        //But 1.00 gives the nicest weights IMO
        public float efficiency = 0.75f;

        //Array of types to be patched
        public ThingCategory[] shouldPatch = { ThingCategory.Building };

        /// <summary>
        /// Writes setting to file
        /// </summary>
        public override void ExposeData()
        {
            Scribe_Values.Look(ref efficiency, "efficienct", 0.75f);
            //Scribe_Collections.Look(ref shouldPatch, "shouldPatch", LookMode.Reference);
            base.ExposeData();
        }
    }

}

