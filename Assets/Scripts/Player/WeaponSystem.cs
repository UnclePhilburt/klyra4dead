using UnityEngine;
using Photon.Pun;
using System.Collections;

public class WeaponSystem : MonoBehaviourPunCallbacks
{
    [Header("Weapon Settings")]
    public float damage = 25f;
    public float fireRate = 0.1f;
    public float range = 100f;
    public int maxAmmo = 30;
    public int reserveAmmo = 120;
    public float reloadTime = 2f;

    [Header("Recoil")]
    public float recoilVertical = 2f;
    public float recoilHorizontal = 0.5f;
    public float recoilRecovery = 5f;

    [Header("References")]
    public Transform firePoint;
    public GameObject muzzleFlashPrefab;
    public GameObject bulletHolePrefab;
    public GameObject bloodEffectPrefab;
    public AudioClip fireSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;

    [Header("Aiming")]
    public float hipAccuracy = 0.05f;
    public float aimAccuracy = 0.01f;
    public float aimSpeed = 10f;

    // State
    public int CurrentAmmo { get; private set; }
    public bool IsReloading { get; private set; }
    public bool IsAiming { get; private set; }
    public float ReloadProgress { get; private set; }

    // Events
    public event System.Action<int, int> OnAmmoChanged;
    public event System.Action OnFire;
    public event System.Action OnReloadStart;
    public event System.Action OnReloadComplete;

    private float nextFireTime;
    private float currentRecoil;
    private AudioSource audioSource;
    private Camera playerCamera;

    void Start()
    {
        CurrentAmmo = maxAmmo;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        OnAmmoChanged?.Invoke(CurrentAmmo, reserveAmmo);
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        // Find camera if not set
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Handle input
        HandleInput();

        // Recover recoil
        if (currentRecoil > 0)
        {
            currentRecoil = Mathf.Lerp(currentRecoil, 0, recoilRecovery * Time.deltaTime);
        }
    }

    void HandleInput()
    {
        // Aiming
        IsAiming = Input.GetMouseButton(1);

        // Fire
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime && !IsReloading)
        {
            if (CurrentAmmo > 0)
            {
                Fire();
            }
            else
            {
                // Play empty click
                if (emptySound != null && audioSource != null)
                {
                    audioSource.PlayOneShot(emptySound);
                }
                nextFireTime = Time.time + 0.2f;
            }
        }

        // Reload
        if (Input.GetKeyDown(KeyCode.R) && !IsReloading && CurrentAmmo < maxAmmo && reserveAmmo > 0)
        {
            StartCoroutine(Reload());
        }
    }

    void Fire()
    {
        nextFireTime = Time.time + fireRate;
        CurrentAmmo--;

        // Calculate spread
        float accuracy = IsAiming ? aimAccuracy : hipAccuracy;
        Vector3 spread = new Vector3(
            Random.Range(-accuracy, accuracy),
            Random.Range(-accuracy, accuracy),
            0
        );

        // Get fire direction from camera
        Vector3 fireDirection = playerCamera.transform.forward;
        fireDirection = Quaternion.Euler(spread) * fireDirection;

        // Add recoil to spread
        fireDirection = Quaternion.Euler(-currentRecoil, Random.Range(-recoilHorizontal, recoilHorizontal), 0) * fireDirection;

        // Raycast
        Ray ray = new Ray(playerCamera.transform.position, fireDirection);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, range))
        {
            // Check what we hit
            PlayerHealth playerHealth = hit.collider.GetComponent<PlayerHealth>();
            ZombieHealth zombieHealth = hit.collider.GetComponent<ZombieHealth>();

            if (zombieHealth != null)
            {
                // Hit a zombie
                zombieHealth.TakeDamage(damage, photonView.ViewID);

                // Blood effect
                if (bloodEffectPrefab != null)
                {
                    photonView.RPC("RPC_SpawnEffect", RpcTarget.All,
                        bloodEffectPrefab.name, hit.point, Quaternion.LookRotation(hit.normal));
                }
            }
            else if (playerHealth == null) // Don't damage other players (friendly fire off)
            {
                // Hit environment - spawn bullet hole
                if (bulletHolePrefab != null)
                {
                    photonView.RPC("RPC_SpawnEffect", RpcTarget.All,
                        bulletHolePrefab.name, hit.point, Quaternion.LookRotation(hit.normal));
                }
            }

            Debug.Log($"[WeaponSystem] Hit {hit.collider.name} at {hit.point}");
        }

        // Muzzle flash
        if (muzzleFlashPrefab != null && firePoint != null)
        {
            photonView.RPC("RPC_MuzzleFlash", RpcTarget.All);
        }

        // Sound
        if (fireSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(fireSound);
        }

        // Apply recoil
        currentRecoil += recoilVertical;

        OnFire?.Invoke();
        OnAmmoChanged?.Invoke(CurrentAmmo, reserveAmmo);
    }

    IEnumerator Reload()
    {
        IsReloading = true;
        ReloadProgress = 0f;
        OnReloadStart?.Invoke();

        if (reloadSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(reloadSound);
        }

        Debug.Log("[WeaponSystem] Reloading...");

        float elapsed = 0f;
        while (elapsed < reloadTime)
        {
            elapsed += Time.deltaTime;
            ReloadProgress = elapsed / reloadTime;
            yield return null;
        }

        // Calculate ammo to reload
        int ammoNeeded = maxAmmo - CurrentAmmo;
        int ammoToReload = Mathf.Min(ammoNeeded, reserveAmmo);

        CurrentAmmo += ammoToReload;
        reserveAmmo -= ammoToReload;

        IsReloading = false;
        ReloadProgress = 0f;

        OnReloadComplete?.Invoke();
        OnAmmoChanged?.Invoke(CurrentAmmo, reserveAmmo);

        Debug.Log($"[WeaponSystem] Reload complete. Ammo: {CurrentAmmo}/{reserveAmmo}");
    }

    public void AddAmmo(int amount)
    {
        reserveAmmo += amount;
        OnAmmoChanged?.Invoke(CurrentAmmo, reserveAmmo);
        Debug.Log($"[WeaponSystem] Added {amount} ammo. Reserve: {reserveAmmo}");
    }

    [PunRPC]
    void RPC_MuzzleFlash()
    {
        if (muzzleFlashPrefab != null && firePoint != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation);
            Destroy(flash, 0.1f);
        }
    }

    [PunRPC]
    void RPC_SpawnEffect(string effectName, Vector3 position, Quaternion rotation)
    {
        // Find prefab by name in resources
        GameObject prefab = Resources.Load<GameObject>("Effects/" + effectName);
        if (prefab != null)
        {
            GameObject effect = Instantiate(prefab, position, rotation);
            Destroy(effect, 5f);
        }
    }
}
