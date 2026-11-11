using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] private PlayerCore player;
    [SerializeField] private EnemyWeapon weapon;
    [SerializeField, Min(0f)] private float detectionRad = 15f;
    [SerializeField] private SphereCollider detectionSphere;
    [SerializeField, Min(1)] private int maxHealth = 100;
    public int health;
    private Camera playerCamera;
    private Collider playerCollider;

    [Header("Health bar")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Transform healthSliderContainer;


    public bool IsAlive => health > 0;

    private void Awake()
    {
        health = maxHealth;
        if (detectionSphere != null) detectionSphere.isTrigger = true;
        if (healthSlider != null)
        {
            healthSlider.minValue = 0;
            healthSlider.maxValue = maxHealth;
            healthSlider.wholeNumbers = true;
        }
        UpdateHealthBar();
    }

    private void Start()
    {
        playerCamera = Camera.main;
        if (player == null) player = FindFirstObjectByType<PlayerCore>();
        if (player != null) playerCollider = player.GetComponent<Collider>();
        detectionSphere.radius = detectionRad;
    }

    private void Update()
    {
        if (!IsAlive || player == null || !player.isActiveAndEnabled || player.Health <= 0 || Time.deltaTime <= 0f)
            return;

        Vector3 center = transform.position;

        if ((player.transform.position - center).sqrMagnitude > Mathf.Pow(detectionRad, 2)) return;

        Vector3 aimPoint = playerCollider != null ? playerCollider.bounds.center : player.transform.position;
        Vector3 direction = aimPoint - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        if (weapon != null) weapon.TryShoot(player, aimPoint);
    }

    private void OnDrawGizmos()
    {
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRad);
        Gizmos.color = previousColor;
    }

    private void LateUpdate()
    {
        // Keep health bar parallel to camera's screen
        if (playerCamera != null && healthSliderContainer != null)
            healthSliderContainer.rotation = playerCamera.transform.rotation;
    }

    private void UpdateHealthBar()
    {
        if (healthSlider != null)
        {
            healthSlider.value = health;
        }
    }

    public void TakeDamage(int damage)
    {
        if (!IsAlive || damage <= 0) return;

        health -= Mathf.Min(damage, health);
        UpdateHealthBar();

        if (!IsAlive) Destroy(gameObject);
    }

    public void Kill()
    {
        TakeDamage(health);
    }
}
