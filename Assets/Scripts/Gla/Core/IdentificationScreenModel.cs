using System;

namespace FlipNflop.Gla
{
    /// <summary>
    /// The step of the entry screen shown in <see cref="PlayerMode.Undecided"/>.
    /// </summary>
    public enum IdentificationStep
    {
        /// <summary>E-mail (optional), "Receber código" and "Entrar como convidado", with the consent notice.</summary>
        Email,

        /// <summary>Code, "Confirmar", "Reenviar código", "Voltar" and "Entrar como convidado".</summary>
        Code,

        /// <summary>The question about the anonymous recording of a guest.</summary>
        GuestConsent,
    }

    /// <summary>
    /// The message under the entry screen (design D6).
    /// </summary>
    public enum IdentificationMessage
    {
        None,

        /// <summary>The Portuguese <c>message</c> of the <c>ApiError</c>, in <see cref="IdentificationScreenModel.ApiMessageText"/>.</summary>
        ApiMessage,

        /// <summary>"Não foi possível falar com o servidor. Tente de novo ou entre como convidado."</summary>
        NetworkError,

        /// <summary>A new code was sent ("Reenviar código").</summary>
        CodeResent,
    }

    /// <summary>
    /// Engine-free logic of the entry screen (gla-13 designs D2 and D6): the current step, whether a
    /// request is running, and the message to show. Turns the buttons into calls to
    /// <see cref="PlayerSession"/> and keeps the e-mail of the code request only in memory.
    /// Drawn by <c>IdentificationScreenController</c>, which hides the screen when the
    /// <see cref="PlayerSession.Mode"/> leaves <see cref="PlayerMode.Undecided"/>.
    /// While <see cref="Busy"/>, every action is ignored, so that two answers never race.
    /// </summary>
    public sealed class IdentificationScreenModel
    {
        #region Fields

        private readonly PlayerSession session;
        private string requestedEmail;
        private int requestVersion;

        #endregion

        #region Construction

        /// <summary>
        /// Creates the model of the entry screen, at the e-mail step.
        /// </summary>
        /// <param name="session">The identification state that makes the requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
        public IdentificationScreenModel(PlayerSession session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised after every change of the step, of <see cref="Busy"/> or of the message.
        /// </summary>
        public event Action Changed;

        #endregion

        #region Properties

        public IdentificationStep Step { get; private set; } = IdentificationStep.Email;

        public bool Busy { get; private set; }

        public IdentificationMessage Message { get; private set; } = IdentificationMessage.None;

        /// <summary>
        /// The text of the <c>ApiError</c> when <see cref="Message"/> is <see cref="IdentificationMessage.ApiMessage"/>.
        /// </summary>
        public string ApiMessageText { get; private set; }

        #endregion

        #region Actions

        /// <summary>
        /// "Receber código": asks for the code with <see cref="PlayerSession.RequestCodeAsync"/>. On
        /// success, goes to the code step; on failure, stays at the e-mail step with the message.
        /// </summary>
        /// <param name="email">The e-mail typed.</param>
        public void RequestCode(string email)
        {
            if (Busy || Step != IdentificationStep.Email) return;

            int version = StartRequest();
            session.RequestCodeAsync(email).OnCompleted(response =>
            {
                if (version != requestVersion) return;

                Busy = false;
                if (response.IsSuccess)
                {
                    requestedEmail = email;
                    Step = IdentificationStep.Code;
                    ClearMessage();
                }
                else
                {
                    ShowFailure(response);
                }

                NotifyChanged();
            });
        }

        /// <summary>
        /// "Reenviar código": asks for a new code for the same e-mail and stays at the code step.
        /// </summary>
        public void ResendCode()
        {
            if (Busy || Step != IdentificationStep.Code) return;

            int version = StartRequest();
            session.RequestCodeAsync(requestedEmail).OnCompleted(response =>
            {
                if (version != requestVersion) return;

                Busy = false;
                if (response.IsSuccess)
                    Message = IdentificationMessage.CodeResent;
                else
                    ShowFailure(response);

                NotifyChanged();
            });
        }

        /// <summary>
        /// "Confirmar": verifies the code with <see cref="PlayerSession.VerifyCodeAsync"/>. On success,
        /// the session becomes identified and the screen closes; on failure, stays at the code step
        /// with the message.
        /// </summary>
        /// <param name="code">The code typed; spaces at the ends are removed.</param>
        public void ConfirmCode(string code)
        {
            if (Busy || Step != IdentificationStep.Code) return;

            int version = StartRequest();
            session.VerifyCodeAsync(requestedEmail, code?.Trim()).OnCompleted(response =>
            {
                if (version != requestVersion) return;

                Busy = false;
                if (response.IsSuccess)
                    ClearMessage();
                else
                    ShowFailure(response);

                NotifyChanged();
            });
        }

        /// <summary>
        /// "Voltar": returns from the code step or from the guest question to the e-mail step.
        /// </summary>
        public void Back()
        {
            if (Busy || Step == IdentificationStep.Email) return;

            Step = IdentificationStep.Email;
            ClearMessage();
            NotifyChanged();
        }

        /// <summary>
        /// "Entrar como convidado": shows the question about the anonymous recording.
        /// </summary>
        public void ChooseGuest()
        {
            if (Busy || Step == IdentificationStep.GuestConsent) return;

            Step = IdentificationStep.GuestConsent;
            ClearMessage();
            NotifyChanged();
        }

        /// <summary>
        /// Answers the guest question with <see cref="PlayerSession.EnterAsGuest"/>.
        /// </summary>
        /// <param name="acceptsRecording">Whether the guest accepts the anonymous recording.</param>
        public void AnswerGuestConsent(bool acceptsRecording)
        {
            if (Busy || Step != IdentificationStep.GuestConsent) return;

            session.EnterAsGuest(acceptsRecording);
        }

        /// <summary>
        /// Returns to the e-mail step without message and forgets the e-mail of the code request;
        /// an answer still running is ignored. Called when the entry screen is shown again.
        /// </summary>
        public void Reset()
        {
            requestVersion++;
            requestedEmail = null;
            Busy = false;
            Step = IdentificationStep.Email;
            ClearMessage();
            NotifyChanged();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Marks a request as running and clears the message.
        /// </summary>
        /// <returns>The version of the request, to recognize its answer.</returns>
        private int StartRequest()
        {
            Busy = true;
            ClearMessage();
            int version = ++requestVersion;
            NotifyChanged();
            return version;
        }

        /// <summary>
        /// Shows the message of a failure (design D6): the <c>ApiError</c> message when the API sent
        /// one, otherwise the network message.
        /// </summary>
        /// <param name="response">The failed answer.</param>
        private void ShowFailure(ApiResponse response)
        {
            if (response.Kind == ApiResponseKind.ApiError && !string.IsNullOrWhiteSpace(response.Error.Message))
            {
                Message = IdentificationMessage.ApiMessage;
                ApiMessageText = response.Error.Message;
                return;
            }

            Message = IdentificationMessage.NetworkError;
            ApiMessageText = null;
        }

        /// <summary>
        /// Removes the message.
        /// </summary>
        private void ClearMessage()
        {
            Message = IdentificationMessage.None;
            ApiMessageText = null;
        }

        /// <summary>
        /// Raises <see cref="Changed"/>.
        /// </summary>
        private void NotifyChanged()
        {
            Changed?.Invoke();
        }

        #endregion
    }
}
