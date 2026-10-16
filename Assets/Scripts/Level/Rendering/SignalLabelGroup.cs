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

    #endregion

    #region Public API

    /// <summary>
    /// Activates, positions vertically on its channel, and updates the text, overline, and color of the badge.
    /// Called by <see cref="SignalLineRenderer"/> during level rendering.
    /// </summary>
    public void Configure(string text, Color signalColor, bool showOverline, float channelCenterY)
    {
        gameObject.SetActive(true);
        SetChannelY(channelCenterY);

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
    /// Updates the local Y position of the badge inside the camera-following <c>Labels</c> container.
    /// </summary>
    public void SetChannelY(float channelCenterY)
    {
        transform.localPosition = new Vector3(
            individualOffset.x,
            channelCenterY + individualOffset.y,
            0f
        );
    }

    /// <summary>
    /// Applies the channel signal color to all bound <see cref="colorTintedRenderers"/>.
    /// </summary>
    public void ApplyColor(Color signalColor)
    {
        if (colorTintedRenderers == null) return;

        for (int i = 0; i < colorTintedRenderers.Length; i++)
        {
            if (colorTintedRenderers[i] != null)
            {
                colorTintedRenderers[i].color = signalColor;
            }
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
}
