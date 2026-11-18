namespace FlipNflop.Gla
{
    /// <summary>
    /// How an HTTP exchange failed before an answer of the server arrived.
    /// </summary>
    public enum HttpFailure
    {
        None,
        Network,
        Timeout,
    }

    /// <summary>
    /// The raw outcome of one HTTP exchange of an <see cref="IHttpTransport"/>: the status code and
    /// the body of the answer, or the failure that prevented it. Converted into an
    /// <see cref="ApiResponse"/> by <see cref="GlaApiClient"/>.
    /// </summary>
    public sealed class HttpResult
    {
        #region Construction

        /// <summary>
        /// Creates a result; use <see cref="Answered"/> or <see cref="Failed"/>.
        /// </summary>
        /// <param name="statusCode">The HTTP status, or 0 without an answer.</param>
        /// <param name="body">The answer body, or <c>null</c>.</param>
        /// <param name="failure">The failure, or <see cref="HttpFailure.None"/>.</param>
        /// <param name="error">The description of the failure, for the log.</param>
        private HttpResult(long statusCode, string body, HttpFailure failure, string error)
        {
            StatusCode = statusCode;
            Body = body;
            Failure = failure;
            Error = error;
        }

        /// <summary>
        /// A result with an answer of the server, whatever its status.
        /// </summary>
        /// <param name="statusCode">The HTTP status.</param>
        /// <param name="body">The answer body, possibly empty.</param>
        /// <returns>The result.</returns>
        public static HttpResult Answered(long statusCode, string body)
        {
            return new HttpResult(statusCode, body ?? string.Empty, HttpFailure.None, null);
        }

        /// <summary>
        /// A result without an answer: network error (including CORS refusals, which the browser
        /// hides) or timeout.
        /// </summary>
        /// <param name="failure">The failure.</param>
        /// <param name="error">The description of the failure, for the log.</param>
        /// <returns>The result.</returns>
        public static HttpResult Failed(HttpFailure failure, string error)
        {
            return new HttpResult(0, null, failure == HttpFailure.None ? HttpFailure.Network : failure, error);
        }

        #endregion

        #region Properties

        public long StatusCode { get; }

        public string Body { get; }

        public HttpFailure Failure { get; }

        public string Error { get; }

        #endregion
    }
}
