using UnityEngine;
using UnityEngine.UI;

public class CrosshairUI : MonoBehaviour
{
    [Header("Hip Fire - Dot")]
    public float dotSize = 8f;
    public Color dotColor = Color.white;

    [Header("ADS - Crosshair Lines")]
    public float lineLength = 15f;
    public float lineThickness = 2f;
    public Color lineColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("ADS Gap (expands when moving)")]
    public float gapIdle = 6f;
    public float gapMoving = 25f;

    [Header("Animation")]
    public float transitionSpeed = 12f;
    public float gapSpeed = 8f;

    [Header("Recoil")]
    public float recoilAmount = 12f;
    public float recoilRecoverySpeed = 60f;

    // UI Elements
    private RectTransform crosshairParent;
    private Image circleImage;
    private Image topLine;
    private Image bottomLine;
    private Image leftLine;
    private Image rightLine;

    // State
    private bool isAiming;
    private bool isMoving;
    private float adsLerp = 0f;
    private float currentGap;
    private float currentRecoil = 0f;
    private ThirdPersonMotor motor;

    // Singleton for easy access
    public static CrosshairUI Instance { get; private set; }

    // Circle sprite
    private Sprite circleSprite;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        CreateCircleSprite();
        CreateCrosshairUI();
        motor = FindAnyObjectByType<ThirdPersonMotor>();
        currentGap = gapIdle;
    }

    public void AddRecoil()
    {
        currentRecoil = recoilAmount;
    }

    void CreateCircleSprite()
    {
        // Create a filled circle texture (simple dot)
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color transparent = new Color(0, 0, 0, 0);

        float center = size / 2f;
        float radius = size / 2f - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= radius)
                {
                    // Anti-alias the edge
                    float aa = Mathf.Clamp01(radius - dist + 1f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, aa));
                }
                else
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
        }

        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    void CreateCrosshairUI()
    {
        // Find or create canvas
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("UI Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create crosshair parent
        GameObject parentObj = new GameObject("Crosshair");
        parentObj.transform.SetParent(canvas.transform, false);
        crosshairParent = parentObj.AddComponent<RectTransform>();
        crosshairParent.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairParent.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairParent.anchoredPosition = Vector2.zero;

        // Create dot for hip fire
        GameObject circleObj = new GameObject("Dot");
        circleObj.transform.SetParent(crosshairParent, false);
        circleImage = circleObj.AddComponent<Image>();
        circleImage.sprite = circleSprite;
        circleImage.color = dotColor;
        circleImage.raycastTarget = false;
        RectTransform circleRect = circleImage.rectTransform;
        circleRect.anchorMin = new Vector2(0.5f, 0.5f);
        circleRect.anchorMax = new Vector2(0.5f, 0.5f);
        circleRect.sizeDelta = new Vector2(dotSize, dotSize);
        circleRect.anchoredPosition = Vector2.zero;

        // Create the four lines for ADS
        topLine = CreateLine("Top", crosshairParent);
        bottomLine = CreateLine("Bottom", crosshairParent);
        leftLine = CreateLine("Left", crosshairParent);
        rightLine = CreateLine("Right", crosshairParent);

        UpdateCrosshair();
    }

    Image CreateLine(string name, RectTransform parent)
    {
        GameObject lineObj = new GameObject(name);
        lineObj.transform.SetParent(parent, false);
        Image img = lineObj.AddComponent<Image>();
        img.color = lineColor;
        img.raycastTarget = false;
        return img;
    }

    void Update()
    {
        // Get state from motor
        if (motor != null)
        {
            isAiming = motor.IsAiming;
            // Check if moving based on input
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            isMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;
        }

        // Animate ADS transition
        float targetAds = isAiming ? 1f : 0f;
        adsLerp = Mathf.MoveTowards(adsLerp, targetAds, transitionSpeed * Time.deltaTime);

        // Animate gap based on movement (only when ADS)
        float targetGap = isMoving ? gapMoving : gapIdle;
        currentGap = Mathf.MoveTowards(currentGap, targetGap, gapSpeed * Time.deltaTime * 20f);

        // Decay recoil
        currentRecoil = Mathf.MoveTowards(currentRecoil, 0f, recoilRecoverySpeed * Time.deltaTime);

        UpdateCrosshair();
    }

    void UpdateCrosshair()
    {
        // Dot visibility (hip fire)
        float dotAlpha = Mathf.Lerp(1f, 0f, adsLerp);
        Color dColor = dotColor;
        dColor.a = dotAlpha;
        circleImage.color = dColor;
        circleImage.rectTransform.sizeDelta = new Vector2(dotSize, dotSize);

        // Lines visibility (ADS)
        float lineAlpha = Mathf.Lerp(0f, 1f, adsLerp);
        Color lColor = lineColor;
        lColor.a = lineAlpha;

        // Apply recoil to gap
        float gap = currentGap + currentRecoil;
        float len = lineLength;
        float thick = lineThickness;

        // Top line
        RectTransform topRect = topLine.rectTransform;
        topRect.anchorMin = new Vector2(0.5f, 0.5f);
        topRect.anchorMax = new Vector2(0.5f, 0.5f);
        topRect.sizeDelta = new Vector2(thick, len);
        topRect.anchoredPosition = new Vector2(0, gap + len / 2f);
        topLine.color = lColor;

        // Bottom line
        RectTransform bottomRect = bottomLine.rectTransform;
        bottomRect.anchorMin = new Vector2(0.5f, 0.5f);
        bottomRect.anchorMax = new Vector2(0.5f, 0.5f);
        bottomRect.sizeDelta = new Vector2(thick, len);
        bottomRect.anchoredPosition = new Vector2(0, -(gap + len / 2f));
        bottomLine.color = lColor;

        // Left line
        RectTransform leftRect = leftLine.rectTransform;
        leftRect.anchorMin = new Vector2(0.5f, 0.5f);
        leftRect.anchorMax = new Vector2(0.5f, 0.5f);
        leftRect.sizeDelta = new Vector2(len, thick);
        leftRect.anchoredPosition = new Vector2(-(gap + len / 2f), 0);
        leftLine.color = lColor;

        // Right line
        RectTransform rightRect = rightLine.rectTransform;
        rightRect.anchorMin = new Vector2(0.5f, 0.5f);
        rightRect.anchorMax = new Vector2(0.5f, 0.5f);
        rightRect.sizeDelta = new Vector2(len, thick);
        rightRect.anchoredPosition = new Vector2(gap + len / 2f, 0);
        rightLine.color = lColor;
    }
}
