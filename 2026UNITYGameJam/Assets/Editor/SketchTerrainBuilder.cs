using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Install in Assets/Editor. Coordinates trace IMG_0249 with the page top as +Z.
internal static class SketchTerrainBuilder
{
    private const string ScenePath = "Assets/Scenes/Level tests.unity";
    private const string RootName = "Sketch Terrain";
    private const string OutputFolder = "Assets/GeneratedSketchTerrain";
    private const int Resolution = 1025;
    private const float Width = 250f, Length = 310f, Height = 64f;
    private const float Summit = 44f, Floor = 12f, BoreRadius = 4f, RockHalfWidth = 8f;
    private static readonly Vector2[] Boundary = Trace(new float[] {
        175,540, 188,465, 211,410, 255,361, 338,310, 475,280, 575,286,
        685,309, 775,345, 858,410, 931,482, 1025,548, 1090,633, 1163,727,
        1192,841, 1231,991, 1264,1138, 1281,1254, 1298,1323, 1283,1432,
        1251,1535, 1182,1608, 1093,1656, 1003,1689, 933,1685, 862,1642,
        810,1580, 772,1491, 746,1381, 735,1284, 716,1207, 690,1110,
        655,1020, 623,930, 537,848, 424,783, 316,720, 241,672, 210,641, 183,586 });
    private static readonly Vector2[] Northwest = Trace(new float[] {
        175,540, 188,465, 211,410, 255,361, 338,310, 475,280, 575,286,
        685,309, 718,323, 650,334, 579,361, 542,421, 486,462, 452,497,
        421,504, 374,481, 300,481, 253,459, 215,449, 201,426, 150,420 });
    private static readonly Vector2[] Central = Trace(new float[] {
        150,420, 201,426, 215,449, 253,459, 300,481, 374,481, 421,504,
        452,497, 459,536, 486,566, 536,589, 575,614, 598,640,
        663,650, 708,645, 755,641, 802,625, 856,623, 883,654, 908,693,
        928,749, 918,784, 883,805, 832,828, 811,872, 796,903, 767,930,
        760,967, 765,1000, 756,1061, 732,1088, 690,1110, 655,1020,
        623,930, 537,848, 424,783, 316,720, 241,672, 210,641, 183,586, 175,540 });
    private static readonly Vector2[] Southeast = Trace(new float[] {
        991,900, 1026,907, 1049,940, 1066,990, 1100,1054, 1139,1089,
        1155,1130, 1154,1194, 1156,1275, 1140,1322, 1102,1354,
        1041,1357, 991,1352, 962,1322, 943,1280, 932,1216, 916,1171,
        916,1120, 932,1075, 944,1022, 961,976, 962,935, 976,910 });
    private static readonly Vector2 PortalA = Map(743, 645);
    private static readonly Vector2 PortalB = Map(862, 814);
    private static readonly Vector2 Axis = (PortalB - PortalA).normalized;
    private static readonly Vector2 Side = new Vector2(-Axis.y, Axis.x);
    private static readonly float TunnelLength = Vector2.Distance(PortalA, PortalB);

    [MenuItem("Tools/Terrain/Build Sketch in Level tests")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before creating terrain.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new InvalidOperationException("Cannot find " + ScenePath);
        Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        Shader rockShader = Shader.Find("Universal Render Pipeline/Lit");
        if (terrainShader == null || rockShader == null)
            throw new InvalidOperationException("The project's URP terrain and Lit shaders are required.");

        // Load additively, keeping other open scenes and unsaved changes intact.
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        foreach (GameObject existing in scene.GetRootGameObjects())
            if (existing.name == RootName)
            {
                Selection.activeGameObject = existing;
                Debug.Log("Sketch Terrain already exists. Use Undo to remove the generated hierarchy before rebuilding; existing content was preserved.");
                return;
            }

        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets", "GeneratedSketchTerrain");
        string folder = AssetDatabase.GenerateUniqueAssetPath(OutputFolder + "/Sketch");
        AssetDatabase.CreateFolder(OutputFolder, System.IO.Path.GetFileName(folder));
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create sketch terrain");
        GameObject root = null;
        try
        {
            EditorUtility.DisplayProgressBar("Sketch terrain", "Building five elevation regions", 0.1f);
            var data = new TerrainData { name = "Sketch Heightmap", heightmapResolution = Resolution,
                size = new Vector3(Width, Height, Length), alphamapResolution = 512, baseMapResolution = 1024 };
            AssetDatabase.CreateAsset(data, folder + "/Terrain.asset");
            float[,] heights = new float[Resolution, Resolution];
            for (int z = 0; z < Resolution; z++)
                for (int x = 0; x < Resolution; x++)
                    heights[z, x] = Surface(new Vector2(x * Width / (Resolution - 1), z * Length / (Resolution - 1))) / Height;
            data.SetHeights(0, 0, heights);
            int holesSize = data.holesResolution;
            var surface = new bool[holesSize, holesSize];
            for (int z = 0; z < holesSize; z++)
                for (int x = 0; x < holesSize; x++)
                    surface[z, x] = Inside(new Vector2((x + 0.5f) * Width / holesSize,
                        (z + 0.5f) * Length / holesSize), Boundary);
            data.SetHoles(0, 0, surface);
            Paint(data, folder);
            var terrainMaterial = new Material(terrainShader) { name = "Sketch Terrain URP" };
            terrainMaterial.EnableKeyword("_ALPHATEST_ON");
            AssetDatabase.CreateAsset(terrainMaterial, folder + "/Terrain.mat");
            var rock = new Material(rockShader) { name = "Sketch warm stone" };
            rock.SetColor("_BaseColor", new Color(0.43f, 0.38f, 0.29f));
            rock.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.CreateAsset(rock, folder + "/Rock.mat");

            root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create sketch terrain");
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Editable Terrain - E1 to E5";
            SceneManager.MoveGameObjectToScene(terrainObject, scene);
            terrainObject.transform.SetParent(root.transform, false);
            terrainObject.layer = GroundLayer();
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.materialTemplate = terrainMaterial;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 2f;
            terrain.basemapDistance = 1000f;
            Undo.RegisterCreatedObjectUndo(terrainObject, "Create sketch terrain");
            EditorUtility.DisplayProgressBar("Sketch terrain", "Building arch and map edge", 0.8f);
            AddMesh(root, "E5 rock arch - walkable roof and open passage", Arch(), rock, folder + "/Arch.asset");
            AddMesh(root, "Outer map cliff", Edge(), rock, folder + "/MapEdge.asset");
            var spawn = new GameObject("Suggested player start - E1");
            spawn.transform.SetParent(root.transform, false);
            Vector2 start = Map(1000, 1490);
            spawn.transform.localPosition = new Vector3(start.x, Surface(start) + 2f, start.y);
            Undo.RegisterCreatedObjectUndo(spawn, "Create sketch terrain");
            terrain.Flush();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("Created sketch terrain in Level tests. Save that scene to keep it. Terrain is sculptable; the arch is a separate mesh. Undo removes scene objects; generated assets are retained for redo.", root);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogError("Terrain creation failed. Generated assets were retained in " + folder + " for inspection.");
            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Undo.CollapseUndoOperations(group);
        }
    }

    private static int GroundLayer()
    {
        int layer = LayerMask.NameToLayer("Ground");
        return layer < 0 ? 0 : layer;
    }

    private static Vector2 Map(float x, float y) => new Vector2((x - 150f) * 0.21f, (1710f - y) * 0.21f);
    private static Vector2[] Trace(float[] points)
    {
        var result = new Vector2[points.Length / 2];
        for (int i = 0; i < result.Length; i++) result[i] = Map(points[i * 2], points[i * 2 + 1]);
        return result;
    }

    private static bool Inside(Vector2 p, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[i], b = polygon[j];
            if ((a.y > p.y) != (b.y > p.y) &&
                p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    private static float Region(Vector2 p, Vector2[] polygon)
    {
        float distance = float.MaxValue;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[i], edge = polygon[j] - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, edge) / edge.sqrMagnitude);
            distance = Mathf.Min(distance, (p - a - edge * t).magnitude);
        }
        // One metre cliff transition: steep, but still a regular editable heightfield.
        return Mathf.SmoothStep(0f, 1f, 0.5f + (Inside(p, polygon) ? distance : -distance));
    }

    private static float Surface(Vector2 p)
    {
        float pixelX = p.x / 0.21f + 150f;
        float pixelY = 1710f - p.y / 0.21f;
        float southDash = 1220f + 13f * Mathf.Sin((pixelX - 720f) * 0.008f);
        float northDash = 558f + 14f * Mathf.Sin((pixelX - 660f) * 0.009f);
        float h = Mathf.Lerp(3f, Floor, Mathf.SmoothStep(0f, 1f, (southDash + 65f - pixelY) / 130f));
        h = Mathf.Lerp(h, 22f, Mathf.SmoothStep(0f, 1f, (northDash + 55f - pixelY) / 110f));
        h = Mathf.Lerp(h, 32f, Region(p, Northwest));
        h = Mathf.Lerp(h, 32f, Region(p, Southeast));
        h = Mathf.Lerp(h, Summit, Region(p, Central));
        float noise = (Mathf.PerlinNoise(p.x * 0.035f + 17f, p.y * 0.035f + 9f) - 0.5f) * 0.65f;
        h += noise;
        Vector2 offset = p - PortalA;
        float along = Vector2.Dot(offset, Axis), across = Mathf.Abs(Vector2.Dot(offset, Side));
        float lengthBlend = Mathf.SmoothStep(0f, 1f, (along + 5f) / 4f) *
            Mathf.SmoothStep(0f, 1f, (TunnelLength + 5f - along) / 4f);
        // A terrain trench supplies the passage floor. The rock mesh restores E5 above it.
        float trench = 1f - Mathf.SmoothStep(0f, 1f, (across - BoreRadius - 0.75f) / 2f);
        h = Mathf.Lerp(h, Floor, trench * lengthBlend);
        // Flatten the concealed seam beneath the mesh's outer shoulders.
        if (along >= 0f && along <= TunnelLength && across >= 6.75f && across <= RockHalfWidth + 1f)
            h = Mathf.Lerp(h, Summit, 1f - Mathf.SmoothStep(0f, 1f, across - RockHalfWidth));
        return h;
    }

    private static void Paint(TerrainData data, string folder)
    {
        Color[] colors = { new Color(0.49f, 0.44f, 0.29f), new Color(0.35f, 0.40f, 0.23f), new Color(0.43f, 0.38f, 0.29f) };
        string[] names = { "Dry soil", "Upland grass", "Cliff stone" };
        var layers = new TerrainLayer[3];
        for (int i = 0; i < layers.Length; i++)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGB24, true) { name = names[i], wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    pixels[y * 64 + x] = colors[i] * (0.9f + 0.2f * Mathf.PerlinNoise(x * 0.2f, y * 0.2f));
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, folder + "/" + names[i] + " texture.asset");
            layers[i] = new TerrainLayer { name = names[i], diffuseTexture = texture, tileSize = new Vector2(8f, 8f), smoothness = 0f };
            AssetDatabase.CreateAsset(layers[i], folder + "/" + names[i] + ".terrainlayer");
        }
        data.terrainLayers = layers;
        int size = data.alphamapResolution;
        var alpha = new float[size, size, 3];
        for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1), v = z / (float)(size - 1);
                float stone = Mathf.SmoothStep(0f, 1f, (data.GetSteepness(u, v) - 28f) / 32f);
                float grass = Mathf.Clamp01((data.GetInterpolatedHeight(u, v) - 4f) / 25f) * 0.75f;
                alpha[z, x, 0] = (1f - stone) * (1f - grass);
                alpha[z, x, 1] = (1f - stone) * grass;
                alpha[z, x, 2] = stone;
            }
        data.SetAlphamaps(0, 0, alpha);
    }

    private static Vector3 TunnelPoint(float across, float along, float y)
    {
        Vector2 p = PortalA + Axis * along + Side * across;
        return new Vector3(p.x, y, p.y);
    }

    private static Mesh Arch()
    {
        var mesh = new MeshWriter();
        // Each strip is a closed rock prism. The lower curve leaves an 8m-wide,
        // 8m-high vaulted passage; the top remains part of the E5 plateau.
        var section = new List<Vector2> { new Vector2(-RockHalfWidth, Floor - 1f), new Vector2(-BoreRadius, Floor - 1f) };
        const int segments = 20;
        for (int i = 0; i <= segments; i++)
        {
            float theta = Mathf.PI * i / segments;
            section.Add(new Vector2(-BoreRadius * Mathf.Cos(theta), Floor + 3f + 5f * Mathf.Sin(theta)));
        }
        section.Add(new Vector2(BoreRadius, Floor - 1f));
        section.Add(new Vector2(RockHalfWidth, Floor - 1f));
        for (int i = 0; i < section.Count - 1; i++)
        {
            Vector2 a = section[i], b = section[i + 1];
            Vector3 a0 = TunnelPoint(a.x, 0f, a.y), b0 = TunnelPoint(b.x, 0f, b.y);
            Vector3 a1 = TunnelPoint(a.x, TunnelLength, a.y), b1 = TunnelPoint(b.x, TunnelLength, b.y);
            Vector3 at0 = TunnelPoint(a.x, 0f, Summit), bt0 = TunnelPoint(b.x, 0f, Summit);
            Vector3 at1 = TunnelPoint(a.x, TunnelLength, Summit), bt1 = TunnelPoint(b.x, TunnelLength, Summit);
            mesh.Quad(at0, at1, bt1, bt0, Vector3.up);
            Vector3 inward = new Vector3(-Side.x * (a.x + b.x), -2f, -Side.y * (a.x + b.x));
            mesh.Quad(a0, b0, b1, a1, inward);
            mesh.Quad(a0, at0, bt0, b0, new Vector3(-Axis.x, 0f, -Axis.y));
            mesh.Quad(a1, b1, bt1, at1, new Vector3(Axis.x, 0f, Axis.y));
        }
        foreach (float side in new[] { -RockHalfWidth, RockHalfWidth })
            mesh.Quad(TunnelPoint(side, 0f, Floor - 1f), TunnelPoint(side, TunnelLength, Floor - 1f),
                TunnelPoint(side, TunnelLength, Summit), TunnelPoint(side, 0f, Summit),
                new Vector3(Side.x * side, 0f, Side.y * side));
        return mesh.Create("Central arch");
    }

    private static Mesh Edge()
    {
        var mesh = new MeshWriter();
        for (int i = 0; i < Boundary.Length; i++)
        {
            Vector2 a = Boundary[i], b = Boundary[(i + 1) % Boundary.Length];
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) / 0.5f);
            for (int s = 0; s < steps; s++)
            {
                Vector2 p = Vector2.Lerp(a, b, s / (float)steps), q = Vector2.Lerp(a, b, (s + 1f) / steps);
                Vector2 normal = new Vector2(q.y - p.y, p.x - q.x).normalized;
                if (Inside((p + q) * 0.5f + normal * 0.2f, Boundary)) normal = -normal;
                Vector3 outward = new Vector3(normal.x, 0f, normal.y);
                float hp = Surface(p), hq = Surface(q);
                mesh.Quad(new Vector3(p.x, -8f, p.y), new Vector3(q.x, -8f, q.y),
                    new Vector3(q.x, hq, q.y), new Vector3(p.x, hp, p.y), outward);
                // A narrow lip covers the rasterized hole-mask boundary.
                Vector2 pin = p - normal * 0.6f, qin = q - normal * 0.6f;
                mesh.Quad(new Vector3(p.x, hp, p.y), new Vector3(q.x, hq, q.y),
                    new Vector3(qin.x, Surface(qin) + 0.03f, qin.y), new Vector3(pin.x, Surface(pin) + 0.03f, pin.y), Vector3.up);
            }
        }
        return mesh.Create("Map edge cliffs");
    }

    private static void AddMesh(GameObject root, string name, Mesh mesh, Material material, string path)
    {
        AssetDatabase.CreateAsset(mesh, path);
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        SceneManager.MoveGameObjectToScene(go, root.scene);
        go.transform.SetParent(root.transform, false);
        go.layer = GroundLayer();
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        go.GetComponent<MeshCollider>().sharedMesh = mesh;
        Undo.RegisterCreatedObjectUndo(go, "Create sketch terrain");
    }

    private sealed class MeshWriter
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<Vector2> uv = new List<Vector2>();
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            // Ignore zero-width top strips at the arch's vertical jambs.
            if (Vector3.Cross(b - a, c - a).sqrMagnitude < 0.000001f) return;
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) { Vector3 swap = b; b = d; d = swap; }
            int n = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            uv.AddRange(new[] { Vector2.zero, new Vector2(Vector3.Distance(a, b) / 8f, 0f),
                new Vector2(Vector3.Distance(a, b) / 8f, Vector3.Distance(b, c) / 8f), new Vector2(0f, Vector3.Distance(a, d) / 8f) });
            triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
        }
        public Mesh Create(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
