using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MainMenuSetup
{
    [MenuItem("Tools/Main Menu/Set Up Jackpot Shot Menu")]
    private static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string scenePath = "Assets/Scenes/MainMenu.unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Scripts/IntroShot.png");
        Shader shader = Shader.Find("GameJam/Main Menu Frosted Panel");
        if (background == null || shader == null)
        { Debug.LogError("Wait for the menu background and shader to import, then retry."); return; }
        const string materialPath = "Assets/Scripts/MainMenuPanel.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        MainMenuUI menu = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MainMenuUI>(true)).FirstOrDefault();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set Up Main Menu");
        if (menu == null)
        {
            GameObject root = new GameObject("Jackpot Shot Menu");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create Menu");
            menu = Undo.AddComponent<MainMenuUI>(root);
        }
        var data = new SerializedObject(menu);
        data.FindProperty("background").objectReferenceValue = background;
        data.FindProperty("blurPanelMaterial").objectReferenceValue = material;
        data.FindProperty("gameplayScene").stringValue = "Level-1";
        data.ApplyModifiedProperties();
        Undo.CollapseUndoOperations(group);
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != scenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        const string levelPath = "Assets/Scenes/Level-1.unity";
        var level = scenes.FirstOrDefault(s => s.path == levelPath);
        if (level != null) level.enabled = true;
        else scenes.Add(new EditorBuildSettingsScene(levelPath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = menu.gameObject;
        Debug.Log("Main menu configured. Save MainMenu, open it alone, and enter Play mode to preview. MainMenu is first in the shared build scene list; if your Build Profile overrides the scene list, put it first there too. UI is generated when the scene starts.");
    }
}
