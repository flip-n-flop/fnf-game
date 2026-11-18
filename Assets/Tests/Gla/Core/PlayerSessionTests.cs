using System;
using System.Collections.Generic;
using System.Linq;
using FlipNflop.Commons.Errors;
using FlipNflop.Commons.Messages;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FlipNflop.Gla.Tests
{
    /// <summary>
    /// EditMode tests of <see cref="PlayerSession"/> (gla-13 D1, D3, D4 and D5) against the
    /// scenarios of the spec <c>game-identification</c>: modes, credential, token only in memory,
    /// one session per page load with <c>started_at</c> = page load, and the answers 201, 400, 401,
    /// network error, timeout, 429 and 5xx. Interacts with <see cref="FakeHttpTransport"/>,
    /// <see cref="MemoryStorage"/> and <see cref="RecordingLog"/>.
    /// </summary>
    public class PlayerSessionTests
    {
        #region Constants

        private const string GameVersion = "0.15.0";
        private const string Email = "aluno@estudante.ufjf.br";
        private const string Credential = "v1.Zx8dQm3LrT0aJ5nY2cWk7pHf9sBv4gE6uNq1oRi8tAy";
        private const string GuestToken = "s1.guest-token";
        private const string IdentifiedToken = "s1.identified-token";
        private const string PageLoadedAtText = "2026-10-20T17:32:05.000Z";

        private static readonly DateTimeOffset PageLoadedAt =
            GlaTime.ToContractPrecision(new DateTimeOffset(2026, 10, 20, 14, 32, 5, TimeSpan.FromHours(-3)));

        #endregion

        #region Fields

        private DateTimeOffset now;
        private MemoryStorage storage;
        private FakeHttpTransport transport;
        private RecordingLog log;
        private PlayerSession session;
        private int changes;

        #endregion

        #region Setup

        /// <summary>
        /// Creates the state of a page loaded at 14:32:05 in Brasília, with the clock at 14:33.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            now = PageLoadedAt.AddSeconds(55);
            storage = new MemoryStorage();
            transport = new FakeHttpTransport();
            log = new RecordingLog();
            session = new PlayerSession(PageLoadedAt, GameVersion, new CredentialStore(storage), () => now, log);
            changes = 0;
            session.Changed += () => changes++;
        }

        #endregion

        #region Loading and Configuration

        /// <summary>
        /// Before the configuration, the mode is <see cref="PlayerMode.Loading"/> and nothing is requested.
        /// </summary>
        [Test]
        public void StartsLoadingWithoutRequests()
        {
            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Loading));
            Assert.That(session.State, Is.EqualTo(SessionState.None));
            Assert.That(session.PageLoadedAt, Is.EqualTo(PageLoadedAt));
            Assert.That(session.OpenSessionAsync().Result.Outcome, Is.EqualTo(SessionOpenOutcome.NotAllowed));
            Assert.That(session.RequestCodeAsync(Email).Result.Kind, Is.EqualTo(ApiResponseKind.NotSent));
            Assert.That(transport.Requests, Is.Empty);
        }

        /// <summary>
        /// Scenarios "Build sem coleta" and "Configuração ausente": the collection off makes no request,
        /// logs the reason, and cannot be turned on again in the same page load.
        /// </summary>
        [Test]
        public void CollectionOffMakesNoRequest()
        {
            storage.Values[CredentialStore.CredentialKey] = Credential;
            storage.Values[CredentialStore.ExpiresAtKey] = GlaTime.Format(now.AddHours(3));

            session.Disable("gla-config.json was not read within 5 s");
            session.Enable(new GlaApiClient("http://localhost:5000", transport));
            session.EnterAsGuest(true);

            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Disabled));
            Assert.That(session.IsCollectionBlocked, Is.True);
            Assert.That(session.OpenSessionAsync().Result.Outcome, Is.EqualTo(SessionOpenOutcome.NotAllowed));
            Assert.That(transport.Requests, Is.Empty);
            Assert.That(log.Infos.Any(line => line.Contains("not read within 5 s")), Is.True);
        }

        /// <summary>
        /// Scenario "Primeira visita": without a credential, the entry screen is shown and nothing is requested.
        /// </summary>
        [Test]
        public void FirstVisitShowsTheEntryScreen()
        {
            Enable();

            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Undecided));
            Assert.That(session.State, Is.EqualTo(SessionState.None));
            Assert.That(session.IsCollectionBlocked, Is.False);
            Assert.That(transport.Requests, Is.Empty);
            Assert.That(changes, Is.GreaterThan(0));
        }

        #endregion

        #region Stored Credential

        /// <summary>
        /// Scenario "Recarga 1 hora depois": the entry screen is skipped and the session opened is identified.
        /// </summary>
        [Test]
        public void ReloadOneHourLaterOpensAnIdentifiedSession()
        {
            StoreCredential(now.AddHours(3));
            transport.Enqueue(ApiAnswers.Body(201, new SessionOpened { SessionToken = IdentifiedToken, Identified = true }));

            Enable();

            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Identified));
            Assert.That(session.State, Is.EqualTo(SessionState.Open));
            Assert.That(session.SessionToken, Is.EqualTo(IdentifiedToken));
            JObject body = SessionBody(transport.Requests.Single());
            Assert.That((string)body["credential"], Is.EqualTo(Credential));
            Assert.That((string)body["game_version"], Is.EqualTo(GameVersion));
            Assert.That((string)body["started_at"], Is.EqualTo(PageLoadedAtText));
        }

        /// <summary>
        /// Scenario "Recarga 5 horas depois": the expired credential is removed and the entry screen is shown.
        /// </summary>
        [Test]
        public void ReloadFiveHoursLaterShowsTheEntryScreen()
        {
            StoreCredential(now.AddHours(-1));

            Enable();

            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Undecided));
            Assert.That(storage.Values, Is.Empty);
            Assert.That(transport.Requests, Is.Empty);
        }

        /// <summary>
        /// Scenario "Credencial apagada no servidor": <c>invalid_credential</c> removes the credential
        /// and returns to the entry screen.
        /// </summary>
        [Test]
        public void InvalidCredentialReturnsToTheEntryScreen()
        {
            StoreCredential(now.AddHours(3));
            transport.Enqueue(ApiAnswers.Error(401, ApiErrorCodes.InvalidCredential, ApiAnswers.InvalidCredentialMessage));
            SessionOpenResult result = null;

            Enable();
            session.OpenSessionAsync().OnCompleted(opened => result = opened);

            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Undecided));
            Assert.That(session.State, Is.EqualTo(SessionState.None));
            Assert.That(session.SessionToken, Is.Null);
            Assert.That(storage.Values, Is.Empty);
            Assert.That(result.Outcome, Is.EqualTo(SessionOpenOutcome.NotAllowed));
            Assert.That(transport.Requests.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// The opening that receives <c>invalid_credential</c> reports it to its caller (gla-15 D4).
        /// </summary>
        [Test]
        public void InvalidCredentialIsTheOutcomeOfTheOpening()
        {
            StoreCredential(now.AddHours(3));
            Enable();

            GlaOperation<SessionOpenResult> opening = session.OpenSessionAsync();
            transport.Requests.Single().Answer(ApiAnswers.Error(401, ApiErrorCodes.InvalidCredential, ApiAnswers.InvalidCredentialMessage));

            Assert.That(opening.Result.Outcome, Is.EqualTo(SessionOpenOutcome.CredentialRejected));
            Assert.That(opening.Result.Response.ErrorCode, Is.EqualTo(ApiErrorCodes.InvalidCredential));
        }

        /// <summary>
        /// A credential that expires during the page load is removed before a reopening, without a request.
        /// </summary>
        [Test]
        public void CredentialExpiredBeforeAReopeningIsRemovedWithoutRequest()
        {
            StoreCredential(now.AddHours(3));
            transport.Enqueue(ApiAnswers.Body(201, new SessionOpened { SessionToken = IdentifiedToken, Identified = true }));
            Enable();
            now = now.AddHours(3).AddMinutes(1);

            SessionOpenResult result = session.OpenSessionAsync().Result;

            Assert.That(result.Outcome, Is.EqualTo(SessionOpenOutcome.CredentialRejected));
            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Undecided));
            Assert.That(session.SessionToken, Is.Null);
            Assert.That(storage.Values, Is.Empty);
            Assert.That(transport.Requests.Count, Is.EqualTo(1));
        }

        #endregion

        #region E-mail Verification

        /// <summary>
        /// Scenarios "Código certo" and "Armazenamento depois da verificação": the credential and its
        /// validity are stored (never the e-mail), and the identified session is opened.
        /// </summary>
        [Test]
        public void RightCodeStoresTheCredentialAndOpensTheSession()
        {
            Enable();
            transport.Enqueue(ApiAnswers.NoContent());
            transport.Enqueue(ApiAnswers.Body(200, new CodeVerificationResult { Credential = Credential, ExpiresAt = now.AddHours(4) }));
            transport.Enqueue(ApiAnswers.Body(201, new SessionOpened { SessionToken = IdentifiedToken, Identified = true }));

            ApiResponse request = session.RequestCodeAsync(Email).Result;
            ApiResponse<CodeVerificationResult> verification = session.VerifyCodeAsync(Email, "123456").Result;

            Assert.That(request.IsSuccess, Is.True);
            Assert.That(verification.IsSuccess, Is.True);
            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Identified));
            Assert.That(session.State, Is.EqualTo(SessionState.Open));
            Assert.That(storage.Values.Keys, Is.EquivalentTo(new[] { CredentialStore.CredentialKey, CredentialStore.ExpiresAtKey }));
            Assert.That(storage.Values[CredentialStore.CredentialKey], Is.EqualTo(Credential));
            Assert.That(storage.Values.Values.Any(value => value.Contains(Email) || value.Contains("estudante")), Is.False);
            Assert.That(transport.Requests.Select(item => item.Url).ToArray(), Is.EqualTo(new[]
            {
                "http://localhost:5000/identification/code-requests",
                "http://localhost:5000/identification/code-verifications",
                "http://localhost:5000/sessions",
            }));
            Assert.That((string)SessionBody(transport.Requests[2])["credential"], Is.EqualTo(Credential));
        }

        /// <summary>
        /// Scenario "Domínio recusado": the refusal keeps the entry screen and stores nothing.
        /// </summary>
        [Test]
        public void RefusedDomainKeepsTheEntryScreen()
        {
            Enable();
            transport.Enqueue(ApiAnswers.Error(400, ApiErrorCodes.DomainNotAllowed, ApiAnswers.DomainNotAllowedMessage));

            ApiResponse response = session.RequestCodeAsync("aluno@gmail.com").Result;

            Assert.That(response.ErrorCode, Is.EqualTo(ApiErrorCodes.DomainNotAllowed));
            Assert.That(response.Error.Message, Is.EqualTo(ApiAnswers.DomainNotAllowedMessage));
            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Undecided));
            Assert.That(storage.Values, Is.Empty);
        }

        /// <summary>
        /// A wrong code keeps the entry screen, stores nothing and opens no session.
        /// </summary>
        [Test]
        public void WrongCodeStoresNothing()
        {
            Enable();
            transport.Enqueue(ApiAnswers.Error(400, ApiErrorCodes.InvalidCode, ApiAnswers.InvalidCodeMessage));

            ApiResponse<CodeVerificationResult> response = session.VerifyCodeAsync(Email, "000000").Result;

            Assert.That(response.ErrorCode, Is.EqualTo(ApiErrorCodes.InvalidCode));
            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Undecided));
            Assert.That(storage.Values, Is.Empty);
            Assert.That(transport.Requests.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// A verification that ends after the player entered as guest does not identify him.
        /// </summary>
        [Test]
        public void VerificationEndedAfterTheGuestChoiceIsIgnored()
        {
            Enable();
            GlaOperation<ApiResponse<CodeVerificationResult>> verification = session.VerifyCodeAsync(Email, "123456");

            session.EnterAsGuest(true);
            transport.Requests[0].Answer(ApiAnswers.Body(200, new CodeVerificationResult { Credential = Credential, ExpiresAt = now.AddHours(4) }));

            Assert.That(verification.IsDone, Is.True);
            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Guest));
            Assert.That(storage.Values, Is.Empty);
            Assert.That((string)SessionBody(transport.Requests[1])["credential"], Is.Null);
        }

        #endregion

        #region Guest

        /// <summary>
        /// Scenario "Instante da sessão": the guest who accepts at 14:33 opens an anonymous session with
        /// <c>started_at = 2026-10-20T17:32:05.000Z</c>.
        /// </summary>
        [Test]
        public void GuestWithConsentOpensAnAnonymousSessionAtThePageLoad()
        {
            Enable();
            transport.Enqueue(ApiAnswers.Body(201, new SessionOpened { SessionToken = GuestToken, Identified = false }));

            session.EnterAsGuest(true);

            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Guest));
            Assert.That(session.State, Is.EqualTo(SessionState.Open));
            Assert.That(session.SessionToken, Is.EqualTo(GuestToken));
            JObject body = SessionBody(transport.Requests.Single());
            Assert.That(body["credential"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That((string)body["started_at"], Is.EqualTo(PageLoadedAtText));
            Assert.That(storage.Values, Is.Empty);
        }

        /// <summary>
        /// Scenario "Convidado que recusa": no request at all in the page load, and no way back without reloading.
        /// </summary>
        [Test]
        public void GuestWhoRefusesMakesNoRequest()
        {
            Enable();

            session.EnterAsGuest(false);
            GlaOperation<SessionOpenResult> opening = session.OpenSessionAsync();
            session.ResetIdentity();
            ApiResponse codeRequest = session.RequestCodeAsync(Email).Result;

            Assert.That(session.Mode, Is.EqualTo(PlayerMode.GuestRefused));
            Assert.That(session.State, Is.EqualTo(SessionState.None));
            Assert.That(session.IsCollectionBlocked, Is.True);
            Assert.That(opening.Result.Outcome, Is.EqualTo(SessionOpenOutcome.NotAllowed));
            Assert.That(codeRequest.Kind, Is.EqualTo(ApiResponseKind.NotSent));
            Assert.That(transport.Requests, Is.Empty);
        }

        #endregion

        #region Session Answers

        /// <summary>
        /// Scenario "API fora do ar no início": the network failure leaves the session Pending, the
        /// game playable, and a later opening (gla-15) opens it with the same start.
        /// </summary>
        [Test]
        public void NetworkFailureLeavesPendingAndALaterOpeningSucceeds()
        {
            StoreCredential(now.AddHours(3));
            transport.Enqueue(ApiAnswers.NetworkFailure());

            Enable();

            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Identified));
            Assert.That(session.State, Is.EqualTo(SessionState.Pending));
            Assert.That(session.IsOpening, Is.False);
            Assert.That(session.IsCollectionBlocked, Is.False);

            now = now.AddMinutes(20);
            transport.Enqueue(ApiAnswers.Body(201, new SessionOpened { SessionToken = IdentifiedToken, Identified = true }));
            SessionOpenResult result = session.OpenSessionAsync().Result;

            Assert.That(result.Outcome, Is.EqualTo(SessionOpenOutcome.Opened));
            Assert.That(session.State, Is.EqualTo(SessionState.Open));
            Assert.That(transport.Requests.Select(item => (string)SessionBody(item)["started_at"]), Is.All.EqualTo(PageLoadedAtText));
        }

        /// <summary>
        /// Timeout, 429, 5xx and an unreadable answer are transient: the session stays Pending.
        /// </summary>
        /// <param name="answer">Which answer the API gives.</param>
        [TestCase("timeout")]
        [TestCase("429")]
        [TestCase("500")]
        [TestCase("503-html")]
        [TestCase("201-unreadable")]
        public void TransientAnswersLeaveThePendingState(string answer)
        {
            Enable();
            transport.Enqueue(TransientAnswer(answer));
            SessionOpenResult result = null;

            session.EnterAsGuest(true);
            session.OpenSessionAsync().OnCompleted(opened => result = opened);
            transport.Unanswered.Single().Answer(TransientAnswer(answer));

            Assert.That(session.State, Is.EqualTo(SessionState.Pending));
            Assert.That(session.SessionToken, Is.Null);
            Assert.That(result.Outcome, Is.EqualTo(SessionOpenOutcome.TransientFailure));
            Assert.That(log.Warnings.Any(line => line.Contains("Pending")), Is.True);
        }

        /// <summary>
        /// Scenario "Relógio adiantado": the 400 makes the session Rejected, logs the message, and no
        /// other request is made in the page load, not even by "Trocar e-mail" or a reopening.
        /// </summary>
        [Test]
        public void BadRequestRejectsTheSessionUntilTheReload()
        {
            Enable();
            transport.Enqueue(ApiAnswers.Error(400, ApiErrorCodes.InvalidRequest, ApiAnswers.FutureStartMessage));

            session.EnterAsGuest(true);
            SessionOpenResult reopening = session.OpenSessionAsync().Result;
            session.ResetIdentity();

            Assert.That(session.State, Is.EqualTo(SessionState.Rejected));
            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Guest));
            Assert.That(session.IsCollectionBlocked, Is.True);
            Assert.That(reopening.Outcome, Is.EqualTo(SessionOpenOutcome.Rejected));
            Assert.That(reopening.Response, Is.Null);
            Assert.That(transport.Requests.Count, Is.EqualTo(1));
            Assert.That(log.Warnings.Any(line => line.Contains(ApiAnswers.FutureStartMessage)), Is.True);
        }

        /// <summary>
        /// A second opening while one runs joins it: a single request, one result for both callers.
        /// </summary>
        [Test]
        public void ConcurrentOpeningsShareOneRequest()
        {
            Enable();
            session.EnterAsGuest(true);

            GlaOperation<SessionOpenResult> first = session.OpenSessionAsync();
            GlaOperation<SessionOpenResult> second = session.OpenSessionAsync();

            Assert.That(session.IsOpening, Is.True);
            Assert.That(session.State, Is.EqualTo(SessionState.Pending));
            Assert.That(second, Is.SameAs(first));
            Assert.That(transport.Requests.Count, Is.EqualTo(1));

            transport.Requests[0].Answer(ApiAnswers.Body(201, new SessionOpened { SessionToken = GuestToken, Identified = false }));

            Assert.That(first.Result.Outcome, Is.EqualTo(SessionOpenOutcome.Opened));
            Assert.That(session.IsOpening, Is.False);
        }

        /// <summary>
        /// A reopening after hours (expired token, gla-15 D5) sends the same <c>started_at</c> of the page load.
        /// </summary>
        [Test]
        public void ReopeningUsesThePageLoadInstant()
        {
            Enable();
            transport.Enqueue(ApiAnswers.Body(201, new SessionOpened { SessionToken = GuestToken, Identified = false }));
            session.EnterAsGuest(true);
            now = now.AddHours(13);
            transport.Enqueue(ApiAnswers.Body(201, new SessionOpened { SessionToken = "s1.second-guest-token", Identified = false }));

            SessionOpenResult result = session.OpenSessionAsync().Result;

            Assert.That(result.Outcome, Is.EqualTo(SessionOpenOutcome.Opened));
            Assert.That(session.SessionToken, Is.EqualTo("s1.second-guest-token"));
            Assert.That(transport.Requests.Select(item => (string)SessionBody(item)["started_at"]), Is.All.EqualTo(PageLoadedAtText));
        }

        #endregion

        #region Switching Identity

        /// <summary>
        /// Scenario "Máquina compartilhada": "Trocar e-mail" removes the credential and the token and
        /// returns to the entry screen.
        /// </summary>
        [Test]
        public void SwitchingEmailDiscardsTheCredentialAndTheToken()
        {
            StoreCredential(now.AddHours(3));
            transport.Enqueue(ApiAnswers.Body(201, new SessionOpened { SessionToken = IdentifiedToken, Identified = true }));
            Enable();

            session.ResetIdentity();

            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Undecided));
            Assert.That(session.State, Is.EqualTo(SessionState.None));
            Assert.That(session.SessionToken, Is.Null);
            Assert.That(storage.Values, Is.Empty);
            Assert.That(transport.Requests.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// Scenario "Troca de e-mail no meio da aula": the guest switches at 15:10, identifies himself,
        /// and a second session is opened with the same <c>started_at</c>.
        /// </summary>
        [Test]
        public void GuestWhoIdentifiesLaterOpensASecondSessionAtThePageLoad()
        {
            Enable();
            transport.Enqueue(ApiAnswers.Body(201, new SessionOpened { SessionToken = GuestToken, Identified = false }));
            session.EnterAsGuest(true);

            now = new DateTimeOffset(2026, 10, 20, 15, 10, 0, TimeSpan.FromHours(-3));
            session.ResetIdentity();
            transport.Enqueue(ApiAnswers.NoContent());
            transport.Enqueue(ApiAnswers.Body(200, new CodeVerificationResult { Credential = Credential, ExpiresAt = now.AddHours(4) }));
            transport.Enqueue(ApiAnswers.Body(201, new SessionOpened { SessionToken = IdentifiedToken, Identified = true }));
            session.RequestCodeAsync(Email);
            session.VerifyCodeAsync(Email, "123456");

            List<FakeHttpTransport.PendingRequest> openings = transport.Requests.Where(item => item.Url.EndsWith("/sessions")).ToList();
            Assert.That(openings.Count, Is.EqualTo(2));
            Assert.That(SessionBody(openings[0])["credential"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That((string)SessionBody(openings[1])["credential"], Is.EqualTo(Credential));
            Assert.That(openings.Select(item => (string)SessionBody(item)["started_at"]), Is.All.EqualTo(PageLoadedAtText));
            Assert.That(session.SessionToken, Is.EqualTo(IdentifiedToken));
            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Identified));
        }

        /// <summary>
        /// An opening still running when the player switches is superseded: its token is never used.
        /// </summary>
        [Test]
        public void OpeningAnsweredAfterASwitchIsIgnored()
        {
            Enable();
            session.EnterAsGuest(true);
            GlaOperation<SessionOpenResult> opening = session.OpenSessionAsync();

            session.ResetIdentity();
            transport.Requests[0].Answer(ApiAnswers.Body(201, new SessionOpened { SessionToken = GuestToken, Identified = false }));

            Assert.That(opening.Result.Outcome, Is.EqualTo(SessionOpenOutcome.Superseded));
            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Undecided));
            Assert.That(session.State, Is.EqualTo(SessionState.None));
            Assert.That(session.SessionToken, Is.Null);
        }

        #endregion

        #region Privacy and Robustness

        /// <summary>
        /// Requirement "Token só em memória": the token, the credential and the e-mail never reach the
        /// storage (except the credential key) nor the log.
        /// </summary>
        [Test]
        public void TokenNeverLeavesTheMemory()
        {
            Enable();
            transport.Enqueue(ApiAnswers.NoContent());
            transport.Enqueue(ApiAnswers.Body(200, new CodeVerificationResult { Credential = Credential, ExpiresAt = now.AddHours(4) }));
            transport.Enqueue(ApiAnswers.Body(201, new SessionOpened { SessionToken = IdentifiedToken, Identified = true }));

            session.RequestCodeAsync(Email);
            session.VerifyCodeAsync(Email, "123456");

            Assert.That(session.SessionToken, Is.EqualTo(IdentifiedToken));
            Assert.That(storage.Values.Values.Any(value => value.Contains(IdentifiedToken)), Is.False);
            Assert.That(log.All.Any(line => line.Contains(IdentifiedToken) || line.Contains(Credential) || line.Contains(Email) || line.Contains("123456")), Is.False);
        }

        /// <summary>
        /// A listener that throws is logged and does not break the state.
        /// </summary>
        [Test]
        public void FailingListenerDoesNotBreakTheState()
        {
            session.Changed += () => throw new InvalidOperationException("broken listener");

            Enable();

            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Undecided));
            Assert.That(log.Warnings.Any(line => line.Contains("broken listener")), Is.True);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Turns the collection on with the fake transport.
        /// </summary>
        private void Enable()
        {
            session.Enable(new GlaApiClient("http://localhost:5000", transport));
        }

        /// <summary>
        /// Stores a credential, as a previous page load did.
        /// </summary>
        /// <param name="expiresAt">Its validity.</param>
        private void StoreCredential(DateTimeOffset expiresAt)
        {
            new CredentialStore(storage).Save(Credential, expiresAt);
        }

        /// <summary>
        /// The body of a session opening.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The JSON object.</returns>
        private static JObject SessionBody(FakeHttpTransport.PendingRequest request)
        {
            Assert.That(request.Url, Does.EndWith("/sessions"));
            JObject body = RequestJson.Parse(request.Json);
            Assert.That(body.Properties().Select(property => property.Name), Is.EquivalentTo(new[] { "game_version", "credential", "started_at" }));
            return body;
        }

        /// <summary>
        /// A transient answer of the session opening.
        /// </summary>
        /// <param name="answer">Its name in the test cases.</param>
        /// <returns>The result.</returns>
        private static HttpResult TransientAnswer(string answer)
        {
            switch (answer)
            {
                case "timeout":
                    return ApiAnswers.TimeoutFailure();
                case "429":
                    return ApiAnswers.Error(429, ApiErrorCodes.RateLimited, ApiAnswers.RateLimitedMessage);
                case "500":
                    return ApiAnswers.Error(500, ApiErrorCodes.InternalError, "Ocorreu um erro inesperado no servidor. Tente de novo mais tarde.");
                case "503-html":
                    return HttpResult.Answered(503, "<html>Service Unavailable</html>");
                default:
                    return HttpResult.Answered(201, "{\"session_token\":\"s1\"}");
            }
        }

        #endregion
    }
}
