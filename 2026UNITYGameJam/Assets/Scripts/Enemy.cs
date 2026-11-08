using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    [SerializeField] private float detectionRad;
    [SerializeField] private int maxHealth = 100;
    public int health;
    private Camera playerCamera;

    [Header("Health bar")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Transform healthSliderContainer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Assign player cam
        if (playerCamera == null)
            playerCamera = Camera.main;

        health = maxHealth;
        if (healthSlider != null)
        {
            healthSlider.minValue = 0;
            healthSlider.maxValue = maxHealth;
            healthSlider.wholeNumbers = true;
        }
        UpdateHealthBar();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void LateUpdate()
    {
        // Keep health bar parallel to camera's screen
        if(playerCamera != null)
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
        health -= damage;
        health = Math.Max(health, 0);

        UpdateHealthBar();
    }
}
