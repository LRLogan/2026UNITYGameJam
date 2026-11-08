using System;

/// <summary>
/// Overarching bullet type
/// </summary>
public enum BulletType
{
    Standard,
    Jackpot
}

/// <summary>
/// Nitty gritty logic for the revolver that handles randomizing bullet order for reloading, spinning, and ammo tracking
/// </summary>
internal sealed class RevolverCylinder
{
    internal const int ChamberCount = 6;

    private readonly Random random;
    private int jackpotShotNumber;

    internal int AmmoRemaining { get; private set; }
    internal int NextShotNumber => AmmoRemaining == 0 ? 0 : ChamberCount - AmmoRemaining + 1;
    internal bool NextShotIsJackpot => AmmoRemaining > 0 && NextShotNumber == jackpotShotNumber;

    internal RevolverCylinder(Random random)
    {
        this.random = random;
    }

    internal void Reload(int luckyNumber, float luckyChamberWeight)
    {
        AmmoRemaining = ChamberCount;

        // Give the lucky chamber extra weight while keeping the other five equally likely.
        double roll = random.NextDouble() * (ChamberCount - 1 + (double)luckyChamberWeight);
        if (roll < luckyChamberWeight)
        {
            jackpotShotNumber = luckyNumber;
            return;
        }

        int otherChamber = 1 + (int)(roll - luckyChamberWeight);
        jackpotShotNumber = otherChamber < luckyNumber ? otherChamber : otherChamber + 1;
    }

    internal void Spin()
    {
        // Only the jackpot's position can change because the other rounds are identical.
        if (AmmoRemaining > 1 && jackpotShotNumber != 0)
            jackpotShotNumber = random.Next(NextShotNumber, ChamberCount + 1);
    }

    internal bool TryFire(out BulletType bullet)
    {
        bullet = BulletType.Standard;
        if (AmmoRemaining == 0) return false;

        if (NextShotIsJackpot)
        {
            bullet = BulletType.Jackpot;
            jackpotShotNumber = 0;
        }

        AmmoRemaining--;
        return true;
    }

    internal void Clear()
    {
        AmmoRemaining = 0;
        jackpotShotNumber = 0;
    }
}
