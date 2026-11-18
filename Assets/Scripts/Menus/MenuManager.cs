using System.Collections.Generic;
using FlipNflop.Gla;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

/// <summary>
/// Controls main menu navigation, panel switching, level selection, and WebGL json uploads.
/// Interacts with <see cref="UIDocument"/> for UI Toolkit elements, <see cref="UploadMenuManager"/> for level file uploads,
/// <see cref="LevelSequenceManager"/> to track current level index, <see cref="SceneManager"/> for scene loading,
/// and <see cref="GlaServices"/>, which overlays the GLA entry screen and the "Trocar e-mail" button on the menu.
/// </summary>
public class MenuManager : MonoBehaviour
{
    #region Fields & Properties

    [Header("Scene Navigation")]
    [Tooltip("Name of the scene loaded when a level is selected.")]
    [SerializeField] private string customLevelName = "Custom";

    [Header("Level JSON Files")]
    [Tooltip("Drag the level .json files here.")]
    [FormerlySerializedAs("arquivosDasFases")]
    [SerializeField] private TextAsset[] levelFiles = new TextAsset[9];

    /// <summary>
    /// Static identifier of the JSON level file name to be loaded by LevelJsonLoader.
    /// Interacts with <see cref="LevelJsonLoader"/> and <see cref="ResultScreenController"/>.
    /// </summary>
    public static string LevelToLoadJSON = "";

    /// <summary>
    /// Cached reference to the attached UIDocument component.
    /// </summary>
    private UIDocument uiDocument;

    /// <summary>
    /// Root main menu visual element container.
    /// </summary>
    private VisualElement mainMenuPanel;

    /// <summary>
    /// About info panel visual element.
    /// </summary>
    private VisualElement aboutPanel;

    /// <summary>
    /// Level selection panel visual element.
    /// </summary>
    private VisualElement levelSelectPanel;

    /// <summary>
    /// HowToPlay panel visual element.
    /// </summary>
    private VisualElement howToPlayPanel;

    /// <summary>
    /// Settings/configuration panel visual element.
    /// </summary>
    private VisualElement settingsPanel;

    /// <summary>
    /// Cached reference to the attached UploadMenuManager component.
    /// </summary>
    private UploadMenuManager uploadManager;

    /// <summary>
    /// Navigation back button to return to main menu from submenus.
    /// </summary>
    private Button backButton;

    /// <summary>
    /// Currently active submenu panel, or null if on main menu.
    /// </summary>
    private VisualElement activeSubmenu;

    /// <summary>
    /// List containing all registered submenu visual elements.
    /// </summary>
    private readonly List<VisualElement> submenus = new();

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Caches UI elements, attaches button listeners, configures level select buttons, and hands the
    /// document root to the GLA entry screen hook.
    /// Interacts with <see cref="UIDocument"/>, <see cref="UploadMenuManager"/> and <see cref="GlaServices.AttachEntryScreen"/>.
    /// </summary>
    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        uploadManager = GetComponent<UploadMenuManager>();

        if (uiDocument == null)
            return;

        VisualElement root = uiDocument.rootVisualElement;

        mainMenuPanel = root.Q<VisualElement>("MainMenu");
        aboutPanel = root.Q<VisualElement>("About");
        levelSelectPanel = root.Q<VisualElement>("LevelSelect");
        howToPlayPanel = root.Q<VisualElement>("HowToPlay");
        settingsPanel = root.Q<VisualElement>("Settings");
        backButton = root.Q<Button>("BackButton");

        submenus.Clear();
        submenus.Add(aboutPanel);
        submenus.Add(levelSelectPanel);
        submenus.Add(howToPlayPanel);
        submenus.Add(settingsPanel);

        if (backButton != null)
            backButton.clicked += CloseActiveSubmenu;

        Button playButton = root.Q<Button>("PlayButton");
        if (playButton != null)
            playButton.clicked += OpenLevelSelect;

        Button uploadButton = root.Q<Button>("UploadButton");
        if (uploadButton != null && uploadManager != null)
            uploadButton.clicked += uploadManager.OnClickUpload;

        Button howToPlayButton = root.Q<Button>("HowToPlayButton");
        if (howToPlayButton != null)
            howToPlayButton.clicked += OpenHowToPlay;

        Button aboutButton = root.Q<Button>("AboutButton");
        if (aboutButton != null)
            aboutButton.clicked += OpenAbout;

        Button settingsButton = root.Q<Button>("SettingsButton");
        if (settingsButton != null)
            settingsButton.clicked += OpenSettings;

        for (int i = 0; i < levelFiles.Length; i++)
        {
            TextAsset jsonFile = levelFiles[i];
            if (jsonFile != null)
            {
                string jsonName = jsonFile.name;
                string buttonId = $"Level{i + 1}";

                ConfigureLevelButton(root, buttonId, jsonName, i);
            }
        }

        GlaServices glaServices = GlaServices.Instance;
        if (glaServices != null)
            glaServices.AttachEntryScreen(root);
    }

    /// <summary>
    /// Unsubscribes back button click events.
    /// </summary>
    private void OnDisable()
    {
        if (backButton != null)
            backButton.clicked -= CloseActiveSubmenu;
    }

    /// <summary>
    /// Checks for escape key press to close active submenus.
    /// Interacts with <see cref="Keyboard.current"/>.
    /// </summary>
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseActiveSubmenu();
    }

    #endregion

    #region Level Button Setup

    /// <summary>
    /// Configures a level selection button in the UI Toolkit hierarchy with click handlers.
    /// Interacts with <see cref="SelectLevelAndLoad(string, int)"/>.
    /// </summary>
    /// <param name="root">Root visual element of the UI document.</param>
    /// <param name="buttonId">UI element name/ID of the level button.</param>
    /// <param name="jsonName">Name of the target JSON level file.</param>
    /// <param name="levelIndex">Index of the level in sequence.</param>
    private void ConfigureLevelButton(VisualElement root, string buttonId, string jsonName, int levelIndex)
    {
        Button button = root.Q<Button>(buttonId);
        if (button != null)
        {
            button.clicked += () => SelectLevelAndLoad(jsonName, levelIndex);
        }
        else
        {
            Debug.LogWarning($"Button {buttonId} not found in UI Builder!");
        }
    }

    #endregion

    #region Panel Navigation

    /// <summary>
    /// Displays the About panel.
    /// </summary>
    public void OpenAbout()
    {
        OpenSubmenu(aboutPanel);
    }

    /// <summary>
    /// Legacy alias for OpenAbout.
    /// </summary>
    public void AbrirSobre() => OpenAbout();

    /// <summary>
    /// Closes the About panel and returns to the main menu.
    /// </summary>
    public void CloseAbout()
    {
        CloseSubmenu(aboutPanel);
    }

    /// <summary>
    /// Legacy alias for CloseAbout.
    /// </summary>
    public void FecharSobre() => CloseAbout();

    /// <summary>
    /// Displays the Level Select panel.
    /// </summary>
    public void OpenLevelSelect()
    {
        OpenSubmenu(levelSelectPanel);
    }

    /// <summary>
    /// Legacy alias for OpenLevelSelect.
    /// </summary>
    public void AbrirSelecaoDeNiveis() => OpenLevelSelect();

    /// <summary>
    /// Closes the Level Select panel and returns to the main menu.
    /// </summary>
    public void CloseLevelSelect()
    {
        CloseSubmenu(levelSelectPanel);
    }

    /// <summary>
    /// Legacy alias for CloseLevelSelect.
    /// </summary>
    public void FecharSelecaoDeNiveis() => CloseLevelSelect();

    /// <summary>
    /// Displays the HowToPlay panel.
    /// </summary>
    public void OpenHowToPlay()
    {
        OpenSubmenu(howToPlayPanel);
    }

    /// <summary>
    /// Legacy alias for OpenHowToPlay.
    /// </summary>
    public void AbrirHowToPlay() => OpenHowToPlay();

    /// <summary>
    /// Closes the HowToPlay panel and returns to the main menu.
    /// </summary>
    public void CloseHowToPlay()
    {
        CloseSubmenu(howToPlayPanel);
    }

    /// <summary>
    /// Legacy alias for CloseHowToPlay.
    /// </summary>
    public void FecharHowToPlay() => CloseHowToPlay();

    /// <summary>
    /// Displays the Settings panel.
    /// </summary>
    public void OpenSettings()
    {
        OpenSubmenu(settingsPanel);
    }

    /// <summary>
    /// Legacy alias for OpenSettings.
    /// </summary>
    public void AbrirOpcoes() => OpenSettings();

    /// <summary>
    /// Closes the Settings panel and returns to the main menu.
    /// </summary>
    public void CloseSettings()
    {
        CloseSubmenu(settingsPanel);
    }

    /// <summary>
    /// Legacy alias for CloseSettings.
    /// </summary>
    public void FecharOpcoes() => CloseSettings();

    /// <summary>
    /// Closes whatever submenu is currently active.
    /// </summary>
    private void CloseActiveSubmenu()
    {
        if (activeSubmenu != null)
            CloseSubmenu(activeSubmenu);
    }

    /// <summary>
    /// Opens a specified submenu panel and hides the main menu panel.
    /// </summary>
    /// <param name="submenu">Target VisualElement submenu panel to display.</param>
    private void OpenSubmenu(VisualElement submenu)
    {
        if (submenu == null)
        {
            Debug.LogError("Submenu element not found!");
            return;
        }

        if (mainMenuPanel != null)
            mainMenuPanel.AddToClassList("hidden");

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
    /// Closes a specified submenu panel and restores visibility of the main menu panel.
    /// </summary>
    /// <param name="submenu">Target VisualElement submenu panel to hide.</param>
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

        if (mainMenuPanel != null)
            mainMenuPanel.RemoveFromClassList("hidden");
    }

    #endregion

    #region JSON Level Loading

    /// <summary>
    /// Sets the target level JSON name and index, then loads the game level scene.
    /// Interacts with <see cref="LevelSequenceManager.CurrentLevelIndex"/> and <see cref="SceneManager"/>.
    /// </summary>
    /// <param name="levelJsonName">Name of the JSON file to load.</param>
    /// <param name="levelIndex">Index of the level in sequence.</param>
    public void SelectLevelAndLoad(string levelJsonName, int levelIndex)
    {
        LevelSequenceManager.CurrentLevelIndex = levelIndex;

        LevelToLoadJSON = levelJsonName;
        Debug.Log($"JSON Selected successfully: {levelJsonName} | Index: {levelIndex}");

        SceneManager.LoadScene(customLevelName);
    }

    #endregion
}
