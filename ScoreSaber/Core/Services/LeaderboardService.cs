﻿using Newtonsoft.Json;
using System.Threading.Tasks;
using ScoreSaber.Core.Data.Wrappers;
using ScoreSaber.Core.Data.Models;
using System;

namespace ScoreSaber.Core.Services {
    internal class LeaderboardService {

        public LeaderboardMap currentLoadedLeaderboard = null;

        public LeaderboardService() {
            Plugin.Log.Debug("LeaderboardService Setup");
        }

        public async Task<LeaderboardMap> GetLeaderboardData(IDifficultyBeatmap difficultyBeatmap, PlatformLeaderboardsModel.ScoresScope scope, int page, PlayerSpecificSettings playerSpecificSettings, bool filterAroundCountry = false) {

            string leaderboardUrl = GetLeaderboardUrl(difficultyBeatmap, scope, page, filterAroundCountry);
            string leaderboardRawData = await Plugin.HttpInstance.GetAsync(leaderboardUrl);
            Leaderboard leaderboardData = JsonConvert.DeserializeObject<Leaderboard>(leaderboardRawData);

            var beatmapData = await difficultyBeatmap.GetBeatmapDataAsync(difficultyBeatmap.GetEnvironmentInfo(), playerSpecificSettings);

            Plugin.Log.Debug($"Current leaderboard set to: {difficultyBeatmap.level.levelID}:{difficultyBeatmap.level.songName}");
            currentLoadedLeaderboard = new LeaderboardMap(leaderboardData, difficultyBeatmap, beatmapData);
            return currentLoadedLeaderboard;
        }

        public async Task<Leaderboard> GetCurrentLeaderboard(IDifficultyBeatmap difficultyBeatmap) {

            string leaderboardUrl = GetLeaderboardUrl(difficultyBeatmap, PlatformLeaderboardsModel.ScoresScope.Global, 1, false);

            int attempts = 0;
            while (attempts < 4) {
                try {
                    string leaderboardRawData = await Plugin.HttpInstance.GetAsync(leaderboardUrl);
                    Leaderboard leaderboardData = JsonConvert.DeserializeObject<Leaderboard>(leaderboardRawData);
                    return leaderboardData;
                } catch (Exception) {
                }
                attempts++;
                await Task.Delay(1000);
            }
            return null;
        }
      
        private string GetLeaderboardUrl(IDifficultyBeatmap difficultyBeatmap, PlatformLeaderboardsModel.ScoresScope scope, int page, bool filterAroundCountry) {

            string url = "/game/leaderboard";
            string leaderboardId = difficultyBeatmap.level.levelID.Split('_')[2];
            string gameMode = $"Solo{difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName}";
            string difficulty = BeatmapDifficultyMethods.DefaultRating(difficultyBeatmap.difficulty).ToString();

            if (!filterAroundCountry) {
                switch (scope) {
                    case PlatformLeaderboardsModel.ScoresScope.Global:
                        url = $"{url}/{leaderboardId}/mode/{gameMode}/difficulty/{difficulty}?page={page}";
                        break;
                    case PlatformLeaderboardsModel.ScoresScope.AroundPlayer:
                        url = $"{url}/around-player/{leaderboardId}/mode/{gameMode}/difficulty/{difficulty}";
                        break;
                    case PlatformLeaderboardsModel.ScoresScope.Friends:
                        url = $"{url}/around-friends/{leaderboardId}/mode/{gameMode}/difficulty/{difficulty}?page={page}";
                        break;
                }
            } else {
                if(Plugin.Settings.locationFilterMode.ToLower() == "region") {
                    url = $"{url}/around-region/{leaderboardId}/mode/{gameMode}/difficulty/{difficulty}?page={page}";
                }
                else if(Plugin.Settings.locationFilterMode.ToLower() == "country") {
                    url = $"{url}/around-country/{leaderboardId}/mode/{gameMode}/difficulty/{difficulty}?page={page}";
                } else {
                    Plugin.Log.Error("Invalid location filter mode, falling back to country");
                    url = $"{url}/around-country/{leaderboardId}/mode/{gameMode}/difficulty/{difficulty}?page={page}";
                }
            }

            if (Plugin.Settings.hideNAScoresFromLeaderboard) {
                url = $"{url}&hideNA=1";
            }

            return url;
        }
    }
}
