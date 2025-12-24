using UnityEngine;
using Unity.Cinemachine;
using Photon.Pun;

public class ThirdPersonMotor : MonoBehaviourPunCallbacks, IPunObservable
{
    public float speed = 4.0f;
    public float runSpeed = 10.0f;
    public float sensitivity = 2f;
    public float WaterHeight = 15.5f;

    [Header("Cinemachine Camera Settings")]
    public float cameraDistance = 3f;
    public float cameraHeight = 1.4f;
    [Range(0f, 3f)]
    public float shoulderOffset = 1.2f;
    [Range(40f, 100f)]
    public float cameraFOV = 65f;
    [Range(0f, 1f)]
    public float cameraDamping = 0.1f;

    [Header("Character Model")]
    [Tooltip("Drag your character FBX here (e.g., Rifle Idle 1)")]
    public GameObject characterPrefab;
    [Tooltip("Drag SwatAnimator.controller here")]
    public RuntimeAnimatorController animatorController;

    [Header("Footstep Noise (Stealth)")]
    [Tooltip("Time between footstep noises when walking")]
    public float walkNoiseInterval = 0.8f;
    [Tooltip("Time between footstep noises when running")]
    public float runNoiseInterval = 0.35f;

    CharacterController character;
    Animator animator;
    GameObject characterModel;
    Transform cameraLookAt;
    Transform cameraPivot;
    Transform headBone;
    Transform spineBone;
    Transform chestBone;
    Transform rightShoulder;
    Transform leftShoulder;

    float yaw, pitch;
    float targetYaw, targetPitch;
    float gravity = -9.8f;
    bool skipNextMouseInput = false;

    [Header("Mouse Smoothing")]
    [Range(0f, 1f)]
    public float mouseSmoothing = 0.5f;

    [Header("Aiming")]
    public float aimSpineOffset = 10f; // Raise spine when aiming (more upright)
    public float aimSpineYaw = 0.3f; // Left/right spine rotation multiplier when aiming
    public float aimFOV = 45f;
    public float aimTransitionSpeed = 10f;
    [Range(0.3f, 1f)]
    public float aimSpeedMultiplier = 0.65f;

    CinemachineCamera cinemachineCam;
    CinemachineThirdPersonFollow thirdPersonFollow;

    bool isAiming = false;
    float defaultFOV;
    PlayerKick playerKick;

    // Footstep noise timer
    float nextFootstepTime;

    // Public property for UI
    public bool IsAiming => isAiming;

    // Network sync
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private float networkMoveX;
    private float networkMoveY;
    private bool networkIsAiming;
    private float lerpSpeed = 10f;

    // Check if this is the local player
    public bool IsLocalPlayer => !PhotonNetwork.IsConnected || photonView == null || photonView.IsMine;

    void Start()
    {
        character = GetComponent<CharacterController>();

        // Hide the capsule mesh
        MeshRenderer capsuleRenderer = GetComponent<MeshRenderer>();
        if (capsuleRenderer != null) capsuleRenderer.enabled = false;

        // Destroy old camera child if exists
        Transform oldCam = transform.Find("Camera");
        if (oldCam != null) Destroy(oldCam.gameObject);

        // Spawn character model (for all players - local and remote)
        SetupCharacterModel();

        // Only setup camera and cursor for local player
        if (IsLocalPlayer)
        {
            SetupCinemachine();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // Disable CharacterController on remote players to avoid physics conflicts
            if (character != null) character.enabled = false;
        }

        yaw = targetYaw = transform.eulerAngles.y;
        pitch = targetPitch = 0f;

        playerKick = GetComponent<PlayerKick>();

        // Initialize network position
        networkPosition = transform.position;
        networkRotation = transform.rotation;
    }

    void SetupCharacterModel()
    {
        if (characterPrefab != null)
        {
            characterModel = Instantiate(characterPrefab, transform);
            characterModel.transform.localPosition = new Vector3(0, -1f, 0);
            characterModel.transform.localRotation = Quaternion.identity;
            characterModel.name = "CharacterModel";

            animator = characterModel.GetComponentInChildren<Animator>();
            if (animator != null && animatorController != null)
            {
                animator.runtimeAnimatorController = animatorController;
                animator.applyRootMotion = false;

                // Find bones for procedural aiming
                headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                spineBone = animator.GetBoneTransform(HumanBodyBones.Spine);
                chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
                rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);

                if (headBone != null && spineBone != null)
                    Debug.Log("[ThirdPersonMotor] Bones found for procedural aiming!");

                Debug.Log("[ThirdPersonMotor] Character model and animator set up!");
            }
        }
        else
        {
            Debug.LogWarning("[ThirdPersonMotor] No character prefab assigned!");
            animator = GetComponentInChildren<Animator>();
        }
    }

    void SetupCinemachine()
    {
        // Create camera pivot that rotates with yaw (separate from character rotation)
        GameObject pivotObj = new GameObject("CameraPivot");
        cameraPivot = pivotObj.transform;
        cameraPivot.position = transform.position;

        // Create look-at target as child of pivot
        GameObject lookAtObj = new GameObject("CameraLookAt");
        cameraLookAt = lookAtObj.transform;
        cameraLookAt.SetParent(cameraPivot);
        cameraLookAt.localPosition = new Vector3(0, cameraHeight, 0);

        // Only disable cameras that are children of THIS player (not other players' cameras)
        Camera[] myCams = GetComponentsInChildren<Camera>(true);
        foreach (Camera c in myCams)
        {
            c.gameObject.SetActive(false);
        }

        // Create main camera
        GameObject mainCamObj = new GameObject("MainCamera");
        mainCamObj.tag = "MainCamera";
        Camera mainCam = mainCamObj.AddComponent<Camera>();
        mainCam.fieldOfView = cameraFOV;
        mainCam.nearClipPlane = 0.1f;
        mainCamObj.AddComponent<AudioListener>();

        // Add Cinemachine Brain to main camera
        CinemachineBrain brain = mainCamObj.AddComponent<CinemachineBrain>();

        // Create Cinemachine Camera
        GameObject cmCamObj = new GameObject("CM ThirdPerson");
        cinemachineCam = cmCamObj.AddComponent<CinemachineCamera>();
        cinemachineCam.Follow = cameraPivot;
        cinemachineCam.LookAt = cameraLookAt;
        cinemachineCam.Lens.FieldOfView = cameraFOV;

        // Add Third Person Follow component
        thirdPersonFollow = cmCamObj.AddComponent<CinemachineThirdPersonFollow>();
        thirdPersonFollow.Damping = new Vector3(0.1f, 1.05f, 0.1f);
        thirdPersonFollow.ShoulderOffset = new Vector3(0.36f, -0.56f, 1.19f);
        thirdPersonFollow.VerticalArmLength = 1.13f;
        thirdPersonFollow.CameraSide = 1f;
        thirdPersonFollow.CameraDistance = 3.07f;

        // Add rotation composer for smooth aiming
        CinemachineRotationComposer rotationComposer = cmCamObj.AddComponent<CinemachineRotationComposer>();
        rotationComposer.TargetOffset = new Vector3(0.98f, 1.47f, 3.51f);
        rotationComposer.Damping = new Vector2(0.1f, 0.1f);

        // Add deoccluder for collision handling
        cmCamObj.AddComponent<CinemachineDeoccluder>();

        // Store default FOV for aiming transitions
        defaultFOV = cameraFOV;

        Debug.Log("[ThirdPersonMotor] Cinemachine camera set up!");
    }

    void CheckForWaterHeight()
    {
        if (transform.position.y < WaterHeight)
            gravity = 0f;
        else
            gravity = -9.8f;
    }

    void Update()
    {
        // Remote player - just interpolate position and animate
        if (!IsLocalPlayer)
        {
            UpdateRemotePlayer();
            return;
        }

        // Re-lock cursor when clicking back into the game
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            skipNextMouseInput = true;
            return;
        }

        // Input
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        // Use raw mouse input for consistent WebGL behavior
        float rotX = Input.GetAxisRaw("Mouse X") * sensitivity;
        float rotY = Input.GetAxisRaw("Mouse Y") * sensitivity;

        // Skip mouse input for one frame after re-locking
        if (skipNextMouseInput)
        {
            rotX = 0;
            rotY = 0;
            skipNextMouseInput = false;
        }

        CheckForWaterHeight();

        // Camera rotation - only rotate when cursor is locked
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            targetYaw += rotX;
            targetPitch += rotY;
            targetPitch = Mathf.Clamp(targetPitch, -80f, 70f);

            // Smooth interpolation
            float smoothSpeed = (1f - mouseSmoothing) * 50f + 10f; // Range: 10-60
            yaw = Mathf.Lerp(yaw, targetYaw, Time.deltaTime * smoothSpeed);
            pitch = Mathf.Lerp(pitch, targetPitch, Time.deltaTime * smoothSpeed);
        }

        // Update camera pivot position and rotation (follows player, rotates with yaw)
        if (cameraPivot != null)
        {
            cameraPivot.position = transform.position;
            cameraPivot.rotation = Quaternion.Euler(0, yaw, 0);
        }

        // Update camera look target based on pitch
        if (cameraLookAt != null)
        {
            float pitchOffset = Mathf.Tan(pitch * Mathf.Deg2Rad) * 5f;
            cameraLookAt.localPosition = new Vector3(0, cameraHeight + pitchOffset, 2f);
        }

        // Movement relative to camera direction (disabled while kicking)
        if (playerKick == null) playerKick = GetComponent<PlayerKick>();
        bool isKicking = playerKick != null && playerKick.IsKicking;

        float currentSpeed = isRunning ? runSpeed : speed;
        if (isAiming) currentSpeed *= aimSpeedMultiplier;

        // Zero out movement when kicking
        if (isKicking)
        {
            moveHorizontal = 0f;
            moveVertical = 0f;
        }

        Vector3 movement = new Vector3(moveHorizontal * currentSpeed, gravity, moveVertical * currentSpeed);
        movement = Quaternion.Euler(0, yaw, 0) * movement;

        character.Move(movement * Time.deltaTime);

        // Aiming with right mouse button (can't aim while running)
        isAiming = Input.GetMouseButton(1) && !isRunning;

        // Rotate character when moving OR when aiming
        bool isMoving = Mathf.Abs(moveHorizontal) > 0.1f || Mathf.Abs(moveVertical) > 0.1f;
        if (isMoving || isAiming)
        {
            // Add offset when aiming to correct gun pointing left
            float aimYawOffset = isAiming ? 25f : 0f;
            transform.rotation = Quaternion.Euler(0, yaw + aimYawOffset, 0);
        }

        // === FOOTSTEP NOISE FOR STEALTH SYSTEM ===
        if (isMoving && Time.time >= nextFootstepTime)
        {
            MakeFootstepNoise(isRunning);
        }

        // Smooth FOV transition for ADS
        if (cinemachineCam != null)
        {
            float targetFOV = isAiming ? aimFOV : defaultFOV;
            cinemachineCam.Lens.FieldOfView = Mathf.Lerp(
                cinemachineCam.Lens.FieldOfView,
                targetFOV,
                Time.deltaTime * aimTransitionSpeed
            );
        }

        // Update animator with input values
        UpdateAnimator(moveHorizontal, moveVertical, isRunning);

        // Tab to switch shoulders
        if (Input.GetKeyDown(KeyCode.Tab) && thirdPersonFollow != null)
        {
            Vector3 current = thirdPersonFollow.ShoulderOffset;
            thirdPersonFollow.ShoulderOffset = new Vector3(-current.x, current.y, current.z);
        }

        // Escape to toggle cursor
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
        }
    }

    void MakeFootstepNoise(bool isRunning)
    {
        if (NoiseManager.Instance == null) return;

        if (isRunning)
        {
            // Running = loud footsteps, zombies hear from 20m
            NoiseManager.Instance.MakeNoise(transform.position, NoiseManager.NoiseType.Run, transform);
            nextFootstepTime = Time.time + runNoiseInterval;
        }
        else
        {
            // Walking = quiet footsteps, zombies hear from ~5m
            NoiseManager.Instance.MakeNoise(transform.position, NoiseManager.NoiseType.Walk, transform);
            nextFootstepTime = Time.time + walkNoiseInterval;
        }
    }

    float smoothMoveX = 0f;
    float smoothMoveY = 0f;

    // Random idle
    float idleTimer = 0f;
    float nextIdleChange = 5f;
    int currentIdleVariant = 0;

    void UpdateAnimator(float horizontalInput, float verticalInput, bool isRunning)
    {
        if (animator == null) return;

        // Calculate target values based on input and run state
        float speedMultiplier = isRunning ? 1f : 0.5f;
        float targetMoveX = horizontalInput * speedMultiplier;
        float targetMoveY = verticalInput * speedMultiplier;

        // Smooth the transitions
        smoothMoveX = Mathf.Lerp(smoothMoveX, targetMoveX, Time.deltaTime * 10f);
        smoothMoveY = Mathf.Lerp(smoothMoveY, targetMoveY, Time.deltaTime * 10f);

        animator.SetFloat("MoveX", smoothMoveX);
        animator.SetFloat("MoveY", smoothMoveY);
        animator.SetBool("IsAiming", isAiming);

        // Random idle variations when standing still
        bool isIdle = Mathf.Abs(horizontalInput) < 0.1f && Mathf.Abs(verticalInput) < 0.1f && !isAiming;
        if (isIdle)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= nextIdleChange)
            {
                currentIdleVariant = Random.Range(0, 3); // 0, 1, or 2
                nextIdleChange = Random.Range(4f, 8f);
                idleTimer = 0f;
            }
        }
        else
        {
            idleTimer = 0f;
            currentIdleVariant = 0; // Reset to main idle when moving
        }
        animator.SetInteger("IdleVariant", currentIdleVariant);

        if (Input.GetKeyDown(KeyCode.G))
        {
            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
            string clipName = clips.Length > 0 ? clips[0].clip.name : "none";
            Debug.Log($"[Animator] Clip: {clipName}, MoveX: {smoothMoveX:F2}, MoveY: {smoothMoveY:F2}");
        }
    }

    private Texture2D crosshairTexture;

    void OnGUI()
    {
        // Only draw crosshair for local player
        if (!IsLocalPlayer) return;

        if (crosshairTexture == null)
        {
            int size = 6;
            crosshairTexture = new Texture2D(size, size);
            float radius = size / 2f;
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(radius - 0.5f, radius - 0.5f));
                    pixels[y * size + x] = dist < radius - 0.5f ? Color.white : Color.clear;
                }
            }
            crosshairTexture.SetPixels(pixels);
            crosshairTexture.Apply();
        }

        float drawSize = 3f;
        float px = Screen.width / 2 - drawSize / 2;
        float py = Screen.height / 2 - drawSize / 2;
        GUI.DrawTexture(new Rect(px, py, drawSize, drawSize), crosshairTexture);
    }

    void LateUpdate()
    {
        // Skip procedural aiming for remote players
        if (!IsLocalPlayer) return;

        // Calculate yaw difference between camera and character body
        float bodyYaw = transform.eulerAngles.y;
        float aimYaw = Mathf.DeltaAngle(bodyYaw, yaw);

        if (isAiming)
        {
            // When aiming, only adjust pitch (up/down) - keep torso straight
            float spinePitch = Mathf.Clamp(-pitch * 0.15f + aimSpineOffset, -10f, 20f); // Offset raises spine
            float chestPitch = Mathf.Clamp(-pitch * 0.2f + aimSpineOffset * 0.5f, -10f, 15f); // Less forward lean

            if (spineBone != null)
            {
                Quaternion spineRotation = Quaternion.Euler(spinePitch, 0, 0);
                spineBone.localRotation = spineBone.localRotation * spineRotation;
            }

            if (chestBone != null)
            {
                Quaternion chestRotation = Quaternion.Euler(chestPitch, 0, 0);
                chestBone.localRotation = chestBone.localRotation * chestRotation;
            }

            // Head
            if (headBone != null)
            {
                float headPitch = Mathf.Clamp(-pitch * 0.2f, -20f, 20f);
                Quaternion headRotation = Quaternion.Euler(headPitch, 0, 0);
                headBone.localRotation = headBone.localRotation * headRotation;
            }

        }
        else
        {
            // When not aiming, just do head look
            if (headBone != null)
            {
                float headYaw = Mathf.Clamp(aimYaw, -70f, 70f);
                float headPitch = Mathf.Clamp(-pitch - 15f, -30f, 40f);
                Quaternion headRotation = Quaternion.Euler(headPitch, headYaw, 0);
                headBone.localRotation = headBone.localRotation * headRotation;
            }
        }
    }

    void UpdateRemotePlayer()
    {
        // Smoothly interpolate position and rotation
        transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * lerpSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * lerpSpeed);

        // Update animator with network values
        if (animator != null)
        {
            smoothMoveX = Mathf.Lerp(smoothMoveX, networkMoveX, Time.deltaTime * 10f);
            smoothMoveY = Mathf.Lerp(smoothMoveY, networkMoveY, Time.deltaTime * 10f);
            animator.SetFloat("MoveX", smoothMoveX);
            animator.SetFloat("MoveY", smoothMoveY);
            animator.SetBool("IsAiming", networkIsAiming);
        }
    }

    // Photon network serialization
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Local player sends data
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(smoothMoveX);
            stream.SendNext(smoothMoveY);
            stream.SendNext(isAiming);
        }
        else
        {
            // Remote players receive data
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkMoveX = (float)stream.ReceiveNext();
            networkMoveY = (float)stream.ReceiveNext();
            networkIsAiming = (bool)stream.ReceiveNext();
        }
    }

    void OnDestroy()
    {
        if (IsLocalPlayer && cameraPivot != null) Destroy(cameraPivot.gameObject);
    }
}
