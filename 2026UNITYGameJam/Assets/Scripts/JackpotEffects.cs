using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Jackpot bullets can heve these effects
/// </summary>
public enum JackpotEffect
{
    Explosion,
    ChainLightning
}

/// <summary>
/// Jackpot bullet effect logic
/// </summary>
[Serializable]
public sealed class JackpotEffects
{
    [SerializeField] private LayerMask enemyLayer = Physics.DefaultRaycastLayers;

    [Header("Explosion")]
    [SerializeField, Min(0f)] private float explosionRadius = 4f;
    [SerializeField, Min(0)] private int explosionDamage = 50;
    [SerializeField, Min(0)] private int explosionSelfDamage = 30;

    [Header("Chain lightning")]
    [SerializeField, Min(0f)] private float lightningRadius = 5f;
    [SerializeField, Min(0)] private int lightningDamage = 35;
    [SerializeField, Min(0)] private int lightningSelfDamage = 20;
    [SerializeField, Min(0)] private int lightningMaxHops = 3;

    public float ExplosionRadius => explosionRadius;

    /// <summary>
    /// Randomizer to choose and delegate the correct effect logic
    /// </summary>
    /// <param name="player"></param>
    /// <param name="strikePoints"></param>
    /// <returns></returns>
    internal JackpotEffect Apply(PlayerCore player, List<Vector3> strikePoints)
    {
        if (UnityEngine.Random.Range(0, 2) == 0)
        {
            Explode(player, strikePoints[0]);
            return JackpotEffect.Explosion;
        }

        ChainLightning(player, strikePoints);
        return JackpotEffect.ChainLightning;
    }

    /// <summary>
    /// Exploding bullet
    /// </summary>
    /// <param name="player"></param>
    /// <param name="impactPoint"></param>
    private void Explode(PlayerCore player, Vector3 impactPoint)
    {
        HashSet<Enemy> affected = new HashSet<Enemy>();
        Collider[] colliders = Physics.OverlapSphere(impactPoint, explosionRadius, enemyLayer,
            QueryTriggerInteraction.Ignore);
        foreach (Collider collider in colliders)
        {
            Enemy enemy = collider.GetComponentInParent<Enemy>();
            if (enemy != null && enemy.IsAlive && affected.Add(enemy))
                enemy.TakeDamage(explosionDamage);
        }

        if (PlayerIsInRange(player, impactPoint, explosionRadius))
            player.TakeDamage(explosionSelfDamage);
    }

    /// <summary>
    /// Loghting bullet
    /// </summary>
    /// <param name="player"></param>
    /// <param name="strikePoints"></param>
    private void ChainLightning(PlayerCore player, List<Vector3> strikePoints)
    {
        HashSet<Enemy> affected = new HashSet<Enemy>();
        Vector3 origin = strikePoints[0];
        bool playerInRange = PlayerIsInRange(player, origin, lightningRadius);

        // Each strike starts the next hop. The player takes collateral damage at most once
        for (int hop = 0; hop < lightningMaxHops; hop++)
        {
            Enemy enemy = FindNextLightningTarget(origin, affected, out Vector3 strikePoint);
            if (enemy == null) break;

            affected.Add(enemy);
            enemy.TakeDamage(lightningDamage);
            strikePoints.Add(strikePoint);
            origin = strikePoint;
            playerInRange |= PlayerIsInRange(player, origin, lightningRadius);
        }

        if (playerInRange) player.TakeDamage(lightningSelfDamage);
    }

    /// <summary>
    /// Helper to find close targets for chain lighting
    /// </summary>
    /// <param name="origin"></param>
    /// <param name="affected"></param>
    /// <param name="strikePoint"></param>
    /// <returns></returns>
    private Enemy FindNextLightningTarget(Vector3 origin, HashSet<Enemy> affected, out Vector3 strikePoint)
    {
        Collider[] colliders = Physics.OverlapSphere(origin, lightningRadius, enemyLayer,
            QueryTriggerInteraction.Ignore);

        Enemy closestEnemy = null;
        float closestDistance = float.PositiveInfinity;
        strikePoint = origin;

        // Loop through the nearby objects to extract the enemies
        foreach (Collider collider in colliders)
        {
            Enemy enemy = collider.GetComponentInParent<Enemy>();
            if (enemy == null || !enemy.IsAlive || affected.Contains(enemy)) continue;

            Vector3 point = collider.ClosestPoint(origin);
            float distance = (point - origin).sqrMagnitude;
            if (distance >= closestDistance) continue;

            closestEnemy = enemy;
            closestDistance = distance;
            strikePoint = point;
        }

        return closestEnemy;
    }

    /// <summary>
    /// imple check to see if the player is in range of an effect
    /// </summary>
    /// <param name="player"></param>
    /// <param name="origin"></param>
    /// <param name="radius"></param>
    /// <returns></returns>
    private static bool PlayerIsInRange(PlayerCore player, Vector3 origin, float radius)
    {
        return (player.transform.position - origin).sqrMagnitude <= radius * radius;
    }
}
