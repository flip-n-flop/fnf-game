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

    [Header("Bright Dynamic Positioning")]
    [Tooltip("RectTransform of the LabelBright highlight sprite.")]
    [SerializeField] private RectTransform brightRect;

    [Tooltip("Local Y anchored position for LabelBright when the signal is High (1).")]
    [SerializeField] private float brightHighY = 0.0f;

    [Tooltip("Local Y anchored position for LabelBright when the signal is Low (0).")]
    [SerializeField] private float brightLowY = -0.98f;

    [Tooltip("Speed of vertical transition when signal logic state flips.")]
    [SerializeField] private float transitionSpeed = 15f;

    #endregion

    private float targetBrightY = 0.0f;

    #region Unity Lifecycle

    private void Awake()
    {
        EnsureBrightReference();
    }

    private void Update()
    {
        if (brightRect == null) return;

        Vector2 pos = brightRect.anchoredPosition;
        if (!Mathf.Approximately(pos.y, targetBrightY))
        {
            pos.y = Mathf.MoveTowards(pos.y, targetBrightY, transitionSpeed * Time.deltaTime);
            brightRect.anchoredPosition = pos;
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
    /// Applies the channel signal color to all bound <see cref="colorTintedRenderers"/>,
    /// preserving each renderer's configured alpha transparency.
    /// </summary>
    public void ApplyColor(Color signalColor)
    {
        if (colorTintedRenderers == null || colorTintedRenderers.Length == 0)
        {
            Transform bright = transform.Find("White_Box/LabelBright");
            if (bright != null)
            {
                var sr = bright.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    colorTintedRenderers = new[] { sr };
                }
            }
        }

        if (colorTintedRenderers == null) return;

        for (int i = 0; i < colorTintedRenderers.Length; i++)
        {
            if (colorTintedRenderers[i] != null)
            {
                Color current = colorTintedRenderers[i].color;
                float alpha = current.a > 0.001f ? current.a : signalColor.a;
                colorTintedRenderers[i].color = new Color(signalColor.r, signalColor.g, signalColor.b, alpha);
            }
        }
    }

    /// <summary>
    /// Sets whether the input signal entering/exiting the badge is currently in High (1) or Low (0) logic state,
    /// shifting LabelBright vertically to follow the line.
    /// </summary>
    /// <param name="isHigh">True if signal is High (1), false if Low (0).</param>
    /// <param name="instant">If true, immediately snaps without smooth transition.</param>
    public void SetSignalState(bool isHigh, bool instant = false)
    {
        EnsureBrightReference();
        targetBrightY = isHigh ? brightHighY : brightLowY;

        if (brightRect == null) return;

        if (instant || !Application.isPlaying)
        {
            Vector2 pos = brightRect.anchoredPosition;
            pos.y = targetBrightY;
            brightRect.anchoredPosition = pos;
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

    private void EnsureBrightReference()
    {
        if (brightRect == null)
        {
            Transform bright = transform.Find("White_Box/LabelBright");
            if (bright != null)
            {
                brightRect = bright.GetComponent<RectTransform>();
            }
        }
    }

    #endregion
}
