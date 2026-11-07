using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCore : MonoBehaviour
{
    [Header("Camera and look")]
    [SerializeField] private float sensX;
    [SerializeField] private float sensY;
    [SerializeField] private Transform cameraHolder;


    [Tooltip("Keeps track of current orientation")]
    [SerializeField] private Transform orientation;


    [Header("Movement")]
    [SerializeField] private float groundAcc = 25f;
    [SerializeField] private float maxGroundSpeed = 8f;
    [SerializeField] private float groundFriction = 10f;
    private Rigidbody rb;
    private Vector3 moveDir;
    private float xRot;
    private float curSpeed;


    [Header("Ground detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 1.5f;
    [SerializeField] private float maxSlopeAngle = 50f;
    [SerializeField] private float groundProbeRadius = 0.3f;
    private bool isGrounded;
    private Vector3 groundNormal = Vector3.up;
    private float groundAngle;




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
    void FixedUpdate()
    {
        CheckGround();

        #region main movement and directional updates
        // Get movement direction relative to the player
        Vector3 desiredDir =
            transform.forward * moveDir.z +
            transform.right * moveDir.x;

        if (desiredDir.sqrMagnitude > 1f)
        {
            desiredDir.Normalize();
        }

        // Separate horizontal and vertical velocity
        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        // Store current horizontal movement speed
        curSpeed = horizontalVelocity.magnitude;

        // Apply movement
        if (isGrounded)
        {
            GroundMove(ref horizontalVelocity, desiredDir);
        }

        // Recombine horizontal and vertical velocity
        rb.linearVelocity = new Vector3(
            horizontalVelocity.x,
            velocity.y,
            horizontalVelocity.z
        );

        #endregion
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
        float targetSpeed = maxGroundSpeed;

        Vector3 targetVelocity =
            desiredDir * targetSpeed;

        velocity = Vector3.MoveTowards(
            velocity,
            targetVelocity,
            groundAcc * Time.fixedDeltaTime
        );

        // Preserve momentum when climbing slopes.
        if (currentSpeed > velocity.magnitude &&
            currentSpeed < maxGroundSpeed)
        {
            velocity = velocity.normalized * currentSpeed;
        }
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
    private void CheckGround()
    {
        Vector3 origin = transform.position;

        // Debug line for distance check if needed
        //Debug.DrawLine(origin, new Vector3(origin.x, origin.y - groundCheckDistance, origin.z), Color.magenta);

        if (Physics.SphereCast(
            origin,
            groundProbeRadius,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore))
        {
            groundNormal = hit.normal;

            groundAngle = Vector3.Angle(
                groundNormal,
                Vector3.up
            );

            isGrounded = groundAngle <= maxSlopeAngle;
        }
        else
        {
            groundNormal = Vector3.up;
            groundAngle = 0f;
            isGrounded = false;
        }
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

    #endregion
}
