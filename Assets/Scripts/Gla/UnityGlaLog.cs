using UnityEngine;

namespace FlipNflop.Gla
{
    /// <summary>
    /// <see cref="IGlaLog"/> over <see cref="Debug"/>, with the <c>[GLA]</c> prefix. On WebGL the lines
    /// go to the browser console. Created by <see cref="GlaServices"/>.
    /// </summary>
    public sealed class UnityGlaLog : IGlaLog
    {
        #region Constants

        private const string Prefix = "[GLA] ";

        #endregion

        #region IGlaLog

        /// <summary>
        /// Writes an informative line with <see cref="Debug.Log(object)"/>.
        /// </summary>
        /// <param name="message">The line.</param>
        public void Info(string message)
        {
            Debug.Log(Prefix + message);
        }

        /// <summary>
        /// Writes a warning with <see cref="Debug.LogWarning(object)"/>.
        /// </summary>
        /// <param name="message">The line.</param>
        public void Warning(string message)
        {
            Debug.LogWarning(Prefix + message);
        }

        #endregion
    }
}
