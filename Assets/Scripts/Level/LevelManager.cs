using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Singleton manager holding level configuration settings (diagram bounds, clock steps) and scene navigation parameters.
/// Referenced globally by <see cref="LevelJsonLoader"/> and player movement/path verification components.
/// </summary>
public class LevelManager : MonoBehaviour
{
    #region Singleton & Serialized Fields

    /// <summary>
    /// Global static instance of the <see cref="LevelManager"/> singleton.
    /// </summary>
    public static LevelManager Instance { get; private set; }

    [Header("Level Settings")]
    [Tooltip("Logical length of the diagram (last clock edge / end of signals).")]
    public float diagramEndX = 25f;

    [Tooltip("Extra slack space after the diagram to allow processing the final transition before ending.")]
    public float phaseSlackTiles = 3f;

    [Tooltip("Total playable length (diagramEndX + phaseSlackTiles). Used in phase completion checks.")]
    public float phaseEndX = 28f;

    [Tooltip("Legacy value representing the logical end of the diagram (without slack). Used by diagram-related systems.")]
    public float levelEndX = 25f;

    [Tooltip("Clock step interval along the X axis (distance between clock lines / sampling interval).")]
    public float clockStepX = 6f;

    [Header("Navigation")]
    [SerializeField] private string nextLevel;
    [SerializeField] private string menu;

    [FormerlySerializedAs("levelAtual")]
    [SerializeField] private string currentLevel;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Enforces singleton instance integrity and calculates default level boundary positions.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        phaseEndX = diagramEndX + phaseSlackTiles;
        levelEndX = diagramEndX;
    }

    #endregion
}