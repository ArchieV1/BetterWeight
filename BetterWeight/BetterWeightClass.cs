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
using UnityEngine;
using System.Linq;

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
                newMass = (float) Math.Round(initMass * 5f, BetterWeight.numberOfDPToRoundTo, MidpointRounding.AwayFromZero) / 5f;
                newMass = (float)Math.Round(newMass, BetterWeight.numberOfDPToRoundTo);
            }
            else
            {
                newMass = (float)Math.Round(initMass, BetterWeight.numberOfDPToRoundTo, MidpointRounding.AwayFromZero);
            }

            return newMass;
        }

        /// <summary>
        /// If passed value is in listToPatch it should be patched with new weight.
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
        /// <param name="thing">The thing to have its new value calculated</param>
        /// <returns>The (new) mass of the passed value</returns>
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
        /// <summary>
        /// Runs before GetValueUnfinalized
        /// With nullcheck, if requested value of mass does not exist (So it is using default) then add a mass value and calculate it
        /// </summary>
        /// <param name="__result"></param>
        /// <param name="req"></param>
        /// <param name="applyPostProcess"></param>
        /// <returns></returns>
        static bool Prefix(float __result, StatRequest req, bool applyPostProcess)
        {
            // Quick check to make sure thing isn't null
            if (req.Thing == null)
            {
                return true;
            }

            if (req.Thing.def == null)
            {
                return true;
            }

            if (req.StatBases == null)
            {
                return true;
            }

            if (PatchTools.ShouldPatch(req.Thing.def))
            {
                bool needsMass = true;
                for (var index = 0; index < req.StatBases.Count; index++) //iterate through all stats in request
                {
                    var stat = req.StatBases[index]; //get current stat
                    if (stat.stat.label == "mass") //check if it is the mass
                    {
                        var new_mass = PatchTools.RoundMass(PatchTools.CalculateMass(req.Thing.def));
                        if (stat.value != 0 && stat.value != 1)
                        {
                            Log.Message("Changed mass for " + req.Def.defName + " to " + new_mass, true);
                            req.StatBases[index].value = new_mass; //set mass of item here    
                        }

                        needsMass = false;
                    }
                }

                if (needsMass)
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

        public static IEnumerable<ThingDef> toPatchList { get; internal set; }

        public static SettingHandle<int> defaultEfficiency;
        public static SettingHandle<int> numberOfDPToRoundTo;
        public static SettingHandle<bool> roundToNearest5;
        public static Dictionary<ThingDef, float> thingDefEffeciency = new Dictionary<ThingDef, float>();
        public static List<ThingDef> listToPatch = new List<ThingDef>();
        public static List<ThingDef> listNotToPatch = new List<ThingDef>();
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

            listNotToPatch = Settings.GetHandle<List<ThingDef>>(
                "BetterWeight_ListNotToPatch",
                "To NOT Patch",
                "The list of things to NOT be assigned a new calculated mass,",
                null);
            // Set default list to patch
            if (listNotToPatch == null)
            {
                listNotToPatch = generateDefaultListToNotPatch();
            }

            thingDefEffeciency = Settings.GetHandle<Dictionary<ThingDef, float>>(
                "BetterWeight_thingDefEfficiency",
                "Thing / Efficiency",
                "The name of the thing / The efficiency of that thing",
                BetterWeight.thingDefEffeciency);
            // Set default dict to edit values individually
            if (thingDefEffeciency == null)
            {
                thingDefEffeciency = generateDefaultDictionary();
            }
        }

        public override void SettingsChanged()
        {
            base.SettingsChanged();
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

                // Make settings invisible in the settings menu
                Settings.GetHandle<List<ThingDef>>("BetterWeight_ListToPatch").NeverVisible = true;
                Settings.GetHandle<List<ThingDef>>("BetterWeight_ListNotToPatch").NeverVisible = true;
                Settings.GetHandle<Dictionary<ThingDef, float>>("BetterWeight_thingDefEfficiency").NeverVisible = true;

                // Make the settings save when game closes
                Settings.GetHandle<List<ThingDef>>("BetterWeight_ListToPatch").OnValueChanged(listToPatch);
                Settings.GetHandle<List<ThingDef>>("BetterWeight_ListNotToPatch").OnValueChanged(listNotToPatch);

                if (BetterWeight.listNotToPatch == null)
                {
                    listNotToPatch = generateDefaultListToNotPatch();
                }

                Log.Message(DateTime.Now.ToString("h:mm:ss tt") + " Finished loading BetterWeight");
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                Log.Error("Failed to load BetterWeight.");
                Log.Error("Please leave a bug report at https://github.com/ArchieV1/BetterWeight");
            }
        }

        private void InitializeSettings()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Generate list of all things to be patched with the efficiency per item. NOT IN USE
        /// </summary>
        /// <returns>Dictionary of ThingDef to efficiency</returns>
        public Dictionary<ThingDef, float> generateDefaultDictionary()
        {
            Dictionary<ThingDef, float> dictionary = new Dictionary<ThingDef, float>();

            foreach (ThingDef thing in BetterWeight.listToPatch)
            {
                dictionary.Add(thing, BetterWeight.defaultEfficiency);
            }
            return dictionary;
        }

        /// <summary>
        /// Generate list of ThingDefs that are the default. Category = Building && baseMass = 1
        /// </summary>
        /// <returns>List of things that should have a new mass calculated</returns>
        public static List<ThingDef> generateDefaultListToPatch()
        {
            List<ThingDef> things = (List<ThingDef>) DefDatabase<ThingDef>.AllDefs;
            List<ThingDef> toPatch = new List<ThingDef>();

            foreach (ThingDef thing in things)
            {
                if (thing.category == ThingCategory.Building && thing.BaseMass == 1 && (thing.costList != null || thing.costStuffCount != 0))
                {
                    toPatch.Add(thing);
                }
            }
            return toPatch;
            
        }

        public static List<ThingDef> generateDefaultListToNotPatch()
        {
            List<ThingDef> things = (List<ThingDef>)DefDatabase<ThingDef>.AllDefs;
            List<ThingDef> toNotPatch = new List<ThingDef>();

            foreach (ThingDef thing in things)
            {
                if (thing.category == ThingCategory.Building && thing.BaseMass != 1 && (thing.costList != null || thing.costStuffCount != 0))
                {
                    toNotPatch.Add(thing);
                }
            }
            return toNotPatch;
        }

        public static void SortlistNotToPatchlistToPatch()
        {
            // Order the lists by name
            BetterWeight.listNotToPatch = BetterWeight.listNotToPatch.OrderBy(keySelector: kS => kS.defName).ToList();
            BetterWeight.listToPatch = BetterWeight.listToPatch.OrderBy(keySelector: kS => kS.defName).ToList();
        }

        /// <summary>
        /// Save both BetterWeight_ListToPatch and BetterWeight_ListNotToPatch to the HugsLib settings file
        /// </summary>
        public void SaveListToPatchANDListToNotPatch()
        {
            Settings.GetHandle<List<ThingDef>>("BetterWeight_ListToPatch").ForceSaveChanges();
            Settings.GetHandle<List<ThingDef>>("BetterWeight_ListNotToPatch").ForceSaveChanges();
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
            String[] stringThings = settingValue.Split('\n');
            foreach(String str in stringThings)
            {
                things.Add(ThingDef.Named(str));
            }
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

    internal class BetterWeightSettingsValues : ModSettings
    {
        /// <summary>
        /// Take the settings from the HugsLib settings section
        /// </summary>
 
        public List<ThingDef> ToPatch = BetterWeight.listToPatch;
        public List<ThingDef> NotToPatch = BetterWeight.listNotToPatch;

        public override void ExposeData()
        {
            //Scribe_Values.Look(ref ToPatch, "ToPatch");
            //Scribe_Values.Look(ref NotToPatch, "NotToPatch");


            base.ExposeData();
        }
    }

    public class BetterWeightSettingsMenu : Mod
    {
        // Control the scroll bars and which is currently selected
        private Vector2 ScrollPositionLeft;
        private Vector2 ScrollPositionRight;
        private ThingDef leftSelected;
        private ThingDef rightSelected;

        /// <summary>
        /// References the settings init'd above
        /// </summary>
        BetterWeightSettingsValues settings;

        /// <summary>
        /// Constructor to resolve the settings
        /// </summary>
        /// <param name="content"></param>
        public BetterWeightSettingsMenu(ModContentPack content) : base(content)
        {
            settings = GetSettings<BetterWeightSettingsValues>();
        }

        /// <summary>
        /// The GUI settings page
        /// </summary>
        /// <param name="inRect"></param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Sort the lists alphabetically
            BetterWeight.SortlistNotToPatchlistToPatch();

            base.DoSettingsWindowContents(inRect: inRect);


            Rect topRect = inRect.TopPart(0.13f);
            Rect MainRect = inRect.BottomPart(0.87f);

            Widgets.Label(topRect.TopHalf(), "For changes to take effect you must reload your save\n" +
                "More settings can be found at the bottom of the Mod Settings list");

            Rect leftSide = MainRect.LeftPart(0.48f);
            Rect rightSide = MainRect.RightPart(0.48f);

            // Left side of selection window
            float num = 30f;

            Rect viewRectLeft = new Rect(x: 0f, y: 0f, width: leftSide.width - 30, height: BetterWeight.listToPatch.Count * 30f);

            Rect leftTitle = new Rect(x: leftSide.xMin, y: leftSide.yMin-30, width: viewRectLeft.width - 10, height: 30);
            Widgets.Label(leftTitle, "BetterWeight");

            Widgets.BeginScrollView(outRect: leftSide, scrollPosition: ref ScrollPositionLeft, viewRect: viewRectLeft);
            if (BetterWeight.listToPatch != null)
            {
                foreach (ThingDef thing in BetterWeight.listToPatch)
                {
                    try
                    {
                        Rect rowRect = new Rect(x: 5, y: num, width: viewRectLeft.width - 10, height: 30);
                        Widgets.DrawHighlightIfMouseover(rect: rowRect);

                        // The name and icon of the thing
                        Widgets.DefLabelWithIcon(rowRect, thing);

                        // Show the number on the right side of the name
                        Rect rightPartRow = rowRect.RightPartPixels(90);
                        Rect massRect = rightPartRow.LeftPart(pct: 0.45f);
                        Rect weightRect = rightPartRow.RightPart(pct: 0.55f);

                        // Old Mass
                        Widgets.Label(massRect, thing.BaseMass.ToString());
                        // Weight
                        Widgets.Label(weightRect, PatchTools.RoundMass(PatchTools.CalculateMass(thing)).ToString());

                        // Logic for button clicked
                        if (Widgets.ButtonInvisible(butRect: rowRect))
                        {
                            leftSelected = thing;
                        }

                        if (leftSelected == thing)
                        {
                            Widgets.DrawHighlightSelected(rowRect);
                        }

                        num += 30f;
                    }
                    catch (Exception e)
                    {
                        Log.Error(e.ToString());
                        Log.Error("broke on " + thing.defName);
                        foreach (ThingDef def in BetterWeight.listToPatch)
                        {
                            Log.Error(def.defName);
                        }
                    }
                }
            }
            Widgets.EndScrollView();
            // End of left side

            //Right side of selection window
            num = 30f;

            Rect viewRectRight = new Rect(x: 0f, y: 0f, width: rightSide.width - 30, height: BetterWeight.listNotToPatch.Count * 30f);

            Rect rightTitle = new Rect(x: rightSide.xMin, y: rightSide.yMin - 30, width: viewRectLeft.width - 10, height: 30);
            Widgets.Label(rightTitle, new GUIContent("Default Mass", "All things in this category will not be effected by BetterMass and will instead use their default mass"));

            Widgets.BeginScrollView(outRect: rightSide, scrollPosition: ref ScrollPositionRight, viewRect: viewRectRight);
            if (BetterWeight.listNotToPatch != null)
            {
                foreach (ThingDef thing in BetterWeight.listNotToPatch)
                {
                    try
                    {
                        Rect rowRect = new Rect(x: 5, y: num, width: viewRectRight.width - 10, height: 30);
                        Widgets.DrawHighlightIfMouseover(rect: rowRect);

                        Widgets.DefLabelWithIcon(rowRect, thing);

                        // Show the number on the right side of the name
                        Rect rightPartRow = rowRect.RightPartPixels(90);
                        Rect massRect = rightPartRow.LeftPart(pct: 0.45f);
                        Rect weightRect = rightPartRow.RightPart(pct: 0.55f);

                        // Old Mass
                        Widgets.Label(massRect, thing.BaseMass.ToString());
                        // Weight
                        Widgets.Label(weightRect, PatchTools.RoundMass(PatchTools.CalculateMass(thing)).ToString());


                        // Logic for thing clicked
                        if (Widgets.ButtonInvisible(butRect: rowRect))
                        {
                            rightSelected = thing;
                        }

                        if (rightSelected == thing)
                        {
                            Widgets.DrawHighlightSelected(rowRect);
                        }
                        num += 30f;
                    }
                    catch (Exception e)
                    {
                        Log.Error("broke on " + thing.defName);
                        foreach (ThingDef def in BetterWeight.listNotToPatch)
                        {
                            Log.Error(def.defName);
                        }
                    }
                }
            }
            Widgets.EndScrollView();
            // End of right side

            // Central buttons
            try
            {
            // Right arrow
            // Moving from BetterWeight to Default Mass
                if (Widgets.ButtonImage(butRect: MainRect.BottomPart(pct: 0.6f).TopPart(pct: 0.1f).RightPart(pct: 0.525f).LeftPart(pct: 0.1f).RightPart(0.5f), tex: TexUI.ArrowTexRight) && leftSelected != null)
                {
                    // Add and remove them from correct lists
                    BetterWeight.listNotToPatch.Add(leftSelected);
                    BetterWeight.listToPatch.Remove(leftSelected);

                    leftSelected = null;
                }
                // Left arrow
                // Moving from Default Mass to BetterWeight
                if (Widgets.ButtonImage(butRect: MainRect.BottomPart(pct: 0.6f).TopPart(pct: 0.1f).RightPart(pct: 0.525f).LeftPart(pct: 0.1f).LeftPart(0.5f), tex: TexUI.ArrowTexLeft) && rightSelected != null)
                {
                    BetterWeight.listToPatch.Add(rightSelected);
                    BetterWeight.listNotToPatch.Remove(rightSelected);

                    rightSelected = null;
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }

            // Save the settings to file


            base.DoSettingsWindowContents(inRect);
        }

        /// <summary>
        /// Override SettingsCategory to show up in the list of settings.
        /// </summary>
        /// <returns>The translated mod name</returns>
        public override string SettingsCategory()
        {
            return "BetterWeight - Patch List";
        }

    }
}