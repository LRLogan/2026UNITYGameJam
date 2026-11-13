using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BoxophobicSkyboxSetup
{
    private const string ScenePath = "Assets/Scenes/Level-1.unity";
    private const string Materials = "Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Materials/";

    [InitializeOnLoadMethod]
    private static void ApplyOnceAfterImport()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += () =>
        {
            string key = Application.dataPath + "/BoxophobicSkyboxSetup/Day-20261112";
            if (EditorPrefs.GetBool(key, false)) return;
            // Consume the one-time request even if the user has switched scenes or entered Play.
            EditorPrefs.SetBool(key, true);
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath) return;
            Apply("Day", !scene.isDirty);
        };
    }

    [MenuItem("Tools/Skybox/Level-1/Use Day")]
    public static void UseDay() => Apply("Day", false);

    [MenuItem("Tools/Skybox/Level-1/Use Night")]
    public static void UseNight() => Apply("Night", false);

    // Explicit batch entry point, never invoked automatically on import or Play.
    public static void ApplyDayAndSave() => Apply("Day", true);

    private static void Apply(string variant, bool save)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before changing the saved scene skybox.");
        string path = Materials + "Skybox Cubemap Extended " + variant + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null || material.shader == null)
            throw new InvalidOperationException("Cannot load skybox material: " + path);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new InvalidOperationException("Missing scene: " + ScenePath);

        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            // RenderSettings belongs to the active scene, not the project settings
            // singleton registry. Unity 6000.3 exposes its instance accessor internally.
            MethodInfo getter = typeof(RenderSettings).GetMethod("GetRenderSettings",
                BindingFlags.Static | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (getter == null)
                throw new InvalidOperationException("This Unity version does not expose the RenderSettings Undo accessor.");
            var settings = getter.Invoke(null, null) as UnityEngine.Object;
            if (settings == null) throw new InvalidOperationException("The active scene has no RenderSettings object for Undo.");
            Undo.RecordObject(settings, "Set BOXOPHOBIC skybox");
            RenderSettings.skybox = material;
            EditorUtility.SetDirty(settings);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!Application.isBatchMode) DynamicGI.UpdateEnvironment();
            if (save && !EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Failed to save Level-1.");
            Debug.Log("BOXOPHOBIC_SKYBOX_APPLIED: " + ScenePath + " -> " + path + "; saved=" + save);
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }
}
