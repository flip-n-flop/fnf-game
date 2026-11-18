using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using FlipNflop.Commons.Building;
using FlipNflop.Commons.Errors;
using FlipNflop.Commons.Messages;
using FlipNflop.Commons.Records;
using FlipNflop.Commons.Validation;
using FlipNflop.Gla.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace FlipNflop.Gla.Tests
{
    /// <summary>
    /// EditMode tests of <see cref="GlaJson"/> against the canonical examples of FlipNflopCommons,
    /// copied to <c>Assets/Tests/Gla/Contract</c> by <c>scripts/update-commons.sh</c>, following
    /// "Como testar um serializador" of the commons <c>contract/README.md</c>.
    /// Interacts with <see cref="AttemptRecordValidator"/> and <see cref="AttemptRecordBuilder"/>.
    /// </summary>
    public class GlaJsonTests
    {
        #region Example Catalog

        private static readonly Dictionary<string, Type> SubjectTypes = new Dictionary<string, Type>
        {
            ["record"] = typeof(Phase),
            ["code-request"] = typeof(CodeRequest),
            ["code-verification"] = typeof(CodeVerification),
            ["code-verification-result"] = typeof(CodeVerificationResult),
            ["session-opening"] = typeof(SessionOpening),
            ["session-opened"] = typeof(SessionOpened),
            ["api-error"] = typeof(ApiError),
        };

        private static readonly Dictionary<string, int> RecordColumnCounts = new Dictionary<string, int>
        {
            ["record.arrival.json"] = 42,
            ["record.fall.json"] = 42,
            ["record.collision.json"] = 36,
            ["record.restart.json"] = 48,
            ["record.exit.json"] = 54,
        };

        private static readonly string[] InvalidRules = { "no-offset", "missing", "unknown", "null", "uppercase", "invalid", "number" };

        /// <summary>
        /// The folder with the canonical examples, inside the Unity project (<see cref="Application.dataPath"/>).
        /// </summary>
        private static string ContractDirectory => Path.Combine(Application.dataPath, "Tests", "Gla", "Contract");

        /// <summary>
        /// The file names of <c>Contract/valid</c>, one test case each.
        /// </summary>
        /// <returns>The file names, sorted.</returns>
        private static IEnumerable<string> ValidExamples() => ExampleNames("valid");

        /// <summary>
        /// The file names of <c>Contract/invalid</c>, one test case each.
        /// </summary>
        /// <returns>The file names, sorted.</returns>
        private static IEnumerable<string> InvalidExamples() => ExampleNames("invalid");

        #endregion

        #region Tests

        /// <summary>
        /// Each valid example is read with the type of its subject and written again with the same
        /// keys and values; a record also passes <see cref="AttemptRecordValidator"/> with its number of columns.
        /// </summary>
        /// <param name="fileName">The example file.</param>
        [TestCaseSource(nameof(ValidExamples))]
        public void ValidExampleRoundTripsWithSameKeysAndValues(string fileName)
        {
            string original = File.ReadAllText(Path.Combine(ContractDirectory, "valid", fileName));
            Type type = TypeOf(fileName);

            object value = Deserialize(original, type);
            string written = GlaJson.Serialize(value);

            string difference = FirstDifference(ParseByValue(original), ParseByValue(written), "$");
            Assert.IsNull(difference, $"{fileName}: {difference}\nWritten: {written}");

            if (value is Phase record)
            {
                IReadOnlyList<string> problems = AttemptRecordValidator.Validate(record, RecordColumnCounts[fileName]);
                Assert.IsEmpty(problems, $"{fileName}: {string.Join("; ", problems)}");
            }
        }

        /// <summary>
        /// Each invalid example is refused, and the error cites the field named in the file name.
        /// </summary>
        /// <param name="fileName">The example file, <c>subject.rule-field.json</c>.</param>
        [TestCaseSource(nameof(InvalidExamples))]
        public void InvalidExampleIsRefusedCitingTheField(string fileName)
        {
            string json = File.ReadAllText(Path.Combine(ContractDirectory, "invalid", fileName));
            Type type = TypeOf(fileName);
            string field = FieldOf(fileName);

            var error = Assert.Catch<JsonException>(() => Deserialize(json, type), fileName);
            StringAssert.Contains(field, error.Message, $"{fileName}: the error does not cite '{field}'.");
        }

        /// <summary>
        /// An abandoned attempt built by <see cref="AttemptRecordBuilder"/> is written with every
        /// field, with explicit <c>null</c> in the validation fields and in each item of <c>correct</c>.
        /// </summary>
        [Test]
        public void AbandonedRecordIsWrittenWithExplicitNulls()
        {
            const int columns = 48;
            var start = new DateTimeOffset(2026, 10, 20, 14, 45, 10, 3, TimeSpan.FromHours(-3));
            var builder = new AttemptRecordBuilder("fifth_level", columns, "0.15.0", start);
            for (int column = 0; column < 16; column++) builder.AddTime(column, 1.25);

            var trace = new int?[columns];
            for (int column = 0; column < 16; column++) trace[column] = column % 2;

            bool finished = builder.TryFinish(EndCause.Restart, start.AddMilliseconds(21773), trace, null, out Phase record);
            Assert.IsTrue(finished);

            JObject json = ParseByValue(GlaJson.Serialize(record));
            var section = (JObject)((JArray)json["sections"])[0];
            var pathPlayer = (JObject)section["path_player"];
            var columnsObject = (JObject)pathPlayer["columns"];

            AssertExplicitNull(section, "conclusion");
            AssertExplicitNull(section, "performance");
            AssertExplicitNull(pathPlayer, "overall_accuracy");
            Assert.AreEqual("restart", (string)pathPlayer["end_cause"]);
            Assert.AreEqual("2026-10-20T17:45:10.003Z", (string)section["dateTimeStart"]);
            Assert.AreEqual("2026-10-20T17:45:31.776Z", (string)section["dateTimeFinish"]);

            var correct = (JArray)columnsObject["correct"];
            Assert.AreEqual(columns, correct.Count);
            Assert.IsTrue(correct.All(item => item.Type == JTokenType.Null), "correct must hold only nulls.");

            var time = (JArray)columnsObject["time"];
            Assert.AreEqual(JTokenType.Null, time[columns - 1].Type);

            AssertKeys(json, "phase_name", "sections");
            AssertKeys(section, "dateTimeStart", "dateTimeFinish", "conclusion", "performance", "path_player");
            AssertKeys(pathPlayer, "game_version", "end_cause", "overall_accuracy", "columns");
            AssertKeys(columnsObject, "time", "hints", "correct", "trace");
        }

        /// <summary>
        /// A guest session opening is written with <c>"credential": null</c> and the date in UTC.
        /// </summary>
        [Test]
        public void GuestSessionOpeningIsWrittenWithNullCredentialAndUtcDate()
        {
            var opening = new SessionOpening
            {
                GameVersion = "0.15.0",
                Credential = null,
                StartedAt = new DateTimeOffset(2026, 10, 20, 14, 15, 40, 512, TimeSpan.FromHours(-3)),
            };

            JObject json = ParseByValue(GlaJson.Serialize(opening));

            AssertExplicitNull(json, "credential");
            Assert.AreEqual("2026-10-20T17:15:40.512Z", (string)json["started_at"]);
        }

        /// <summary>
        /// Writing a field that does not admit <c>null</c> as <c>null</c> is refused, citing the field.
        /// </summary>
        [Test]
        public void NullInRequiredFieldIsRefusedWhenWriting()
        {
            var error = Assert.Catch<JsonException>(() => GlaJson.Serialize(new SessionOpened { SessionToken = null, Identified = false }));
            StringAssert.Contains("session_token", error.Message);
        }

        /// <summary>
        /// A field of the wrong JSON kind is refused when reading an API response.
        /// </summary>
        [Test]
        public void ValueOfWrongKindIsRefusedWhenReading()
        {
            var error = Assert.Catch<JsonException>(() => GlaJson.Deserialize<SessionOpened>("{\"session_token\":\"t\",\"identified\":\"true\"}"));
            StringAssert.Contains("identified", error.Message);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Lists the example files of a folder of <see cref="ContractDirectory"/>.
        /// </summary>
        /// <param name="folder"><c>valid</c> or <c>invalid</c>.</param>
        /// <returns>The file names, sorted.</returns>
        private static IEnumerable<string> ExampleNames(string folder)
        {
            return Directory.GetFiles(Path.Combine(ContractDirectory, folder), "*.json")
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// The contract type of an example, from the subject at the start of the file name.
        /// </summary>
        /// <param name="fileName">The example file.</param>
        /// <returns>The contract type.</returns>
        private static Type TypeOf(string fileName)
        {
            string subject = fileName.Substring(0, fileName.IndexOf('.'));
            Assert.IsTrue(SubjectTypes.ContainsKey(subject), $"Unknown subject '{subject}' in {fileName}.");
            return SubjectTypes[subject];
        }

        /// <summary>
        /// The transport name that the refusal of an invalid example must cite: the part after the rule.
        /// </summary>
        /// <param name="fileName">The example file, <c>subject.rule-field.json</c>.</param>
        /// <returns>The field name.</returns>
        private static string FieldOf(string fileName)
        {
            string ruleAndField = Path.GetFileNameWithoutExtension(fileName).Substring(fileName.IndexOf('.') + 1);
            string rule = InvalidRules.FirstOrDefault(r => ruleAndField.StartsWith(r + "-", StringComparison.Ordinal));
            Assert.IsNotNull(rule, $"Unknown rule in {fileName}.");
            return ruleAndField.Substring(rule.Length + 1);
        }

        /// <summary>
        /// Calls <see cref="GlaJson.Deserialize{T}"/> for a type known only at run time.
        /// </summary>
        /// <param name="json">The JSON text.</param>
        /// <param name="type">The contract type.</param>
        /// <returns>The object read.</returns>
        private static object Deserialize(string json, Type type)
        {
            var method = typeof(GlaJson).GetMethod(nameof(GlaJson.Deserialize)).MakeGenericMethod(type);
            try
            {
                return method.Invoke(null, new object[] { json });
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        /// <summary>
        /// Parses JSON keeping dates as text and numbers as numbers.
        /// </summary>
        /// <param name="json">The JSON text.</param>
        /// <returns>The JSON object.</returns>
        private static JObject ParseByValue(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Decimal })
            {
                return JObject.Load(reader);
            }
        }

        /// <summary>
        /// Compares two JSON trees by value (same keys, same values, numbers compared as numbers).
        /// </summary>
        /// <param name="expected">The original tree.</param>
        /// <param name="actual">The tree written by <see cref="GlaJson"/>.</param>
        /// <param name="path">The path of the current node, for the message.</param>
        /// <returns>A description of the first difference, or <c>null</c> when equal.</returns>
        private static string FirstDifference(JToken expected, JToken actual, string path)
        {
            if (IsNumber(expected) && IsNumber(actual))
            {
                decimal a = Convert.ToDecimal(((JValue)expected).Value, CultureInfo.InvariantCulture);
                decimal b = Convert.ToDecimal(((JValue)actual).Value, CultureInfo.InvariantCulture);
                return a == b ? null : $"{path}: expected {a}, got {b}";
            }

            if (expected.Type != actual.Type) return $"{path}: expected {expected.Type}, got {actual.Type}";

            switch (expected)
            {
                case JObject expectedObject:
                {
                    var actualObject = (JObject)actual;
                    var expectedKeys = expectedObject.Properties().Select(p => p.Name).OrderBy(k => k, StringComparer.Ordinal).ToArray();
                    var actualKeys = actualObject.Properties().Select(p => p.Name).OrderBy(k => k, StringComparer.Ordinal).ToArray();
                    if (!expectedKeys.SequenceEqual(actualKeys))
                        return $"{path}: expected keys [{string.Join(", ", expectedKeys)}], got [{string.Join(", ", actualKeys)}]";

                    foreach (string key in expectedKeys)
                    {
                        string difference = FirstDifference(expectedObject[key], actualObject[key], path + "." + key);
                        if (difference != null) return difference;
                    }

                    return null;
                }
                case JArray expectedArray:
                {
                    var actualArray = (JArray)actual;
                    if (expectedArray.Count != actualArray.Count)
                        return $"{path}: expected {expectedArray.Count} items, got {actualArray.Count}";

                    for (int i = 0; i < expectedArray.Count; i++)
                    {
                        string difference = FirstDifference(expectedArray[i], actualArray[i], $"{path}[{i}]");
                        if (difference != null) return difference;
                    }

                    return null;
                }
                default:
                    return JToken.DeepEquals(expected, actual) ? null : $"{path}: expected {expected}, got {actual}";
            }
        }

        /// <summary>
        /// Whether a token is a JSON number.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns><c>true</c> for integers and floats.</returns>
        private static bool IsNumber(JToken token) => token.Type == JTokenType.Integer || token.Type == JTokenType.Float;

        /// <summary>
        /// Asserts that an object has the key with an explicit JSON <c>null</c>.
        /// </summary>
        /// <param name="json">The object.</param>
        /// <param name="key">The transport name.</param>
        private static void AssertExplicitNull(JObject json, string key)
        {
            Assert.IsTrue(json.ContainsKey(key), $"'{key}' must be written.");
            Assert.AreEqual(JTokenType.Null, json[key].Type, $"'{key}' must be null.");
        }

        /// <summary>
        /// Asserts that an object has exactly the given keys.
        /// </summary>
        /// <param name="json">The object.</param>
        /// <param name="keys">The expected transport names.</param>
        private static void AssertKeys(JObject json, params string[] keys)
        {
            CollectionAssert.AreEquivalent(keys, json.Properties().Select(p => p.Name).ToArray());
        }

        #endregion
    }
}
