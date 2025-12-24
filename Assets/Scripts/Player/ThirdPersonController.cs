using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviourPunCallbacks
{
    [Header("Movement")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float rotationSpeed = 10f;
    public float gravity = -20f;
    public float jumpHeight = 1.5f;

    [Header("Camera")]
    public float cameraSensitivity = 2f;
    public float cameraDistance = 4f;
    public float cameraHeight = 2f;
    public float minVerticalAngle = -30f;
    public float maxVerticalAngle = 60f;
    public LayerMask cameraCollisionMask;

    [Header("Ground Check")]
    public float groundCheckRadius = 0.3f;
    public LayerMask groundMask;

    [Header("Combat")]
    public Transform aimTarget;

    // Components
    private CharacterController controller;
    private Animator animator;
    private Camera playerCamera;
    private Transform cameraTarget;

    // State
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float verticalVelocity;
    private float cameraVerticalAngle;
    private float cameraHorizontalAngle;
    private bool isGrounded;
    private bool isRunning;
    private bool isAiming;
    public bool IsAiming => isAiming;

    // Network
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private float networkLerpSpeed = 10f;
    private float networkCameraPitch;  // Synced vertical look angle for head/flashlight

    // Head bone for flashlight sync
    private Transform headBone;
    public float CameraPitch => cameraVerticalAngle;  // Expose for other scripts

    // Animator hashes
    private int speedHash;
    private int isGroundedHash;
    private int isRunningHash;
    private int isAimingHash;
    private int jumpHash;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        // Cache animator hashes
        speedHash = Animator.StringToHash("Speed");
        isGroundedHash = Animator.StringToHash("IsGrounded");
        isRunningHash = Animator.StringToHash("IsRunning");
        isAimingHash = Animator.StringToHash("IsAiming");
        jumpHash = Animator.StringToHash("Jump");

        if (photonView == null || photonView.IsMine)
        {
            SetupLocalPlayer();
        }
        else
        {
            SetupRemotePlayer();
        }
    }

    void SetupLocalPlayer()
    {
        // Create camera
        GameObject camObj = new GameObject("PlayerCamera");
        playerCamera = camObj.AddComponent<Camera>();
        playerCamera.fieldOfView = 60f;
        playerCamera.nearClipPlane = 0.1f;
        playerCamera.farClipPlane = 1000f;
        camObj.AddComponent<AudioListener>();
        camObj.tag = "MainCamera";

        // Create camera target (pivot point)
        GameObject targetObj = new GameObject("CameraTarget");
        cameraTarget = targetObj.transform;
        cameraTarget.SetParent(transform);
        cameraTarget.localPosition = new Vector3(0, cameraHeight, 0);

        // Create aim target
        if (aimTarget == null)
        {
            GameObject aimObj = new GameObject("AimTarget");
            aimTarget = aimObj.transform;
        }

        // Initialize camera angle
        cameraHorizontalAngle = transform.eulerAngles.y;

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Disable main camera if exists
        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam != playerCamera)
        {
            mainCam.gameObject.SetActive(false);
        }

        Debug.Log("[ThirdPersonController] Local player setup complete");
    }

    void SetupRemotePlayer()
    {
        // Remote players don't need camera or input
        enabled = true; // Keep enabled for animation sync

        // Find head bone for flashlight direction sync
        if (animator != null)
        {
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        Debug.Log("[ThirdPersonController] Remote player setup complete");
    }

    void Update()
    {
        if (photonView == null || photonView.IsMine)
        {
            HandleInput();
            HandleMovement();
            HandleCamera();
            UpdateAnimator();
        }
        else
        {
            // Interpolate remote player position
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * networkLerpSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * networkLerpSpeed);
        }
    }

    // Apply head rotation after animation - for flashlight sync on remote players
    void LateUpdate()
    {
        if (photonView != null && !photonView.IsMine && headBone != null)
        {
            // Apply the synced camera pitch to the head bone so flashlight points correctly
            Quaternion headRotation = headBone.localRotation;
            Vector3 euler = headRotation.eulerAngles;
            // Blend the pitch into the head's X rotation
            euler.x = Mathf.LerpAngle(euler.x, -networkCameraPitch, 0.7f);
            headBone.localRotation = Quaternion.Euler(euler);
        }
    }

    void HandleInput()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        var gamepad = Gamepad.current;

        // Movement input
        moveInput = Vector2.zero;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) moveInput.y += 1;
            if (keyboard.sKey.isPressed) moveInput.y -= 1;
            if (keyboard.aKey.isPressed) moveInput.x -= 1;
            if (keyboard.dKey.isPressed) moveInput.x += 1;
            isRunning = keyboard.leftShiftKey.isPressed;
        }
        if (gamepad != null)
        {
            moveInput += gamepad.leftStick.ReadValue();
            isRunning = gamepad.leftTrigger.isPressed;
        }
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        // Look input
        lookInput = Vector2.zero;
        if (mouse != null && mouse.rightButton != null)
        {
            lookInput = mouse.delta.ReadValue() * cameraSensitivity * 0.1f;
        }
        if (gamepad != null)
        {
            lookInput += gamepad.rightStick.ReadValue() * cameraSensitivity;
        }

        // Aim input
        if (mouse != null && mouse.rightButton != null)
        {
            isAiming = mouse.rightButton.isPressed || Input.GetMouseButton(1);
        }
        if (gamepad != null)
        {
            isAiming = gamepad.leftShoulder.isPressed;
        }

        // Jump
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && isGrounded)
        {
            Jump();
        }
        if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame && isGrounded)
        {
            Jump();
        }
    }

    void HandleMovement()
    {
        // Ground check
        isGrounded = Physics.CheckSphere(
            transform.position + Vector3.up * groundCheckRadius,
            groundCheckRadius,
            groundMask
        );

        // Apply gravity
        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }
        verticalVelocity += gravity * Time.deltaTime;

        // Calculate move direction relative to camera
        Vector3 cameraForward = playerCamera.transform.forward;
        Vector3 cameraRight = playerCamera.transform.right;
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection = (cameraForward * moveInput.y + cameraRight * moveInput.x);

        // Apply speed
        float currentSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 velocity = moveDirection * currentSpeed;
        velocity.y = verticalVelocity;

        // Move
        controller.Move(velocity * Time.deltaTime);

        // Rotate character towards movement direction
        if (moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    void HandleCamera()
    {
        if (playerCamera == null) return;

        // Update camera angles
        cameraHorizontalAngle += lookInput.x;
        cameraVerticalAngle -= lookInput.y;
        cameraVerticalAngle = Mathf.Clamp(cameraVerticalAngle, minVerticalAngle, maxVerticalAngle);

        // Calculate camera position
        Quaternion cameraRotation = Quaternion.Euler(cameraVerticalAngle, cameraHorizontalAngle, 0);
        Vector3 cameraOffset = cameraRotation * new Vector3(0, 0, -cameraDistance);
        Vector3 targetPosition = cameraTarget.position + cameraOffset;

        // Camera collision
        RaycastHit hit;
        if (Physics.Linecast(cameraTarget.position, targetPosition, out hit, cameraCollisionMask))
        {
            targetPosition = hit.point + hit.normal * 0.2f;
        }

        // Apply camera transform
        playerCamera.transform.position = targetPosition;
        playerCamera.transform.LookAt(cameraTarget.position);

        // Update aim target (for shooting direction)
        if (aimTarget != null)
        {
            Ray aimRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit aimHit;
            if (Physics.Raycast(aimRay, out aimHit, 100f))
            {
                aimTarget.position = aimHit.point;
            }
            else
            {
                aimTarget.position = aimRay.GetPoint(100f);
            }
        }
    }

    void Jump()
    {
        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        if (animator != null)
        {
            animator.SetTrigger(jumpHash);
        }
    }

    void UpdateAnimator()
    {
        if (animator == null) return;

        // Normalize speed to 0-1 range for SwatAnimator blend tree
        float speed = moveInput.magnitude * (isRunning ? runSpeed : walkSpeed);
        float normalizedSpeed = Mathf.Clamp01(speed / runSpeed);
        animator.SetFloat(speedHash, normalizedSpeed);
        animator.SetBool(isGroundedHash, isGrounded);
        animator.SetBool(isRunningHash, isRunning);
        animator.SetBool(isAimingHash, isAiming);
    }

    // Photon serialization
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send normalized speed (0-1)
            float speed = moveInput.magnitude * (isRunning ? runSpeed : walkSpeed);
            float normalizedSpeed = Mathf.Clamp01(speed / runSpeed);

            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(normalizedSpeed);
            stream.SendNext(isRunning);
            stream.SendNext(isAiming);
            stream.SendNext(cameraVerticalAngle);  // Sync head pitch for flashlight
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            float normalizedSpeed = (float)stream.ReceiveNext();
            isRunning = (bool)stream.ReceiveNext();
            isAiming = (bool)stream.ReceiveNext();
            networkCameraPitch = (float)stream.ReceiveNext();  // Receive head pitch

            // Update animator for remote player with normalized speed
            if (animator != null)
            {
                animator.SetFloat(speedHash, normalizedSpeed);
                animator.SetBool(isRunningHash, isRunning);
                animator.SetBool(isAimingHash, isAiming);
            }
        }
    }

    void OnDestroy()
    {
        if ((photonView == null || photonView.IsMine) && playerCamera != null)
        {
            Destroy(playerCamera.gameObject);
        }
    }
}
