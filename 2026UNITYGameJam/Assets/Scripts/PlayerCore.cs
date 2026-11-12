using UnityEngine;
using UnityEngine.InputSystem;
using static Unity.Collections.Unicode;
using DG.Tweening;

public enum MovementState
{
    Ground,
    Slope,
    Air,
    Wallrunning
}

public class PlayerCore : MonoBehaviour
{
    [Header("Camera and look")]
    [SerializeField] private float sensX;
    [SerializeField] private float sensY;
    [SerializeField] private Transform cameraHolder;
    [SerializeField, Min(0f)] private float wallRunFovChange = 5f;
    [SerializeField, Min(0f)] private float wallRunTilt = 5f;
    [SerializeField] private Camera playerCamera;
    private float fovBeforeWallRun;
    private float cameraRoll;


    [Header("Bace Movement")]
    [SerializeField] private float groundWalkAcc = 15f;
    [SerializeField] private float groundSprintAcc = 25f;
    [SerializeField] private float groundDeceleration = 10f;
    [SerializeField] private float maxGroundWalkSpeed = 6f;
    [SerializeField] private float maxGroundSprintSpeed = 15f;
    [SerializeField] private float groundFriction = 10f;
    [SerializeField] private float jumpForce = 0.75f;
    [SerializeField] private float maxAirSpeed = 10f;
    [SerializeField] private float airAcc = 3f;
    [SerializeField] private float airControl = 5f;
    private Rigidbody rb;
    private Vector3 moveDir;
    private float xRot;
    private bool jumpHeld;
    private bool isSprinting;
    private MovementState movementState = MovementState.Air;
    private bool jumpAscending;


    [Header("Wallrunning")]
    [SerializeField] LayerMask wallLayer;
    [SerializeField] private float wallRunForce;
    [SerializeField] private float maxWallRunTime;
    [SerializeField, Min(0f)] private float maxWallRunSpeed = 15f;
    [SerializeField] private float wallClimbSpeed = 5f;
    [SerializeField, Min(0f)] private float wallAdhesionForce = 20f;
    [Tooltip("Zero holds altitude. One uses normal gravity.")]
    [SerializeField, Range(0f, 1f)] private float wallRunGravityScale;
    private float wallRunTimer;
    private Vector3 wallTangent;
    private bool gravityBeforeWallRun;
    private bool upwardsRunning;
    private bool downwardsRunning;


    [Header("Wall running detection")]
    [SerializeField] private float wallCheckDist;
    [SerializeField] private float minJumpHeight;
    [SerializeField, Min(0f)] private float exitWallTime = 0.25f;
    [SerializeField, Min(0f)] private float wallContactGraceTime = 0.08f;
    private RaycastHit wallHit;
    private RaycastHit exitedWallHit;
    private bool hasWallContact;
    private float wallContactTimer;
    private float exitWallTimer;
    private Collider playerCollider;

    [Header("Wallrunning jumping")]
    [SerializeField] private float wallJumpUpForce = 5f;
    [SerializeField] private float wallJumpSideForce = 5f;
    [SerializeField, Min(0f)] private float wallJumpForwardForce = 2f;
    [SerializeField, Min(1f)] private float wallJumpTransferMultiplier = 1.6f;
    [SerializeField, Min(0f)] private float wallJumpSteeringLockTime = 0.15f;
    private float wallJumpBufferTimer;
    private float wallJumpSteeringTimer;


    [Header("Ground detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 1.5f;
    [SerializeField] private float maxSlopeAngle = 50f;
    [SerializeField] private float groundProbeRadius = 0.3f;
    private Vector3 groundNormal = Vector3.up;

    [Header("Health")]
    [SerializeField, Min(1)] private int maxHealth = 100;
    private int health;

    public int Health => health;
    public int MaxHealth => maxHealth;
    public event System.Action HealthChanged;
    internal Transform AimTransform => playerCamera != null ? playerCamera.transform : transform;

    private void Awake()
    {
        SetHealth(maxHealth);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        fovBeforeWallRun = playerCamera.fieldOfView;

        // Prevent the player from tipping over
        rb.freezeRotation = true;
    }

    private void FixedUpdate()
    {
        // Timers
        exitWallTimer = Mathf.Max(0f, exitWallTimer - Time.fixedDeltaTime);
        wallContactTimer = Mathf.Max(0f, wallContactTimer - Time.fixedDeltaTime);
        wallJumpSteeringTimer = Mathf.Max(0f, wallJumpSteeringTimer - Time.fixedDeltaTime);

        // Hnadle the current movement state
        Vector3 velocity = rb.linearVelocity;
        Vector3 desiredDir = transform.forward * moveDir.z + transform.right * moveDir.x;

        if (desiredDir.sqrMagnitude > 1f) desiredDir.Normalize();
        MovementState nextState = ResolveMovementState(velocity, desiredDir);

        // Check anyhting that can change the state and save it to the next state
        bool grounded = nextState == MovementState.Ground || nextState == MovementState.Slope;
        bool canJump = jumpHeld && grounded;

        // A fresh press can launch even if steering away just ended the run
        bool canWallJump = wallJumpBufferTimer > 0f && !grounded &&
            (nextState == MovementState.Air || movementState == MovementState.Wallrunning) &&
            (hasWallContact || wallContactTimer > 0f);

        wallJumpBufferTimer = canWallJump || grounded
            ? 0f : Mathf.Max(0f, wallJumpBufferTimer - Time.fixedDeltaTime);

        if (grounded)
        {
            wallContactTimer = 0f;
            exitWallTimer = 0f;
            wallJumpSteeringTimer = 0f;
        }

        if (canJump || canWallJump)
        {
            velocity.y = 0f;
            nextState = MovementState.Air;
        }

        bool startingWallRun = nextState == MovementState.Wallrunning &&
            movementState != MovementState.Wallrunning;

        // Update the movement state if anything above changed it
        ChangeMovementState(nextState);

        # region Actually do the thing that will change the state
        if (startingWallRun) StartWallRun(ref velocity, desiredDir);

        // Special overrides for movement (like wall jumping) otherwise apply regula movement logic
        if (canWallJump)
        {
            Vector3 normal = HorizontalWallNormal(wallHit);

            // Cancel inward drift before launching and keep the speed along the wall
            velocity -= normal * Mathf.Min(0f, Vector3.Dot(velocity, normal));
        }
        else
        {
            ApplyMovement(ref velocity, desiredDir);
        }
        rb.linearVelocity = velocity;

        if (canJump) Jump();
        if (canWallJump) WallJump(desiredDir);
        #endregion
    }

    /// <summary>
    /// Obj cleanup
    /// </summary>
    private void OnDisable()
    {
        ChangeMovementState(MovementState.Air);
        jumpAscending = false;
        jumpHeld = false;
        wallJumpBufferTimer = 0f;
        exitWallTimer = 0f;
        wallContactTimer = 0f;
        wallJumpSteeringTimer = 0f;
    }

    #region Movement state machine
    /// <summary>
    /// Handel the logic for starting each state
    /// </summary>
    /// <param name="velocity"></param>
    /// <param name="desiredDir"></param>
    /// <returns></returns>
    private MovementState ResolveMovementState(Vector3 velocity, Vector3 desiredDir)
    {
        MovementState groundState = CheckGround();
        hasWallContact = false;

        // The sphere cast can still reach the floor just after takeoff.
        jumpAscending = jumpAscending && velocity.y > 0f;
        if (groundState != MovementState.Air && !jumpAscending)
            return groundState;

        if (movementState == MovementState.Wallrunning)
        {
            if (!AboveGround())
            {
                wallContactTimer = 0f;
                return MovementState.Air;
            }

            // Track the attached surface in world space so camera yaw cannot drop contact.
            Vector3 normal = HorizontalWallNormal(wallHit);
            if (ProbeWall(-normal, out RaycastHit hit) &&
                Vector3.Dot(normal, HorizontalWallNormal(hit)) > 0.5f)
            {
                SetWallContact(hit);
                normal = HorizontalWallNormal(hit);
            }

            if (wallRunTimer > 0f && (hasWallContact || wallContactTimer > 0f) &&
                WantsWallRun(desiredDir, normal))
                return MovementState.Wallrunning;

            return MovementState.Air;
        }

        // Clearance matters even at the apex or while falling.
        if (groundState == MovementState.Air && maxWallRunTime > 0f &&
            desiredDir.sqrMagnitude > 0.01f && AboveGround() &&
            TryFindWall(velocity, desiredDir, out RaycastHit entryHit))
        {
            SetWallContact(entryHit);
            return MovementState.Wallrunning;
        }

        return MovementState.Air;
    }

    /// <summary>
    /// Handel anything special the needs to happen when the state changes
    /// </summary>
    /// <param name="nextState"></param>
    private void ChangeMovementState(MovementState nextState)
    {
        // No change in state
        if (movementState == nextState) return;

        if (movementState == MovementState.Wallrunning)
        {
            if (nextState == MovementState.Air)
                BlockWallReentry();

            StopWallRun();
        }

        movementState = nextState;
    }

    /// <summary>
    /// Delegates the actual movement logic for each state
    /// </summary>
    /// <param name="velocity"></param>
    /// <param name="desiredDir"></param>
    private void ApplyMovement(ref Vector3 velocity, Vector3 desiredDir)
    {
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);

        switch (movementState)
        {
            case MovementState.Ground:
                GroundMove(ref horizontalVelocity, desiredDir);
                break;
            case MovementState.Air:
                if (wallJumpSteeringTimer <= 0f)
                    AirMove(ref horizontalVelocity, desiredDir);
                break;
            case MovementState.Slope:
                SlopeMove(ref velocity, desiredDir);
                return;
            case MovementState.Wallrunning:
                WallRunMove(ref velocity);
                return;
        }

        velocity.x = horizontalVelocity.x;
        velocity.z = horizontalVelocity.z;
    }
    #endregion

    #region Movement behaviors
    private void Jump()
    {
        jumpAscending = true;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private void WallJump(Vector3 desiredDir)
    {
        Vector3 normal = HorizontalWallNormal(wallHit);
        // Steering away gives a stronger transfer while preserving the upward lift

        float transfer = Mathf.Clamp01(Vector3.Dot(desiredDir, normal));
        float sideForce = wallJumpSideForce * Mathf.Lerp(1f, wallJumpTransferMultiplier, transfer);
        Vector3 force = Vector3.up * wallJumpUpForce + normal * sideForce +
            wallTangent * wallJumpForwardForce;

        BlockWallReentry();
        wallContactTimer = 0f;

        // Let the launch clear the wall before air steering bends the trajectory
        wallJumpSteeringTimer = wallJumpSteeringLockTime;
        jumpAscending = true;
        rb.AddForce(force, ForceMode.Impulse);
    }

    private void StartWallRun(ref Vector3 velocity, Vector3 desiredDir)
    {
        wallTangent = Vector3.Cross(HorizontalWallNormal(wallHit), Vector3.up);

        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float direction = Vector3.Dot(horizontalVelocity, wallTangent);

        if (Mathf.Abs(direction) <= 0.1f)
            direction = Vector3.Dot(desiredDir, wallTangent);

        if (Mathf.Abs(direction) <= 0.1f)
            direction = Vector3.Dot(transform.forward, wallTangent);

        if (direction < 0f)
            wallTangent = -wallTangent;

        // Redirect incoming horizontal speed once. Projecting it onto the tangent
        // would throw most of it away on a steep approach
        velocity = wallTangent * horizontalVelocity.magnitude;
        wallRunTimer = maxWallRunTime;

        gravityBeforeWallRun = rb.useGravity;
        rb.useGravity = gravityBeforeWallRun && wallRunGravityScale > 0f;

        // camera effects
        DoFov(fovBeforeWallRun + wallRunFovChange);
        if (direction < 0f) DoTilt(wallRunTilt);
        if (direction > 0f) DoTilt(-wallRunTilt);
    }

    private void WallRunMove(ref Vector3 velocity)
    {
        if (!rb.useGravity) velocity.y = 0f;
        if (upwardsRunning) velocity.y = wallClimbSpeed;
        if (downwardsRunning) velocity.y = -wallClimbSpeed;

        if (hasWallContact)
        {
            Vector3 normal = HorizontalWallNormal(wallHit);
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (Vector3.Dot(tangent, wallTangent) < 0f)
                tangent = -tangent;

            // Follow the surface through bends without changing speed or reversing direction
            velocity = Quaternion.FromToRotation(wallTangent, tangent) * velocity;
            wallTangent = tangent;

            // Cap added speed while allowing faster momentum carried in from a sprint
            float speedToAdd = maxWallRunSpeed - Vector3.Dot(velocity, wallTangent);
            if (speedToAdd > 0f)
            {
                float force = Mathf.Min(wallRunForce, speedToAdd * rb.mass / Time.fixedDeltaTime);
                rb.AddForce(wallTangent * force, ForceMode.Force);
            }

            ApplyWallAdhesion(normal);
        }

        if (rb.useGravity)
            rb.AddForce(-Physics.gravity * (1f - wallRunGravityScale), ForceMode.Acceleration);

        wallRunTimer -= Time.fixedDeltaTime;
    }

    private void ApplyWallAdhesion(Vector3 normal)
    {
        // Pull across a gap. Pressing into a wall continuously adds friction at contact
        if (playerCollider != null)
        {
            Vector3 center = rb.worldCenterOfMass;
            Vector3 surfaceNormal = wallHit.normal;
            Vector3 wallPoint = center - surfaceNormal * Vector3.Dot(center - wallHit.point, surfaceNormal);
            Vector3 playerPoint = playerCollider.ClosestPoint(wallPoint);

            if (Vector3.Dot(playerPoint - wallPoint, surfaceNormal) <= 0.03f) return;
        }

        rb.AddForce(-normal * wallAdhesionForce, ForceMode.Force);
    }

    private void StopWallRun()
    {
        // reset gravity feild
        rb.useGravity = gravityBeforeWallRun;

        // reset cam effects
        DoFov(fovBeforeWallRun);
        DoTilt(0);
    }

    /// <summary>
    /// Handles movement on ground
    /// </summary>
    /// <param name="velocity"></param>
    /// <param name="desiredDir"></param>
    private void GroundMove(ref Vector3 velocity, Vector3 desiredDir)
    {
        if (desiredDir.sqrMagnitude < 0.001f)
        {
            ApplyGroundFriction(ref velocity);
            return;
        }

        // Project the direction to the terrains slopw to keep directionsl and force consistency 
        desiredDir = Vector3.ProjectOnPlane(
            desiredDir,
            groundNormal
        ).normalized;

        float currentSpeed = velocity.magnitude;
        float targetSpeed = isSprinting ?
            maxGroundSprintSpeed : maxGroundWalkSpeed;

        Vector3 targetVelocity = desiredDir * targetSpeed;
        float distDelta = isSprinting ?
            maxGroundSprintSpeed * Time.fixedDeltaTime :
            maxGroundWalkSpeed * Time.fixedDeltaTime;

        velocity = Vector3.MoveTowards(
            velocity,
            targetVelocity,
            distDelta
        );

        // Preserve momentum when climbing slopes
        if (currentSpeed > velocity.magnitude)
        {
            if ((currentSpeed < maxGroundWalkSpeed && !isSprinting) ||
                currentSpeed < maxGroundSprintSpeed && isSprinting)
                velocity = velocity.normalized * currentSpeed;
        }
    }

    /// <summary>
    /// Handles directional movement in air
    /// </summary>
    /// <param name="velocity"></param>
    /// <param name="desiredDir"></param>
    private void AirMove(ref Vector3 velocity, Vector3 desiredDir)
    {
        // No movement input
        if (desiredDir.sqrMagnitude < 0.001f) return;

        desiredDir.Normalize();

        // Accelerate toward the desired direction
        float currentSpeed = Vector3.Dot(
            velocity,
            desiredDir
        );
        float speedToAdd = maxAirSpeed - currentSpeed;

        // Add speed if we need it
        if (speedToAdd > 0f)
        {
            float acceleration = airAcc * Time.fixedDeltaTime;

            acceleration = Mathf.Min(
                acceleration,
                speedToAdd
            );

            velocity += desiredDir * acceleration;
        }

        // Rotate existing momentum toward the new direction
        float currentMagnitude = velocity.magnitude;
        if (currentMagnitude > 0.01f)
        {
            Vector3 currentDirection = velocity.normalized;

            Vector3 newDirection = Vector3.RotateTowards(
                currentDirection,
                desiredDir,
                airControl * Time.fixedDeltaTime,
                0f
            );

            velocity = newDirection * currentMagnitude;
        }
    }

    /// <summary>
    /// Similar to Ground move but taylored for slopes
    /// </summary>
    /// <param name="velocity"></param>
    /// <param name="desiredDir"></param>
    private void SlopeMove(ref Vector3 velocity, Vector3 desiredDir)
    {
        velocity = Vector3.ProjectOnPlane(velocity, groundNormal);

        // Apply friction onno movement input
        if (desiredDir.sqrMagnitude < 0.001f)
        {
            ApplyGroundFriction(ref velocity);
            return;
        }

        Vector3 slopeDirection = Vector3.ProjectOnPlane(desiredDir, groundNormal).normalized;
        float targetSpeed = isSprinting ? maxGroundSprintSpeed : maxGroundWalkSpeed;
        float acceleration = isSprinting ? groundSprintAcc : groundWalkAcc;

        velocity = Vector3.MoveTowards(
            velocity,
            slopeDirection * targetSpeed,
            acceleration * Time.fixedDeltaTime
        );
    }
    #endregion

    private void ApplyGroundFriction(ref Vector3 velocity)
    {
        velocity = Vector3.MoveTowards(
            velocity,
            Vector3.zero,
            groundFriction * Time.fixedDeltaTime
        );
    }

    /// <summary>
    /// Spherecast based ground detection
    /// </summary>
    /// <returns></returns>
    private MovementState CheckGround()
    {
        if (!Physics.SphereCast(
            transform.position,
            groundProbeRadius,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore))
        {
            return MovementState.Air;
        }

        float angle = Vector3.Angle(hit.normal, Vector3.up);

        if (angle > maxSlopeAngle)
            return MovementState.Air;

        if (angle <= 0.5f)
        {
            groundNormal = Vector3.up;
            return MovementState.Ground;
        }

        groundNormal = hit.normal;
        return MovementState.Slope;
    }

    #region Wall handeling
    /// <summary>
    /// Predictive wall detection to allow smooth entry from all desired angles
    /// </summary>
    /// <param name="velocity"></param>
    /// <param name="desiredDir"></param>
    /// <param name="bestHit"></param>
    /// <returns></returns>
    private bool TryFindWall(Vector3 velocity, Vector3 desiredDir, out RaycastHit bestHit)
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        bestHit = default;

        // Side probes catch parallel runs. Forward probes catch angled approaches
        // before the collision solver has a chance to remove the incoming speed
        ConsiderWall(right, velocity, desiredDir, ref bestHit);
        ConsiderWall(-right, velocity, desiredDir, ref bestHit);
        ConsiderWall((forward + right).normalized, velocity, desiredDir, ref bestHit);
        ConsiderWall((forward - right).normalized, velocity, desiredDir, ref bestHit);
        ConsiderWall(forward, velocity, desiredDir, ref bestHit);

        // After a wall jump the flight direction can differ from the camera heading
        Vector3 travel = new Vector3(velocity.x, 0f, velocity.z);
        if (travel.sqrMagnitude > 0.01f)
            ConsiderWall(travel.normalized, velocity, desiredDir, ref bestHit);

        return bestHit.collider != null;
    }

    /// <summary>
    /// Filters through each wall hit from walls the player is on or was just on
    /// keeping the clocest valid hit
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="velocity"></param>
    /// <param name="desiredDir"></param>
    /// <param name="bestHit"></param>
    private void ConsiderWall(Vector3 direction, Vector3 velocity, Vector3 desiredDir, ref RaycastHit bestHit)
    {
        if (!ProbeWall(direction, out RaycastHit hit))  return;

        //These checks let you run along an angled wall and deliberately peel off it using the same controls
        Vector3 normal = HorizontalWallNormal(hit);
        if (!WantsWallRun(desiredDir, normal) || IsReentryBlocked(hit) ||
            Vector3.Dot(velocity, normal) > 0.5f)
            return;

        if (bestHit.collider == null || hit.distance < bestHit.distance)
            bestHit = hit;
    }

    /// <summary>
    /// Helper to check a specific wall
    /// It checks for a wall object with a minimum normal vec
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="hit"></param>
    /// <returns></returns>
    private bool ProbeWall(Vector3 direction, out RaycastHit hit)
    {
        return Physics.Raycast(transform.position, direction, out hit,
            wallCheckDist, wallLayer, QueryTriggerInteraction.Ignore) &&
            Mathf.Abs(hit.normal.y) <= 0.35f;
    }

    /// <summary>
    /// Deternines what the player is trying to do
    /// </summary>
    /// <param name="desiredDir"></param>
    /// <param name="normal"></param>
    /// <returns></returns>
    private bool WantsWallRun(Vector3 desiredDir, Vector3 normal)
    {
        // Determines how much of the players input points away from the wall
        // Allows for some composation with small tolerances
        // Below -0.2 will allow the player to run even without forward input
        // Above 0.2 will reject the entry
        float away = Vector3.Dot(desiredDir, normal);
        return away < -0.2f || (moveDir.z > 0.1f && away <= 0.2f);
    }

    /// <summary>
    /// Confirmation of wall contact
    /// </summary>
    /// <param name="hit"></param>
    private void SetWallContact(RaycastHit hit)
    {
        wallHit = hit;
        hasWallContact = true;
        wallContactTimer = wallContactGraceTime;
    }

    /// <summary>
    /// Short cooldown for wall running after jumping from another
    /// </summary>
    private void BlockWallReentry()
    {
        exitedWallHit = wallHit;
        exitWallTimer = exitWallTime;
    }

    private bool IsReentryBlocked(RaycastHit hit)
    {
        // Match the surface plane, so seams count as the same wall and the opposite
        // face of a shared mesh is still available for an immediate transfer
        return exitWallTimer > 0f &&
            Vector3.Dot(hit.normal, exitedWallHit.normal) > 0.9f &&
            Mathf.Abs(Vector3.Dot(hit.point - exitedWallHit.point, exitedWallHit.normal)) < 0.25f;
    }

    /// <summary>
    /// Gives the controller a ground-level direction pointing away from the wall.
    /// </summary>
    /// <param name="hit"></param>
    /// <returns></returns>
    private static Vector3 HorizontalWallNormal(RaycastHit hit)
    {
        return new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
    }
    #endregion

    /// <summary>
    /// Simple helper to check for appropriate height to start wallrunning
    /// </summary>
    /// <returns></returns>
    private bool AboveGround()
    {
        return !Physics.Raycast(transform.position, Vector3.down, minJumpHeight, groundLayer, QueryTriggerInteraction.Ignore);
    }

    #region Player Input Endpoints
    public void OnLook(InputAction.CallbackContext ctx)
    {
        Vector2 lookInput = ctx.ReadValue<Vector2>();

        // Horizontal rotation.
        transform.Rotate(
            Vector3.up *
            lookInput.x *
            sensX
        );

        // Vertical rotation.
        xRot -= lookInput.y * sensY;

        xRot = Mathf.Clamp(
            xRot,
            -90f,
            90f
        );

        ApplyCameraRotation();
    }

    private void ApplyCameraRotation()
    {
        if (playerCamera.transform != null)
            playerCamera.transform.localRotation = Quaternion.Euler(xRot, 0f, cameraRoll);
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        Vector2 input = ctx.ReadValue<Vector2>();

        moveDir = new Vector3(
            input.x,
            0f,
            input.y
        );
    }

    public void OnSprint(InputAction.CallbackContext ctx)
    {
        if (ctx.performed) isSprinting = true;
        if (ctx.canceled) isSprinting = false;
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        // Offloads checks to FixedUpdate to account for holding the jump
        if (ctx.performed)
        {
            // Buffer a new press near the wall so input and contact need not share a tick.
            if (!jumpHeld) wallJumpBufferTimer = 0.1f;
            jumpHeld = true;
        }
        if (ctx.canceled) jumpHeld = false;
    }

    // WallRunUp and down pass values into wallrunning movement method for checks
    public void OnWallRunUp(InputAction.CallbackContext ctx)
    {
        if (ctx.started) upwardsRunning = true;
        if (ctx.canceled) upwardsRunning = false;
    }

    public void OnWallRunDown(InputAction.CallbackContext ctx)
    {
        if (ctx.started) downwardsRunning = true;
        if (ctx.canceled) downwardsRunning = false;
    }

    #endregion

    #region Cam effects
    public void DoFov(float endVal)
    {
        playerCamera.DOFieldOfView(endVal, 0.25f);
    }

    public void DoTilt(float zTilt)
    {
        cameraHolder.DOLocalRotate(new Vector3(0, 0, zTilt), 0.25f);
    }

    #endregion

    public void TakeDamage(int damage)
    {
        if (damage > 0) SetHealth(health - Mathf.Min(damage, health));
    }

    public void Heal(int amount)
    {
        if (amount > 0) SetHealth(health + Mathf.Min(amount, maxHealth - health));
    }

    private void SetHealth(int value)
    {
        if (health == value) return;

        health = value;
        HealthChanged?.Invoke();
    }
}
