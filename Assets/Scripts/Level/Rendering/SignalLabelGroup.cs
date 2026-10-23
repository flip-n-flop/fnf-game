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

    [Tooltip("SpriteRenderer of the LabelHolo holographic layer inside Gray_Box.")]
    [SerializeField] private SpriteRenderer holoRenderer;

    [Tooltip("Alpha of LabelHolo when the signal at the spider's position is active.")]
    [SerializeField] private float holoHighAlpha = 0.8f;

    [Tooltip("Speed of vertical and alpha transition when signal logic state flips.")]
    [SerializeField] private float transitionSpeed = 15f;

    #endregion

    private float targetBrightY = 0.0f;
    private float targetHoloAlpha = 0.0f;
    private static Material sharedFallbackHoloMat;

    /// <summary>
    /// Returns true when the active-low negation bar (Overline) above the label text is active.
    /// </summary>
    public bool IsInverted => overlineObject != null && overlineObject.activeSelf;

    #region Unity Lifecycle

    private void Awake()
    {
        EnsureReferences();
    }

    private void Update()
    {
        if (brightRect != null)
        {
            Vector2 pos = brightRect.anchoredPosition;
            if (!Mathf.Approximately(pos.y, targetBrightY))
            {
                pos.y = Mathf.MoveTowards(pos.y, targetBrightY, transitionSpeed * Time.deltaTime);
                brightRect.anchoredPosition = pos;
            }
        }

        if (holoRenderer != null)
        {
            Color c = holoRenderer.color;
            if (!Mathf.Approximately(c.a, targetHoloAlpha))
            {
                c.a = Mathf.MoveTowards(c.a, targetHoloAlpha, transitionSpeed * Time.deltaTime);
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

    #endregion
}
