using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class LuckyShotHudSetup
{
    private const string MenuPath = "Tools/Lucky Shot/";
    private const string UndoLabel = "Create Lucky Shot HUD";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem(MenuPath + "Create HUD")]
    private static void CreateHud()
    {
        Scene scene = SceneManager.GetActiveScene();
        Revolver[] revolvers = FindInScene<Revolver>(scene);
        GameManager[] managers = FindInScene<GameManager>(scene);

        if (FindInScene<LuckyShotHud>(scene).Length > 0)
        {
            Debug.Log("The active scene already has a Lucky Shot HUD.");
            return;
        }

        if (revolvers.Length != 1 || managers.Length > 1)
        {
            Debug.LogWarning("Create HUD requires one Revolver and at most one GameManager in the active scene.");
            return;
        }

        Revolver revolver = revolvers[0];
        GameManager manager = managers.SingleOrDefault();
        if (manager != null)
        {
            Object assignedRevolver = new SerializedObject(manager).FindProperty("revolver").objectReferenceValue;
            if (assignedRevolver != null && assignedRevolver != revolver)
            {
                Debug.LogWarning("The GameManager already points to a different Revolver.", manager);
                return;
            }
        }

        TMP_FontAsset font = LoadFont();
        if (font == null) return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoLabel);

        if (manager == null)
        {
            var managerObject = new GameObject("GameManager");
            Undo.RegisterCreatedObjectUndo(managerObject, UndoLabel);
            manager = Undo.AddComponent<GameManager>(managerObject);
        }

        var managerData = new SerializedObject(manager);
        managerData.FindProperty("revolver").objectReferenceValue = revolver;
        managerData.ApplyModifiedProperties();

        var root = new GameObject("LuckyShotHUD", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, UndoLabel);

        Canvas canvas = Undo.AddComponent<Canvas>(root);
        Undo.RecordObject(canvas, UndoLabel);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = Undo.AddComponent<UnityEngine.UI.CanvasScaler>(root);
        Undo.RecordObject(scaler, UndoLabel);
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var topLeft = new Vector2(0f, 1f);
        var bottomRight = new Vector2(1f, 0f);
        TextMeshProUGUI score = CreateReadout(root.transform, "Score", font, topLeft,
            new Vector2(24f, -24f), TextAlignmentOptions.MidlineLeft, "SCORE  0\nPENDING  0");
        TextMeshProUGUI ammo = CreateReadout(root.transform, "Ammo", font, bottomRight,
            new Vector2(-24f, 24f), TextAlignmentOptions.MidlineRight, "AMMO  0 / 6\nNO ACTIVE ROUND");

        Vector2 center = new Vector2(0.5f, 0.5f);
        RectTransform crosshair = CreateRect(root.transform, "Crosshair", center, Vector2.zero, new Vector2(26f, 26f));
        Color edge = new Color32(12, 14, 18, 235);
        CreateImage(crosshair, "HorizontalOutline", center, Vector2.zero, new Vector2(26f, 6f), edge);
        CreateImage(crosshair, "VerticalOutline", center, Vector2.zero, new Vector2(6f, 26f), edge);
        var horizontal = CreateImage(crosshair, "Horizontal", center, Vector2.zero, new Vector2(22f, 2f), Color.white);
        var vertical = CreateImage(crosshair, "Vertical", center, Vector2.zero, new Vector2(2f, 22f), Color.white);

        var hud = Undo.AddComponent<LuckyShotHud>(root);
        var hudData = new SerializedObject(hud);
        hudData.FindProperty("revolver").objectReferenceValue = revolver;
        hudData.FindProperty("gameManager").objectReferenceValue = manager;
        hudData.FindProperty("ammoText").objectReferenceValue = ammo;
        hudData.FindProperty("scoreText").objectReferenceValue = score;
        SerializedProperty bars = hudData.FindProperty("crosshairBars");
        bars.arraySize = 2;
        bars.GetArrayElementAtIndex(0).objectReferenceValue = horizontal;
        bars.GetArrayElementAtIndex(1).objectReferenceValue = vertical;
        hudData.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
    }

    private static TMP_FontAsset LoadFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null && Resources.Load<TMP_Settings>("TMP Settings") != null) return font;

        TMP_PackageResourceImporter.ImportResources(importEssentials: true, importExamples: false, interactive: false);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null && Resources.Load<TMP_Settings>("TMP Settings") != null) return font;

        Debug.Log("Run Create HUD again after the TextMesh Pro Essentials import finishes.");
        return null;
    }

    private static TextMeshProUGUI CreateReadout(Transform parent, string name, TMP_FontAsset font,
        Vector2 anchor, Vector2 position, TextAlignmentOptions alignment, string preview)
    {
        var panel = CreateImage(parent, name + "Panel", anchor, position, new Vector2(268f, 76f),
            new Color32(12, 14, 18, 218));
        RectTransform rect = CreateRect(panel.transform, name, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(236f, 64f));

        var text = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
        Undo.RecordObject(text, UndoLabel);
        text.font = font;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = 24f;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = preview;
        return text;
    }

    private static UnityEngine.UI.Image CreateImage(Transform parent, string name, Vector2 anchor,
        Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = CreateRect(parent, name, anchor, position, size);
        var image = Undo.AddComponent<UnityEngine.UI.Image>(rect.gameObject);
        Undo.RecordObject(image, UndoLabel);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 anchor,
        Vector2 position, Vector2 size)
    {
        var child = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(child, UndoLabel);
        var rect = (RectTransform)child.transform;
        Undo.SetTransformParent(rect, parent, UndoLabel);
        Undo.RecordObject(rect, UndoLabel);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        return rect;
    }

    [MenuItem(MenuPath + "Start Test Round (Lucky 4)")]
    private static void StartTestRound()
    {
        GameManager manager = GetTestManager();
        if (manager != null) manager.StartRound(4);
    }

    [MenuItem(MenuPath + "End Test Round")]
    private static void EndTestRound()
    {
        GameManager manager = GetTestManager();
        if (manager != null) manager.EndRound();
    }

    [MenuItem(MenuPath + "Create HUD", true)]
    private static bool CanCreateHud() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem(MenuPath + "Start Test Round (Lucky 4)", true)]
    [MenuItem(MenuPath + "End Test Round", true)]
    private static bool CanTestRound() => EditorApplication.isPlaying;

    private static GameManager GetTestManager()
    {
        GameManager[] managers = FindInScene<GameManager>(SceneManager.GetActiveScene());
        if (managers.Length == 1) return managers[0];

        Debug.LogWarning("Testing requires one GameManager in the active scene. Run Create HUD in Edit Mode first.");
        return null;
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component
    {
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(component => component.gameObject.scene == scene)
            .ToArray();
    }
}
