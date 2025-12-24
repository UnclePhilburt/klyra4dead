using UnityEngine;
using Photon.Pun;
using System;

public class PlayerHealth : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Downed State")]
    public float downedHealth = 50f;
    public float bleedOutTime = 30f;
    public float reviveTime = 3f;

    [Header("Effects")]
    public GameObject bloodEffect;
    public AudioClip hurtSound;
    public AudioClip deathSound;
    public AudioClip reviveSound;

    // State
    public bool IsDead { get; private set; }
    public float HealthPercent => maxHealth > 0 ? currentHealth / maxHealth : 0f;
    public bool IsDowned { get; private set; }
    public bool IsBeingRevived { get; private set; }
    public float ReviveProgress { get; private set; }

    // Events
    public event Action<float, float> OnHealthChanged;
    public event Action OnDowned;
    public event Action OnRevived;
    public event Action OnDeath;

    private float bleedOutTimer;
    private PlayerHealth reviver;
    private AudioSource audioSource;
    private Animator animator;

    // Check if this is the local player
    private bool IsLocalPlayer => !PhotonNetwork.IsConnected || photonView == null || photonView.IsMine;

    void Start()
    {
        currentHealth = maxHealth;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        animator = GetComponentInChildren<Animator>();

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void Update()
    {
        if (!IsLocalPlayer) return;

        // Bleed out when downed
        if (IsDowned && !IsBeingRevived)
        {
            bleedOutTimer -= Time.deltaTime;
            if (bleedOutTimer <= 0)
            {
                Die();
            }
        }
    }

    [PunRPC]
    public void TakeDamage(float damage, int attackerViewID)
    {
        if (IsDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Play hurt effects
        if (hurtSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hurtSound);
        }

        if (bloodEffect != null)
        {
            Instantiate(bloodEffect, transform.position + Vector3.up, Quaternion.identity);
        }

        // Check for down/death
        if (currentHealth <= 0)
        {
            if (!IsDowned)
            {
                GoDown();
            }
            else
            {
                Die();
            }
        }

        string playerName = PhotonNetwork.IsConnected ? photonView.Owner.NickName : "Player";
        #if UNITY_EDITOR
        Debug.Log($"[PlayerHealth] {playerName} took {damage} damage, health: {currentHealth}");
        #endif
    }

    public void Damage(float damage, int attackerViewID = -1)
    {
        if (!PhotonNetwork.IsConnected || photonView == null || photonView.IsMine)
        {
            TakeDamage(damage, attackerViewID);
        }
        else
        {
            photonView.RPC("TakeDamage", RpcTarget.All, damage, attackerViewID);
        }
    }

    void GoDown()
    {
        if (IsDowned || IsDead) return;

        IsDowned = true;
        bleedOutTimer = bleedOutTime;
        currentHealth = downedHealth;

        if (animator != null)
        {
            animator.SetBool("IsDowned", true);
        }

        OnDowned?.Invoke();
        string playerName = PhotonNetwork.IsConnected ? photonView.Owner.NickName : "Player";
        #if UNITY_EDITOR
        Debug.Log($"[PlayerHealth] {playerName} is downed!");
        #endif

        // Notify all clients
        if (PhotonNetwork.IsConnected && photonView.IsMine)
        {
            photonView.RPC("RPC_SetDowned", RpcTarget.Others, true);
        }
    }

    [PunRPC]
    void RPC_SetDowned(bool downed)
    {
        IsDowned = downed;
        if (animator != null)
        {
            animator.SetBool("IsDowned", downed);
        }
    }

    public void StartRevive(PlayerHealth reviverPlayer)
    {
        if (!IsDowned || IsDead || IsBeingRevived) return;

        IsBeingRevived = true;
        reviver = reviverPlayer;
        ReviveProgress = 0f;

        string reviverName = PhotonNetwork.IsConnected && reviverPlayer.photonView != null ? reviverPlayer.photonView.Owner.NickName : "Player";
        string playerName = PhotonNetwork.IsConnected ? photonView.Owner.NickName : "Player";
        #if UNITY_EDITOR
        Debug.Log($"[PlayerHealth] {reviverName} is reviving {playerName}");
        #endif
    }

    public void UpdateRevive(float deltaTime)
    {
        if (!IsBeingRevived) return;

        ReviveProgress += deltaTime / reviveTime;

        if (ReviveProgress >= 1f)
        {
            CompleteRevive();
        }
    }

    public void CancelRevive()
    {
        IsBeingRevived = false;
        ReviveProgress = 0f;
        reviver = null;
    }

    void CompleteRevive()
    {
        IsDowned = false;
        IsBeingRevived = false;
        ReviveProgress = 0f;
        currentHealth = maxHealth * 0.3f; // Revive with 30% health

        if (animator != null)
        {
            animator.SetBool("IsDowned", false);
        }

        if (reviveSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(reviveSound);
        }

        OnRevived?.Invoke();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        string playerName = PhotonNetwork.IsConnected ? photonView.Owner.NickName : "Player";
        #if UNITY_EDITOR
        Debug.Log($"[PlayerHealth] {playerName} was revived!");
        #endif

        // Notify all clients
        if (PhotonNetwork.IsConnected && photonView.IsMine)
        {
            photonView.RPC("RPC_SetDowned", RpcTarget.Others, false);
        }

        reviver = null;
    }

    void Die()
    {
        if (IsDead) return;

        IsDead = true;
        IsDowned = false;
        currentHealth = 0;

        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        if (deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        OnDeath?.Invoke();
        string playerName = PhotonNetwork.IsConnected ? photonView.Owner.NickName : "Player";
        #if UNITY_EDITOR
        Debug.Log($"[PlayerHealth] {playerName} died!");
        #endif

        // Disable player control
        var controller = GetComponent<ThirdPersonController>();
        if (controller != null)
        {
            controller.enabled = false;
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || IsDowned) return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        string playerName = PhotonNetwork.IsConnected ? photonView.Owner.NickName : "Player";
        #if UNITY_EDITOR
        Debug.Log($"[PlayerHealth] {playerName} healed {amount}, health: {currentHealth}");
        #endif
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(currentHealth);
            stream.SendNext(IsDowned);
            stream.SendNext(IsDead);
        }
        else
        {
            currentHealth = (float)stream.ReceiveNext();
            IsDowned = (bool)stream.ReceiveNext();
            IsDead = (bool)stream.ReceiveNext();
        }
    }
}
