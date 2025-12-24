using UnityEngine;
using System.Collections;

public class HorrorAmbience : MonoBehaviour
{
    public static HorrorAmbience Instance { get; private set; }

    [Header("Ambient Audio")]
    public AudioClip[] ambientLoops;
    public AudioClip[] scaryStingers;
    public AudioClip[] distantSounds;
    [Range(0f, 1f)] public float ambientVolume = 0.15f;
    [Range(0f, 1f)] public float stingerVolume = 0.4f;
    [Range(0f, 1f)] public float distantVolume = 0.2f;

    [Header("Lightning")]
    public bool enableLightning = true;
    public float minLightningInterval = 30f;
    public float maxLightningInterval = 120f;
    public AudioClip[] thunderSounds;
    [Range(0f, 1f)] public float thunderVolume = 0.5f;
    public Light lightningLight;
    private float nextLightningTime;

    [Header("Distant Sounds")]
    public float minDistantSoundInterval = 20f;
    public float maxDistantSoundInterval = 60f;
    private float nextDistantSoundTime;

    [Header("Heartbeat (When Low Health)")]
    public AudioClip heartbeatLoop;
    [Range(0f, 1f)] public float heartbeatVolume = 0.3f;
    public float heartbeatThreshold = 0.3f;

    [Header("Combat Transition")]
    public float fadeSpeed = 2f;

    private AudioSource ambientSource;
    private AudioSource effectsSource;
    private AudioSource heartbeatSource;
    private PlayerHealth localPlayer;
    
    private bool inCombat = false;
    private float targetAmbientVolume;
    private bool ambientStarted = false;
    private bool heartbeatStarted = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.loop = true;
        ambientSource.spatialBlend = 0f;
        ambientSource.volume = 0f;
        ambientSource.playOnAwake = false;

        effectsSource = gameObject.AddComponent<AudioSource>();
        effectsSource.spatialBlend = 0f;
        effectsSource.playOnAwake = false;

        heartbeatSource = gameObject.AddComponent<AudioSource>();
        heartbeatSource.loop = true;
        heartbeatSource.spatialBlend = 0f;
        heartbeatSource.volume = 0f;
        heartbeatSource.playOnAwake = false;

        if (enableLightning && lightningLight == null)
        {
            GameObject lightObj = new GameObject("LightningLight");
            lightningLight = lightObj.AddComponent<Light>();
            lightningLight.type = LightType.Directional;
            lightningLight.color = new Color(0.8f, 0.85f, 1f);
            lightningLight.intensity = 0f;
            lightningLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        nextLightningTime = Time.time + Random.Range(minLightningInterval, maxLightningInterval);
        nextDistantSoundTime = Time.time + Random.Range(5f, minDistantSoundInterval);
        targetAmbientVolume = ambientVolume;

        StartCoroutine(FindLocalPlayer());
    }

    IEnumerator FindLocalPlayer()
    {
        while (localPlayer == null)
        {
            foreach (var ph in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
            {
                var pv = ph.GetComponent<Photon.Pun.PhotonView>();
                if (pv != null && pv.IsMine)
                {
                    localPlayer = ph;
                    break;
                }
            }
            yield return new WaitForSeconds(1f);
        }
    }

    void Update()
    {
        int chasingCount = ZombieAI.GetChasingCount();
        inCombat = chasingCount > 0;

        // Start ambient only when we have clips and not in combat
        if (!ambientStarted && !inCombat && ambientLoops != null && ambientLoops.Length > 0)
        {
            ambientSource.clip = ambientLoops[Random.Range(0, ambientLoops.Length)];
            ambientSource.Play();
            ambientStarted = true;
        }

        targetAmbientVolume = inCombat ? 0f : ambientVolume;

        if (ambientSource != null && ambientStarted)
        {
            ambientSource.volume = Mathf.MoveTowards(ambientSource.volume, targetAmbientVolume, fadeSpeed * Time.deltaTime);
        }

        if (!inCombat)
        {
            if (enableLightning && Time.time >= nextLightningTime)
            {
                StartCoroutine(DoLightning());
                nextLightningTime = Time.time + Random.Range(minLightningInterval, maxLightningInterval);
            }

            if (distantSounds != null && distantSounds.Length > 0 && Time.time >= nextDistantSoundTime)
            {
                PlayDistantSound();
                nextDistantSoundTime = Time.time + Random.Range(minDistantSoundInterval, maxDistantSoundInterval);
            }
        }

        UpdateHeartbeat();
    }

    IEnumerator DoLightning()
    {
        if (lightningLight == null) yield break;

        int flashes = Random.Range(1, 4);
        for (int i = 0; i < flashes; i++)
        {
            lightningLight.intensity = Random.Range(1.5f, 3f);
            yield return new WaitForSeconds(Random.Range(0.05f, 0.1f));
            lightningLight.intensity = 0f;
            yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
        }

        if (thunderSounds != null && thunderSounds.Length > 0)
        {
            yield return new WaitForSeconds(Random.Range(0.5f, 2f));
            effectsSource.PlayOneShot(thunderSounds[Random.Range(0, thunderSounds.Length)], thunderVolume);
        }
    }

    void PlayDistantSound()
    {
        if (distantSounds == null || distantSounds.Length == 0) return;
        AudioClip clip = distantSounds[Random.Range(0, distantSounds.Length)];
        effectsSource.PlayOneShot(clip, distantVolume);
    }

    void UpdateHeartbeat()
    {
        if (heartbeatSource == null || heartbeatLoop == null) return;

        float targetVolume = 0f;

        if (localPlayer != null && !localPlayer.IsDead)
        {
            float healthPercent = localPlayer.currentHealth / localPlayer.maxHealth;
            if (healthPercent < heartbeatThreshold)
            {
                // Start heartbeat only when needed
                if (!heartbeatStarted)
                {
                    heartbeatSource.clip = heartbeatLoop;
                    heartbeatSource.Play();
                    heartbeatStarted = true;
                }
                targetVolume = (1f - healthPercent / heartbeatThreshold) * heartbeatVolume;
            }
        }

        heartbeatSource.volume = Mathf.Lerp(heartbeatSource.volume, targetVolume, Time.deltaTime * 3f);
    }

    public void PlayStinger()
    {
        if (scaryStingers == null || scaryStingers.Length == 0) return;
        effectsSource.PlayOneShot(scaryStingers[Random.Range(0, scaryStingers.Length)], stingerVolume);
    }

    public void TriggerLightning()
    {
        StartCoroutine(DoLightning());
    }
}
