﻿using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.Tags;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using IPA.Loader;
using IPA.Utilities;
using IPA.Utilities.Async;
using LeaderboardCore.Interfaces;
using ScoreSaber.Core.Daemons;
using ScoreSaber.Core.Data.Models;
using ScoreSaber.Core.Data.Wrappers;
using ScoreSaber.Core.ReplaySystem;
using ScoreSaber.Core.ReplaySystem.Data;
using ScoreSaber.Core.Services;
using ScoreSaber.Core.Utils;
using ScoreSaber.Extensions;
using ScoreSaber.UI.Elements.Leaderboard;
using ScoreSaber.UI.Elements.Profile;
using ScoreSaber.UI.Leaderboard;
using ScoreSaber.UI.Main;
using SiraUtil.Affinity;
using SiraUtil.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;
using Button = UnityEngine.UI.Button;

namespace ScoreSaber.UI.Leaderboard {

    [HotReload(RelativePathToLayout = @"./ScoreSaberLeaderboardViewController.bsml")]
    [ViewDefinition("ScoreSaber.UI.Leaderboard.ScoreSaberLeaderboardViewController.bsml")]
    internal class ScoreSaberLeaderboardViewController : BSMLAutomaticViewController, INotifyLeaderboardSet, IInitializable {

        // TODO: Put both of these somewhere nicer?
#pragma warning disable CS0169 // The field 'ScoreSaberLeaderboardViewController.headerText' is never used
#pragma warning disable CS0649 // Field 'ScoreSaberLeaderboardViewController.myHeader' is never assigned to, and will always have its default value null

        public enum ScoreSaberScoresScope {
            Global,
            Player,
            Friends,
            Area
        }

        public enum UploadStatus {
            Packaging = 0,
            Uploading = 1,
            Success = 2,
            Retrying = 3,
            Error = 4,
            Done
        }

        [UIParams]
        private readonly BSMLParserParams _parserParams = null;

        [UIComponent("leaderboardTableView")]
        private readonly LeaderboardTableView leaderboardTableView = null;

        [UIComponent("leaderboardTableView")]
        internal readonly Transform leaderboardTransform = null;

        [UIComponent("myHeader")]
        private readonly Backgroundable myHeader;

        [UIComponent("headerText")]
        private readonly TextMeshProUGUI headerText;

        [UIComponent("errorText")]
        private readonly TextMeshProUGUI _errorText;

        [UIValue("imageHolders")]
        [Inject] internal List<ProfilePictureView> _ImageHolders = null;

        [UIValue("cellClickerHolders")]
        [Inject] internal List<CellClickingView> _cellClickingHolders = null;

        [UIValue("entry-holder")]
        internal EntryHolder _infoButtons = null;
        
        [UIValue("score-detail-view")]
        protected ScoreDetailView _scoreDetailView = null;

        [UIComponent("profile-detail-view")]
        protected readonly ProfileDetailView _profileDetailView = null;

        [UIComponent("up_button")]
        private readonly Button _upButton;

        [UIComponent("down_button")]
        private readonly Button _downButton;

        [UIObject("loadingLB")]
        private readonly GameObject loadingLB;

        [UIAction("OnPageUp")] private void UpButtonClicked() => DirectionalButtonClicked(false);
        [UIAction("OnPageDown")] private void DownButtonClicked() => DirectionalButtonClicked(true);


        public bool activated { get; private set; }
        public int leaderboardPage { get; set; } = 1;

        public ScoreSaberScoresScope currentScoreScope { get; set; }

        private bool _replayDownloading;
        private string _currentLeaderboardRefreshId = string.Empty;
        private BeatmapKey _currentBeatmapKey;

        [Inject] private readonly PanelView _panelView;
        [Inject] private readonly SiraLog _log;
        [Inject] private readonly DiContainer _container;
        [Inject] private readonly IUploadDaemon _uploadDaemon;
        [Inject] private readonly ReplayLoader _replayLoader;
        [Inject] private readonly PlayerService _playerService;
        [Inject] private readonly LeaderboardService _leaderboardService;
        [Inject] private readonly PlayerDataModel _playerDataModel;
        [Inject] internal readonly PlatformLeaderboardViewController _platformLeaderboardViewController;
        [Inject] private readonly MaxScoreCache _maxScoreCache;
        [Inject] private readonly PlatformLeaderboardViewController _plvc;
        [Inject] private readonly BeatmapLevelsModel _beatmapLevelsModel;

        private void infoButtons_infoButtonClicked(int index) {
            if (_leaderboardService.currentLoadedLeaderboard == null) { return; }

            _parserParams.EmitEvent("present-score-info");
            _scoreDetailView.SetScoreInfo(_leaderboardService.currentLoadedLeaderboard.scores[index], _replayDownloading);
        }

        private void scoreDetailView_showProfile(string playerId) {

            _parserParams.EmitEvent("close-modals");
            _parserParams.EmitEvent("show-profile");
            _profileDetailView.ShowProfile(playerId).RunTask();
        }

        private void scoreDetailView_startReplay(ScoreMap score) {
            StartReplay(score).RunTask();
        }

        private void playerService_LoginStatusChanged(PlayerService.LoginStatus loginStatus, string status) {
            switch (loginStatus) {
                case PlayerService.LoginStatus.InProgress:
                    _panelView.SetPromptInfo(status, true);
                    break;
                case PlayerService.LoginStatus.Error:
                    _panelView.SetPromptError(status, false);
                    break;
                case PlayerService.LoginStatus.Success:
                    _panelView.SetPromptSuccess(status, false, 3f);
                    _panelView.RankUpdater().RunTask();
                    RefreshLeaderboard();
                    break;
            }
            Plugin.Log.Debug(status);
        }

        private void uploadDaemon_UploadStatusChanged(UploadStatus status, string statusText) {
            if (statusText != string.Empty) {
                Plugin.Log.Debug($"{statusText}");
            }
            switch (status) {
                case UploadStatus.Packaging:
                    _panelView.Loaded(false);
                    _panelView.SetPromptInfo(statusText, true);
                    ByeImages();
                    break;
                case UploadStatus.Uploading:
                    _panelView.SetPromptInfo(statusText, true);
                    break;
                case UploadStatus.Success:
                    _panelView.SetPromptSuccess(statusText, false, 2f);
                    break;
                case UploadStatus.Retrying:
                    _panelView.SetPromptError(statusText, true);
                    break;
                case UploadStatus.Error:
                    _panelView.SetPromptError(statusText, false, 3f);
                    break;
                case UploadStatus.Done:
                    RefreshLeaderboard();
                    _panelView.UpdateRank().RunTask();
                    break;
            }
        }

        private ImageView _imgView;

        internal static readonly FieldAccessor<ImageView, float>.Accessor ImageSkew = FieldAccessor<ImageView, float>.GetAccessor("_skew");
        internal static readonly FieldAccessor<ImageView, bool>.Accessor ImageGradient = FieldAccessor<ImageView, bool>.GetAccessor("_gradient");

        [UIAction("#post-parse")]
        private void PostParse() {
            myHeader.Background.material = Utilities.ImageResources.NoGlowMat;
            var loadingLB = leaderboardTransform.Find("LoadingControl").gameObject;
            Transform loadingContainer = loadingLB.transform.Find("LoadingContainer");
            loadingContainer.gameObject.SetActive(false);
            Destroy(loadingContainer.Find("Text").gameObject);
            Destroy(loadingLB.transform.Find("RefreshContainer").gameObject);
            Destroy(loadingLB.transform.Find("DownloadingContainer").gameObject);
            _imgView = myHeader.Background as ImageView;
            Color color = new Color(255f / 255f, 222f / 255f, 24f / 255f);
            _imgView.color = color;
            _imgView.color0 = color;
            _imgView.color1 = color;
            ImageSkew(ref _imgView) = 0.18f;
            ImageGradient(ref _imgView) = true;
        }

        [UIAction("OpenLeaderboardPage")]
        internal void OpenLeaderboardPage() {
            Application.OpenURL($"https://scoresaber.com/leaderboard/{_leaderboardService.currentLoadedLeaderboard.leaderboardInfoMap.leaderboardInfo.id}");
        }

        [UIAction("SettingsClicked")]
        internal void OpenBugPage() => ScoreSaberSettingsFlowCoordinator.ShowSettingsFlowCoordinator();


        [UIAction("OnIconSelected")]
        private void OnIconSelected(SegmentedControl segmentedControl, int index) {
            currentScoreScope = (ScoreSaberScoresScope)index;
            leaderboardPage = 0;
            CheckPage();
            OnLeaderboardSet(_currentBeatmapKey);
        }

        [UIValue("leaderboardIcons")]
        private List<IconSegmentedControl.DataItem> leaderboardIcons {
            get {
#pragma warning disable CS0618 // Type or member is obsolete
                return new List<IconSegmentedControl.DataItem>
        {
            new IconSegmentedControl.DataItem(Utilities.FindSpriteInAssembly("ScoreSaber.Resources.globe.png"), "Global"),
            new IconSegmentedControl.DataItem(Utilities.FindSpriteInAssembly("ScoreSaber.Resources.Player.png"), "Around you"),
            new IconSegmentedControl.DataItem(Utilities.FindSpriteInAssembly("ScoreSaber.Resources.Player.png"), "Friends"),
            new IconSegmentedControl.DataItem(Utilities.FindSpriteInAssembly("ScoreSaber.Resources.country.png"), "Country")
        };
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling) {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            if (!base.isActiveAndEnabled) return;
            if (!_plvc) return;
            if (firstActivation) {
                _panelView.statusWasSelected = delegate () {
                    if (_leaderboardService.currentLoadedLeaderboard == null) { return; }
                    _parserParams.EmitEvent("close-modals");
                    Application.OpenURL($"https://scoresaber.com/leaderboard/{_leaderboardService.currentLoadedLeaderboard.leaderboardInfoMap.leaderboardInfo.id}");
                };

                _panelView.rankingWasSelected = delegate () {
                    _parserParams.EmitEvent("close-modals");
                    _parserParams.EmitEvent("show-profile");
                    _profileDetailView.ShowProfile(_playerService.localPlayerInfo.playerId).RunTask();
                };

                _container.Inject(_profileDetailView);
                _playerService.GetLocalPlayerInfo();
                _ImageHolders.ForEach(holder => holder.ClearSprite());
                activated = true;
            }
            Transform header = _plvc.transform.Find("HeaderPanel");
            _plvc.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 0);
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling) {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
            if (!_plvc || !_plvc.isActivated) return;
            _plvc.GetComponentInChildren<TextMeshProUGUI>().color = Color.white;
            if (!_plvc.isActivated) return;
            if (_scoreDetailView.detailModalRoot != null) _scoreDetailView.detailModalRoot.Hide(false);
            if (_profileDetailView.profileModalRoot != null) _profileDetailView.profileModalRoot.Hide(false);
        }

        private CancellationTokenSource cancellationToken;

        public async Task RefreshLeaderboard(BeatmapLevel beatmapLevel, BeatmapKey beatmapKey, LeaderboardTableView tableView, ScoreSaberScoresScope scope, GameObject loadingControl, string refreshId) {
            Plugin.Log.Info("begin refresh leaderboard");
            try {
                loadingControl.SetActive(false);
                _errorText.gameObject.SetActive(false);
                tableView.SetScores(new List<LeaderboardTableView.ScoreData>(), -1);
                _currentLeaderboardRefreshId = refreshId;
                if (_uploadDaemon.uploading) { return; }
                if (!activated) { return; }

                if (scope == ScoreSaberScoresScope.Player) {
                    _upButton.interactable = false;
                    _downButton.interactable = false;
                } else {
                    _upButton.interactable = true;
                    _downButton.interactable = true;
                }

                ByeImages();
                _errorText.gameObject.SetActive(false);
                loadingControl.SetActive(true);

                if (cancellationToken != null) {
                    cancellationToken.Cancel();
                    cancellationToken.Dispose();
                }
                cancellationToken = new CancellationTokenSource();

                if (_playerService.loginStatus == PlayerService.LoginStatus.Error) {
                    SetErrorState(tableView, loadingControl, null, null, "ScoreSaber authentication failed, please restart Beat Saber", false);
                    ByeImages();
                    return;
                }

                if (_playerService.loginStatus != PlayerService.LoginStatus.Success) {
                    return;
                }


                await Task.Delay(500); // Delay before doing anything to prevent leaderboard spam

                Plugin.Log.Info("AFTER TASK DELAY");
                if (_currentLeaderboardRefreshId == refreshId) {
                    int maxMultipliedScore = await _maxScoreCache.GetMaxScore(beatmapLevel, beatmapKey);
                    LeaderboardMap leaderboardData = await _leaderboardService.GetLeaderboardData(maxMultipliedScore, beatmapLevel, beatmapKey, scope, leaderboardPage, _playerDataModel.playerData.playerSpecificSettings);
                    Plugin.Log.Info("AFTER LB DATA");

                    if (_currentLeaderboardRefreshId != refreshId) {
                        return; // we need to check this again, since some time may have passed due to waiting for leaderboard data
                    }
                    Plugin.Log.Info("AFTER CHECK");

                    SetRankedStatus(leaderboardData.leaderboardInfoMap.leaderboardInfo);
                    List<LeaderboardTableView.ScoreData> leaderboardTableScoreData = leaderboardData.ToScoreData();
                    int playerScoreIndex = GetPlayerScoreIndex(leaderboardData);
                    if (leaderboardTableScoreData.Count != 0) {
                        if (scope == ScoreSaberScoresScope.Player && playerScoreIndex == -1) {
                            SetErrorState(tableView, loadingControl, null, null, "You haven't set a score on this leaderboard");
                        } else {
                            if (_currentLeaderboardRefreshId != refreshId) {
                                return; // we need to check this again, since some time may have passed due to waiting for leaderboard data
                            }
                            tableView.SetScores(leaderboardTableScoreData, playerScoreIndex);
                            Plugin.Log.Info("AFTER TABLE ST SCORE DELAY");
                            PatchLeaderboardTableView(tableView);
                            for (int i = 0; i < leaderboardTableScoreData.Count; i++) {
                                _ImageHolders[i].setProfileImage(leaderboardData.scores[i].score.leaderboardPlayerInfo.profilePicture, i, cancellationToken.Token);
                            }
                            loadingControl.gameObject.SetActive(false);
                            _errorText.gameObject.SetActive(false);
                            if (_uploadDaemon.uploading) {
                                _panelView.DismissPrompt();
                            }
                        }
                    } else {
                        if (leaderboardPage > 1) {
                            SetErrorState(tableView, loadingControl, null, null, "No scores on this page");
                        } else {
                            SetErrorState(tableView, loadingControl, null, null, "No scores on this leaderboard, be the first!");
                        }
                        ByeImages();
                    }
                }
            } catch (HttpErrorException httpError) {
                SetErrorState(tableView, loadingControl, httpError);
            } catch (Exception exception) {
                SetErrorState(tableView, loadingControl, null, exception);
            }
        }

        private void SetRankedStatus(LeaderboardInfo leaderboardInfo) {
            if (leaderboardInfo.ranked) {
                if (leaderboardInfo.positiveModifiers) {
                    _panelView.SetRankedStatus("Ranked (DA = +0.02, GN +0.04)");
                } else {
                    _panelView.SetRankedStatus("Ranked (modifiers disabled)");
                }
                return;
            }
            if (leaderboardInfo.qualified) {
                _panelView.SetRankedStatus("Qualified");
                return;
            }
            if (leaderboardInfo.loved) {
                _panelView.SetRankedStatus("Loved");
                return;
            }
            _panelView.SetRankedStatus("Unranked");
        }

        public int GetPlayerScoreIndex(LeaderboardMap leaderboardMap) {
            for (int i = 0; i < leaderboardMap.scores.Length; i++) {
                if (leaderboardMap.scores[i].score.leaderboardPlayerInfo.id == _playerService.localPlayerInfo.playerId) {
                    return i;
                }
            }
            return -1;
        }

        public void AllowReplayWatching(bool value) {

            _scoreDetailView.AllowReplayWatching(value);
        }

        private void SetErrorState(LeaderboardTableView tableView, GameObject loadingControl, HttpErrorException httpErrorException = null, Exception exception = null, string errorText = "Failed to load leaderboard, score won't upload", bool showRefreshButton = true) {

            if (httpErrorException != null) {
                if (httpErrorException.isNetworkError) {
                    errorText = "Failed to load leaderboard due to a network error, score won't upload";
                    _leaderboardService.currentLoadedLeaderboard = null;
                }
                if (httpErrorException.isScoreSaberError) {
                    errorText = httpErrorException.scoreSaberError.errorMessage;
                    if (errorText == "Leaderboard not found") {
                        _leaderboardService.currentLoadedLeaderboard = null;
                        _panelView.SetRankedStatus("");
                    }
                }
            }
            if (exception != null) {
                Plugin.Log.Error(exception.ToString());
            }
            loadingControl.gameObject.SetActive(false);
            _errorText.gameObject.SetActive(true);
            _errorText.text = errorText;
            tableView.SetScores(new List<LeaderboardTableView.ScoreData>(), -1);
            ByeImages();
        }

        public void DirectionalButtonClicked(bool down) {

            if (down) {
                leaderboardPage++;
            } else {
                leaderboardPage--;
            }
            RefreshLeaderboard();
            CheckPage();
        }

        public void ChangePageButtonsEnabledState(bool state) {

            if (state) {
                if (leaderboardPage > 1) {
                    _upButton.interactable = state;
                }
                _downButton.interactable = state;
            } else {
                _upButton.interactable = state;
                _downButton.interactable = state;
            }
        }

        public void CheckPage() {

            if (leaderboardPage > 0) {
                _upButton.interactable = true;
            } else {
                _upButton.interactable = false;
            }
        }

        public void RefreshLeaderboard() {

            if (!activated || _currentBeatmapKey == null)
                return;
            BeatmapLevel beatmapLevel = _beatmapLevelsModel.GetBeatmapLevel(_currentBeatmapKey.levelId);
            RefreshLeaderboard(beatmapLevel, _currentBeatmapKey, leaderboardTableView, currentScoreScope, loadingLB, Guid.NewGuid().ToString()).RunTask();
        }

        internal void ByeImages() {
            _ImageHolders.ForEach(holder => holder.ClearSprite());
        }

        private async Task StartReplay(ScoreMap score) {

            _parserParams.EmitEvent("close-modals");
            _replayDownloading = true;

            try {
                _panelView.SetPromptInfo("Downloading Replay...", true);
                byte[] replay = await _playerService.GetReplayData(score.parent.beatmapLevel, score.parent.beatmapKey, score.parent.leaderboardInfo.id, score);
                _panelView.SetPromptInfo("Replay downloaded! Unpacking...", true);
                await _replayLoader.Load(replay, score.parent.beatmapLevel, score.parent.beatmapKey, score.gameplayModifiers, score.score.leaderboardPlayerInfo.name);
                _panelView.SetPromptSuccess("Replay Started!", false, 1f);
            } catch (ReplayVersionException ex) {
                _panelView.SetPromptError("Unsupported replay version", false);
                Plugin.Log.Error($"Failed to start replay (unsupported version): {ex}");
            } catch (Exception ex) {
                _panelView.SetPromptError("Failed to start replay! Error written to log.", false);
                Plugin.Log.Error($"Failed to start replay: {ex}");
            }
            _replayDownloading = false;
        }

        private bool obtainedAnchor = false;
        private Vector2 normalAnchor = Vector2.zero;

        void PatchLeaderboardTableView(LeaderboardTableView tableView) {
            int i = 0;
            foreach (LeaderboardTableCell cell in tableView.GetComponentsInChildren<LeaderboardTableCell>()) {

                LeaderboardTableCell tableCell = (LeaderboardTableCell)cell;

                CellClicker cellClicker = _cellClickingHolders[i].cellClickerImage.gameObject.AddComponent<CellClicker>();
                cellClicker.onClick = _infoButtons.InfoButtonClicked;
                cellClicker.index = i;
                cellClicker.seperator = tableCell.GetField<Image, LeaderboardTableCell>("_separatorImage") as ImageView;

                TextMeshProUGUI _playerNameText = tableCell.GetField<TextMeshProUGUI, LeaderboardTableCell>("_playerNameText");

                if (!obtainedAnchor) {
                    normalAnchor = _playerNameText.rectTransform.anchoredPosition;
                    obtainedAnchor = true;
                }

                _playerNameText.richText = true;
                Vector2 newPosition = new Vector2(normalAnchor.x + 2.5f, 0f);
                _playerNameText.rectTransform.anchoredPosition = newPosition;
                tableCell.showSeparator = true;
                i++;
            }
        }

        public void Initialize() {
            _infoButtons = new EntryHolder();
            _scoreDetailView = new ScoreDetailView();
            _scoreDetailView.showProfile += scoreDetailView_showProfile;
            _scoreDetailView.startReplay += scoreDetailView_startReplay;
            _playerService.LoginStatusChanged += playerService_LoginStatusChanged;
            _infoButtons.infoButtonClicked += infoButtons_infoButtonClicked;
            _uploadDaemon.UploadStatusChanged += uploadDaemon_UploadStatusChanged;
        }

        public void Dispose() {

            _playerService.LoginStatusChanged -= playerService_LoginStatusChanged;
            _uploadDaemon.UploadStatusChanged -= uploadDaemon_UploadStatusChanged;
            _infoButtons.infoButtonClicked -= infoButtons_infoButtonClicked;
            _scoreDetailView.startReplay -= scoreDetailView_startReplay;
            _scoreDetailView.showProfile -= scoreDetailView_showProfile;
        }

        public void OnLeaderboardSet(BeatmapKey beatmapKey) {
            _currentBeatmapKey = beatmapKey;
            try {
                Plugin.Log.Notice("OnLeaderboardSet");
                BeatmapLevel beatmapLevel = _beatmapLevelsModel.GetBeatmapLevel(beatmapKey.levelId);
                Plugin.Log.Notice("Got beatmaplevel");
                RefreshLeaderboard(beatmapLevel, beatmapKey, leaderboardTableView, currentScoreScope, loadingLB, Guid.NewGuid().ToString()).RunTask();
            } catch(Exception ex) { Plugin.Log.Error(ex.Message); }
        }


        // probably a better place to put this
        public class CellClicker : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler {
            public Action<int> onClick;
            public int index;
            public ImageView seperator;
            public Vector3 originalScale;
            private bool isScaled = false;

            private Color origColour = new Color(1, 1, 1, 1);
            private Color origColour0 = new Color(1, 1, 1, 0.2509804f);
            private Color origColour1 = new Color(1, 1, 1, 0);

            private void Start() {
                originalScale = seperator.transform.localScale;
            }

            public void OnPointerClick(PointerEventData data) {
                BeatSaberUI.BasicUIAudioManager.HandleButtonClickEvent();
                onClick(index);
            }

            public void OnPointerEnter(PointerEventData eventData) {
                if (!isScaled) {
                    seperator.transform.localScale = originalScale * 1.8f;
                    isScaled = true;
                }

                Color targetColor = Color.white;
                Color targetColor0 = Color.white;
                Color targetColor1 = new Color(1, 1, 1, 0);

                float lerpDuration = 0.15f;

                StopAllCoroutines();
                StartCoroutine(LerpColors(seperator, seperator.color, targetColor, seperator.color0, targetColor0, seperator.color1, targetColor1, lerpDuration));
            }

            public void OnPointerExit(PointerEventData eventData) {
                if (isScaled) {
                    seperator.transform.localScale = originalScale;
                    isScaled = false;
                }

                float lerpDuration = 0.05f;

                StopAllCoroutines();
                StartCoroutine(LerpColors(seperator, seperator.color, origColour, seperator.color0, origColour0, seperator.color1, origColour1, lerpDuration));
            }


            private IEnumerator LerpColors(ImageView target, Color startColor, Color endColor, Color startColor0, Color endColor0, Color startColor1, Color endColor1, float duration) {
                float elapsedTime = 0f;
                while (elapsedTime < duration) {
                    float t = elapsedTime / duration;
                    target.color = Color.Lerp(startColor, endColor, t);
                    target.color0 = Color.Lerp(startColor0, endColor0, t);
                    target.color1 = Color.Lerp(startColor1, endColor1, t);
                    elapsedTime += Time.deltaTime;
                    yield return null;
                }
                target.color = endColor;
                target.color0 = endColor0;
                target.color1 = endColor1;
            }

            private void OnDestroy() {
                StopAllCoroutines();
                onClick = null;
                seperator.color = origColour;
                seperator.color0 = origColour0;
                seperator.color1 = origColour1;
            }
        }
    }
}