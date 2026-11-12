using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private PlayerCore player;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private List<Transform> objectiveSpawnPoints = new List<Transform>();
    [SerializeField, Min(0.1f)] private float interactRadius = 2f;
    [SerializeField, Min(0.1f)] private float extractionRadius = 3f;
    [SerializeField] private string interactPrompt = "Press Interact to pick up";
    [SerializeField] private string mainMenuScene = "MainMenu";

    private readonly Queue<Transform> remainingObjectives = new Queue<Transform>();
    private PlayerObjective activeObjective;
    private PlayerObjective extractionZone;
    private LevelHud hud;
    private Vector3 initialPlayerPosition;
    private double roundStartTime;
    private int totalObjectives;
    private float previousTimeScale;
    private InputActionMap pausedActionMap;
    private bool showingResults;
    private bool loadingScene;

    private void Start()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (player == null) player = FindFirstObjectByType<PlayerCore>();
        if (gameManager == null || player == null)
        {
            Debug.LogWarning("LevelManager needs a GameManager and PlayerCore.", this);
            return;
        }

        initialPlayerPosition = player.transform.position;
        var interaction = player.GetComponent<PlayerInteract>();
        if (interaction != null) interaction.Bind(this);

        hud = new LevelHud(transform);
        gameManager.RoundStarted += BeginLevel;
        gameManager.RoundEnded += ClearLevel;
        if (gameManager.IsRoundActive) BeginLevel();
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.RoundStarted -= BeginLevel;
            gameManager.RoundEnded -= ClearLevel;
        }

        if (showingResults) ResumeGameplay();
    }

    private void Update()
    {
        if (activeObjective != null) activeObjective.ShowPrompt(CanCollect(player));
    }

    private void BeginLevel()
    {
        roundStartTime = Time.timeAsDouble;
        remainingObjectives.Clear();
        foreach (Transform point in objectiveSpawnPoints)
        {
            if (point != null) remainingObjectives.Enqueue(point);
        }

        totalObjectives = remainingObjectives.Count;
        if (totalObjectives == 0)
        {
            hud.SetInstructions("Assign an objective spawn point to begin.");
            return;
        }

        SpawnNextObjective();
    }

    private void SpawnNextObjective()
    {
        Transform point = remainingObjectives.Dequeue();
        GameObject pickup = CreateMarker("Objective", point.position, new Color32(255, 185, 54, 255));
        activeObjective = pickup.AddComponent<PlayerObjective>();

        var prompt = new GameObject("Interact Prompt", typeof(RectTransform)).AddComponent<TextMeshPro>();
        prompt.transform.SetParent(pickup.transform, false);
        prompt.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        prompt.rectTransform.sizeDelta = new Vector2(6f, 1f);
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.fontSize = 3f;
        prompt.text = interactPrompt;
        activeObjective.SetPrompt(prompt, player);
        hud.SetInstructions($"Find objective {totalObjectives - remainingObjectives.Count} / {totalObjectives}");
    }

    private bool CanCollect(PlayerCore candidate)
    {
        return gameManager != null && gameManager.IsRoundActive && activeObjective != null &&
            candidate != null && candidate == player &&
            (candidate.transform.position - activeObjective.transform.position).sqrMagnitude <= interactRadius * interactRadius;
    }

    internal void TryCollect(PlayerCore candidate)
    {
        if (!CanCollect(candidate)) return;

        RemoveMarker(activeObjective);
        activeObjective = null;
        if (remainingObjectives.Count > 0) SpawnNextObjective();
        else EnableExtraction();
    }

    private void EnableExtraction()
    {
        Vector3 position = spawnPoint != null ? spawnPoint.position : initialPlayerPosition;
        GameObject marker = CreateMarker("Extraction", position, new Color32(75, 235, 175, 255));
        var trigger = marker.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = extractionRadius;
        extractionZone = marker.AddComponent<PlayerObjective>();
        extractionZone.EnableExtraction(this);
        hud.SetInstructions("All objectives collected. Return to spawn to extract.");
    }

    internal void TryExtract(PlayerCore candidate)
    {
        if (extractionZone == null || candidate == null || candidate != player || !gameManager.IsRoundActive)
            return;

        gameManager.CompleteRound(Time.timeAsDouble - roundStartTime, player);
        showingResults = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        PlayerInput input = player.GetComponent<PlayerInput>();
        if (input != null && input.currentActionMap != null && input.currentActionMap.enabled)
        {
            pausedActionMap = input.currentActionMap;
            pausedActionMap.Disable();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        hud.ShowResult(gameManager.LastResult, () => LoadScene(gameObject.scene.path), () => LoadScene(mainMenuScene));
    }

    private void LoadScene(string scene)
    {
        if (loadingScene) return;
        if (scene != null && scene.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
            scene = scene.Substring(0, scene.Length - ".unity".Length);

        if (string.IsNullOrWhiteSpace(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            string sceneName = string.IsNullOrWhiteSpace(scene) ? "the destination scene" : System.IO.Path.GetFileName(scene);
            hud.SetLoadError($"Add {sceneName} to the Build Profile's scene list.");
            return;
        }

        loadingScene = true;
        ResumeGameplay();
        SceneManager.LoadScene(scene);
    }

    private void ResumeGameplay()
    {
        showingResults = false;
        Time.timeScale = previousTimeScale;
        pausedActionMap?.Enable();
        pausedActionMap = null;
    }

    private void ClearLevel()
    {
        RemoveMarker(activeObjective);
        RemoveMarker(extractionZone);
        activeObjective = null;
        extractionZone = null;
        remainingObjectives.Clear();
        hud.SetInstructions(string.Empty);
    }

    private GameObject CreateMarker(string name, Vector3 position, Color color)
    {
        var marker = new GameObject(name);
        marker.transform.SetParent(transform, false);
        marker.transform.position = position;
        CreateMarkerPart(marker.transform, "Cube", new Vector3(0f, 0.45f, 0f), Vector3.one * 0.8f, color);
        CreateMarkerPart(marker.transform, "Beacon", new Vector3(0f, 3f, 0f), new Vector3(0.12f, 6f, 0.12f), color);
        return marker;
    }

    private static void CreateMarkerPart(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<BoxCollider>().enabled = false;

        var tint = new MaterialPropertyBlock();
        tint.SetColor("_BaseColor", color);
        tint.SetColor("_Color", color);
        part.GetComponent<Renderer>().SetPropertyBlock(tint);
    }

    private static void RemoveMarker(PlayerObjective marker)
    {
        if (marker == null) return;
        marker.gameObject.SetActive(false);
        Destroy(marker.gameObject);
    }
}
