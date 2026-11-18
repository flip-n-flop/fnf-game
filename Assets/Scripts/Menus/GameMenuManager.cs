using System.Collections.Generic;
using FlipNflop.Commons.Records;
using FlipNflop.Gla;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Manages the in-game pause menu UI and state transitions.
/// Interacts with <see cref="UIDocument"/> for UI rendering, <see cref="PlayerInput"/> to toggle player controls,
/// <see cref="InputActionReference"/> for pause inputs, and <see cref="SceneManager"/> for scene loading.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameMenuManager : MonoBehaviour
{
    #region Fields & Properties

    [Header("Scene Settings")]
    [Tooltip("Name of the scene loaded when exiting to the main menu.")]
    [SerializeField] private string mainMenuSceneName = "MenuWeb";

    [Header("Player Controls")]
    [Tooltip("PlayerInput component used to disable actions while paused.")]
    [SerializeField] private PlayerInput playerInput;

    [Tooltip("Input action reference used to toggle the pause menu.")]
    [SerializeField] private InputActionReference pauseAction;

    [Header("HUD")]
    [Tooltip("Signal label HUD to hide when menu is open.")]
    [SerializeField] private SignalLabelRenderer signalLabelRenderer;

    /// <summary>
    /// Cached reference to the UIDocument component attached to this GameObject.
    /// </summary>
    private UIDocument uiDocument;

    /// <summary>
    /// Root overlay container for the game menu.
    /// </summary>
    private VisualElement gameMenuOverlay;

    /// <summary>
    /// Main menu panel containing main pause options.
    /// </summary>
    private VisualElement panelMain;

    /// <summary>
    /// Options panel for game settings.
    /// </summary>
    private VisualElement panelOptions;

    /// <summary>
    /// HowToPlay panel displaying game instructions.
    /// </summary>
    private VisualElement panelHowToPlay;

    /// <summary>
    /// Currently active active submenu panel, or null if on main panel.
    /// </summary>
    private VisualElement activeSubmenu;

    /// <summary>
    /// HUD button to open the pause menu.
    /// </summary>
    private Button menuHudButton;

    /// <summary>
    /// Button to continue/resume gameplay.
    /// </summary>
    private Button continueButton;

    /// <summary>
    /// Button to restart the current level scene.
    /// </summary>
    private Button retryButton;

    /// <summary>
    /// Button to open the howToPlay panel.
    /// </summary>
    private Button howToPlayButton;

    /// <summary>
    /// Button to navigate back from submenus to the main pause panel.
    /// </summary>
    private Button backButton;

    /// <summary>
    /// Button to open the options panel.
    /// </summary>
    private Button optionsButton;

    /// <summary>
    /// Button to exit the level and load the main menu scene.
    /// </summary>
    private Button exitButton;

    /// <summary>
    /// List of all available submenu visual elements.
    /// </summary>
    private readonly List<VisualElement> submenus = new();

    /// <summary>
    /// Indicates whether the pause menu overlay is currently visible and active.
    /// </summary>
    public bool IsMenuOpen =>
        gameMenuOverlay != null &&
        gameMenuOverlay.style.display == DisplayStyle.Flex;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Initializes component references before OnEnable.
    /// Retrieves the attached <see cref="UIDocument"/>.
    /// </summary>
    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    /// <summary>
    /// Caches UI elements, configures initial display state, and registers UI and input callbacks.
    /// Interacts with <see cref="UIDocument"/>.
    /// </summary>
    private void OnEnable()
    {
        if (uiDocument == null)
            return;

        CacheUIElements();
        ConfigureInitialState();
        RegisterCallbacks();
    }

    /// <summary>
    /// Unregisters UI and input event callbacks when component is disabled.
    /// </summary>
    private void OnDisable()
    {
        UnregisterCallbacks();
    }

    #endregion

    #region Initialization & UI Setup

    /// <summary>
    /// Queries and caches all UI VisualElement and Button references from <see cref="UIDocument.rootVisualElement"/>.
    /// </summary>
    private void CacheUIElements()
    {
        VisualElement root = uiDocument.rootVisualElement;

        menuHudButton = root.Q<Button>("MenuButton");

        gameMenuOverlay = root.Q<VisualElement>("GameMenu");
        panelMain = root.Q<VisualElement>("PanelMain");
        panelOptions = root.Q<VisualElement>("PanelOptions");
        panelHowToPlay = root.Q<VisualElement>("HowToPlay");

        submenus.Clear();
        submenus.Add(panelOptions);
        submenus.Add(panelHowToPlay);

        continueButton = root.Q<Button>("Continue");
        retryButton = root.Q<Button>("Retry");
        howToPlayButton = root.Q<Button>("HowToPlayButton");
        backButton = root.Q<Button>("BackButton");
        optionsButton = root.Q<Button>("Options");
        exitButton = root.Q<Button>("Exit");
    }

    /// <summary>
    /// Hides the menu overlay and all submenus, setting initial UI visibility states.
    /// </summary>
    private void ConfigureInitialState()
    {
        if (gameMenuOverlay != null)
            gameMenuOverlay.style.display = DisplayStyle.None;

        if (panelMain != null)
            panelMain.RemoveFromClassList("hidden");

        foreach (VisualElement submenu in submenus)
        {
            if (submenu != null)
                submenu.AddToClassList("hidden");
        }

        if (backButton != null)
            backButton.AddToClassList("hidden");

        activeSubmenu = null;
    }

    #endregion

    #region Event Subscriptions

    /// <summary>
    /// Registers button click callbacks and input action listener.
    /// Interacts with <see cref="InputActionReference"/>.
    /// </summary>
    private void RegisterCallbacks()
    {
        if (menuHudButton != null)
            menuHudButton.clicked += OpenMenu;

        if (continueButton != null)
            continueButton.clicked += ContinueGame;

        if (retryButton != null)
            retryButton.clicked += RestartLevel;

        if (howToPlayButton != null)
            howToPlayButton.clicked += OpenHowToPlay;

        if (backButton != null)
            backButton.clicked += CloseActiveSubmenu;

        if (optionsButton != null)
            optionsButton.clicked += OpenOptions;

        if (exitButton != null)
            exitButton.clicked += ExitToMainMenu;

        if (pauseAction != null && pauseAction.action != null)
        {
            pauseAction.action.performed += OnPausePerformed;
            pauseAction.action.Enable();
        }
    }

    /// <summary>
    /// Unsubscribes button click callbacks and input action listener.
    /// </summary>
    private void UnregisterCallbacks()
    {
        if (menuHudButton != null)
            menuHudButton.clicked -= OpenMenu;

        if (continueButton != null)
            continueButton.clicked -= ContinueGame;

        if (retryButton != null)
            retryButton.clicked -= RestartLevel;

        if (howToPlayButton != null)
            howToPlayButton.clicked -= OpenHowToPlay;

        if (backButton != null)
            backButton.clicked -= CloseActiveSubmenu;

        if (optionsButton != null)
            optionsButton.clicked -= OpenOptions;

        if (exitButton != null)
            exitButton.clicked -= ExitToMainMenu;

        if (pauseAction != null && pauseAction.action != null)
            pauseAction.action.performed -= OnPausePerformed;
    }

    #endregion

    #region Input Handlers

    /// <summary>
    /// Callback invoked when the pause input action is triggered.
    /// </summary>
    /// <param name="context">The input action execution context.</param>
    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        HandlePauseNavigation();
    }

    /// <summary>
    /// Evaluates navigation state on pause input: closes active submenu, resumes game, or opens menu.
    /// </summary>
    private void HandlePauseNavigation()
    {
        if (IsMenuOpen && activeSubmenu != null)
        {
            CloseActiveSubmenu();
        }
        else if (IsMenuOpen)
        {
            ContinueGame();
        }
        else
        {
            OpenMenu();
        }
    }

    #endregion

    #region Public Menu API

    /// <summary>
    /// Pauses time, disables player inputs via <see cref="PlayerInput"/>, and displays the pause menu overlay.
    /// </summary>
    public void OpenMenu()
    {
        Time.timeScale = 0f;

        DisablePlayerInput();

        if (signalLabelRenderer != null)
            signalLabelRenderer.HideHUD();

        if (menuHudButton != null)
            menuHudButton.style.display = DisplayStyle.None;

        if (gameMenuOverlay != null)
            gameMenuOverlay.style.display = DisplayStyle.Flex;

        if (panelMain != null)
            panelMain.RemoveFromClassList("hidden");

        foreach (VisualElement submenu in submenus)
        {
            if (submenu != null)
                submenu.AddToClassList("hidden");
        }

        if (backButton != null)
            backButton.AddToClassList("hidden");

        activeSubmenu = null;

        if (continueButton != null)
            continueButton.Focus();
    }

    /// <summary>
    /// Resumes normal time scale, re-enables player input via <see cref="PlayerInput"/>, and hides the pause menu overlay.
    /// </summary>
    public void ContinueGame()
    {
        Time.timeScale = 1f;

        EnablePlayerInput();

        if (gameMenuOverlay != null)
            gameMenuOverlay.style.display = DisplayStyle.None;

        if (menuHudButton != null)
            menuHudButton.style.display = DisplayStyle.Flex;

        if (signalLabelRenderer != null)
            signalLabelRenderer.ShowHUD();

        if (gameMenuOverlay != null)
            gameMenuOverlay.Blur();
    }

    /// <summary>
    /// Resumes time, re-enables input, and reloads the current active scene via <see cref="SceneManager"/>.
    /// </summary>
    public void RestartLevel()
    {
        GlaServices.Instance?.Collector?.Abandon(EndCause.Restart);
        Time.timeScale = 1f;

        EnablePlayerInput();

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Resumes time, re-enables input, and loads the main menu scene via <see cref="SceneManager"/>.
    /// </summary>
    public void ExitToMainMenu()
    {
        GlaServices.Instance?.Collector?.Abandon(EndCause.Exit);
        Time.timeScale = 1f;

        EnablePlayerInput();

        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Opens the options submenu panel.
    /// </summary>
    public void OpenOptions()
    {
        OpenSubmenu(panelOptions);
    }

    /// <summary>
    /// Opens the howToPlay submenu panel.
    /// </summary>
    public void OpenHowToPlay()
    {
        OpenSubmenu(panelHowToPlay);
    }

    /// <summary>
    /// Closes the howToPlay panel and returns to the main pause panel.
    /// </summary>
    public void CloseHowToPlay()
    {
        CloseSubmenu(panelHowToPlay);
    }

    #endregion

    #region Submenu Navigation

    /// <summary>
    /// Closes whatever submenu is currently active.
    /// </summary>
    private void CloseActiveSubmenu()
    {
        if (activeSubmenu != null)
            CloseSubmenu(activeSubmenu);
    }

    /// <summary>
    /// Opens a specified submenu panel and hides the main panel.
    /// </summary>
    /// <param name="submenu">The target VisualElement submenu panel to open.</param>
    private void OpenSubmenu(VisualElement submenu)
    {
        if (submenu == null)
        {
            Debug.LogError("Submenu element not found!");
            return;
        }

        if (panelMain != null)
            panelMain.AddToClassList("hidden");

        foreach (VisualElement item in submenus)
        {
            if (item != null)
                item.AddToClassList("hidden");
        }

        submenu.RemoveFromClassList("hidden");
        activeSubmenu = submenu;

        if (backButton != null)
            backButton.RemoveFromClassList("hidden");
    }

    /// <summary>
    /// Closes a specified submenu panel and restores visibility of the main panel.
    /// </summary>
    /// <param name="submenu">The target VisualElement submenu panel to close.</param>
    private void CloseSubmenu(VisualElement submenu)
    {
        if (submenu == null)
        {
            Debug.LogError("Submenu element not found!");
            return;
        }

        submenu.AddToClassList("hidden");
        activeSubmenu = null;

        if (backButton != null)
            backButton.AddToClassList("hidden");

        if (panelMain != null)
            panelMain.RemoveFromClassList("hidden");

        if (submenu == panelOptions && optionsButton != null)
            optionsButton.Focus();
        else if (howToPlayButton != null)
            howToPlayButton.Focus();
    }

    #endregion

    #region Player Input Toggle

    /// <summary>
    /// Disables the "Player" action map in <see cref="PlayerInput"/> to block gameplay actions during pause.
    /// </summary>
    private void DisablePlayerInput()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        InputActionMap playerMap = playerInput.actions.FindActionMap("Player");

        if (playerMap != null)
            playerMap.Disable();
    }

    /// <summary>
    /// Re-enables the "Player" action map in <see cref="PlayerInput"/> upon unpausing.
    /// </summary>
    private void EnablePlayerInput()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        InputActionMap playerMap = playerInput.actions.FindActionMap("Player");

        if (playerMap != null)
            playerMap.Enable();
    }

    #endregion
}
