using System;
using System.Collections.Generic;
using Verse;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using System.Linq;
using System.ComponentModel;
using System.Diagnostics;

namespace ArchieVBetterWeight
{
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
            // NOTE
            // Do not put any logging in here. It runs too many times and will clog the log

            float stuffMass = 0;

            // Quick check to make sure thing isn't null
            if (req.Thing == null || req.Thing.def == null || req.StatBases == null)
            {
                return true;
            }

            if (req.Thing.def.defName.ToUpper().Contains("DOOR"))
            {
                Log.Error("DOOR");
                stuffMass = req.Thing.Stuff.GetStatValueAbstract(StatDefOf.Mass);
                Log.Message($"{req.Thing}'s stuff ({req.Thing.Stuff.defName}) has a mass {stuffMass} FIRST ONE", true);
                Log.Message($"Should patch door: {BetterWeight.ShouldPatch(req.Thing.def)}");
                for (var index = 0; index < req.StatBases.Count; index++)
                {
                    Log.Message(req.StatBases[index].ToString());
                }

                //DOOR JUST DOESNT REQUEST WEIGHT????
                StackTrace stackTrace = new StackTrace();
                StackFrame[] stackFrames = stackTrace.GetFrames();

                var list = new List<string>();
                list.AddRange(stackFrames.Select(frame => frame.GetMethod().Name));
                list.Remove(stackFrames[0].GetMethod().Name);
                Log.Error("Traceback door");
                Log.Error(string.Join(", ", list));
            }

            if (req.Thing.Stuff != null)
            {
                stuffMass = req.Thing.Stuff.GetStatValueAbstract(StatDefOf.Mass);
                if (BetterWeight.instance.Settings.devMode)
                {
                    Log.Message($"{req.Thing}'s stuff ({req.Thing.Stuff.defName}) has a mass {stuffMass} SECOND ONE", true);
                }
            }

            if (BetterWeight.ShouldPatch(req.Thing.def))
            {
                bool needsMass = true;
                for (var index = 0; index < req.StatBases.Count; index++) //iterate through all stats in request
                {
                    var stat = req.StatBases[index]; //get current stat
                    if (stat.stat.label == "mass") //check if it is the mass
                    {
                        var new_mass = BetterWeight.RoundMass(BetterWeight.CalculateMass(req.Thing.def, stuffMass));
                        if (stat.value != 0 && stat.value != 1)
                        {
                            req.StatBases[index].value = new_mass; //set mass of item here    
                        }

                        needsMass = false;

                        StackTrace stackTrace = new StackTrace();
                        StackFrame[] stackFrames = stackTrace.GetFrames();

                        var list = new List<string>();
                        list.AddRange(stackFrames.Select(frame => frame.GetMethod().Name));
                        list.Remove(stackFrames[0].GetMethod().Name);
                        Log.Error("Traceback for mass");
                        Log.Error(string.Join(", ", list));
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
                        value = BetterWeight.CalculateMass(req.Thing.def, stuffMass)
                    };

                    req.StatBases.Add(statModifier);
                }
            }

            // Returns true so function runs with modifed StatReq
            return true; 
        }
    }

    [HarmonyPatch(typeof(Building_Door))]
    [HarmonyPatch((".ctor"))]
    static class DoorMass_Patch
    {
        static void Postfix(Building_Door __instance)
        {
            __instance.def.BaseMass = BetterWeight.RoundMass(BetterWeight.CalculateMass(__instance.def.BaseMass, __instance.Stuff.BaseMass));
        }
    }

    // Exists just to use this constructor
    [StaticConstructorOnStartup]
    class StaticClass
    {
        static StaticClass()
        {
            if (BetterWeight.instance.Settings.devMode)
            { Log.Warning("BetterWeight all buildings:"); LogAllBuildings(); }

            BetterWeight.SetDefaultSettingsIfNeeded();
        }

        public static void LogAllBuildings()
        {
            List<ThingDef> things = DefDatabase<ThingDef>.AllDefsListForReading;
            List<ThingDef> buildings = new List<ThingDef>();

            foreach (ThingDef thing in things)
            {
                if (thing.category == ThingCategory.Building && !thing.defName.Contains("Frame"))
                {
                    buildings.Add(thing);
                }
            }

            Log.Message(string.Join("\n", buildings), true);
        }
    }
    
    class BetterWeight : Mod
    {
        public static BetterWeight instance;
        public BetterWeightSettings settings;
        private List<ThingDef> alreadyLogged;

        // NOTE
        // This is "Settings" not "settings". ALWAYS USE THIS ONE
        internal BetterWeightSettings Settings
        {
            get => settings ?? (settings = GetSettings<BetterWeightSettings>());
            set => settings = value;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="content"></param>
        public BetterWeight(ModContentPack content) : base(content)
        {
            try
            {
                Log.Message(DateTime.Now.ToString("HH:mm:ss tt") + " Loading BetterWeight...");

                //Log.Error(String.Join(",", CostListCalculator.CostListAdjusted(thing)));
                instance = this;
                alreadyLogged = new List<ThingDef>();

                Harmony harmony = new Harmony("uk.ArchieV.projects.modding.Rimworld.BetterWeight");
                harmony.PatchAll();

                Log.Message(DateTime.Now.ToString("HH:mm:ss tt") + " Finished loading BetterWeight");
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                Log.Error("Failed to load BetterWeight.");
                Log.Error("Please leave a bug report at https://github.com/ArchieV1/BetterWeight");
            }

        }

        #region SettingsMenu
        /// ---------------------------------------------------------------------------------------------------------------------
        ///                                             Settings menu
        /// ---------------------------------------------------------------------------------------------------------------------


        // Control the scroll bars and which is currently selected
        // Outside of the func so they're remembered
        private Vector2 ScrollPositionLeft;
        private Vector2 ScrollPositionRight;
        private List<ThingDef> leftSelected = new List<ThingDef>();
        private List<ThingDef> rightSelected = new List<ThingDef>();

        /// <summary>
        /// The GUI settings page
        /// </summary>
        /// <param name="inRect"></param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Sort the lists alphabetically
            SortlistNotToPatchlistToPatch();

            base.DoSettingsWindowContents(inRect: inRect);


            Rect topRect = inRect.TopPart(0.10f);
            Rect MainRect = inRect.BottomPart(0.90f).TopPart(0.75f);

            Widgets.Label(topRect.TopHalf(), "If you disable an object from using BetterWeight you must restart your game for changes to take effect");

            Rect leftSide = MainRect.LeftPart(0.46f);
            Rect rightSide = MainRect.RightPart(0.46f);


            // ----------------------------------------------------------------------------------------------------------------
            //                                              Dev Options
            // ----------------------------------------------------------------------------------------------------------------
            Rect devModeToggleRect = inRect.RightPart(0.06f).TopPart(0.04f);
            Widgets.CheckboxLabeled(devModeToggleRect, "Dev", ref instance.Settings.devMode);

            List<ThingDef> generateSelectionWindow(String sideStr, Rect sideRect, List<ThingDef> list, String title, String titleToolTip, ref Vector2 scrollPosition, List<ThingDef> selectedArray)
            {
                float num = 0f;

                Rect viewRect = new Rect(x: 0f, y: 0f, width: sideRect.width - 30, height: list.Count * 30f);

                Rect titleRect = new Rect(x: sideRect.xMin, y: sideRect.yMin - 30, width: viewRect.width - 10, height: 30);
                Widgets.Label(titleRect, title);
                if (Mouse.IsOver(titleRect))
                {
                    TooltipHandler.TipRegion(titleRect, titleToolTip);
                }

                Widgets.BeginScrollView(outRect: sideRect, scrollPosition: ref scrollPosition, viewRect: viewRect);
                if (Settings.ToPatch != null)
                {
                    foreach (ThingDef thing in list)
                    {
                        Rect rowRect = new Rect(x: 5, y: num, width: viewRect.width - 10, height: 30);
                        Widgets.DrawHighlightIfMouseover(rect: rowRect);

                        // The name and icon of the thing
                        Widgets.DefLabelWithIcon(rowRect, thing);

                        // Say if this "thing" is by default patched or not
                        if (Settings.DefaultToPatch.Contains(thing))
                        {
                            Rect rightPartRow = rowRect.RightPartPixels(47+25);
                            Widgets.Label(rightPartRow, "Default");
                        }

                        // Logic for thingDef clicked
                        if (Widgets.ButtonInvisible(butRect: rowRect))
                        {
                            // Ctrl click lets you select 2+ before moving them
                            if (Event.current.control || Event.current.command)
                            {
                                if (Settings.devMode)
                                {
                                    Log.Message("Ctrl/Cmd clicked" + sideStr + "\nBefore:");
                                    Log.Message(String.Join(", ", selectedArray));
                                }

                                selectedArray.Add(thing);

                                if (Settings.devMode)
                                {
                                    Log.Message("After:");
                                    Log.Message(String.Join(", ", selectedArray));
                                }
                            }
                            else
                            {
                                // Shift click selects all between last clicked and most recently clicked
                                if (Event.current.shift)
                                {
                                    if (selectedArray.Count > 0)
                                    {
                                        int lastSelectedIndex =
                                            list.IndexOf(selectedArray[selectedArray.Count - 1]);
                                        int currentSelectedIndex = list.IndexOf(thing);
                                        selectedArray = currentSelectedIndex > lastSelectedIndex
                                            ? list.GetRange(lastSelectedIndex,
                                                currentSelectedIndex - lastSelectedIndex + 1)
                                            : list.GetRange(currentSelectedIndex,
                                                lastSelectedIndex - currentSelectedIndex);
                                    }
                                    else
                                    {
                                        selectedArray = new List<ThingDef>() {thing};
                                    }
                                }
                                // Normal click clears the currently selected
                                else
                                {
                                    selectedArray = new List<ThingDef>() {thing};
                                }
                            }
                        }
                        if (selectedArray.Contains(thing))
                        {
                            Widgets.DrawHighlightSelected(rowRect);
                        }

                        num += 30f;
                    }
                }
                Widgets.EndScrollView();

                return selectedArray;
            }
            
            leftSelected = generateSelectionWindow("leftSide", leftSide, Settings.NotToPatch, "Use Default Mass", "All things in this category will not be effected by BetterMass and will instead use their default mass", ref ScrollPositionLeft, leftSelected);
            rightSelected = generateSelectionWindow("rightSide", rightSide, Settings.ToPatch, "Use BetterWeight", "", ref ScrollPositionRight, rightSelected);

            #region Centre buttons
            // ----------------------------------------------------------------------------------------------------------------
            //                                                  Central buttons
            // ----------------------------------------------------------------------------------------------------------------
            // Right arrow
            // Moving from NotToPatch to ToPatch (Left side to right side)
            if (Widgets.ButtonImage(
                butRect: MainRect.BottomPart(pct: 0.6f).TopPart(pct: 0.1f).RightPart(pct: 0.525f)
                    .LeftPart(pct: 0.1f).RightPart(0.5f), tex: TexUI.ArrowTexRight) && leftSelected != null)
            {
                // Add and remove them from correct lists
                foreach (ThingDef thing in leftSelected)
                {
                    Settings.ToPatch.Add(thing);
                    Settings.NotToPatch.Remove(thing);
                }

                SortlistNotToPatchlistToPatch();
                leftSelected = new List<ThingDef>();
            }
            // Left arrow
            // Moving from ToPatch to NotToPatch (Right side to left side)
            if (Widgets.ButtonImage(
                butRect: MainRect.BottomPart(pct: 0.6f).TopPart(pct: 0.1f).RightPart(pct: 0.525f)
                    .LeftPart(pct: 0.1f).LeftPart(0.5f), tex: TexUI.ArrowTexLeft) && rightSelected != null)
            {
                foreach (ThingDef thing in rightSelected)
                {
                    Settings.NotToPatch.Add(thing);
                    Settings.ToPatch.Remove(thing);
                }
                rightSelected = new List<ThingDef>();

                SortlistNotToPatchlistToPatch();
            }

            // Reset button
            Rect resetButton = MainRect.BottomPart(pct: 0.7f).TopPart(pct: 0.1f).RightPart(pct: 0.525f).LeftPart(0.1f);
            if (Widgets.ButtonText(resetButton, "Reset"))
            {
                SetListsToDefault();
                base.DoSettingsWindowContents(inRect);
            }
            if (Mouse.IsOver(resetButton))
            {
                TooltipHandler.TipRegion(resetButton, "Reset both lists to default");
            }
            #endregion

            #region Bottom buttons
            // ----------------------------------------------------------------------------------------------------------------
            //                                              Bottom Options
            // ----------------------------------------------------------------------------------------------------------------
            Rect otherSettingsRect = inRect.BottomPart(0.90f).BottomPart(0.20f);

            // Left side
            Rect otherSettingsLeft = otherSettingsRect.LeftPart(0.45f);

            // numberOfDPToRoundTo. Int with min and max val
            Rect numDPRect = otherSettingsLeft.TopHalf();
            Rect numDPLabel = numDPRect.LeftHalf();
            Rect numDPSlider = numDPRect.RightHalf().BottomPart(0.7f);

            Widgets.Label(numDPLabel, "Number of Decimal Places to Round Masses to");
            instance.Settings.numberOfDPToRoundTo = (int)Math.Round(Widgets.HorizontalSlider(numDPSlider, instance.Settings.numberOfDPToRoundTo, 0f, 2f, false, null, "0", "2", -1), MidpointRounding.AwayFromZero);

            // roundToNearest5. Bool
            Rect Nearest5Rect = otherSettingsLeft.BottomHalf();
            Rect Nearest5Label = Nearest5Rect.LeftHalf();
            Rect Nearest5Toggle = Nearest5Rect.RightHalf();

            Widgets.CheckboxLabeled(Nearest5Rect, "Round to nearest 5", ref instance.Settings.roundToNearest5);

            // Right side
            Rect otherSettingsRight = otherSettingsRect.RightPart(0.45f);

            // defaultEfficiency. Float with min and max val
            Rect efficiencyRect = otherSettingsRight.TopHalf();
            Rect efficiencyLabel = efficiencyRect.LeftHalf().BottomPart(0.7f);
            Rect efficiencySlider = efficiencyRect.RightHalf();

            Widgets.Label(efficiencyLabel, "Efficiency");
            if (Mouse.IsOver(efficiencyLabel))
            {
                TooltipHandler.TipRegion(efficiencyLabel, "A higher number means a higher BetterWeight\n" +
                    "BetterWeight = Sum of all components × Efficiency");
            }

            instance.Settings.defaultEfficiency = Widgets.HorizontalSlider(efficiencySlider, instance.Settings.defaultEfficiency, 5, 300, false, instance.Settings.defaultEfficiency.ToString(), "5", "300", 5);

            // Reset extra settings
            Rect resetExtraButton = otherSettingsRight.BottomHalf().BottomPart(0.5f).RightPart(0.8f);
            //Widgets.ButtonText(resetExtraButton, "Reset other settings (NOT LISTS)");
            //Widgets.DrawHighlightIfMouseover(resetExtraButton);

            if (Mouse.IsOver(resetExtraButton))
            {
                TooltipHandler.TipRegion(resetExtraButton, "Reset settings that are not the lists to their default values");
            }
            if (Widgets.ButtonText(resetExtraButton, "Reset other settings"))
            {
                ResetOtherSettings();
            }
            #endregion
            
            base.DoSettingsWindowContents(inRect);
        }

        /// <summary>
        /// Override SettingsCategory to show up in the list of settings.
        /// </summary>
        /// <returns>The (translated) mod name.</returns>
        public override string SettingsCategory()
        {
            return "BetterWeight";
        }

        #endregion

        #region PatchFunctions
        /// ---------------------------------------------------------------------------------------------------------------------
        ///                                             Patch functions
        /// ---------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Round the mass based on the settings above
        /// </summary>
        /// <param name="initMass"></param>
        /// <returns></returns>
        public static float RoundMass(float initMass)
        {
            float newMass;

            if (instance.Settings.roundToNearest5)
            {
                newMass = (float) Math.Round(initMass * 5f, instance.Settings.numberOfDPToRoundTo, MidpointRounding.AwayFromZero) / 5f;
                newMass = (float) Math.Round(newMass, instance.Settings.numberOfDPToRoundTo);
            }
            else
            {
                newMass = (float) Math.Round(initMass, instance.Settings.numberOfDPToRoundTo, MidpointRounding.AwayFromZero);
            }

            if(newMass == 0f)
            {
                newMass = 0.5f;
            }

            return newMass;
        }

        /// <summary>
        /// Calculate mass recusively. NO ROUNDING IS DONE HERE
        /// </summary>
        /// <param name="thing">The thing to have its new value calculated</param>
        /// <returns>The (new) mass of the passed value</returns>
        public static float CalculateMass(ThingDef thing, float stuffMass)
        {
            float mass = 0.00f;
            if (thing.defName.ToUpper() == "DOOR")
            {
                Log.Error($"DOOR: costStuffCount: {thing.costStuffCount} * {stuffMass}");
            }

                if (instance.Settings.devMode &&
                !instance.alreadyLogged.Contains(thing) &&
                thing.costList == null && 
                thing.costStuffCount == 0)
            {
                Log.Error($"{thing} has no costList AND no costStuffCount", true);
            }

            try
            {
                if (thing.costList != null)
                {
                    foreach (ThingDefCountClass part in thing.costList)
                    {
                        mass += part.thingDef.BaseMass * part.count * instance.Settings.defaultEfficiency / 100f;
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
                    if (stuffMass == 0)
                    {
                        if (instance.Settings.devMode)
                        {
                            Log.Error($"stuffMass is 0 for {thing}'s\nDefaulting to 1", true);
                        }
                        stuffMass = 1;
                    }

                    mass += thing.costStuffCount * stuffMass * instance.Settings.defaultEfficiency / 100f;
                    Log.Message($"{thing}: {thing.costStuffCount} * {stuffMass} * {instance.Settings.defaultEfficiency} / 100f = {mass}");
                }
                else
                {
                    Log.Message($"{thing.defName}'s costStuccCount = {thing.costStuffCount}");
                }
            }
            catch (Exception e)
            {
                Log.Message(e.ToString());
            }

            if (instance.Settings.devMode && !instance.alreadyLogged.Contains(thing))
            {
                Log.Message($"{thing.defName}'s (pre-rounded) mass = {mass}", true);
            }

            instance.alreadyLogged.Add(thing);
            return mass;
        }

        /// <summary>
        /// If passed value is in listToPatch it should be patched with new weight.
        /// </summary>
        /// <param name="thing">The thing to be checked if it needs patching</param>
        /// <returns>true if it should be patched</returns>
        public static bool ShouldPatch(ThingDef thing)
        {
            if (instance.Settings.ToPatch.Contains(thing)) { return true; }
            else { return false; }
        }
        #endregion

        #region SettingsFunctions
        /// ---------------------------------------------------------------------------------------------------------------------
        ///                                             Settings functions
        /// ---------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Set ToPatch and NotToPatch to their default lists from the lists that are generated at game start
        /// </summary>
        public static void SetListsToDefault()
        {
            instance.Settings.ToPatch = instance.Settings.DefaultToPatch;
            instance.Settings.NotToPatch = instance.Settings.DefaultNotToPatch;
        }

        /// <summary>
        /// Generate DefaultToPatch and DefaultNotToPatch for the settings menu
        /// </summary>
        public static void GenerateDefaultLists()
        {
            instance.Settings.DefaultToPatch = GenerateDefaultListToPatch();
            instance.Settings.DefaultNotToPatch = GenerateDefaultListToNotPatch();
        }

        /// <summary>
        /// Sort ToPatch and NotToPatch alphabetically
        /// </summary>
        public static void SortlistNotToPatchlistToPatch()
        {
            // Order the lists by name if they have any stuff in the lists
            if (!instance.Settings.NotToPatch.NullOrEmpty())
            {
                instance.Settings.NotToPatch = instance.Settings.NotToPatch.OrderBy(keySelector: kS => kS.defName).ToList();
            }

            if (!instance.Settings.ToPatch.NullOrEmpty())
            {
                instance.Settings.ToPatch = instance.Settings.ToPatch.OrderBy(keySelector: kS => kS.defName).ToList();
            }
        }
        
        /// <summary>
        /// Reset all settings to default
        /// </summary>
        public static void ResetSettings()
        {
            ResetOtherSettings();
            SetListsToDefault();
        }

        /// <summary>
        /// Reset default defaultEfficiency, roundToNearest5 and numberOfDPToRoundTo
        /// </summary>
        public static void ResetOtherSettings()
        {
            instance.Settings.defaultEfficiency = 65f;
            instance.Settings.roundToNearest5 = true;
            instance.Settings.numberOfDPToRoundTo = 0;
        }

        /// <summary>
        /// If settings are null for some reason (First install the lists will be blank) then set them
        /// </summary>
        public static void SetDefaultSettingsIfNeeded()
        {
            if (instance.Settings.devMode)
            { Log.Warning("SetDefaultSettingsIfNeeded"); }
            
            // Generate the default lists every time to make sure they are correct and save them for the settings menu
            GenerateDefaultLists();

            // If just one is blank that could just be config. If both are blank there's an issue
            if (instance.Settings.NotToPatch.NullOrEmpty() && instance.Settings.ToPatch.NullOrEmpty())
            {
                SetListsToDefault();
            }

            if (instance.Settings.defaultEfficiency.Equals(0))
            {
                instance.Settings.defaultEfficiency = 65f;
            }

            if (instance.Settings.roundToNearest5.Equals(null))
            {
                instance.Settings.roundToNearest5 = true;
            }

            if (instance.Settings.numberOfDPToRoundTo.Equals(null))
            {
                instance.Settings.numberOfDPToRoundTo = 0;
            }
        }

        /// <summary>
        /// Generate list of ThingDefs that are, by default, to be patched.
        /// Category = Building && baseMass = 1 && (has either costList or costStuffCount)
        /// </summary>
        /// <returns>List of thingDefs that should have a new mass calculated by default</returns>
        public static List<ThingDef> GenerateDefaultListToPatch()
        {
            List<ThingDef> things = DefDatabase<ThingDef>.AllDefsListForReading;
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

        /// <summary>
        /// Generates list of ThingDefs that are, by default, not to be patched.
        /// </summary>
        /// <returns>List of thingDefs that should not have a new mass calculated by default</returns>
        public static List<ThingDef> GenerateDefaultListToNotPatch()
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
        #endregion
    }

    [StaticConstructorOnStartup]
    internal class BetterWeightSettings : ModSettings
    {
        /// Create settings

        public List<ThingDef> ToPatch = new List<ThingDef>();
        public List<ThingDef> NotToPatch = new List<ThingDef>();

        public bool roundToNearest5;
        public int numberOfDPToRoundTo;
        public float defaultEfficiency;

        // To tag the default options in the settings menu
        // and the "reset" button works in the settings menu
        public List<ThingDef> DefaultToPatch = new List<ThingDef>();
        public List<ThingDef> DefaultNotToPatch = new List<ThingDef>();

        // Enable dev options
        public bool devMode;

        /// <summary>
        /// Make the settings accessable
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            // Reference settings to val in settings with default values
            Scribe_Values.Look(ref roundToNearest5, "BetterWeight_roundToNearest5", true);
            Scribe_Values.Look(ref numberOfDPToRoundTo, "BetterWeight_numberOfDPToRoundTo", 0);
            Scribe_Values.Look(ref defaultEfficiency, "BetterWeight_defaultEfficiency", 65f);

            Scribe_Values.Look(ref devMode, "BetterWeight_devMode", false);

            // Do it with ToPatch
            // Create list of strings from ToPatch and if that is blank then create blanks list.
            // Then ref to the settings file
            // Then turn list of strings back into list of ThingDefs by getting the ThingDefs from the DefDatabase using their names
            // The default must be blank as the defs haven't been loaded yet to make a default list
            List<string> list1 = ToPatch?.Select(selector: thing => thing.defName).ToList() ?? new List<string>();
            Scribe_Collections.Look(list: ref list1, label: "ToPatch");
            ToPatch = list1.Select(selector: DefDatabase<ThingDef>.GetNamedSilentFail).Where(predicate: td => td != null).ToList();

            // Same with NotToPatch
            // The default must be blank as the defs haven't been loaded yet to make a default list
            List<string> list2 = NotToPatch?.Select(selector: td => td.defName).ToList() ?? new List<string>();
            Scribe_Collections.Look(list: ref list2, label: "NotToPatch");
            NotToPatch = list2.Select(selector: DefDatabase<ThingDef>.GetNamedSilentFail).Where(predicate: td => td != null).ToList();

            // Same with DefaultToPatch
            // The default must be blank as the defs haven't been loaded yet to make a default list
            List<string> list3 = DefaultToPatch?.Select(selector: td => td.defName).ToList() ?? new List<string>();
            Scribe_Collections.Look(list: ref list3, label: "DefaultToPatch");
            DefaultToPatch = list3.Select(selector: DefDatabase<ThingDef>.GetNamedSilentFail).Where(predicate: td => td != null).ToList();

            // Same with DefaultNotToPatch
            // The default must be blank as the defs haven't been loaded yet to make a default list
            List<string> list4 = DefaultNotToPatch?.Select(selector: td => td.defName).ToList() ?? new List<string>();
            Scribe_Collections.Look(list: ref list4, label: "DefaultNotToPatch");
            DefaultNotToPatch = list4.Select(selector: DefDatabase<ThingDef>.GetNamedSilentFail).Where(predicate: td => td != null).ToList();
        }
    }
}