using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FlipNflop.Commons.Errors;
using FlipNflop.Commons.Messages;
using FlipNflop.Commons.Records;
using NUnit.Framework;

namespace FlipNflop.Gla.Tests
{
    /// <summary>Exercises ordered delivery and identity isolation with the real session and API client.</summary>
    public sealed class AttemptDeliveryQueueTests
    {
        private FakeHttpTransport transport;
        private RecordingLog log;
        private PlayerSession session;
        private AttemptDeliveryQueue queue;
        private List<int> waits;
        private DateTimeOffset now;
        private CredentialStore credentials;
        private GlaApiClient api;

        [SetUp]
        public void SetUp()
        {
            now = DateTimeOffset.UtcNow;
            transport = new FakeHttpTransport();
            log = new RecordingLog();
            credentials = new CredentialStore(new MemoryStorage());
            session = new PlayerSession(now, "0.15.0", credentials, () => now, log);
            waits = new List<int>();
            queue = new AttemptDeliveryQueue(session, log, seconds => { waits.Add(seconds); return seconds; }, true);
            api = new GlaApiClient("http://localhost:5000", transport);
        }

        private static HttpResult Opened(string token, bool identified = false) =>
            ApiAnswers.Body(201, new SessionOpened { SessionToken = token, Identified = identified });

        private void Guest(HttpResult opening)
        {
            session.Enable(api);
            transport.Enqueue(opening);
            session.EnterAsGuest(true);
        }

        private Phase Record(string phase = "first_level") => new Phase
        {
            PhaseName = phase, Sections = new List<Section> { new Section { DateTimeStart = now, PathPlayer = new PathPlayer { EndCause = EndCause.Exit } } }
        };

        private static void Run(IEnumerator iterator)
        {
            while (iterator.MoveNext())
                if (iterator.Current is IEnumerator child) Run(child);
        }

        [Test]
        public void PreservesBytesOrderAndTokenThroughRetryAndIdentityReset()
        {
            Guest(Opened("old"));
            var first = Record();
            queue.Enqueue(first);
            first.PhaseName = "changed";
            queue.Enqueue(Record("second_level"));
            session.ResetIdentity();
            transport.Enqueue(Opened("new"));
            session.EnterAsGuest(true);
            transport.Enqueue(ApiAnswers.NetworkFailure());
            transport.Enqueue(ApiAnswers.NoContent());
            transport.Enqueue(ApiAnswers.NoContent());
            Run(queue.Drain());
            var sends = transport.Requests.Where(r => r.Url.EndsWith("/attempts")).ToArray();
            Assert.That(sends.Length, Is.EqualTo(3));
            Assert.That(sends.Select(r => r.BearerToken), Is.All.EqualTo("old"));
            Assert.That(sends[0].Json, Is.EqualTo(sends[1].Json).And.Contains("first_level"));
            Assert.That(sends[2].Json, Does.Contain("second_level"));
            Assert.That(waits, Is.EqualTo(new[] { 2 }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BoundsNetworkRetriesAndDiscardsWaitingGroupWhenOpeningFails(bool pending)
        {
            Guest(pending ? ApiAnswers.TimeoutFailure() : Opened("session"));
            queue.Enqueue(Record());
            if (pending) queue.Enqueue(Record());
            for (int i = 0; i < 6; i++) transport.Enqueue(ApiAnswers.NetworkFailure());
            Run(queue.Drain());
            Assert.That(waits, Is.EqualTo(new[] { 2, 4, 8, 16, 32 }));
            Assert.That(transport.Requests.Count, Is.EqualTo(7));
            Assert.That(queue.HasPending, Is.False);
        }

        [Test]
        public void PendingGroupAttachesToItsOpeningEvenIfIdentityChangesBeforeDrain()
        {
            Guest(ApiAnswers.TimeoutFailure());
            queue.Enqueue(Record());
            transport.Enqueue(Opened("first"));
            session.OpenSessionAsync();
            session.ResetIdentity();
            transport.Enqueue(Opened("second"));
            session.EnterAsGuest(true);
            transport.Enqueue(ApiAnswers.NoContent());
            Run(queue.Drain());
            Assert.That(transport.Requests.Last().BearerToken, Is.EqualTo("first"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExpirationRetokensTheGroupForItsOriginalIdentity(bool identified)
        {
            if (identified)
            {
                credentials.Save("original-credential", now.AddHours(4));
                transport.Enqueue(Opened("first", true));
                session.Enable(api);
            }
            else Guest(Opened("first"));
            queue.Enqueue(Record());
            queue.Enqueue(Record());
            transport.Enqueue(ApiAnswers.Error(401, ApiErrorCodes.InvalidSession, "expired"));
            transport.Enqueue(Opened("second", identified));
            transport.Enqueue(ApiAnswers.NoContent());
            transport.Enqueue(ApiAnswers.NoContent());
            Run(queue.Drain());
            Assert.That(transport.Requests.Where(r => r.Url.EndsWith("/attempts")).Select(r => r.BearerToken),
                Is.EqualTo(new[] { "first", "second", "second" }));
            Assert.That(transport.Requests[0].Json, Is.EqualTo(transport.Requests[2].Json));
        }

        [Test]
        public void ExpiredCredentialDiscardsGroupAndLaterAttemptsUntilMenuChoice()
        {
            credentials.Save("original-credential", now.AddHours(1));
            transport.Enqueue(Opened("first", true));
            session.Enable(api);
            queue.Enqueue(Record());
            queue.Enqueue(Record());
            now = now.AddHours(2);
            transport.Enqueue(ApiAnswers.Error(401, ApiErrorCodes.InvalidSession, "expired"));
            Run(queue.Drain());
            Assert.That(session.Mode, Is.EqualTo(PlayerMode.Undecided));
            Assert.That(queue.Enqueue(Record()), Is.False);
            Assert.That(transport.Requests.Count, Is.EqualTo(2));
        }

        [Test]
        public void RejectedOpeningEndsDeliveryUntilReload()
        {
            Guest(ApiAnswers.TimeoutFailure());
            queue.Enqueue(Record());
            queue.Enqueue(Record());
            transport.Enqueue(ApiAnswers.Error(400, ApiErrorCodes.InvalidRequest, "clock"));
            Run(queue.Drain());
            Assert.That(session.State, Is.EqualTo(SessionState.Rejected));
            Assert.That(queue.Enqueue(Record()), Is.False);
            Assert.That(transport.Requests.Count, Is.EqualTo(2));
        }

        [Test]
        public void DiscardsSupersededPendingIdentity()
        {
            Guest(ApiAnswers.TimeoutFailure());
            queue.Enqueue(Record());
            session.ResetIdentity();
            transport.Enqueue(Opened("second"));
            session.EnterAsGuest(true);
            Run(queue.Drain());
            Assert.That(transport.Requests.Count, Is.EqualTo(2));
        }

        [TestCase(429)]
        [TestCase(503)]
        public void PendingOpeningRetriesTransientResponseAndSendsWithTheNewToken(int status)
        {
            Guest(ApiAnswers.TimeoutFailure());
            queue.Enqueue(Record());
            transport.Enqueue(HttpResult.Answered(status, ""));
            transport.Enqueue(Opened("recovered"));
            transport.Enqueue(ApiAnswers.NoContent());
            Run(queue.Drain());
            Assert.That(waits, Is.EqualTo(new[] { 2 }));
            Assert.That(transport.Requests.Last().BearerToken, Is.EqualTo("recovered"));
        }

        [Test]
        public void SecondExpirationDiscardsRetokenedGroupWithoutOpeningAgain()
        {
            Guest(Opened("first"));
            queue.Enqueue(Record());
            queue.Enqueue(Record());
            transport.Enqueue(ApiAnswers.Error(401, ApiErrorCodes.InvalidSession, "expired"));
            transport.Enqueue(Opened("second"));
            transport.Enqueue(ApiAnswers.Error(401, ApiErrorCodes.InvalidSession, "expired"));
            Run(queue.Drain());
            Assert.That(transport.Requests.Count, Is.EqualTo(4));
        }

        [Test]
        public void RefusalAndUndecidedNeverEnqueue()
        {
            Assert.That(queue.Enqueue(Record()), Is.False);
            session.Enable(api);
            Assert.That(queue.Enqueue(Record()), Is.False);
            session.EnterAsGuest(false);
            Assert.That(queue.Enqueue(Record()), Is.False);
            Run(queue.Drain());
            Assert.That(transport.Requests, Is.Empty);
        }

        [Test]
        public void InvalidRecordLogsDetailsWithoutRetry()
        {
            Guest(Opened("secret-token"));
            queue.Enqueue(Record());
            transport.Enqueue(HttpResult.Answered(400, "{\"error\":\"invalid_record\",\"message\":\"invalid\",\"details\":[\"columns.time length\"]}"));
            Run(queue.Drain());
            Assert.That(log.Warnings.Last(), Does.Contain("columns.time length").And.Not.Contains("secret-token"));
            Assert.That(transport.Requests.Count, Is.EqualTo(2));
        }
    }
}
