using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using FlipNflop.Commons.Errors;
using FlipNflop.Gla.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FlipNflop.Gla.Tests
{
    /// <summary>
    /// <see cref="IHttpTransport"/> of the tests: records every request and answers it at once with
    /// the next queued result, or keeps it pending until the test answers it with
    /// <see cref="PendingRequest.Answer"/>.
    /// </summary>
    internal sealed class FakeHttpTransport : IHttpTransport
    {
        #region Fields

        private readonly Queue<HttpResult> queuedResults = new Queue<HttpResult>();

        #endregion

        #region Properties

        public List<PendingRequest> Requests { get; } = new List<PendingRequest>();

        /// <summary>
        /// The requests not answered yet, in the order they were made.
        /// </summary>
        public IEnumerable<PendingRequest> Unanswered => Requests.Where(request => !request.IsAnswered);

        #endregion

        #region Public API

        /// <summary>
        /// Queues the result of the next request, which is then answered before <see cref="PostJson"/> returns.
        /// </summary>
        /// <param name="result">The result.</param>
        public void Enqueue(HttpResult result)
        {
            queuedResults.Enqueue(result);
        }

        /// <summary>
        /// Records the request and answers it with the next queued result, if any.
        /// </summary>
        /// <param name="url">The address.</param>
        /// <param name="json">The body.</param>
        /// <param name="bearerToken">The token, or <c>null</c>.</param>
        /// <param name="timeoutSeconds">The timeout.</param>
        /// <param name="onCompleted">Receives the result.</param>
        public void PostJson(string url, string json, string bearerToken, int timeoutSeconds, Action<HttpResult> onCompleted)
        {
            var request = new PendingRequest(url, json, bearerToken, timeoutSeconds, onCompleted);
            Requests.Add(request);
            if (queuedResults.Count > 0) request.Answer(queuedResults.Dequeue());
        }

        #endregion

        /// <summary>
        /// A request recorded by <see cref="FakeHttpTransport"/>.
        /// </summary>
        internal sealed class PendingRequest
        {
            private readonly Action<HttpResult> onCompleted;

            /// <summary>
            /// Records a request.
            /// </summary>
            /// <param name="url">The address.</param>
            /// <param name="json">The body.</param>
            /// <param name="bearerToken">The token, or <c>null</c>.</param>
            /// <param name="timeoutSeconds">The timeout.</param>
            /// <param name="onCompleted">Receives the result.</param>
            public PendingRequest(string url, string json, string bearerToken, int timeoutSeconds, Action<HttpResult> onCompleted)
            {
                Url = url;
                Json = json;
                BearerToken = bearerToken;
                TimeoutSeconds = timeoutSeconds;
                this.onCompleted = onCompleted;
            }

            public string Url { get; }

            public string Json { get; }

            public string BearerToken { get; }

            public int TimeoutSeconds { get; }

            public bool IsAnswered { get; private set; }

            /// <summary>
            /// Answers the request.
            /// </summary>
            /// <param name="result">The result.</param>
            /// <exception cref="InvalidOperationException">The request was already answered.</exception>
            public void Answer(HttpResult result)
            {
                if (IsAnswered) throw new InvalidOperationException("The fake request was already answered.");

                IsAnswered = true;
                onCompleted(result);
            }
        }
    }

    /// <summary>
    /// <see cref="IKeyValueStorage"/> of the tests, in memory, counting the saves.
    /// </summary>
    internal sealed class MemoryStorage : IKeyValueStorage
    {
        public Dictionary<string, string> Values { get; } = new Dictionary<string, string>();

        public int SaveCount { get; private set; }

        /// <summary>
        /// Whether the key exists.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns><c>true</c> when it exists.</returns>
        public bool HasKey(string key) => Values.ContainsKey(key);

        /// <summary>
        /// Reads a text, empty when missing, like <c>PlayerPrefs</c>.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The text.</returns>
        public string GetString(string key) => Values.TryGetValue(key, out string value) ? value : string.Empty;

        /// <summary>
        /// Stores a text.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The text.</param>
        public void SetString(string key, string value) => Values[key] = value;

        /// <summary>
        /// Removes a key.
        /// </summary>
        /// <param name="key">The key.</param>
        public void DeleteKey(string key) => Values.Remove(key);

        /// <summary>
        /// Counts a save.
        /// </summary>
        public void Save() => SaveCount++;
    }

    /// <summary>
    /// <see cref="IGlaLog"/> of the tests, keeping the lines.
    /// </summary>
    internal sealed class RecordingLog : IGlaLog
    {
        public List<string> Infos { get; } = new List<string>();

        public List<string> Warnings { get; } = new List<string>();

        public IEnumerable<string> All => Infos.Concat(Warnings);

        /// <summary>
        /// Keeps an informative line.
        /// </summary>
        /// <param name="message">The line.</param>
        public void Info(string message) => Infos.Add(message);

        /// <summary>
        /// Keeps a warning.
        /// </summary>
        /// <param name="message">The line.</param>
        public void Warning(string message) => Warnings.Add(message);
    }

    /// <summary>
    /// Answers of the API as the routes send them: the bodies are written by <see cref="GlaJson"/>
    /// from the commons types, as the API writes them with the same names, and the messages are
    /// those of <c>GlaErrorMessages</c> of the API (gla-04, gla-05).
    /// </summary>
    internal static class ApiAnswers
    {
        public const string DomainNotAllowedMessage = "Use o seu e-mail institucional (domínios aceitos: estudante.ufjf.br, ufjf.br).";
        public const string InvalidCodeMessage = "Código incorreto. Confira o e-mail ou peça um novo código.";
        public const string RateLimitedMessage = "Muitas tentativas. Aguarde alguns minutos e tente de novo.";
        public const string CodeNotSentMessage = "Não foi possível enviar o código agora. Tente de novo em alguns minutos ou entre como convidado.";
        public const string FutureStartMessage = "O campo 'started_at' está à frente do relógio do servidor. Acerte a data e a hora do computador e recarregue a página.";
        public const string InvalidCredentialMessage = "A sua identificação expirou ou não é válida. Entre de novo com o seu e-mail ou como convidado.";

        /// <summary>
        /// A success without body (204).
        /// </summary>
        /// <returns>The result.</returns>
        public static HttpResult NoContent() => HttpResult.Answered(204, string.Empty);

        /// <summary>
        /// A success with a contract body.
        /// </summary>
        /// <param name="status">The status, such as 201.</param>
        /// <param name="body">The contract object.</param>
        /// <returns>The result.</returns>
        public static HttpResult Body(long status, object body) => HttpResult.Answered(status, GlaJson.Serialize(body));

        /// <summary>
        /// An error with the single error body.
        /// </summary>
        /// <param name="status">The status.</param>
        /// <param name="code">The <c>error</c> code.</param>
        /// <param name="message">The Portuguese message.</param>
        /// <returns>The result.</returns>
        public static HttpResult Error(long status, string code, string message) =>
            HttpResult.Answered(status, GlaJson.Serialize(ApiError.Create(code, message)));

        /// <summary>
        /// A network failure.
        /// </summary>
        /// <returns>The result.</returns>
        public static HttpResult NetworkFailure() => HttpResult.Failed(HttpFailure.Network, "Cannot connect to destination host");

        /// <summary>
        /// A timeout.
        /// </summary>
        /// <returns>The result.</returns>
        public static HttpResult TimeoutFailure() => HttpResult.Failed(HttpFailure.Timeout, "no answer within the timeout");
    }

    /// <summary>
    /// Reads the request bodies in the tests keeping the dates as the text sent, as the API reads them.
    /// </summary>
    internal static class RequestJson
    {
        /// <summary>
        /// Parses a JSON object without turning ISO 8601 texts into dates.
        /// </summary>
        /// <param name="json">The body.</param>
        /// <returns>The object.</returns>
        public static JObject Parse(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
            {
                return JObject.Load(reader);
            }
        }
    }
}
