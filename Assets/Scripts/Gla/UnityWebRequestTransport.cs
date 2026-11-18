using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace FlipNflop.Gla
{
    /// <summary>
    /// <see cref="IHttpTransport"/> over <see cref="UnityWebRequest"/>, which uses the browser
    /// <c>fetch</c> on WebGL. Each request runs in a coroutine of the host (<see cref="GlaServices"/>,
    /// kept across scenes by <c>DontDestroyOnLoad</c>), which aborts it after the timeout measured in
    /// real time, so that the pause (<c>Time.timeScale = 0</c>) does not stop it.
    /// </summary>
    public sealed class UnityWebRequestTransport : IHttpTransport
    {
        #region Constants

        private const string JsonContentType = "application/json";

        #endregion

        #region Fields

        private readonly MonoBehaviour host;

        #endregion

        #region Construction

        /// <summary>
        /// Creates the transport.
        /// </summary>
        /// <param name="host">The component that runs the coroutines of the requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="host"/> is <c>null</c>.</exception>
        public UnityWebRequestTransport(MonoBehaviour host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            this.host = host;
        }

        #endregion

        #region IHttpTransport

        /// <summary>
        /// Starts the coroutine that posts the JSON. Interacts with <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/>.
        /// </summary>
        /// <param name="url">The absolute address.</param>
        /// <param name="json">The request body.</param>
        /// <param name="bearerToken">The session token, or <c>null</c>.</param>
        /// <param name="timeoutSeconds">The timeout.</param>
        /// <param name="onCompleted">Receives the result.</param>
        public void PostJson(string url, string json, string bearerToken, int timeoutSeconds, Action<HttpResult> onCompleted)
        {
            if (onCompleted == null) throw new ArgumentNullException(nameof(onCompleted));

            host.StartCoroutine(Send(url, json, bearerToken, timeoutSeconds, onCompleted));
        }

        #endregion

        #region Request

        /// <summary>
        /// Posts the JSON, waits for the answer or the timeout, and reports the result. The body of an
        /// error answer (4xx, 5xx) is kept, because it carries the <c>ApiError</c>.
        /// </summary>
        /// <param name="url">The absolute address.</param>
        /// <param name="json">The request body.</param>
        /// <param name="bearerToken">The session token, or <c>null</c>.</param>
        /// <param name="timeoutSeconds">The timeout.</param>
        /// <param name="onCompleted">Receives the result.</param>
        /// <returns>The coroutine.</returns>
        private static IEnumerator Send(string url, string json, string bearerToken, int timeoutSeconds, Action<HttpResult> onCompleted)
        {
            HttpResult result;
            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json ?? string.Empty)) { contentType = JsonContentType };
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", JsonContentType);
                request.SetRequestHeader("Accept", JsonContentType);
                if (!string.IsNullOrEmpty(bearerToken))
                    request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
                request.timeout = timeoutSeconds;

                float startedAt = Time.realtimeSinceStartup;
                bool timedOut = false;
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (Time.realtimeSinceStartup - startedAt >= timeoutSeconds)
                    {
                        timedOut = true;
                        request.Abort();
                        break;
                    }

                    yield return null;
                }

                result = ToResult(request, timedOut, Time.realtimeSinceStartup - startedAt >= timeoutSeconds);
            }

            try
            {
                onCompleted(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Converts the finished request into an <see cref="HttpResult"/>.
        /// </summary>
        /// <param name="request">The finished or aborted request.</param>
        /// <param name="timedOut">Whether the coroutine aborted it.</param>
        /// <param name="reachedTimeout">Whether the time of the timeout passed, which tells a native timeout from other network failures.</param>
        /// <returns>The result.</returns>
        private static HttpResult ToResult(UnityWebRequest request, bool timedOut, bool reachedTimeout)
        {
            if (timedOut)
                return HttpResult.Failed(HttpFailure.Timeout, "no answer within the timeout");

            if (request.result == UnityWebRequest.Result.Success || request.result == UnityWebRequest.Result.ProtocolError)
            {
                string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                return HttpResult.Answered(request.responseCode, body);
            }

            HttpFailure failure = reachedTimeout ? HttpFailure.Timeout : HttpFailure.Network;
            return HttpResult.Failed(failure, request.error);
        }

        #endregion
    }
}
