using UnityEngine;
using Photon.Pun;
using System;

public class ZombieHealth : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Effects")]
    public GameObject deathEffect;
    public AudioClip hurtSound;
    public AudioClip deathSound;

    [Header("Drops")]
    public GameObject[] possibleDrops;
    public float dropChance = 0.2f;

    // State
    public bool IsDead { get; private set; }

    // Events
    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;
    public static event Action OnAnyZombieDeath;
    public event Action<int> OnDamaged; // Passes attacker ID

    private AudioSource audioSource;
    private Animator animator;
    private ZombieAI zombieAI;
    private ZombieRagdoll ragdoll;

    // For ragdoll force
    private Vector3 lastHitDirection;
    private Vector3 lastHitPoint;

    void Start()
    {
        currentHealth = maxHealth;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        animator = GetComponentInChildren<Animator>();
        zombieAI = GetComponent<ZombieAI>();
        ragdoll = GetComponent<ZombieRagdoll>();
    }

    [PunRPC]
    public void TakeDamage(float damage, int attackerViewID)
    {
        #if UNITY_EDITOR
        Debug.Log($"[ZombieHealth] TakeDamage RPC received: {damage} dmg, health before: {currentHealth}, IsDead: {IsDead}");
        #endif
        if (IsDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamaged?.Invoke(attackerViewID);

        // Alert the zombie to the attacker
        if (zombieAI != null && attackerViewID != -1)
        {
            PhotonView attackerView = PhotonView.Find(attackerViewID);
            if (attackerView != null)
            {
                zombieAI.SetTarget(attackerView.transform);
            }
        }

        // Play hurt sound
        if (zombieAI != null)
        {
            zombieAI.PlayHurtSound();
        }
        else if (hurtSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hurtSound);
        }

        #if UNITY_EDITOR
        Debug.Log($"[ZombieHealth] Zombie took {damage} damage, health: {currentHealth}");
        #endif

        if (currentHealth <= 0)
        {
            Die(attackerViewID);
        }
    }

    public void Damage(float damage, int attackerViewID = -1)
    {
        #if UNITY_EDITOR
        Debug.Log($"[ZombieHealth] Damage called: {damage} dmg, attacker: {attackerViewID}, IsMine: {photonView != null && photonView.IsMine}, Connected: {PhotonNetwork.IsConnected}");
        #endif

        if (!PhotonNetwork.IsConnected)
        {
            // Offline mode - process locally
            #if UNITY_EDITOR
            Debug.Log("[ZombieHealth] Offline mode - processing locally");
            #endif
            TakeDamage(damage, attackerViewID);
        }
        else if (photonView == null || photonView.IsMine)
        {
            // I own this zombie (master client) - process locally
            #if UNITY_EDITOR
            Debug.Log("[ZombieHealth] I own zombie - processing locally");
            #endif
            TakeDamage(damage, attackerViewID);
        }
        else
        {
            // I don't own this zombie - send damage to owner (master client)
            #if UNITY_EDITOR
            Debug.Log("[ZombieHealth] Sending RPC to MasterClient");
            #endif
            photonView.RPC("TakeDamage", RpcTarget.MasterClient, damage, attackerViewID);
        }
    }

    // Simple damage method for offline/local use
    public void TakeDamageLocal(float damage, Vector3 hitDirection = default, Vector3 hitPoint = default)
    {
        if (IsDead) return;

        lastHitDirection = hitDirection;
        lastHitPoint = hitPoint;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Play hurt sound
        if (zombieAI != null)
        {
            zombieAI.PlayHurtSound();
        }
        else if (hurtSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hurtSound);
        }

        #if UNITY_EDITOR
        Debug.Log($"[ZombieHealth] Zombie took {damage} damage, health: {currentHealth}");
        #endif

        if (currentHealth <= 0)
        {
            DieLocal();
        }
    }

    void DieLocal()
    {
        if (IsDead) return;

        IsDead = true;
        currentHealth = 0;

        // Play death sound
        if (zombieAI != null)
        {
            zombieAI.PlayDeathSound();
        }
        else if (deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        // Enable ragdoll instead of death animation
        if (ragdoll != null)
        {
            Vector3 force = lastHitDirection * 50f;
            ragdoll.EnableRagdollWithForce(force, lastHitPoint);
        }
        else // Enable ragdoll instead of death animation
        if (ragdoll != null)
        {
            ragdoll.EnableRagdoll();
        }
        else if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
        }

        if (zombieAI != null)
        {
            zombieAI.enabled = false;
        }

        // Disable main collider
        CapsuleCollider mainCol = GetComponent<CapsuleCollider>();
        if (mainCol != null)
        {
            mainCol.enabled = false;
        }

        // Disable NavMeshAgent
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }

        OnDeath?.Invoke();
        OnAnyZombieDeath?.Invoke();

        #if UNITY_EDITOR
        Debug.Log("[ZombieHealth] Zombie died!");
        #endif

        Destroy(gameObject, 5f);
    }

    void Die(int killerViewID)
    {
        if (IsDead) return;

        IsDead = true;
        currentHealth = 0;

        // Play death sound
        if (zombieAI != null)
        {
            zombieAI.PlayDeathSound();
        }
        else if (deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        // Enable ragdoll instead of death animation
        if (ragdoll != null)
        {
            ragdoll.EnableRagdoll();
        }
        else if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
        }

        // Disable AI
        if (zombieAI != null)
        {
            zombieAI.enabled = false;
        }

        // Disable collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        OnDeath?.Invoke();
        OnAnyZombieDeath?.Invoke();

        // Award points to killer
        if (killerViewID != -1)
        {
            PhotonView killerView = PhotonView.Find(killerViewID);
            if (killerView != null)
            {
                PlayerScore score = killerView.GetComponent<PlayerScore>();
                if (score != null)
                {
                    score.AddKill(100); // 100 points per kill
                }
            }
        }

        #if UNITY_EDITOR
        Debug.Log($"[ZombieHealth] Zombie died! Killer: {killerViewID}");
        #endif

        // Spawn drops
        if (photonView != null && photonView.IsMine && possibleDrops.Length > 0 && UnityEngine.Random.value < dropChance * (GameManager.Instance != null ? GameManager.Instance.GetItemDropMultiplier() : 1f))
        {
            int dropIndex = UnityEngine.Random.Range(0, possibleDrops.Length);
            PhotonNetwork.Instantiate("Drops/" + possibleDrops[dropIndex].name,
                transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }

        // Destroy after animation
        if (photonView != null && photonView.IsMine)
        {
            StartCoroutine(DestroyAfterDelay(3f));
        }
    }

    System.Collections.IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        PhotonNetwork.Destroy(gameObject);
    }

    // Called on remote clients when zombie dies via network sync
    void OnRemoteDeath()
    {
        #if UNITY_EDITOR
        Debug.Log("[ZombieHealth] Remote death triggered");
        #endif

        // Play death sound
        if (zombieAI != null)
        {
            zombieAI.PlayDeathSound();
        }
        else if (deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        // Play death animation
        // Enable ragdoll instead of death animation
        if (ragdoll != null)
        {
            ragdoll.EnableRagdoll();
        }
        else if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        // Disable AI and collider
        if (zombieAI != null) zombieAI.enabled = false;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(currentHealth);
            stream.SendNext(IsDead);
        }
        else
        {
            currentHealth = (float)stream.ReceiveNext();
            bool wasDead = IsDead;
            IsDead = (bool)stream.ReceiveNext();

            // Trigger death effects on remote clients when zombie dies
            if (IsDead && !wasDead)
            {
                OnRemoteDeath();
            }
        }
    }

    /// <summary>
    /// Reset health for object pooling.
    /// </summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        IsDead = false;

        // Re-enable components
        if (zombieAI != null) zombieAI.enabled = true;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        CapsuleCollider mainCol = GetComponent<CapsuleCollider>();
        if (mainCol != null) mainCol.enabled = true;

        // Reset ragdoll if present
        if (ragdoll != null)
        {
            ragdoll.SetRagdollEnabled(false);
        }
    }
}
