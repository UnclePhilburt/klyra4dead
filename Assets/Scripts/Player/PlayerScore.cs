using UnityEngine;
using Photon.Pun;
using System;

public class PlayerScore : MonoBehaviourPunCallbacks, IPunObservable
{
    public int Score { get; private set; }
    public int Kills { get; private set; }
    public int Revives { get; private set; }
    public int Deaths { get; private set; }

    public event Action<int> OnScoreChanged;
    public event Action<int> OnKillsChanged;

    void Start()
    {
        // Subscribe to death events
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.OnDeath += OnPlayerDeath;
            health.OnRevived += OnPlayerRevived;
        }
    }

    void OnDestroy()
    {
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.OnDeath -= OnPlayerDeath;
            health.OnRevived -= OnPlayerRevived;
        }
    }

    public void AddKill(int points)
    {
        if (!photonView.IsMine) return;

        Kills++;
        Score += points;

        OnKillsChanged?.Invoke(Kills);
        OnScoreChanged?.Invoke(Score);

        Debug.Log($"[PlayerScore] {photonView.Owner.NickName} - Kill! Score: {Score}, Kills: {Kills}");
    }

    public void AddRevive(int points = 50)
    {
        if (!photonView.IsMine) return;

        Revives++;
        Score += points;

        OnScoreChanged?.Invoke(Score);
        Debug.Log($"[PlayerScore] {photonView.Owner.NickName} - Revive! Score: {Score}");
    }

    public void AddPoints(int points)
    {
        if (!photonView.IsMine) return;

        Score += points;
        OnScoreChanged?.Invoke(Score);
    }

    void OnPlayerDeath()
    {
        Deaths++;
    }

    void OnPlayerRevived()
    {
        // Could give bonus points to the reviver here
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(Score);
            stream.SendNext(Kills);
            stream.SendNext(Revives);
            stream.SendNext(Deaths);
        }
        else
        {
            Score = (int)stream.ReceiveNext();
            Kills = (int)stream.ReceiveNext();
            Revives = (int)stream.ReceiveNext();
            Deaths = (int)stream.ReceiveNext();
        }
    }
}
