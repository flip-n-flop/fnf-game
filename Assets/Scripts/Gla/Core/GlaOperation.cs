using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace FlipNflop.Gla
{
    /// <summary>
    /// An asynchronous operation of the GLA client (an API call or a session opening), without
    /// threads, as WebGL requires. The caller either registers a callback with
    /// <see cref="OnCompleted"/> or, in a Unity coroutine, waits for it with <c>yield return operation</c>
    /// (it is an <see cref="IEnumerator"/> that runs until <see cref="IsDone"/>).
    /// Completed by <see cref="GlaApiClient"/> and <see cref="PlayerSession"/>.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    public sealed class GlaOperation<T> : IEnumerator
    {
        #region Fields

        private List<Action<T>> callbacks = new List<Action<T>>();

        #endregion

        #region Properties

        public bool IsDone { get; private set; }

        /// <summary>
        /// The result; the default value of <typeparamref name="T"/> until <see cref="IsDone"/>.
        /// </summary>
        public T Result { get; private set; }

        #endregion

        #region Public API

        /// <summary>
        /// Registers a callback for the result. If the operation is already done, the callback runs at once.
        /// </summary>
        /// <param name="callback">Receives the result.</param>
        /// <returns>This operation, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <c>null</c>.</exception>
        public GlaOperation<T> OnCompleted(Action<T> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            if (IsDone)
                callback(Result);
            else
                callbacks.Add(callback);

            return this;
        }

        #endregion

        #region Completion

        /// <summary>
        /// Creates an operation that is already done, for calls answered without a request.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>The done operation.</returns>
        internal static GlaOperation<T> FromResult(T result)
        {
            var operation = new GlaOperation<T>();
            operation.Complete(result);
            return operation;
        }

        /// <summary>
        /// Stores the result and runs every registered callback. All callbacks run even if one throws;
        /// the first exception is thrown again afterwards.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <exception cref="InvalidOperationException">The operation was already completed.</exception>
        internal void Complete(T result)
        {
            if (IsDone) throw new InvalidOperationException("The GLA operation was already completed.");

            IsDone = true;
            Result = result;
            List<Action<T>> pending = callbacks;
            callbacks = null;

            ExceptionDispatchInfo firstFailure = null;
            foreach (Action<T> callback in pending)
            {
                try
                {
                    callback(result);
                }
                catch (Exception exception)
                {
                    if (firstFailure == null) firstFailure = ExceptionDispatchInfo.Capture(exception);
                }
            }

            firstFailure?.Throw();
        }

        #endregion

        #region IEnumerator

        object IEnumerator.Current => null;

        /// <summary>
        /// Keeps a Unity coroutine waiting while the operation runs.
        /// </summary>
        /// <returns><c>true</c> while the operation is not done.</returns>
        bool IEnumerator.MoveNext() => !IsDone;

        /// <summary>
        /// Does nothing: an operation cannot be restarted.
        /// </summary>
        void IEnumerator.Reset()
        {
        }

        #endregion
    }
}
