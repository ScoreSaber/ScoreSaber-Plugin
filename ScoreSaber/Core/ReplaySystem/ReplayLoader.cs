using IPA.Utilities.Async;
using ScoreSaber.Core.Daemons;
using ScoreSaber.Core.ReplaySystem.Data;
using ScoreSaber.Core.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using UnityEngine;

namespace ScoreSaber.Core.ReplaySystem {
    internal class ReplayLoader {

        private readonly PlayerDataModel _playerDataModel;
        private readonly MenuTransitionsHelper _menuTransitionsHelper;
        private readonly StandardLevelScenesTransitionSetupDataSO _standardLevelScenesTransitionSetupDataSO;
        private readonly ReplayFileReader _replayFileReader;
        private readonly EnvironmentsListModel _environmentsListModel;

        public ReplayLoader(PlayerDataModel playerDataModel, MenuTransitionsHelper menuTransitionsHelper, EnvironmentsListModel environmentsListModel) {

            _playerDataModel = playerDataModel;
            _menuTransitionsHelper = menuTransitionsHelper;
            _standardLevelScenesTransitionSetupDataSO = Accessors.StandardLevelScenesTransitionSetupData(ref _menuTransitionsHelper);
            _replayFileReader = new ReplayFileReader();
            _environmentsListModel = environmentsListModel;
        }

        public async Task Load(byte[] replay, BeatmapLevel beatmapLevel, BeatmapKey beatmapKey, GameplayModifiers modifiers, string playerName) {

            Plugin.ReplayState.CurrentBeatmapLevel = beatmapLevel;
            Plugin.ReplayState.CurrentBeatmapKey = beatmapKey;
            Plugin.ReplayState.CurrentModifiers = modifiers;
            Plugin.ReplayState.CurrentPlayerName = playerName;
            if (replay[0] == 93 && replay[1] == 0 && replay[2] == 0 && replay[3] == 128) {
                await LoadLegacyReplay(replay, beatmapLevel, beatmapKey, modifiers);
            } else {
                ReplayFile replayFile = await LoadReplay(replay);
                await StartReplay(replayFile, beatmapLevel, beatmapKey);
            }
        }

        private async Task LoadLegacyReplay(byte[] replay, BeatmapLevel beatmapLevel, BeatmapKey beatmapKey, GameplayModifiers gameplayModifiers) {
            await Task.Run(async () => {
                byte[] decompressed = SevenZip.Compression.LZMA.SevenZipHelper.Decompress(replay);
                BinaryFormatter formatter = new BinaryFormatter();
                Z.SavedData replayData = null;
                try {
                    using (var dataStream = new MemoryStream(decompressed)) {
                        replayData = (Z.SavedData)formatter.Deserialize(dataStream);
                    }
                } catch (Exception) {
                    throw new Exception("Failed to deserialize replay!");
                }

                Plugin.ReplayState.LoadedLegacyKeyframes = await AddFrames(replayData);
                Plugin.ReplayState.IsPlaybackEnabled = true;
                Plugin.ReplayState.IsLegacyReplay = true;
                PlayerData playerData = _playerDataModel.playerData;
                PlayerSpecificSettings playerSettings = playerData.playerSpecificSettings;
                _standardLevelScenesTransitionSetupDataSO.didFinishEvent -= UploadDaemonHelper.ThreeInstance;
                if (gameplayModifiers == null) {
                    gameplayModifiers = new GameplayModifiers();
                }

                _menuTransitionsHelper.StartStandardLevel(
                    gameMode: "Replay",
                    beatmapKey: beatmapKey,
                    beatmapLevel: beatmapLevel,
                    overrideEnvironmentSettings: playerData.overrideEnvironmentSettings,
                    playerOverrideColorScheme: playerData.colorSchemesSettings.GetOverrideColorScheme(),
                    playerOverrideLightshowColors: playerData.colorSchemesSettings.ShouldOverrideLightshowColors(),
                    beatmapOverrideColorScheme: beatmapLevel.GetColorScheme(beatmapKey.beatmapCharacteristic, beatmapKey.difficulty),
                    gameplayModifiers: gameplayModifiers,
                    playerSpecificSettings: playerSettings,
                    practiceSettings: null,
                    environmentsListModel: _environmentsListModel,
                    backButtonText: "Exit Replay",
                    useTestNoteCutSoundEffects: false,
                    startPaused: false,
                    beforeSceneSwitchToGameplayCallback: null,
                    afterSceneSwitchToGameplayCallback: null,
                    levelFinishedCallback: ReplayEnd,
                    levelRestartedCallback: null
                );
            });
        }

        private async Task<List<Z.Keyframe>> AddFrames(Z.SavedData replayData) {
            List<Z.Keyframe> _keyframes = new List<Z.Keyframe>(replayData._keyframes.Length);
            await Task.Run(() => {
                if (replayData != null) {
                    for (int i = 0; i < replayData._keyframes.Length; i++) {
                        Z.SavedData.KeyframeSerializable ks = replayData._keyframes[i];
                        Z.Keyframe k = new Z.Keyframe {
                            _pos1 = new Vector3(ks._xPos1, ks._yPos1, ks._zPos1),
                            _pos2 = new Vector3(ks._xPos2, ks._yPos2, ks._zPos2),
                            _pos3 = new Vector3(ks._xPos3, ks._yPos3, ks._zPos3),
                            _rot1 = new Quaternion(ks._xRot1, ks._yRot1, ks._zRot1, ks._wRot1),
                            _rot2 = new Quaternion(ks._xRot2, ks._yRot2, ks._zRot2, ks._wRot2),
                            _rot3 = new Quaternion(ks._xRot3, ks._yRot3, ks._zRot3, ks._wRot3),
                            _time = ks._time,
                            score = ks.score,
                            combo = ks.combo
                        };
                        _keyframes.Add(k);
                    }
                }
            });
            return _keyframes;
        }

        private async Task<ReplayFile> LoadReplay(byte[] replay) {

            var replayFile = await Task.Run(() => {
                return _replayFileReader.Read(replay);
            });
            return replayFile;
        }


        private async Task StartReplay(ReplayFile replay, BeatmapLevel beatmapLevel, BeatmapKey beatmapKey) {

            await Task.Run(() => {
                Plugin.ReplayState.IsLegacyReplay = false;
                Plugin.ReplayState.IsPlaybackEnabled = true;
                Plugin.ReplayState.LoadedReplayFile = replay;
                PlayerData playerData = _playerDataModel.playerData;
                PlayerSpecificSettings localPlayerSettings = playerData.playerSpecificSettings;
                PlayerSpecificSettings playerSettings = new PlayerSpecificSettings(replay.metadata.LeftHanded, replay.metadata.InitialHeight,
                    replay.heightKeyframes.Count > 0, localPlayerSettings.sfxVolume, localPlayerSettings.reduceDebris,
                    localPlayerSettings.noTextsAndHuds, localPlayerSettings.noFailEffects, localPlayerSettings.advancedHud,
                    localPlayerSettings.autoRestart, localPlayerSettings.saberTrailIntensity, localPlayerSettings.noteJumpDurationTypeSettings,
                    localPlayerSettings.noteJumpFixedDuration, localPlayerSettings.noteJumpStartBeatOffset, localPlayerSettings.hideNoteSpawnEffect,
                    localPlayerSettings.adaptiveSfx, localPlayerSettings.arcsHapticFeedback, localPlayerSettings.arcVisibility,
                    localPlayerSettings.environmentEffectsFilterDefaultPreset,
                    localPlayerSettings.environmentEffectsFilterExpertPlusPreset,
                    localPlayerSettings.headsetHapticIntensity);

                _standardLevelScenesTransitionSetupDataSO.didFinishEvent -= UploadDaemonHelper.ThreeInstance;
                
                _menuTransitionsHelper.StartStandardLevel(
                    gameMode: "Replay",
                    beatmapKey: beatmapKey,
                    beatmapLevel: beatmapLevel,
                    overrideEnvironmentSettings: playerData.overrideEnvironmentSettings,
                    playerOverrideColorScheme: playerData.colorSchemesSettings.GetOverrideColorScheme(),
                    playerOverrideLightshowColors: playerData.colorSchemesSettings.ShouldOverrideLightshowColors(),
                    beatmapOverrideColorScheme: beatmapLevel.GetColorScheme(beatmapKey.beatmapCharacteristic, beatmapKey.difficulty),
                    gameplayModifiers: LeaderboardUtils.GetModifierFromStrings(replay.metadata.Modifiers.ToArray(), false).gameplayModifiers,
                    playerSpecificSettings: playerSettings,
                    practiceSettings: null,
                    environmentsListModel: _environmentsListModel,
                    backButtonText: "Exit Replay",
                    useTestNoteCutSoundEffects: false,
                    startPaused: false,
                    beforeSceneSwitchToGameplayCallback: null,
                    afterSceneSwitchToGameplayCallback: null,
                    levelFinishedCallback: ReplayEnd,
                    levelRestartedCallback: null
                );
            });
        }

        private void ReplayEnd(StandardLevelScenesTransitionSetupDataSO standardLevelSceneSetupData, LevelCompletionResults levelCompletionResults) {

            Plugin.ReplayState.IsPlaybackEnabled = false;
            if (Plugin.ScoreSubmission) {
                _standardLevelScenesTransitionSetupDataSO.didFinishEvent += UploadDaemonHelper.ThreeInstance;
            }
        }
    }
}