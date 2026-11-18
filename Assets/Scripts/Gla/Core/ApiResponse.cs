using FlipNflop.Commons.Errors;

namespace FlipNflop.Gla
{
    /// <summary>
    /// The kind of an answer of the API, as seen by the game.
    /// </summary>
    public enum ApiResponseKind
    {
        /// <summary>A 2xx status, with the expected body when the route has one.</summary>
        Success,

        /// <summary>A non-2xx status with the single error body (<see cref="ApiError"/>).</summary>
        ApiError,

        /// <summary>No answer: the connection failed, or the browser refused it (CORS).</summary>
        NetworkError,

        /// <summary>No answer within <see cref="GlaApiClient.TimeoutSeconds"/>.</summary>
        Timeout,

        /// <summary>An answer that breaks the contract: a 2xx with an unreadable body, or an error without <see cref="ApiError"/>.</summary>
        UnexpectedResponse,

        /// <summary>No request was made: the state of the game does not allow it, or the body could not be written.</summary>
        NotSent,
    }

    /// <summary>
    /// An answer of the API converted by <see cref="GlaApiClient"/>: its kind, the HTTP status and,
    /// for an error, the <see cref="ApiError"/> read with <c>GlaJson</c>, whose <c>message</c>, in
    /// Portuguese, is what the e-mail flow shows. Never carries the session token or the credential
    /// in <see cref="Problem"/> or <see cref="ToString"/>.
    /// </summary>
    public class ApiResponse
    {
        #region Construction

        /// <summary>
        /// Creates an answer.
        /// </summary>
        /// <param name="kind">The kind.</param>
        /// <param name="statusCode">The HTTP status, or 0 without an answer.</param>
        /// <param name="error">The error body, or <c>null</c>.</param>
        /// <param name="problem">What went wrong without an error body, for the log, or <c>null</c>.</param>
        internal ApiResponse(ApiResponseKind kind, long statusCode, ApiError error, string problem)
        {
            Kind = kind;
            StatusCode = statusCode;
            Error = error;
            Problem = problem;
        }

        #endregion

        #region Properties

        public ApiResponseKind Kind { get; }

        public long StatusCode { get; }

        public ApiError Error { get; }

        /// <summary>
        /// What went wrong when there is no error body (network failure, timeout, unreadable answer), for the log.
        /// </summary>
        public string Problem { get; }

        public bool IsSuccess => Kind == ApiResponseKind.Success;

        /// <summary>
        /// The <c>error</c> code of the body (one of <see cref="ApiErrorCodes"/>), or <c>null</c>.
        /// </summary>
        public string ErrorCode => Error?.Error;

        /// <summary>
        /// Whether trying again later may succeed: network error, timeout, 429 or 5xx.
        /// </summary>
        public bool IsTransientFailure =>
            Kind == ApiResponseKind.NetworkError
            || Kind == ApiResponseKind.Timeout
            || (Kind != ApiResponseKind.Success && (StatusCode == 429 || StatusCode >= 500));

        #endregion

        #region Public API

        /// <summary>
        /// Describes the answer for the log, such as <c>201</c>, <c>400 invalid_request</c> or <c>timeout</c>.
        /// </summary>
        /// <returns>The description.</returns>
        public override string ToString()
        {
            switch (Kind)
            {
                case ApiResponseKind.Success:
                    return StatusCode.ToString();
                case ApiResponseKind.ApiError:
                    return $"{StatusCode} {ErrorCode}";
                case ApiResponseKind.NetworkError:
                    return string.IsNullOrEmpty(Problem) ? "network error" : $"network error ({Problem})";
                case ApiResponseKind.Timeout:
                    return "timeout";
                case ApiResponseKind.UnexpectedResponse:
                    return $"unexpected answer {StatusCode} ({Problem})";
                default:
                    return string.IsNullOrEmpty(Problem) ? "not sent" : $"not sent ({Problem})";
            }
        }

        #endregion
    }

    /// <summary>
    /// An <see cref="ApiResponse"/> of a route whose success has a body, read with <c>GlaJson</c>.
    /// </summary>
    /// <typeparam name="T">The contract type of the success body, such as <c>SessionOpened</c>.</typeparam>
    public sealed class ApiResponse<T> : ApiResponse where T : class
    {
        #region Construction

        /// <summary>
        /// Creates an answer.
        /// </summary>
        /// <param name="kind">The kind.</param>
        /// <param name="statusCode">The HTTP status, or 0 without an answer.</param>
        /// <param name="value">The success body, or <c>null</c>.</param>
        /// <param name="error">The error body, or <c>null</c>.</param>
        /// <param name="problem">What went wrong without an error body, or <c>null</c>.</param>
        internal ApiResponse(ApiResponseKind kind, long statusCode, T value, ApiError error, string problem)
            : base(kind, statusCode, error, problem)
        {
            Value = value;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The success body; <c>null</c> unless <see cref="ApiResponse.IsSuccess"/>.
        /// </summary>
        public T Value { get; }

        #endregion
    }
}
