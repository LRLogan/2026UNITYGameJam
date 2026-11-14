using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] private EnemyWeapon weapon;
    [SerializeField, Min(0f)] private float detectionRad = 15f;
    [SerializeField] private SphereCollider detectionSphere;
    [SerializeField, Min(1)] private int maxHealth = 100;
    public int health;
    private Camera playerCamera;
    private Collider playerCollider;
    private PlayerCore player;

    [Header("Animation")]
    [SerializeField] private Animator characterAnimator;
    private static readonly int ShootTrigger = Animator.StringToHash("Shoot");
    private bool hasShootTrigger;

    [Header("Health bar")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Transform healthSliderContainer;


    public bool IsAlive => health > 0;

    private void Awake()
    {
        health = maxHealth;
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
        if (characterAnimator != null && characterAnimator.runtimeAnimatorController != null)
        {
            characterAnimator.applyRootMotion = false;
            foreach (AnimatorControllerParameter parameter in characterAnimator.parameters)
                if (parameter.nameHash == ShootTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
                    hasShootTrigger = true;
        }
        if (detectionSphere != null)
        {
            detectionSphere.isTrigger = true;
            detectionSphere.radius = detectionRad;
        }

        Canvas canvas = healthSlider != null ? healthSlider.GetComponentInParent<Canvas>() : null;
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace || healthSlider.fillRect == null)
            CreateHealthBar();
        else if (healthSliderContainer == null)
            healthSliderContainer = healthSlider.transform;

        healthSlider.minValue = 0;
        healthSlider.maxValue = maxHealth;
        healthSlider.wholeNumbers = true;
        UpdateHealthBar();
    }

    private void Start()
    {
        playerCamera = Camera.main;
        if (player == null) player = FindFirstObjectByType<PlayerCore>();
        if (player != null) playerCollider = player.GetComponent<Collider>();
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

        if (weapon != null && weapon.TryShoot(player, aimPoint)
            && hasShootTrigger && characterAnimator != null && characterAnimator.isActiveAndEnabled)
        {
            characterAnimator.ResetTrigger(ShootTrigger);
            characterAnimator.SetTrigger(ShootTrigger);
        }
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

    private void CreateHealthBar()
    {
        // Preserve the prefab's authored anchor before assigning the generated billboard.
        Transform anchor = healthSliderContainer != null ? healthSliderContainer : transform.Find("Enemy hp");
        var bar = new GameObject("Enemy Health", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.Image), typeof(Slider));
        RectTransform barRect = (RectTransform)bar.transform;
        barRect.SetParent(anchor != null ? anchor : transform, false);
        barRect.sizeDelta = new Vector2(100f, 10f);
        barRect.localScale = Vector3.one * 0.012f;

        if (anchor == null)
        {
            float top = transform.position.y + 1f;
            foreach (Collider body in GetComponents<Collider>())
                if (body.enabled && !body.isTrigger && body != detectionSphere)
                    top = Mathf.Max(top, body.bounds.max.y);
            barRect.position = new Vector3(transform.position.x, top + 0.25f, transform.position.z);
        }
        bar.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var background = bar.GetComponent<UnityEngine.UI.Image>();
        background.color = new Color32(20, 20, 24, 235);
        background.raycastTarget = false;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        RectTransform fillRect = (RectTransform)fill.transform;
        fillRect.SetParent(barRect, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        var fillImage = fill.GetComponent<UnityEngine.UI.Image>();
        fillImage.color = new Color32(235, 72, 72, 255);
        fillImage.raycastTarget = false;

        healthSlider = bar.GetComponent<Slider>();
        healthSlider.interactable = false;
        healthSlider.fillRect = fillRect;
        healthSliderContainer = barRect;
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
