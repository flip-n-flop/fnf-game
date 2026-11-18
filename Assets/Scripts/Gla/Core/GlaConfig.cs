using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FlipNflop.Gla
{
    /// <summary>
    /// Build configuration of the GLA, read from <c>StreamingAssets/gla-config.json</c>:
    /// <c>{ "api_base_url": "https://host[/path]", "collection_enabled": true }</c>.
    /// The file can be edited after the build to change the API address without compiling again.
    /// Parsed with Newtonsoft.Json (<see cref="JToken"/>), because it is not part of the contract of
    /// FlipNflopCommons. Read by <c>GlaServices</c>, which turns an invalid file into collection off.
    /// </summary>
    public sealed class GlaConfig
    {
        #region Constants

        public const string FileName = "gla-config.json";
        public const string ApiBaseUrlKey = "api_base_url";
        public const string CollectionEnabledKey = "collection_enabled";

        #endregion

        #region Construction

        /// <summary>
        /// Creates a configuration already checked by <see cref="TryParse"/>.
        /// </summary>
        /// <param name="apiBaseUrl">The API address without the final slash, or <c>null</c> with the collection off.</param>
        /// <param name="collectionEnabled">Whether the collection is on.</param>
        private GlaConfig(string apiBaseUrl, bool collectionEnabled)
        {
            ApiBaseUrl = apiBaseUrl;
            CollectionEnabled = collectionEnabled;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The absolute <c>http</c> or <c>https</c> address of the API, without the final slash.
        /// <c>null</c> when the collection is off.
        /// </summary>
        public string ApiBaseUrl { get; }

        public bool CollectionEnabled { get; }

        #endregion

        #region Public API

        /// <summary>
        /// Reads the text of <c>gla-config.json</c>. <c>collection_enabled</c> must be a boolean.
        /// With the collection on, <c>api_base_url</c> must be an absolute <c>http</c> or <c>https</c>
        /// address without query or fragment; with it off, the address is ignored. Unknown keys are ignored.
        /// </summary>
        /// <param name="json">The file text.</param>
        /// <param name="config">The configuration read, or <c>null</c>.</param>
        /// <param name="problem">Why the text was refused, in English, for the log; <c>null</c> on success.</param>
        /// <returns><c>true</c> when the text is a valid configuration.</returns>
        public static bool TryParse(string json, out GlaConfig config, out string problem)
        {
            config = null;
            problem = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                problem = "the file is empty";
                return false;
            }

            JToken token;
            try
            {
                token = JToken.Parse(json);
            }
            catch (JsonException exception)
            {
                problem = $"the file is not valid JSON ({exception.Message})";
                return false;
            }

            if (!(token is JObject root))
            {
                problem = "the file is not a JSON object";
                return false;
            }

            JToken enabledToken = root[CollectionEnabledKey];
            if (enabledToken == null || enabledToken.Type != JTokenType.Boolean)
            {
                problem = $"\"{CollectionEnabledKey}\" must be true or false";
                return false;
            }

            bool collectionEnabled = enabledToken.Value<bool>();
            if (!collectionEnabled)
            {
                config = new GlaConfig(null, false);
                return true;
            }

            JToken urlToken = root[ApiBaseUrlKey];
            string url = urlToken != null && urlToken.Type == JTokenType.String ? urlToken.Value<string>() : null;
            if (!TryNormalizeBaseUrl(url, out string baseUrl))
            {
                problem = $"\"{ApiBaseUrlKey}\" must be the absolute http or https address of the API, without query or fragment";
                return false;
            }

            config = new GlaConfig(baseUrl, true);
            return true;
        }

        /// <summary>
        /// Checks an API address and removes the spaces at the ends and the final slashes.
        /// </summary>
        /// <param name="url">The address, possibly <c>null</c>.</param>
        /// <param name="baseUrl">The normalized address, or <c>null</c>.</param>
        /// <returns><c>true</c> when the address is an absolute <c>http</c> or <c>https</c> URL without query or fragment.</returns>
        public static bool TryNormalizeBaseUrl(string url, out string baseUrl)
        {
            baseUrl = null;
            if (string.IsNullOrWhiteSpace(url)) return false;

            string trimmed = url.Trim().TrimEnd('/');
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
            if (string.IsNullOrEmpty(uri.Host)) return false;
            if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;

            baseUrl = trimmed;
            return true;
        }

        /// <summary>
        /// Describes the configuration for the log, without any secret (the file has none).
        /// </summary>
        /// <returns>The two values of the file.</returns>
        public override string ToString()
        {
            return CollectionEnabled
                ? $"{CollectionEnabledKey}=true, {ApiBaseUrlKey}={ApiBaseUrl}"
                : $"{CollectionEnabledKey}=false";
        }

        #endregion
    }
}
