using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private List<Transform> objectiveSpawnPoints = new List<Transform>();
    [SerializeField, Min(0.1f)] private float interactRadius = 2f;
    [SerializeField, Min(0.1f)] private float extractionRadius = 3f;
    [SerializeField] private string interactPrompt = "Press Interact to pick up";
    [SerializeField] private string mainMenuScene = "MainMenu";
    [Header("Objective visuals")]
    [SerializeField] private GameObject objectiveSwordPrefab;
    [SerializeField] private Material beaconMaterial;
    [SerializeField, Min(10f)] private float beaconHeight = 150f;
    [SerializeField, Min(0.1f)] private float beaconWidth = 3f;
    [SerializeField, Min(0.1f)] private float swordHeight = 1.5f;

    private PlayerCore player;
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
        if (activeObjective != null) activeObjective.ShowPrompt(IsInPickupRange());
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
        GameObject pickup = CreateMarker("Objective", point.position, new Color32(255, 185, 54, 255), true);
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

    private bool IsInPickupRange()
    {
        return activeObjective != null && player != null &&
            Vector3.Distance(player.transform.position, activeObjective.transform.position) <= interactRadius;
    }

    internal void TryCollect()
    {
        if (!IsInPickupRange()) return;

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

    private GameObject CreateMarker(string name, Vector3 position, Color color, bool isPickup = false)
    {
        var marker = new GameObject(name);
        marker.transform.SetParent(transform, false);
        marker.transform.position = position;
        if (isPickup && objectiveSwordPrefab != null)
        {
            GameObject sword = Instantiate(objectiveSwordPrefab, marker.transform);
            sword.name = "Objective Sword";
            sword.transform.localPosition = Vector3.zero;
            sword.transform.localRotation = Quaternion.identity;
            // Normalize the asset's longest dimension into an upright, readable pickup.
            Renderer[] renderers = sword.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Vector3 size = bounds.size;
                if (size.x > size.y && size.x > size.z) sword.transform.rotation = Quaternion.Euler(0, 0, 90);
                else if (size.z > size.y) sword.transform.rotation = Quaternion.Euler(-90, 0, 0);
                bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                sword.transform.localScale *= swordHeight / Mathf.Max(0.001f, bounds.size.y);
                bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                sword.transform.position += new Vector3(position.x - bounds.center.x,
                    position.y + 0.15f - bounds.min.y, position.z - bounds.center.z);
            }
            foreach (Collider collider in sword.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (Rigidbody body in sword.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
        }
        else CreateMarkerPart(marker.transform, "Marker Base", new Vector3(0f, 0.15f, 0f), new Vector3(1.2f, 0.15f, 1.2f), color);

        if (beaconMaterial != null)
        {
            var beamObject = new GameObject("Beacon");
            beamObject.transform.SetParent(marker.transform, false);
            var beam = beamObject.AddComponent<LineRenderer>();
            beam.sharedMaterial = beaconMaterial;
            beam.useWorldSpace = true;
            beam.alignment = LineAlignment.View;
            beam.textureMode = LineTextureMode.Stretch;
            beam.positionCount = 2;
            beam.SetPosition(0, position + Vector3.up * 0.1f);
            beam.SetPosition(1, position + Vector3.up * beaconHeight);
            beam.startWidth = beaconWidth;
            beam.endWidth = beaconWidth * 0.6f;
            beam.startColor = color;
            beam.endColor = color;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.receiveShadows = false;
        }
        else CreateMarkerPart(marker.transform, "Beacon", Vector3.up * beaconHeight * 0.5f,
            new Vector3(0.2f, beaconHeight, 0.2f), color);
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
