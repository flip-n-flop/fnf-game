using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace FlipNflop.Gla
{
    /// <summary>
    /// Services of the Game Learning Analytics, created by code before the first scene, without any
    /// scene edit (gla-13 design D1), and kept across scenes by <c>DontDestroyOnLoad</c>.
    /// Records <see cref="PageLoadedAt"/>, reads <c>StreamingAssets/gla-config.json</c> with
    /// <see cref="UnityWebRequest"/> (<see cref="GlaConfig"/>), creates the <see cref="GlaApiClient"/>
    /// over <see cref="UnityWebRequestTransport"/> and owns the <see cref="PlayerSession"/>, whose
    /// credential lives in <see cref="PlayerPrefsStorage"/>. While the configuration is read, the mode
    /// is <see cref="PlayerMode.Loading"/>; without a valid configuration in
    /// <see cref="ConfigTimeoutSeconds"/>, the collection is turned off.
    /// <c>MenuManager.OnEnable</c> calls <see cref="AttachEntryScreen"/>; the attempt collection
    /// (gla-14) and the delivery (gla-15) hang under this object.
    /// </summary>
    public sealed class GlaServices : MonoBehaviour
    {
        #region Constants

        public const float ConfigTimeoutSeconds = 5f;
        private const string ObjectName = "GlaServices";

        #endregion

        #region Fields

        private IGlaLog log;
        private IdentificationScreenController entryScreen;

        #endregion

        #region Properties

        /// <summary>
        /// The services of this page load, or <c>null</c> before the bootstrap and after the object is destroyed.
        /// </summary>
        public static GlaServices Instance { get; private set; }

        /// <summary>
        /// The instant of the page load (UTC, millisecond precision): the <c>started_at</c> of every session.
        /// </summary>
        public DateTimeOffset PageLoadedAt { get; private set; }

        /// <summary>
        /// The configuration read, or <c>null</c> while loading or when it is missing or invalid.
        /// </summary>
        public GlaConfig Config { get; private set; }

        /// <summary>
        /// The API client, or <c>null</c> while loading and with the collection off.
        /// </summary>
        public GlaApiClient Api { get; private set; }

        public PlayerSession Session { get; private set; }

        public AttemptCollector Collector { get; private set; }

        public IGlaLog Log => log;

        #endregion

        #region Bootstrap

        /// <summary>
        /// Clears the static instance when entering Play Mode with the Domain Reload off.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        /// <summary>
        /// Creates the services before the first scene loads and records the instant of the page load.
        /// Interacts with <see cref="UnityEngine.Object.DontDestroyOnLoad"/>.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;

            DateTimeOffset pageLoadedAt = GlaTime.ToContractPrecision(DateTimeOffset.UtcNow);
            var servicesObject = new GameObject(ObjectName);
            DontDestroyOnLoad(servicesObject);
            servicesObject.AddComponent<GlaServices>().Initialize(pageLoadedAt);
        }

        /// <summary>
        /// Creates the identification state in <see cref="PlayerMode.Loading"/> and starts reading the
        /// configuration. Interacts with <see cref="Application.version"/> and <see cref="PlayerPrefsStorage"/>.
        /// </summary>
        /// <param name="pageLoadedAt">The instant of the page load.</param>
        private void Initialize(DateTimeOffset pageLoadedAt)
        {
            Instance = this;
            PageLoadedAt = pageLoadedAt;
            log = new UnityGlaLog();
            Session = new PlayerSession(
                pageLoadedAt,
                Application.version,
                new CredentialStore(new PlayerPrefsStorage()),
                () => DateTimeOffset.UtcNow,
                log);

            var collectorObject = new GameObject(nameof(AttemptCollector));
            collectorObject.transform.SetParent(transform, false);
            Collector = collectorObject.AddComponent<AttemptCollector>();

            log.Info($"Page loaded at {GlaTime.Format(pageLoadedAt)}, game version {Application.version}. Reading {GlaConfig.FileName}.");
            StartCoroutine(LoadConfig());
        }

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Releases the entry screen and the static instance.
        /// </summary>
        private void OnDestroy()
        {
            entryScreen?.Dispose();
            entryScreen = null;
            if (Instance == this) Instance = null;
        }

        #endregion

        #region Public API

        /// <summary>
        /// The gla-13 hook of the main menu (design D2), called by <c>MenuManager.OnEnable</c> with the
        /// root of its <see cref="UIDocument"/>: overlays the waiting screen while loading and the entry
        /// screen in <see cref="PlayerMode.Undecided"/>, and adds the "Trocar e-mail" button. Does
        /// nothing with the collection off. Interacts with <see cref="IdentificationScreenController"/>.
        /// </summary>
        /// <param name="root">The root visual element of the menu document.</param>
        public void AttachEntryScreen(VisualElement root)
        {
            if (root == null || Session == null) return;

            entryScreen?.Dispose();
            entryScreen = null;
            if (Session.Mode == PlayerMode.Disabled) return;

            entryScreen = IdentificationScreenController.Attach(root, Session, log);
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Reads <c>gla-config.json</c> from <see cref="Application.streamingAssetsPath"/> with
        /// <see cref="UnityWebRequest"/> (the only way on WebGL) and turns the collection on or off.
        /// Interacts with <see cref="GlaConfig.TryParse"/>, <see cref="PlayerSession.Enable"/> and
        /// <see cref="PlayerSession.Disable"/>.
        /// </summary>
        /// <returns>The coroutine.</returns>
        private IEnumerator LoadConfig()
        {
            string url = ConfigUrl();
            float startedAt = Time.realtimeSinceStartup;
            string text = null;
            string failure = null;

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = Mathf.CeilToInt(ConfigTimeoutSeconds);
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (Time.realtimeSinceStartup - startedAt >= ConfigTimeoutSeconds)
                    {
                        request.Abort();
                        failure = $"{GlaConfig.FileName} was not read within {ConfigTimeoutSeconds} s";
                        break;
                    }

                    yield return null;
                }

                if (failure == null)
                {
                    if (request.result == UnityWebRequest.Result.Success)
                        text = request.downloadHandler.text;
                    else
                        failure = $"{GlaConfig.FileName} could not be read ({request.error})";
                }
            }

            if (failure != null)
            {
                Session.Disable(failure);
                yield break;
            }

            if (!GlaConfig.TryParse(text, out GlaConfig config, out string problem))
            {
                Session.Disable($"{GlaConfig.FileName} is invalid: {problem}");
                yield break;
            }

            Config = config;
            log.Info($"Configuration read: {config}.");
            if (!config.CollectionEnabled)
            {
                Session.Disable($"{GlaConfig.CollectionEnabledKey} is false");
                yield break;
            }

            Api = new GlaApiClient(config.ApiBaseUrl, new UnityWebRequestTransport(this));
            Session.Enable(Api);
        }

        /// <summary>
        /// The address of the configuration file: an http URL on WebGL, a <c>file://</c> URL in the editor and on desktop.
        /// </summary>
        /// <returns>The address.</returns>
        private static string ConfigUrl()
        {
            string path = Application.streamingAssetsPath + "/" + GlaConfig.FileName;
            return path.Contains("://") ? path : "file://" + path;
        }

        #endregion
    }
}
