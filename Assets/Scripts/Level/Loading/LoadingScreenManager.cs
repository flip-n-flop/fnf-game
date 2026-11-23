using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
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
    private readonly List<(SpriteRenderer sr, float baseAlpha)> cachedSprites = new List<(SpriteRenderer, float)>();
    private readonly List<(TMP_Text tmp, float baseAlpha)> cachedTexts = new List<(TMP_Text, float)>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        int loadingLayer = LayerMask.NameToLayer("LoadingUI");
        if (loadingLayer >= 0)
        {
            SetLayerRecursively(gameObject, loadingLayer);
            if (loadingCamera != null)
            {
                loadingCamera.cullingMask = 1 << loadingLayer;
            }
        }

        if (halftoneRenderer != null)
        {
            halftoneMatInstance = halftoneRenderer.material;
        }

        CacheVisualAlphas();
        if (fadeOverlay != null) fadeOverlay.gameObject.SetActive(false);

        SceneManager.sceneLoaded += OnSceneLoaded;

        if (showOnAwakeForTesting)
        {
            isLoading = true;
            SetScreenActive(true);
            SetVisualsAlpha(1f);
        }
        else
        {
            SetScreenActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (isLoading)
        {
            HideActiveUIDocuments();
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
        Debug.Log($"[LoadingScreen] Start loading '{sceneName}'");

        ApplyTexts(levelTitle, tip);
        SetAsSolidBaseCamera();
        SetVisualsAlpha(1f);
        SetScreenActive(true);

        // 1. Fade out active menu UIDocuments directly over the already-opaque LoadingScreen
        //    so no empty camera background or black frame ever shows.
        hiddenUIDocuments.Clear();
        UIDocument[] sourceDocs = FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude);
        if (sourceDocs.Length > 0 && fadeInDuration > 0.001f)
        {
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeInDuration)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                float opVal = 1f - Mathf.Clamp01(fadeElapsed / fadeInDuration);
                for (int i = 0; i < sourceDocs.Length; i++)
                {
                    if (sourceDocs[i] != null && sourceDocs[i].rootVisualElement != null)
                        sourceDocs[i].rootVisualElement.style.opacity = opVal;
                }
                yield return null;
            }
        }
        HideActiveUIDocuments();

        // 2. Pause gameplay time and load + activate the new scene behind the opaque loading screen
        Time.timeScale = 0f;
        hiddenUIDocuments.Clear();

        float elapsed = 0f;
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[LoadingScreen] LoadSceneAsync returned null for '{sceneName}'");
            Time.timeScale = 1f;
            RestoreHiddenUIDocuments();
            SetScreenActive(false);
            isLoading = false;
            yield break;
        }

        while (!op.isDone)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.Log($"[LoadingScreen] Scene '{sceneName}' loaded");

        // Let Start() and AspectRatioEnforcer settle while still behind the opaque loading screen
        HideActiveUIDocuments();
        AspectRatioEnforcer.RefreshNow();
        yield return null;
        HideActiveUIDocuments();
        yield return null;
        HideActiveUIDocuments();

        // 3. Finish any remaining minimum display duration while the level is already loaded behind it
        while (elapsed < displayDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 4. Attach LoadingCamera as a URP Overlay on the new scene's Main Camera, unpause, and fade out
        AspectRatioEnforcer.RefreshNow();
        Camera targetCam = Camera.main;
        bool attachedToTarget = AttachAsOverlay(targetCam);

        Time.timeScale = 1f;

        if (attachedToTarget)
        {
            yield return FadeVisuals(1f, 0f, fadeOutDuration);
            DetachFromOverlay(targetCam);
            SetAsSolidBaseCamera();
        }

        RestoreHiddenUIDocuments();
        SetScreenActive(false);
        isLoading = false;
        Debug.Log($"[LoadingScreen] Transition complete for '{sceneName}'");
    }

    private bool AttachAsOverlay(Camera baseCam)
    {
        if (loadingCamera == null || baseCam == null || baseCam == loadingCamera)
            return false;

        var baseData = baseCam.GetUniversalAdditionalCameraData();
        var loadingData = loadingCamera.GetUniversalAdditionalCameraData();
        if (baseData == null || loadingData == null)
            return false;

        loadingData.renderType = CameraRenderType.Overlay;
        if (!baseData.cameraStack.Contains(loadingCamera))
        {
            baseData.cameraStack.Add(loadingCamera);
        }
        return true;
    }

    private void DetachFromOverlay(Camera baseCam)
    {
        if (loadingCamera == null || baseCam == null)
            return;

        var baseData = baseCam.GetUniversalAdditionalCameraData();
        if (baseData != null)
        {
            baseData.cameraStack.Remove(loadingCamera);
        }
    }

    private void SetAsSolidBaseCamera()
    {
        if (loadingCamera == null) return;

        var loadingData = loadingCamera.GetUniversalAdditionalCameraData();
        if (loadingData != null)
        {
            loadingData.renderType = CameraRenderType.Base;
        }
        loadingCamera.clearFlags = CameraClearFlags.SolidColor;
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
        if (fadeOverlay != null) fadeOverlay.gameObject.SetActive(false);
        if (active) AspectRatioEnforcer.RefreshNow();
    }

    private void CacheVisualAlphas()
    {
        cachedSprites.Clear();
        cachedTexts.Clear();
        if (visualsRoot == null) return;

        SpriteRenderer[] sprites = visualsRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            cachedSprites.Add((sprites[i], sprites[i].color.a));
        }

        TMP_Text[] texts = visualsRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            cachedTexts.Add((texts[i], texts[i].color.a));
        }
    }

    private IEnumerator FadeVisuals(float fromAlpha, float toAlpha, float duration)
    {
        if (duration <= 0.001f)
        {
            SetVisualsAlpha(toAlpha);
            yield break;
        }

        float elapsed = 0f;
        SetVisualsAlpha(fromAlpha);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetVisualsAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }

        SetVisualsAlpha(toAlpha);
    }

    private void SetVisualsAlpha(float multiplier)
    {
        for (int i = 0; i < cachedSprites.Count; i++)
        {
            var (sr, baseAlpha) = cachedSprites[i];
            if (sr == null) continue;
            Color c = sr.color;
            c.a = baseAlpha * multiplier;
            sr.color = c;
        }

        for (int i = 0; i < cachedTexts.Count; i++)
        {
            var (tmp, baseAlpha) = cachedTexts[i];
            if (tmp == null) continue;
            Color c = tmp.color;
            c.a = baseAlpha * multiplier;
            tmp.color = c;
        }
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
