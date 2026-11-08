using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    [SerializeField] private float detectionRad;
    [SerializeField, Min(1)] private int maxHealth = 100;
    public int health;
    private Camera playerCamera;

    [Header("Health bar")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Transform healthSliderContainer;

    public bool IsAlive => health > 0;

    private void Awake()
    {
        health = maxHealth;
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
