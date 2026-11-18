using System;
using Newtonsoft.Json;

namespace FlipNflop.Gla.Serialization
{
    /// <summary>
    /// Serializer of the GLA contract in the game: the attempt record (<c>Phase</c>) and the
    /// identification and session messages of the shared library FlipNflopCommons, in the transport
    /// format fixed by <c>contract/README.md</c> of the commons.
    /// Uses Newtonsoft.Json (package <c>com.unity.nuget.newtonsoft-json</c>) with
    /// <see cref="GlaContractResolver"/>, which takes every name from <c>ContractNames</c>,
    /// <see cref="EndCauseConverter"/> and <see cref="UtcDateTimeOffsetConverter"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Every field is written, with an explicit <c>null</c> when it has no value.</item>
    /// <item>The end cause is written as lowercase text and the dates in UTC as <c>yyyy-MM-ddTHH:mm:ss.fffZ</c>.</item>
    /// <item>
    /// When reading (the API responses <c>ApiError</c>, <c>CodeVerificationResult</c> and
    /// <c>SessionOpened</c>), a missing field, an unknown field, a <c>null</c> where the type does
    /// not admit it, a date without offset and a value of the wrong JSON kind are format errors.
    /// </item>
    /// </list>
    /// Used by the identification (gla-13), the attempt collection (gla-14) and the delivery (gla-15).
    /// </remarks>
    public static class GlaJson
    {
        #region Settings

        /// <summary>
        /// Shared contract resolver; it caches the contract of each type, so one instance serves every call.
        /// </summary>
        private static readonly GlaContractResolver SharedResolver = new GlaContractResolver();

        /// <summary>
        /// Settings used by <see cref="Serialize"/> and <see cref="Deserialize{T}"/>.
        /// </summary>
        private static readonly JsonSerializerSettings SharedSettings = CreateSettings();

        /// <summary>
        /// Creates a new instance of the settings of the contract, for callers that drive a
        /// <see cref="JsonSerializer"/> themselves. Interacts with <see cref="GlaContractResolver"/>,
        /// <see cref="EndCauseConverter"/> and <see cref="UtcDateTimeOffsetConverter"/>.
        /// </summary>
        /// <returns>Settings with the contract names, explicit nulls, strict reading and the two converters.</returns>
        public static JsonSerializerSettings CreateSettings()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = SharedResolver,
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include,
                MissingMemberHandling = MissingMemberHandling.Error,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double,
                TypeNameHandling = TypeNameHandling.None,
                Formatting = Formatting.None,
            };
            settings.Converters.Add(new EndCauseConverter());
            settings.Converters.Add(new UtcDateTimeOffsetConverter());
            return settings;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Writes a contract object (an attempt record or a message) as JSON.
        /// </summary>
        /// <param name="value">The object to write.</param>
        /// <param name="indented">Whether to indent the JSON, for logs; the API accepts both forms.</param>
        /// <returns>The JSON text.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="JsonSerializationException">A field that does not admit <c>null</c> is <c>null</c>, or the end cause is not defined.</exception>
        public static string Serialize(object value, bool indented = false)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            return JsonConvert.SerializeObject(value, indented ? Formatting.Indented : Formatting.None, SharedSettings);
        }

        /// <summary>
        /// Reads a contract object from JSON, refusing what the contract refuses.
        /// </summary>
        /// <typeparam name="T">The contract type, for example <c>SessionOpened</c>.</typeparam>
        /// <param name="json">The JSON text of one object.</param>
        /// <returns>The object read.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="json"/> is <c>null</c>.</exception>
        /// <exception cref="JsonException">The text is not valid JSON or breaks a rule of the contract; the message cites the field.</exception>
        public static T Deserialize<T>(string json) where T : class
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            T value = JsonConvert.DeserializeObject<T>(json, SharedSettings);
            if (value == null)
                throw new JsonSerializationException($"Expected a JSON object of type {typeof(T).Name}, got null.");

            return value;
        }

        #endregion
    }
}
