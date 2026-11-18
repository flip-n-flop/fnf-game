using FlipNflop.Commons.PhaseRules;
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

    private Label timerLabel;
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
    /// Queries the TimerText label from the UIDocument if assigned.
    /// </summary>
    private void InitUI()
    {
        if (uiDocument != null)
        {
            timerLabel = uiDocument.rootVisualElement.Q<Label>("TimerText");
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
    /// The returned score is the one shown on the result screen; <see cref="PathVerifier"/> puts it in the validation result.
    /// </summary>
    /// <param name="correctSegments">Number of path segments the player drew correctly.</param>
    /// <param name="gabaritoTotal">Total number of segments in the reference path.</param>
    /// <param name="success">Whether the overall level attempt was successful.</param>
    /// <returns>The final score.</returns>
    public int ReportResult(int correctSegments, int gabaritoTotal, bool success)
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

        return score;
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Updates the UI Toolkit Label with formatted MM:SS elapsed time.
    /// </summary>
    private void UpdateTimerUI()
    {
        if (timerLabel == null) return;
        int m = Mathf.FloorToInt(elapsedTime / 60f);
        int s = Mathf.FloorToInt(elapsedTime % 60f);
        timerLabel.text = $"{m:00}:{s:00}";
    }

    /// <summary>
    /// Calculates the final score value based on accuracy ratio minus elapsed seconds penalty.
    /// score = max(0, round(accuracyRatio * maxScore) - floor(elapsedSeconds)), and 0 without segments.
    /// Delegates to <see cref="ScoreRules.Compute"/> of the shared library FlipNflopCommons, with the serialized <see cref="maxScore"/>.
    /// </summary>
    /// <param name="correct">Number of correct segments.</param>
    /// <param name="total">Total segments.</param>
    /// <param name="seconds">Total elapsed time in seconds.</param>
    /// <returns>Final calculated score integer.</returns>
    private int ComputeScore(int correct, int total, float seconds)
    {
        return ScoreRules.Compute(correct, total, seconds, maxScore);
    }

    #endregion
}