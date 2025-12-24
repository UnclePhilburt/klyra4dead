using UnityEngine;

public class MuzzleFlash : MonoBehaviour
{
    [Header("Light Flash")]
    public Color flashColor = new Color(1f, 0.8f, 0.4f);
    public float lightIntensity = 3f;
    public float lightRange = 10f;

    [Header("Timing")]
    public float flashDuration = 0.05f;
    public float totalLifetime = 0.15f;

    private Light flashLight;
    private ParticleSystem sparksParticle;
    private ParticleSystem smokeParticle;
    private float spawnTime;

    void Awake()
    {
        spawnTime = Time.time;
        CreateEffects();
    }

    void CreateEffects()
    {
        // Create flash light
        GameObject lightObj = new GameObject("FlashLight");
        lightObj.transform.SetParent(transform);
        lightObj.transform.localPosition = Vector3.zero;

        flashLight = lightObj.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = flashColor;
        flashLight.intensity = lightIntensity;
        flashLight.range = lightRange;
        flashLight.shadows = LightShadows.None;

        // Create sparks particle system
        GameObject sparksObj = new GameObject("Sparks");
        sparksObj.transform.SetParent(transform);
        sparksObj.transform.localPosition = Vector3.zero;
        sparksObj.transform.localRotation = Quaternion.identity;

        sparksParticle = sparksObj.AddComponent<ParticleSystem>();
        SetupSparks(sparksParticle);
        sparksParticle.Play();

        // Create smoke puff
        GameObject smokeObj = new GameObject("Smoke");
        smokeObj.transform.SetParent(transform);
        smokeObj.transform.localPosition = Vector3.zero;
        smokeObj.transform.localRotation = Quaternion.identity;

        smokeParticle = smokeObj.AddComponent<ParticleSystem>();
        SetupSmoke(smokeParticle);
        smokeParticle.Play();

        // Create flash sprite
        GameObject spriteObj = new GameObject("FlashSprite");
        spriteObj.transform.SetParent(transform);
        spriteObj.transform.localPosition = Vector3.zero;
        spriteObj.transform.localScale = Vector3.one * 0.3f;

        SpriteRenderer sr = spriteObj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateFlashSprite();
        sr.color = new Color(1f, 0.9f, 0.6f, 0.9f);
        sr.material = new Material(Shader.Find("Sprites/Default"));

        // Make sprite face camera
        spriteObj.AddComponent<BillboardSprite>();
    }

    void SetupSparks(ParticleSystem ps)
    {
        // Stop the system before modifying properties
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        
        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = 0.1f;
        main.startSpeed = 15f;
        main.startSize = 0.02f;
        main.startColor = new Color(1f, 0.7f, 0.3f);
        main.maxParticles = 20;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 10, 15)
        });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.01f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.8f, 0.3f), 0f),
                new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, 0f);

        // Renderer
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.SetColor("_Color", Color.white);
    }

    void SetupSmoke(ParticleSystem ps)
    {
        // Stop the system before modifying properties
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        
        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = 0.3f;
        main.startSpeed = 2f;
        main.startSize = 0.1f;
        main.startColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
        main.maxParticles = 5;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 3, 5)
        });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.02f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.4f, 0.4f, 0.4f), 0f),
                new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.4f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, 2f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.SetColor("_Color", Color.white);
    }

    Sprite CreateFlashSprite()
    {
        // Create a simple white circle texture for the flash
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1f - (dist / radius));
                alpha = alpha * alpha; // Falloff
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    void Update()
    {
        float elapsed = Time.time - spawnTime;

        // Fade out light quickly
        if (flashLight != null)
        {
            float lightT = elapsed / flashDuration;
            flashLight.intensity = Mathf.Lerp(lightIntensity, 0f, lightT);
        }

        // Destroy after lifetime
        if (elapsed >= totalLifetime)
        {
            Destroy(gameObject);
        }
    }
}

// Helper component to make sprite always face camera
public class BillboardSprite : MonoBehaviour
{
    void LateUpdate()
    {
        if (Camera.main != null)
        {
            transform.LookAt(Camera.main.transform);
            transform.Rotate(0, 180, 0);
        }
    }
}
