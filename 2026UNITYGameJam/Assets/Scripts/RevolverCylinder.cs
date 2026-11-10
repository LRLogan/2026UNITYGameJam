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
    private int luckyShotNumber;
    private int jackpotShotNumber;

    internal int AmmoRemaining { get; private set; }
    internal int NextShotNumber => AmmoRemaining == 0 ? 0 : ChamberCount - AmmoRemaining + 1;
    internal bool NextShotIsJackpot => AmmoRemaining > 0 &&
        NextShotNumber == luckyShotNumber && NextShotNumber == jackpotShotNumber;

    internal RevolverCylinder(Random random)
    {
        this.random = random;
    }

    internal void Reload(int luckyNumber, float luckyChamberWeight)
    {
        AmmoRemaining = ChamberCount;
        luckyShotNumber = luckyNumber;
        jackpotShotNumber = RollJackpotPosition(luckyChamberWeight);
    }

    internal void Spin(float luckyChamberWeight)
    {
        // Only the jackpot's position can change because the other rounds are identical.
        if (AmmoRemaining > 1 && jackpotShotNumber != 0)
            jackpotShotNumber = RollJackpotPosition(luckyChamberWeight);
    }

    private int RollJackpotPosition(float luckyChamberWeight)
    {
        int firstShot = NextShotNumber;
        if (luckyShotNumber < firstShot)
            return random.Next(firstShot, ChamberCount + 1);

        // Weight the lucky chamber among the unfired shots.
        double roll = random.NextDouble() * (AmmoRemaining - 1 + (double)luckyChamberWeight);
        if (roll < luckyChamberWeight) return luckyShotNumber;

        int otherShot = firstShot + (int)(roll - luckyChamberWeight);
        return otherShot < luckyShotNumber ? otherShot : otherShot + 1;
    }

    internal bool TryFire(out BulletType bullet)
    {
        bullet = BulletType.Standard;
        if (AmmoRemaining == 0) return false;

        if (NextShotIsJackpot) bullet = BulletType.Jackpot;

        // Firing the jackpot round outside the lucky chamber consumes it as a standard shot.
        if (NextShotNumber == jackpotShotNumber) jackpotShotNumber = 0;

        AmmoRemaining--;
        return true;
    }

    internal void Clear()
    {
        AmmoRemaining = 0;
        luckyShotNumber = 0;
        jackpotShotNumber = 0;
    }
}
