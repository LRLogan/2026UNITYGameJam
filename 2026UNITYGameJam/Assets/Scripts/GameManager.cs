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
    public RoundResult LastResult { get; private set; }

    // Events 
    public event Action RoundStarted;
    public event Action RoundEnded;
    public event Action ScoreChanged;

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
        FinishRound(null);
    }

    internal void CompleteRound(double elapsedSeconds, PlayerCore player, bool extracted = true)
    {
        FinishRound(new RoundResult(PendingJackpotPoints, elapsedSeconds, player.Health, player.MaxHealth, extracted));
    }

    private void FinishRound(RoundResult? result)
    {
        if (!IsRoundActive) return;

        IsRoundActive = false;
        if (result.HasValue) LastResult = result.Value;
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

public readonly struct RoundResult
{
    public readonly int Score;
    public readonly double ElapsedSeconds;
    public readonly int Health;
    public readonly int MaxHealth;
    public readonly bool Extracted;

    public RoundResult(int score, double elapsedSeconds, int health, int maxHealth, bool extracted = true)
    {
        Score = score;
        ElapsedSeconds = elapsedSeconds;
        Health = health;
        MaxHealth = maxHealth;
        Extracted = extracted;
    }
}
