using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Actual Revolver weapon responcible for overarching events like shooting the shot
/// and user input endpoints
/// </summary>
public class Revolver : MonoBehaviour
{
    [SerializeField] private PlayerCore player;

    [Header("Shooting")]
    [SerializeField, Min(0.01f)] private float bulletRad = 0.5f;
    [SerializeField, Min(1f)] private float shotRange = 100f;
    [SerializeField, Min(1)] private int standardDamage = 20;
    [SerializeField] private LayerMask detectionLayers = Physics.DefaultRaycastLayers;

    [Header("Weapon visuals")]
    [SerializeField] private Transform weaponCenter;
    [SerializeField] private Transform weaponCamAllignment;
    [SerializeField, FormerlySerializedAs("bulletOrigin")] private Transform weaponMuzzle;
    [SerializeField, Min(0.05f)] private float weaponSpinDuration = 0.35f;

    [Header("Jackpot")]
    [SerializeField, Range(1f, 2f)] private float luckyChamberWeight = 1.25f;
    [SerializeField, Min(0)] private int jackpotHeal = 25;
    [SerializeField] private JackpotEffects jackpotEffects = new JackpotEffects();

    private readonly RevolverCylinder cylinder = new RevolverCylinder(new System.Random());
    private GameManager gameManager;
    private Quaternion weaponRestRotation;
    private Quaternion alignmentRestRotation;
    private float weaponSpinAngle;
    private Tween weaponSpinTween;
    private MuzzleFlash muzzleFlash;

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
        if (weaponCenter == null) weaponCenter = transform;
        if (weaponCamAllignment == null) weaponCamAllignment = weaponCenter;
        weaponRestRotation = weaponCenter.localRotation;
        alignmentRestRotation = weaponCamAllignment.localRotation;
        if (weaponMuzzle != null) muzzleFlash = weaponMuzzle.GetComponentInChildren<MuzzleFlash>();
    }

    private void LateUpdate()
    {
        UpdateWeaponRotation();
    }

    private void OnDisable()
    {
        weaponSpinTween?.Kill();
        weaponSpinTween = null;
        weaponSpinAngle = 0f;
        UpdateWeaponRotation();
    }

    private void UpdateWeaponRotation()
    {
        if (weaponCamAllignment != null && player != null)
        {
            Vector3 aim = player.AimTransform.forward;
            if (weaponCamAllignment.parent != null)
                aim = weaponCamAllignment.parent.InverseTransformDirection(aim);

            float horizontal = Mathf.Sqrt(aim.x * aim.x + aim.z * aim.z);
            float pitch = -Mathf.Atan2(aim.y, horizontal) * Mathf.Rad2Deg;
            if (weaponCamAllignment == weaponCenter) pitch += weaponSpinAngle;
            weaponCamAllignment.localRotation = Quaternion.AngleAxis(pitch, Vector3.right) * alignmentRestRotation;
        }

        if (weaponCenter != null && weaponCenter != weaponCamAllignment)
            weaponCenter.localRotation = Quaternion.AngleAxis(weaponSpinAngle, Vector3.right) * weaponRestRotation;
    }

    private void AnimateWeaponSpin()
    {
        if (weaponCenter == null) return;

        weaponSpinTween?.Kill();
        weaponSpinAngle = 0f;
        weaponSpinTween = DOTween.To(() => weaponSpinAngle, angle => weaponSpinAngle = angle,
                360f, weaponSpinDuration)
            .SetEase(Ease.OutCubic)
            .OnComplete(() => weaponSpinAngle = 0f);
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
        if (!CanUseWeapon) return;

        LoadCylinder();
        AnimateWeaponSpin();
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

        cylinder.Spin(luckyChamberWeight);
        AnimateWeaponSpin();
        CylinderChanged?.Invoke();
    }

    public void Shoot()
    {
        if (!CanUseWeapon || player == null || !cylinder.TryFire(out BulletType bullet)) return;

        UpdateWeaponRotation();
        if (muzzleFlash != null && muzzleFlash.isActiveAndEnabled) muzzleFlash.Play();

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
        Vector3 origin = weaponMuzzle != null ? weaponMuzzle.position : aim.position;
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
