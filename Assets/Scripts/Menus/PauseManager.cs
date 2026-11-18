using FlipNflop.Commons.Records;
using FlipNflop.Gla;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Manages GameObject-based pause menu activation and scene navigation.
/// Interacts with <see cref="SceneManager"/> for loading the menu scene, <see cref="Keyboard"/> for toggle keys, and controls <see cref="Time.timeScale"/>.
/// </summary>
public class PauseManager : MonoBehaviour
{
    #region Fields & Properties

    [Header("UI & Navigation Settings")]
    [Tooltip("Transform container of the pause menu UI element.")]
    public Transform pauseMenu;

    [Tooltip("Name of the scene loaded when exiting to menu.")]
    [FormerlySerializedAs("nomeMenu")]
    [SerializeField] private string menuSceneName = "MainMenu";

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Listens for key inputs (P or Escape) to toggle pause state and pause time scale.
    /// Interacts with Unity's <see cref="Keyboard"/> API.
    /// </summary>
    private void Update()
    {
        if (Keyboard.current != null && (Keyboard.current.pKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            if (pauseMenu != null && !pauseMenu.gameObject.activeSelf)
            {
                pauseMenu.gameObject.SetActive(true);
                Time.timeScale = 0f;
            }
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Resumes normal time scale and loads the configured menu scene via <see cref="SceneManager"/>.
    /// </summary>
    public void OpenMenuScene()
    {
        GlaServices.Instance?.Collector?.Abandon(EndCause.Exit);
        SceneManager.LoadScene(menuSceneName);
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Legacy alias for OpenMenuScene.
    /// </summary>
    public void irMenu() => OpenMenuScene();

    /// <summary>
    /// Deactivates the pause menu overlay and resumes normal time scale.
    /// </summary>
    public void ClosePauseMenu()
    {
        if (pauseMenu != null)
            pauseMenu.gameObject.SetActive(false);

        Time.timeScale = 1f;
    }

    /// <summary>
    /// Legacy alias for ClosePauseMenu.
    /// </summary>
    public void FecharPause() => ClosePauseMenu();

    #endregion
}
