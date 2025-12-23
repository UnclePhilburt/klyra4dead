using UnityEngine;
using Photon.Pun;

public class PlayerShooting : MonoBehaviourPunCallbacks
{
    [Header("Shooting")]
    public float damage = 25f;
    public float range = 100f;
    public float fireRate = 0.1f;

    [Header("Ammo")]
    public int magazineSize = 30;
    public int reserveAmmo = 90;
    public float reloadTime = 2f;

    [Header("Effects")]
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public GameObject muzzleFlashPrefab;
    public GameObject hitEffectPrefab;

    // State
    private int currentAmmo;
    private bool isReloading;
    private float reloadEndTime;

    private float nextFireTime;
    private AudioSource audioSource;
    private Camera playerCamera;
    private Animator animator;
    private ThirdPersonMotor motor;

    // Public accessors for UI
    public int CurrentAmmo => currentAmmo;
    public int ReserveAmmo => reserveAmmo;
    public int MagazineSize => magazineSize;
    public bool IsReloading => isReloading;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        animator = GetComponentInChildren<Animator>();
        motor = GetComponent<ThirdPersonMotor>();
        currentAmmo = magazineSize;
    }

    // Check if this is the local player
    private bool IsLocalPlayer => !PhotonNetwork.IsConnected || photonView.IsMine;

    void Update()
    {
        // Only process input for local player
        if (!IsLocalPlayer) return;

        // Find camera if not set
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Handle reload completion
        if (isReloading && Time.time >= reloadEndTime)
        {
            FinishReload();
        }

        // R to reload (if not already reloading and not full)
        if (Input.GetKeyDown(KeyCode.R) && !isReloading && currentAmmo < magazineSize)
        {
            StartReload();
        }

        // Left click to shoot (only when ADS)
        bool isAiming = motor != null && motor.IsAiming;
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime && !isReloading && isAiming)
        {
            if (currentAmmo > 0)
            {
                Shoot();
                nextFireTime = Time.time + fireRate;
            }
            else
            {
                // Auto reload when empty
                StartReload();
            }
        }
    }

    void StartReload()
    {
        if (isReloading) return;

        isReloading = true;
        reloadEndTime = Time.time + reloadTime;

        // Trigger reload animation
        if (animator != null)
        {
            animator.SetTrigger("Reload");
            animator.SetBool("IsReloading", true);
        }

        // Play reload sound
        if (reloadSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(reloadSound);
        }

        Debug.Log("[Shooting] Reloading...");
    }

    void FinishReload()
    {
        isReloading = false;

        if (animator != null)
        {
            animator.SetBool("IsReloading", false);
        }

        // Unlimited reserve ammo for now - just refill magazine
        currentAmmo = magazineSize;

        Debug.Log($"[Shooting] Reload complete. Ammo: {currentAmmo}/{magazineSize}");
    }

    void Shoot()
    {
        if (playerCamera == null) return;

        currentAmmo--;

        // Trigger fire animation
        if (animator != null)
        {
            animator.SetTrigger("Fire");
        }

        // Crosshair recoil
        if (CrosshairUI.Instance != null)
        {
            CrosshairUI.Instance.AddRecoil();
        }

        // Play sound
        if (shootSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shootSound);
        }

        // Raycast from center of screen
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        Debug.DrawRay(ray.origin, ray.direction * range, Color.red, 0.1f);

        if (Physics.Raycast(ray, out hit, range))
        {
            Debug.Log($"[Shooting] Hit: {hit.collider.gameObject.name}");

            // Check for zombie
            ZombieHealth zombieHealth = hit.collider.GetComponent<ZombieHealth>();
            if (zombieHealth == null)
            {
                zombieHealth = hit.collider.GetComponentInParent<ZombieHealth>();
            }

            if (zombieHealth != null)
            {
                // Check for headshot (collider or bone name contains "head")
                bool isHeadshot = hit.collider.name.ToLower().Contains("head");
                float finalDamage = isHeadshot ? 9999f : damage;

                zombieHealth.TakeDamageLocal(finalDamage, ray.direction, hit.point);

                if (isHeadshot)
                    Debug.Log("[Shooting] HEADSHOT! Instant kill!");
                else
                    Debug.Log($"[Shooting] Dealt {damage} damage to zombie");
            }

            // Spawn hit effect
            if (hitEffectPrefab != null)
            {
                GameObject hitEffect = Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(hitEffect, 2f);
            }
        }
    }
}
