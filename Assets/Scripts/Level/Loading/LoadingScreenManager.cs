using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Persistent (<c>DontDestroyOnLoad</c>) world-space loading screen controller using a dedicated orthographic camera,
/// standalone <see cref="SpriteRenderer"/> layers, and <see cref="TMP_Text"/> elements (no Canvas / no UI Toolkit).
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance { get; private set; }

    [Header("Core Rig")]
    [Tooltip("Dedicated orthographic Camera used only while the loading screen is active.")]
    [SerializeField] private Camera loadingCamera;

    [Tooltip("Parent Transform holding all visual layers (background, diagonal panel, halftone, texts).")]
    [SerializeField] private GameObject visualsRoot;

    [Header("Text Elements (Optional)")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text tipText;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private LoadingWaveText loadingWaveText;

    [Header("Infinite Halftone Scroll (Optional)")]
    [Tooltip("Renderer whose material mainTextureOffset scrolls continuously while loading.")]
    [SerializeField] private Renderer halftoneRenderer;

    [Tooltip("UV scroll speed per second (unscaled time). Requires a Wrap Mode = Repeat texture.")]
    [SerializeField] private Vector2 halftoneScrollSpeed = new Vector2(-0.25f, 0.12f);

    [Header("Timing")]
    [Tooltip("Minimum time (in seconds) the loading screen stays visible so the design/animation can be seen.")]
    [SerializeField] private float minDisplayDuration = 2.0f;

    [Tooltip("Fade-in duration in seconds (unscaled).")]
    [SerializeField] private float fadeInDuration = 0.25f;

    [Tooltip("Fade-out duration in seconds (unscaled).")]
    [SerializeField] private float fadeOutDuration = 0.35f;

    [Header("Fade Overlay")]
    [Tooltip("Full-screen dark SpriteRenderer placed in front of the camera for smooth fade-in / fade-out.")]
    [SerializeField] private SpriteRenderer fadeOverlay;

    [Header("Debug")]
    [Tooltip("If enabled, keeps the loading screen visible on Awake for quick visual preview in Play Mode.")]
    [SerializeField] private bool showOnAwakeForTesting = false;

    private bool isLoading;
    private Material halftoneMatInstance;
    private Vector2 halftoneOffset;
    private readonly List<UIDocument> hiddenUIDocuments = new List<UIDocument>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (halftoneRenderer != null)
        {
            halftoneMatInstance = halftoneRenderer.material;
        }

        if (showOnAwakeForTesting)
        {
            isLoading = true;
            SetScreenActive(true);
            SetOverlayAlpha(0f);
        }
        else
        {
            SetScreenActive(false);
        }
    }

    private void Update()
    {
        if (!isLoading || halftoneMatInstance == null) return;

        halftoneOffset += halftoneScrollSpeed * Time.unscaledDeltaTime;
        halftoneOffset.x = Mathf.Repeat(halftoneOffset.x, 1f);
        halftoneOffset.y = Mathf.Repeat(halftoneOffset.y, 1f);
        halftoneMatInstance.mainTextureOffset = halftoneOffset;
    }

    /// <summary>
    /// Static convenience wrapper. Falls back to direct <see cref="SceneManager.LoadScene(string)"/>
    /// if no <see cref="LoadingScreenManager"/> instance is present.
    /// </summary>
    public static void Load(string sceneName, string levelTitle = null, string tip = null, float? overrideMinDuration = null)
    {
        if (Instance != null)
        {
            Instance.LoadScene(sceneName, levelTitle, tip, overrideMinDuration);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    /// <summary>
    /// Shows the loading screen, loads <paramref name="sceneName"/> asynchronously, waits at least
    /// <see cref="minDisplayDuration"/>, and fades out into the loaded scene.
    /// </summary>
    public void LoadScene(string sceneName, string levelTitle = null, string tip = null, float? overrideMinDuration = null)
    {
        if (isLoading) return;
        StartCoroutine(LoadSceneRoutine(sceneName, levelTitle, tip, overrideMinDuration ?? minDisplayDuration));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, string levelTitle, string tip, float displayDuration)
    {
        isLoading = true;

        HideActiveUIDocuments();
        ApplyTexts(levelTitle, tip);
        SetScreenActive(true);

        // 1. Fade from black/scene into the loading screen
        yield return FadeOverlay(1f, 0f, fadeInDuration);

        // 2. Start async scene load without activating immediately
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op != null)
        {
            op.allowSceneActivation = false;
        }

        float elapsed = 0f;
        while (elapsed < displayDuration || (op != null && op.progress < 0.9f))
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 3. Activate scene and wait for it to finish initializing
        if (op != null)
        {
            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                yield return null;
            }
        }

        // Wait one extra frame so LevelJsonLoader / Start() finishes setting up the level
        yield return null;

        // Keep new scene UIDocuments hidden until fade-out completes
        HideActiveUIDocuments();

        // 4. Fade out to reveal the loaded level
        yield return FadeOverlay(0f, 1f, fadeOutDuration);

        RestoreHiddenUIDocuments();
        SetScreenActive(false);
        isLoading = false;
    }

    private void HideActiveUIDocuments()
    {
        UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude);
        for (int i = 0; i < docs.Length; i++)
        {
            UIDocument doc = docs[i];
            if (doc != null && doc.rootVisualElement != null && doc.rootVisualElement.style.display != DisplayStyle.None)
            {
                doc.rootVisualElement.style.display = DisplayStyle.None;
                if (!hiddenUIDocuments.Contains(doc))
                {
                    hiddenUIDocuments.Add(doc);
                }
            }
        }
    }

    private void RestoreHiddenUIDocuments()
    {
        for (int i = 0; i < hiddenUIDocuments.Count; i++)
        {
            UIDocument doc = hiddenUIDocuments[i];
            if (doc != null && doc.rootVisualElement != null)
            {
                doc.rootVisualElement.style.display = DisplayStyle.Flex;
            }
        }
        hiddenUIDocuments.Clear();
    }

    private void ApplyTexts(string levelTitle, string tip)
    {
        if (titleText != null && !string.IsNullOrEmpty(levelTitle))
        {
            titleText.text = levelTitle;
        }

        if (tipText != null && !string.IsNullOrEmpty(tip))
        {
            tipText.text = tip;
        }

        if (loadingWaveText != null)
        {
            loadingWaveText.RefreshMeshCache();
        }
    }

    private void SetScreenActive(bool active)
    {
        if (loadingCamera != null) loadingCamera.enabled = active;
        if (visualsRoot != null) visualsRoot.SetActive(active);
        if (fadeOverlay != null) fadeOverlay.gameObject.SetActive(active);
        if (active) AspectRatioEnforcer.RefreshNow();
    }

    private IEnumerator FadeOverlay(float fromAlpha, float toAlpha, float duration)
    {
        if (fadeOverlay == null || duration <= 0.001f)
        {
            SetOverlayAlpha(toAlpha);
            yield break;
        }

        float elapsed = 0f;
        SetOverlayAlpha(fromAlpha);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetOverlayAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }

        SetOverlayAlpha(toAlpha);
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (fadeOverlay == null) return;
        Color c = fadeOverlay.color;
        c.a = alpha;
        fadeOverlay.color = c;
    }
}
