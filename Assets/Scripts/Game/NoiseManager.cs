using UnityEngine;
using System.Collections.Generic;

public class NoiseManager : MonoBehaviour
{
    public static NoiseManager Instance { get; private set; }

    [Header("Noise Ranges")]
    public float gunshotRange = 60f;
    public float sprintRange = 20f;
    public float runRange = 12f;
    public float walkRange = 5f;
    public float crouchRange = 2f;
    public float meleeRange = 8f;
    public float explosionRange = 80f;

    public enum NoiseType
    {
        Gunshot, Explosion, Sprint, Run, Walk, Crouch, Melee, Reload, Pain, ZombieDeath
    }

    private List<NoiseEvent> recentNoises = new List<NoiseEvent>();

    struct NoiseEvent
    {
        public Vector3 position;
        public float radius;
        public float time;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        recentNoises.RemoveAll(n => Time.time - n.time > 2f);
    }

    public void MakeNoise(Vector3 position, NoiseType type, Transform source = null)
    {
        float range = GetNoiseRange(type);
        if (range <= 0) return;

        recentNoises.Add(new NoiseEvent { position = position, radius = range, time = Time.time });
        AlertZombiesInRange(position, range, source);
    }

    public void MakeNoise(Vector3 position, float customRange, Transform source = null)
    {
        if (customRange <= 0) return;
        recentNoises.Add(new NoiseEvent { position = position, radius = customRange, time = Time.time });
        AlertZombiesInRange(position, customRange, source);
    }

    float GetNoiseRange(NoiseType type)
    {
        return type switch
        {
            NoiseType.Gunshot => gunshotRange,
            NoiseType.Explosion => explosionRange,
            NoiseType.Sprint => sprintRange,
            NoiseType.Run => runRange,
            NoiseType.Walk => walkRange,
            NoiseType.Crouch => crouchRange,
            NoiseType.Melee => meleeRange,
            NoiseType.Reload => 5f,
            NoiseType.Pain => 15f,
            NoiseType.ZombieDeath => 10f,
            _ => 10f
        };
    }

    void AlertZombiesInRange(Vector3 position, float range, Transform source)
    {
        // Use cached zombie list from ZombieManager instead of expensive FindObjectsByType
        if (ZombieManager.Instance == null) return;

        float rangeSqr = range * range;
        var zombies = ZombieManager.Instance.GetAllZombies();

        for (int i = 0; i < zombies.Count; i++)
        {
            var zombie = zombies[i];
            if (zombie == null || !zombie.enabled) continue;

            // Use squared distance to avoid expensive sqrt
            float distSqr = WebGLOptimizer.SqrDistance(zombie.transform.position, position);
            if (distSqr <= rangeSqr)
            {
                float distance = Mathf.Sqrt(distSqr); // Only sqrt when needed
                float hearingChance = 1f - (distance / range) * 0.5f;
                if (Random.value < hearingChance)
                    zombie.HearNoise(position, source);
            }
        }
    }
}
