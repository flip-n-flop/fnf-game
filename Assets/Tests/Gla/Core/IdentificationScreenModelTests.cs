using System;
using System.Linq;
using FlipNflop.Commons.Errors;
using FlipNflop.Commons.Messages;
using NUnit.Framework;

namespace FlipNflop.Gla.Tests
{
    /// <summary>
    /// EditMode tests of <see cref="IdentificationScreenModel"/> (gla-13 D2 and D6): the steps e-mail,
    /// code and guest consent, "Reenviar código", "Voltar", the busy state and the messages (the
    /// <c>ApiError</c> message or the network message). Interacts with <see cref="PlayerSession"/>
    /// over <see cref="FakeHttpTransport"/>.
    /// </summary>
    public class IdentificationScreenModelTests
    {
        #region Constants

        private const string Email = "aluno@estudante.ufjf.br";

        #endregion

        #region Fields

        private DateTimeOffset now;
        private FakeHttpTransport transport;
        private MemoryStorage storage;
        private PlayerSession session;
        private IdentificationScreenModel model;

        #endregion

        #region Setup

        /// <summary>
        /// Creates the model of a first visit, in <see cref="PlayerMode.Undecided"/>.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            now = new DateTimeOffset(2026, 10, 20, 17, 33, 0, TimeSpan.Zero);
            transport = new FakeHttpTransport();
            storage = new MemoryStorage();
            session = new PlayerSession(now.AddMinutes(-1), "0.15.0", new CredentialStore(storage), () => now, new RecordingLog());
            session.Enable(new GlaApiClient("http://localhost:5000", transport));
            model = new IdentificationScreenModel(session);
        }

        #endregion

        #region Tests

        /// <summary>
        /// Scenario "Primeira visita": the screen starts at the e-mail step, without message.
        /// </summary>
        [Test]
        public void StartsAtTheEmailStep()
        {
            Assert.That(model.Step, Is.EqualTo(IdentificationStep.Email));
            Assert.That(model.Message, Is.EqualTo(IdentificationMessage.None));
            Assert.That(model.Busy, Is.False);
        }

        /// <summary>
        /// Scenario "Domínio recusado": the API message is shown and the e-mail step stays.
        /// </summary>
        [Test]
        public void RefusedDomainShowsTheApiMessageAndKeepsTheEmailStep()
        {
            transport.Enqueue(ApiAnswers.Error(400, ApiErrorCodes.DomainNotAllowed, ApiAnswers.DomainNotAllowedMessage));

            model.RequestCode("aluno@gmail.com");

            Assert.That(model.Step, Is.EqualTo(IdentificationStep.Email));
            Assert.That(model.Message, Is.EqualTo(IdentificationMessage.ApiMessage));
            Assert.That(model.ApiMessageText, Is.EqualTo(ApiAnswers.DomainNotAllowedMessage));
            Assert.That(model.Busy, Is.False);
        }

        /// <summary>
        /// Requirement "Falha de rede não bloqueia o jogo": the e-mail flow shows the network message.
        /// </summary>
        /// <param name="failure">The failure.</param>
        [TestCase("network")]
        [TestCase("timeout")]
        [TestCase("502-html")]
        public void NetworkFailureShowsTheNetworkMessage(string failure)
        {
            transport.Enqueue(failure == "network"
                ? ApiAnswers.NetworkFailure()
                : failure == "timeout" ? ApiAnswers.TimeoutFailure() : HttpResult.Answered(502, "<html></html>"));

            model.RequestCode(Email);

            Assert.That(model.Step, Is.EqualTo(IdentificationStep.Email));
            Assert.That(model.Message, Is.EqualTo(IdentificationMessage.NetworkError));
            Assert.That(model.ApiMessageText, Is.Null);
        }

        /// <summary>
        /// The 500 of an e-mail that could not be sent and the 429 show the API message.
        /// </summary>
        [Test]
        public void ServerErrorsWithBodyShowTheApiMessage()
        {
            transport.Enqueue(ApiAnswers.Error(500, ApiErrorCodes.InternalError, ApiAnswers.CodeNotSentMessage));
            model.RequestCode(Email);
            Assert.That(model.ApiMessageText, Is.EqualTo(ApiAnswers.CodeNotSentMessage));

            transport.Enqueue(ApiAnswers.Error(429, ApiErrorCodes.RateLimited, ApiAnswers.RateLimitedMessage));
            model.RequestCode(Email);
            Assert.That(model.ApiMessageText, Is.EqualTo(ApiAnswers.RateLimitedMessage));
        }

        /// <summary>
        /// A request accepted goes to the code step; "Reenviar código" asks again for the same e-mail
        /// and says so; "Voltar" returns to the e-mail step.
        /// </summary>
        [Test]
        public void CodeStepResendsAndGoesBack()
        {
            transport.Enqueue(ApiAnswers.NoContent());
            model.RequestCode(Email);
            Assert.That(model.Step, Is.EqualTo(IdentificationStep.Code));

            transport.Enqueue(ApiAnswers.NoContent());
            model.ResendCode();
            Assert.That(model.Step, Is.EqualTo(IdentificationStep.Code));
            Assert.That(model.Message, Is.EqualTo(IdentificationMessage.CodeResent));
            Assert.That(transport.Requests.Select(request => request.Json), Is.All.EqualTo("{\"email\":\"" + Email + "\"}"));

            model.Back();
            Assert.That(model.Step, Is.EqualTo(IdentificationStep.Email));
            Assert.That(model.Message, Is.EqualTo(IdentificationMessage.None));
        }

        /// <summary>
        /// A wrong code keeps the code step with the API message; the right one identifies the player.
        /// </summary>
        [Test]
        public void ConfirmShowsTheErrorOrIdentifies()
        {
            transport.Enqueue(ApiAnswers.NoContent());
            model.RequestCode(Email);

            transport.Enqueue(ApiAnswers.Error(400, ApiErrorCodes.InvalidCode, ApiAnswers.InvalidCodeMessage));
            model.ConfirmCode("000000");
            Assert.That(model.Step, Is.EqualTo(IdentificationStep.Code));
            Assert.That(model.ApiMessageText, Is.EqualTo(ApiAnswers.InvalidCodeMessage));
            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Undecided));

            transport.Enqueue(ApiAnswers.Body(200, new CodeVerificationResult { Credential = "v1.credential", ExpiresAt = now.AddHours(4) }));
            model.ConfirmCode(" 123456 ");
            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Identified));
            Assert.That(model.Message, Is.EqualTo(IdentificationMessage.None));
            Assert.That(transport.Requests[2].Json, Is.EqualTo("{\"email\":\"" + Email + "\",\"code\":\"123456\"}"));
        }

        /// <summary>
        /// While a request runs, every action is ignored, and the buttons can be disabled.
        /// </summary>
        [Test]
        public void ActionsAreIgnoredWhileBusy()
        {
            int changes = 0;
            model.Changed += () => changes++;

            model.RequestCode(Email);
            model.RequestCode(Email);
            model.ChooseGuest();

            Assert.That(model.Busy, Is.True);
            Assert.That(model.Step, Is.EqualTo(IdentificationStep.Email));
            Assert.That(transport.Requests.Count, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(1));

            transport.Requests[0].Answer(ApiAnswers.NoContent());
            Assert.That(model.Busy, Is.False);
            Assert.That(model.Step, Is.EqualTo(IdentificationStep.Code));
        }

        /// <summary>
        /// Requirement "Convidado com consentimento": the guest question opens a session when accepted
        /// and none when refused.
        /// </summary>
        /// <param name="accepts">The answer.</param>
        [TestCase(true)]
        [TestCase(false)]
        public void GuestQuestionAnswersTheSession(bool accepts)
        {
            model.ChooseGuest();
            Assert.That(model.Step, Is.EqualTo(IdentificationStep.GuestConsent));

            transport.Enqueue(ApiAnswers.Body(201, new SessionOpened { SessionToken = "s1.guest", Identified = false }));
            model.AnswerGuestConsent(accepts);

            Assert.That(session.Mode, Is.EqualTo(accepts ? PlayerMode.Guest : PlayerMode.GuestRefused));
            Assert.That(transport.Requests.Count, Is.EqualTo(accepts ? 1 : 0));
        }

        /// <summary>
        /// "Voltar" leaves the guest question; "Entrar como convidado" is also offered at the code step.
        /// </summary>
        [Test]
        public void GuestQuestionIsReachableFromBothSteps()
        {
            model.ChooseGuest();
            model.Back();
            Assert.That(model.Step, Is.EqualTo(IdentificationStep.Email));

            transport.Enqueue(ApiAnswers.NoContent());
            model.RequestCode(Email);
            model.ChooseGuest();
            Assert.That(model.Step, Is.EqualTo(IdentificationStep.GuestConsent));
        }

        /// <summary>
        /// Reset forgets the e-mail and ignores an answer still running.
        /// </summary>
        [Test]
        public void ResetIgnoresTheRunningAnswer()
        {
            model.RequestCode(Email);

            model.Reset();
            transport.Requests[0].Answer(ApiAnswers.NoContent());

            Assert.That(model.Step, Is.EqualTo(IdentificationStep.Email));
            Assert.That(model.Busy, Is.False);
            Assert.That(model.Message, Is.EqualTo(IdentificationMessage.None));
        }

        #endregion
    }
}
