using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Manages custom JSON level file uploads in WebGL builds using native JavaScript interop.
/// Interacts with <see cref="UIDocument"/> for error feedback, <see cref="UploadedLevelJson"/> for storing uploaded JSON data,
/// <see cref="MenuManager"/> to clear static resource references, and <see cref="SceneManager"/> to launch the target level.
/// </summary>
public class UploadMenuManager : MonoBehaviour
{
    #region Fields & Properties

    [Header("Scene Navigation")]
    [Tooltip("Name of the scene loaded after uploading a custom JSON level.")]
    [SerializeField] private string customSceneName = "Custom";

    /// <summary>
    /// Visual element displaying error feedback when JSON upload fails or is empty.
    /// </summary>
    private VisualElement errorFeedback;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Retrieves attached <see cref="UIDocument"/> and initializes the error feedback UI container.
    /// </summary>
    private void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();
        if (uiDocument != null)
        {
            errorFeedback = uiDocument.rootVisualElement.Q<VisualElement>("ErrorFeedback");

            if (errorFeedback != null)
                errorFeedback.style.display = DisplayStyle.None;
        }
    }

    #endregion

    #region WebGL Interop & API

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// Native JavaScript method call to open the WebGL file picker dialog.
    /// </summary>
    [DllImport("__Internal")]
    private static extern void UploadJSON(string objectName, string callbackMethod);
#endif

    /// <summary>
    /// Triggered when the upload button is clicked. Invokes JavaScript file browser in WebGL.
    /// </summary>
    public void OnClickUpload()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        UploadJSON(gameObject.name, nameof(OnJSONReceived));
#else
        Debug.LogWarning("File picker only works in WebGL build. Use the Inspector for testing.");
#endif
    }

    /// <summary>
    /// JavaScript callback invoked when JSON file contents are read from file picker.
    /// Interacts with <see cref="UploadedLevelJson.Content"/>, <see cref="MenuManager.LevelToLoadJSON"/>, and <see cref="SceneManager"/>.
    /// </summary>
    /// <param name="jsonContent">Raw string contents of the uploaded JSON level file.</param>
    public void OnJSONReceived(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            if (errorFeedback != null)
                errorFeedback.style.display = DisplayStyle.Flex;

            return;
        }

        if (errorFeedback != null)
            errorFeedback.style.display = DisplayStyle.None;

        UploadedLevelJson.Content = jsonContent;
        MenuManager.LevelToLoadJSON = "";
        LoadingScreenManager.Load(customSceneName);
    }

    #endregion
}