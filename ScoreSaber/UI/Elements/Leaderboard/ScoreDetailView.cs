﻿using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using ScoreSaber.Core.Data;
using ScoreSaber.Core.Data.Models;
using ScoreSaber.Core.Data.Wrappers;
using ScoreSaber.Core.Utils;
using ScoreSaber.Extensions;
using System;
using UnityEngine.UI;

namespace ScoreSaber.UI.Elements.Leaderboard {
    internal class ScoreDetailView {

        #region BSML Components
        [UIComponent("detail-modal-root")]
        public ModalView detailModalRoot = null;
        [UIComponent("prefix-text")]
        protected readonly CurvedTextMeshPro _prefixText = null;
        [UIComponent("name-text")]
        protected readonly CurvedTextMeshPro _nameText = null;
        [UIComponent("devicehmd-text")]
        protected readonly CurvedTextMeshPro _deviceHmdText = null;
        [UIComponent("devicecontrollerleft-text")]
        protected readonly CurvedTextMeshPro _deviceControllerLeftText = null;
        [UIComponent("devicecontrollerright-text")]
        protected readonly CurvedTextMeshPro _deviceControllerRightText = null;
        [UIComponent("score-text")]
        protected readonly CurvedTextMeshPro _scoreText = null;
        [UIComponent("pp-text")]
        protected readonly CurvedTextMeshPro _ppText = null;
        [UIComponent("max-combo-text")]
        protected readonly CurvedTextMeshPro _maxComboText = null;
        [UIComponent("full-combo-text")]
        protected readonly CurvedTextMeshPro _fullComboText = null;
        [UIComponent("bad-cuts-text")]
        protected readonly CurvedTextMeshPro _badCutsText = null;
        [UIComponent("missed-notes-text")]
        protected readonly CurvedTextMeshPro _missedNotesText = null;
        [UIComponent("modifiers-text")]
        protected readonly CurvedTextMeshPro _modifiersText = null;
        [UIComponent("time-text")]
        protected readonly CurvedTextMeshPro _timeText = null;

        [UIComponent("prefix-image")]
        private readonly ImageView _scoreInfoPrefixPicture = null;
        public string scoreInfoPrefixPicture {
            set {
                if (value == null) {
                    _scoreInfoPrefixPicture.gameObject.SetActive(false);
                    return;
                }
                _scoreInfoPrefixPicture.gameObject.SetActive(true);
                _scoreInfoPrefixPicture.SetImageAsync(value).RunTask();
            }
        }

        private readonly HoverHint _scoreInfoHoverHint = null;
        public HoverHint scoreInfoHoverHint {
            get {
                if (_scoreInfoHoverHint == null) {
                    return _scoreInfoPrefixPicture.gameObject.GetComponent<HoverHint>();
                }
                return _scoreInfoHoverHint;
            }
        }

        [UIComponent("watch-replay-button")]
        protected readonly Button _watchReplayButton = null;
        [UIComponent("show-profile-button")]
        protected readonly Button _showProfileButton = null;
        [UIAction("show-profile-click")] private void ShowProfileClicked() => showProfile?.Invoke(_currentScore.score.leaderboardPlayerInfo.id);
        [UIAction("replay-click")] private void ReplayClicked() => StartReplay();
        #endregion

        public event Action<string> showProfile;
        public event Action<ScoreMap> startReplay;

        private bool _allowReplayWatching = true;

        private ScoreMap _currentScore { get; set; }

        [UIAction("#post-parse")]
        public void Parsed() {
            _nameText.fontSizeMin = 2.5f;
            _nameText.fontSizeMax = 4.0f;
            _nameText.enableAutoSizing = true;
            _watchReplayButton.transform.localScale *= .4f;
            _showProfileButton.transform.localScale *= .4f;
        }

        public void SetScoreInfo(ScoreMap scoreMap, bool replayDownloading) {

            _currentScore = scoreMap;
            Score score = scoreMap.score;
            SetCrowns(score.leaderboardPlayerInfo.id);
            _nameText.text = $"{score.leaderboardPlayerInfo.name}'s score";
            _deviceHmdText.SetFancyText("HMD", score.deviceHmd ?? VRDevices.GetLegacyHmdFriendlyName(score.hmd));
            _deviceControllerLeftText.SetFancyText("Left Controller", score.deviceControllerLeft ?? "N/A");
            _deviceControllerRightText.SetFancyText("Right Controller", score.deviceControllerRight ?? "N/A");
            _scoreText.SetFancyText("Score", $"{string.Format("{0:n0}", score.modifiedScore)} (<color=#FFD42A>{scoreMap.accuracy}%</color>)");
            _ppText.SetFancyText("Performance Points", $"<color=#6772E5>{score.pp}pp</color>");
            _maxComboText.SetFancyText("Combo", score.maxCombo != 0 ? score.maxCombo.ToString() : "N/A");
            _fullComboText.SetFancyText("Full Combo", score.maxCombo != 0 ? score.fullCombo ? "<color=#9EDBB1>Yes</color>" : "<color=#FF0000>No</color>" : "N/A");
            _badCutsText.SetFancyText("Bad Cuts", score.maxCombo != 0 ? score.badCuts > 0 ? $"<color=#FF0000>{score.badCuts}</color>" : score.badCuts.ToString() : "N/A");
            _missedNotesText.SetFancyText("Missed Notes", score.maxCombo != 0 ? score.missedNotes > 0 ? $"<color=#FF0000>{score.missedNotes}</color>" : score.missedNotes.ToString() : "N/A");
            _modifiersText.SetFancyText("Modifiers", score.modifiers);
            _timeText.SetFancyText("Time Set", new TimeSpan(DateTime.UtcNow.Ticks - score.timeSet.Ticks).ToNaturalTime(2, true) + " ago");

            if (!replayDownloading) {
                SetButtonState(_watchReplayButton, score.hasReplay && _allowReplayWatching);
            }
        }

        private void SetCrowns(string playerId) {

            scoreInfoPrefixPicture = null;
            scoreInfoHoverHint.enabled = false;
            Tuple<string, string> crownDetails = LeaderboardUtils.GetCrownDetails(playerId);

            if (!string.IsNullOrEmpty(crownDetails.Item1)) {
                scoreInfoHoverHint.enabled = true;
                scoreInfoPrefixPicture = crownDetails.Item1;
                scoreInfoHoverHint.text = crownDetails.Item2;
            }
        }

        private void StartReplay() {
            _watchReplayButton.interactable = false;
            startReplay?.Invoke(_currentScore);
        }

        public void AllowReplayWatching(bool value) {
            _allowReplayWatching = value;

            SetButtonState(_watchReplayButton, value);
        }

        private void SetButtonState(Button button, bool value) {
            
            if (button == null)
                return;

            button.interactable = value;
            button.gameObject.GetComponent<HoverHint>().enabled = value;
        }
    }
}
