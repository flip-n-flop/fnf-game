using System;
using System.Globalization;
using Newtonsoft.Json;

namespace FlipNflop.Gla.Serialization
{
    /// <summary>
    /// Read-only Newtonsoft.Json converter that keeps the JSON kind of the scalar values of the
    /// contract, which Newtonsoft would otherwise coerce: a string must be a JSON string (so
    /// <c>"code": 12345</c> is refused), a boolean a JSON boolean, an integer a JSON integer without
    /// decimal part, and a <c>float</c> a finite JSON number. A JSON <c>null</c> is accepted only
    /// where the C# type admits it; the presence rules stay with <see cref="GlaContractResolver"/>.
    /// Writing is left to Newtonsoft (<see cref="CanWrite"/> is <c>false</c>), so a <c>float</c> keeps
    /// its shortest round-trip text. Applied by <see cref="GlaContractResolver"/> to the contract
    /// properties and to the items of their arrays and lists.
    /// </summary>
    internal sealed class StrictValueConverter : JsonConverter
    {
        #region Singleton

        /// <summary>
        /// Shared instance; the converter has no state.
        /// </summary>
        public static readonly StrictValueConverter Instance = new StrictValueConverter();

        #endregion

        #region JsonConverter

        /// <summary>
        /// Writing uses the default behavior of Newtonsoft.Json.
        /// </summary>
        public override bool CanWrite => false;

        /// <summary>
        /// Whether the converter handles the type: <see cref="string"/>, <see cref="bool"/>,
        /// <see cref="int"/>, <see cref="float"/> and their nullable forms.
        /// </summary>
        /// <param name="objectType">The type to convert.</param>
        /// <returns><c>true</c> for the scalar types of the contract.</returns>
        public override bool CanConvert(Type objectType)
        {
            Type type = Nullable.GetUnderlyingType(objectType) ?? objectType;
            return type == typeof(string) || type == typeof(bool) || type == typeof(int) || type == typeof(float);
        }

        /// <summary>
        /// Not used: <see cref="CanWrite"/> is <c>false</c>.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="value">The value.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <exception cref="NotSupportedException">Always.</exception>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException("StrictValueConverter only reads.");
        }

        /// <summary>
        /// Reads the value, refusing a JSON kind that does not match the C# type.
        /// </summary>
        /// <param name="reader">The JSON reader, on the value.</param>
        /// <param name="objectType">The target type.</param>
        /// <param name="existingValue">Ignored.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>The value read, or <c>null</c>.</returns>
        /// <exception cref="JsonSerializationException">The JSON kind does not match the type; the message cites the path.</exception>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Type underlying = Nullable.GetUnderlyingType(objectType);
            Type type = underlying ?? objectType;

            if (reader.TokenType == JsonToken.Null)
            {
                if (underlying != null || !objectType.IsValueType) return null;
                throw Refuse(reader, "null", type);
            }

            if (type == typeof(string))
            {
                if (reader.TokenType == JsonToken.String) return (string)reader.Value;
            }
            else if (type == typeof(bool))
            {
                if (reader.TokenType == JsonToken.Boolean) return (bool)reader.Value;
            }
            else if (type == typeof(int))
            {
                if (reader.TokenType == JsonToken.Integer && reader.Value is long whole && whole >= int.MinValue && whole <= int.MaxValue)
                    return (int)whole;
            }
            else if (type == typeof(float))
            {
                if (reader.TokenType == JsonToken.Integer || reader.TokenType == JsonToken.Float)
                {
                    float number = (float)Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
                    if (!float.IsNaN(number) && !float.IsInfinity(number)) return number;
                }
            }

            string found = reader.TokenType == JsonToken.String
                ? "string"
                : Convert.ToString(reader.Value, CultureInfo.InvariantCulture) ?? reader.TokenType.ToString();
            throw Refuse(reader, found, type);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Builds the format error for a value of the wrong kind.
        /// </summary>
        /// <param name="reader">The JSON reader, for the path.</param>
        /// <param name="found">What was found.</param>
        /// <param name="expected">The expected C# type.</param>
        /// <returns>The exception to throw.</returns>
        private static JsonSerializationException Refuse(JsonReader reader, string found, Type expected)
        {
            return new JsonSerializationException(
                $"Invalid value {found}: expected {ExpectedKind(expected)}. Path '{reader.Path}'.");
        }

        /// <summary>
        /// The JSON kind expected for a C# type, for the error messages.
        /// </summary>
        /// <param name="type">The C# type, without <see cref="Nullable{T}"/>.</param>
        /// <returns>A short description.</returns>
        private static string ExpectedKind(Type type)
        {
            if (type == typeof(string)) return "a JSON string";
            if (type == typeof(bool)) return "a JSON boolean";
            if (type == typeof(int)) return "a JSON integer";
            return "a finite JSON number";
        }

        #endregion
    }
}
