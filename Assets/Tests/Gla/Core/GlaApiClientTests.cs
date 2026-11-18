using System;
using System.Linq;
using FlipNflop.Commons.Errors;
using FlipNflop.Commons.Messages;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FlipNflop.Gla.Tests
{
    /// <summary>
    /// EditMode tests of <see cref="GlaApiClient"/> (gla-13 D1): the request bodies written by
    /// <c>GlaJson</c>, the routes, the timeout, and the conversion of 2xx, error bodies, network
    /// failures and timeouts into <see cref="ApiResponse"/>. Interacts with <see cref="FakeHttpTransport"/>.
    /// </summary>
    public class GlaApiClientTests
    {
        #region Fields

        private FakeHttpTransport transport;
        private GlaApiClient client;

        #endregion

        #region Setup

        /// <summary>
        /// Creates the client over a fake transport.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            transport = new FakeHttpTransport();
            client = new GlaApiClient("http://localhost:5000/", transport);
        }

        #endregion

        #region Requests

        /// <summary>
        /// The code request goes to its route with the e-mail as typed, the timeout of 10 s and no token.
        /// </summary>
        [Test]
        public void CodeRequestPostsTheEmail()
        {
            client.RequestCodeAsync(" Aluno@Estudante.UFJF.br ");

            FakeHttpTransport.PendingRequest request = transport.Requests.Single();
            Assert.That(request.Url, Is.EqualTo("http://localhost:5000/identification/code-requests"));
            Assert.That(request.TimeoutSeconds, Is.EqualTo(10));
            Assert.That(request.BearerToken, Is.Null);
            Assert.That(RequestJson.Parse(request.Json).ToString(Newtonsoft.Json.Formatting.None), Is.EqualTo("{\"email\":\" Aluno@Estudante.UFJF.br \"}"));
        }

        /// <summary>
        /// The verification sends the code as text, keeping the leading zeros.
        /// </summary>
        [Test]
        public void CodeVerificationPostsTheCodeAsText()
        {
            client.VerifyCodeAsync("aluno@estudante.ufjf.br", "012345");

            FakeHttpTransport.PendingRequest request = transport.Requests.Single();
            Assert.That(request.Url, Is.EqualTo("http://localhost:5000/identification/code-verifications"));
            JObject body = RequestJson.Parse(request.Json);
            Assert.That(body.Properties().Select(property => property.Name), Is.EquivalentTo(new[] { "email", "code" }));
            Assert.That(body["code"].Type, Is.EqualTo(JTokenType.String));
            Assert.That((string)body["code"], Is.EqualTo("012345"));
        }

        /// <summary>
        /// The guest session opening writes <c>credential</c> as an explicit <c>null</c> and the start in UTC.
        /// </summary>
        [Test]
        public void GuestSessionOpeningWritesNullCredential()
        {
            var opening = new SessionOpening
            {
                GameVersion = "0.15.0",
                Credential = null,
                StartedAt = new DateTimeOffset(2026, 10, 20, 14, 32, 5, TimeSpan.FromHours(-3)),
            };

            client.OpenSessionAsync(opening);

            FakeHttpTransport.PendingRequest request = transport.Requests.Single();
            Assert.That(request.Url, Is.EqualTo("http://localhost:5000/sessions"));
            JObject body = RequestJson.Parse(request.Json);
            Assert.That(body.Properties().Select(property => property.Name), Is.EquivalentTo(new[] { "game_version", "credential", "started_at" }));
            Assert.That(body["credential"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(request.Json, Does.Contain("\"started_at\":\"2026-10-20T17:32:05.000Z\""));
        }

        /// <summary>
        /// A raw JSON is sent as is, with the bearer token (the attempt delivery of gla-15).
        /// </summary>
        [Test]
        public void RawPostKeepsTheBytesAndTheToken()
        {
            const string json = "{\"phase_name\":\"third_level\"}";

            client.PostJsonAsync("attempts", json, "token-1");

            FakeHttpTransport.PendingRequest request = transport.Requests.Single();
            Assert.That(request.Url, Is.EqualTo("http://localhost:5000/attempts"));
            Assert.That(request.Json, Is.EqualTo(json));
            Assert.That(request.BearerToken, Is.EqualTo("token-1"));
        }

        /// <summary>
        /// The address must be absolute http or https.
        /// </summary>
        [Test]
        public void RefusesAnInvalidAddress()
        {
            Assert.Throws<ArgumentException>(() => new GlaApiClient("localhost", transport));
            Assert.Throws<ArgumentNullException>(() => new GlaApiClient("http://localhost:5000", null));
        }

        #endregion

        #region Answers

        /// <summary>
        /// Task 1.2: a code request answered with 204 is a success.
        /// </summary>
        [Test]
        public void NoContentIsASuccess()
        {
            transport.Enqueue(ApiAnswers.NoContent());

            ApiResponse response = client.RequestCodeAsync("aluno@estudante.ufjf.br").Result;

            Assert.That(response.Kind, Is.EqualTo(ApiResponseKind.Success));
            Assert.That(response.StatusCode, Is.EqualTo(204));
            Assert.That(response.IsTransientFailure, Is.False);
        }

        /// <summary>
        /// Task 1.2: a refused domain gives the <see cref="ApiError"/> with the Portuguese message.
        /// </summary>
        [Test]
        public void ErrorBodyBecomesAnApiError()
        {
            transport.Enqueue(ApiAnswers.Error(400, ApiErrorCodes.DomainNotAllowed, ApiAnswers.DomainNotAllowedMessage));

            ApiResponse response = client.RequestCodeAsync("aluno@gmail.com").Result;

            Assert.That(response.Kind, Is.EqualTo(ApiResponseKind.ApiError));
            Assert.That(response.StatusCode, Is.EqualTo(400));
            Assert.That(response.ErrorCode, Is.EqualTo(ApiErrorCodes.DomainNotAllowed));
            Assert.That(response.Error.Message, Is.EqualTo(ApiAnswers.DomainNotAllowedMessage));
            Assert.That(response.IsTransientFailure, Is.False);
            Assert.That(response.ToString(), Is.EqualTo("400 domain_not_allowed"));
        }

        /// <summary>
        /// The verification success carries the credential and its validity.
        /// </summary>
        [Test]
        public void VerificationSuccessCarriesTheCredential()
        {
            var expiresAt = new DateTimeOffset(2026, 10, 20, 21, 14, 58, TimeSpan.Zero);
            transport.Enqueue(ApiAnswers.Body(200, new CodeVerificationResult { Credential = "v1.credential", ExpiresAt = expiresAt }));

            ApiResponse<CodeVerificationResult> response = client.VerifyCodeAsync("aluno@estudante.ufjf.br", "123456").Result;

            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Value.Credential, Is.EqualTo("v1.credential"));
            Assert.That(response.Value.ExpiresAt, Is.EqualTo(expiresAt));
        }

        /// <summary>
        /// The session opening success carries the token; 401 <c>invalid_credential</c> and 400
        /// <c>invalid_request</c> are API errors.
        /// </summary>
        [Test]
        public void SessionAnswersAreConverted()
        {
            transport.Enqueue(ApiAnswers.Body(201, new SessionOpened { SessionToken = "s1.token", Identified = true }));
            transport.Enqueue(ApiAnswers.Error(401, ApiErrorCodes.InvalidCredential, ApiAnswers.InvalidCredentialMessage));
            transport.Enqueue(ApiAnswers.Error(400, ApiErrorCodes.InvalidRequest, ApiAnswers.FutureStartMessage));
            var opening = new SessionOpening { GameVersion = "0.15.0", Credential = "v1.credential", StartedAt = DateTimeOffset.UtcNow };

            ApiResponse<SessionOpened> opened = client.OpenSessionAsync(opening).Result;
            ApiResponse<SessionOpened> refusedCredential = client.OpenSessionAsync(opening).Result;
            ApiResponse<SessionOpened> refusedStart = client.OpenSessionAsync(opening).Result;

            Assert.That(opened.Kind, Is.EqualTo(ApiResponseKind.Success));
            Assert.That(opened.StatusCode, Is.EqualTo(201));
            Assert.That(opened.Value.SessionToken, Is.EqualTo("s1.token"));
            Assert.That(opened.Value.Identified, Is.True);
            Assert.That(refusedCredential.Kind, Is.EqualTo(ApiResponseKind.ApiError));
            Assert.That(refusedCredential.ErrorCode, Is.EqualTo(ApiErrorCodes.InvalidCredential));
            Assert.That(refusedStart.StatusCode, Is.EqualTo(400));
            Assert.That(refusedStart.ErrorCode, Is.EqualTo(ApiErrorCodes.InvalidRequest));
            Assert.That(refusedStart.Error.Message, Is.EqualTo(ApiAnswers.FutureStartMessage));
        }

        /// <summary>
        /// Network failure and timeout have no status and are transient; 429 and 5xx are transient too.
        /// </summary>
        [Test]
        public void FailuresAreTransient()
        {
            transport.Enqueue(ApiAnswers.NetworkFailure());
            transport.Enqueue(ApiAnswers.TimeoutFailure());
            transport.Enqueue(ApiAnswers.Error(429, ApiErrorCodes.RateLimited, ApiAnswers.RateLimitedMessage));
            transport.Enqueue(ApiAnswers.Error(500, ApiErrorCodes.InternalError, ApiAnswers.CodeNotSentMessage));
            transport.Enqueue(HttpResult.Answered(502, "<html>Bad Gateway</html>"));

            ApiResponse network = client.RequestCodeAsync("a@ufjf.br").Result;
            ApiResponse timeout = client.RequestCodeAsync("a@ufjf.br").Result;
            ApiResponse limited = client.RequestCodeAsync("a@ufjf.br").Result;
            ApiResponse serverError = client.RequestCodeAsync("a@ufjf.br").Result;
            ApiResponse badGateway = client.RequestCodeAsync("a@ufjf.br").Result;

            Assert.That(network.Kind, Is.EqualTo(ApiResponseKind.NetworkError));
            Assert.That(network.StatusCode, Is.EqualTo(0));
            Assert.That(timeout.Kind, Is.EqualTo(ApiResponseKind.Timeout));
            Assert.That(limited.Kind, Is.EqualTo(ApiResponseKind.ApiError));
            Assert.That(serverError.Kind, Is.EqualTo(ApiResponseKind.ApiError));
            Assert.That(serverError.Error.Message, Is.EqualTo(ApiAnswers.CodeNotSentMessage));
            Assert.That(badGateway.Kind, Is.EqualTo(ApiResponseKind.UnexpectedResponse));
            Assert.That(badGateway.StatusCode, Is.EqualTo(502));
            Assert.That(new[] { network, timeout, limited, serverError, badGateway }.All(response => response.IsTransientFailure), Is.True);
        }

        /// <summary>
        /// A 2xx whose body breaks the contract, and an error without the single error body, are unexpected answers.
        /// </summary>
        [Test]
        public void ContractBreaksAreUnexpectedAnswers()
        {
            transport.Enqueue(HttpResult.Answered(201, "{\"session_token\":\"s1\"}"));
            transport.Enqueue(HttpResult.Answered(201, "{\"session_token\":\"s1\",\"identified\":false,\"extra\":1}"));
            transport.Enqueue(HttpResult.Answered(404, string.Empty));
            var opening = new SessionOpening { GameVersion = "0.15.0", Credential = null, StartedAt = DateTimeOffset.UtcNow };

            ApiResponse<SessionOpened> missingField = client.OpenSessionAsync(opening).Result;
            ApiResponse<SessionOpened> unknownField = client.OpenSessionAsync(opening).Result;
            ApiResponse<SessionOpened> notFound = client.OpenSessionAsync(opening).Result;

            Assert.That(missingField.Kind, Is.EqualTo(ApiResponseKind.UnexpectedResponse));
            Assert.That(missingField.Value, Is.Null);
            Assert.That(unknownField.Kind, Is.EqualTo(ApiResponseKind.UnexpectedResponse));
            Assert.That(notFound.Kind, Is.EqualTo(ApiResponseKind.UnexpectedResponse));
            Assert.That(notFound.StatusCode, Is.EqualTo(404));
            Assert.That(notFound.IsTransientFailure, Is.False);
        }

        /// <summary>
        /// A transport that throws counts as a network error, and the answer is still delivered.
        /// </summary>
        [Test]
        public void ThrowingTransportIsANetworkError()
        {
            var throwing = new GlaApiClient("http://localhost:5000", new ThrowingTransport());

            GlaOperation<ApiResponse> operation = throwing.RequestCodeAsync("a@ufjf.br");

            Assert.That(operation.IsDone, Is.True);
            Assert.That(operation.Result.Kind, Is.EqualTo(ApiResponseKind.NetworkError));
        }

        /// <summary>
        /// The operation stays pending until the answer arrives, and then runs the callbacks once.
        /// </summary>
        [Test]
        public void OperationWaitsForTheAnswer()
        {
            int calls = 0;
            GlaOperation<ApiResponse> operation = client.RequestCodeAsync("a@ufjf.br").OnCompleted(_ => calls++);
            System.Collections.IEnumerator coroutine = operation;

            Assert.That(operation.IsDone, Is.False);
            Assert.That(coroutine.MoveNext(), Is.True);
            transport.Requests.Single().Answer(ApiAnswers.NoContent());

            Assert.That(operation.IsDone, Is.True);
            Assert.That(coroutine.MoveNext(), Is.False);
            Assert.That(calls, Is.EqualTo(1));
            operation.OnCompleted(_ => calls++);
            Assert.That(calls, Is.EqualTo(2));
        }

        #endregion

        /// <summary>
        /// A transport that fails before sending.
        /// </summary>
        private sealed class ThrowingTransport : IHttpTransport
        {
            /// <summary>
            /// Throws, as a transport that cannot start the request.
            /// </summary>
            /// <param name="url">The address.</param>
            /// <param name="json">The body.</param>
            /// <param name="bearerToken">The token.</param>
            /// <param name="timeoutSeconds">The timeout.</param>
            /// <param name="onCompleted">Not called.</param>
            public void PostJson(string url, string json, string bearerToken, int timeoutSeconds, Action<HttpResult> onCompleted)
            {
                throw new InvalidOperationException("no network stack");
            }
        }
    }
}
