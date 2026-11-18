using System;
using NUnit.Framework;

namespace FlipNflop.Gla.Tests
{
    /// <summary>
    /// EditMode tests of <see cref="CredentialStore"/> (gla-13 D3): only the credential and its
    /// validity are stored, the validity is checked when the game starts, and an expired or
    /// unreadable credential is removed. Interacts with <see cref="MemoryStorage"/>.
    /// </summary>
    public class CredentialStoreTests
    {
        #region Fields

        private static readonly DateTimeOffset VerifiedAt = new DateTimeOffset(2026, 10, 20, 17, 14, 58, TimeSpan.Zero);
        private static readonly DateTimeOffset ExpiresAt = VerifiedAt.AddHours(4);

        private MemoryStorage storage;
        private CredentialStore store;

        #endregion

        #region Setup

        /// <summary>
        /// Creates the store over an empty storage.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            storage = new MemoryStorage();
            store = new CredentialStore(storage);
        }

        #endregion

        #region Tests

        /// <summary>
        /// Scenario "Armazenamento depois da verificação": the storage has the credential and the
        /// validity, nothing else, and it was saved.
        /// </summary>
        [Test]
        public void SavesOnlyTheCredentialAndItsValidity()
        {
            store.Save("v1.credential", ExpiresAt);

            Assert.That(storage.Values.Keys, Is.EquivalentTo(new[] { "gla.credential", "gla.credential_expires_at" }));
            Assert.That(storage.Values["gla.credential"], Is.EqualTo("v1.credential"));
            Assert.That(storage.Values["gla.credential_expires_at"], Is.EqualTo("2026-10-20T21:14:58.000Z"));
            Assert.That(storage.SaveCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Scenario "Recarga 1 hora depois": the credential is still valid.
        /// </summary>
        [Test]
        public void CredentialIsValidOneHourLater()
        {
            store.Save("v1.credential", ExpiresAt);

            StoredCredentialStatus status = store.Load(VerifiedAt.AddHours(1), out string credential, out DateTimeOffset validity);

            Assert.That(status, Is.EqualTo(StoredCredentialStatus.Valid));
            Assert.That(credential, Is.EqualTo("v1.credential"));
            Assert.That(validity, Is.EqualTo(ExpiresAt));
            Assert.That(storage.Values.Count, Is.EqualTo(2));
        }

        /// <summary>
        /// Scenario "Recarga 5 horas depois", and the instant of the validity itself: the keys are removed.
        /// </summary>
        /// <param name="hoursAfterVerification">Hours after the verification.</param>
        [TestCase(5.0)]
        [TestCase(4.0)]
        public void ExpiredCredentialIsRemoved(double hoursAfterVerification)
        {
            store.Save("v1.credential", ExpiresAt);

            StoredCredentialStatus status = store.Load(VerifiedAt.AddHours(hoursAfterVerification), out string credential, out _);

            Assert.That(status, Is.EqualTo(StoredCredentialStatus.Expired));
            Assert.That(credential, Is.Null);
            Assert.That(storage.Values, Is.Empty);
            Assert.That(storage.SaveCount, Is.EqualTo(2));
        }

        /// <summary>
        /// Task 2.2: a validity edited to the past is expired.
        /// </summary>
        [Test]
        public void ValidityEditedToThePastIsExpired()
        {
            store.Save("v1.credential", ExpiresAt);
            storage.Values["gla.credential_expires_at"] = "2020-01-01T00:00:00Z";

            Assert.That(store.Load(VerifiedAt, out _, out _), Is.EqualTo(StoredCredentialStatus.Expired));
            Assert.That(storage.Values, Is.Empty);
        }

        /// <summary>
        /// A key alone, an empty credential or an unreadable validity are removed.
        /// </summary>
        /// <param name="credential">The stored credential, or <c>null</c> for no key.</param>
        /// <param name="validity">The stored validity, or <c>null</c> for no key.</param>
        [TestCase("v1.credential", null)]
        [TestCase(null, "2026-10-20T21:14:58.000Z")]
        [TestCase("", "2026-10-20T21:14:58.000Z")]
        [TestCase("v1.credential", "daqui a 4 horas")]
        public void UnreadableCredentialIsRemoved(string credential, string validity)
        {
            if (credential != null) storage.Values["gla.credential"] = credential;
            if (validity != null) storage.Values["gla.credential_expires_at"] = validity;

            Assert.That(store.Load(VerifiedAt, out string loaded, out _), Is.EqualTo(StoredCredentialStatus.Unreadable));
            Assert.That(loaded, Is.Null);
            Assert.That(storage.Values, Is.Empty);
        }

        /// <summary>
        /// Without keys, nothing is found and nothing is written.
        /// </summary>
        [Test]
        public void EmptyStorageHasNoCredential()
        {
            Assert.That(store.Load(VerifiedAt, out _, out _), Is.EqualTo(StoredCredentialStatus.Absent));
            Assert.That(storage.SaveCount, Is.EqualTo(0));
        }

        /// <summary>
        /// Clearing removes only the two keys of the GLA.
        /// </summary>
        [Test]
        public void ClearKeepsTheOtherPreferences()
        {
            storage.Values["SignalColor_J"] = "2";
            store.Save("v1.credential", ExpiresAt);

            store.Clear();

            Assert.That(storage.Values.Keys, Is.EquivalentTo(new[] { "SignalColor_J" }));
        }

        /// <summary>
        /// An empty credential is never stored.
        /// </summary>
        [Test]
        public void RefusesAnEmptyCredential()
        {
            Assert.Throws<ArgumentException>(() => store.Save(string.Empty, ExpiresAt));
            Assert.That(storage.Values, Is.Empty);
        }

        #endregion
    }
}
