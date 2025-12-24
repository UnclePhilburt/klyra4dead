using UnityEngine;

public class CombatMusicManager : MonoBehaviour
{
    [Header("Music Tracks")]
    public AudioClip[] combatTracks;

    [Header("Volume Settings")]
    [Range(0f, 1f)]
    public float musicVolume = 0.5f;
    
    [Header("Fade Settings")]
    [Tooltip("Seconds to fade in")]
    public float fadeInDuration = 2f;
    [Tooltip("Seconds to fade out (smooth exit)")]
    public float fadeOutDuration = 6f;
    
    [Header("Combat Timing")]
    [Tooltip("How long after ALL zombies stop chasing before fade starts")]
    public float combatEndDelay = 8f;
    [Tooltip("Minimum time combat music plays before it can stop")]
    public float minimumCombatDuration = 15f;

    private AudioSource audioSource;
    private bool inCombat;
    private float combatStartTime;
    private float lastZombieChasingTime;
    private float targetVolume;
    private float currentFadeSpeed;
    private int lastTrackIndex = -1;

    public static CombatMusicManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.volume = 0f;
        audioSource.playOnAwake = false;
    }

    void Update()
    {
        // Check if any zombies are still chasing
        int chasingCount = ZombieAI.GetChasingCount();
        
        if (chasingCount > 0)
        {
            lastZombieChasingTime = Time.time;
            
            if (!inCombat)
            {
                StartCombat();
            }
        }
        
        // Check if combat should end
        if (inCombat)
        {
            float timeSinceCombatStart = Time.time - combatStartTime;
            float timeSinceLastChase = Time.time - lastZombieChasingTime;
            
            if (timeSinceCombatStart > minimumCombatDuration && 
                timeSinceLastChase > combatEndDelay)
            {
                EndCombat();
            }
        }

        // Smooth volume fade using Lerp for extra smoothness
        if (Mathf.Abs(audioSource.volume - targetVolume) > 0.001f)
        {
            // Use different speeds for fade in vs fade out
            float fadeDuration = targetVolume > audioSource.volume ? fadeInDuration : fadeOutDuration;
            float fadeSpeed = musicVolume / fadeDuration;
            
            // Smooth step for extra smoothness on fade out
            if (targetVolume < audioSource.volume)
            {
                // Exponential fade out (sounds more natural)
                audioSource.volume = Mathf.Lerp(audioSource.volume, targetVolume, Time.deltaTime / (fadeOutDuration * 0.3f));
            }
            else
            {
                // Linear fade in
                audioSource.volume = Mathf.MoveTowards(audioSource.volume, targetVolume, fadeSpeed * Time.deltaTime);
            }
        }
        
        // Stop playing when fully faded out
        if (audioSource.volume <= 0.005f && targetVolume == 0f && audioSource.isPlaying)
        {
            audioSource.volume = 0f;
            audioSource.Stop();
            Debug.Log("[CombatMusic] Fully faded out, stopped.");
        }
    }

    public void TriggerCombat()
    {
        lastZombieChasingTime = Time.time;
        
        if (!inCombat)
        {
            StartCombat();
        }
    }

    void StartCombat()
    {
        if (combatTracks == null || combatTracks.Length == 0) return;

        inCombat = true;
        combatStartTime = Time.time;
        lastZombieChasingTime = Time.time;
        targetVolume = musicVolume;

        // Pick a random track (different from last one if possible)
        int trackIndex = Random.Range(0, combatTracks.Length);
        if (combatTracks.Length > 1 && trackIndex == lastTrackIndex)
        {
            trackIndex = (trackIndex + 1) % combatTracks.Length;
        }
        lastTrackIndex = trackIndex;

        audioSource.clip = combatTracks[trackIndex];
        audioSource.Play();

        Debug.Log($"[CombatMusic] Combat started!");
    }

    void EndCombat()
    {
        inCombat = false;
        targetVolume = 0f;

        Debug.Log("[CombatMusic] Combat ended, beginning smooth fade out...");
    }

    public void StopMusic()
    {
        inCombat = false;
        targetVolume = 0f;
        audioSource.volume = 0f;
        audioSource.Stop();
    }
}
