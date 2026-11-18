namespace FlipNflop.Gla
{
    /// <summary>
    /// How the player entered the game in this page load (design D1 of gla-13). Read by the entry
    /// screen, by the attempt collection (gla-14 D7) and by the delivery (gla-15 D6).
    /// </summary>
    public enum PlayerMode
    {
        /// <summary>The configuration is being read; the menu is covered by the waiting screen.</summary>
        Loading,

        /// <summary>No choice yet (or the credential was lost): the entry screen is shown in the menu.</summary>
        Undecided,

        /// <summary>The player verified the institutional e-mail; the sessions carry the credential.</summary>
        Identified,

        /// <summary>A guest who accepted the anonymous recording; the sessions have no credential.</summary>
        Guest,

        /// <summary>A guest who refused the recording: no session and no request until the page is reloaded.</summary>
        GuestRefused,

        /// <summary>The collection is off (configuration off, missing, invalid or late): no request at all.</summary>
        Disabled,
    }

    /// <summary>
    /// The state of the game session of the current mode (design D4 of gla-13).
    /// </summary>
    public enum SessionState
    {
        /// <summary>The mode has no session (<see cref="PlayerMode.Loading"/>, <see cref="PlayerMode.Undecided"/>, <see cref="PlayerMode.GuestRefused"/> or <see cref="PlayerMode.Disabled"/>).</summary>
        None,

        /// <summary>The session is being opened, or the opening failed (network, timeout, 429, 5xx) and will be tried again before sending (gla-15 D4).</summary>
        Pending,

        /// <summary>The session is open and its token is in memory.</summary>
        Open,

        /// <summary>The API refused the opening with 400 (a computer clock ahead): nothing more is sent until the page is reloaded.</summary>
        Rejected,
    }

    /// <summary>
    /// The outcome of <see cref="PlayerSession.OpenSessionAsync"/>.
    /// </summary>
    public enum SessionOpenOutcome
    {
        /// <summary>The session was opened (201).</summary>
        Opened,

        /// <summary>Network error, timeout, 429, 5xx or an unreadable answer: the state is <see cref="SessionState.Pending"/>.</summary>
        TransientFailure,

        /// <summary><c>invalid_credential</c> (401), or the credential expired in the browser: the credential was discarded and the mode is <see cref="PlayerMode.Undecided"/>.</summary>
        CredentialRejected,

        /// <summary>The API refused the opening with 400, now or earlier in this page load: the state is <see cref="SessionState.Rejected"/>.</summary>
        Rejected,

        /// <summary>The mode does not open sessions; no request was made.</summary>
        NotAllowed,

        /// <summary>The player changed identity or mode while the opening ran; its answer was ignored.</summary>
        Superseded,
    }

    /// <summary>
    /// The result of <see cref="PlayerSession.OpenSessionAsync"/>: the outcome and the answer of the
    /// API, when a request was made. Never exposes the session token.
    /// </summary>
    public sealed class SessionOpenResult
    {
        #region Construction

        /// <summary>
        /// Creates a result.
        /// </summary>
        /// <param name="outcome">The outcome.</param>
        /// <param name="response">The answer of the API, or <c>null</c> without a request.</param>
        internal SessionOpenResult(SessionOpenOutcome outcome, ApiResponse response)
        {
            Outcome = outcome;
            Response = response;
        }

        #endregion

        #region Properties

        public SessionOpenOutcome Outcome { get; }

        /// <summary>
        /// The answer of the API, or <c>null</c> when no request was made.
        /// </summary>
        public ApiResponse Response { get; }

        #endregion
    }
}
