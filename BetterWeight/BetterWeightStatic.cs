using System;
using Verse;

namespace ArchieVBetterWeight
{
    /// <summary>
    /// Exists just to use this constructor annotation.
    /// </summary>
    [StaticConstructorOnStartup]
    class BetterWeightStatic
    {
        static BetterWeightStatic()
        {
            if (BetterWeight.Instance.Settings.DevMode)
            {
                Log.Warning("StaticClass");
                LogAllBuildings();
            }

            BetterWeight.SetDefaultSettingsIfNeeded();

            try
            {
                Log.Message($"{DateTime.Now:HH:mm:ss tt} Loading BetterWeight...");
                BetterWeight.RefreshSettings();
                BetterWeight.CalculateAllMasses(true);
                Log.Message($"{DateTime.Now:HH:mm:ss tt} Finished loading BetterWeight");
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                Log.Error("Failed to load BetterWeight.");
                Log.Error("Please leave a bug report at https://github.com/ArchieV1/BetterWeight");
            }
        }

        /// <summary>
        /// Log all buildings.
        /// </summary>
        private static void LogAllBuildings()
        {
            for (var i = 0; i < DefDatabase<ThingDef>.AllDefsListForReading.Count; i++)
            {
                var thing = DefDatabase<ThingDef>.AllDefsListForReading[i];
                if (thing.category == ThingCategory.Building && !thing.defName.Contains("Frame"))
                {
                    Log.Message(thing.defName);
                }
            }
        }
    }
}