namespace FlipNflop.Gla
{
    /// <summary>
    /// Log of the GLA client, kept apart from <c>UnityEngine.Debug</c> so that the session and
    /// identification logic runs without the engine. Implemented in the game by <c>UnityGlaLog</c>.
    /// The messages never carry the e-mail, the code, the credential or the session token.
    /// </summary>
    public interface IGlaLog
    {
        /// <summary>
        /// Writes an informative line, such as the configuration read or a session opened.
        /// </summary>
        /// <param name="message">The line, in English.</param>
        void Info(string message);

        /// <summary>
        /// Writes a warning, such as a refused session opening or an unreadable answer.
        /// </summary>
        /// <param name="message">The line, in English.</param>
        void Warning(string message);
    }
}
