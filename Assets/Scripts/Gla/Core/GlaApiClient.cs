using System;
using FlipNflop.Commons.Errors;
using FlipNflop.Commons.Messages;
using FlipNflop.Gla.Serialization;
using Newtonsoft.Json;

namespace FlipNflop.Gla
{
    /// <summary>
    /// HTTP client of the GLA API routes used by the game. Writes the request bodies and reads the
    /// answers with <see cref="GlaJson"/> (the types of FlipNflopCommons), sends them through an
    /// <see cref="IHttpTransport"/> with a timeout of <see cref="TimeoutSeconds"/>, and converts every
    /// outcome into an <see cref="ApiResponse"/>, reading the single error body as <see cref="ApiError"/>.
    /// Created by <c>GlaServices</c> when the configuration turns the collection on; used by
    /// <see cref="PlayerSession"/> and by the attempt delivery (gla-15, <see cref="PostJsonAsync"/>).
    /// </summary>
    public sealed class GlaApiClient
    {
        #region Constants

        public const int TimeoutSeconds = 10;
        public const string CodeRequestsPath = "/identification/code-requests";
        public const string CodeVerificationsPath = "/identification/code-verifications";
        public const string SessionsPath = "/sessions";

        #endregion

        #region Fields

        private readonly IHttpTransport transport;

        #endregion

        #region Construction

        /// <summary>
        /// Creates the client of one API address.
        /// </summary>
        /// <param name="baseUrl">The API address, as in <see cref="GlaConfig.ApiBaseUrl"/>; final slashes are removed.</param>
        /// <param name="transport">Sends the requests.</param>
        /// <exception cref="ArgumentException"><paramref name="baseUrl"/> is not an absolute http or https address.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="transport"/> is <c>null</c>.</exception>
        public GlaApiClient(string baseUrl, IHttpTransport transport)
        {
            if (!GlaConfig.TryNormalizeBaseUrl(baseUrl, out string normalized))
                throw new ArgumentException("The API address must be an absolute http or https URL.", nameof(baseUrl));

            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            BaseUrl = normalized;
        }

        #endregion

        #region Properties

        public string BaseUrl { get; }

        #endregion

        #region Public API

        /// <summary>
        /// The absolute address of a route of the API.
        /// </summary>
        /// <param name="path">The route, such as <see cref="SessionsPath"/>.</param>
        /// <returns>The address.</returns>
        public string UrlFor(string path)
        {
            if (string.IsNullOrEmpty(path)) return BaseUrl;
            return path[0] == '/' ? BaseUrl + path : BaseUrl + "/" + path;
        }

        /// <summary>
        /// <c>POST /identification/code-requests</c> with a <see cref="CodeRequest"/>: asks for a code
        /// sent to the e-mail. Success is 204; the errors are <c>invalid_email</c>,
        /// <c>domain_not_allowed</c> (400), <c>rate_limited</c> (429) and <c>internal_error</c> (500).
        /// </summary>
        /// <param name="email">The e-mail as typed; the API normalizes it.</param>
        /// <returns>The answer.</returns>
        public GlaOperation<ApiResponse> RequestCodeAsync(string email)
        {
            return PostWithoutResult(CodeRequestsPath, new CodeRequest { Email = email ?? string.Empty }, null);
        }

        /// <summary>
        /// <c>POST /identification/code-verifications</c> with a <see cref="CodeVerification"/>.
        /// Success is 200 with a <see cref="CodeVerificationResult"/>; the errors are
        /// <c>invalid_code</c>, <c>expired_code</c> (400) and <c>rate_limited</c> (429).
        /// </summary>
        /// <param name="email">The e-mail of the code request.</param>
        /// <param name="code">The code, as text.</param>
        /// <returns>The answer, with the credential on success.</returns>
        public GlaOperation<ApiResponse<CodeVerificationResult>> VerifyCodeAsync(string email, string code)
        {
            var body = new CodeVerification { Email = email ?? string.Empty, Code = code ?? string.Empty };
            return PostWithResult<CodeVerificationResult>(CodeVerificationsPath, body);
        }

        /// <summary>
        /// <c>POST /sessions</c> with a <see cref="SessionOpening"/>. Success is 201 with a
        /// <see cref="SessionOpened"/>; the errors are <c>invalid_request</c> (400, including a
        /// <c>started_at</c> ahead of the server clock) and <c>invalid_credential</c> (401).
        /// </summary>
        /// <param name="opening">The opening: game version, credential (or <c>null</c> for a guest) and start.</param>
        /// <returns>The answer, with the session token on success.</returns>
        public GlaOperation<ApiResponse<SessionOpened>> OpenSessionAsync(SessionOpening opening)
        {
            return PostWithResult<SessionOpened>(SessionsPath, opening);
        }

        /// <summary>
        /// <c>POST</c> of a JSON already written, with an optional <c>Authorization: Bearer</c>, for a
        /// route whose success has no body, such as the attempt record (<c>POST /attempts</c>, gla-15),
        /// whose resending must carry exactly the same bytes.
        /// </summary>
        /// <param name="path">The route.</param>
        /// <param name="json">The body.</param>
        /// <param name="bearerToken">The session token, or <c>null</c>.</param>
        /// <returns>The answer.</returns>
        public GlaOperation<ApiResponse> PostJsonAsync(string path, string json, string bearerToken)
        {
            var operation = new GlaOperation<ApiResponse>();
            if (json == null)
            {
                operation.Complete(new ApiResponse(ApiResponseKind.NotSent, 0, null, "the body is null"));
                return operation;
            }

            Send(path, json, bearerToken, result => operation.Complete(ToResponse(result)));
            return operation;
        }

        #endregion

        #region Request Helpers

        /// <summary>
        /// Writes a contract body with <see cref="GlaJson"/> and posts it to a route without a success body.
        /// </summary>
        /// <param name="path">The route.</param>
        /// <param name="body">The contract object.</param>
        /// <param name="bearerToken">The session token, or <c>null</c>.</param>
        /// <returns>The answer.</returns>
        private GlaOperation<ApiResponse> PostWithoutResult(string path, object body, string bearerToken)
        {
            if (!TrySerialize(body, out string json, out string problem))
                return GlaOperation<ApiResponse>.FromResult(new ApiResponse(ApiResponseKind.NotSent, 0, null, problem));

            return PostJsonAsync(path, json, bearerToken);
        }

        /// <summary>
        /// Writes a contract body with <see cref="GlaJson"/>, posts it, and reads the success body as <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The contract type of the success body.</typeparam>
        /// <param name="path">The route.</param>
        /// <param name="body">The contract object.</param>
        /// <returns>The answer.</returns>
        private GlaOperation<ApiResponse<T>> PostWithResult<T>(string path, object body) where T : class
        {
            var operation = new GlaOperation<ApiResponse<T>>();
            if (!TrySerialize(body, out string json, out string problem))
            {
                operation.Complete(new ApiResponse<T>(ApiResponseKind.NotSent, 0, null, null, problem));
                return operation;
            }

            Send(path, json, null, result => operation.Complete(ToResponse<T>(result)));
            return operation;
        }

        /// <summary>
        /// Hands a request to the transport; a transport that throws counts as a network error.
        /// </summary>
        /// <param name="path">The route.</param>
        /// <param name="json">The body.</param>
        /// <param name="bearerToken">The session token, or <c>null</c>.</param>
        /// <param name="onCompleted">Receives the raw result exactly once.</param>
        private void Send(string path, string json, string bearerToken, Action<HttpResult> onCompleted)
        {
            bool completed = false;
            void CompleteOnce(HttpResult result)
            {
                if (completed) return;
                completed = true;
                onCompleted(result ?? HttpResult.Failed(HttpFailure.Network, "the transport gave no result"));
            }

            try
            {
                transport.PostJson(UrlFor(path), json, bearerToken, TimeoutSeconds, CompleteOnce);
            }
            catch (Exception exception) when (!completed)
            {
                CompleteOnce(HttpResult.Failed(HttpFailure.Network, exception.Message));
            }
        }

        /// <summary>
        /// Writes a contract object with <see cref="GlaJson"/>.
        /// </summary>
        /// <param name="body">The object.</param>
        /// <param name="json">The JSON, or <c>null</c>.</param>
        /// <param name="problem">Why it could not be written, or <c>null</c>.</param>
        /// <returns><c>true</c> when the object was written.</returns>
        private static bool TrySerialize(object body, out string json, out string problem)
        {
            try
            {
                json = GlaJson.Serialize(body);
                problem = null;
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
            {
                json = null;
                problem = $"the body could not be written ({exception.Message})";
                return false;
            }
        }

        #endregion

        #region Response Conversion

        /// <summary>
        /// Converts the raw result of a route without a success body.
        /// </summary>
        /// <param name="result">The raw result.</param>
        /// <returns>The answer.</returns>
        private static ApiResponse ToResponse(HttpResult result)
        {
            if (TryConvertFailure(result, out ApiResponseKind failureKind))
                return new ApiResponse(failureKind, 0, null, result.Error);

            if (IsSuccessStatus(result.StatusCode))
                return new ApiResponse(ApiResponseKind.Success, result.StatusCode, null, null);

            ApiError error = ReadError(result, out string problem);
            return error != null
                ? new ApiResponse(ApiResponseKind.ApiError, result.StatusCode, error, null)
                : new ApiResponse(ApiResponseKind.UnexpectedResponse, result.StatusCode, null, problem);
        }

        /// <summary>
        /// Converts the raw result of a route whose success body is <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The contract type of the success body.</typeparam>
        /// <param name="result">The raw result.</param>
        /// <returns>The answer.</returns>
        private static ApiResponse<T> ToResponse<T>(HttpResult result) where T : class
        {
            if (TryConvertFailure(result, out ApiResponseKind failureKind))
                return new ApiResponse<T>(failureKind, 0, null, null, result.Error);

            if (IsSuccessStatus(result.StatusCode))
            {
                try
                {
                    T value = GlaJson.Deserialize<T>(result.Body ?? string.Empty);
                    return new ApiResponse<T>(ApiResponseKind.Success, result.StatusCode, value, null, null);
                }
                catch (JsonException exception)
                {
                    string problem = $"the body is not a {typeof(T).Name}: {exception.Message}";
                    return new ApiResponse<T>(ApiResponseKind.UnexpectedResponse, result.StatusCode, null, null, problem);
                }
            }

            ApiError error = ReadError(result, out string errorProblem);
            return error != null
                ? new ApiResponse<T>(ApiResponseKind.ApiError, result.StatusCode, null, error, null)
                : new ApiResponse<T>(ApiResponseKind.UnexpectedResponse, result.StatusCode, null, null, errorProblem);
        }

        /// <summary>
        /// Recognizes an exchange without an answer.
        /// </summary>
        /// <param name="result">The raw result.</param>
        /// <param name="kind">The kind of the failure.</param>
        /// <returns><c>true</c> when there is no answer of the server.</returns>
        private static bool TryConvertFailure(HttpResult result, out ApiResponseKind kind)
        {
            if (result.Failure == HttpFailure.Timeout)
            {
                kind = ApiResponseKind.Timeout;
                return true;
            }

            if (result.Failure == HttpFailure.Network || result.StatusCode <= 0)
            {
                kind = ApiResponseKind.NetworkError;
                return true;
            }

            kind = ApiResponseKind.Success;
            return false;
        }

        /// <summary>
        /// Whether a status is a success (2xx).
        /// </summary>
        /// <param name="statusCode">The HTTP status.</param>
        /// <returns><c>true</c> for 200 to 299.</returns>
        private static bool IsSuccessStatus(long statusCode) => statusCode >= 200 && statusCode < 300;

        /// <summary>
        /// Reads the single error body of an error answer with <see cref="GlaJson"/>.
        /// </summary>
        /// <param name="result">The raw result.</param>
        /// <param name="problem">Why the body is not an <see cref="ApiError"/>, or <c>null</c>.</param>
        /// <returns>The error, or <c>null</c> when the body is not one.</returns>
        private static ApiError ReadError(HttpResult result, out string problem)
        {
            try
            {
                ApiError error = GlaJson.Deserialize<ApiError>(result.Body ?? string.Empty);
                problem = null;
                return error;
            }
            catch (JsonException exception)
            {
                problem = $"the body is not an ApiError: {exception.Message}";
                return null;
            }
        }

        #endregion
    }
}
