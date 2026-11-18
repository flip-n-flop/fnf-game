using System;
using FlipNflop.Commons.Errors;
using FlipNflop.Commons.Messages;

namespace FlipNflop.Gla
{
    /// <summary>
    /// The single identification and session state of the game in a page load (gla-13, designs D1,
    /// D3, D4 and D5): the <see cref="Mode"/> chosen by the player, the session <see cref="State"/>,
    /// the session token (only in memory, never in the browser storage) and the credential, kept by
    /// <see cref="CredentialStore"/>. Every session of the page load is opened with
    /// <c>started_at</c> = <see cref="PageLoadedAt"/>.
    /// Talks to the API through <see cref="GlaApiClient"/>; created by <c>GlaServices</c>, shown by
    /// the entry screen, read by the attempt collection (gla-14) and used by the delivery (gla-15),
    /// which calls <see cref="OpenSessionAsync"/> while the session is <see cref="SessionState.Pending"/>
    /// or after <c>invalid_session</c>.
    /// </summary>
    public sealed class PlayerSession
    {
        #region Fields

        private readonly CredentialStore credentials;
        private readonly Func<DateTimeOffset> utcNow;
        private readonly IGlaLog log;

        private GlaApiClient api;
        private string credential;
        private DateTimeOffset credentialExpiresAt;
        private int identityVersion;
        private GlaOperation<SessionOpenResult> openingOperation;

        #endregion

        #region Construction

        /// <summary>
        /// Creates the state in <see cref="PlayerMode.Loading"/>, before the configuration is read.
        /// </summary>
        /// <param name="pageLoadedAt">The instant of the page load, the <c>started_at</c> of every session.</param>
        /// <param name="gameVersion">The <c>Application.version</c> sent as <c>game_version</c>.</param>
        /// <param name="credentials">The browser storage of the credential.</param>
        /// <param name="utcNow">The clock that checks the validity of the credential.</param>
        /// <param name="log">The GLA log.</param>
        /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
        public PlayerSession(DateTimeOffset pageLoadedAt, string gameVersion, CredentialStore credentials, Func<DateTimeOffset> utcNow, IGlaLog log)
        {
            PageLoadedAt = pageLoadedAt;
            GameVersion = gameVersion ?? throw new ArgumentNullException(nameof(gameVersion));
            this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
            this.utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised after every change of <see cref="Mode"/> or <see cref="State"/>. The exception of a
        /// listener is logged and does not stop the others.
        /// </summary>
        public event Action Changed;

        #endregion

        #region Properties

        /// <summary>
        /// The instant of the page load, in UTC with millisecond precision: the <c>started_at</c> of
        /// every session opened in this page load (the first one, after "Trocar e-mail" and after an
        /// expired token).
        /// </summary>
        public DateTimeOffset PageLoadedAt { get; }

        public string GameVersion { get; }

        /// <summary>In-memory generation of the identity that owns a queued attempt.</summary>
        internal int IdentityVersion => identityVersion;

        /// <summary>The configured transport shared by session opening and attempt delivery.</summary>
        internal GlaApiClient Api => api;

        public PlayerMode Mode { get; private set; } = PlayerMode.Loading;

        public SessionState State { get; private set; } = SessionState.None;

        /// <summary>
        /// The token of the open session, only in memory; <c>null</c> unless <see cref="State"/> is
        /// <see cref="SessionState.Open"/>. Never write it to the log or to the browser storage.
        /// </summary>
        public string SessionToken { get; private set; }

        /// <summary>
        /// Whether a session opening is waiting for the API.
        /// </summary>
        public bool IsOpening => openingOperation != null;

        /// <summary>
        /// Whether nothing may be collected or sent in this page load: a guest who refused the
        /// recording, the collection off, or a session refused with 400 (gla-14 D7, gla-15 D6).
        /// </summary>
        public bool IsCollectionBlocked =>
            Mode == PlayerMode.GuestRefused || Mode == PlayerMode.Disabled || State == SessionState.Rejected;

        #endregion

        #region Configuration

        /// <summary>
        /// Turns the collection off (configuration off, missing, invalid or not read in 5 seconds):
        /// from <see cref="PlayerMode.Loading"/> to <see cref="PlayerMode.Disabled"/>, with the reason in the log.
        /// </summary>
        /// <param name="reason">Why, in English.</param>
        public void Disable(string reason)
        {
            if (Mode != PlayerMode.Loading)
            {
                log.Warning($"The collection cannot be turned off in mode {Mode}.");
                return;
            }

            Mode = PlayerMode.Disabled;
            log.Info($"Collection off: {reason}. The game runs without the entry screen and without requests to the API.");
            NotifyChanged();
        }

        /// <summary>
        /// Turns the collection on with the API of the configuration (design D3): a valid stored
        /// credential skips the entry screen and opens an identified session; an expired or unreadable
        /// one is removed; without a credential, the mode becomes <see cref="PlayerMode.Undecided"/>.
        /// Interacts with <see cref="CredentialStore.Load"/>.
        /// </summary>
        /// <param name="apiClient">The client of the API.</param>
        /// <exception cref="ArgumentNullException"><paramref name="apiClient"/> is <c>null</c>.</exception>
        public void Enable(GlaApiClient apiClient)
        {
            if (apiClient == null) throw new ArgumentNullException(nameof(apiClient));

            if (Mode != PlayerMode.Loading)
            {
                log.Warning($"The collection cannot be turned on in mode {Mode}.");
                return;
            }

            api = apiClient;
            StoredCredentialStatus status = credentials.Load(utcNow(), out string stored, out DateTimeOffset expiresAt);
            switch (status)
            {
                case StoredCredentialStatus.Valid:
                    credential = stored;
                    credentialExpiresAt = expiresAt;
                    Mode = PlayerMode.Identified;
                    log.Info($"Stored credential valid until {GlaTime.Format(expiresAt)}: the entry screen is skipped.");
                    NotifyChanged();
                    OpenSessionAsync();
                    return;
                case StoredCredentialStatus.Expired:
                    log.Info($"The stored credential expired at {GlaTime.Format(expiresAt)} and was removed.");
                    break;
                case StoredCredentialStatus.Unreadable:
                    log.Warning("The stored credential was unreadable and was removed.");
                    break;
                default:
                    log.Info("No stored credential.");
                    break;
            }

            Mode = PlayerMode.Undecided;
            NotifyChanged();
        }

        #endregion

        #region Entry Screen

        /// <summary>
        /// Asks the API for a code sent to the e-mail (<see cref="GlaApiClient.RequestCodeAsync"/>).
        /// Only in <see cref="PlayerMode.Undecided"/>; otherwise nothing is sent. The e-mail is not kept.
        /// </summary>
        /// <param name="email">The e-mail as typed.</param>
        /// <returns>The answer, whose <see cref="ApiError"/> message the entry screen shows.</returns>
        public GlaOperation<ApiResponse> RequestCodeAsync(string email)
        {
            if (Mode != PlayerMode.Undecided || api == null)
                return GlaOperation<ApiResponse>.FromResult(new ApiResponse(ApiResponseKind.NotSent, 0, null, $"mode {Mode}"));

            return api.RequestCodeAsync(email).OnCompleted(response => log.Info($"Code request -> {response}."));
        }

        /// <summary>
        /// Verifies the code (<see cref="GlaApiClient.VerifyCodeAsync"/>). On success, stores the
        /// credential and its validity (never the e-mail), turns the mode into
        /// <see cref="PlayerMode.Identified"/> and opens the identified session. A success that arrives
        /// after the player chose another way in is ignored. Only in <see cref="PlayerMode.Undecided"/>.
        /// </summary>
        /// <param name="email">The e-mail of the code request.</param>
        /// <param name="code">The code typed.</param>
        /// <returns>The answer, whose <see cref="ApiError"/> message the entry screen shows.</returns>
        public GlaOperation<ApiResponse<CodeVerificationResult>> VerifyCodeAsync(string email, string code)
        {
            if (Mode != PlayerMode.Undecided || api == null)
            {
                var notSent = new ApiResponse<CodeVerificationResult>(ApiResponseKind.NotSent, 0, null, null, $"mode {Mode}");
                return GlaOperation<ApiResponse<CodeVerificationResult>>.FromResult(notSent);
            }

            int version = identityVersion;
            var operation = new GlaOperation<ApiResponse<CodeVerificationResult>>();
            api.VerifyCodeAsync(email, code).OnCompleted(response =>
            {
                log.Info($"Code verification -> {response}.");
                if (response.IsSuccess)
                {
                    if (version == identityVersion && Mode == PlayerMode.Undecided)
                        AcceptCredential(response.Value);
                    else
                        log.Info("The verification ended after the player chose another way in; its credential was ignored.");
                }

                operation.Complete(response);
            });
            return operation;
        }

        /// <summary>
        /// "Entrar como convidado", after the consent question: with consent, the mode becomes
        /// <see cref="PlayerMode.Guest"/> and a guest session is opened; without it, the mode becomes
        /// <see cref="PlayerMode.GuestRefused"/>, and no session is opened and nothing is sent until
        /// the page is reloaded. Only in <see cref="PlayerMode.Undecided"/>.
        /// </summary>
        /// <param name="acceptsRecording">Whether the guest accepts the anonymous recording of the attempts.</param>
        public void EnterAsGuest(bool acceptsRecording)
        {
            if (Mode != PlayerMode.Undecided)
            {
                log.Warning($"Guest entry ignored in mode {Mode}.");
                return;
            }

            identityVersion++;
            if (acceptsRecording)
            {
                Mode = PlayerMode.Guest;
                log.Info("Guest who accepted the anonymous recording.");
                NotifyChanged();
                OpenSessionAsync();
                return;
            }

            Mode = PlayerMode.GuestRefused;
            log.Info("Guest who refused the recording: no session is opened and nothing is sent until the page is reloaded.");
            NotifyChanged();
        }

        /// <summary>
        /// "Trocar e-mail" / "Identificar-me" (design D5): removes the stored credential, drops the
        /// session token from memory and returns to <see cref="PlayerMode.Undecided"/>, which shows the
        /// entry screen. Only for <see cref="PlayerMode.Identified"/> and <see cref="PlayerMode.Guest"/>,
        /// and never after a session refused with 400, so that no other request is made in that page
        /// load. An opening still running is superseded. Records already queued by the delivery keep
        /// their own token (gla-15 D2).
        /// </summary>
        public void ResetIdentity()
        {
            if ((Mode != PlayerMode.Identified && Mode != PlayerMode.Guest) || State == SessionState.Rejected)
            {
                log.Info($"Identity reset ignored in mode {Mode}, session {State}.");
                return;
            }

            log.Info("Identity reset: the credential and the session token were discarded.");
            DiscardIdentity();
        }

        #endregion

        #region Sessions

        /// <summary>
        /// Opens a new session for the current mode (design D4): <c>POST /sessions</c> with the game
        /// version, the credential (identified) or <c>null</c> (guest) and <c>started_at</c> =
        /// <see cref="PageLoadedAt"/>. While it runs, the state is <see cref="SessionState.Pending"/>,
        /// and another call joins the same opening. The answers:
        /// <list type="bullet">
        /// <item>201: <see cref="SessionState.Open"/>, with the token in memory;</item>
        /// <item>network error, timeout, 429, 5xx or an unreadable answer: <see cref="SessionState.Pending"/>;</item>
        /// <item>401 <c>invalid_credential</c>: the credential is removed and the mode becomes <see cref="PlayerMode.Undecided"/>;</item>
        /// <item>400: <see cref="SessionState.Rejected"/> until the page is reloaded, with the message in the log and no retry.</item>
        /// </list>
        /// No request is made in the modes without session, after a 400, or for an identified player
        /// whose credential expired in the browser (then the credential is removed as with
        /// <c>invalid_credential</c>).
        /// </summary>
        /// <returns>The opening; a Unity coroutine may wait for it with <c>yield return</c>.</returns>
        public GlaOperation<SessionOpenResult> OpenSessionAsync()
        {
            if (openingOperation != null) return openingOperation;

            if (State == SessionState.Rejected)
                return GlaOperation<SessionOpenResult>.FromResult(new SessionOpenResult(SessionOpenOutcome.Rejected, null));

            if ((Mode != PlayerMode.Identified && Mode != PlayerMode.Guest) || api == null)
                return GlaOperation<SessionOpenResult>.FromResult(new SessionOpenResult(SessionOpenOutcome.NotAllowed, null));

            bool identified = Mode == PlayerMode.Identified;
            if (identified && (string.IsNullOrEmpty(credential) || utcNow() >= credentialExpiresAt))
            {
                log.Info($"The credential expired at {GlaTime.Format(credentialExpiresAt)}: it was removed, and the entry screen returns in the next menu.");
                DiscardIdentity();
                return GlaOperation<SessionOpenResult>.FromResult(new SessionOpenResult(SessionOpenOutcome.CredentialRejected, null));
            }

            var operation = new GlaOperation<SessionOpenResult>();
            openingOperation = operation;
            int version = identityVersion;
            SessionToken = null;
            State = SessionState.Pending;
            log.Info($"Opening a {(identified ? "identified" : "guest")} session, started_at={GlaTime.Format(PageLoadedAt)}.");
            NotifyChanged();

            var opening = new SessionOpening
            {
                GameVersion = GameVersion,
                Credential = identified ? credential : null,
                StartedAt = PageLoadedAt,
            };
            api.OpenSessionAsync(opening).OnCompleted(response => CompleteOpening(operation, version, identified, response));
            return operation;
        }

        /// <summary>
        /// Applies the answer of a session opening and completes it, unless the identity changed meanwhile.
        /// </summary>
        /// <param name="operation">The opening.</param>
        /// <param name="version">The identity version when it started.</param>
        /// <param name="identified">Whether it carried the credential.</param>
        /// <param name="response">The answer of <c>POST /sessions</c>.</param>
        private void CompleteOpening(GlaOperation<SessionOpenResult> operation, int version, bool identified, ApiResponse<SessionOpened> response)
        {
            if (openingOperation == operation) openingOperation = null;

            if (version != identityVersion)
            {
                log.Info($"Session opening -> {response}, ignored: the player changed identity meanwhile.");
                operation.Complete(new SessionOpenResult(SessionOpenOutcome.Superseded, response));
                return;
            }

            SessionOpenOutcome outcome;
            if (response.IsSuccess)
            {
                SessionToken = response.Value.SessionToken;
                State = SessionState.Open;
                outcome = SessionOpenOutcome.Opened;
                log.Info($"Session opened -> {response}, identified={response.Value.Identified}.");
                if (response.Value.Identified != identified)
                    log.Warning($"The API opened a session with identified={response.Value.Identified} for mode {Mode}.");
            }
            else if (response.StatusCode == 400)
            {
                SessionToken = null;
                State = SessionState.Rejected;
                outcome = SessionOpenOutcome.Rejected;
                string reason = response.Error != null ? response.Error.Message : response.Problem;
                log.Warning($"Session opening refused -> {response}: {reason} Nothing is collected or sent until the page is reloaded.");
            }
            else if (identified && response.StatusCode == 401 && response.ErrorCode == ApiErrorCodes.InvalidCredential)
            {
                outcome = SessionOpenOutcome.CredentialRejected;
                log.Info($"Session opening -> {response}: the credential was removed, and the entry screen returns in the menu.");
                DiscardIdentity();
                operation.Complete(new SessionOpenResult(outcome, response));
                return;
            }
            else
            {
                State = SessionState.Pending;
                outcome = SessionOpenOutcome.TransientFailure;
                log.Warning($"Session opening failed -> {response}; the session stays Pending and is opened again before sending.");
            }

            NotifyChanged();
            operation.Complete(new SessionOpenResult(outcome, response));
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Stores the credential of a successful verification and opens the identified session.
        /// Interacts with <see cref="CredentialStore.Save"/>.
        /// </summary>
        /// <param name="result">The verification result.</param>
        private void AcceptCredential(CodeVerificationResult result)
        {
            if (string.IsNullOrEmpty(result.Credential))
            {
                log.Warning("The verification answer has an empty credential; it was ignored.");
                return;
            }

            identityVersion++;
            credential = result.Credential;
            credentialExpiresAt = result.ExpiresAt;
            credentials.Save(credential, credentialExpiresAt);
            Mode = PlayerMode.Identified;
            State = SessionState.None;
            SessionToken = null;
            log.Info($"E-mail verified: credential stored until {GlaTime.Format(credentialExpiresAt)}.");
            NotifyChanged();
            OpenSessionAsync();
        }

        /// <summary>
        /// Removes the credential from memory and from the storage, drops the session and returns to
        /// <see cref="PlayerMode.Undecided"/>. Interacts with <see cref="CredentialStore.Clear"/>.
        /// </summary>
        private void DiscardIdentity()
        {
            identityVersion++;
            openingOperation = null;
            credential = null;
            credentialExpiresAt = default;
            credentials.Clear();
            SessionToken = null;
            State = SessionState.None;
            Mode = PlayerMode.Undecided;
            NotifyChanged();
        }

        /// <summary>
        /// Raises <see cref="Changed"/> for each listener, logging the exception of any of them.
        /// </summary>
        private void NotifyChanged()
        {
            Action handlers = Changed;
            if (handlers == null) return;

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception exception)
                {
                    log.Warning($"A listener of PlayerSession.Changed failed: {exception}");
                }
            }
        }

        #endregion
    }
}
