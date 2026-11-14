using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ObjectiveVisualSetup
{
    [MenuItem("Tools/Objectives/Apply Sword and Tall Beacons to Open Scenes")]
    private static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject sword = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LowPolyMedievalStarterPack/Prefabs/MedievalSword.prefab");
        Shader shader = Shader.Find("GameJam/Objective Beacon");
        if (sword == null || shader == null)
        { Debug.LogError("Missing MedievalSword prefab or Objective Beacon shader. Wait for import and retry."); return; }
        const string folder = "Assets/ObjectiveVisuals";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "ObjectiveVisuals");
        const string path = folder + "/ObjectiveBeacon.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure Objective Visuals");
        int count = 0;
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (LevelManager manager in root.GetComponentsInChildren<LevelManager>(true))
                {
                    var data = new SerializedObject(manager);
                    data.FindProperty("objectiveSwordPrefab").objectReferenceValue = sword;
                    data.FindProperty("beaconMaterial").objectReferenceValue = material;
                    data.FindProperty("beaconHeight").floatValue = 150f;
                    data.FindProperty("beaconWidth").floatValue = 3f;
                    data.ApplyModifiedProperties();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
                    EditorSceneManager.MarkSceneDirty(scene);
                    count++;
                }
        }
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        Debug.Log($"Configured {count} LevelManagers with the sword and 150m beacon. Save the scenes, then start a round. Open other gameplay scenes and rerun to configure them too.");
    }
}
