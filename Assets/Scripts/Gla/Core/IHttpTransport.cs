using System;

namespace FlipNflop.Gla
{
    /// <summary>
    /// Sends one JSON <c>POST</c> and reports its <see cref="HttpResult"/>. Implemented in the game by
    /// <c>UnityWebRequestTransport</c> (<c>UnityWebRequest</c>, the browser <c>fetch</c> on WebGL),
    /// and in the tests by a fake. Used by <see cref="GlaApiClient"/>.
    /// </summary>
    public interface IHttpTransport
    {
        /// <summary>
        /// Sends <paramref name="json"/> as <c>application/json</c> to <paramref name="url"/>.
        /// <paramref name="onCompleted"/> is called exactly once, on the main thread, possibly
        /// before the method returns.
        /// </summary>
        /// <param name="url">The absolute address.</param>
        /// <param name="json">The request body.</param>
        /// <param name="bearerToken">The session token for <c>Authorization: Bearer</c>, or <c>null</c>.</param>
        /// <param name="timeoutSeconds">After this time without an answer, the exchange ends with <see cref="HttpFailure.Timeout"/>.</param>
        /// <param name="onCompleted">Receives the result.</param>
        void PostJson(string url, string json, string bearerToken, int timeoutSeconds, Action<HttpResult> onCompleted);
    }
}
