using System;
using NUnit.Framework;

namespace FlipNflop.Gla.Tests
{
    /// <summary>
    /// EditMode tests of <see cref="GlaConfig"/> (the reading of <c>gla-config.json</c>, gla-13 D1)
    /// and of <see cref="GlaTime"/> (the instant of the page load sent as <c>started_at</c>).
    /// </summary>
    public class GlaConfigTests
    {
        #region GlaConfig

        /// <summary>
        /// The file of the build turns the collection on with the API address.
        /// </summary>
        [Test]
        public void ReadsTheApiAddressAndTheSwitch()
        {
            bool parsed = GlaConfig.TryParse("{ \"api_base_url\": \"https://gla.example.org/api/\", \"collection_enabled\": true }", out GlaConfig config, out string problem);

            Assert.That(parsed, Is.True, problem);
            Assert.That(config.CollectionEnabled, Is.True);
            Assert.That(config.ApiBaseUrl, Is.EqualTo("https://gla.example.org/api"));
        }

        /// <summary>
        /// Scenario "Build sem coleta": <c>collection_enabled = false</c> is valid, whatever the address.
        /// </summary>
        /// <param name="json">The file text.</param>
        [TestCase("{ \"api_base_url\": \"http://localhost:5000\", \"collection_enabled\": false }")]
        [TestCase("{ \"collection_enabled\": false }")]
        [TestCase("{ \"api_base_url\": \"\", \"collection_enabled\": false }")]
        public void CollectionOffIsValidWithoutAddress(string json)
        {
            bool parsed = GlaConfig.TryParse(json, out GlaConfig config, out string problem);

            Assert.That(parsed, Is.True, problem);
            Assert.That(config.CollectionEnabled, Is.False);
            Assert.That(config.ApiBaseUrl, Is.Null);
        }

        /// <summary>
        /// Unknown keys do not invalidate the file.
        /// </summary>
        [Test]
        public void IgnoresUnknownKeys()
        {
            bool parsed = GlaConfig.TryParse("{ \"api_base_url\": \"http://localhost:5000\", \"collection_enabled\": true, \"note\": 1 }", out GlaConfig config, out _);

            Assert.That(parsed, Is.True);
            Assert.That(config.ApiBaseUrl, Is.EqualTo("http://localhost:5000"));
        }

        /// <summary>
        /// An invalid file is refused with a reason, which turns the collection off in <c>GlaServices</c>.
        /// </summary>
        /// <param name="json">The file text.</param>
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("{ \"api_base_url\": \"http://localhost:5000\" }")]
        [TestCase("{ \"api_base_url\": \"http://localhost:5000\", \"collection_enabled\": \"true\" }")]
        [TestCase("{ \"api_base_url\": \"http://localhost:5000\", \"collection_enabled\": 1 }")]
        [TestCase("{ \"collection_enabled\": true }")]
        [TestCase("{ \"api_base_url\": null, \"collection_enabled\": true }")]
        [TestCase("{ \"api_base_url\": \"localhost:5000\", \"collection_enabled\": true }")]
        [TestCase("{ \"api_base_url\": \"ftp://example.org\", \"collection_enabled\": true }")]
        [TestCase("{ \"api_base_url\": \"/api\", \"collection_enabled\": true }")]
        [TestCase("{ \"api_base_url\": \"https://example.org/?x=1\", \"collection_enabled\": true }")]
        [TestCase("{ \"api_base_url\": \"https://example.org/#x\", \"collection_enabled\": true }")]
        [TestCase("{ \"api_base_url\": \"http://localhost:5000\", \"collection_enabled\": true } trailing")]
        public void RefusesAnInvalidFile(string json)
        {
            bool parsed = GlaConfig.TryParse(json, out GlaConfig config, out string problem);

            Assert.That(parsed, Is.False);
            Assert.That(config, Is.Null);
            Assert.That(problem, Is.Not.Null.And.Not.Empty);
        }

        /// <summary>
        /// The configuration file committed in <c>StreamingAssets</c> is read as valid.
        /// </summary>
        [Test]
        public void TheCommittedFileFormatIsValid()
        {
            const string committed = "{\n  \"api_base_url\": \"http://localhost:5000\",\n  \"collection_enabled\": true\n}\n";

            Assert.That(GlaConfig.TryParse(committed, out GlaConfig config, out string problem), Is.True, problem);
            Assert.That(config.ToString(), Is.EqualTo("collection_enabled=true, api_base_url=http://localhost:5000"));
        }

        #endregion

        #region GlaTime

        /// <summary>
        /// Scenario "Instante da sessão": the page loaded at 14:32:05 in Brasília is 17:32:05.000Z.
        /// </summary>
        [Test]
        public void PageLoadInstantIsWrittenInUtc()
        {
            var loadedInBrasilia = new DateTimeOffset(2026, 10, 20, 14, 32, 5, TimeSpan.FromHours(-3));

            DateTimeOffset pageLoadedAt = GlaTime.ToContractPrecision(loadedInBrasilia);

            Assert.That(pageLoadedAt.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(GlaTime.Format(pageLoadedAt), Is.EqualTo("2026-10-20T17:32:05.000Z"));
        }

        /// <summary>
        /// The instant kept in memory drops what the JSON cannot carry (below the millisecond).
        /// </summary>
        [Test]
        public void PageLoadInstantIsTruncatedToTheMillisecond()
        {
            DateTimeOffset precise = new DateTimeOffset(2026, 10, 20, 17, 32, 5, 123, TimeSpan.Zero).AddTicks(4567);

            DateTimeOffset pageLoadedAt = GlaTime.ToContractPrecision(precise);

            Assert.That(pageLoadedAt, Is.EqualTo(new DateTimeOffset(2026, 10, 20, 17, 32, 5, 123, TimeSpan.Zero)));
            Assert.That(GlaTime.Format(pageLoadedAt), Is.EqualTo("2026-10-20T17:32:05.123Z"));
        }

        /// <summary>
        /// The stored validity is read with or without offset (without it, as UTC), and garbage is refused.
        /// </summary>
        [Test]
        public void ParsesStoredInstants()
        {
            Assert.That(GlaTime.TryParse("2026-10-20T21:14:58.000Z", out DateTimeOffset utc), Is.True);
            Assert.That(utc, Is.EqualTo(new DateTimeOffset(2026, 10, 20, 21, 14, 58, TimeSpan.Zero)));
            Assert.That(GlaTime.TryParse("2026-10-20T18:14:58-03:00", out DateTimeOffset withOffset), Is.True);
            Assert.That(withOffset, Is.EqualTo(utc));
            Assert.That(GlaTime.TryParse("2026-10-20T21:14:58", out DateTimeOffset withoutOffset), Is.True);
            Assert.That(withoutOffset, Is.EqualTo(utc));
            Assert.That(GlaTime.TryParse("amanhã", out _), Is.False);
            Assert.That(GlaTime.TryParse(null, out _), Is.False);
        }

        #endregion
    }
}
