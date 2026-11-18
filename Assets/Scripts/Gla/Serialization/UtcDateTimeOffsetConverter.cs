using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace FlipNflop.Gla.Serialization
{
    /// <summary>
    /// Newtonsoft.Json converter of the contract dates (<c>dateTimeStart</c>, <c>dateTimeFinish</c>,
    /// <c>expires_at</c>, <c>started_at</c>): writes in UTC as <c>yyyy-MM-ddTHH:mm:ss.fffZ</c>, and
    /// reads ISO 8601 text with an explicit offset (<c>Z</c> or <c>±hh:mm</c>), with or without a
    /// fraction of second, refusing a date without offset. Registered by <see cref="GlaJson.CreateSettings"/>,
    /// which turns off the date parsing of the reader so that the text reaches this converter unchanged.
    /// </summary>
    internal sealed class UtcDateTimeOffsetConverter : JsonConverter
    {
        #region Constants

        private const string WriteFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'";

        private static readonly Regex DateWithOffset = new Regex(
            "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]{1,7})?(Z|[+-][0-9]{2}:[0-9]{2})$",
            RegexOptions.CultureInvariant);

        #endregion

        #region JsonConverter

        /// <summary>
        /// Whether the converter handles the type: <see cref="DateTimeOffset"/> or <c>DateTimeOffset?</c>.
        /// </summary>
        /// <param name="objectType">The type to convert.</param>
        /// <returns><c>true</c> for the date types.</returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTimeOffset) || objectType == typeof(DateTimeOffset?);
        }

        /// <summary>
        /// Writes the instant in UTC, with milliseconds and <c>Z</c>.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="value">The date, or <c>null</c>.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var date = (DateTimeOffset)value;
            writer.WriteValue(date.UtcDateTime.ToString(WriteFormat, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Reads a date with an explicit offset, keeping the offset of the text.
        /// </summary>
        /// <param name="reader">The JSON reader, on the value.</param>
        /// <param name="objectType">The target type.</param>
        /// <param name="existingValue">Ignored.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>The date, or <c>null</c> for a JSON <c>null</c> into <c>DateTimeOffset?</c>.</returns>
        /// <exception cref="JsonSerializationException">The value is not an ISO 8601 text with offset; the message cites the path.</exception>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null && objectType == typeof(DateTimeOffset?)) return null;

            if (reader.TokenType == JsonToken.String)
            {
                var text = (string)reader.Value;
                if (DateWithOffset.IsMatch(text) &&
                    DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset date))
                {
                    return date;
                }

                throw new JsonSerializationException(
                    $"Invalid date '{text}': expected ISO 8601 with an explicit offset (Z or ±hh:mm). Path '{reader.Path}'.");
            }

            throw new JsonSerializationException(
                $"Invalid date: expected an ISO 8601 string, got {reader.TokenType}. Path '{reader.Path}'.");
        }

        #endregion
    }
}
