﻿using HMUI;

namespace ScoreSaber.Utilities {
    internal static class LeaderboardExtensions {

        internal static void SetFancyText(this CurvedTextMeshPro curvedTextMeshPro, string title, string text) {
            curvedTextMeshPro.text = $"<color=#6F6F6F>{title}:</color> {text}";
        }

    }
}