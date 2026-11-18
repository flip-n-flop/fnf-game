using UnityEngine;

namespace FlipNflop.Gla
{
    /// <summary>
    /// <see cref="IKeyValueStorage"/> over <see cref="PlayerPrefs"/>. On WebGL, <c>PlayerPrefs</c> is
    /// written to the browser IndexedDB by <see cref="PlayerPrefs.Save"/>; without IndexedDB (private
    /// mode of some browsers) the values last only until the page is reloaded. Used by
    /// <see cref="CredentialStore"/>, created by <see cref="GlaServices"/>.
    /// </summary>
    public sealed class PlayerPrefsStorage : IKeyValueStorage
    {
        #region IKeyValueStorage

        /// <summary>
        /// Whether the key exists, with <see cref="PlayerPrefs.HasKey"/>.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns><c>true</c> when the key exists.</returns>
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);

        /// <summary>
        /// Reads a text with <see cref="PlayerPrefs.GetString(string)"/>.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The text, or an empty text.</returns>
        public string GetString(string key) => PlayerPrefs.GetString(key);

        /// <summary>
        /// Stores a text with <see cref="PlayerPrefs.SetString"/>.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The text.</param>
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);

        /// <summary>
        /// Removes a key with <see cref="PlayerPrefs.DeleteKey"/>.
        /// </summary>
        /// <param name="key">The key.</param>
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);

        /// <summary>
        /// Writes the changes with <see cref="PlayerPrefs.Save"/>.
        /// </summary>
        public void Save() => PlayerPrefs.Save();

        #endregion
    }
}
