using System;
using System.Collections;
using System.Collections.Generic;
using FlipNflop.Commons.Errors;
using FlipNflop.Commons.Records;
using FlipNflop.Gla.Serialization;

namespace FlipNflop.Gla
{
    /// <summary>Page-local FIFO that drives GlaApiClient and PlayerSession through a single coroutine.</summary>
    public sealed class AttemptDeliveryQueue : IDisposable
    {
        private const int MaxAttempts = 6;
        private readonly PlayerSession session;
        private readonly IGlaLog log;
        private readonly Func<int, object> wait;
        private readonly bool development;
        private readonly Queue<PendingRecord> records = new Queue<PendingRecord>();
        private PendingRecord active;

        /// <summary>Immutable payload and identity captured by RecordReady, with its replaceable session token.</summary>
        private sealed class PendingRecord
        {
            public readonly string Json;
            public readonly string Phase;
            public readonly string Start;
            public readonly int Owner;
            public string Token;
            public int Attempt;

            /// <summary>Serializes the record once before any asynchronous work.</summary>
            public PendingRecord(Phase record, PlayerSession session)
            {
                Json = GlaJson.Serialize(record);
                Phase = record.PhaseName;
                Start = GlaTime.Format(record.Sections[0].DateTimeStart);
                Owner = session.IdentityVersion;
                Token = session.SessionToken;
            }
        }

        /// <summary>Observes session transitions and uses the supplied pause-independent wait instruction.</summary>
        public AttemptDeliveryQueue(PlayerSession session, IGlaLog log, Func<int, object> wait, bool development)
        {
            this.session = session;
            this.log = log;
            this.wait = wait;
            this.development = development;
            session.Changed += SessionChanged;
        }

        public bool HasPending => records.Count > 0;

        /// <summary>Releases the subscription and all pending payloads when the page ends.</summary>
        public void Dispose()
        {
            session.Changed -= SessionChanged;
            records.Clear();
        }

        /// <summary>Accepts records only for a known identity with an open or pending session.</summary>
        public bool Enqueue(Phase record)
        {
            if (session.IsCollectionBlocked) return false;
            if (session.State != SessionState.Open && session.State != SessionState.Pending)
            {
                log.Info($"Discarded attempt without an open or pending session (mode {session.Mode}).");
                return false;
            }
            records.Enqueue(new PendingRecord(record, session));
            return true;
        }

        /// <summary>Binds all waiting records as soon as their own session opens, including during a scene change.</summary>
        private void SessionChanged()
        {
            if (session.IsCollectionBlocked)
            {
                records.Clear();
                return;
            }
            if (session.State != SessionState.Open) return;
            ReplaceToken(session.IdentityVersion, null, session.SessionToken);
        }

        /// <summary>Retokens the affected group, including the record currently being delivered.</summary>
        private void ReplaceToken(int owner, string oldToken, string newToken)
        {
            if (active != null && active.Owner == owner && active.Token == oldToken) active.Token = newToken;
            foreach (var item in records)
                if (item.Owner == owner && item.Token == oldToken) item.Token = newToken;
        }

        /// <summary>Removes the waiting records of an identity whose session can no longer be used.</summary>
        private void DiscardGroup(int owner, string token)
        {
            int count = records.Count;
            for (int i = 0; i < count; i++)
            {
                var item = records.Dequeue();
                if (item.Owner != owner || item.Token != token) records.Enqueue(item);
            }
            log.Warning("Discarded attempts whose session could not be opened or renewed.");
        }

        /// <summary>Drains in order. The persistent AttemptDelivery starts only one instance.</summary>
        public IEnumerator Drain()
        {
            while (records.Count != 0 && !session.IsCollectionBlocked)
            {
                active = records.Dequeue();
                yield return Send(active);
                active = null;
            }
        }

        /// <summary>Posts the original payload with bounded backoff and one replay after session renewal.</summary>
        private IEnumerator Send(PendingRecord record)
        {
            if (record.Token == null) yield return Open(record);
            bool replay = false;
            while (record.Token != null && !session.IsCollectionBlocked)
            {
                string sentToken = record.Token;
                record.Attempt++;
                var sending = session.Api.PostJsonAsync("/attempts", record.Json, sentToken);
                yield return sending;
                var answer = sending.Result;
                if (development)
                    log.Info($"send phase={record.Phase} start={record.Start} try={record.Attempt} -> {answer}");
                if (answer.StatusCode == 204 || session.IsCollectionBlocked) yield break;
                if (answer.StatusCode == 401 && answer.ErrorCode == ApiErrorCodes.InvalidSession)
                {
                    if (replay || record.Owner != session.IdentityVersion)
                    {
                        DiscardGroup(record.Owner, sentToken);
                        yield break;
                    }
                    replay = true;
                    ReplaceToken(record.Owner, sentToken, null);
                    yield return Open(record);
                    continue;
                }
                if (!replay && answer.IsTransientFailure && record.Attempt < MaxAttempts)
                {
                    yield return wait(1 << record.Attempt);
                    continue;
                }
                string details = answer.StatusCode == 400 && answer.Error?.Details != null
                    ? string.Join(", ", answer.Error.Details) : string.Empty;
                log.Warning($"Discarded attempt after {record.Attempt} send(s): {answer}. {details}");
                yield break;
            }
        }

        /// <summary>Opens or renews only the owning identity's session. PlayerSession handles expired credentials and 400.</summary>
        private IEnumerator Open(PendingRecord record)
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                if (session.IsCollectionBlocked || session.IdentityVersion != record.Owner) break;
                if (record.Token != null) yield break;
                var opening = session.OpenSessionAsync();
                yield return opening;
                if (record.Token != null) yield break;
                if (opening.Result.Outcome != SessionOpenOutcome.TransientFailure ||
                    !opening.Result.Response.IsTransientFailure || attempt == MaxAttempts) break;
                yield return wait(1 << attempt);
            }
            DiscardGroup(record.Owner, null);
        }
    }
}
