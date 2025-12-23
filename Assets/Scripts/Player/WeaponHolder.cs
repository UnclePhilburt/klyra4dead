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

    private Transform rightHand;
    private GameObject currentWeapon;
    private Animator animator;

    void Start()
    {
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
                    // FBX needs different scale than prefab
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

        Debug.Log($"[WeaponHolder] Attached weapon: {prefab.name} at scale {weaponScale}");
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
