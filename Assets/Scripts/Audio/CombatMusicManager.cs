using UnityEngine;

public class CombatMusicManager : MonoBehaviour
{
    [Header("Music Tracks")]
    public AudioClip[] combatTracks;

    [Header("Settings")]
    [Range(0f, 1f)]
    public float musicVolume = 0.5f;
    public float fadeInSpeed = 1f;
    public float fadeOutSpeed = 2f;
    public float combatCooldown = 10f; // How long after last aggro before music stops

    private AudioSource audioSource;
    private bool inCombat;
    private float lastCombatTime;
    private float targetVolume;
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
        // Check if we should still be in combat
        if (inCombat && Time.time - lastCombatTime > combatCooldown)
        {
            EndCombat();
        }

        // Fade volume
        if (audioSource.volume < targetVolume)
        {
            audioSource.volume = Mathf.MoveTowards(audioSource.volume, targetVolume, fadeInSpeed * Time.deltaTime);
        }
        else if (audioSource.volume > targetVolume)
        {
            audioSource.volume = Mathf.MoveTowards(audioSource.volume, targetVolume, fadeOutSpeed * Time.deltaTime);

            // Stop playing when faded out
            if (audioSource.volume <= 0.01f && targetVolume == 0f)
            {
                audioSource.Stop();
            }
        }
    }

    // Call this when zombies get aggroed
    public void TriggerCombat()
    {
        lastCombatTime = Time.time;

        if (!inCombat)
        {
            StartCombat();
        }
    }

    void StartCombat()
    {
        if (combatTracks == null || combatTracks.Length == 0) return;

        inCombat = true;
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

        Debug.Log($"[CombatMusic] Combat started! Playing: {combatTracks[trackIndex].name}");
    }

    void EndCombat()
    {
        inCombat = false;
        targetVolume = 0f;

        Debug.Log("[CombatMusic] Combat ended, fading out...");
    }

    // Force stop immediately
    public void StopMusic()
    {
        inCombat = false;
        targetVolume = 0f;
        audioSource.volume = 0f;
        audioSource.Stop();
    }
}
