using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the player character's movement, jumping, gravity inversion, and dashing mechanics.
/// Interacts with Unity <see cref="InputSystem"/>, <see cref="Rigidbody2D"/>, <see cref="Animator"/>, <see cref="SpriteRenderer"/>,
/// <see cref="PathVerifier"/> for final path check, <see cref="CameraController"/> for camera target tracking,
/// <see cref="HintController"/> for level hints, <see cref="ScoreController"/> for timer management,
/// and <see cref="LevelManager"/> for level boundaries.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    #region Nested Types

    /// <summary>
    /// Represents the exclusive action states for special player movements (flipping or dashing).
    /// </summary>
    private enum ActionState { None, Flipping, Dashing }

    #endregion

    #region Inspector Fields

    [Header("Movement Settings")]
    [Tooltip("Base horizontal movement speed (units/second).")]
    [SerializeField] private float moveSpeed = 5.0f;

    [Tooltip("Vertical impulse force applied when jumping.")]
    [SerializeField] private float jumpForce = 12.0f;

    [Header("Ground Check Settings")]
    [Tooltip("Radius of the overlap sphere used for ground detection.")]
    [SerializeField] private float groundCheckRadius = 0.2f;

    [Tooltip("Transform position marking the center of ground detection.")]
    [SerializeField] private Transform groundCheckPoint;

    [Tooltip("Layer mask specifying ground collision layers.")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Animation Settings")]
    [Tooltip("Multiplier applied to the normalized speed to feed the runSpeed parameter.")]
    [SerializeField] private float runAnimSpeedMultiplier = 1f;

    [Tooltip("Damping time for smoothing the runSpeed parameter updates.")]
    [SerializeField] private float runAnimDampTime = 0.05f;

    [Tooltip("Minimum speed multiplier when in run state to avoid animation sticking at start.")]
    [Range(0f, 2f)]
    [SerializeField] private float minRunAnimSpeed = 0.3f;

    [Tooltip("If true, base run animation speed on input target speed (immediate); otherwise use rigidbody velocity (lagged).")]
    [SerializeField] private bool useInputForRunSpeed = true;

    [Header("Movement Smoothing")]
    [Tooltip("How fast the player accelerates toward target speed (units/s^2).")]
    [SerializeField] private float acceleration = 25f;

    [Tooltip("How fast the player slows down when releasing input (units/s^2).")]
    [SerializeField] private float deceleration = 35f;

    [Tooltip("Multiplier applied to acceleration/deceleration while airborne.")]
    [SerializeField] private float airControlMultiplier = 0.7f;

    [Header("Gameplay Settings")]
    [Tooltip("World Y threshold (positive and negative) below/above which player dies.")]
    [SerializeField] private float fallKillThreshold = -25f;

    [Header("Dash Settings")]
    [Tooltip("Horizontal dash speed applied during a dash.")]
    [SerializeField] private float dashSpeed = 18f;

    [Tooltip("Dash duration in seconds.")]
    [SerializeField] private float dashDuration = 0.15f;

    [Tooltip("Cooldown between dashes in seconds.")]
    [SerializeField] private float dashCooldown = 0.5f;

    [Range(0f, 1f)]
    [Tooltip("Inertia factor kept right after dash ends (0 = snap to input speed, 1 = keep full dash speed).")]
    [SerializeField] private float postDashInertiaFactor = 0.5f;

    [Header("Object References")]
    [SerializeField] private PathVerifier pathVerifier;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private HintController hintController;
    [SerializeField] private ScoreController scoreController;

    [Header("Input System")]
    [Tooltip("Reference to the Move action (1D Axis/float).")]
    [SerializeField] private InputActionReference moveAction;

    [Tooltip("Reference to the Jump action (Button).")]
    [SerializeField] private InputActionReference jumpAction;

    [Tooltip("Reference to the Flip Gravity action (Button).")]
    [SerializeField] private InputActionReference flipGravityAction;

    [Tooltip("Reference to the Dash action (Button).")]
    [SerializeField] private InputActionReference dashAction;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip footstepSound;

    [Tooltip("Pitch variation for footstep sounds to avoid repetitive audio playback.")]
    [SerializeField] private float pitchVariation = 0.1f;

    [Header("Safety")]
    [Tooltip("Maximum duration allowed for a flip operation before it automatically resets.")]
    [SerializeField] private float flipTimeout = 1f;

    #endregion

    #region Properties

    /// <summary>
    /// Logical gravity inversion state (independent from temporary physics tweaks like gravityScale = 0 during dash).
    /// </summary>
    public bool IsGravityInverted => isGravityInvertedState;

    /// <summary>
    /// Indicates whether the player is currently executing a gravity flip animation.
    /// </summary>
    private bool isFlipping => actionState == ActionState.Flipping;

    /// <summary>
    /// Indicates whether the player is currently performing a dash move.
    /// </summary>
    private bool isDashing => actionState == ActionState.Dashing;

    #endregion

    #region Private Fields & State

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool isGravityInvertedState;
    private float horizontalInput;
    private bool isGrounded;
    private bool jumpInput;
    private bool gravityFlipInput;
    private ActionState actionState = ActionState.None;
    private float flipStartTime;
    private float dashEndTime;
    private float nextDashTime;
    private float dashDir = 1f;
    private float preDashGravityScale = 1f;
    private RigidbodyConstraints2D preDashConstraints;
    private float dashLockY;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Initializes local component references and finds missing manager references.
    /// Interacts with <see cref="ScoreController"/>, <see cref="PathVerifier"/>, <see cref="CameraController"/>, and <see cref="HintController"/>.
    /// </summary>
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        scoreController = GetOrFind(scoreController, "ScoreController not found!");
        pathVerifier = GetOrFind(pathVerifier, "PathVerifier not found!");
        cameraController = GetOrFind(cameraController, "CameraController not found!");
        hintController = GetOrFind(hintController, "HintController not found!");
    }

    /// <summary>
    /// Sets the initial physics state and ensures gravity is positive.
    /// </summary>
    private void Start()
    {
        rb.gravityScale = Mathf.Abs(rb.gravityScale);
        isGravityInvertedState = rb.gravityScale < 0f;
    }

    /// <summary>
    /// Updates win/lose conditions, animation parameters, and flip safety timeouts per frame.
    /// </summary>
    private void Update()
    {
        UpdateAnimator();
        CheckWinAndLoseConditions();
        CheckFlipTimeout();
    }

    /// <summary>
    /// Updates physics calculations including ground check, movement, jumping, flipping, and dashing.
    /// </summary>
    private void FixedUpdate()
    {
        CheckIfGrounded();
        HandleMovement();
        HandleJump();
        HandleGravityFlip();
        HandleDash();
    }

    #endregion

    #region Input Subscriptions

    /// <summary>
    /// Subscribes input action callbacks using Unity's <see cref="InputSystem"/>.
    /// </summary>
    private void OnEnable()
    {
        if (moveAction?.action != null)
        {
            moveAction.action.performed += OnMovePerformed;
            moveAction.action.canceled += OnMoveCanceled;
            moveAction.action.Enable();
        }

        if (jumpAction?.action != null)
        {
            jumpAction.action.performed += OnJumpPerformed;
            jumpAction.action.Enable();
        }

        if (flipGravityAction?.action != null)
        {
            flipGravityAction.action.performed += OnFlipGravityPerformed;
            flipGravityAction.action.Enable();
        }

        if (dashAction?.action != null)
        {
            dashAction.action.performed += OnDashPerformed;
            dashAction.action.Enable();
        }
    }

    /// <summary>
    /// Unsubscribes input action callbacks.
    /// </summary>
    private void OnDisable()
    {
        if (moveAction?.action != null)
        {
            moveAction.action.performed -= OnMovePerformed;
            moveAction.action.canceled -= OnMoveCanceled;
            moveAction.action.Disable();
        }

        if (jumpAction?.action != null)
        {
            jumpAction.action.performed -= OnJumpPerformed;
            jumpAction.action.Disable();
        }

        if (flipGravityAction?.action != null)
        {
            flipGravityAction.action.performed -= OnFlipGravityPerformed;
            flipGravityAction.action.Disable();
        }

        if (dashAction?.action != null)
        {
            dashAction.action.performed -= OnDashPerformed;
            dashAction.action.Disable();
        }
    }

    /// <summary>
    /// Handles horizontal movement input performing callback.
    /// </summary>
    private void OnMovePerformed(InputAction.CallbackContext ctx) => horizontalInput = ctx.ReadValue<float>();

    /// <summary>
    /// Handles horizontal movement input canceled callback.
    /// </summary>
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => horizontalInput = 0f;

    /// <summary>
    /// Handles jump input callback.
    /// </summary>
    private void OnJumpPerformed(InputAction.CallbackContext ctx) => jumpInput = true;

    /// <summary>
    /// Handles gravity flip input callback.
    /// </summary>
    private void OnFlipGravityPerformed(InputAction.CallbackContext ctx) => gravityFlipInput = true;

    /// <summary>
    /// Handles dash input callback.
    /// </summary>
    private void OnDashPerformed(InputAction.CallbackContext ctx) => TryStartDash();

    #endregion

    #region Helper Methods

    /// <summary>
    /// Finds object of type T if serialised reference is missing.
    /// </summary>
    private T GetOrFind<T>(T reference, string errorMessage) where T : Object
    {
        if (reference == null)
            reference = FindAnyObjectByType<T>();

        if (reference == null)
            Debug.LogError(errorMessage);

        return reference;
    }

    /// <summary>
    /// Resets flipping state if flip duration exceeds timeout threshold.
    /// </summary>
    private void CheckFlipTimeout()
    {
        if (actionState == ActionState.Flipping && Time.time - flipStartTime > flipTimeout)
        {
            actionState = ActionState.None;
        }
    }

    /// <summary>
    /// Performs ground detection check using Physics2D overlap circle.
    /// </summary>
    private void CheckIfGrounded()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);
    }

    /// <summary>
    /// Checks fall out-of-bounds death conditions and stage finish triggers.
    /// Interacts with <see cref="LevelManager"/>, <see cref="ScoreController"/>, <see cref="PathVerifier"/>, and <see cref="CameraController"/>.
    /// </summary>
    private void CheckWinAndLoseConditions()
    {
        float limit = Mathf.Abs(fallKillThreshold);
        float y = transform.position.y;

        if (y < -limit || y > limit)
        {
            PlayerDeath();
        }

        if (transform.position.x > LevelManager.Instance.levelEndX + 1)
        {
            scoreController.StopTimer();

            rb.linearVelocity = Vector2.zero;
            horizontalInput = 0f;

            animator.SetBool("run", false);
            animator.SetFloat("speed", 0f);

            pathVerifier.FinalizeAndCheckPath();

            cameraController.EnableManualControl();

            this.enabled = false;
        }
    }

    #endregion

    #region Movement & Mechanics

    /// <summary>
    /// Applies horizontal movement based on player input.
    /// Overrides movement during dash to lock vertical position and maintain dash speed.
    /// </summary>
    private void HandleMovement()
    {
        if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);
            rb.position = new Vector2(rb.position.x, dashLockY);
            FlipSprite();
            return;
        }

        float currentX = rb.linearVelocity.x;
        bool hasInput = Mathf.Abs(horizontalInput) > 0.01f;
        float accel = hasInput ? acceleration : deceleration;
        if (!isGrounded) accel *= airControlMultiplier;

        float targetX = horizontalInput * moveSpeed;
        float newX = Mathf.MoveTowards(currentX, targetX, accel * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
        FlipSprite();
    }

    /// <summary>
    /// Updates sprite flip direction based on movement input, dash direction, and gravity inversion state.
    /// </summary>
    private void FlipSprite()
    {
        if (isFlipping) return;

        if (isDashing)
        {
            spriteRenderer.flipX = (dashDir < 0f) ^ IsGravityInverted;
            return;
        }

        if (Mathf.Abs(horizontalInput) < 0.1f) return;
        bool wantsToGoLeft = horizontalInput < 0;
        spriteRenderer.flipX = wantsToGoLeft ^ IsGravityInverted;
    }

    /// <summary>
    /// Processes jump input when grounded, applying vertical velocity in direction opposite to gravity.
    /// </summary>
    private void HandleJump()
    {
        if (jumpInput && isGrounded && actionState == ActionState.None)
        {
            float jumpDirection = IsGravityInverted ? -1f : 1f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce * jumpDirection);
            animator.SetTrigger("jump");
        }
        jumpInput = false;
    }

    /// <summary>
    /// Handles gravity inversion initiation when triggered by input while grounded.
    /// </summary>
    private void HandleGravityFlip()
    {
        if (gravityFlipInput && isGrounded && actionState == ActionState.None)
        {
            actionState = ActionState.Flipping;
            flipStartTime = Time.time;

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            jumpInput = false;

            rb.gravityScale *= -1;
            isGravityInvertedState = !isGravityInvertedState;

            animator.SetTrigger("flip");
        }
        gravityFlipInput = false;
    }

    /// <summary>
    /// Animation Event method executed during Flip animation to rotate character transform visually.
    /// </summary>
    public void ExecuteFlipVisuals()
    {
        transform.Rotate(0f, 0f, 180f);
        spriteRenderer.flipX = !spriteRenderer.flipX;

        actionState = ActionState.None;
    }

    /// <summary>
    /// Attempts to initiate a dash action if cooldown has elapsed and player is not flipping.
    /// </summary>
    private void TryStartDash()
    {
        if (Time.time < nextDashTime) return;
        if (actionState == ActionState.Flipping) return;

        if (Mathf.Abs(horizontalInput) > 0.01f)
            dashDir = Mathf.Sign(horizontalInput);
        else if (Mathf.Abs(rb.linearVelocity.x) > 0.05f)
            dashDir = Mathf.Sign(rb.linearVelocity.x);
        else
        {
            bool facingLeft = spriteRenderer.flipX ^ IsGravityInverted;
            dashDir = facingLeft ? -1f : 1f;
        }

        dashLockY = rb.position.y;
        preDashGravityScale = rb.gravityScale;
        preDashConstraints = rb.constraints;
        rb.gravityScale = 0f;
        rb.constraints = preDashConstraints | RigidbodyConstraints2D.FreezePositionY;
        rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);

        actionState = ActionState.Dashing;
        dashEndTime = Time.time + dashDuration;
        nextDashTime = Time.time + dashCooldown;
    }

    /// <summary>
    /// Manages active dash state and restores original physics settings upon dash conclusion.
    /// </summary>
    private void HandleDash()
    {
        if (!isDashing) return;
        if (Time.time >= dashEndTime)
        {
            actionState = ActionState.None;
            rb.gravityScale = preDashGravityScale;
            rb.constraints = preDashConstraints;

            float currentX = rb.linearVelocity.x;
            float targetX = horizontalInput * moveSpeed;
            float newX = Mathf.Lerp(currentX, targetX, 1f - Mathf.Clamp01(postDashInertiaFactor));
            rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
        }
    }

    #endregion

    #region Collision & Death

    /// <summary>
    /// Triggers death sequence by stopping timers, enabling manual camera control, and finalizing path verification.
    /// Interacts with <see cref="ScoreController"/>, <see cref="CameraController"/>, and <see cref="PathVerifier"/>.
    /// </summary>
    private void PlayerDeath()
    {
        scoreController.StopTimer();

        cameraController.EnableManualControlWithRightLimit(transform.position.x);

        pathVerifier.FinalizeAndCheckPath(transform.position.x);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Collision detection handler. Triggers player death upon colliding with objects tagged "Enemy".
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            PlayerDeath();
        }
    }

    /// <summary>
    /// Trigger detection handler. Triggers player death upon entering trigger colliders tagged "Enemy".
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            PlayerDeath();
        }
    }

    #endregion

    #region Animation

    /// <summary>
    /// Updates animator parameter flags for running, ground state, falling, and normalized speed.
    /// </summary>
    private void UpdateAnimator()
    {
        bool isRunning = Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        animator.SetBool("run", isRunning);
        animator.SetBool("grounded", isGrounded);

        float gravitySign = Mathf.Sign(rb.gravityScale);
        bool isFalling = !isGrounded && (rb.linearVelocity.y * gravitySign) < 0f;
        animator.SetBool("fall", isFalling);

        float speedX = useInputForRunSpeed
            ? Mathf.Abs(horizontalInput) * moveSpeed
            : Mathf.Abs(rb.linearVelocity.x);
        float normalized = moveSpeed > 0f ? speedX / moveSpeed : 0f;
        float target = normalized * Mathf.Max(0f, runAnimSpeedMultiplier);
        target = isRunning ? Mathf.Max(target, minRunAnimSpeed) : 0f;
        animator.SetFloat("speed", target, Mathf.Max(0f, runAnimDampTime), Time.deltaTime);
    }

    #endregion

    #region Audio

    /// <summary>
    /// Plays footstep audio with pitch variation. Intended to be called via Animation Events.
    /// </summary>
    public void PlayFootstepSound()
    {
        audioSource.pitch = Random.Range(1f - pitchVariation, 1f + pitchVariation);

        audioSource.PlayOneShot(footstepSound);
    }

    #endregion

    #region Debug & Gizmos

    /// <summary>
    /// Draws ground detection gizmo in Scene view when object is selected.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
    }

    #endregion
}