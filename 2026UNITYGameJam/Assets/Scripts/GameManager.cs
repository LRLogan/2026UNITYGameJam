using System;
using UnityEngine;

/// <summary>
/// Center for game flow and global /persistng data
/// </summary>
public class GameManager : MonoBehaviour
{
    // Revolver / lucky number mechanic related data
    [SerializeField] private Revolver revolver;
    [SerializeField, Min(0)] private int jackpotHitBonus = 100;
    [SerializeField, Min(0)] private int jackpotMissPenalty = 50;

    public bool IsRoundActive { get; private set; }
    public int LuckyNumber { get; private set; }

    // Holds how many points the player gets at end of round from jackpot shots
    public int PendingJackpotPoints { get; private set; }
    public int LastRoundJackpotPoints { get; private set; }
    public int TotalScore { get; private set; }

    // Events 
    public event Action RoundStarted;
    public event Action RoundEnded;
    public event Action ScoreChanged;

    void Start()
    {
        StartRound(4);
    }

    public void StartRound(int luckyNumber)
    {
        // Ensure the player is all set up
        if (IsRoundActive || revolver == null || luckyNumber < 1 || luckyNumber > RevolverCylinder.ChamberCount)
            return;

        LuckyNumber = luckyNumber;
        PendingJackpotPoints = 0;
        IsRoundActive = true;
        revolver.BeginRound(this);
        RoundStarted?.Invoke();
    }

    /// <summary>
    /// Reset all needed states to close out a run
    /// </summary>
    public void EndRound()
    {
        if (!IsRoundActive) return;

        IsRoundActive = false;
        LastRoundJackpotPoints = PendingJackpotPoints;
        TotalScore += LastRoundJackpotPoints;
        PendingJackpotPoints = 0;
        if (revolver != null) revolver.EndRound();
        ScoreChanged?.Invoke();
        RoundEnded?.Invoke();
    }

    /// <summary>
    /// Helper to add jacpot points to the global counter
    /// </summary>
    /// <param name="hit"></param>
    internal void RecordJackpotShot(bool hit)
    {
        if (!IsRoundActive) return;

        PendingJackpotPoints += hit ? jackpotHitBonus : -jackpotMissPenalty;
        ScoreChanged?.Invoke();
    }
}
