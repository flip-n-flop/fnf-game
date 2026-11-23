using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Enforces a strict 16:9 viewport across all cameras and UI Toolkit <see cref="UIDocument"/> roots,
/// adding solid black letterbox/pillarbox bars on non-16:9 viewports (including WebGL / Unity Play resizes and fullscreen toggles).
/// Automatically initializes before the first scene loads and persists across scene transitions.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class AspectRatioEnforcer : MonoBehaviour
{
    public const float TargetAspect = 16f / 9f;

    public static AspectRatioEnforcer Instance { get; private set; }

    /// <summary>
    /// Normalized screen-space viewport rectangle (bottom-left origin, matching <see cref="Camera.rect"/>)
    /// corresponding to the active 16:9 region.
    /// </summary>
    public static Rect CurrentViewportRect { get; private set; } = new Rect(0f, 0f, 1f, 1f);

    /// <summary>
    /// Invoked whenever the 16:9 viewport rectangle changes (e.g. WebGL window resize or fullscreen toggle).
    /// </summary>
    public static event Action<Rect> OnViewportChanged;

    private Camera backdropCamera;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private int lastCameraCount = -1;
    private int uiRefreshCounter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject go = new GameObject(nameof(AspectRatioEnforcer));
        go.AddComponent<AspectRatioEnforcer>();
    }

    /// <summary>
    /// Immediately reapplies the 16:9 viewport to all active cameras and <see cref="UIDocument"/> instances.
    /// </summary>
    public static void RefreshNow()
    {
        if (Instance != null)
        {
            Instance.ApplyAll(force: true);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateBackdropCamera();
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyAll(force: true);
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
        ApplyAll(force: true);
    }

    private void LateUpdate()
    {
        bool resolutionChanged = Screen.width != lastScreenWidth || Screen.height != lastScreenHeight;
        bool cameraCountChanged = Camera.allCamerasCount != lastCameraCount;

        if (resolutionChanged || cameraCountChanged)
        {
            ApplyAll(force: resolutionChanged);
            uiRefreshCounter = 0;
            return;
        }

        // Periodic lightweight safeguard for dynamically enabled/instantiated UIDocuments (e.g. in WebGL)
        uiRefreshCounter++;
        if (uiRefreshCounter >= 30)
        {
            uiRefreshCounter = 0;
            ApplyToAllUIDocuments();
        }
    }

    private void CreateBackdropCamera()
    {
        GameObject camObj = new GameObject("LetterboxBackdropCamera");
        camObj.transform.SetParent(transform, false);

        backdropCamera = camObj.AddComponent<Camera>();
        backdropCamera.clearFlags = CameraClearFlags.SolidColor;
        backdropCamera.backgroundColor = Color.black;
        backdropCamera.cullingMask = 0;
        backdropCamera.depth = -1000f;
        backdropCamera.orthographic = true;
        backdropCamera.rect = new Rect(0f, 0f, 1f, 1f);
        backdropCamera.useOcclusionCulling = false;
        backdropCamera.allowHDR = false;
        backdropCamera.allowMSAA = false;
    }

    private void ApplyAll(bool force)
    {
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect newRect = Compute16By9Rect(Screen.width, Screen.height);
        bool rectChanged = force || !RectApproximately(newRect, CurrentViewportRect);

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastCameraCount = Camera.allCamerasCount;
        CurrentViewportRect = newRect;

        ApplyToAllCameras();
        ApplyToAllUIDocuments();

        if (rectChanged)
        {
            OnViewportChanged?.Invoke(CurrentViewportRect);
        }
    }

    private void ApplyToAllCameras()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam == null || cam == backdropCamera) continue;
            if (cam.targetTexture != null) continue;

            if (!RectApproximately(cam.rect, CurrentViewportRect))
            {
                cam.rect = CurrentViewportRect;
            }
        }
    }

    private void ApplyToAllUIDocuments()
    {
        UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        float leftPct = CurrentViewportRect.x * 100f;
        float topPct = (1f - CurrentViewportRect.y - CurrentViewportRect.height) * 100f;
        float widthPct = CurrentViewportRect.width * 100f;
        float heightPct = CurrentViewportRect.height * 100f;

        for (int i = 0; i < docs.Length; i++)
        {
            UIDocument doc = docs[i];
            if (doc == null) continue;

            VisualElement root = doc.rootVisualElement;
            if (root == null) continue;

            root.style.position = Position.Absolute;
            root.style.left = Length.Percent(leftPct);
            root.style.top = Length.Percent(topPct);
            root.style.width = Length.Percent(widthPct);
            root.style.height = Length.Percent(heightPct);
            root.style.overflow = Overflow.Hidden;
        }
    }

    private static Rect Compute16By9Rect(int width, int height)
    {
        float windowAspect = (float)width / height;

        if (Mathf.Abs(windowAspect - TargetAspect) < 0.0005f)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        if (windowAspect > TargetAspect)
        {
            // Wider than 16:9 -> pillarbox (black bars left & right)
            float scaleWidth = TargetAspect / windowAspect;
            float x = (1f - scaleWidth) * 0.5f;
            return new Rect(x, 0f, scaleWidth, 1f);
        }
        else
        {
            // Taller than 16:9 -> letterbox (black bars top & bottom)
            float scaleHeight = windowAspect / TargetAspect;
            float y = (1f - scaleHeight) * 0.5f;
            return new Rect(0f, y, 1f, scaleHeight);
        }
    }

    private static bool RectApproximately(Rect a, Rect b)
    {
        return Mathf.Abs(a.x - b.x) < 0.0001f &&
               Mathf.Abs(a.y - b.y) < 0.0001f &&
               Mathf.Abs(a.width - b.width) < 0.0001f &&
               Mathf.Abs(a.height - b.height) < 0.0001f;
    }
}
