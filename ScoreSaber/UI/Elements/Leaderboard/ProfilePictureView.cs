﻿using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using IPA.Utilities.Async;
using ScoreSaber.UI.Leaderboard;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Tweening;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;
#pragma warning disable CS0618 // Type or member is obsolete

namespace ScoreSaber.UI.Elements.Leaderboard {
    internal class ProfilePictureView {
        
        private readonly int index;

        public bool isLoading = false;

        private ICoroutineStarter coroutineStarter;

        internal Sprite nullSprite => Utilities.FindSpriteInAssembly("ScoreSaber.Resources.blank.png");

        public ProfilePictureView(int index) {
            this.index = index;
        }

        [Inject]
        public void Init(ICoroutineStarter coroutineStarter) {
            this.coroutineStarter = coroutineStarter;
        }

        [UIComponent("profileImage")]
        public ImageView profileImage = null;

        [UIObject("profileloading")]
        public GameObject loadingIndicator = null;

        [UIAction("#post-parse")]
        public void Parsed() {
            profileImage.material = Plugin.NoGlowMatRound;
            profileImage.sprite = Utilities.FindSpriteInAssembly("ScoreSaber.Resources.blank.png");
            profileImage.gameObject.SetActive(true);
            loadingIndicator.gameObject.SetActive(false);
            Active(false);
        }

        public void setProfileImage(string url, int pos, CancellationToken cancellationToken) {
            try {
                cancellationToken.ThrowIfCancellationRequested();
                if (SpriteCache.cachedSprites.ContainsKey(url)) {
                    profileImage.gameObject.SetActive(true);
                    profileImage.sprite = SpriteCache.cachedSprites[url];
                    loadingIndicator.gameObject.SetActive(false);
                    return;
                }
                loadingIndicator.gameObject.SetActive(true);
                coroutineStarter.StartCoroutine(GetSpriteAvatar(url, OnAvatarDownloadSuccess, OnAvatarDownloadFailure, cancellationToken, pos));
            } catch (OperationCanceledException) {
                OnAvatarDownloadFailure("Cancelled", pos, cancellationToken);
            } finally {
                SpriteCache.MaintainSpriteCache();
            }
        }

        internal static IEnumerator GetSpriteAvatar(string url, Action<Sprite, int, string, CancellationToken> onSuccess, Action<string, int, CancellationToken> onFailure, CancellationToken cancellationToken, int pos) {
            var handler = new DownloadHandlerTexture();
            var www = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
            www.downloadHandler = handler;
            yield return www.SendWebRequest();

            while (!www.isDone) {
                if (cancellationToken.IsCancellationRequested) {
                    onFailure?.Invoke("Cancelled", pos, cancellationToken);
                    yield break;
                }
                yield return null;
            }

            if (www.result == UnityWebRequest.Result.ProtocolError || www.result == UnityWebRequest.Result.ConnectionError) {
                onFailure?.Invoke(www.error, pos, cancellationToken);
                yield break;
            }
            if (!string.IsNullOrEmpty(www.error)) {
                onFailure?.Invoke(www.error, pos, cancellationToken);
                yield break;
            }

            Sprite sprite = Sprite.Create(handler.texture, new Rect(0, 0, handler.texture.width, handler.texture.height), Vector2.one * 0.5f);
            onSuccess?.Invoke(sprite, pos, url, cancellationToken);
            yield break;
        }

        internal void OnAvatarDownloadSuccess(Sprite a, int pos, string url, CancellationToken cancellationToken) {
            SpriteCache.AddSpriteToCache(url, a);
            if (cancellationToken != null) {
                if (cancellationToken.IsCancellationRequested) {
                    return;
                }
            }
            profileImage.gameObject.SetActive(true);
            profileImage.sprite = a;
            loadingIndicator.gameObject.SetActive(false);
        }

        internal void OnAvatarDownloadFailure(string error, int pos, CancellationToken cancellationToken) {
            if (cancellationToken != null) {
                if (cancellationToken.IsCancellationRequested) {
                    return;
                }
            }
            ClearSprite();
        }
        
        public void ClearSprite() {
            if (profileImage != null) {
                profileImage.sprite = nullSprite;
            }
            if(loadingIndicator != null) {
                loadingIndicator.gameObject.SetActive(false);
            }
        }

        public void Active(bool state) {
            if (profileImage != null) {
                profileImage.gameObject.SetActive(state);
            }
        }
    }

    internal static class SpriteCache {
        internal static Dictionary<string, Sprite> cachedSprites = new Dictionary<string, Sprite>();
        private static int MaxSpriteCacheSize = 150;
        internal static Queue<string> spriteCacheQueue = new Queue<string>();
        internal static void MaintainSpriteCache() {
            while (cachedSprites.Count > MaxSpriteCacheSize) {
                string oldestUrl = spriteCacheQueue.Dequeue();
                cachedSprites.Remove(oldestUrl);
            }
        }

        internal static void AddSpriteToCache(string url, Sprite sprite) {
            if(cachedSprites.ContainsKey(url)) {
                return;
            }
            cachedSprites.Add(url, sprite);
            spriteCacheQueue.Enqueue(url);
        }
    }

    internal class TweeningService {
        [Inject] private TimeTweeningManager _tweeningManager = null;
        private HashSet<Transform> activeRotationTweens = new HashSet<Transform>();

        public void RotateTransform(Transform transform, float rotationAmount, float time, Action callback = null) {
            if (activeRotationTweens.Contains(transform)) return;
            float startRotation = transform.rotation.eulerAngles.z;
            float endRotation = startRotation + rotationAmount;

            Tween tween = new FloatTween(startRotation, endRotation, (float u) => {
                transform.rotation = Quaternion.Euler(0f, 0f, u);
            }, 0.1f, EaseType.Linear, 0f);
            tween.onCompleted = () => {
                callback?.Invoke();
                activeRotationTweens.Remove(transform);
            };
            tween.onKilled = () => {
                if (transform != null) transform.rotation = Quaternion.Euler(0f, 0f, endRotation);
                callback?.Invoke();
                activeRotationTweens.Remove(transform);
            };
            activeRotationTweens.Add(transform);
            _tweeningManager.AddTween(tween, transform);
        }

        public void FadeText(TextMeshProUGUI text, bool fadeIn, float time) {
            float startAlpha = fadeIn ? 0f : 1f;
            float endAlpha = fadeIn ? 1f : 0f;

            Tween tween = new FloatTween(startAlpha, endAlpha, (float u) => {
                text.color = text.color.ColorWithAlpha(u);
            }, 0.4f, EaseType.Linear, 0f);
            tween.onCompleted = () => {
                if (text == null) return;
                text.gameObject.SetActive(fadeIn);
            };
            tween.onKilled = () => {
                if (text == null) return;
                text.gameObject.SetActive(fadeIn);
                text.color = text.color.ColorWithAlpha(endAlpha);
            };
            text.gameObject.SetActive(true);
            _tweeningManager.AddTween(tween, text);
        }

        public void LerpColor(ImageView currentImageView, Color newColor, float time = 0.0f) {

            Tween tween = new ColorTween(currentImageView.color, newColor, (Color u) => {
                currentImageView.color = u;
                currentImageView.color0 = u;
                currentImageView.color1 = u;
            }, time == 0.0f ? 0.3f : time, EaseType.Linear, 0f);
            tween.onCompleted = () => {
                if (currentImageView == null) return;
                currentImageView.color = newColor;
                currentImageView.color0 = newColor;
                currentImageView.color1 = newColor;
            };
            tween.onKilled = () => {
                if (currentImageView == null) return;
                currentImageView.color = newColor;
                currentImageView.color0 = newColor;
                currentImageView.color1 = newColor;
            };
            _tweeningManager.AddTween(tween, currentImageView);
        }
    }
}
