using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages level timer tracking, score calculation based on accuracy and elapsed time, and UI Toolkit display updates.
/// Interacts with <see cref="UIDocument"/> / <see cref="Label"/> for UI output and <see cref="ResultScreenController"/> to report final results.
/// </summary>
public class ScoreController : MonoBehaviour
{
    #region Singleton & Fields

    /// <summary>
    /// Singleton instance reference for global access.
    /// </summary>
    public static ScoreController Instance { get; private set; }

    [Header("In-Game UI Toolkit")]
    [Tooltip("UIDocument containing the HUD layout (HUDLabels.uxml).")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Score Settings")]
    [Tooltip("Maximum possible score (achieved with 100% accuracy in 0 seconds).")]
    [SerializeField] private int maxScore = 1000;

    private Label timerSecTensLabel;
    private Label timerSecOnesLabel;
    private Label timerCentiTensLabel;
    private Label timerCentiOnesLabel;
    private float elapsedTime;
    private bool isRunning;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Initializes singleton instance.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Initializes UI elements and starts the timer.
    /// </summary>
    private void Start()
    {
        InitUI();
        StartTimer();
    }

    /// <summary>
    /// Advances timer each frame while active and updates timer UI.
    /// </summary>
    private void Update()
    {
        if (!isRunning) return;
        elapsedTime += Time.deltaTime;
        UpdateTimerUI();
    }

    #endregion

    #region UI Initialization

    /// <summary>
    /// Queries the timer digit labels from the UIDocument if assigned.
    /// </summary>
    private void InitUI()
    {
        if (uiDocument != null)
        {
            VisualElement root = uiDocument.rootVisualElement;
            timerSecTensLabel = root.Q<Label>("TimerSecTens");
            timerSecOnesLabel = root.Q<Label>("TimerSecOnes");
            timerCentiTensLabel = root.Q<Label>("TimerCentiTens");
            timerCentiOnesLabel = root.Q<Label>("TimerCentiOnes");
        }
    }

    #endregion

    #region Timer Control

    /// <summary>
    /// Resets elapsed time to zero and activates the timer.
    /// </summary>
    public void StartTimer()
    {
        elapsedTime = 0f;
        isRunning = true;
        UpdateTimerUI();
    }

    /// <summary>
    /// Pauses timer progression.
    /// </summary>
    public void StopTimer() => isRunning = false;

    /// <summary>
    /// Gets the current elapsed level time in seconds.
    /// </summary>
    /// <returns>Elapsed time in seconds.</returns>
    public float GetElapsedTime() => elapsedTime;

    #endregion

    #region Score Evaluation & Reporting

    /// <summary>
    /// Stops the timer, computes the final score based on segment accuracy and time, and reports results to <see cref="ResultScreenController"/>.
    /// </summary>
    /// <param name="correctSegments">Number of path segments the player drew correctly.</param>
    /// <param name="gabaritoTotal">Total number of segments in the reference path.</param>
    /// <param name="success">Whether the overall level attempt was successful.</param>
    public void ReportResult(int correctSegments, int gabaritoTotal, bool success)
    {
        StopTimer();

        int score = ComputeScore(correctSegments, gabaritoTotal, elapsedTime);
        int misses = gabaritoTotal - correctSegments;
        if (misses < 0) misses = 0;

        int m = Mathf.FloorToInt(elapsedTime / 60f);
        int s = Mathf.FloorToInt(elapsedTime % 60f);
        string timeString = $"{m:00}:{s:00}";

        Debug.Log($"[ScoreController] correct:{correctSegments}/{gabaritoTotal} | time:{timeString} | score:{score}");

        ResultScreenController.Instance.SetResultData(
            score.ToString(),
            timeString
        );
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Updates the UI Toolkit timer digit Labels with elapsed seconds and centiseconds.
    /// </summary>
    private void UpdateTimerUI()
    {
        int seconds = Mathf.FloorToInt(elapsedTime);
        int centiseconds = Mathf.FloorToInt((elapsedTime * 100f) % 100f);

        if (timerSecTensLabel != null) timerSecTensLabel.text = (seconds / 10).ToString();
        if (timerSecOnesLabel != null) timerSecOnesLabel.text = (seconds % 10).ToString();
        if (timerCentiTensLabel != null) timerCentiTensLabel.text = (centiseconds / 10).ToString();
        if (timerCentiOnesLabel != null) timerCentiOnesLabel.text = (centiseconds % 10).ToString();
    }

    /// <summary>
    /// Calculates the final score value based on accuracy ratio minus elapsed seconds penalty.
    /// score = max(0, round(accuracyRatio * maxScore) - floor(elapsedSeconds))
    /// </summary>
    /// <param name="correct">Number of correct segments.</param>
    /// <param name="total">Total segments.</param>
    /// <param name="seconds">Total elapsed time in seconds.</param>
    /// <returns>Final calculated score integer.</returns>
    private int ComputeScore(int correct, int total, float seconds)
    {
        if (total <= 0) return 0;
        float accuracy = (float)correct / total;
        int accuracyPart = Mathf.RoundToInt(accuracy * maxScore);
        int timePenalty = Mathf.FloorToInt(seconds);
        return Mathf.Max(0, accuracyPart - timePenalty);
    }

    #endregion
}