using System.Collections.Generic;
using Verse;
using System.Linq;

namespace ArchieVBetterWeight
{
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