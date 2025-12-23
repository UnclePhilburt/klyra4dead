using UnityEngine;

public class AmmoUI : MonoBehaviour
{
    [Header("Style")]
    public int fontSize = 32;
    public Color textColor = Color.white;
    public Color lowAmmoColor = Color.red;
    public int lowAmmoThreshold = 5;

    [Header("Position")]
    public float xOffset = 20f;
    public float yOffset = 20f;

    private PlayerShooting shooting;
    private GUIStyle ammoStyle;

    void Start()
    {
        // Find local player's shooting component
        FindShootingComponent();
    }

    void Update()
    {
        if (shooting == null)
        {
            FindShootingComponent();
        }
    }

    void FindShootingComponent()
    {
        // Find ThirdPersonMotor (local player) and get shooting from it
        ThirdPersonMotor motor = FindFirstObjectByType<ThirdPersonMotor>();
        if (motor != null)
        {
            shooting = motor.GetComponent<PlayerShooting>();
        }
    }

    void OnGUI()
    {
        if (shooting == null) return;

        // Setup style
        if (ammoStyle == null)
        {
            ammoStyle = new GUIStyle(GUI.skin.label);
            ammoStyle.fontSize = fontSize;
            ammoStyle.fontStyle = FontStyle.Bold;
            ammoStyle.alignment = TextAnchor.LowerRight;
        }

        // Color based on ammo count
        bool isLow = shooting.CurrentAmmo <= lowAmmoThreshold;
        ammoStyle.normal.textColor = isLow ? lowAmmoColor : textColor;

        // Position in bottom right
        float width = 200f;
        float height = 50f;
        float x = Screen.width - width - xOffset;
        float y = Screen.height - height - yOffset;

        // Draw ammo text
        string ammoText = $"{shooting.CurrentAmmo} / {shooting.MagazineSize}";

        // Shadow
        GUI.color = Color.black;
        GUI.Label(new Rect(x + 2, y + 2, width, height), ammoText, ammoStyle);

        // Main text
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, width, height), ammoText, ammoStyle);
    }
}
