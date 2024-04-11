using System.Collections.Generic;
using Verse;
using System.Linq;

namespace ArchieVBetterWeight
{
    [StaticConstructorOnStartup]
    internal class BetterWeightSettings : ModSettings
    {
        /// <summary>
        /// List of Buildings to patch.
        /// </summary>
        public List<ThingDef> ToPatch { get; set; } = new List<ThingDef>();

        /// <summary>
        /// List of Buildings not to patch.
        /// </summary>
        public List<ThingDef> NotToPatch { get; set; } = new List<ThingDef>();

        /// <summary>
        /// Whether or not to round masses to nearest 5.
        /// </summary>
        public bool RoundToNearest5;

        /// <summary>
        /// Number of decimal place to round values to.
        /// </summary>
        public int NumberOfDPToRoundTo;

        /// <summary>
        /// Percentage multiplier to multiply total mass of buildings by between 0F and 1F.
        /// </summary>
        public float DefaultEfficiency;

        /// <summary>
        /// Enable dev logging.
        /// </summary>
        public bool DevMode;

        /// <summary>
        /// List of things to patch by default.
        /// </summary>
        public List<ThingDef> DefaultToPatch { get; set; } = new List<ThingDef>();

        /// <summary>
        /// List of things not to patch by default.
        /// </summary>
        public List<ThingDef> DefaultNotToPatch { get; set; } = new List<ThingDef>();

        /// <summary>
        /// Make the settings accessable
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            // Reference settings to val in settings with default values
            Scribe_Values.Look(ref RoundToNearest5, "BetterWeight_roundToNearest5", true);
            Scribe_Values.Look(ref NumberOfDPToRoundTo, "BetterWeight_numberOfDPToRoundTo", 0);
            Scribe_Values.Look(ref DefaultEfficiency, "BetterWeight_defaultEfficiency", 65f);

            Scribe_Values.Look(ref DevMode, "BetterWeight_devMode", false);

            // Do it with ToPatch
            // Create list of strings from ToPatch and if that is blank then create blanks list.
            // Then ref to the settings file
            // Then turn list of strings back into list of ThingDefs by getting the ThingDefs from the DefDatabase using their names
            // The default must be blank as the defs haven't been loaded yet to make a default list
            List<string> toPatchStrings = ToPatch?.Select(selector: thing => thing.defName).ToList() ?? new List<string>();
            Scribe_Collections.Look(list: ref toPatchStrings, label: "ToPatch");
            ToPatch = toPatchStrings.Select(selector: DefDatabase<ThingDef>.GetNamedSilentFail).Where(td => td != null).ToList();

            // Same with NotToPatch
            // The default must be blank as the defs haven't been loaded yet to make a default list
            List<string> notToPatchStrings = NotToPatch?.Select(selector: td => td.defName).ToList() ?? new List<string>();
            Scribe_Collections.Look(list: ref notToPatchStrings, label: "NotToPatch");
            NotToPatch = notToPatchStrings.Select(selector: DefDatabase<ThingDef>.GetNamedSilentFail).Where(td => td != null).ToList();

            // Same with DefaultToPatch
            // The default must be blank as the defs haven't been loaded yet to make a default list
            List<string> defaultToPatchStrings = DefaultToPatch?.Select(selector: td => td.defName).ToList() ?? new List<string>();
            Scribe_Collections.Look(list: ref defaultToPatchStrings, label: "DefaultToPatch");
            DefaultToPatch = defaultToPatchStrings.Select(selector: DefDatabase<ThingDef>.GetNamedSilentFail).Where(td => td != null).ToList();

            // Same with DefaultNotToPatch
            // The default must be blank as the defs haven't been loaded yet to make a default list
            List<string> defaultNotToPatchStrings = DefaultNotToPatch?.Select(selector: td => td.defName).ToList() ?? new List<string>();
            Scribe_Collections.Look(list: ref defaultNotToPatchStrings, label: "DefaultNotToPatch");
            DefaultNotToPatch = defaultNotToPatchStrings.Select(selector: DefDatabase<ThingDef>.GetNamedSilentFail).Where(td => td != null).ToList();
        }
    }
}