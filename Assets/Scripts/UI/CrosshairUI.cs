using UnityEngine;
using Photon.Pun;
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

    [Header("ADS Gap")]
    public float gapIdle = 6f;
    public float gapMoving = 25f;

    [Header("Animation")]
    public float transitionSpeed = 12f;
    public float gapSpeed = 8f;

    [Header("Recoil")]
    public float recoilAmount = 12f;
    public float recoilRecoverySpeed = 60f;

    private RectTransform crosshairParent;
    private Image circleImage;
    private Image topLine, bottomLine, leftLine, rightLine;

    private bool isAiming;
    private bool isMoving;
    private float adsLerp = 0f;
    private float currentGap;
    private float currentRecoil = 0f;
    private ThirdPersonMotor motor;
    private ThirdPersonController controller;

    public static CrosshairUI Instance { get; private set; }
    private Sprite circleSprite;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        CreateCircleSprite();
        CreateCrosshairUI();
        currentGap = gapIdle;
    }

    public void AddRecoil() { currentRecoil = recoilAmount; }

    void CreateCircleSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = size / 2f - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float aa = Mathf.Clamp01(radius - dist + 1f);
                tex.SetPixel(x, y, dist <= radius ? new Color(1, 1, 1, aa) : Color.clear);
            }
        }
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    void CreateCrosshairUI()
    {
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

        GameObject parentObj = new GameObject("Crosshair");
        parentObj.transform.SetParent(canvas.transform, false);
        crosshairParent = parentObj.AddComponent<RectTransform>();
        crosshairParent.anchorMin = crosshairParent.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairParent.anchoredPosition = Vector2.zero;

        circleImage = CreateImage("Dot", crosshairParent, circleSprite, dotColor, dotSize);
        topLine = CreateImage("Top", crosshairParent, null, lineColor, 0);
        bottomLine = CreateImage("Bottom", crosshairParent, null, lineColor, 0);
        leftLine = CreateImage("Left", crosshairParent, null, lineColor, 0);
        rightLine = CreateImage("Right", crosshairParent, null, lineColor, 0);
    }

    Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color, float size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Image img = obj.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        if (size > 0) rt.sizeDelta = new Vector2(size, size);
        return img;
    }

    void Update()
    {
        // Find local player controller - also re-find if destroyed or disabled
        if (controller == null || !controller.isActiveAndEnabled)
        {
            controller = null;
            foreach (var c in FindObjectsByType<ThirdPersonController>(FindObjectsSortMode.None))
            {
                if (!c.isActiveAndEnabled) continue;
                var pv = c.GetComponent<PhotonView>();
                if (pv == null || pv.IsMine) { controller = c; break; }
            }
        }
        if (controller == null && (motor == null || !motor.isActiveAndEnabled))
        {
            motor = null;
            foreach (var m in FindObjectsByType<ThirdPersonMotor>(FindObjectsSortMode.None))
            {
                if (!m.isActiveAndEnabled) continue;
                var pv = m.GetComponent<PhotonView>();
                if (pv == null || pv.IsMine) { motor = m; break; }
            }
        }

        // Get aiming state
        if (controller != null && controller.isActiveAndEnabled) isAiming = controller.IsAiming;
        else if (motor != null && motor.isActiveAndEnabled) isAiming = motor.IsAiming;
        else isAiming = false;

        // Movement check
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        isMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;

        // Animate
        adsLerp = Mathf.MoveTowards(adsLerp, isAiming ? 1f : 0f, transitionSpeed * Time.deltaTime);
        currentGap = Mathf.MoveTowards(currentGap, isMoving ? gapMoving : gapIdle, gapSpeed * Time.deltaTime * 20f);
        currentRecoil = Mathf.MoveTowards(currentRecoil, 0f, recoilRecoverySpeed * Time.deltaTime);

        UpdateCrosshair();
    }

    void UpdateCrosshair()
    {
        if (circleImage == null) return;

        Color dColor = dotColor;
        dColor.a = Mathf.Lerp(1f, 0f, adsLerp);
        circleImage.color = dColor;
        circleImage.rectTransform.sizeDelta = new Vector2(dotSize, dotSize);

        Color lColor = lineColor;
        lColor.a = Mathf.Lerp(0f, 1f, adsLerp);
        float gap = currentGap + currentRecoil;

        SetLine(topLine, lColor, lineThickness, lineLength, new Vector2(0, gap + lineLength / 2f));
        SetLine(bottomLine, lColor, lineThickness, lineLength, new Vector2(0, -(gap + lineLength / 2f)));
        SetLine(leftLine, lColor, lineLength, lineThickness, new Vector2(-(gap + lineLength / 2f), 0));
        SetLine(rightLine, lColor, lineLength, lineThickness, new Vector2(gap + lineLength / 2f, 0));
    }

    void SetLine(Image line, Color color, float width, float height, Vector2 pos)
    {
        if (line == null) return;
        line.color = color;
        line.rectTransform.sizeDelta = new Vector2(width, height);
        line.rectTransform.anchoredPosition = pos;
    }
}
