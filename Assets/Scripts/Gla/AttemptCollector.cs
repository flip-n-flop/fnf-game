using System;
using System.Collections.Generic;
using FlipNflop.Commons.Building;
using FlipNflop.Commons.Records;
using FlipNflop.Gla.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlipNflop.Gla
{
    /// <summary>Collects gameplay events into a commons record and publishes it before scene changes.</summary>
    public sealed class AttemptCollector : MonoBehaviour
    {
        /// <summary>Raised synchronously for the completed record, including abandonment before unload.</summary>
        public event Action<Phase> RecordReady;

        private readonly HashSet<string> reported = new HashSet<string>();
        private AttemptRecordBuilder builder;
        private Action<int, double> addTime;
        private EndCause? cause;
        private DateTimeOffset finishedAt;
        private Scene scene;
        private PlayerController player;
        private float origin;
        private float previousX;
        private int countedFrame;

        /// <summary>Listens for validation from PathVerifier and scene teardown.</summary>
        private void OnEnable()
        {
            PathVerifier.ValidationCompleted += CompleteWithValidation;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        /// <summary>Releases scene and validation subscriptions.</summary>
        private void OnDisable()
        {
            PathVerifier.ValidationCompleted -= CompleteWithValidation;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        /// <summary>Starts a catalog attempt after LevelJsonLoader has resolved the source and diagram.</summary>
        public void BeginAttempt(string name, int columns, bool fromResources, float startX, Scene levelScene)
        {
            try
            {
                FinishPending();
                if (!fromResources || Array.IndexOf(LevelSequenceManager.Levels, name) < 0 ||
                    GlaServices.Instance.Session.IsCollectionBlocked) return;
                builder = new AttemptRecordBuilder(name, columns, Application.version, DateTimeOffset.UtcNow);
                addTime = builder.AddTime;
                origin = startX;
                previousX = startX;
                countedFrame = -1;
                scene = levelScene;
            }
            catch (Exception error) { Fail(nameof(BeginAttempt), error); }
        }

        /// <summary>Accumulates scaled frame time after the player is available.</summary>
        private void Update()
        {
            try
            {
                if (builder == null || cause.HasValue) return;
                if (GlaServices.Instance.Session.IsCollectionBlocked) { Clear(); return; }
                if (player == null) player = FindAnyObjectByType<PlayerController>();
                if (player != null) Sample(player.transform.position.x);
            }
            catch (Exception error) { Fail(nameof(Update), error); }
        }

        /// <summary>Reconciles the position with the last sample, counting each frame's duration at most once.</summary>
        private void Sample(float x)
        {
            double seconds = countedFrame == Time.frameCount ? 0 : Time.deltaTime;
            ColumnTiming.Distribute(previousX - origin, x - origin, seconds, builder.ColumnCount, addTime);
            previousX = x;
            countedFrame = Time.frameCount;
        }

        /// <summary>Counts an OperationHint request at the player position.</summary>
        public void RecordHint(float x)
        {
            try
            {
                if (builder != null && !cause.HasValue)
                    builder.AddHint(ColumnTiming.Column(x - origin, builder.ColumnCount));
            }
            catch (Exception error) { Fail(nameof(RecordHint), error); }
        }

        /// <summary>Closes timing at the player position before PathVerifier evaluates arrival or death.</summary>
        public void NotifyEnd(EndCause endCause, float x)
        {
            try
            {
                if (builder == null || cause.HasValue) return;
                Sample(x);
                cause = endCause;
                finishedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception error) { Fail(nameof(NotifyEnd), error); }
        }

        /// <summary>Completes an announced end with the validation result emitted by PathVerifier.</summary>
        public void CompleteWithValidation(TraceValidationResult result)
        {
            try
            {
                if (builder == null || !cause.HasValue) return;
                Publish(result.Trace, new ValidationResult(result.Conclusion, result.Performance, result.OverallAccuracy, result.Correct));
            }
            catch (Exception error) { Fail(nameof(CompleteWithValidation), error); }
        }

        /// <summary>Snapshots the live SignalPath and publishes an abandonment before the menu changes scene.</summary>
        public void Abandon(EndCause endCause)
        {
            try
            {
                if (builder == null || cause.HasValue) return;
                if (player == null) player = FindAnyObjectByType<PlayerController>();
                float x = player != null ? player.transform.position.x : previousX;
                Sample(x);
                cause = endCause;
                finishedAt = DateTimeOffset.UtcNow;
                SignalPath path = player != null ? player.GetComponent<SignalPath>() : null;
                int?[] trace = path != null ? path.TraceAt(x, origin, builder.ColumnCount) : new int?[builder.ColumnCount];
                Publish(trace, null);
            }
            catch (Exception error) { Fail(nameof(Abandon), error); }
        }

        /// <summary>Finalizes a notified end if its scene unloads before validation arrives.</summary>
        private void OnSceneUnloaded(Scene unloaded)
        {
            try { if (unloaded == scene) FinishPending(); }
            catch (Exception error) { Fail(nameof(OnSceneUnloaded), error); }
        }

        /// <summary>Supplies the shared short-trace result when a notified end has no validation.</summary>
        private void FinishPending()
        {
            if (builder != null && cause.HasValue)
            {
                Debug.LogWarning("[GLA] Missing path validation. Recording a short trace.");
                CompleteWithValidation(TraceValidationResult.ForShortTrace(builder.ColumnCount));
            }
            Clear();
        }

        /// <summary>Publishes the first completed record and logs its contract JSON in development builds.</summary>
        private void Publish(int?[] trace, ValidationResult validation)
        {
            if (GlaServices.Instance.Session.IsCollectionBlocked) { Clear(); return; }
            bool ready = builder.TryFinish(cause.Value, finishedAt, trace, validation, out Phase record);
            Clear();
            if (!ready) return;
            if (Debug.isDebugBuild) Debug.Log(GlaJson.Serialize(record, true));
            RecordReady?.Invoke(record);
        }

        /// <summary>Drops references to the current scene and builder.</summary>
        private void Clear()
        {
            builder = null;
            addTime = null;
            player = null;
            cause = null;
        }

        /// <summary>Discards a failed attempt and reports each failing hook only once.</summary>
        private void Fail(string source, Exception error)
        {
            Clear();
            if (reported.Add(source)) Debug.LogWarning($"[GLA] {source}: {error.GetType().Name} — {error.Message}");
        }
    }
}
