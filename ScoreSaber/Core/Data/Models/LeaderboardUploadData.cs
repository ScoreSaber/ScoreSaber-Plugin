using HarmonyLib;
using Newtonsoft.Json;
using ScoreSaber.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ScoreSaber.Core.Data.Models {

    internal class ScoreSaberUploadData {
        [JsonProperty("playerName")]
        internal string playerName;
        [JsonProperty("playerId")]
        internal string playerId;
        [JsonProperty("score")]
        internal int score;
        [JsonProperty("leaderboardId")]
        internal string leaderboardId;
        [JsonProperty("songName")]
        internal string songName;
        [JsonProperty("songSubName")]
        internal string songSubName;
        [JsonProperty("levelAuthorName")]
        internal string levelAuthorName;
        [JsonProperty("songAuthorName")]
        internal string songAuthorName;
        [JsonProperty("bpm")]
        internal int bpm;
        [JsonProperty("difficulty")]
        internal int difficulty;
        [JsonProperty("infoHash")]
        internal string infoHash;
        [JsonProperty("modifiers")]
        internal List<string> modifiers;
        [JsonProperty("gameMode")]
        internal string gameMode;
        [JsonProperty("badCutsCount")]
        internal int badCutsCount;
        [JsonProperty("missedCount")]
        internal int missedCount;
        [JsonProperty("maxCombo")]
        internal int maxCombo;
        [JsonProperty("fullCombo")]
        internal bool fullCombo;
        [JsonProperty("hmd")]
        internal int? hmd;
        [JsonProperty("deviceHmdIdentifier")]
        internal string deviceHmdIdentifier;
        [JsonProperty("deviceControllerLeftIdentifier")]
        internal string deviceControllerLeftIdentifier;
        [JsonProperty("deviceControllerRightIdentifier")]
        internal string deviceControllerRightIdentifier;

        internal static ScoreSaberUploadData Create(BeatmapLevel beatmapLevel, BeatmapKey beatmapKey, LevelCompletionResults results, LocalPlayerInfo playerInfo, string infoHash) {

            ScoreSaberUploadData data = new ScoreSaberUploadData();

            string[] levelInfo = beatmapKey.levelId.Split('_');
            data.gameMode = $"Solo{beatmapKey.beatmapCharacteristic.serializedName}";
            data.difficulty = BeatmapDifficultyMethods.DefaultRating(beatmapKey.difficulty);
            data.infoHash = infoHash;
            data.leaderboardId = levelInfo[2];
            data.songName = beatmapLevel.songName;
            data.songSubName = beatmapLevel.songSubName;
            data.songAuthorName = beatmapLevel.songAuthorName;
            data.levelAuthorName = friendlyLevelAuthorName(beatmapLevel.allMappers, beatmapLevel.allLighters);
            data.bpm = Convert.ToInt32(beatmapLevel.beatsPerMinute);

            data.playerName = playerInfo.playerName;
            data.playerId = playerInfo.playerId;

            data.badCutsCount = results.badCutsCount;
            data.missedCount = results.missedCount;
            data.maxCombo = results.maxCombo;
            data.fullCombo = results.fullCombo;

            data.score = results.multipliedScore;
            data.modifiers = LeaderboardUtils.GetModifierList(results);
            data.hmd = null; // we can't generate the legacy hmd data anymore
            data.deviceHmdIdentifier = VRDevices.GetDeviceHMD();
            data.deviceControllerLeftIdentifier = VRDevices.GetDeviceControllerLeft();
            data.deviceControllerRightIdentifier = VRDevices.GetDeviceControllerRight();
            return data;
        }

        static string friendlyLevelAuthorName(string[] mappers, string[] lighters) {
            List<string> mappersAndLighters = new List<string>();
            mappersAndLighters.AddRange(mappers);
            mappersAndLighters.AddRange(lighters);

            if (mappersAndLighters.Count == 0)
                return "";
            if(mappersAndLighters.Count == 1) {
                return mappersAndLighters.First();
            }
            return $"{string.Join(", ", mappersAndLighters.Take(mappersAndLighters.Count - 1))} & {mappersAndLighters.Last()}";
        }
    }
}