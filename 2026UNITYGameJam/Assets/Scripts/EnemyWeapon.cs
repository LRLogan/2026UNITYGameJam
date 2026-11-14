using UnityEngine;

/// <summary>
/// Simple weapon that will be weilded by enemies
/// </summary>
public class EnemyWeapon : MonoBehaviour
{
    [SerializeField] private Transform bulletOrigin;
    [SerializeField] private MuzzleFlash muzzleFlash;
    [SerializeField, Min(0)] private int damage = 10;
    [SerializeField, Min(0.01f)] private float shotCooldown = 1f;
    [SerializeField, Min(0f)] private float shotRange = 30f;
    [SerializeField] private LayerMask detectionLayers = Physics.DefaultRaycastLayers;

    private Enemy owner;
    private float nextShotTime;

    private void Awake()
    {
        owner = GetComponentInParent<Enemy>();
        if (muzzleFlash == null) muzzleFlash = GetComponentInChildren<MuzzleFlash>(true);
        // Existing prefabs use EnemyWeapon/Muzzle/MuzzleFlash.
        if (bulletOrigin == null) bulletOrigin = transform.Find("Muzzle");
        if (bulletOrigin == null && muzzleFlash != null) bulletOrigin = muzzleFlash.transform;
        if (muzzleFlash != null) muzzleFlash.SetCameraFacing(true);
    }

    public bool TryShoot(PlayerCore target, Vector3 aimPoint)
    {
        if (!isActiveAndEnabled || owner == null || !owner.isActiveAndEnabled || !owner.IsAlive ||
            target == null || !target.isActiveAndEnabled || target.Health <= 0 || Time.time < nextShotTime)
            return false;

        Vector3 origin = bulletOrigin != null ? bulletOrigin.position : transform.position;
        Vector3 direction = aimPoint - origin;
        if (direction.sqrMagnitude < 0.001f || direction.sqrMagnitude > shotRange * shotRange) return false;

        nextShotTime = Time.time + Mathf.Max(0.01f, shotCooldown);
        if (muzzleFlash != null && muzzleFlash.isActiveAndEnabled) muzzleFlash.Play();
        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, shotRange,
            detectionLayers, QueryTriggerInteraction.Ignore);

        // RaycastAll is unordered. Skip our own colliders and keep the closest solid hit.
        float closestDistance = float.PositiveInfinity;
        PlayerCore hitPlayer = null;
        foreach (RaycastHit hit in hits)
        {
            if (hit.distance >= closestDistance || hit.collider.GetComponentInParent<Enemy>() == owner)
                continue;

            closestDistance = hit.distance;
            hitPlayer = hit.collider.GetComponentInParent<PlayerCore>();
        }

        if (hitPlayer == target) target.TakeDamage(damage);
        return true;
    }
}
