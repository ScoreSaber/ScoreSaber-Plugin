﻿using HarmonyLib;
using System;

namespace ScoreSaber.Core.ReplaySystem.HarmonyPatches
{
    [HarmonyPatch(typeof(RelativeScoreAndImmediateRankCounter), nameof(RelativeScoreAndImmediateRankCounter.UpdateRelativeScoreAndImmediateRank))]
    internal class ImmediateRankReinitializer
    {
        internal static bool Prefix(RelativeScoreAndImmediateRankCounter __instance, int score, int maxPossibleScore, ref Action ___relativeScoreOrImmediateRankDidChangeEvent) {

            if (Plugin.ReplayState.IsPlaybackEnabled && !Plugin.ReplayState.IsLegacyReplay) {
                if (score == 0 && maxPossibleScore == 0) {
                    __instance.relativeScore = 1f;
                    __instance.immediateRank = RankModel.Rank.SS;
                    ___relativeScoreOrImmediateRankDidChangeEvent.Invoke();
                    return false;
                }
                return true;
            }
            return true;
        }
    }
}
