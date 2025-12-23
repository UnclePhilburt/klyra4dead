using UnityEngine;

public class TutorialQuestUI : MonoBehaviour
{
    [Header("Quest Settings")]
    public string questText = "REACH THE SAFE ZONE";
    public string completedText = "SAFE ZONE UNLOCKED!";
    public float completedDisplayTime = 3f;

    [Header("Waypoint")]
    public Transform safeZoneTarget;
    public Color waypointColor = Color.green;
    public float markerSize = 30f;

    [Header("Style")]
    public int fontSize = 24;
    public Color questColor = Color.yellow;
    public Color completedColor = Color.green;

    private bool questComplete = false;
    private float completedTime;
    private GUIStyle style;
    private GUIStyle distanceStyle;
    private Texture2D markerTexture;

    public static TutorialQuestUI Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Check if already completed
        if (SpawnManager.Instance != null && SpawnManager.Instance.hasReachedSafeZone)
        {
            questComplete = true;
            completedTime = -999f; // Don't show anything
            Debug.Log("[TutorialQuest] Quest already completed - not showing");
        }
        else
        {
            Debug.Log("[TutorialQuest] Quest active - showing objective");
        }

        // Create marker texture
        markerTexture = new Texture2D(1, 1);
        markerTexture.SetPixel(0, 0, Color.white);
        markerTexture.Apply();
    }

    public void CompleteQuest()
    {
        if (questComplete) return;

        questComplete = true;
        completedTime = Time.time;

        Debug.Log("[Tutorial] Quest completed!");
    }

    void OnGUI()
    {
        // Don't show if already completed before this session
        if (questComplete && completedTime < 0)
        {
            return;
        }

        // Setup style
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = fontSize;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.UpperCenter;
        }

        string text;
        Color color;

        if (!questComplete)
        {
            text = questText;
            color = questColor;
        }
        else if (Time.time - completedTime < completedDisplayTime)
        {
            text = completedText;
            color = completedColor;
        }
        else
        {
            return; // Hide after completed message shown
        }

        style.normal.textColor = color;

        // Position at top center
        float width = 400f;
        float height = 50f;
        float x = (Screen.width - width) / 2f;
        float y = 60f;

        // Shadow
        GUI.color = Color.black;
        GUI.Label(new Rect(x + 2, y + 2, width, height), text, style);

        // Main text
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, width, height), text, style);

        // Draw waypoint marker if quest not complete
        if (!questComplete)
        {
            DrawWaypoint();
        }
    }

    void DrawWaypoint()
    {
        if (safeZoneTarget == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // Get screen position of safe zone
        Vector3 screenPos = cam.WorldToScreenPoint(safeZoneTarget.position);

        // Check if in front of camera
        bool isBehind = screenPos.z < 0;

        // Convert to GUI coordinates (flip Y)
        screenPos.y = Screen.height - screenPos.y;

        // Setup distance style
        if (distanceStyle == null)
        {
            distanceStyle = new GUIStyle(GUI.skin.label);
            distanceStyle.fontSize = 16;
            distanceStyle.fontStyle = FontStyle.Bold;
            distanceStyle.alignment = TextAnchor.MiddleCenter;
        }

        // Calculate distance
        ThirdPersonMotor player = FindFirstObjectByType<ThirdPersonMotor>();
        float distance = 0f;
        if (player != null)
        {
            distance = Vector3.Distance(player.transform.position, safeZoneTarget.position);
        }

        // If behind or off screen, show arrow at edge
        if (isBehind || screenPos.x < 0 || screenPos.x > Screen.width || screenPos.y < 0 || screenPos.y > Screen.height)
        {
            // Clamp to screen edge with padding
            float padding = 50f;

            if (isBehind)
            {
                screenPos.x = Screen.width - screenPos.x;
                screenPos.y = Screen.height - screenPos.y;
            }

            screenPos.x = Mathf.Clamp(screenPos.x, padding, Screen.width - padding);
            screenPos.y = Mathf.Clamp(screenPos.y, padding, Screen.height - padding);

            // Draw arrow pointing to edge
            GUI.color = waypointColor;
            DrawMarker(screenPos, markerSize, true);

            // Distance text
            distanceStyle.normal.textColor = waypointColor;
            GUI.Label(new Rect(screenPos.x - 50, screenPos.y + markerSize, 100, 30), $"{distance:F0}m", distanceStyle);
        }
        else
        {
            // On screen - draw marker at position
            GUI.color = waypointColor;
            DrawMarker(screenPos, markerSize, false);

            // Distance text below marker
            distanceStyle.normal.textColor = waypointColor;
            GUI.Label(new Rect(screenPos.x - 50, screenPos.y + markerSize, 100, 30), $"{distance:F0}m", distanceStyle);
        }
    }

    void DrawMarker(Vector3 pos, float size, bool isOffScreen)
    {
        if (markerTexture == null) return;

        // Draw diamond shape
        float halfSize = size / 2f;

        // Draw as rotated square (diamond)
        Matrix4x4 matrixBackup = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, new Vector2(pos.x, pos.y));

        float squareSize = size * 0.7f;
        GUI.DrawTexture(new Rect(pos.x - squareSize/2, pos.y - squareSize/2, squareSize, squareSize), markerTexture);

        GUI.matrix = matrixBackup;

        // Draw inner diamond (darker)
        GUI.color = new Color(0, 0, 0, 0.5f);
        GUIUtility.RotateAroundPivot(45f, new Vector2(pos.x, pos.y));
        float innerSize = squareSize * 0.6f;
        GUI.DrawTexture(new Rect(pos.x - innerSize/2, pos.y - innerSize/2, innerSize, innerSize), markerTexture);
        GUI.matrix = matrixBackup;
    }
}
