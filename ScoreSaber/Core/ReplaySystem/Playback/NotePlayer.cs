using IPA.Utilities;
using ScoreSaber.Core.ReplaySystem.Data;
using ScoreSaber.Extensions;
using SiraUtil.Affinity;
using SiraUtil.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zenject;
using static NoteData;

namespace ScoreSaber.Core.ReplaySystem.Playback {
    internal class NotePlayer : TimeSynchronizer, ITickable, IScroller, IAffinity
    {
        private int _nextIndex = 0;
        private readonly SiraLog _siraLog;
        private readonly SaberManager _saberManager;
        private readonly NoteEvent[] _sortedNoteEvents;
        private readonly ReplayFile _replayFile;
        private readonly MemoryPoolContainer<GameNoteController> _gameNotePool;
        private readonly MemoryPoolContainer<GameNoteController> _burstSliderHeadNotePool;
        private readonly MemoryPoolContainer<BurstSliderGameNoteController> _burstSliderNotePool;
        private readonly MemoryPoolContainer<BombNoteController> _bombNotePool;

        private readonly Dictionary<NoteCutInfo, NoteEvent> _recognizedNoteCutInfos = new Dictionary<NoteCutInfo, NoteEvent>();

        public NotePlayer(SiraLog siraLog, ReplayFile file, SaberManager saberManager, BasicBeatmapObjectManager basicBeatmapObjectManager) {

            _siraLog = siraLog;
            _saberManager = saberManager;
            _gameNotePool = Accessors.GameNotePool(ref basicBeatmapObjectManager);
            _burstSliderHeadNotePool = Accessors.BurstSliderHeadNotePool(ref basicBeatmapObjectManager);
            _burstSliderNotePool = Accessors.BurstSliderNotePool(ref basicBeatmapObjectManager);
            _bombNotePool = Accessors.BombNotePool(ref basicBeatmapObjectManager);
            _sortedNoteEvents = file.noteKeyframes.OrderBy(nk => nk.Time).ToArray();
            _replayFile = file;
        }

        public void Tick() {

            while (_nextIndex < _sortedNoteEvents.Length && audioTimeSyncController.songTime >= _sortedNoteEvents[_nextIndex].Time) {
                
                NoteEvent activeEvent = _sortedNoteEvents[_nextIndex++];
                ProcessEvent(activeEvent);
            }
        }

        private void ProcessEvent(NoteEvent activeEvent) {

            if (activeEvent.EventType == NoteEventType.GoodCut || activeEvent.EventType == NoteEventType.BadCut) {
                foreach (var noteController in _gameNotePool.activeItems) {
                    if (HandleEvent(activeEvent, noteController)) {
                        return;
                    }
                }
                foreach (var noteController in _burstSliderHeadNotePool.activeItems) {
                    if (HandleEvent(activeEvent, noteController)) {
                        return;
                    }
                }
                foreach (var noteController in _burstSliderNotePool.activeItems) {
                    if (HandleEvent(activeEvent, noteController)) {
                        return;
                    }
                }
            } else if (activeEvent.EventType == NoteEventType.Bomb) {
                foreach (var bombController in _bombNotePool.activeItems) {
                    if (HandleEvent(activeEvent, bombController)) {
                        return;
                    }
                }
            }
        }

        private bool HandleEvent(NoteEvent activeEvent, NoteController noteController) {

            if (DoesNoteMatchID(activeEvent.NoteID, noteController.noteData)) {
                Saber correctSaber = noteController.noteData.colorType == ColorType.ColorA ? _saberManager.leftSaber : _saberManager.rightSaber;
                var noteTransform = noteController.noteTransform;

                NoteCutInfo noteCutInfo = new NoteCutInfo(noteController.noteData,
                    activeEvent.SaberSpeed > 2f,
                    activeEvent.DirectionOK,
                    activeEvent.SaberType == (int)correctSaber.saberType,
                    false,
                    activeEvent.SaberSpeed,
                    activeEvent.SaberDirection.Convert(),
                    noteController.noteData.colorType == ColorType.ColorA ? SaberType.SaberA : SaberType.SaberB,
                    noteController.noteData.time - activeEvent.Time,
                    activeEvent.CutDirectionDeviation,
                    activeEvent.CutPoint.Convert(),
                    activeEvent.CutNormal.Convert(),
                    activeEvent.CutDistanceToCenter,
                    activeEvent.CutAngle,
                    
                    noteController.worldRotation,
                    noteController.inverseWorldRotation,
                    noteTransform.rotation,
                    noteTransform.position,

                    correctSaber.movementDataForLogic
                );

                _recognizedNoteCutInfos.Add(noteCutInfo, activeEvent);
                noteController.InvokeMethod<object, NoteController>("SendNoteWasCutEvent", noteCutInfo);
                return true;
            }
            return false;
        }

        bool DoesNoteMatchID(NoteID id, NoteData noteData) {

            if (!Mathf.Approximately(id.Time, noteData.time) || id.LineIndex != noteData.lineIndex || id.LineLayer != (int)noteData.noteLineLayer || id.ColorType != (int)noteData.colorType || id.CutDirection != (int)noteData.cutDirection)
                return false;

            if (id.GameplayType is int gameplayType && gameplayType != (int)noteData.gameplayType)
                return false;

            if (!id.MatchesScoringType(noteData.scoringType, _replayFile.metadata.GameVersion))
                return false;

            if (id.CutDirectionAngleOffset is float cutDirectionAngleOffset && !Mathf.Approximately(cutDirectionAngleOffset, noteData.cutDirectionAngleOffset))
                return false;

            return true;
        }

        [AffinityPostfix, AffinityPatch(typeof(GoodCutScoringElement), nameof(GoodCutScoringElement.Init))]
        protected void ForceCompleteGoodScoringElements(GoodCutScoringElement __instance, NoteCutInfo noteCutInfo, CutScoreBuffer ____cutScoreBuffer) {

            // Just in case someone else is creating their own scoring elements, we want to ensure that we're only force completing ones we know we've created
            if (!_recognizedNoteCutInfos.TryGetValue(noteCutInfo, out var activeEvent))
                return;

            _recognizedNoteCutInfos.Remove(noteCutInfo);

            if (!__instance.isFinished) {

                var ratingCounter = Accessors.RatingCounter(ref ____cutScoreBuffer);

                // Supply the rating counter with the proper cut ratings
                Accessors.AfterCutRating(ref ratingCounter) = activeEvent.AfterCutRating;
                Accessors.BeforeCutRating(ref ratingCounter) = activeEvent.BeforeCutRating;

                // Then immediately finish it
                ____cutScoreBuffer.HandleSaberSwingRatingCounterDidFinish(ratingCounter);

                ScoringElement element = __instance;
                Accessors.ScoringElementFinisher(ref element, true);
            }
        }

        public void TimeUpdate(float newTime) {

            for (int c = 0; c < _sortedNoteEvents.Length; c++) {
                if (_sortedNoteEvents[c].Time > newTime) {
                    _nextIndex = c;
                    return;
                }
            }
            _nextIndex = _sortedNoteEvents.Length;
        }
    }
}