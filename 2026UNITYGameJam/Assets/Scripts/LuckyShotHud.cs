using TMPro;
using UnityEngine;

public class LuckyShotHud : MonoBehaviour
{
    [SerializeField] private Revolver revolver;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private PlayerCore player;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private UnityEngine.UI.Image[] crosshairBars;
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private Color jackpotColor = new Color32(255, 211, 82, 255);

    private void OnEnable()
    {
        if (revolver != null) revolver.CylinderChanged += RefreshCylinder;
        if (gameManager != null) gameManager.ScoreChanged += RefreshStatus;
        if (player == null) player = FindFirstObjectByType<PlayerCore>();
        if (player != null) player.HealthChanged += RefreshStatus;

        RefreshCylinder();
        RefreshStatus();
    }

    private void OnDisable()
    {
        if (revolver != null) revolver.CylinderChanged -= RefreshCylinder;
        if (gameManager != null) gameManager.ScoreChanged -= RefreshStatus;
        if (player != null) player.HealthChanged -= RefreshStatus;
    }

    private void RefreshCylinder()
    {
        if (revolver == null) return;

        bool jackpotReady = revolver.NextShotIsJackpot;
        if (ammoText != null)
        {
            int nextShot = revolver.NextShotNumber;
            string shotStatus = nextShot > 0 ? $"SHOT {nextShot}" : "CYLINDER EMPTY";
            if (gameManager != null)
            {
                shotStatus = gameManager.IsRoundActive
                    ? $"{shotStatus}  |  LUCKY {gameManager.LuckyNumber}"
                    : "NO ACTIVE ROUND";
            }

            string ammo = $"AMMO  {revolver.AmmoRemaining} / {RevolverCylinder.ChamberCount}";
            string jackpotStatus = jackpotReady ? "\nJACKPOT READY" : string.Empty;
            ammoText.text = $"{ammo}\n{shotStatus}{jackpotStatus}";
            ammoText.color = jackpotReady ? jackpotColor : Color.white;
        }

        if (crosshairBars == null) return;

        foreach (UnityEngine.UI.Image bar in crosshairBars)
        {
            if (bar != null) bar.color = jackpotReady ? jackpotColor : crosshairColor;
        }
    }

    private void RefreshStatus()
    {
        if (scoreText == null) return;

        string status = gameManager != null
            ? $"SCORE  {gameManager.TotalScore}\nPENDING  {gameManager.PendingJackpotPoints:+0;-0;0}"
            : string.Empty;
        if (player != null)
        {
            string health = $"HEALTH  {player.Health} / {player.MaxHealth}";
            status = status.Length > 0 ? $"{status}\n{health}" : health;
        }

        scoreText.text = status;
    }
}
