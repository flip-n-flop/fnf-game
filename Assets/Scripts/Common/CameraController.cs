using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls camera movement, supporting automatic player tracking and manual horizontal control.
/// Interacts with <see cref="LevelManager"/> to clamp camera movement within valid level boundaries.
/// </summary>
public class CameraController : MonoBehaviour
{
    #region Enums

    /// <summary>
    /// Operating modes for camera movement control.
    /// </summary>
    private enum CameraMode
    {
        FollowPlayer,
        ManualControl
    }

    #endregion

    #region Serialized Fields

    [Header("Follow Configuration")]
    [Tooltip("Target transform that the camera will follow.")]
    public Transform target;

    [Tooltip("Smooth damping factor for camera tracking movement.")]
    public float smoothSpeed = 0.125f;

    [Tooltip("Offset position relative to the target transform.")]
    public Vector3 offset;

    [Tooltip("Horizontal viewport position (0 = left edge, 0.5 = center) where the camera keeps the target.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float targetViewportX = 0.4f;

    [Header("Manual Control Configuration")]
    [Tooltip("Speed at which the camera moves in manual mode.")]
    public float manualMoveSpeed = 10f;

    [Header("Level Limits")]
    [Tooltip("The minimum X position where the camera stops at the start of the level.")]
    public float minX;

    [Tooltip("Extra X padding beyond the camera position at phaseEndX.")]
    public float endPadding = 0f;

    #endregion

    #region Private Fields

    /// <summary>
    /// Calculated maximum horizontal position for the camera.
    /// </summary>
    private float maxX;

    /// <summary>
    /// Current active camera operating mode.
    /// </summary>
    private CameraMode currentMode;

    /// <summary>
    /// Reference to the attached Camera component.
    /// </summary>
    private Camera cam;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Caches component references on awake.
    /// </summary>
    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    /// <summary>
    /// Initializes default mode and calculates camera level boundary limits using <see cref="LevelManager"/>.
    /// </summary>
    private void Start()
    {
        currentMode = CameraMode.FollowPlayer;

        if (LevelManager.Instance != null)
        {
            float halfScreenWidth = cam.orthographicSize * cam.aspect;
            float levelRight = LevelManager.Instance.levelEndX + 10f;

            float computedMax = (levelRight - halfScreenWidth) + endPadding;
            maxX = Mathf.Max(minX, computedMax);
        }
        else
        {
            Debug.LogError("LevelManager not found in the scene!");
        }
    }

    /// <summary>
    /// Updates camera position each frame based on the active camera mode.
    /// </summary>
    private void LateUpdate()
    {
        if (currentMode == CameraMode.FollowPlayer)
        {
            FollowPlayer();
        }
        else if (currentMode == CameraMode.ManualControl)
        {
            HandleManualControl();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Switches the camera to manual input control mode.
    /// </summary>
    public void EnableManualControl()
    {
        currentMode = CameraMode.ManualControl;
    }

    /// <summary>
    /// Enables manual control and locks the right boundary limit at the current camera position.
    /// Prevents the camera from moving further right upon player death.
    /// </summary>
    /// <param name="rightLimitWorldX">World X position for the right boundary limit.</param>
    public void EnableManualControlWithRightLimit(float rightLimitWorldX)
    {
        float currentCenterX = transform.position.x;
        maxX = Mathf.Max(minX, currentCenterX);

        currentMode = CameraMode.ManualControl;

        Vector3 pos = transform.position;
        pos.x = ClampCameraX(pos.x);
        transform.position = pos;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Clamps the camera X position based on current min/max boundaries.
    /// </summary>
    /// <param name="x">The unclamped X coordinate.</param>
    /// <returns>The clamped X coordinate within level bounds.</returns>
    private float ClampCameraX(float x)
    {
        return Mathf.Clamp(x, minX, maxX);
    }

    /// <summary>
    /// Smoothly interpolates the camera position towards the target position.
    /// </summary>
    private void FollowPlayer()
    {
        if (target != null)
        {
            float halfScreenWidth = cam != null ? cam.orthographicSize * cam.aspect : 0f;
            float viewportOffsetX = (0.5f - targetViewportX) * (2f * halfScreenWidth);
            Vector3 desiredPosition = new Vector3(target.position.x + offset.x + viewportOffsetX, transform.position.y, transform.position.z);
            desiredPosition.x = ClampCameraX(desiredPosition.x);
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
            transform.position = smoothedPosition;
        }
    }

    /// <summary>
    /// Handles horizontal manual camera movement based on keyboard input.
    /// Interacts with Unity's <see cref="Keyboard"/> API.
    /// </summary>
    private void HandleManualControl()
    {
        float horizontalInput = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                horizontalInput += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                horizontalInput -= 1f;
        }

        Vector3 movement = new Vector3(horizontalInput * manualMoveSpeed * Time.deltaTime, 0, 0);
        Vector3 newPosition = transform.position + movement;
        newPosition.x = ClampCameraX(newPosition.x);
        transform.position = newPosition;
    }

    #endregion
}