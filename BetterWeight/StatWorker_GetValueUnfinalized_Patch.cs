using Verse;
using HarmonyLib;
using RimWorld;

namespace ArchieVBetterWeight
{
    [HarmonyPatch(typeof(StatWorker))]
    [HarmonyPatch(nameof(StatWorker.GetValueUnfinalized))]
    static class StatWorker_GetValueUnfinalized_Patch
    {
        /// <summary>
        /// Runs before GetValueUnfinalized
        /// With null check, only assigns a modified value if its permutation exists
        /// </summary>
        /// <param name="__result"></param>
        /// <param name="req"></param>
        /// <param name="applyPostProcess"></param>
        /// <returns></returns>
        static bool Prefix(float __result, StatRequest req, bool applyPostProcess)
        {
            // Quick check to make sure thing isn't null
            if (req.StuffDef == null || req.Thing == null || !req.Thing.def.MadeFromStuff || req.Thing.def.category != ThingCategory.Building || req.BuildableDef.statBases == null)
            {
                return true;
            }
            string identifier = req.Thing.def.defName + req.StuffDef.defName;
            if (!BetterWeight.CachedMassMap.TryGetValue(identifier, out float value)) return true;
            for (int i = 0; i < req.BuildableDef.statBases.Count; i++)
            {
                StatModifier stat = req.BuildableDef.statBases[i];
                if (stat.stat.label != "mass") continue;
                stat.value = value;
            }
            
            // Return true so original method will now run
            return true;
        }
    }
}