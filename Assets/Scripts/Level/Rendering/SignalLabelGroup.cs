using TMPro;
using UnityEngine;

/// <summary>
/// Represents a world-space signal badge hierarchy (e.g., J, K, PRE, CLR, CLK) in the scenario.
/// Exposes simple bindings for the label text, active-low overline bar, and color-tinted sprite layers.
/// Managed and positioned by <see cref="SignalLineRenderer"/>.
/// </summary>
public class SignalLabelGroup : MonoBehaviour
{
    #region Serialized Fields

    [Header("Hierarchy Bindings")]
    [Tooltip("TextMeshPro component displaying the signal name (e.g., J, K, D, PRE, CLR, CLK).")]
    [SerializeField] private TMP_Text labelText;

    [Tooltip("Child GameObject representing the active-low negation bar (overline) above PRE/CLR.")]
    [SerializeField] private GameObject overlineObject;

    [Tooltip("Child SpriteRenderers (e.g., ColorDetail) that receive the channel signal color.")]
    [SerializeField] private SpriteRenderer[] colorTintedRenderers;

    [Header("Fine Tuning")]
    [Tooltip("Optional local positional offset specific to this badge.")]
    [SerializeField] private Vector2 individualOffset = Vector2.zero;

    [Header("Bright & Holo Dynamic State")]
    [Tooltip("RectTransform of the LabelBright highlight sprite.")]
    [SerializeField] private RectTransform brightRect;

    [Tooltip("Local Y anchored position for LabelBright when the signal is High (1).")]
    [SerializeField] private float brightHighY = 0.0f;

    [Tooltip("Local Y anchored position for LabelBright when the signal is Low (0).")]
    [SerializeField] private float brightLowY = -0.98f;

    [Tooltip("Horizontal X offset (in world units) relative to the line cutoff where LabelBright checks if the signal line is High (1) or Low (0).")]
    [SerializeField] private float brightLineXOffset = 1.25f;

    [Tooltip("Smoothing time (in seconds) for LabelBright to smoothly slide between High and Low positions.")]
    [SerializeField] private float brightSmoothTime = 0.3f;

    [Tooltip("SpriteRenderer of the LabelHolo holographic layer inside Gray_Box.")]
    [SerializeField] private SpriteRenderer holoRenderer;

    [Tooltip("Alpha of LabelHolo when the signal at the spider's position is active.")]
    [SerializeField] private float holoHighAlpha = 0.8f;

    [Tooltip("Smoothing time (in seconds) for LabelHolo to fade in and fade out.")]
    [SerializeField] private float holoSmoothTime = 0.12f;

    [Tooltip("Speed of vertical and alpha transition when signal logic state flips.")]
    [SerializeField] private float transitionSpeed = 15f;

    #endregion

    private float targetBrightY = 0.0f;
    private float brightVelocityY = 0.0f;
    private float targetHoloAlpha = 0.0f;
    private float holoAlphaVelocity = 0.0f;
    private static Material sharedFallbackHoloMat;

    /// <summary>
    /// Returns true when the active-low negation bar (Overline) above the label text is active.
    /// </summary>
    public bool IsInverted => overlineObject != null && overlineObject.activeSelf;

    /// <summary>
    /// Horizontal X offset (in world units) relative to the badge line cutoff where <c>LabelBright</c> evaluates the signal line state.
    /// </summary>
    public float BrightLineXOffset => brightLineXOffset;

    #region Unity Lifecycle

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnValidate()
    {
        EnsureReferences();
    }

    private void Update()
    {
        if (brightRect != null)
        {
            Vector2 pos = brightRect.anchoredPosition;
            if (Mathf.Abs(pos.y - targetBrightY) > 0.0005f)
            {
                float smoothTime = Mathf.Max(0.01f, brightSmoothTime);
                pos.y = Mathf.SmoothDamp(pos.y, targetBrightY, ref brightVelocityY, smoothTime, Mathf.Infinity, Time.deltaTime);
                brightRect.anchoredPosition = pos;
            }
            else if (pos.y != targetBrightY)
            {
                pos.y = targetBrightY;
                brightVelocityY = 0f;
                brightRect.anchoredPosition = pos;
            }
        }

        if (holoRenderer != null)
        {
            Color c = holoRenderer.color;
            if (Mathf.Abs(c.a - targetHoloAlpha) > 0.001f)
            {
                float smoothTime = Mathf.Max(0.01f, holoSmoothTime);
                c.a = Mathf.SmoothDamp(c.a, targetHoloAlpha, ref holoAlphaVelocity, smoothTime, Mathf.Infinity, Time.deltaTime);
                holoRenderer.color = c;
                holoRenderer.enabled = c.a > 0.001f;
            }
            else if (c.a != targetHoloAlpha)
            {
                c.a = targetHoloAlpha;
                holoAlphaVelocity = 0f;
                holoRenderer.color = c;
                holoRenderer.enabled = c.a > 0.001f;
            }
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Activates, scales, positions vertically on its channel, and updates the text, overline, and color of the badge.
    /// Called by <see cref="SignalLineRenderer"/> during level rendering.
    /// </summary>
    public void Configure(string text, Color signalColor, bool showOverline, float channelCenterY, float scale = 1f)
    {
        EnsureReferences();
        gameObject.SetActive(true);
        SetChannelY(channelCenterY, scale);

        if (labelText != null && !string.IsNullOrEmpty(text))
        {
            labelText.text = text;
        }

        if (overlineObject != null)
        {
            overlineObject.SetActive(showOverline);
        }

        ApplyColor(signalColor);
    }

    /// <summary>
    /// Updates the local Y position and uniform scale of the badge inside the camera-following <c>Labels</c> container.
    /// </summary>
    public void SetChannelY(float channelCenterY, float scale = 1f)
    {
        transform.localScale = new Vector3(scale, scale, 1f);
        transform.localPosition = new Vector3(
            individualOffset.x,
            channelCenterY + individualOffset.y,
            0f
        );
    }

    /// <summary>
    /// Applies the channel signal color to all bound <see cref="colorTintedRenderers"/> and <see cref="holoRenderer"/>,
    /// preserving each renderer's configured alpha transparency.
    /// </summary>
    public void ApplyColor(Color signalColor)
    {
        EnsureReferences();

        if (colorTintedRenderers == null || colorTintedRenderers.Length == 0)
        {
            Transform bright = transform.Find("White_Box/LabelBright");
            if (bright != null && bright.TryGetComponent(out SpriteRenderer brightSr))
            {
                colorTintedRenderers = new[] { brightSr };
            }
        }

        if (colorTintedRenderers != null)
        {
            for (int i = 0; i < colorTintedRenderers.Length; i++)
            {
                SpriteRenderer sr = colorTintedRenderers[i];
                if (sr != null && sr != holoRenderer)
                {
                    Color current = sr.color;
                    float alpha = current.a > 0.001f ? current.a : signalColor.a;
                    sr.color = new Color(signalColor.r, signalColor.g, signalColor.b, alpha);
                }
            }
        }

        if (holoRenderer != null)
        {
            float currentAlpha = Application.isPlaying ? holoRenderer.color.a : targetHoloAlpha;
            holoRenderer.color = new Color(signalColor.r, signalColor.g, signalColor.b, currentAlpha);
        }
    }

    /// <summary>
    /// Sets the vertical position of <c>LabelBright</c> based on whether the signal line at the badge's X cutoff is High (1) or Low (0).
    /// </summary>
    /// <param name="isLineHighAtBadge">True if the waveform at the badge cutoff is High (top), false if Low (bottom).</param>
    /// <param name="instant">If true, immediately snaps without smooth transition.</param>
    public void SetSignalState(bool isLineHighAtBadge, bool instant = false)
    {
        EnsureReferences();
        targetBrightY = isLineHighAtBadge ? brightHighY : brightLowY;

        if (brightRect == null) return;

        if (instant || !Application.isPlaying)
        {
            brightVelocityY = 0f;
            Vector2 pos = brightRect.anchoredPosition;
            pos.y = targetBrightY;
            brightRect.anchoredPosition = pos;
        }
    }

    /// <summary>
    /// Updates the visibility of <c>LabelHolo</c> based on the signal state at the spider's X position.
    /// For normal signals, <c>LabelHolo</c> is active when the line is High (top).
    /// For inverted signals (when the <c>Overline</c> bar above the text is active), <c>LabelHolo</c> is active when the line is Low (bottom).
    /// </summary>
    /// <param name="isLineHighAtSpider">True if the waveform at the spider's X position is High (top), false if Low (bottom).</param>
    /// <param name="instant">If true, immediately snaps alpha without smooth transition.</param>
    public void SetHoloState(bool isLineHighAtSpider, bool instant = false)
    {
        EnsureReferences();

        bool isSignalActive = IsInverted ? !isLineHighAtSpider : isLineHighAtSpider;
        targetHoloAlpha = isSignalActive ? holoHighAlpha : 0f;

        if (holoRenderer == null) return;

        if (instant || !Application.isPlaying)
        {
            holoAlphaVelocity = 0f;
            Color c = holoRenderer.color;
            c.a = targetHoloAlpha;
            holoRenderer.color = c;
            holoRenderer.enabled = targetHoloAlpha > 0.001f;
        }
        else if (isSignalActive && !holoRenderer.enabled)
        {
            holoRenderer.enabled = true;
        }
    }

    /// <summary>
    /// Deactivates this badge when its corresponding signal channel is not present in the level.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    #endregion

    #region Private Helpers

    private void EnsureReferences()
    {
        SyncOverlineHierarchy();

        if (brightRect == null)
        {
            Transform bright = transform.Find("White_Box/LabelBright");
            if (bright != null)
            {
                brightRect = bright.GetComponent<RectTransform>();
            }
        }

        if (holoRenderer == null)
        {
            Transform grayBox = transform.Find("Gray_Box");
            if (grayBox != null)
            {
                Transform holo = grayBox.Find("LabelHolo");
                if (holo != null)
                {
                    holoRenderer = holo.GetComponent<SpriteRenderer>();
                }
                else if (Application.isPlaying)
                {
                    GameObject holoObj = new GameObject("LabelHolo", typeof(RectTransform), typeof(SpriteRenderer));
                    RectTransform rt = holoObj.GetComponent<RectTransform>();
                    rt.SetParent(grayBox, false);
                    rt.localPosition = Vector3.zero;
                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one;

                    holoRenderer = holoObj.GetComponent<SpriteRenderer>();
                    if (grayBox.TryGetComponent(out SpriteRenderer graySr))
                    {
                        holoRenderer.sprite = graySr.sprite;
                        rt.sizeDelta = graySr.size;
                    }

                    Shader holoShader = Shader.Find("Custom/Sprite_Holographic");
                    if (holoShader != null)
                    {
                        if (sharedFallbackHoloMat == null)
                        {
                            sharedFallbackHoloMat = new Material(holoShader);
                        }
                        holoRenderer.sharedMaterial = sharedFallbackHoloMat;
                    }
                }
            }
        }

        if (holoRenderer != null)
        {
            holoRenderer.maskInteraction = SpriteMaskInteraction.None;
            holoRenderer.sortingOrder = 7;
        }
    }

    private void SyncOverlineHierarchy()
    {
        if (labelText == null)
        {
            labelText = GetComponentInChildren<TMP_Text>(true);
        }

        bool changed = false;

        if (labelText != null)
        {
            if (labelText.TryGetComponent(out MeshRenderer textMr) && textMr.sortingOrder < 8)
            {
                textMr.sortingOrder = 8;
                changed = true;
            }

            Transform textParent = labelText.transform.parent;
            if (textParent != null && textParent != transform)
            {
                if (textParent.name != "TextGroup")
                {
                    textParent.name = "TextGroup";
                    changed = true;
                }

                if (overlineObject == null)
                {
                    for (int i = 0; i < textParent.childCount; i++)
                    {
                        Transform sibling = textParent.GetChild(i);
                        if (sibling != labelText.transform && sibling.childCount > 0)
                        {
                            overlineObject = sibling.gameObject;
                            changed = true;
                            break;
                        }
                    }
                }
            }
        }

        if (overlineObject != null && overlineObject.TryGetComponent<SpriteRenderer>(out _))
        {
            Transform parent = overlineObject.transform.parent;
            if (parent != null && parent != transform)
            {
                overlineObject = parent.gameObject;
                changed = true;
            }
        }

        if (overlineObject != null && overlineObject.transform.childCount > 0)
        {
            if (overlineObject.name != "OverlineGroup")
            {
                overlineObject.name = "OverlineGroup";
                changed = true;
            }

            Transform groupTransform = overlineObject.transform;
            for (int i = 0; i < groupTransform.childCount; i++)
            {
                Transform child = groupTransform.GetChild(i);
                if (!child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(true);
                    changed = true;
                }

                if (child.TryGetComponent(out SpriteRenderer sr))
                {
                    string targetName;
                    int targetOrder;

                    if (sr.color.r > 0.5f)
                    {
                        targetName = "OverlineFill";
                        targetOrder = 10;
                    }
                    else if (child.localScale.y > 0.19f)
                    {
                        targetName = "OverlineOutline";
                        targetOrder = 9;
                    }
                    else
                    {
                        targetName = "OverlineShadow";
                        targetOrder = 8;
                    }

                    if (child.name != targetName)
                    {
                        child.name = targetName;
                        changed = true;
                    }

                    if (sr.sortingOrder != targetOrder)
                    {
                        sr.sortingOrder = targetOrder;
                        changed = true;
                    }
                }
            }
        }

#if UNITY_EDITOR
        if (changed && !Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
#endif
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void EditorAutoSyncOverlineHierarchy()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying) return;

            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot != null)
            {
                if (prefabStage.prefabContentsRoot.TryGetComponent(out SignalLabelGroup stageGroup))
                {
                    stageGroup.EnsureReferences();
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                }
            }

            SignalLabelGroup prefabAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<SignalLabelGroup>("Assets/Prefab/InputLabel.prefab");
            if (prefabAsset != null)
            {
                prefabAsset.EnsureReferences();
            }

            SignalLabelGroup[] sceneGroups = FindObjectsByType<SignalLabelGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (SignalLabelGroup group in sceneGroups)
            {
                if (group == null) continue;
                group.EnsureReferences();
                if (group.overlineObject != null && group.labelText != null)
                {
                    string txt = group.labelText.text != null ? group.labelText.text.Trim().ToUpperInvariant() : "";
                    bool shouldShow = txt == "PRE" || txt == "CLR";
                    if (group.overlineObject.activeSelf != shouldShow)
                    {
                        group.overlineObject.SetActive(shouldShow);
                        UnityEditor.EditorUtility.SetDirty(group.overlineObject);
                    }
                }
            }
        };
    }
#endif

    #endregion
}
