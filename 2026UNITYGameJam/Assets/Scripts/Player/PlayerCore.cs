using System.IO.IsolatedStorage;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public enum MovementState
{
    Ground,
    Slope,
    Air
}

public class PlayerCore : MonoBehaviour
{
    [Header("Camera and look")]
    [SerializeField] private float sensX;
    [SerializeField] private float sensY;
    [SerializeField] private Transform cameraHolder;


    [Tooltip("Keeps track of current orientation")]
    [SerializeField] private Transform orientation;


    [Header("Movement")]
    [SerializeField] private float groundWalkAcc = 15f;
    [SerializeField] private float groundSprintAcc = 25f;
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


    [Header("Ground detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 1.5f;
    [SerializeField] private float maxSlopeAngle = 50f;
    [SerializeField] private float groundProbeRadius = 0.3f;
    private Vector3 groundNormal = Vector3.up;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        rb = GetComponent<Rigidbody>();

        // Prevent the player from tipping over
        rb.freezeRotation = true;
    }

    // Update is called once per frame
    private void FixedUpdate()
    {
        Vector3 velocity = rb.linearVelocity;
        movementState = CheckGround(velocity.y);

        bool canJump = jumpHeld && movementState != MovementState.Air;
        if (canJump)
        {
            velocity.y = 0f;
            movementState = MovementState.Air;
        }

        // Get the desired direction
        Vector3 desiredDir =
            transform.forward * moveDir.z +
            transform.right * moveDir.x;

        if (desiredDir.sqrMagnitude > 1f) desiredDir.Normalize();

        // Check movement state and apply movement accordingly
        if (movementState == MovementState.Slope)
        {
            SlopeMove(ref velocity, desiredDir);
        }
        else
        {
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);

            if (movementState == MovementState.Ground)
                GroundMove(ref horizontalVelocity, desiredDir);
            else
                AirMove(ref horizontalVelocity, desiredDir);

            velocity.x = horizontalVelocity.x;
            velocity.z = horizontalVelocity.z;
        }

        rb.linearVelocity = velocity;

        if (canJump) Jump();
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

        // Preserve momentum when climbing slopes.
        if (currentSpeed > velocity.magnitude)
        {
            if((currentSpeed < maxGroundWalkSpeed && !isSprinting) ||
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
        // No movement input.
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
    /// <param name="verticalSpeed"></param>
    /// <returns></returns>
    private MovementState CheckGround(float verticalSpeed)
    {
        // The sphere cast can still reach the floor just after takeoff.
        if (jumpAscending && verticalSpeed > 0f)
            return MovementState.Air;

        jumpAscending = false;

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

    private void Jump()
    {
        jumpAscending = true;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
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

        cameraHolder.localRotation =
            Quaternion.Euler(
                xRot,
                0f,
                0f
            );
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
        if (ctx.performed) jumpHeld = true;
        if (ctx.canceled) jumpHeld = false;
    }

    #endregion
}
