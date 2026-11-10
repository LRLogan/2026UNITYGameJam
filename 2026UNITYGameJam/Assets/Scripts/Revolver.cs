using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Actual Revolver weapon responcible for overarching events like shooting the shot
/// and user input endpoints
/// </summary>
public class Revolver : MonoBehaviour
{
    [SerializeField] private PlayerCore player;

    [Header("Shooting")]
    [SerializeField] private Transform bulletOrigin;
    [SerializeField, Min(0.01f)] private float bulletRad = 0.5f;
    [SerializeField, Min(1f)] private float shotRange = 100f;
    [SerializeField, Min(1)] private int standardDamage = 20;
    [SerializeField] private LayerMask detectionLayers = Physics.DefaultRaycastLayers;

    [Header("Jackpot")]
    [SerializeField, Range(1f, 2f)] private float luckyChamberWeight = 1.25f;
    [SerializeField, Min(0)] private int jackpotHeal = 25;
    [SerializeField] private JackpotEffects jackpotEffects = new JackpotEffects();

    private readonly RevolverCylinder cylinder = new RevolverCylinder(new System.Random());
    private GameManager gameManager;

    public int AmmoRemaining => cylinder.AmmoRemaining;
    public int NextShotNumber => cylinder.NextShotNumber;
    public bool NextShotIsJackpot => cylinder.NextShotIsJackpot;

    // Action to alert the UI when it needs to change weapon data
    public event Action CylinderChanged;
    public event Action<bool> JackpotShotResolved;

    // The first point is the impact. Any remaining points are the lightning hops in order.
    public event Action<JackpotEffect, IReadOnlyList<Vector3>> JackpotEffectResolved;

    private bool CanUseWeapon => isActiveAndEnabled && gameManager != null && gameManager.IsRoundActive;

    private void Awake()
    {
        if (player == null) player = GetComponentInParent<PlayerCore>();
    }

    internal void BeginRound(GameManager manager)
    {
        gameManager = manager;
        LoadCylinder();
    }

    internal void EndRound()
    {
        gameManager = null;
        cylinder.Clear();
        CylinderChanged?.Invoke();
    }

    public void Reload()
    {
        if (CanUseWeapon) LoadCylinder();
    }

    /// <summary>
    /// Helper for initital ammo loading
    /// </summary>
    private void LoadCylinder()
    {
        cylinder.Reload(gameManager.LuckyNumber, luckyChamberWeight);
        CylinderChanged?.Invoke();
    }

    public void SpinCylinder()
    {
        if (!CanUseWeapon || AmmoRemaining == 0) return;

        cylinder.Spin();
        CylinderChanged?.Invoke();
    }

    public void Shoot()
    {
        if (!CanUseWeapon || player == null || !cylinder.TryFire(out BulletType bullet)) return;

        Enemy target = FindShotTarget(out Vector3 hitPoint);
        if (bullet == BulletType.Jackpot)
            ResolveJackpotShot(target, hitPoint);
        else if (target != null)
            target.TakeDamage(standardDamage);

        CylinderChanged?.Invoke();
    }

    /// <summary>
    /// Since the spherecast goes for the full length this finds the first real target hit
    /// </summary>
    /// <param name="hitPoint"></param>
    /// <returns></returns>
    private Enemy FindShotTarget(out Vector3 hitPoint)
    {
        Transform aim = player.AimTransform;
        Vector3 origin = bulletOrigin != null ? bulletOrigin.position : aim.position;
        RaycastHit[] hits = Physics.SphereCastAll(origin, bulletRad, aim.forward, shotRange,
            detectionLayers, QueryTriggerInteraction.Ignore);

        // The closest solid hit blocks the shot. Ignore the shooter's own colliders
        float closestDistance = float.PositiveInfinity;
        Enemy target = null;
        hitPoint = origin;

        foreach (RaycastHit hit in hits)
        {
            if (hit.distance >= closestDistance || hit.collider.transform.IsChildOf(player.transform))
                continue;

            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null && !enemy.IsAlive) continue;

            closestDistance = hit.distance;
            target = enemy;
            hitPoint = hit.distance > 0f ? hit.point : origin;
        }

        // Return the target in front of the rest that is actually an enemy
        return target;
    }

    private void ResolveJackpotShot(Enemy target, Vector3 hitPoint)
    {
        // Let the game manager know a jackpot shot was discharged to add or subtract points
        bool hit = target != null;
        gameManager.RecordJackpotShot(hit);

        // If it hits
        if (hit)
        {
            player.Heal(jackpotHeal);
            target.Kill();

            List<Vector3> strikePoints = new List<Vector3> { hitPoint };
            JackpotEffect effect = jackpotEffects.Apply(player, strikePoints);
            JackpotEffectResolved?.Invoke(effect, strikePoints);
        }

        JackpotShotResolved?.Invoke(hit);
    }

    #region input endpoints
    public void OnShoot(InputAction.CallbackContext ctx)
    {
        if (ctx.started) Shoot();
    }

    public void OnReload(InputAction.CallbackContext ctx)
    {
        if (ctx.started) Reload();
    }

    public void OnSpin(InputAction.CallbackContext ctx)
    {
        if (ctx.started) SpinCylinder();
    }
    #endregion
}
