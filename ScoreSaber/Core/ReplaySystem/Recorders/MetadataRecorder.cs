using System;
using System.Collections.Generic;
using ScoreSaber.Core.ReplaySystem.Data;
using Zenject;

namespace ScoreSaber.Core.ReplaySystem.Recorders
{
    internal class MetadataRecorder : TimeSynchronizer, IInitializable, IDisposable
    {
        BeatmapObjectSpawnController.InitData _beatmapObjectSpawnControllerInitData;
        private readonly GameplayCoreSceneSetupData _gameplayCoreSceneSetupData;
        private readonly SettingsManager _settingsManager;
        private readonly IGameEnergyCounter _gameEnergyCounter;
        private float _failTime;

        public MetadataRecorder(GameplayCoreSceneSetupData gameplayCoreSceneSetupData, BeatmapObjectSpawnController.InitData beatmapObjectSpawnControllerInitData, IGameEnergyCounter gameEnergyCounter, SettingsManager settingsManager) {

            _beatmapObjectSpawnControllerInitData = beatmapObjectSpawnControllerInitData;
            _gameEnergyCounter = gameEnergyCounter;
            _gameplayCoreSceneSetupData = gameplayCoreSceneSetupData;
            _settingsManager = settingsManager;
        }


        public void Initialize() {

            _gameEnergyCounter.gameEnergyDidReach0Event += GameEnergyCounter_gameEnergyDidReach0Event;
        }

        public void Dispose() {

            _gameEnergyCounter.gameEnergyDidReach0Event -= GameEnergyCounter_gameEnergyDidReach0Event;
        }


        private void GameEnergyCounter_gameEnergyDidReach0Event() {

            _failTime = audioTimeSyncController.songTime;
        }

        public Metadata Export() {

            VRPosition roomCenter = new VRPosition() {
                X = _settingsManager.settings.room.center.x,
                Y = _settingsManager.settings.room.center.y,
                Z = _settingsManager.settings.room.center.z
            };

            return new Metadata() {
                Version = new Version("3.1.0"),
                LevelID = _gameplayCoreSceneSetupData.beatmapLevel.levelID,
                Difficulty = BeatmapDifficultyMethods.DefaultRating(_gameplayCoreSceneSetupData.beatmapKey.difficulty),
                Characteristic = _gameplayCoreSceneSetupData.beatmapKey.beatmapCharacteristic.serializedName,
                Environment = _gameplayCoreSceneSetupData.targetEnvironmentInfo.serializedName,
                Modifiers = GetModifierList(_gameplayCoreSceneSetupData.gameplayModifiers),
                NoteSpawnOffset = _beatmapObjectSpawnControllerInitData.noteJumpValue,
                LeftHanded = _gameplayCoreSceneSetupData.playerSpecificSettings.leftHanded,
                InitialHeight = _gameplayCoreSceneSetupData.playerSpecificSettings.playerHeight,
                RoomRotation = _settingsManager.settings.room.rotation,
                RoomCenter = roomCenter,
                FailTime = _failTime,
                GameVersion = IPA.Utilities.UnityGame.GameVersion.SemverValue,
                PluginVersion = Plugin.Instance.LibVersion,
                Platform = "PC",
            };

        }

        public string[] GetModifierList(GameplayModifiers modifiers) {

            List<string> result = new List<string>();
            if (modifiers.energyType == GameplayModifiers.EnergyType.Battery) {
                result.Add("BE");
            }
            if (modifiers.noFailOn0Energy) {
                result.Add("NF");
            }
            if (modifiers.instaFail) {
                result.Add("IF");
            }
            if (modifiers.failOnSaberClash) {
                result.Add("SC");
            }
            if (modifiers.enabledObstacleType == GameplayModifiers.EnabledObstacleType.NoObstacles) {
                result.Add("NO");
            }
            if (modifiers.noBombs) {
                result.Add("NB");
            }
            if (modifiers.strictAngles) {
                result.Add("SA");
            }
            if (modifiers.disappearingArrows) {
                result.Add("DA");
            }
            if (modifiers.ghostNotes) {
                result.Add("GN");
            }
            if (modifiers.songSpeed == GameplayModifiers.SongSpeed.Slower) {
                result.Add("SS");
            }
            if (modifiers.songSpeed == GameplayModifiers.SongSpeed.Faster) {
                result.Add("FS");
            }
            if (modifiers.songSpeed == GameplayModifiers.SongSpeed.SuperFast) {
                result.Add("SF");
            }
            if (modifiers.smallCubes) {
                result.Add("SC");
            }
            if (modifiers.proMode) {
                result.Add("PM");
            }
            if (modifiers.noArrows) {
                result.Add("NA");
            }
            return result.ToArray();
        }

    }
}
