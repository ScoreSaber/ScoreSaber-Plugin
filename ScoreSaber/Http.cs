﻿using Newtonsoft.Json;
using ScoreSaber.Core.Data.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ScoreSaber
{
    /// <summary>
    /// HTTP Options
    /// </summary>
    internal struct HttpOptions
    {
        /// <summary>
        /// Application Name
        /// </summary>
        public string applicationName { get; set; }

        /// <summary>
        /// Application Version
        /// </summary>
        public Version version { get; set; }

        /// <summary>
        /// The BaseURL
        /// </summary>
        public string baseURL { get; set; }

    }

    internal sealed class Http
    {
        internal Dictionary<string, string> PersistentRequestHeaders { get; private set; }
        internal HttpOptions options;

        internal Http(HttpOptions _options = new HttpOptions()) {

            options = _options;
            PersistentRequestHeaders = new Dictionary<string, string>();

            if ((_options.applicationName != null && _options.version == null) || (_options.applicationName == null && _options.version != null)) {
                throw new ArgumentException("You must specify either both or none of ApplicationName and Version");
            }

            string libVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string userAgent = $"Default/{libVersion}";

            if (_options.applicationName != null) {
                userAgent = $"{_options.applicationName}/{_options.version}";
            }

            PersistentRequestHeaders.Add("User-Agent", userAgent);
        }

        internal async Task SendHttpAsyncRequest(UnityWebRequest request) {

            foreach (var header in PersistentRequestHeaders) {
                request.SetRequestHeader(header.Key, header.Value);
            }

            AsyncOperation asyncOperation = request.SendWebRequest();
            while (!asyncOperation.isDone) {
                await Task.Delay(100);
            }
        }

        internal async Task<string> GetRawAsync(string url, string protocolAndSubdomain = "https://") {

            using (UnityWebRequest request = UnityWebRequest.Get(protocolAndSubdomain + url)) {
                request.timeout = 5;
                await SendHttpAsyncRequest(request);
                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError) {
                    throw ThrowHttpException(request);
                } else {
                    return Encoding.UTF8.GetString(request.downloadHandler.data);
                }
            }
        }

        internal async Task<string> GetAsync(string url, string protocolAndSubdomain = "https://") {

            url = $"{protocolAndSubdomain}{options.baseURL}{url}";
            using (UnityWebRequest request = UnityWebRequest.Get(url)) {
                request.timeout = 5;
                await SendHttpAsyncRequest(request);
                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError) {
                    throw ThrowHttpException(request);
                } else {
                    return Encoding.UTF8.GetString(request.downloadHandler.data);
                }
            }
        }

        internal async Task<byte[]> DownloadAsync(string url, string protocolAndSubdomain = "https://") {

            url = $"{protocolAndSubdomain}{options.baseURL}{url}";
            using (UnityWebRequest request = UnityWebRequest.Get(url)) {
                await SendHttpAsyncRequest(request);
                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError) {
                    throw ThrowHttpException(request);
                } else {
                    return request.downloadHandler.data;
                }
            }
        }

        internal async Task<string> PostAsync(string url, WWWForm form, string protocolAndSubdomain = "https://") {

            url = $"{protocolAndSubdomain}{options.baseURL}{url}";
            using (UnityWebRequest request = UnityWebRequest.Post(url, form)) {
                request.timeout = 120;
                await SendHttpAsyncRequest(request);
                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError) {
                    throw ThrowHttpException(request);
                } else {
                    return Encoding.UTF8.GetString(request.downloadHandler.data);
                }
            }
        }

        internal HttpErrorException ThrowHttpException(UnityWebRequest request) {
            if (request.downloadHandler.data != null) {
                return new HttpErrorException(
                    request.result == UnityWebRequest.Result.ConnectionError,
                    request.result == UnityWebRequest.Result.ProtocolError,
                    Encoding.UTF8.GetString(request.downloadHandler.data)
                ); // Epic
            } else {
                return new HttpErrorException(
                    request.result == UnityWebRequest.Result.ConnectionError,
                    request.result == UnityWebRequest.Result.ProtocolError
                ); // Epic
            }
        }
    }

    internal class HttpErrorException : Exception {
        internal bool isNetworkError { get; set; }
        internal bool isHttpError { get; set; }
        internal bool isScoreSaberError { get; set; }
        internal ScoreSaberError scoreSaberError { get; set; }
        internal HttpErrorException(bool _isNetworkError, bool _isHttpError, string _scoreSaberErrorMessage = "") {
            isNetworkError = _isNetworkError;
            isHttpError = _isHttpError;
            if (_scoreSaberErrorMessage != string.Empty) {
                try {
                    scoreSaberError = JsonConvert.DeserializeObject<ScoreSaberError>(_scoreSaberErrorMessage);
                    isScoreSaberError = true;
                } catch (Exception) { }
            }
        }
    }
}