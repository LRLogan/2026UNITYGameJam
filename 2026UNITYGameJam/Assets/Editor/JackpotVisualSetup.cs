using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class JackpotVisualSetup
{
    [MenuItem("Tools/Jackpot/Connect Zap to Selected Player")]
    private static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject zap = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Zap VFX URP/VFX/Zap/Particles/VFX_Zap_02_Blue.prefab");
        if (zap == null) { Debug.LogError("Cannot find the imported blue Zap prefab."); return; }
        var weapons = Selection.gameObjects.SelectMany(go => go.GetComponentsInChildren<Revolver>(true))
            .Concat(Selection.gameObjects.Select(go => go.GetComponentInParent<Revolver>()))
            .Where(w => w != null).Distinct().ToArray();
        int count = 0;
        foreach (Revolver weapon in weapons)
        {
            if (EditorUtility.IsPersistent(weapon)) continue;
            var data = new SerializedObject(weapon);
            data.FindProperty("lightningImpactPrefab").objectReferenceValue = zap;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(weapon);
            EditorSceneManager.MarkSceneDirty(weapon.gameObject.scene);
            count++;
        }
        Debug.Log(count > 0 ? $"Connected Zap to {count} revolvers. Save the prefab/scene. Explosion and chain arcs are connected automatically."
            : "Open Player.prefab in Prefab Mode, select its root, then run this command again.");
    }
}
