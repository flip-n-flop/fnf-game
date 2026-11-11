using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Encapsulates all logic for the "Colors" tab in the settings menu, including preset palettes, swatch pickers, and the custom RGB overlay.
/// Interacts with <see cref="SignalColorManager"/> for signal color persistence and synchronization.
/// </summary>
public class ColorSettingsTab : ISettingsTab
{
    #region Enums

    /// <summary>
    /// Identifies the signal or feedback element type being configured for custom color selection.
    /// </summary>
    private enum SignalType
    {
        /// <summary>J input signal.</summary>
        J,

        /// <summary>K input signal.</summary>
        K,

        /// <summary>Clock (CLK) input signal.</summary>
        CLK,

        /// <summary>Preset input signal.</summary>
        Preset,

        /// <summary>Clear input signal.</summary>
        Clear,

        /// <summary>Successful feedback signal visual indicator.</summary>
        FeedbackSuccess,

        /// <summary>Failure feedback signal visual indicator.</summary>
        FeedbackFailure
    }

    #endregion

    #region Private Fields - Signal UI Elements

    /// <summary>
    /// UI Toolkit Dropdown field for selecting preset color palettes.
    /// </summary>
    private DropdownField dropdownPaleta;

    /// <summary>
    /// UI Toolkit Button for resetting all signal colors to default palette.
    /// </summary>
    private Button btnResetColors;

    /// <summary>
    /// Flag indicating if a preset palette application is currently in progress to avoid recursive UI callback loops.
    /// </summary>
    private bool isApplyingPreset = false;

    /// <summary>Preview visual element for J signal color.</summary>
    private VisualElement previewJ;

    /// <summary>Preview visual element for K signal color.</summary>
    private VisualElement previewK;

    /// <summary>Preview visual element for CLK signal color.</summary>
    private VisualElement previewCLK;

    /// <summary>Preview visual element for Preset signal color.</summary>
    private VisualElement previewPreset;

    /// <summary>Preview visual element for Clear signal color.</summary>
    private VisualElement previewClear;

    /// <summary>Preview visual element for successful feedback color.</summary>
    private VisualElement previewFeedbackSuccess;

    /// <summary>Preview visual element for failed feedback color.</summary>
    private VisualElement previewFeedbackFailure;

    /// <summary>Container element holding preset color swatch buttons for J signal.</summary>
    private VisualElement containerSwatchesJ;

    /// <summary>Container element holding preset color swatch buttons for K signal.</summary>
    private VisualElement containerSwatchesK;

    /// <summary>Container element holding preset color swatch buttons for CLK signal.</summary>
    private VisualElement containerSwatchesCLK;

    /// <summary>Container element holding preset color swatch buttons for Preset signal.</summary>
    private VisualElement containerSwatchesPreset;

    /// <summary>Container element holding preset color swatch buttons for Clear signal.</summary>
    private VisualElement containerSwatchesClear;

    /// <summary>Container element holding preset color swatch buttons for Feedback Success.</summary>
    private VisualElement containerSwatchesFeedbackSuccess;

    /// <summary>Container element holding preset color swatch buttons for Feedback Failure.</summary>
    private VisualElement containerSwatchesFeedbackFailure;

    /// <summary>Button to open custom RGB overlay for J signal.</summary>
    private Button btnCustomJ;

    /// <summary>Button to open custom RGB overlay for K signal.</summary>
    private Button btnCustomK;

    /// <summary>Button to open custom RGB overlay for CLK signal.</summary>
    private Button btnCustomCLK;

    /// <summary>Button to open custom RGB overlay for Preset signal.</summary>
    private Button btnCustomPreset;

    /// <summary>Button to open custom RGB overlay for Clear signal.</summary>
    private Button btnCustomClear;

    /// <summary>Button to open custom RGB overlay for Feedback Success.</summary>
    private Button btnCustomFeedbackSuccess;

    /// <summary>Button to open custom RGB overlay for Feedback Failure.</summary>
    private Button btnCustomFeedbackFailure;

    #endregion

    #region Private Fields - RGB Overlay Elements

    /// <summary>Modal overlay visual element for custom RGB color editing.</summary>
    private VisualElement rgbOverlay;

    /// <summary>Header title label inside the RGB overlay.</summary>
    private Label rgbTitle;

    /// <summary>Color preview block inside the RGB overlay.</summary>
    private VisualElement rgbPreview;

    /// <summary>Text input field for entering color values in hexadecimal format.</summary>
    private TextField inputHex;

    /// <summary>Red channel slider in the RGB overlay.</summary>
    private Slider sliderR;

    /// <summary>Green channel slider in the RGB overlay.</summary>
    private Slider sliderG;

    /// <summary>Blue channel slider in the RGB overlay.</summary>
    private Slider sliderB;

    /// <summary>Confirm button in the RGB overlay.</summary>
    private Button btnConfirmRGB;

    /// <summary>Cancel button in the RGB overlay.</summary>
    private Button btnCancelRGB;

    /// <summary>Tracks which signal type is currently being modified in the RGB overlay.</summary>
    private SignalType activeCustomSignal = SignalType.J;

    #endregion

    #region Private Fields - Swatch Lists

    /// <summary>List of swatch buttons generated for J signal presets.</summary>
    private readonly List<Button> swatchesJ = new();

    /// <summary>List of swatch buttons generated for K signal presets.</summary>
    private readonly List<Button> swatchesK = new();

    /// <summary>List of swatch buttons generated for CLK signal presets.</summary>
    private readonly List<Button> swatchesCLK = new();

    /// <summary>List of swatch buttons generated for Preset signal presets.</summary>
    private readonly List<Button> swatchesPreset = new();

    /// <summary>List of swatch buttons generated for Clear signal presets.</summary>
    private readonly List<Button> swatchesClear = new();

    /// <summary>List of swatch buttons generated for Feedback Success presets.</summary>
    private readonly List<Button> swatchesFeedbackSuccess = new();

    /// <summary>List of swatch buttons generated for Feedback Failure presets.</summary>
    private readonly List<Button> swatchesFeedbackFailure = new();

    #endregion

    #region ISettingsTab Implementation

    /// <summary>
    /// Caches all UI Toolkit elements from the root hierarchy and initializes color controls.
    /// Interacts with <see cref="SignalColorManager"/>.
    /// </summary>
    /// <param name="root">The root <see cref="VisualElement"/> container of the options menu.</param>
    public void Init(VisualElement root)
    {
        CacheElements(root);
        InitColorSettings();
    }

    /// <summary>
    /// Registers event callbacks for custom color buttons, RGB sliders, hex inputs, and reset buttons.
    /// </summary>
    public void RegisterCallbacks()
    {
        if (btnCustomJ != null) btnCustomJ.clicked += OpenCustomJ;
        if (btnCustomK != null) btnCustomK.clicked += OpenCustomK;
        if (btnCustomCLK != null) btnCustomCLK.clicked += OpenCustomCLK;
        if (btnCustomPreset != null) btnCustomPreset.clicked += OpenCustomPreset;
        if (btnCustomClear != null) btnCustomClear.clicked += OpenCustomClear;
        if (btnCustomFeedbackSuccess != null) btnCustomFeedbackSuccess.clicked += OpenCustomFeedbackSuccess;
        if (btnCustomFeedbackFailure != null) btnCustomFeedbackFailure.clicked += OpenCustomFeedbackFailure;

        if (btnConfirmRGB != null) btnConfirmRGB.clicked += ConfirmRGBColor;
        if (btnCancelRGB != null) btnCancelRGB.clicked += CloseRGBOverlay;

        if (btnResetColors != null) btnResetColors.clicked += ResetDefaultColors;

        sliderR?.RegisterValueChangedCallback(OnRGBSliderChanged);
        sliderG?.RegisterValueChangedCallback(OnRGBSliderChanged);
        sliderB?.RegisterValueChangedCallback(OnRGBSliderChanged);

        inputHex?.RegisterValueChangedCallback(OnHexInputChanged);
    }

    /// <summary>
    /// Unregisters event callbacks for custom color buttons, RGB sliders, hex inputs, and reset buttons to prevent memory leaks.
    /// </summary>
    public void UnregisterCallbacks()
    {
        if (btnCustomJ != null) btnCustomJ.clicked -= OpenCustomJ;
        if (btnCustomK != null) btnCustomK.clicked -= OpenCustomK;
        if (btnCustomCLK != null) btnCustomCLK.clicked -= OpenCustomCLK;
        if (btnCustomPreset != null) btnCustomPreset.clicked -= OpenCustomPreset;
        if (btnCustomClear != null) btnCustomClear.clicked -= OpenCustomClear;
        if (btnCustomFeedbackSuccess != null) btnCustomFeedbackSuccess.clicked -= OpenCustomFeedbackSuccess;
        if (btnCustomFeedbackFailure != null) btnCustomFeedbackFailure.clicked -= OpenCustomFeedbackFailure;

        if (btnConfirmRGB != null) btnConfirmRGB.clicked -= ConfirmRGBColor;
        if (btnCancelRGB != null) btnCancelRGB.clicked -= CloseRGBOverlay;

        if (btnResetColors != null) btnResetColors.clicked -= ResetDefaultColors;

        sliderR?.UnregisterValueChangedCallback(OnRGBSliderChanged);
        sliderG?.UnregisterValueChangedCallback(OnRGBSliderChanged);
        sliderB?.UnregisterValueChangedCallback(OnRGBSliderChanged);

        inputHex?.UnregisterValueChangedCallback(OnHexInputChanged);
    }

    /// <summary>
    /// Called when the active localization locale changes.
    /// Refreshes palette dropdown labels and synchronizes color UI state.
    /// Interacts with <see cref="SignalColorManager.GetLocalizedPaletteName(int)"/>.
    /// </summary>
    public void OnLocaleChanged()
    {
        RefreshPaletteDropdown();
        SyncColorUI();
    }

    #endregion

    #region UI Element Caching

    /// <summary>
    /// Queries and caches visual elements from the UI Toolkit root tree.
    /// </summary>
    /// <param name="root">The root visual element container.</param>
    private void CacheElements(VisualElement root)
    {
        dropdownPaleta = root.Q<DropdownField>("DropdownPaleta");
        btnResetColors = root.Q<Button>("BtnResetColors");

        previewJ = root.Q<VisualElement>("PreviewJ");
        previewK = root.Q<VisualElement>("PreviewK");
        previewCLK = root.Q<VisualElement>("PreviewCLK");
        previewPreset = root.Q<VisualElement>("PreviewPreset");
        previewClear = root.Q<VisualElement>("PreviewClear");
        previewFeedbackSuccess = root.Q<VisualElement>("PreviewFeedbackSuccess");
        previewFeedbackFailure = root.Q<VisualElement>("PreviewFeedbackFailure");

        containerSwatchesJ = root.Q<VisualElement>("ContainerSwatchesJ");
        containerSwatchesK = root.Q<VisualElement>("ContainerSwatchesK");
        containerSwatchesCLK = root.Q<VisualElement>("ContainerSwatchesCLK");
        containerSwatchesPreset = root.Q<VisualElement>("ContainerSwatchesPreset");
        containerSwatchesClear = root.Q<VisualElement>("ContainerSwatchesClear");
        containerSwatchesFeedbackSuccess = root.Q<VisualElement>("ContainerSwatchesFeedbackSuccess");
        containerSwatchesFeedbackFailure = root.Q<VisualElement>("ContainerSwatchesFeedbackFailure");

        btnCustomJ = root.Q<Button>("BtnCustomJ");
        btnCustomK = root.Q<Button>("BtnCustomK");
        btnCustomCLK = root.Q<Button>("BtnCustomCLK");
        btnCustomPreset = root.Q<Button>("BtnCustomPreset");
        btnCustomClear = root.Q<Button>("BtnCustomClear");
        btnCustomFeedbackSuccess = root.Q<Button>("BtnCustomFeedbackSuccess");
        btnCustomFeedbackFailure = root.Q<Button>("BtnCustomFeedbackFailure");

        rgbOverlay = root.Q<VisualElement>("RGBOverlay");
        rgbTitle = root.Q<Label>("RGBTitle");
        rgbPreview = root.Q<VisualElement>("RGBPreview");

        inputHex = root.Q<TextField>("InputHex");

        sliderR = root.Q<Slider>("SliderR");
        sliderG = root.Q<Slider>("SliderG");
        sliderB = root.Q<Slider>("SliderB");

        btnConfirmRGB = root.Q<Button>("BtnConfirmRGB");
        btnCancelRGB = root.Q<Button>("BtnCancelRGB");
    }

    #endregion

    #region Color Settings & Swatch Logic

    /// <summary>
    /// Initializes color dropdown options, generates preset swatches for all signals, and synchronizes the UI.
    /// Interacts with <see cref="SignalColorManager.Instance"/>.
    /// </summary>
    private void InitColorSettings()
    {
        if (dropdownPaleta != null)
        {
            RefreshPaletteDropdown();

            dropdownPaleta.RegisterValueChangedCallback(evt =>
            {
                string localizedCustomName = SignalColorManager.GetLocalizedPaletteName(SignalColorManager.CUSTOM_INDEX);

                if (evt.newValue == localizedCustomName || isApplyingPreset)
                    return;

                int selectedIndex = -1;
                for (int i = 0; i < SignalColorManager.Palettes.Length; i++)
                {
                    if (evt.newValue == SignalColorManager.GetLocalizedPaletteName(i))
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                if (selectedIndex >= 0)
                {
                    isApplyingPreset = true;

                    SignalColorManager.Instance.ApplyPalette(selectedIndex);
                    SyncColorUI();

                    dropdownPaleta.SetValueWithoutNotify(evt.newValue);

                    isApplyingPreset = false;
                }
            });
        }

        GenerateSwatches(containerSwatchesJ, swatchesJ, SignalType.J);
        GenerateSwatches(containerSwatchesK, swatchesK, SignalType.K);
        GenerateSwatches(containerSwatchesCLK, swatchesCLK, SignalType.CLK);
        GenerateSwatches(containerSwatchesPreset, swatchesPreset, SignalType.Preset);
        GenerateSwatches(containerSwatchesClear, swatchesClear, SignalType.Clear);
        GenerateSwatches(containerSwatchesFeedbackSuccess, swatchesFeedbackSuccess, SignalType.FeedbackSuccess);
        GenerateSwatches(containerSwatchesFeedbackFailure, swatchesFeedbackFailure, SignalType.FeedbackFailure);

        SyncColorUI();
    }

    /// <summary>
    /// Populates the palette dropdown menu with localized names for preset palettes and the custom option.
    /// Interacts with <see cref="SignalColorManager.GetLocalizedPaletteName(int)"/> and <see cref="SignalColorManager.Instance"/>.
    /// </summary>
    private void RefreshPaletteDropdown()
    {
        if (dropdownPaleta == null)
            return;

        string localizedCustomName = SignalColorManager.GetLocalizedPaletteName(SignalColorManager.CUSTOM_INDEX);

        List<string> choices = new List<string>();
        for (int i = 0; i < SignalColorManager.Palettes.Length; i++)
        {
            choices.Add(SignalColorManager.GetLocalizedPaletteName(i));
        }
        choices.Add(localizedCustomName);

        dropdownPaleta.choices = choices;

        int activePaletteIndex = SignalColorManager.Instance.GetCurrentPaletteIndex();
        if (activePaletteIndex >= 0)
        {
            dropdownPaleta.SetValueWithoutNotify(SignalColorManager.GetLocalizedPaletteName(activePaletteIndex));
        }
        else
        {
            dropdownPaleta.SetValueWithoutNotify(localizedCustomName);
        }
    }

    /// <summary>
    /// Dynamically creates swatch button elements for a signal container using colors from <see cref="SignalColorManager.PresetColors"/>.
    /// Interacts with <see cref="SignalColorManager.Instance"/>.
    /// </summary>
    /// <param name="container">Target <see cref="VisualElement"/> container to add swatches to.</param>
    /// <param name="swatches">List holding references to created swatch <see cref="Button"/> elements.</param>
    /// <param name="signalType">Signal type enum assigned to these swatches.</param>
    private void GenerateSwatches(
        VisualElement container,
        List<Button> swatches,
        SignalType signalType)
    {
        if (container == null)
            return;

        container.Clear();
        swatches.Clear();

        for (int i = 0; i < SignalColorManager.PresetColors.Length; i++)
        {
            int colorIndex = i;
            Color color = SignalColorManager.PresetColors[i];

            Button swatch = new();

            swatch.style.width = 30;
            swatch.style.height = 30;
            swatch.style.minWidth = 30;
            swatch.style.minHeight = 30;
            swatch.style.maxWidth = 30;
            swatch.style.maxHeight = 30;
            swatch.style.flexShrink = 0;

            swatch.style.paddingTop = 0;
            swatch.style.paddingRight = 0;
            swatch.style.paddingBottom = 0;
            swatch.style.paddingLeft = 0;

            swatch.style.marginRight = 5;
            swatch.style.marginBottom = 5;

            swatch.style.backgroundColor = color;

            swatch.style.borderTopWidth = 2;
            swatch.style.borderBottomWidth = 2;
            swatch.style.borderLeftWidth = 2;
            swatch.style.borderRightWidth = 2;

            swatch.clicked += () =>
            {
                SignalColorManager.Instance.SetAndNotify(signalType.ToString(), colorIndex);
                SyncColorUI();
            };

            container.Add(swatch);
            swatches.Add(swatch);
        }
    }

    /// <summary>
    /// Synchronizes all preview backgrounds, swatch selection borders, and dropdown selections with <see cref="SignalColorManager.Instance"/>.
    /// </summary>
    private void SyncColorUI()
    {
        previewJ.style.backgroundColor = SignalColorManager.Instance.ColorJ;
        previewK.style.backgroundColor = SignalColorManager.Instance.ColorK;
        previewCLK.style.backgroundColor = SignalColorManager.Instance.ColorCLK;
        previewPreset.style.backgroundColor = SignalColorManager.Instance.ColorPreset;
        previewClear.style.backgroundColor = SignalColorManager.Instance.ColorClear;
        previewFeedbackSuccess.style.backgroundColor = SignalColorManager.Instance.ColorFeedbackSuccess;
        previewFeedbackFailure.style.backgroundColor = SignalColorManager.Instance.ColorFeedbackFailure;

        UpdateSwatchBorders(swatchesJ, SignalColorManager.Instance.IndexJ);
        UpdateSwatchBorders(swatchesK, SignalColorManager.Instance.IndexK);
        UpdateSwatchBorders(swatchesCLK, SignalColorManager.Instance.IndexCLK);
        UpdateSwatchBorders(swatchesPreset, SignalColorManager.Instance.IndexPreset);
        UpdateSwatchBorders(swatchesClear, SignalColorManager.Instance.IndexClear);
        UpdateSwatchBorders(swatchesFeedbackSuccess, SignalColorManager.Instance.IndexFeedbackSuccess);
        UpdateSwatchBorders(swatchesFeedbackFailure, SignalColorManager.Instance.IndexFeedbackFailure);

        int activePaletteIndex = SignalColorManager.Instance.GetCurrentPaletteIndex();

        if (activePaletteIndex >= 0)
        {
            dropdownPaleta?.SetValueWithoutNotify(SignalColorManager.GetLocalizedPaletteName(activePaletteIndex));
        }
        else if (!isApplyingPreset)
        {
            dropdownPaleta?.SetValueWithoutNotify(SignalColorManager.GetLocalizedPaletteName(SignalColorManager.CUSTOM_INDEX));
        }
    }

    /// <summary>
    /// Updates border highlight colors for a list of swatch buttons to indicate the currently selected preset index.
    /// </summary>
    /// <param name="swatches">List of swatch <see cref="Button"/> elements.</param>
    /// <param name="selectedIndex">Currently selected preset index, or <see cref="SignalColorManager.CUSTOM_INDEX"/> (-1).</param>
    private void UpdateSwatchBorders(List<Button> swatches, int selectedIndex)
    {
        for (int i = 0; i < swatches.Count; i++)
        {
            Color borderColor = i == selectedIndex ? Color.white : Color.clear;

            swatches[i].style.borderTopColor = borderColor;
            swatches[i].style.borderBottomColor = borderColor;
            swatches[i].style.borderLeftColor = borderColor;
            swatches[i].style.borderRightColor = borderColor;
        }
    }

    /// <summary>
    /// Resets all signal colors to default palette 0 in <see cref="SignalColorManager.Instance"/> and saves changes using <see cref="PlayerPrefs"/>.
    /// </summary>
    private void ResetDefaultColors()
    {
        SignalColorManager.Instance.ApplyPalette(0);
        SyncColorUI();
        PlayerPrefs.Save();
    }

    #endregion

    #region RGB Overlay Logic

    /// <summary>Opens the RGB color editor overlay for the J signal.</summary>
    private void OpenCustomJ() => OpenRGBOverlay(SignalType.J);

    /// <summary>Opens the RGB color editor overlay for the K signal.</summary>
    private void OpenCustomK() => OpenRGBOverlay(SignalType.K);

    /// <summary>Opens the RGB color editor overlay for the CLK signal.</summary>
    private void OpenCustomCLK() => OpenRGBOverlay(SignalType.CLK);

    /// <summary>Opens the RGB color editor overlay for the Preset signal.</summary>
    private void OpenCustomPreset() => OpenRGBOverlay(SignalType.Preset);

    /// <summary>Opens the RGB color editor overlay for the Clear signal.</summary>
    private void OpenCustomClear() => OpenRGBOverlay(SignalType.Clear);

    /// <summary>Opens the RGB color editor overlay for successful feedback.</summary>
    private void OpenCustomFeedbackSuccess() => OpenRGBOverlay(SignalType.FeedbackSuccess);

    /// <summary>Opens the RGB color editor overlay for failed feedback.</summary>
    private void OpenCustomFeedbackFailure() => OpenRGBOverlay(SignalType.FeedbackFailure);

    /// <summary>
    /// Displays the custom RGB overlay for a specific signal type, initializing sliders and preview element with current color values.
    /// Interacts with <see cref="SignalColorManager.Instance"/>.
    /// </summary>
    /// <param name="signalType">The signal type to edit.</param>
    private void OpenRGBOverlay(SignalType signalType)
    {
        activeCustomSignal = signalType;

        if (rgbTitle != null)
            rgbTitle.text = $"Cor Personalizada: {signalType}";

        Color currentColor = signalType switch
        {
            SignalType.J => SignalColorManager.Instance.ColorJ,
            SignalType.K => SignalColorManager.Instance.ColorK,
            SignalType.CLK => SignalColorManager.Instance.ColorCLK,
            SignalType.Preset => SignalColorManager.Instance.ColorPreset,
            SignalType.Clear => SignalColorManager.Instance.ColorClear,
            SignalType.FeedbackSuccess => SignalColorManager.Instance.ColorFeedbackSuccess,
            SignalType.FeedbackFailure => SignalColorManager.Instance.ColorFeedbackFailure,
            _ => SignalColorManager.PresetColors[0]
        };

        sliderR?.SetValueWithoutNotify(Mathf.RoundToInt(currentColor.r * 255f));
        sliderG?.SetValueWithoutNotify(Mathf.RoundToInt(currentColor.g * 255f));
        sliderB?.SetValueWithoutNotify(Mathf.RoundToInt(currentColor.b * 255f));

        UpdateRGBPreview();

        if (rgbOverlay != null)
            rgbOverlay.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    /// Hides the custom RGB overlay without applying changes.
    /// </summary>
    private void CloseRGBOverlay()
    {
        if (rgbOverlay != null)
            rgbOverlay.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// Event handler for RGB slider changes. Updates color preview block and hex field.
    /// </summary>
    /// <param name="evt">Slider value change event.</param>
    private void OnRGBSliderChanged(ChangeEvent<float> evt) => UpdateRGBPreview();

    /// <summary>
    /// Recalculates preview color from RGB sliders and updates the preview element and hex text field.
    /// Interacts with <see cref="ColorUtility.ToHtmlStringRGB(Color)"/>.
    /// </summary>
    private void UpdateRGBPreview()
    {
        float r = (sliderR?.value ?? 255f) / 255f;
        float g = (sliderG?.value ?? 255f) / 255f;
        float b = (sliderB?.value ?? 255f) / 255f;

        Color color = new(r, g, b);

        if (rgbPreview != null)
            rgbPreview.style.backgroundColor = color;

        inputHex?.SetValueWithoutNotify("#" + ColorUtility.ToHtmlStringRGB(color));
    }

    /// <summary>
    /// Event handler for hex input text field changes. Parses hex string and synchronizes RGB sliders if valid.
    /// Interacts with <see cref="ColorUtility.TryParseHtmlString(string, out Color)"/>.
    /// </summary>
    /// <param name="evt">Text change event containing the newly typed hex string.</param>
    private void OnHexInputChanged(ChangeEvent<string> evt)
    {
        string hex = evt.newValue.Trim();

        if (!hex.StartsWith("#"))
            hex = "#" + hex;

        if (!ColorUtility.TryParseHtmlString(hex, out Color color))
            return;

        sliderR?.SetValueWithoutNotify(Mathf.RoundToInt(color.r * 255f));
        sliderG?.SetValueWithoutNotify(Mathf.RoundToInt(color.g * 255f));
        sliderB?.SetValueWithoutNotify(Mathf.RoundToInt(color.b * 255f));

        if (rgbPreview != null)
            rgbPreview.style.backgroundColor = color;
    }

    /// <summary>
    /// Saves selected RGB color to <see cref="SignalColorManager.Instance"/> for the active signal, syncs UI, and closes overlay.
    /// </summary>
    private void ConfirmRGBColor()
    {
        float r = (sliderR?.value ?? 255f) / 255f;
        float g = (sliderG?.value ?? 255f) / 255f;
        float b = (sliderB?.value ?? 255f) / 255f;

        SignalColorManager.Instance.SetCustomColor(activeCustomSignal.ToString(), new Color(r, g, b));

        SyncColorUI();
        CloseRGBOverlay();
    }

    #endregion
}
