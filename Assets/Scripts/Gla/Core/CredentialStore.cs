using System;

namespace FlipNflop.Gla
{
    /// <summary>
    /// What <see cref="CredentialStore.Load"/> found in the browser storage.
    /// </summary>
    public enum StoredCredentialStatus
    {
        /// <summary>No credential is stored.</summary>
        Absent,

        /// <summary>A credential whose validity has not passed.</summary>
        Valid,

        /// <summary>A credential whose validity passed; it cannot be reused.</summary>
        Expired,

        /// <summary>A credential without a readable validity, or a validity without a credential; it cannot be reused.</summary>
        Unreadable,
    }

    /// <summary>
    /// Keeps the identification credential in the browser storage (A2, design D3): only the keys
    /// <see cref="CredentialKey"/> and <see cref="ExpiresAtKey"/> (ISO 8601 in UTC), followed by
    /// <see cref="IKeyValueStorage.Save"/>. No key holds the e-mail or the session token.
    /// Used by <see cref="PlayerSession"/>.
    /// </summary>
    public sealed class CredentialStore
    {
        #region Constants

        public const string CredentialKey = "gla.credential";
        public const string ExpiresAtKey = "gla.credential_expires_at";

        #endregion

        #region Fields

        private readonly IKeyValueStorage storage;

        #endregion

        #region Construction

        /// <summary>
        /// Creates the store over a storage.
        /// </summary>
        /// <param name="storage">The browser storage.</param>
        /// <exception cref="ArgumentNullException"><paramref name="storage"/> is <c>null</c>.</exception>
        public CredentialStore(IKeyValueStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        #endregion

        #region Public API

        /// <summary>
        /// Stores the credential and its validity through <see cref="IKeyValueStorage"/>, removing the old
        /// validity first so that an interrupted update cannot pair a new credential with a stale expiry.
        /// </summary>
        /// <param name="credential">The opaque credential of the code verification.</param>
        /// <param name="expiresAt">When the API stops accepting it.</param>
        /// <exception cref="ArgumentException"><paramref name="credential"/> is empty.</exception>
        public void Save(string credential, DateTimeOffset expiresAt)
        {
            if (string.IsNullOrEmpty(credential))
                throw new ArgumentException("The credential must not be empty.", nameof(credential));

            storage.DeleteKey(ExpiresAtKey);
            storage.SetString(CredentialKey, credential);
            storage.SetString(ExpiresAtKey, GlaTime.Format(expiresAt));
            storage.Save();
        }

        /// <summary>
        /// Reads the stored credential. An expired or unreadable one is discarded and cleanup is attempted.
        /// The credential expires at the instant of its validity.
        /// </summary>
        /// <param name="now">The current instant.</param>
        /// <param name="credential">The valid credential, or <c>null</c>.</param>
        /// <param name="expiresAt">Its validity, or the stored one when expired, or the default value.</param>
        /// <returns>What was found.</returns>
        public StoredCredentialStatus Load(DateTimeOffset now, out string credential, out DateTimeOffset expiresAt)
        {
            credential = null;
            expiresAt = default;

            bool hasCredential = storage.HasKey(CredentialKey);
            bool hasExpiry = storage.HasKey(ExpiresAtKey);
            if (!hasCredential && !hasExpiry) return StoredCredentialStatus.Absent;

            string storedCredential = hasCredential ? storage.GetString(CredentialKey) : null;
            string storedExpiry = hasExpiry ? storage.GetString(ExpiresAtKey) : null;
            if (string.IsNullOrEmpty(storedCredential) || !GlaTime.TryParse(storedExpiry, out DateTimeOffset validity))
            {
                TryClear();
                return StoredCredentialStatus.Unreadable;
            }

            expiresAt = validity;
            if (now >= validity)
            {
                TryClear();
                return StoredCredentialStatus.Expired;
            }

            credential = storedCredential;
            return StoredCredentialStatus.Valid;
        }

        /// <summary>
        /// Removes the two keys and saves the storage ("Trocar e-mail", <c>invalid_credential</c>, expired credential).
        /// </summary>
        public void Clear()
        {
            storage.DeleteKey(CredentialKey);
            storage.DeleteKey(ExpiresAtKey);
            storage.Save();
        }

        /// <summary>Attempts to clear the credential through <see cref="IKeyValueStorage"/> even when persistence is unavailable.</summary>
        internal bool TryClear()
        {
            try
            {
                Clear();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion
    }
}
