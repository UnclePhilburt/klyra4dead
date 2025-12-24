using UnityEngine;

public class WeaponHolder : MonoBehaviour
{
    [Header("Weapon")]
    public GameObject weaponPrefab;
    public Vector3 positionOffset = new Vector3(0.04f, 0.26f, 0f);
    public Vector3 rotationOffset = new Vector3(-90.7f, 89.9f, 6.43f);
    public float weaponScale = 0.18f;

    [Header("Auto-Find")]
    public string autoFindWeaponName = "assault1";

    [Header("Muzzle Flash")]
    public Vector3 firePointOffset = new Vector3(0f, 0f, 3f); // Forward from gun
    public GameObject muzzleFlashPrefab;

    private Transform rightHand;
    private GameObject currentWeapon;
    private Transform firePoint;
    private Animator animator;

    void Start()
    {
        // Load muzzle flash prefab from Resources if not assigned
        if (muzzleFlashPrefab == null)
        {
            muzzleFlashPrefab = Resources.Load<GameObject>("Effects/MuzzleFlash");
        }

        // Delay to let character model spawn first
        Invoke(nameof(SetupWeapon), 0.2f);
    }

    void SetupWeapon()
    {
        animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError("[WeaponHolder] No animator found!");
            return;
        }

        if (!animator.isHuman)
        {
            Debug.LogError("[WeaponHolder] Animator is not humanoid!");
            return;
        }

        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

        if (rightHand == null)
        {
            Debug.LogError("[WeaponHolder] Right hand bone not found!");
            return;
        }

        Debug.Log($"[WeaponHolder] Found right hand: {rightHand.name}");

        // Try to auto-find weapon if none assigned
        if (weaponPrefab == null && !string.IsNullOrEmpty(autoFindWeaponName))
        {
            // Try prefab first
            weaponPrefab = Resources.Load<GameObject>($"Weapons/{autoFindWeaponName}");

            if (weaponPrefab == null)
            {
                // Try loading FBX directly from Low Poly Guns
                #if UNITY_EDITOR
                weaponPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Low Poly Guns/Models/Guns/{autoFindWeaponName}/{autoFindWeaponName}.fbx");
                if (weaponPrefab != null)
                {
                    Debug.Log($"[WeaponHolder] Loaded weapon from FBX: {autoFindWeaponName}");
                }
                #endif
            }
            else
            {
                Debug.Log($"[WeaponHolder] Loaded weapon prefab: {autoFindWeaponName}");
            }
        }

        if (weaponPrefab != null)
        {
            AttachWeapon(weaponPrefab);
        }
        else
        {
            Debug.LogWarning("[WeaponHolder] No weapon prefab assigned! Run Tools > Create Weapon Prefabs first, or assign one manually.");
        }
    }

    public void AttachWeapon(GameObject prefab)
    {
        if (rightHand == null) return;

        // Remove old weapon
        if (currentWeapon != null)
        {
            Destroy(currentWeapon);
        }

        // Spawn new weapon
        currentWeapon = Instantiate(prefab, rightHand);
        currentWeapon.transform.localPosition = positionOffset;
        currentWeapon.transform.localRotation = Quaternion.Euler(rotationOffset);
        currentWeapon.transform.localScale = Vector3.one * weaponScale;
        currentWeapon.name = "Weapon";

        // Disable any colliders on the weapon so it doesn't interfere
        foreach (var col in currentWeapon.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        // Create FirePoint for muzzle flash
        CreateFirePoint();

        Debug.Log($"[WeaponHolder] Attached weapon: {prefab.name} at scale {weaponScale}");
    }

    void CreateFirePoint()
    {
        if (currentWeapon == null) return;

        // Try to find existing barrel/muzzle transform
        Transform barrelTransform = FindBarrelTransform(currentWeapon.transform);

        // Create FirePoint
        GameObject firePointObj = new GameObject("FirePoint");
        
        if (barrelTransform != null)
        {
            // Attach to barrel
            firePointObj.transform.SetParent(barrelTransform);
            firePointObj.transform.localPosition = Vector3.forward * 0.5f; // Slightly forward from barrel
            firePointObj.transform.localRotation = Quaternion.identity;
            Debug.Log($"[WeaponHolder] FirePoint attached to barrel: {barrelTransform.name}");
        }
        else
        {
            // Attach to weapon root with offset
            firePointObj.transform.SetParent(currentWeapon.transform);
            firePointObj.transform.localPosition = firePointOffset;
            firePointObj.transform.localRotation = Quaternion.identity;
            Debug.Log("[WeaponHolder] FirePoint attached to weapon with offset");
        }

        firePoint = firePointObj.transform;

        // Assign to shooting scripts
        AssignToShootingScripts();
    }

    Transform FindBarrelTransform(Transform root)
    {
        string[] barrelNames = { "barrel", "muzzle", "flash", "tip", "end", "front" };

        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            string nameLower = child.name.ToLower();
            foreach (string barrelName in barrelNames)
            {
                if (nameLower.Contains(barrelName))
                {
                    return child;
                }
            }
        }

        // If no barrel found, find the furthest forward point
        Transform furthest = null;
        float maxZ = float.MinValue;

        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            if (child.localPosition.z > maxZ)
            {
                maxZ = child.localPosition.z;
                furthest = child;
            }
        }

        return furthest != root ? furthest : null;
    }

    void AssignToShootingScripts()
    {
        // Assign to PlayerShooting
        PlayerShooting ps = GetComponent<PlayerShooting>();
        if (ps != null)
        {
            ps.firePoint = firePoint;
            if (muzzleFlashPrefab != null)
            {
                ps.muzzleFlashPrefab = muzzleFlashPrefab;
            }
            Debug.Log("[WeaponHolder] Assigned FirePoint and MuzzleFlash to PlayerShooting");
        }

        // Also assign to WeaponSystem if present
        WeaponSystem ws = GetComponent<WeaponSystem>();
        if (ws != null)
        {
            ws.firePoint = firePoint;
            if (muzzleFlashPrefab != null)
            {
                ws.muzzleFlashPrefab = muzzleFlashPrefab;
            }
            Debug.Log("[WeaponHolder] Assigned FirePoint and MuzzleFlash to WeaponSystem");
        }
    }

    public Transform GetFirePoint()
    {
        return firePoint;
    }

    public GameObject GetCurrentWeapon()
    {
        return currentWeapon;
    }

    // Adjust in inspector
    void OnValidate()
    {
        if (currentWeapon != null)
        {
            currentWeapon.transform.localPosition = positionOffset;
            currentWeapon.transform.localRotation = Quaternion.Euler(rotationOffset);
            currentWeapon.transform.localScale = Vector3.one * weaponScale;
        }
    }
}
