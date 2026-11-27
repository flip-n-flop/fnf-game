using System.Collections;
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
    [SerializeField] private float jumpForce = 12.5f;

    [Range(0f, 1f)]
    [Tooltip("Multiplier applied to vertical velocity when releasing the jump button early while ascending.")]
    [SerializeField] private float jumpCutMultiplier = 0.5f;

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
    [SerializeField] private float acceleration = 35f;

    [Tooltip("How fast the player slows down when releasing input (units/s^2).")]
    [SerializeField] private float deceleration = 35f;

    [Tooltip("Multiplier applied to acceleration/deceleration while airborne.")]
    [SerializeField] private float airControlMultiplier = 1.0f;

    [Header("Gameplay Settings")]
    [Tooltip("World Y threshold (positive and negative) below/above which player dies.")]
    [SerializeField] private float fallKillThreshold = -5f;

    [Tooltip("Target X position where the player finishes the intro walk and gains control.")]
    [SerializeField] private float spawnTargetX = 6f;

    [Tooltip("Whether the character walks automatically from off-screen to spawnTargetX at the start of the level.")]
    [SerializeField] private bool playIntroWalk = true;

    [Tooltip("Distance outside the left edge of the camera screen where the intro walk begins.")]
    [SerializeField] private float introOffscreenOffset = 1.5f;

    [Tooltip("Extra distance the character walks forward automatically after the guide bar closes at level end.")]
    [SerializeField] private float outroWalkDistance = 1.8f;

    [Header("Death Animation")]
    [Tooltip("Brief freeze duration before the death pop-up begins.")]
    [SerializeField] private float deathFreezeDelay = 0.12f;

    [Tooltip("Initial upward velocity for the death pop-up arc.")]
    [SerializeField] private float deathPopUpVelocity = 12f;

    [Tooltip("Downward gravity acceleration applied during the death fall.")]
    [SerializeField] private float deathFallGravity = 35f;

    [Tooltip("Extra margin below the bottom of the camera before showing the post-death screen.")]
    [SerializeField] private float deathOffscreenMargin = 2f;

    [Tooltip("Maximum safety duration (seconds) for the death fall before showing the post-death screen.")]
    [SerializeField] private float deathMaxDuration = 2.0f;

    [Header("Guide Bar Settings")]
    [Tooltip("SpriteRenderer of the vertical tolerance guide bar (child named 'Guide').")]
    [SerializeField] private SpriteRenderer guideRenderer;

    [Tooltip("Duration in seconds for the guide bar to expand once the player gains control.")]
    [SerializeField] private float guideExpandDuration = 0.75f;

    [Tooltip("Ease-in exponent for the guide expansion (> 1 starts slowly and accelerates).")]
    [SerializeField] private float guideEaseInPower = 2.5f;

    [Tooltip("Maximum vertical half-height in world units reached by the guide bar.")]
    [SerializeField] private float guideMaxWorldRadius = 25f;

    [Tooltip("Normalized softness (0..0.9) of the top/bottom fade-out blur on the guide bar.")]
    [Range(0.05f, 0.9f)]
    [SerializeField] private float guideEdgeSoftness = 0.55f;

    [Tooltip("Strength of the blur at the top and bottom tips of the guide bar.")]
    [Range(0f, 3f)]
    [SerializeField] private float guideTipBlur = 1.5f;

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

    /// <summary>
    /// Indicates whether the player is currently allowed to control the character.
    /// Control is locked during intro walking and while the guide bar expansion animation is playing.
    /// </summary>
    public bool CanPlayerControl => !isIntroWalking && !isGuideExpanding && !hasCompletedLevel && !isDead;

    #endregion

    #region Private Fields & State

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool isGravityInvertedState;
    private bool isIntroWalking;
    private bool isGuideExpanding;
    private bool isGuideCollapsing;
    private bool isOutroWalking;
    private bool hasCompletedLevel;
    private bool isDead;
    private float outroTargetX;
    private float guideExpandTimer;
    private Material guideMaterialInstance;
    private float horizontalInput;
    private bool isGrounded;
    private bool jumpInput;
    private bool isJumpHeld;
    private bool isJumping;
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

        InitGuideBar();
    }

    /// <summary>
    /// Sets the initial physics state and ensures gravity is positive.
    /// </summary>
    private void Start()
    {
        rb.gravityScale = Mathf.Abs(rb.gravityScale);
        isGravityInvertedState = rb.gravityScale < 0f;

        if (playIntroWalk)
        {
            SetGuideProgress(0f);
            StartIntroWalk();
        }
        else
        {
            scoreController?.ResetAndStopTimer();
            StartGuideExpansion();
        }
    }

    /// <summary>
    /// Positions the player off-screen to the left and initiates the automatic walk to <see cref="spawnTargetX"/>.
    /// </summary>
    private void StartIntroWalk()
    {
        isIntroWalking = true;
        jumpInput = false;
        isJumpHeld = false;
        isJumping = false;
        gravityFlipInput = false;

        scoreController?.ResetAndStopTimer();

        float startWalkX = -2.5f;
        Camera cam = cameraController != null ? cameraController.GetComponent<Camera>() : Camera.main;
        if (cam != null)
        {
            float camCenterX = cameraController != null ? Mathf.Max(cam.transform.position.x, cameraController.minX) : cam.transform.position.x;
            float halfWidth = cam.orthographicSize * cam.aspect;
            startWalkX = (camCenterX - halfWidth) - introOffscreenOffset;
        }

        Vector3 startPos = transform.position;
        startPos.x = startWalkX;

        Physics2D.SyncTransforms();
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(startWalkX, startPos.y + 1f), Vector2.down, 5f, groundLayer);
        if (hit.collider != null && TryGetComponent(out Collider2D col))
        {
            startPos.y = hit.point.y + col.bounds.extents.y + 0.02f;
        }

        transform.position = startPos;
        rb.position = new Vector2(startWalkX, startPos.y);
        rb.linearVelocity = new Vector2(moveSpeed, 0f);
        spriteRenderer.flipX = false ^ IsGravityInverted;
    }

    /// <summary>
    /// Updates win/lose conditions, animation parameters, and flip safety timeouts per frame.
    /// </summary>
    private void Update()
    {
        UpdateGuideCollapse();
        if (isDead) return;
        UpdateGuideExpansion();
        if (hasCompletedLevel)
        {
            if (isOutroWalking) UpdateAnimator();
            return;
        }
        UpdateAnimator();
        CheckWinAndLoseConditions();
        CheckFlipTimeout();
    }

    /// <summary>
    /// Updates physics calculations including ground check, movement, jumping, flipping, and dashing.
    /// </summary>
    private void FixedUpdate()
    {
        if (isDead) return;
        if (hasCompletedLevel)
        {
            if (isOutroWalking)
            {
                CheckIfGrounded();
                HandleOutroWalk();
            }
            return;
        }
        CheckIfGrounded();
        HandleMovement();
        HandleJump();
        HandleGravityFlip();
        HandleDash();
    }

    /// <summary>
    /// LateUpdate callback ensuring the vertical guide line maintains world orientation (does not rotate or flip with character).
    /// </summary>
    private void LateUpdate()
    {
        if (guideRenderer != null)
        {
            guideRenderer.transform.rotation = Quaternion.identity;
            guideRenderer.flipX = false;
            guideRenderer.flipY = false;
        }
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
            jumpAction.action.canceled += OnJumpCanceled;
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
            jumpAction.action.canceled -= OnJumpCanceled;
            jumpAction.action.Disable();
        }

        isJumpHeld = false;
        isJumping = false;

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
    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (!CanPlayerControl) return;
        jumpInput = true;
        isJumpHeld = true;
    }

    /// <summary>
    /// Handles jump release callback for variable jump height.
    /// </summary>
    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        isJumpHeld = false;
    }

    /// <summary>
    /// Handles gravity flip input callback.
    /// </summary>
    private void OnFlipGravityPerformed(InputAction.CallbackContext ctx)
    {
        if (!CanPlayerControl) return;
        gravityFlipInput = true;
    }

    /// <summary>
    /// Handles dash input callback.
    /// </summary>
    private void OnDashPerformed(InputAction.CallbackContext ctx)
    {
        if (!CanPlayerControl) return;
        TryStartDash();
    }

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
    /// Resolves the child Guide SpriteRenderer and assigns a material instance using the <c>Custom/Sprite_PlayerGuide</c> shader.
    /// </summary>
    private void InitGuideBar()
    {
        if (guideRenderer == null)
        {
            Transform guideTr = transform.Find("Guide");
            if (guideTr != null)
            {
                guideRenderer = guideTr.GetComponent<SpriteRenderer>();
            }
        }

        if (guideRenderer == null) return;

        guideRenderer.transform.rotation = Quaternion.identity;
        guideRenderer.flipX = false;
        guideRenderer.flipY = false;

        Shader guideShader = Shader.Find("Custom/Sprite_PlayerGuide");
        if (guideShader != null)
        {
            guideMaterialInstance = new Material(guideShader);
            guideMaterialInstance.SetFloat("_MaxRadius", guideMaxWorldRadius);
            guideMaterialInstance.SetFloat("_EdgeSoftness", guideEdgeSoftness);
            guideMaterialInstance.SetFloat("_TipBlur", guideTipBlur);
            guideRenderer.material = guideMaterialInstance;
        }
    }

    /// <summary>
    /// Starts the ease-in expansion animation of the vertical guide bar from the player's position.
    /// </summary>
    private void StartGuideExpansion()
    {
        guideExpandTimer = 0f;
        isGuideExpanding = true;
        SetGuideProgress(0f);
    }

    /// <summary>
    /// Advances the guide bar expansion animation per frame using an ease-in curve.
    /// </summary>
    private void UpdateGuideExpansion()
    {
        if (!isGuideExpanding) return;

        guideExpandTimer += Time.deltaTime;
        float duration = Mathf.Max(0.05f, guideExpandDuration);
        float t = Mathf.Clamp01(guideExpandTimer / duration);
        float easedProgress = Mathf.Pow(t, Mathf.Max(1f, guideEaseInPower));

        SetGuideProgress(easedProgress);

        if (t >= 1f)
        {
            isGuideExpanding = false;
            scoreController?.StartTimer();
        }
    }

    /// <summary>
    /// Starts the reverse collapse animation of the vertical guide bar when the player finishes the level.
    /// </summary>
    private void StartGuideCollapse()
    {
        guideExpandTimer = 0f;
        isGuideExpanding = false;
        isGuideCollapsing = true;
    }

    /// <summary>
    /// Advances the guide bar collapse animation per frame and disables the controller once finished.
    /// </summary>
    private void UpdateGuideCollapse()
    {
        if (!isGuideCollapsing) return;

        guideExpandTimer += Time.deltaTime;
        float duration = Mathf.Max(0.05f, guideExpandDuration);
        float t = Mathf.Clamp01(guideExpandTimer / duration);
        float easedProgress = Mathf.Pow(1f - t, Mathf.Max(1f, guideEaseInPower));

        SetGuideProgress(easedProgress);

        if (t >= 1f)
        {
            isGuideCollapsing = false;
            SetGuideProgress(0f);
            if (!isDead)
            {
                StartOutroWalk();
            }
        }
    }

    /// <summary>
    /// Starts the short automatic walk forward once the guide bar has closed at the end of the level.
    /// </summary>
    private void StartOutroWalk()
    {
        if (outroWalkDistance <= 0.01f)
        {
            this.enabled = false;
            return;
        }

        outroTargetX = rb.position.x + outroWalkDistance;
        isOutroWalking = true;
    }

    /// <summary>
    /// Moves the character forward during the post-finish outro walk and disables the controller upon arrival.
    /// </summary>
    private void HandleOutroWalk()
    {
        if (rb.position.x >= outroTargetX)
        {
            isOutroWalking = false;
            rb.position = new Vector2(outroTargetX, rb.position.y);
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            animator.SetBool("run", false);
            animator.SetFloat("speed", 0f);
            this.enabled = false;
            return;
        }

        rb.linearVelocity = new Vector2(moveSpeed, rb.linearVelocity.y);
        spriteRenderer.flipX = false ^ IsGravityInverted;
    }

    /// <summary>
    /// Updates the guide bar shader progress and renderer visibility.
    /// </summary>
    private void SetGuideProgress(float progress)
    {
        if (guideRenderer == null) return;

        if (guideMaterialInstance != null)
        {
            float halfHeight = 51.9f;
            if (guideRenderer.sprite != null)
            {
                halfHeight = guideRenderer.sprite.bounds.extents.y * Mathf.Abs(guideRenderer.transform.lossyScale.y);
            }

            guideMaterialInstance.SetColor("_Color", Color.white);
            guideMaterialInstance.SetFloat("_HalfSpriteWorldHeight", halfHeight);
            guideMaterialInstance.SetFloat("_ExpandProgress", progress);
            guideMaterialInstance.SetFloat("_MaxRadius", guideMaxWorldRadius);
            guideMaterialInstance.SetFloat("_EdgeSoftness", guideEdgeSoftness);
            guideMaterialInstance.SetFloat("_TipBlur", guideTipBlur);
        }

        guideRenderer.enabled = progress > 0.0005f;
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

        if (transform.position.x > LevelManager.Instance.phaseEndX)
        {
            hasCompletedLevel = true;
            scoreController.StopTimer();

            rb.linearVelocity = Vector2.zero;
            horizontalInput = 0f;

            animator.SetBool("run", false);
            animator.SetFloat("speed", 0f);

            pathVerifier.FinalizeAndCheckPath();

            cameraController.EnableManualControl();

            StartGuideCollapse();
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
        if (isIntroWalking)
        {
            if (rb.position.x >= spawnTargetX)
            {
                isIntroWalking = false;
                jumpInput = false;
                isJumpHeld = false;
                isJumping = false;
                gravityFlipInput = false;
                rb.position = new Vector2(spawnTargetX, rb.position.y);
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                StartGuideExpansion();
                return;
            }
            else
            {
                rb.linearVelocity = new Vector2(moveSpeed, rb.linearVelocity.y);
                spriteRenderer.flipX = false ^ IsGravityInverted;
                return;
            }
        }

        if (isGuideExpanding)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

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
    /// Processes jump input when grounded, applying vertical velocity in direction opposite to gravity,
    /// and scales jump height by cutting vertical velocity when the jump button is released early.
    /// </summary>
    private void HandleJump()
    {
        if (!CanPlayerControl)
        {
            jumpInput = false;
            isJumpHeld = false;
            isJumping = false;
            return;
        }

        float jumpDirection = IsGravityInverted ? -1f : 1f;

        if (jumpInput && isGrounded && actionState == ActionState.None)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce * jumpDirection);
            animator.SetTrigger("jump");
            isJumping = true;
        }
        jumpInput = false;

        if (!isJumping) return;

        if (actionState != ActionState.None || rb.linearVelocity.y * jumpDirection <= 0f)
        {
            isJumping = false;
            return;
        }

        if (!isJumpHeld)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * Mathf.Clamp01(jumpCutMultiplier));
            isJumping = false;
        }
    }

    /// <summary>
    /// Handles gravity inversion initiation when triggered by input while grounded.
    /// </summary>
    private void HandleGravityFlip()
    {
        if (!CanPlayerControl)
        {
            gravityFlipInput = false;
            return;
        }

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

        if (guideRenderer != null)
        {
            guideRenderer.transform.rotation = Quaternion.identity;
            guideRenderer.flipX = false;
            guideRenderer.flipY = false;
        }

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
    /// Triggers the death animation (static idle pose, pop-up arc, fall off-screen) and then
    /// enables manual camera control and finalizes path verification to show the post-death screen.
    /// Interacts with <see cref="ScoreController"/>, <see cref="CameraController"/>, and <see cref="PathVerifier"/>.
    /// </summary>
    public void PlayerDeath()
    {
        if (isDead || hasCompletedLevel) return;
        isDead = true;

        float deathX = transform.position.x;

        scoreController?.StopTimer();
        StartGuideCollapse();

        horizontalInput = 0f;
        jumpInput = false;
        gravityFlipInput = false;
        actionState = ActionState.None;

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            rb.simulated = false;
        }

        if (animator != null)
        {
            animator.SetBool("run", false);
            animator.SetBool("fall", false);
            animator.SetBool("grounded", true);
            animator.SetFloat("speed", 0f);
            animator.Update(0f);
            animator.speed = 0f;
        }

        StartCoroutine(DeathAnimationRoutine(deathX));
    }

    private IEnumerator DeathAnimationRoutine(float deathX)
    {
        Camera cam = cameraController != null ? cameraController.GetComponent<Camera>() : Camera.main;
        float camBottomY = cam != null ? cam.transform.position.y - cam.orthographicSize : transform.position.y - 12f;
        float camTopY = cam != null ? cam.transform.position.y + cam.orthographicSize : transform.position.y + 12f;

        Vector3 pos = transform.position;
        if (pos.y < camBottomY - 0.5f)
        {
            pos.y = camBottomY - 0.5f;
            transform.position = pos;
        }
        else if (pos.y > camTopY + 0.5f)
        {
            pos.y = camTopY + 0.5f;
            transform.position = pos;
        }

        if (deathFreezeDelay > 0f)
        {
            yield return new WaitForSeconds(deathFreezeDelay);
        }

        float velY = deathPopUpVelocity;
        float exitY = camBottomY - deathOffscreenMargin;
        float elapsed = 0f;

        while (transform.position.y > exitY && elapsed < deathMaxDuration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            velY -= deathFallGravity * dt;

            pos = transform.position;
            pos.y += velY * dt;
            transform.position = pos;

            yield return null;
        }

        cameraController?.EnableManualControlWithRightLimit(deathX);
        pathVerifier?.FinalizeAndCheckPath(deathX);
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

        float effectiveInput = CanPlayerControl ? horizontalInput : ((isIntroWalking || isOutroWalking) ? 1f : 0f);
        float speedX = useInputForRunSpeed
            ? Mathf.Abs(effectiveInput) * moveSpeed
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

    private void OnDestroy()
    {
        if (guideMaterialInstance != null)
        {
            Destroy(guideMaterialInstance);
        }
    }

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