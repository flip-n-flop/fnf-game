namespace FlipNflop.Gla
{
    /// <summary>
    /// Persistent text storage of the browser, kept apart from <c>UnityEngine.PlayerPrefs</c> so that
    /// <see cref="CredentialStore"/> runs without the engine. Implemented in the game by
    /// <c>PlayerPrefsStorage</c> (on WebGL, <c>PlayerPrefs</c> lives in the IndexedDB).
    /// </summary>
    public interface IKeyValueStorage
    {
        /// <summary>
        /// Whether a key is stored.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns><c>true</c> when the key exists.</returns>
        bool HasKey(string key);

        /// <summary>
        /// Reads a stored text.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The text, or an empty text when the key does not exist.</returns>
        string GetString(string key);

        /// <summary>
        /// Stores a text, kept only in memory until <see cref="Save"/>.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The text.</param>
        void SetString(string key, string value);

        /// <summary>
        /// Removes a key, if it exists.
        /// </summary>
        /// <param name="key">The key.</param>
        void DeleteKey(string key);

        /// <summary>
        /// Writes the pending changes to the persistent storage (<c>PlayerPrefs.Save</c>).
        /// </summary>
        void Save();
    }
}
