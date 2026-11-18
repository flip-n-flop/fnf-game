using System.Collections;
using FlipNflop.Commons.Records;
using UnityEngine;

namespace FlipNflop.Gla
{
    /// <summary>Connects AttemptCollector to the page-local delivery queue on a persistent Unity object.</summary>
    public sealed class AttemptDelivery : MonoBehaviour
    {
        private GlaServices services;
        private AttemptDeliveryQueue queue;
        private bool running;
        private bool subscribed;

        /// <summary>Installs the queue before the first scene starts, while configuration is loading.</summary>
        public void Initialize(GlaServices owner)
        {
            services = owner;
            queue = new AttemptDeliveryQueue(owner.Session, owner.Log, seconds => new WaitForSecondsRealtime(seconds), Debug.isDebugBuild);
            owner.Session.Changed += UpdateSubscription;
            UpdateSubscription();
        }

        /// <summary>Subscribes to RecordReady only while collection is permitted.</summary>
        private void UpdateSubscription()
        {
            bool allowed = !services.Session.IsCollectionBlocked;
            if (allowed == subscribed) return;
            if (allowed) services.Collector.RecordReady += OnRecord;
            else services.Collector.RecordReady -= OnRecord;
            subscribed = allowed;
        }

        /// <summary>Captures the current session synchronously and schedules network work after gameplay returns.</summary>
        private void OnRecord(Phase record)
        {
            if (!queue.Enqueue(record) || running) return;
            running = true;
            StartCoroutine(Deliver());
        }

        /// <summary>Runs the single FIFO worker across gameplay pauses and scene unloads.</summary>
        private IEnumerator Deliver()
        {
            yield return null;
            try
            {
                while (queue.HasPending) yield return queue.Drain();
            }
            finally { running = false; }
        }

        /// <summary>Detaches the listeners when the persistent services are destroyed.</summary>
        private void OnDestroy()
        {
            if (queue == null) return;
            services.Session.Changed -= UpdateSubscription;
            if (subscribed) services.Collector.RecordReady -= OnRecord;
            queue.Dispose();
        }
    }
}
