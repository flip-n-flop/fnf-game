using System;
using System.Globalization;

namespace FlipNflop.Gla
{
    /// <summary>
    /// Instant helpers of the GLA client: the precision and the text of the instants that the game
    /// sends (<c>started_at</c>) and keeps (<c>gla.credential_expires_at</c>). The text is the same
    /// UTC format written by <c>GlaJson</c> (<c>yyyy-MM-ddTHH:mm:ss.fffZ</c>).
    /// Used by <see cref="PlayerSession"/>, <see cref="CredentialStore"/> and the <c>GlaServices</c> bootstrap.
    /// </summary>
    public static class GlaTime
    {
        #region Constants

        private const string TextFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'";

        #endregion

        #region Public API

        /// <summary>
        /// Converts an instant to UTC and drops what is below the millisecond, the precision of the
        /// contract dates, so that the instant kept in memory is equal to the one written in JSON.
        /// </summary>
        /// <param name="instant">Any instant.</param>
        /// <returns>The same instant in UTC, truncated to the millisecond.</returns>
        public static DateTimeOffset ToContractPrecision(DateTimeOffset instant)
        {
            long ticks = instant.UtcTicks;
            return new DateTimeOffset(ticks - ticks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);
        }

        /// <summary>
        /// Writes an instant in UTC as <c>yyyy-MM-ddTHH:mm:ss.fffZ</c>, for the browser storage and the log.
        /// </summary>
        /// <param name="instant">The instant.</param>
        /// <returns>The text of the instant.</returns>
        public static string Format(DateTimeOffset instant)
        {
            return instant.UtcDateTime.ToString(TextFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads an ISO 8601 instant; a text without offset is read as UTC.
        /// </summary>
        /// <param name="text">The text, possibly <c>null</c>.</param>
        /// <param name="instant">The instant read, or the default value.</param>
        /// <returns><c>true</c> when the text is an instant.</returns>
        public static bool TryParse(string text, out DateTimeOffset instant)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                instant = default;
                return false;
            }

            return DateTimeOffset.TryParse(
                text.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out instant);
        }

        #endregion
    }
}
