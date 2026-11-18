using System;
using FlipNflop.Commons.Contract;
using FlipNflop.Commons.Records;
using Newtonsoft.Json;

namespace FlipNflop.Gla.Serialization
{
    /// <summary>
    /// Newtonsoft.Json converter of <see cref="EndCause"/> (<c>end_cause</c>): writes and reads
    /// one of the five lowercase texts of <see cref="EndCauseText"/>, case-sensitive, and refuses
    /// numbers and any other text. Registered by <see cref="GlaJson.CreateSettings"/>.
    /// </summary>
    internal sealed class EndCauseConverter : JsonConverter
    {
        #region JsonConverter

        /// <summary>
        /// Whether the converter handles the type: <see cref="EndCause"/> or <c>EndCause?</c>.
        /// </summary>
        /// <param name="objectType">The type to convert.</param>
        /// <returns><c>true</c> for the end cause types.</returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(EndCause) || objectType == typeof(EndCause?);
        }

        /// <summary>
        /// Writes the cause as its text from <see cref="EndCauseText.ToText"/>.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="value">The cause, or <c>null</c>.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <exception cref="JsonSerializationException">The value is not one of the five causes.</exception>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var cause = (EndCause)value;
            if (!cause.IsDefined())
                throw new JsonSerializationException($"Cannot write end cause {(int)cause}: it is not one of {string.Join(", ", EndCauseText.All)}. Path '{writer.Path}'.");

            writer.WriteValue(EndCauseText.ToText(cause));
        }

        /// <summary>
        /// Reads the cause with <see cref="EndCauseText.TryParse"/>.
        /// </summary>
        /// <param name="reader">The JSON reader, on the value.</param>
        /// <param name="objectType">The target type.</param>
        /// <param name="existingValue">Ignored.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>The cause, or <c>null</c> for a JSON <c>null</c> into <c>EndCause?</c>.</returns>
        /// <exception cref="JsonSerializationException">The value is not one of the five texts; the message cites the path.</exception>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null && objectType == typeof(EndCause?)) return null;

            if (reader.TokenType == JsonToken.String && EndCauseText.TryParse((string)reader.Value, out EndCause cause))
                return cause;

            string found = reader.TokenType == JsonToken.String ? $"'{reader.Value}'" : reader.TokenType.ToString();
            throw new JsonSerializationException(
                $"Invalid end cause {found}: expected one of {string.Join(", ", EndCauseText.All)}, in lowercase. Path '{reader.Path}'.");
        }

        #endregion
    }
}
