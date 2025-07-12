using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;
using System.Linq;
using HarmonyLib;
using RimWorld;
using static ArchieVBetterWeight.DevLogger;

namespace ArchieVBetterWeight
{
    class BetterWeight : Mod
    {
        private BetterWeightSettings _settings;

        /// <summary>
        /// The instance of the BetterWeight.
        /// </summary>
        public static BetterWeight Instance { get; set; }

        /// <summary>
        /// Cache for the original XML defined mass values, in case the user wants to unpatch them mid-game.
        /// </summary>
        private static Dictionary<string, float> MassMap { get; set; } =  new Dictionary<string, float>();

        /// <summary>
        /// Cache for the calculated values, including stuffed permutations.
        /// </summary>
        public static Dictionary<string, float> CachedMassMap { get; set; }= new Dictionary<string, float>();

        /// <summary>
        /// Contains all changed defs, theoretically improving performance over recalculating everything each time the mod menu is closed.
        /// </summary>
        private static List<ThingDef> ChangedDefs { get; set; } = new List<ThingDef>();

        /// <summary>
        /// Contains all stuff defs for faster recalculation.
        /// </summary>
        private static List<ThingDef> StuffDefs { get; set; } = new List<ThingDef>();

        /// <summary>
        /// List containing the old settings, to be checked against the new settings after the mod menu is closed.
        /// </summary>
        private static object[] OldSettings { get; set; } = new object[3];

        /// <summary>
        /// The settings of the mod.
        /// </summary>
        internal BetterWeightSettings Settings
        {
            get => _settings ?? (_settings = GetSettings<BetterWeightSettings>());
            set => _settings = value;
        }


        /// <summary>
        /// Gets Instance.settings.devMode.
        /// </summary>
        public static bool DevMode => Instance._settings.DevMode;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="content"></param>
        public BetterWeight(ModContentPack content) : base(content)
        {
            Instance = this;
            Harmony harmony = new Harmony("uk.ArchieV.projects.modding.Rimworld.BetterWeight");
            harmony.PatchAll();
        }

        /// <summary>
        /// Calculates all of the masses.
        /// </summary>
        /// <param name="firstLoad"></param>
        public static void CalculateAllMasses(bool firstLoad)
        {
            Log.Message("BetterWeight: (Re-) Calculating all masses...");
            CachedMassMap.Clear();
            List<ThingDef> buildings = new List<ThingDef>();

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.category == ThingCategory.Building)
                {
                    // Add the original value to the mass dictionary on startup
                    if (firstLoad)
                    {
                        MassMap.Add(def.defName, def.BaseMass);
                    }

                    if (ShouldPatch(def))
                    {
                        buildings.Add(def);
                        continue;
                    }

                    // Stuffed buildings will not default back to their original value without this
                    if (MassMap.ContainsKey(def.defName) && !Mathf.Approximately(def.BaseMass, MassMap[def.defName]))
                    {
                        SetMassValueTo(def, MassMap[def.defName]);
                    }
                }

                if (def.IsStuff)
                {
                    StuffDefs.Add(def);
                }
            }

            // Iterate through all buildings to be patched
            foreach (ThingDef buildingDef in buildings)
            {
                DevMessage($"Iterating through building: {buildingDef.defName}");

                // If it's stuffable, calculate every permutation
                if (buildingDef.MadeFromStuff)
                {
                    foreach (ThingDef stuff in CalculatePermutations(buildingDef))
                    {
                        // Only add the permutation if it doesn't already exist
                        string identifier = buildingDef.defName + stuff.defName;

                        if (!firstLoad)
                        {
                            CachedMassMap.Remove(identifier);
                        }

                        if (!CachedMassMap.ContainsKey(identifier))
                        {
                            CachedMassMap.Add(identifier, RoundMass(CalculateMass(buildingDef, stuff.BaseMass)));
                        }
                    }
                }

                PatchMass(buildingDef);
            }
            Log.Message("BetterWeight: Finished (re-) calculating!");
        }

        /// <summary>
        /// Calculate all permutations for the given building.
        /// </summary>
        /// <param name="buildingDef"></param>
        /// <returns></returns>
        private static IEnumerable<ThingDef> CalculatePermutations(ThingDef buildingDef)
        {
            // Go through every StuffCategory for the building
            foreach (StuffCategoryDef stuffCategoryDef in buildingDef.stuffCategories)
            {
                DevMessage($"Iterating through stuffCategories for: {buildingDef.defName}; Now iterating through stuffCategory: {stuffCategoryDef.defName}");

                foreach (ThingDef stuffDef in StuffDefs)
                {
                    DevMessage($"Iterating through stuffCategories for: {buildingDef.defName}; Now iterating through stuffCategory: {stuffCategoryDef.defName}; Checking stuff: {stuffDef.defName}");
                    if (!stuffDef.stuffProps.categories.Contains(stuffCategoryDef)) continue;
                    DevMessage($"Added: Building: {buildingDef.defName}; StuffCategoryDef: {stuffCategoryDef.defName}; Stuff: {stuffDef.defName}; identifier: {buildingDef.defName + stuffDef.defName}");
                    yield return stuffDef;
                }
            }
        }

        /// <summary>
        /// Updates Mass Value of def to given value if it currently has a mass value.
        /// MinifyEverything sets this so is needed to add mass to non-vanilla minifiable items.
        /// </summary>
        /// <param name="def">The thing whose mass should be updated.</param>
        /// <param name="value">The new mass.</param>
        /// <returns>True if the def was updated.</returns>
        private static bool SetMassValueTo(ThingDef def, float value)
        {
            foreach (StatModifier stat in def.statBases)
            {
                if (stat.stat.label != "mass") continue;
                stat.value = value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Assigns the better mass and creates the respective stat if necessary
        /// </summary>
        /// <param name="def"></param>
        private static void PatchMass(ThingDef def)
        {
            if (SetMassValueTo(def, RoundMass(CalculateMass(def))))
            {
                DevMessage($"BetterWeight: Added: {def.defName}; New weight: {RoundMass(CalculateMass(def))}");
                return;
            }
            
            def.statBases.Add(new StatModifier()
            {
                stat = StatDefOf.Mass,
                value = RoundMass(CalculateMass(def))
            });

            DevMessage($"BetterWeight: Added new mass stat for: {def.defName}");
        }

        public static void RefreshSettings()
        {
            OldSettings[0] = Instance._settings.DefaultEfficiency;
            OldSettings[1] = Instance._settings.NumberOfDPToRoundTo;
            OldSettings[2] = Instance._settings.RoundToNearest5;
        }

        /// <summary>
        /// Compares the old settings against the new ones. Yes, it's pretty primitive, but it works. It also shouldn't reload when dev mode is toggled
        /// </summary>
        /// <returns></returns>
        private static bool SettingsChanged()
        {
            return !(
                OldSettings[0].Equals(Instance.Settings.DefaultEfficiency) &&
                OldSettings[1].Equals(Instance.Settings.NumberOfDPToRoundTo) &&
                OldSettings[2].Equals(Instance.Settings.RoundToNearest5)
                );
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            if (SettingsChanged())
            {
                CalculateAllMasses(false);
                ChangedDefs.Clear();
                RefreshSettings();
                return;
            }
            // Recalculate the buildings that have been changed, but only if none of the other settings were touched
            if (ChangedDefs.Count > 0)
            {
                Log.Message("BetterWeight: Recalculating changed buildings...");
                foreach (ThingDef def in ChangedDefs)
                {
                    DevMessage($"Now recalculating {def.defName}");
                    // Remove/add all permutations if it's made from stuff so the harmony patch works properly
                    if (def.MadeFromStuff)
                    {
                        foreach (ThingDef stuffDef in CalculatePermutations(def))
                        {
                            string identifier = def.defName + stuffDef.defName;
                            if (ShouldPatch(def) && !CachedMassMap.ContainsKey(identifier)) CachedMassMap.Add(identifier, RoundMass(CalculateMass(def, stuffDef.BaseMass)));
                            if (!ShouldPatch(def) && CachedMassMap.ContainsKey(identifier)) CachedMassMap.Remove(identifier);
                        }
                    }

                    if (ShouldPatch(def))
                    {
                        PatchMass(def);
                        continue;
                    }

                    foreach (StatModifier stat in def.statBases)
                    {
                        // Set the value back to the original XML defined value
                        if (stat.stat.label == "mass") stat.value = MassMap[def.defName];
                    }
                }

                Log.Message("BetterWeight: Finished recalculating!");
            }
            ChangedDefs.Clear();
        }

        #region SettingsMenu
        /// ---------------------------------------------------------------------------------------------------------------------
        ///                                             Settings menu
        /// ---------------------------------------------------------------------------------------------------------------------


        // Control the scroll bars and which is currently selected
        // Outside of the func so they're remembered
        private Vector2 _scrollPositionLeft;
        private Vector2 _scrollPositionRight;
        private List<ThingDef> _leftSelected = new List<ThingDef>();
        private List<ThingDef> _rightSelected = new List<ThingDef>();

        /// <summary>
        /// The GUI settings page
        /// </summary>
        /// <param name="inRect"></param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Sort the lists alphabetically
            SortListNotToPatchListToPatch();

            base.DoSettingsWindowContents(inRect: inRect);


            Rect topRect = inRect.TopPart(0.10f);
            Rect mainRect = inRect.BottomPart(0.90f).TopPart(0.75f);

            Widgets.Label(topRect.TopHalf(), "Changed buildings will be automatically hot swapped");

            Rect leftSide = mainRect.LeftPart(0.46f);
            Rect rightSide = mainRect.RightPart(0.46f);


            // ----------------------------------------------------------------------------------------------------------------
            //                                              Dev Options
            // ----------------------------------------------------------------------------------------------------------------
            Rect devModeToggleRect = inRect.RightPart(0.06f).TopPart(0.04f);
            Widgets.CheckboxLabeled(devModeToggleRect, "Dev", ref Instance.Settings.DevMode);

            List<ThingDef> GenerateSelectionWindow(string sideStr, Rect sideRect, List<ThingDef> list, string title, string titleToolTip, ref Vector2 scrollPosition, List<ThingDef> selectedArray)
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
                            Rect rightPartRow = rowRect.RightPartPixels(47 + 25);
                            Widgets.Label(rightPartRow, "BW Default");
                        }

                        // Logic for thingDef clicked
                        if (Widgets.ButtonInvisible(butRect: rowRect))
                        {
                            // Ctrl click lets you select 2+ before moving them
                            if (Event.current.control || Event.current.command)
                            {
                                DevMessage($"Ctrl/Cmd clicked{sideStr}\nBefore:");
                                DevMessage(string.Join(", ", selectedArray));

                                selectedArray.Add(thing);

                                DevMessage("After:");
                                DevMessage(string.Join(", ", selectedArray));
                            }
                            else
                            {
                                // Shift click selects all between last clicked and most recently clicked
                                if (Event.current.shift)
                                {
                                    if (selectedArray.Count > 0)
                                    {
                                        int lastSelectedIndex = list.IndexOf(selectedArray[selectedArray.Count - 1]);
                                        int currentSelectedIndex = list.IndexOf(thing);
                                        selectedArray = currentSelectedIndex > lastSelectedIndex
                                            ? list.GetRange(lastSelectedIndex,
                                                currentSelectedIndex - lastSelectedIndex + 1)
                                            : list.GetRange(currentSelectedIndex,
                                                lastSelectedIndex - currentSelectedIndex);
                                    }
                                    else
                                    {
                                        selectedArray = new List<ThingDef>() { thing };
                                    }
                                }
                                // Normal click clears the currently selected
                                else
                                {
                                    selectedArray = new List<ThingDef>() { thing };
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

            _leftSelected = GenerateSelectionWindow("leftSide", leftSide, Settings.NotToPatch, "Use Default Mass", "All things in this category will not be effected by BetterMass and will instead use their default mass", ref _scrollPositionLeft, _leftSelected);
            _rightSelected = GenerateSelectionWindow("rightSide", rightSide, Settings.ToPatch, "Use BetterWeight", "", ref _scrollPositionRight, _rightSelected);

            #region Centre buttons
            // ----------------------------------------------------------------------------------------------------------------
            //                                                  Central buttons
            // ----------------------------------------------------------------------------------------------------------------
            // Right arrow
            // Moving from NotToPatch to ToPatch (Left side to right side)
            if (Widgets.ButtonImage(
                butRect: mainRect.BottomPart(pct: 0.6f).TopPart(pct: 0.1f).RightPart(pct: 0.525f)
                    .LeftPart(pct: 0.1f).RightPart(0.5f), tex: TexUI.ArrowTexRight) && _leftSelected != null)
            {
                // Add and remove them from correct lists
                foreach (ThingDef thing in _leftSelected)
                {
                    Settings.ToPatch.Add(thing);
                    Settings.NotToPatch.Remove(thing);
                    if (!ChangedDefs.Contains(thing)) ChangedDefs.Add(thing);
                }
                _leftSelected = new List<ThingDef>();
            }
            // Left arrow
            // Moving from ToPatch to NotToPatch (Right side to left side)
            if (Widgets.ButtonImage(
                butRect: mainRect.BottomPart(pct: 0.6f).TopPart(pct: 0.1f).RightPart(pct: 0.525f)
                    .LeftPart(pct: 0.1f).LeftPart(0.5f), tex: TexUI.ArrowTexLeft) && _rightSelected != null)
            {
                foreach (ThingDef thing in _rightSelected)
                {
                    Settings.NotToPatch.Add(thing);
                    Settings.ToPatch.Remove(thing);
                    if (!ChangedDefs.Contains(thing)) ChangedDefs.Add(thing);
                }
                _rightSelected = new List<ThingDef>();
            }

            // Reset button
            Rect resetButton = mainRect.BottomPart(pct: 0.7f).TopPart(pct: 0.1f).RightPart(pct: 0.525f).LeftPart(0.1f);
            if (Widgets.ButtonText(resetButton, "Reset"))
            {
                SetListsToDefault();
                CalculateAllMasses(false);
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

            // numberOfDpToRoundTo. Int with min and max val
            Rect numDecimalsRect = otherSettingsLeft.TopHalf();
            Rect numDecimalsLabel = numDecimalsRect.LeftHalf();
            Rect numDecimalsSlider = numDecimalsRect.RightHalf().BottomPart(0.7f);

            Widgets.Label(numDecimalsLabel, "Number of Decimal Places to Round Masses to");
            Instance.Settings.NumberOfDPToRoundTo = (int)Math.Round(Widgets.HorizontalSlider(
                numDecimalsSlider,
                Instance.Settings.NumberOfDPToRoundTo,
                0f,
                2f,
                false,
                null,
                "0",
                "2",
                -1),
                MidpointRounding.AwayFromZero);
            
            // Round to nearest 5
            Rect nearest5Rect = otherSettingsLeft.BottomHalf();
            Widgets.CheckboxLabeled(nearest5Rect, "Round to nearest 5", ref Instance.Settings.RoundToNearest5);

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

            Instance.Settings.DefaultEfficiency = Widgets.HorizontalSlider(efficiencySlider,
                Instance.Settings.DefaultEfficiency,
                5, 
                300,
                false, 
                Instance.Settings.DefaultEfficiency.ToString(),
                "5",
                "300",
                5);

            // Reset extra settings
            Rect resetExtraButton = otherSettingsRight.BottomHalf().BottomPart(0.5f).RightPart(0.8f);

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
        /// <param name="initMass">Initial mass before rounding</param>
        /// <returns></returns>
        private static float RoundMass(float initMass)
        {
            float newMass;

            if (Instance.Settings.RoundToNearest5)
            {
                newMass = (float)Math.Round(initMass * 5f, Instance.Settings.NumberOfDPToRoundTo, MidpointRounding.AwayFromZero) / 5f;
                newMass = (float)Math.Round(newMass, Instance.Settings.NumberOfDPToRoundTo);
            }
            else
            {
                newMass = (float)Math.Round(initMass, Instance.Settings.NumberOfDPToRoundTo, MidpointRounding.AwayFromZero);
            }

            return newMass;
        }

        /// <summary>
        /// Calculate mass recursively. NO ROUNDING IS DONE HERE
        /// </summary>
        /// <param name="thing">The thing to have its new value calculated</param>
        /// <param name="stuffMass">The weight of the stugg</param>
        /// <returns>The (new) mass of the passed value</returns>
        private static float CalculateMass(ThingDef thing, float stuffMass = 1f)
        {
            DevWarning("Started CalculateMass");
            DevMessage($"Now calculating mass for: {thing.defName} using stuffMass: {stuffMass}");
            float mass = 0.00f;

            if (thing.MadeFromStuff)
            {
                DevMessage($"{thing.defName} is made out of stuff, adding extra weight...");
                mass += stuffMass * thing.costStuffCount;
            }

            if (thing.costList.NullOrEmpty())
            {
                DevMessage($"Could not find any additional ingredients for {thing.defName}");
                return mass == 0F ? 1F : mass * Instance._settings.DefaultEfficiency / 100;
            }

            foreach (ThingDefCountClass part in thing.costList)
            {
                mass += part.thingDef.BaseMass * part.count;
            }

            DevMessage($"Calculated mass for: {thing.defName} using stuffMass: {stuffMass} is {mass * Instance._settings.DefaultEfficiency / 100}");
            return mass == 0F ? 1F : mass * Instance._settings.DefaultEfficiency * 0.01F;
        }


        /// <summary>
        /// If passed value is in listToPatch it should be patched with new weight.
        /// </summary>
        /// <param name="thing">The thing to be checked if it needs patching</param>
        /// <returns>true if it should be patched</returns>
        private static bool ShouldPatch(ThingDef thing)
        {
            return Instance.Settings.ToPatch.Contains(thing);
        }
        #endregion
        
        #region SettingsFunctions
        /// ---------------------------------------------------------------------------------------------------------------------
        ///                                             Settings functions
        /// ---------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Set ToPatch and NotToPatch to their default lists from the lists that are generated at game start
        /// </summary>
        private static void SetListsToDefault()
        {
            Instance.Settings.ToPatch = Instance.Settings.DefaultToPatch;
            Instance.Settings.NotToPatch = Instance.Settings.DefaultNotToPatch;
        }

        /// <summary>
        /// Generate DefaultToPatch and DefaultNotToPatch for the settings menu
        /// </summary>
        private static void GenerateDefaultLists()
        {
            Instance.Settings.DefaultToPatch = GenerateDefaultListToPatch();
            Instance.Settings.DefaultNotToPatch = GenerateDefaultListToNotPatch();
        }

        /// <summary>
        /// Sort ToPatch and NotToPatch alphabetically
        /// </summary>
        private static void SortListNotToPatchListToPatch()
        {
            // Order the lists by name if they have any stuff in the lists
            if (!Instance.Settings.NotToPatch.NullOrEmpty())
            {
                Instance.Settings.NotToPatch = Instance.Settings.NotToPatch.OrderBy(keySelector: kS => kS.defName).ToList();
            }

            if (!Instance.Settings.ToPatch.NullOrEmpty())
            {
                Instance.Settings.ToPatch = Instance.Settings.ToPatch.OrderBy(keySelector: kS => kS.defName).ToList();
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
        private static void ResetOtherSettings()
        {
            Instance.Settings.DefaultEfficiency = 65f;
            Instance.Settings.RoundToNearest5 = true;
            Instance.Settings.NumberOfDPToRoundTo = 0;
        }

        /// <summary>
        /// If settings are null for some reason (First install the lists will be blank) then set them
        /// </summary>
        public static void SetDefaultSettingsIfNeeded()
        {
            DevWarning("SetDefaultSettingsIfNeeded");

            // Generate the default lists every time to make sure they are correct and save them for the settings menu
            GenerateDefaultLists();

            // If just one is blank that could just be config. If both are blank there's an issue
            if (Instance.Settings.NotToPatch.NullOrEmpty() && Instance.Settings.ToPatch.NullOrEmpty())
            {
                SetListsToDefault();
            }

            if (Instance.Settings.DefaultEfficiency.Equals(0))
            {
                Instance.Settings.DefaultEfficiency = 65f;
            }

            if (Instance.Settings.RoundToNearest5.Equals(null))
            {
                Instance.Settings.RoundToNearest5 = true;
            }

            if (Instance.Settings.NumberOfDPToRoundTo.Equals(null))
            {
                Instance.Settings.NumberOfDPToRoundTo = 0;
            }
        }

        /// <summary>
        /// Generate list of ThingDefs that are, by default, to be patched.
        /// Category = Building && baseMass = 1 && (has either costList or costStuffCount)
        /// </summary>
        /// <returns>List of thingDefs that should have a new mass calculated by default</returns>
        private static List<ThingDef> GenerateDefaultListToPatch()
        {
            List<ThingDef> things = DefDatabase<ThingDef>.AllDefsListForReading;

            return things.Where(thing =>
                    thing.category == ThingCategory.Building &&
                    IsStrangeMass(thing.BaseMass) &&
                    (thing.costList != null || thing.costStuffCount != 0))
                .ToList();
        }
        
        /// <summary>
        /// Generates list of ThingDefs that are, by default, not to be patched.
        /// </summary>
        /// <returns>List of thingDefs that should not have a new mass calculated by default</returns>
        private static List<ThingDef> GenerateDefaultListToNotPatch()
        {
            List<ThingDef> things = DefDatabase<ThingDef>.AllDefsListForReading;

            return things.Where(thing =>
                    thing.category == ThingCategory.Building &&
                    !IsStrangeMass(thing.BaseMass) &&
                    (thing.costList != null || thing.costStuffCount != 0))
                .ToList();
        }

        /// <summary>
        /// If the given mass does not seem like it was added as part of a building's XML.
        /// Strange if mass is 0/1, has more than 2 decimal places, final digit is neither 0 nor 5.
        /// </summary>
        /// <param name="mass"></param>
        /// <returns></returns>
        private static bool IsStrangeMass(float mass)
        {
            // In 1.5 a change was made that means mass may be calculated as something other than 1 when one isn't set
            // Before 1.5 all non-set masses would just default to 1 but now they always look strange so can be worked
            // out with this method instead.
            int decimalPlaces = GetDecimalPlaces(mass);
            int finalDigit = (int)(Math.Pow(10, decimalPlaces) * mass) % 10;

            return Mathf.Approximately(mass, 0) ||
                   Mathf.Approximately(mass, 1) ||
                   decimalPlaces > 2 ||
                   (finalDigit != 0 && finalDigit != 5); 
        }
        
        private static int GetDecimalPlaces(float num)
        {
            num = Math.Abs(num); // Make sure it is positive
            num -= (int)num;     // Remove the integer part of the number
            int decimalPlaces = 0;
            while (num > 0)
            {
                decimalPlaces++;
                num *= 10;
                num -= (int)num;
            }
            return decimalPlaces;
        }
        #endregion
    }
}