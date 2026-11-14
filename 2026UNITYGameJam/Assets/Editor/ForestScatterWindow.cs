using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ForestScatterWindow : EditorWindow
{
    [SerializeField] private List<GameObject> sources = new List<GameObject>();
    [SerializeField] private int count = 300;
    [SerializeField] private int seed = 137;
    [SerializeField] private float clearance = 0.5f;
    [SerializeField] private float heightVariation = 0.12f;
    [SerializeField] private float maxSlope = 25f;
    [SerializeField] private float patchSize = 30f;
    private Vector2 scroll;
    private string result;

    private struct Tree
    {
        public GameObject source;
        public Bounds bounds;
        public float radius;
    }

    [MenuItem("Tools/Forest/Scatter Trees")]
    private static void Open() => GetWindow<ForestScatterWindow>("Forest Scatter");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Select one whole tree root per desired type in the Hierarchy, then capture selection. Run outside Play mode. Placement uses the active scene's Ground colliders and avoids other renderers/colliders. Save the scene afterwards.", MessageType.Info);
        if (GUILayout.Button("Use Selected Trees as Sources"))
        {
            sources.Clear();
            foreach (GameObject go in Selection.gameObjects)
                if (!EditorUtility.IsPersistent(go) && go.scene == SceneManager.GetActiveScene()
                    && go.activeInHierarchy && go.GetComponentsInChildren<Renderer>().Length > 0)
                    sources.Add(go);
        }
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(110));
        foreach (GameObject source in sources)
            EditorGUILayout.ObjectField(source, typeof(GameObject), true);
        EditorGUILayout.EndScrollView();
        count = Mathf.Clamp(EditorGUILayout.IntField("New tree count", count), 1, 5000);
        seed = EditorGUILayout.IntField("Random seed", seed);
        clearance = Mathf.Max(0.1f, EditorGUILayout.FloatField("Extra clearance (meters)", clearance));
        heightVariation = EditorGUILayout.Slider("Height variation", heightVariation, 0f, 0.2f);
        maxSlope = EditorGUILayout.Slider("Maximum slope", maxSlope, 0f, 40f);
        patchSize = Mathf.Max(5f, EditorGUILayout.FloatField("Forest patch size", patchSize));
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || sources.Count == 0))
            if (GUILayout.Button("Scatter Into Scene (Undo Supported)")) Scatter();
        if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
    }

    private static bool TryBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            if (!found) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            found = true;
        }
        return found && bounds.size.sqrMagnitude > 0.001f;
    }

    private void Scatter()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            result = "Exit Prefab Mode and open the target scene first.";
            return;
        }
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0) { result = "Create a Ground layer first."; return; }
        var trees = new List<Tree>();
        foreach (GameObject source in sources)
        {
            if (source == null || source.scene != scene || !source.activeInHierarchy) continue;
            foreach (GameObject other in sources)
                if (other != null && other != source && source.transform.IsChildOf(other.transform))
                { result = "Select individual tree roots, without selecting their parents or children too."; return; }
            if (!TryBounds(source, out Bounds bounds)) continue;
            Vector3 scale = source.transform.lossyScale;
            if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0
                || Vector3.Dot(source.transform.up, Vector3.up) < 0.999f)
            { result = "Source trees must be upright with positive scale. Select their whole root objects."; return; }
            // Include offset pivots and every LOD in a rotation-invariant footprint.
            Vector3 offset = bounds.center - source.transform.position;
            float x = Mathf.Abs(offset.x) + bounds.extents.x;
            float z = Mathf.Abs(offset.z) + bounds.extents.z;
            trees.Add(new Tree { source = source, bounds = bounds, radius = Mathf.Sqrt(x * x + z * z) });
        }
        if (trees.Count == 0) { result = "Capture at least one valid tree from the active scene."; return; }

        Physics.SyncTransforms();
        var grounds = new HashSet<Collider>();
        var obstacles = new List<Bounds>();
        Bounds area = default;
        bool hasArea = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                bool sourcePart = root.name == "Scattered Forest";
                foreach (Tree tree in trees)
                    if (collider.transform.IsChildOf(tree.source.transform)) sourcePart = true;
                if (collider.gameObject.layer == groundLayer && !sourcePart)
                {
                    grounds.Add(collider);
                    if (!hasArea) area = collider.bounds;
                    else area.Encapsulate(collider.bounds);
                    hasArea = true;
                }
                else obstacles.Add(collider.bounds);
            }
        }
        if (!hasArea) { result = "No enabled, non-trigger Ground colliders found in the active scene."; return; }
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                // Only exclude geometry explicitly assigned to Ground. Trees remain obstacles.
                bool sourcePart = root.name == "Scattered Forest";
                foreach (Tree tree in trees)
                    if (renderer.transform.IsChildOf(tree.source.transform)) sourcePart = true;
                if (renderer.gameObject.layer != groundLayer || sourcePart) obstacles.Add(renderer.bounds);
            }
        // Terrain-painted trees have no ordinary scene renderers: conservatively exclude their bounds too.
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>())
            {
                TerrainData data = terrain.terrainData;
                if (data == null) continue;
                foreach (TreeInstance instance in data.treeInstances)
                {
                    GameObject prefab = data.treePrototypes[instance.prototypeIndex].prefab;
                    if (prefab == null || !TryBounds(prefab, out Bounds b)) continue;
                    Vector3 size = b.size;
                    float radius = Mathf.Max(size.x, size.z) * instance.widthScale;
                    float height = size.y * instance.heightScale;
                    Vector3 basePoint = terrain.transform.position + Vector3.Scale(instance.position, data.size);
                    obstacles.Add(new Bounds(basePoint + Vector3.up * height * 0.5f,
                        new Vector3(radius * 2, height, radius * 2)));
                }
            }

        var random = new System.Random(seed);
        float noiseOffset = (float)random.NextDouble() * 10000f;
        int placed = 0;
        int attempts = 0;
        int maxAttempts = Mathf.Min(count * 80, 200000);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Scatter Forest");
        GameObject forest = null;
        try
        {
            forest = new GameObject("Scattered Forest");
            SceneManager.MoveGameObjectToScene(forest, scene);
            Undo.RegisterCreatedObjectUndo(forest, "Create Forest Group");
            while (placed < count && attempts++ < maxAttempts)
            {
                if (attempts % 100 == 0 && EditorUtility.DisplayCancelableProgressBar("Scatter Forest",
                    $"Placed {placed}/{count}; testing clear ground", (float)attempts / maxAttempts)) break;
                float x = Mathf.Lerp(area.min.x, area.max.x, (float)random.NextDouble());
                float z = Mathf.Lerp(area.min.z, area.max.z, (float)random.NextDouble());
                float density = Mathf.SmoothStep(0.05f, 1f,
                    Mathf.InverseLerp(0.3f, 0.7f, Mathf.PerlinNoise(x / patchSize + noiseOffset, z / patchSize + noiseOffset)));
                if (random.NextDouble() > density) continue;
                Tree tree = trees[random.Next(trees.Count)];
                float heightScale = 1f + ((float)random.NextDouble() * 2f - 1f) * heightVariation;
                float radius = tree.radius + clearance;
                if (!GroundAt(x, z, area, grounds, out RaycastHit hit)) continue;
                bool supported = true;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 0.25f;
                    if (!GroundAt(x + Mathf.Cos(angle) * radius, z + Mathf.Sin(angle) * radius, area, grounds, out RaycastHit edge)
                        || Mathf.Abs(edge.point.y - hit.point.y) > Mathf.Max(0.3f, radius * Mathf.Tan(maxSlope * Mathf.Deg2Rad)))
                    { supported = false; break; }
                }
                if (!supported) continue;
                float height = tree.bounds.size.y * heightScale;
                Bounds candidate = new Bounds(hit.point + Vector3.up * height * 0.5f,
                    new Vector3(radius * 2f, height + clearance * 2f, radius * 2f));
                bool blocked = false;
                foreach (Bounds obstacle in obstacles)
                    if (candidate.Intersects(obstacle)) { blocked = true; break; }
                if (blocked) continue;

                // Clone scene configuration, including existing material and component overrides.
                GameObject clone = Instantiate(tree.source, forest.transform);
                clone.name = tree.source.name + " (Forest)";
                clone.transform.localScale = Vector3.Scale(tree.source.transform.lossyScale, new Vector3(1, heightScale, 1));
                clone.transform.rotation = Quaternion.AngleAxis((float)random.NextDouble() * 360f, Vector3.up) * tree.source.transform.rotation;
                float bottomOffset = (tree.bounds.min.y - tree.source.transform.position.y) * heightScale;
                clone.transform.position = hit.point - Vector3.up * bottomOffset;
                Undo.RegisterCreatedObjectUndo(clone, "Place Forest Tree");
                obstacles.Add(candidate);
                placed++;
            }
            if (placed == 0) Undo.DestroyObjectImmediate(forest);
            else
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = forest;
            }
            result = $"Placed {placed} of {count} trees after {attempts} attempts. "
                + "Save the scene to keep them. Undo removes this batch. "
                + (placed < count ? "Stopped early or ran out of safe spots; spacing was preserved." : "");
            Debug.Log(result);
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            result = "Scatter failed; the batch was rolled back. See Console.";
            Debug.LogException(exception);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Undo.CollapseUndoOperations(undoGroup);
            Physics.SyncTransforms();
        }
    }

    private bool GroundAt(float x, float z, Bounds area, HashSet<Collider> grounds, out RaycastHit hit)
    {
        // First solid surface must be Ground, so roofs/rocks cannot be planted on.
        if (!Physics.Raycast(new Vector3(x, area.max.y + 1000f, z), Vector3.down, out hit,
            area.size.y + 2000f, ~0, QueryTriggerInteraction.Ignore)) return false;
        return grounds.Contains(hit.collider) && Vector3.Angle(hit.normal, Vector3.up) <= maxSlope;
    }
}
