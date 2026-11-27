using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Controls the end-of-level result screen UI overlay and navigation actions.
/// Interacts with <see cref="UIDocument"/> for UI Toolkit elements, <see cref="LocalizationSettings"/> for localized strings,
/// <see cref="LevelSequenceManager"/> for level progression, <see cref="MenuManager"/> for level load configuration,
/// and <see cref="SceneManager"/> for scene loading.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ResultScreenController : MonoBehaviour
{
    #region Fields & Properties

    /// <summary>
    /// Singleton instance of the ResultScreenController.
    /// </summary>
    public static ResultScreenController Instance { get; private set; }

    [Header("Navigation Settings")]
    [Tooltip("Name of the scene that loads the level (matches MenuManager.SelectLevelAndLoad).")]
    [SerializeField] private string customLevelName = "Custom";

    [Tooltip("Name of the main menu scene.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    /// <summary>
    /// Key table name for localized string lookup.
    /// Interacts with <see cref="LocalizationSettings"/>.
    /// </summary>
    private const string TableName = "UIStrings";

    /// <summary>
    /// Root visual element container of the result screen UI.
    /// </summary>
    private VisualElement resultScreen;

    /// <summary>
    /// Main panel containing result stats and buttons.
    /// </summary>
    private VisualElement panelResult;

    /// <summary>
    /// Header label displaying victory or failure text.
    /// </summary>
    private Label resultText;

    /// <summary>
    /// Label displaying total score.
    /// </summary>
    private Label scoreText;

    /// <summary>
    /// Label displaying completion time.
    /// </summary>
    private Label timeText;

    /// <summary>
    /// Label displaying successful hits count.
    /// </summary>
    private Label hitsText;

    /// <summary>
    /// Label displaying misses count.
    /// </summary>
    private Label missesText;

    /// <summary>
    /// Button to proceed to the next level.
    /// </summary>
    private Button continueButton;

    /// <summary>
    /// Button to retry the current level.
    /// </summary>
    private Button tryAgainButton;

    /// <summary>
    /// Button to return to the main menu.
    /// </summary>
    private Button menuButton;

    /// <summary>
    /// Button to toggle result panel visibility.
    /// </summary>
    private Button hideButton;

    /// <summary>
    /// Indicates whether the result panel content is currently expanded/visible.
    /// </summary>
    private bool contentVisible = true;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Initializes singleton instance.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// Queries UI elements, registers button click events, and hides the result screen initially.
    /// Interacts with <see cref="UIDocument"/>.
    /// </summary>
    private void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
            return;

        var root = uiDocument.rootVisualElement;

        resultScreen = root.Q<VisualElement>("ResultScreen");
        panelResult = root.Q<VisualElement>("PanelResult");

        resultText = root.Q<Label>("ResultText");
        scoreText = root.Q<Label>("ScoreText");
        timeText = root.Q<Label>("TimeText");
        hitsText = root.Q<Label>("HitsText");
        missesText = root.Q<Label>("MissesText");

        continueButton = root.Q<Button>("ContinuButton");
        tryAgainButton = root.Q<Button>("TryAgainButton");
        menuButton = root.Q<Button>("MenuButton");
        hideButton = root.Q<Button>("HideButton");

        if (continueButton != null)
            continueButton.clicked += OnContinueClicked;

        if (tryAgainButton != null)
            tryAgainButton.clicked += OnTryAgainClicked;

        if (menuButton != null)
            menuButton.clicked += OnMenuClicked;

        if (hideButton != null)
            hideButton.clicked += OnHideClicked;

        Hide();
    }

    /// <summary>
    /// Unsubscribes button click handlers when component is disabled.
    /// </summary>
    private void OnDisable()
    {
        if (continueButton != null)
            continueButton.clicked -= OnContinueClicked;

        if (tryAgainButton != null)
            tryAgainButton.clicked -= OnTryAgainClicked;

        if (menuButton != null)
            menuButton.clicked -= OnMenuClicked;

        if (hideButton != null)
            hideButton.clicked -= OnHideClicked;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Displays the result screen UI overlay with appropriate localized messages and button options based on success state.
    /// Interacts with <see cref="LocalizationSettings"/> and <see cref="LevelSequenceManager"/>.
    /// </summary>
    /// <param name="success">True if the player passed the level; false otherwise.</param>
    public void Show(bool success)
    {
        resultScreen.RemoveFromClassList("hidden");
        contentVisible = true;
        panelResult.style.display = DisplayStyle.Flex;

        if (hideButton != null)
            hideButton.text = LocalizationSettings.StringDatabase.GetLocalizedString(TableName, "btn_hide_panel");

        bool hasNext = LevelSequenceManager.Levels != null && LevelSequenceManager.HasNextLevel();

        if (continueButton != null)
            continueButton.style.display = (success && hasNext) ? DisplayStyle.Flex : DisplayStyle.None;

        if (tryAgainButton != null)
            tryAgainButton.style.display = success ? DisplayStyle.None : DisplayStyle.Flex;

        if (resultText != null)
        {
            string key = success ? "result_correct" : "result_incorrect";
            resultText.text = LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key);
        }
    }

    /// <summary>
    /// Sets localized score and time text strings on the result screen UI labels.
    /// Interacts with <see cref="LocalizationSettings"/>.
    /// </summary>
    /// <param name="score">Formatted score string to display.</param>
    /// <param name="time">Formatted completion time string to display.</param>
    public void SetResultData(string score, string time)
    {
        string strScore = LocalizationSettings.StringDatabase.GetLocalizedString(TableName, "lbl_score");
        string strTime = LocalizationSettings.StringDatabase.GetLocalizedString(TableName, "lbl_time");

        if (scoreText != null)
            scoreText.text = $"{strScore} {score}";

        if (timeText != null)
            timeText.text = $"{strTime} {time}";
    }

    /// <summary>
    /// Hides the result screen UI overlay.
    /// </summary>
    public void Hide()
    {
        if (resultScreen != null)
            resultScreen.AddToClassList("hidden");
    }

    #endregion

    #region Button Handlers

    /// <summary>
    /// Handler for the Continue button click. Advances level index and loads the next level scene.
    /// Interacts with <see cref="LevelSequenceManager"/>, <see cref="MenuManager"/>, and <see cref="SceneManager"/>.
    /// </summary>
    private void OnContinueClicked()
    {
        Time.timeScale = 1f;

        if (LevelSequenceManager.Levels == null || !LevelSequenceManager.HasNextLevel())
        {
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        LevelSequenceManager.CurrentLevelIndex++;
        MenuManager.LevelToLoadJSON = LevelSequenceManager.Levels[LevelSequenceManager.CurrentLevelIndex];

        LoadingScreenManager.Load(customLevelName);
    }

    /// <summary>
    /// Handler for the Try Again button click. Reloads the current custom level scene.
    /// Interacts with <see cref="SceneManager"/>.
    /// </summary>
    private void OnTryAgainClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(customLevelName);
    }

    /// <summary>
    /// Handler for the Menu button click. Loads the main menu scene.
    /// Interacts with <see cref="SceneManager"/>.
    /// </summary>
    private void OnMenuClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Handler for the Hide button click. Toggles panel visibility and updates button text.
    /// Interacts with <see cref="LocalizationSettings"/>.
    /// </summary>
    private void OnHideClicked()
    {
        contentVisible = !contentVisible;
        panelResult.style.display = contentVisible ? DisplayStyle.Flex : DisplayStyle.None;

        string btnKey = contentVisible ? "btn_hide_panel" : "btn_show_panel";
        if (hideButton != null)
            hideButton.text = LocalizationSettings.StringDatabase.GetLocalizedString(TableName, btnKey);
    }

    #endregion
}