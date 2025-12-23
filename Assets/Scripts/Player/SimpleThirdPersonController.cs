using UnityEngine;

public class SimpleThirdPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float rotationSpeed = 10f;
    public float gravity = -9.8f;
    public float jumpHeight = 1.5f;

    [Header("Camera")]
    public float mouseSensitivity = 2f;
    public float cameraDistance = 5f;
    public float cameraHeight = 1.6f;
    public float minPitch = -30f;
    public float maxPitch = 60f;

    private CharacterController controller;
    private Animator animator;
    private Camera playerCamera;
    private Transform cameraTarget;

    private float yaw;
    private float pitch;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }
        // CharacterController settings - center at height/2 because character pivot is at feet
        controller.height = 2f;
        controller.radius = 0.5f;
        controller.center = new Vector3(0, 1f, 0);  // Half of height, so bottom is at feet
        controller.skinWidth = 0.1f;
        controller.minMoveDistance = 0.001f;
        controller.slopeLimit = 45f;
        controller.stepOffset = 0.6f;

        // Force layer to Default so we collide with ground
        gameObject.layer = 0;

        // Debug collision
        Debug.Log($"[Player] Layer: {gameObject.layer}, Controller enabled: {controller.enabled}");

        // Check what's below us
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 5f, Vector3.down, out hit, 100f))
        {
            Debug.Log($"[Player] Ground below: {hit.collider.gameObject.name} on layer {hit.collider.gameObject.layer}");
        }
        else
        {
            Debug.Log("[Player] NO GROUND DETECTED BY RAYCAST");
        }

        animator = GetComponentInChildren<Animator>();

        // Create camera
        SetupCamera();

        // Ensure there's ground to stand on
        EnsureGround();

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yaw = transform.eulerAngles.y;
    }

    void SetupCamera()
    {
        // Create camera target (pivot point)
        cameraTarget = new GameObject("CameraTarget").transform;
        cameraTarget.SetParent(transform);
        cameraTarget.localPosition = new Vector3(0, cameraHeight, 0);

        // Find or create camera
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            GameObject camObj = new GameObject("PlayerCamera");
            playerCamera = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }

        playerCamera.transform.SetParent(null); // Don't parent to player
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        UpdateCamera();
        UpdateAnimator();

        // Unlock cursor with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked
                ? CursorLockMode.None
                : CursorLockMode.Locked;
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
        }

        // Debug: Press G to log ground info
        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log($"[Player] Position: {transform.position}, IsGrounded: {isGrounded}");
        }

        // Safety respawn if fallen too far
        if (transform.position.y < -50f)
        {
            Debug.LogWarning("[Player] Fell out of world - respawning at FPS controller position");
            controller.enabled = false;
            transform.position = new Vector3(142f, 46f, -155f);
            controller.enabled = true;
        }
    }

    void HandleMouseLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    void HandleMovement()
    {
        // Match FPS controller style - simple and direct
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        // Build movement vector with gravity (like FPS controller)
        Vector3 movement = new Vector3(horizontal * currentSpeed, gravity, vertical * currentSpeed);

        // Rotate by camera yaw
        movement = Quaternion.Euler(0, yaw, 0) * movement;

        // Single Move call (like FPS controller)
        controller.Move(movement * Time.deltaTime);

        // Update grounded state for animator
        isGrounded = controller.isGrounded;

        // Rotate character to face movement direction
        Vector3 flatMovement = new Vector3(movement.x, 0, movement.z);
        if (flatMovement.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(flatMovement);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    void UpdateCamera()
    {
        if (playerCamera == null || cameraTarget == null) return;

        // Update camera target position
        cameraTarget.position = transform.position + Vector3.up * cameraHeight;

        // Calculate camera position
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 offset = rotation * new Vector3(0, 0, -cameraDistance);
        Vector3 targetPosition = cameraTarget.position + offset;

        // Check for obstacles
        RaycastHit hit;
        if (Physics.Raycast(cameraTarget.position, offset.normalized, out hit, cameraDistance))
        {
            targetPosition = hit.point + hit.normal * 0.2f;
        }

        playerCamera.transform.position = targetPosition;
        playerCamera.transform.LookAt(cameraTarget.position);
    }

    void UpdateAnimator()
    {
        if (animator == null) return;

        Vector3 horizontalVelocity = new Vector3(controller.velocity.x, 0, controller.velocity.z);
        float speed = horizontalVelocity.magnitude;

        // Normalize speed to 0-1 range for SwatAnimator blend tree
        // SwatAnimator uses thresholds: 0 (idle), 0.5 (walk), 1 (run)
        float normalizedSpeed = Mathf.Clamp01(speed / runSpeed);
        animator.SetFloat("Speed", normalizedSpeed);

        // Alternative: use separate bools (some setups use these)
        bool isMoving = speed > 0.1f;
        bool isRunning = speed > walkSpeed + 0.5f;

        // Try multiple common parameter names
        TrySetBool("IsMoving", isMoving);
        TrySetBool("IsRunning", isRunning);
        TrySetBool("IsGrounded", isGrounded);
        TrySetBool("IsJumping", !isGrounded && controller.velocity.y > 0);
        TrySetBool("IsFalling", !isGrounded && controller.velocity.y < 0);

        // Blend tree style (normalized 0-1)
        TrySetFloat("MoveSpeed", normalizedSpeed);

        // Velocity for blend trees
        TrySetFloat("VelocityX", horizontalVelocity.x);
        TrySetFloat("VelocityZ", horizontalVelocity.z);
    }

    void TrySetFloat(string param, float value)
    {
        try { animator.SetFloat(param, value); } catch { }
    }

    void TrySetBool(string param, bool value)
    {
        try { animator.SetBool(param, value); } catch { }
    }

    void EnsureGround()
    {
        // Just log ground detection status
        if (Physics.Raycast(transform.position + Vector3.up * 10f, Vector3.down, 50f))
        {
            Debug.Log("[SimpleThirdPersonController] Ground detected below player");
        }
        else
        {
            Debug.Log("[SimpleThirdPersonController] No ground detected - you may fall");
        }
    }
}
