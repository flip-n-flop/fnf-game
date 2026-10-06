using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

#region Config Structs

/// <summary>
/// Defines a configurable input action binding setup in the rebind menu.
/// Interacts with Unity Input System <see cref="InputActionReference"/> and Unity Localization <see cref="LocalizedString"/>.
/// </summary>
[System.Serializable]
public struct ActionRebindSetup
{
    /// <summary>
    /// Localization key reference for the action label displayed in the UI.
    /// </summary>
    [Tooltip("Localization key for the label text displayed in the interface.")]
    public LocalizedString labelText;

    /// <summary>
    /// Reference to the Unity Input System <see cref="InputActionReference"/> being rebound.
    /// </summary>
    public InputActionReference inputAction;

    /// <summary>
    /// Index of the binding within the input action's bindings list.
    /// </summary>
    [Tooltip("Binding index inside the input action.")]
    public int bindingIndex;
}

#endregion

/// <summary>
/// Manages the key rebinding interface, handling interactive input rebinding, conflict resolution, localization, and persistent storage via <see cref="PlayerPrefs"/>.
/// Interacts with Unity Input System (<see cref="InputActionAsset"/>, <see cref="InputActionRebindingExtensions"/>), <see cref="UIDocument"/>, and <see cref="LocalizationSettings"/>.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class RebindManager : MonoBehaviour
{
    #region Constants & Fields

    /// <summary>
    /// <see cref="PlayerPrefs"/> key used to save JSON serialized input binding overrides.
    /// </summary>
    private const string SaveKey = "ControlBindingsSaved";

    /// <summary>
    /// Raised whenever a binding override changes (rebind completed or restored to defaults).
    /// Allows other systems (e.g. TutorialUI) to react without a direct reference to RebindManager.
    /// </summary>
    public static event System.Action OnBindingsChanged;

    /// <summary>
    /// Input Action Asset containing action maps and default key bindings.
    /// </summary>
    [Header("Key Configuration")]
    [SerializeField] private InputActionAsset inputAsset;

    /// <summary>
    /// Array of action rebind setups exposed in the inspector for configuration.
    /// </summary>
    [SerializeField] private ActionRebindSetup[] actionsToRebind;

    /// <summary>
    /// Name of the UI Toolkit ScrollView element in UXML where rebind rows are generated.
    /// </summary>
    [Header("UI Elements")]
    [SerializeField] private string scrollViewName = "ControlsScrollView";

    /// <summary>
    /// Name of the UI Toolkit Button element in UXML used to restore default bindings.
    /// </summary>
    [SerializeField] private string restoreButtonName = "BtnRestoreControls";

    /// <summary>
    /// Reference to the attached <see cref="UIDocument"/> component.
    /// </summary>
    private UIDocument uiDocument;

    /// <summary>
    /// UI Toolkit ScrollView container for control rebind rows.
    /// </summary>
    private ScrollView controlsScrollView;

    /// <summary>
    /// UI Toolkit Button for resetting bindings to defaults.
    /// </summary>
    private Button restoreDefaultsButton;

    /// <summary>
    /// Active interactive rebinding operation instance provided by Unity Input System.
    /// </summary>
    private InputActionRebindingExtensions.RebindingOperation currentRebindOperation;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Initializes component references.
    /// </summary>
    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    /// <summary>
    /// Caches UI elements, registers event callbacks, loads stored bindings from <see cref="PlayerPrefs"/>,
    /// initializes localization completion listeners, and subscribes to locale changes.
    /// Interacts with <see cref="LocalizationSettings"/>.
    /// </summary>
    private void OnEnable()
    {
        if (uiDocument == null) return;

        VisualElement root = uiDocument.rootVisualElement;

        controlsScrollView = root.Q<ScrollView>(scrollViewName);
        restoreDefaultsButton = root.Q<Button>(restoreButtonName);

        RegisterCallbacks();
        LoadBindings();

        LocalizationSettings.InitializationOperation.Completed += _ => DrawUI();

        if (LocalizationSettings.InitializationOperation.IsDone)
            DrawUI();

        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    /// <summary>
    /// Unregisters callbacks, unsubscribes from localization events, and disposes any active rebinding operation.
    /// </summary>
    private void OnDisable()
    {
        UnregisterCallbacks();

        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

        currentRebindOperation?.Dispose();
        currentRebindOperation = null;
    }

    #endregion

    #region Callbacks & Event Registration

    /// <summary>
    /// Registers click callback for the restore defaults button.
    /// </summary>
    private void RegisterCallbacks()
    {
        if (restoreDefaultsButton != null)
            restoreDefaultsButton.clicked += RestoreDefaults;
    }

    /// <summary>
    /// Unregisters click callback for the restore defaults button.
    /// </summary>
    private void UnregisterCallbacks()
    {
        if (restoreDefaultsButton != null)
            restoreDefaultsButton.clicked -= RestoreDefaults;
    }

    /// <summary>
    /// Triggers UI redraw when the selected localization locale changes.
    /// </summary>
    /// <param name="_">The newly selected <see cref="Locale"/>.</param>
    private void OnLocaleChanged(Locale _) => DrawUI();

    #endregion

    #region UI Generation

    /// <summary>
    /// Clears and dynamically populates the controls ScrollView with action rebind rows.
    /// Interacts with <see cref="ActionRebindSetup"/> and UI Toolkit.
    /// </summary>
    private void DrawUI()
    {
        if (controlsScrollView == null || actionsToRebind == null) return;

        controlsScrollView.Clear();

        foreach (ActionRebindSetup setup in actionsToRebind)
        {
            if (setup.inputAction == null || setup.inputAction.action == null) continue;

            controlsScrollView.Add(CreateRow(setup));
        }
    }

    /// <summary>
    /// Creates a UI row visual element containing a localized text label and a rebind button for an action setup.
    /// Interacts with <see cref="LocalizedString.GetLocalizedStringAsync"/> and UI Toolkit controls.
    /// </summary>
    /// <param name="setup">The action rebind setup configuration.</param>
    /// <returns>A configured <see cref="VisualElement"/> row.</returns>
    private VisualElement CreateRow(ActionRebindSetup setup)
    {
        VisualElement row = new();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.Center;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 15;
        row.style.width = Length.Percent(100);

        Label label = new();
        label.style.color = Color.white;
        label.style.fontSize = 20;
        label.style.width = 300;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;

        var loadOp = setup.labelText.GetLocalizedStringAsync();
        loadOp.Completed += op => label.text = op.Result;

        if (loadOp.IsDone)
            label.text = loadOp.Result;

        Button button = new();
        button.AddToClassList("menu-button");
        button.style.width = 200;
        button.style.height = 50;

        UpdateButtonText(button, setup);

        button.clicked += () => StartRebind(setup, button);

        row.Add(label);
        row.Add(button);

        return row;
    }

    #endregion

    #region Rebinding Logic
    /// <summary>
    /// Initiates an interactive rebinding operation for the specified input action.
    /// Disables action, displays listening prompt on button, and configures cancellation / completion callbacks.
    /// Interacts with <see cref="InputActionRebindingExtensions"/>.
    /// </summary>
    /// <param name="setup">Action rebind setup configuration.</param>
    /// <param name="button">Target UI button clicked to rebind.</param>
    private void StartRebind(ActionRebindSetup setup, Button button)
    {
        if (currentRebindOperation != null) return;

        InputAction action = setup.inputAction.action;
        if (action == null) return;

        action.Disable();

        button.text = "[ Pressione ]";
        button.Blur();

        currentRebindOperation = action
            .PerformInteractiveRebinding(setup.bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op => OnRebindComplete(op, setup))
            .OnCancel(op => OnRebindCanceled(op, setup, button))
            .Start();
    }

    /// <summary>
    /// Handles completion of interactive rebinding operation. Resolves binding conflicts, enables action, saves overrides, and redraws UI.
    /// </summary>
    /// <param name="operation">The completed rebinding operation.</param>
    /// <param name="setup">Action rebind setup configuration.</param>
    private void OnRebindComplete(
        InputActionRebindingExtensions.RebindingOperation operation,
        ActionRebindSetup setup)
    {
        operation.Dispose();
        currentRebindOperation = null;

        InputAction action = setup.inputAction.action;
        if (action == null) return;

        string newPath = action.bindings[setup.bindingIndex].effectivePath;
        ResolveConflicts(setup, newPath);

        action.Enable();
        SaveBindings();
        DrawUI();

        OnBindingsChanged?.Invoke();
    }

    /// <summary>
    /// Checks for duplicate bindings across configured actions and unbinds conflicting entries.
    /// </summary>
    /// <param name="changedSetup">The action setup being assigned the new binding.</param>
    /// <param name="newPath">The newly assigned control path string.</param>
    private void ResolveConflicts(ActionRebindSetup changedSetup, string newPath)
    {
        foreach (ActionRebindSetup otherSetup in actionsToRebind)
        {
            InputAction otherAction = otherSetup.inputAction.action;
            if (otherAction == null) continue;

            bool isSameBinding =
                otherAction == changedSetup.inputAction.action &&
                otherSetup.bindingIndex == changedSetup.bindingIndex;

            if (isSameBinding) continue;
            if (otherSetup.bindingIndex >= otherAction.bindings.Count) continue;

            InputBinding binding = otherAction.bindings[otherSetup.bindingIndex];
            string currentPath = binding.hasOverrides ? binding.overridePath : binding.path;

            if (currentPath == newPath)
                otherAction.ApplyBindingOverride(otherSetup.bindingIndex, string.Empty);
        }
    }

    /// <summary>
    /// Handles cancellation of interactive rebinding operation (e.g., via Escape key).
    /// Re-enables the action and restores button display text.
    /// </summary>
    /// <param name="operation">The canceled rebinding operation.</param>
    /// <param name="setup">Action rebind setup configuration.</param>
    /// <param name="button">UI button element.</param>
    private void OnRebindCanceled(
        InputActionRebindingExtensions.RebindingOperation operation,
        ActionRebindSetup setup,
        Button button)
    {
        operation.Dispose();
        currentRebindOperation = null;

        setup.inputAction.action?.Enable();
        UpdateButtonText(button, setup);
    }

    /// <summary>
    /// Updates UI button text to display human-readable name of assigned key binding.
    /// Interacts with <see cref="InputControlPath.ToHumanReadableString(string, InputControlPath.HumanReadableStringOptions)"/>.
    /// </summary>
    /// <param name="button">UI button to update.</param>
    /// <param name="setup">Action rebind setup configuration.</param>
    private void UpdateButtonText(Button button, ActionRebindSetup setup)
    {
        InputAction action = setup.inputAction.action;
        if (action == null) return;
        if (setup.bindingIndex >= action.bindings.Count) return;

        InputBinding binding = action.bindings[setup.bindingIndex];
        string path = binding.hasOverrides ? binding.overridePath : binding.path;

        button.text = string.IsNullOrEmpty(path)
            ? "---"
            : InputControlPath.ToHumanReadableString(
                path,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
    }

    #endregion

    #region Save / Load / Restore

    /// <summary>
    /// Saves current binding overrides from <see cref="InputActionAsset"/> to <see cref="PlayerPrefs"/> as JSON.
    /// </summary>
    private void SaveBindings()
    {
        if (inputAsset == null) return;
        PlayerPrefs.SetString(SaveKey, inputAsset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Loads binding overrides from <see cref="PlayerPrefs"/> JSON data into <see cref="InputActionAsset"/>.
    /// </summary>
    private void LoadBindings()
    {
        if (inputAsset == null) return;
        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
            inputAsset.LoadBindingOverridesFromJson(json);
    }

    /// <summary>
    /// Cancels any active rebind operation, removes all binding overrides from <see cref="InputActionAsset"/>,
    /// deletes stored preferences in <see cref="PlayerPrefs"/>, and redraws UI.
    /// </summary>
    private void RestoreDefaults()
    {
        currentRebindOperation?.Cancel();

        if (inputAsset != null)
        {
            inputAsset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(SaveKey);
        }

        DrawUI();

        OnBindingsChanged?.Invoke();
    }

    #endregion
}
