using UnityEngine;
using Photon.Pun;

/// <summary>
/// Player flashlight attached to head - like a headlamp.
/// Follows head rotation for natural look direction.
/// </summary>
public class Flashlight : MonoBehaviourPun
{
    [Header("Light Settings")]
    public float range = 30f;
    public float spotAngle = 50f;
    public float intensity = 2.5f;
    public Color lightColor = new Color(1f, 0.95f, 0.8f);

    [Header("Head Attachment")]
    public Vector3 headOffset = new Vector3(0f, 0.1f, 0.15f); // Slightly above and in front of head
    public string headBoneName = "Head"; // Common bone names: Head, head, Bip01 Head

    [Header("Battery")]
    public bool useBattery = true;
    public float batteryLife = 300f;
    public float currentBattery;
    public float lowBatteryThreshold = 0.2f;

    [Header("Flickering")]
    public float flickerSpeed = 15f;
    public float flickerIntensity = 0.5f;

    [Header("Controls")]
    public KeyCode toggleKey = KeyCode.F;

    [Header("Audio")]
    public AudioClip toggleSound;

    private Light spotlight;
    private GameObject lightObj;
    private Transform headBone;
    private bool isOn = true;
    private float baseIntensity;
    private AudioSource audioSource;

    void Start()
    {
        currentBattery = batteryLife;
        baseIntensity = intensity;

        // Find head bone
        FindHeadBone();

        if (PhotonNetwork.IsConnected && !photonView.IsMine)
        {
            SetupFlashlightRemote();
            return;
        }

        SetupFlashlight();
    }

    void FindHeadBone()
    {
        // Try to find head bone in hierarchy
        string[] possibleNames = { "Head", "head", "Bip01 Head", "Bip001 Head", "mixamorig:Head", "HEAD" };
        
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // Try using Avatar's head bone
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone != null)
            {
                Debug.Log("[Flashlight] Found head via Animator: " + headBone.name);
                return;
            }
        }

        // Fallback: search by name
        foreach (string boneName in possibleNames)
        {
            headBone = FindChildRecursive(transform, boneName);
            if (headBone != null)
            {
                Debug.Log("[Flashlight] Found head by name: " + headBone.name);
                return;
            }
        }

        // Last resort: use the player transform itself
        Debug.LogWarning("[Flashlight] Could not find head bone, using player transform");
        headBone = transform;
    }

    Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name.ToLower().Contains(name.ToLower()))
                return child;
            
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    void SetupFlashlight()
    {
        lightObj = new GameObject("Headlamp");
        
        // Parent to head bone if found
        if (headBone != null)
        {
            lightObj.transform.SetParent(headBone);
            lightObj.transform.localPosition = headOffset;
            lightObj.transform.localRotation = Quaternion.identity;
        }
        else
        {
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = new Vector3(0, 1.7f, 0.2f);
        }

        spotlight = lightObj.AddComponent<Light>();
        spotlight.type = LightType.Spot;
        spotlight.range = range;
        spotlight.spotAngle = spotAngle;
        spotlight.intensity = intensity;
        spotlight.color = lightColor;
        spotlight.shadows = LightShadows.Soft;
        spotlight.shadowStrength = 1f;
        spotlight.innerSpotAngle = spotAngle * 0.4f;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.5f;

        Debug.Log("[Flashlight] Headlamp setup complete!");
    }

    void SetupFlashlightRemote()
    {
        lightObj = new GameObject("Headlamp");
        
        if (headBone != null)
        {
            lightObj.transform.SetParent(headBone);
            lightObj.transform.localPosition = headOffset;
            lightObj.transform.localRotation = Quaternion.identity;
        }
        else
        {
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = new Vector3(0, 1.7f, 0.2f);
        }

        spotlight = lightObj.AddComponent<Light>();
        spotlight.type = LightType.Spot;
        spotlight.range = range;
        spotlight.spotAngle = spotAngle;
        spotlight.intensity = intensity;
        spotlight.color = lightColor;
        spotlight.shadows = LightShadows.None;
    }

    void Update()
    {
        if (spotlight == null) return;

        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;

        // Toggle
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleFlashlight();
        }

        if (!isOn) return;

        // Battery drain
        if (useBattery)
        {
            currentBattery -= Time.deltaTime;
            if (currentBattery <= 0)
            {
                currentBattery = 0;
                TurnOff();
                return;
            }

            // Low battery flickering
            float batteryPercent = currentBattery / batteryLife;
            if (batteryPercent < lowBatteryThreshold)
            {
                float flicker = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f);
                float flickerAmount = (1f - batteryPercent / lowBatteryThreshold) * flickerIntensity;
                spotlight.intensity = baseIntensity * (1f - flickerAmount + flicker * flickerAmount);
            }
            else
            {
                spotlight.intensity = baseIntensity;
            }
        }
    }

    public void ToggleFlashlight()
    {
        if (isOn) TurnOff();
        else TurnOn();
    }

    public void TurnOn()
    {
        if (currentBattery <= 0 && useBattery) return;

        isOn = true;
        if (spotlight != null) spotlight.enabled = true;

        if (toggleSound != null && audioSource != null)
            audioSource.PlayOneShot(toggleSound);

        if (PhotonNetwork.IsConnected)
            photonView.RPC("RPC_SetFlashlight", RpcTarget.Others, true);
    }

    public void TurnOff()
    {
        isOn = false;
        if (spotlight != null) spotlight.enabled = false;

        if (toggleSound != null && audioSource != null)
            audioSource.PlayOneShot(toggleSound);

        if (PhotonNetwork.IsConnected)
            photonView.RPC("RPC_SetFlashlight", RpcTarget.Others, false);
    }

    [PunRPC]
    void RPC_SetFlashlight(bool on)
    {
        if (spotlight != null)
            spotlight.enabled = on;
    }

    public void AddBattery(float amount)
    {
        currentBattery = Mathf.Min(currentBattery + amount, batteryLife);
    }

    public float GetBatteryPercent()
    {
        return currentBattery / batteryLife;
    }

    void OnGUI()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;
        if (!useBattery) return;

        float percent = GetBatteryPercent();
        Color barColor = percent > 0.3f ? Color.green : (percent > 0.1f ? Color.yellow : Color.red);

        GUI.color = Color.black;
        GUI.Box(new Rect(Screen.width - 120, Screen.height - 40, 104, 24), "");
        
        GUI.color = barColor;
        GUI.Box(new Rect(Screen.width - 118, Screen.height - 38, 100 * percent, 20), "");
        
        GUI.color = Color.white;
        GUI.Label(new Rect(Screen.width - 115, Screen.height - 38, 100, 20), 
            $"[F] Light: {(percent * 100):F0}%");
    }
}
