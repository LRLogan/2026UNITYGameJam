using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only, installed under Assets/Editor. Operates on selected scene/Prefab Mode enemies.
public static class EnemyAnimationSetup
{
    private const string Output = "Assets/EnemyAnimations";
    private const string Clips = "Assets/Human Animations/Animations/";

    [MenuItem("Tools/Enemies/Apply Idle and Shooting to Selected")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("Exit Play mode before setting up enemy animations."); return; }
        Enemy[] enemies = Selection.gameObjects.SelectMany(go => go.GetComponentsInChildren<Enemy>(true))
            .Concat(Selection.gameObjects.Select(go => go.GetComponentInParent<Enemy>()))
            .Where(enemy => enemy != null).Distinct().ToArray();
        if (enemies.Length == 0)
        { Debug.LogWarning("Select an Enemy in the Hierarchy, or open Enemy.prefab in Prefab Mode and select its root."); return; }
        if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets", "EnemyAnimations");
        int applied = 0;
        foreach (Enemy enemy in enemies)
        {
            if (EditorUtility.IsPersistent(enemy))
            { Debug.LogWarning("Open the prefab in Prefab Mode first, then select its root.", enemy); continue; }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set Up Enemy Animation");
            try
            {
                Configure(enemy);
                EditorSceneManager.MarkSceneDirty(enemy.gameObject.scene);
                applied++;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogError($"Could not configure {enemy.name}: {exception.Message}", enemy);
            }
            finally { Undo.CollapseUndoOperations(group); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Configured {applied} enemies. Save the prefab/scene. Inspect the weapon grip in Play mode; its existing model may need a small local position/rotation adjustment.");
    }

    private static void Configure(Enemy enemy)
    {
        var enemyData = new SerializedObject(enemy);
        EnemyWeapon weapon = enemyData.FindProperty("weapon").objectReferenceValue as EnemyWeapon;
        if (weapon == null) weapon = enemy.GetComponentInChildren<EnemyWeapon>(true);
        if (weapon == null) throw new InvalidOperationException("No EnemyWeapon found.");
        Animator animator = enemyData.FindProperty("characterAnimator").objectReferenceValue as Animator;
        if (animator == null)
            animator = enemy.GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(a => a.avatar != null && a.avatar.isValid && a.avatar.isHuman);

        if (animator == null)
        {
            // Recover the original model asset from the static mesh already used by this enemy.
            MeshFilter filter = enemy.GetComponent<MeshFilter>();
            MeshRenderer oldRenderer = enemy.GetComponent<MeshRenderer>();
            string path = filter != null ? AssetDatabase.GetAssetPath(filter.sharedMesh) : "";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Animator source = model != null ? model.GetComponentInChildren<Animator>(true) : null;
            if (source == null || source.avatar == null || !source.avatar.isValid || !source.avatar.isHuman)
                throw new InvalidOperationException("The current model has no valid Humanoid Avatar. Set its FBX Rig to Humanoid, Apply, and retry. No model was substituted.");
            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(model, enemy.transform);
            Undo.RegisterCreatedObjectUndo(rig, "Restore Rigged Humanoid");
            rig.name = "Enemy Animated Model";
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;
            rig.transform.localScale = Vector3.one;
            animator = rig.GetComponentInChildren<Animator>(true);
            // Keep the enemy's chosen body materials and retain the static component for Undo.
            if (oldRenderer != null)
            {
                foreach (SkinnedMeshRenderer skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (skin.sharedMesh == filter.sharedMesh)
                    {
                        Undo.RecordObject(skin, "Preserve Body Materials");
                        skin.sharedMaterials = oldRenderer.sharedMaterials;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
                    }
                Undo.RecordObject(oldRenderer, "Hide Static Body");
                oldRenderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(oldRenderer);
            }
        }
        if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            throw new InvalidOperationException("Assigned Animator must have a valid Humanoid Avatar.");
        string names = string.Join(" ", weapon.GetComponentsInChildren<Transform>(true).Select(t => t.name)).ToLowerInvariant();
        string kind = names.Contains("pistol") || names.Contains("handgun") || names.Contains("human_gun") ? "Gun"
            : names.Contains("assault") ? "AssaultRifle" : "Rifle";
        string avatarName = animator.avatar.name;
        string gender = avatarName.IndexOf("HumanF", StringComparison.OrdinalIgnoreCase) >= 0 ? "Female" : "Male";
        string prefix = gender == "Female" ? "HumanF" : "HumanM";
        AnimationClip idle = LoadClip($"{Clips}{gender}/Combat/{kind}/{prefix}@{kind}_Aim01.fbx");
        AnimationClip shoot = LoadClip($"{Clips}{gender}/Combat/{kind}/{prefix}@{kind}_Aim01_Shoot01.fbx");
        AnimatorController controller = GetController(prefix + "_" + kind, idle, shoot);
        Undo.RecordObject(animator, "Assign Enemy Controller");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.enabled = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);

        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null) throw new InvalidOperationException("Humanoid has no mapped right hand.");
        // Do not move an already attached weapon or discard its existing grip calibration.
        if (!weapon.transform.IsChildOf(hand))
        {
            if (weapon.transform == enemy.transform || animator.transform.IsChildOf(weapon.transform))
                throw new InvalidOperationException("Weapon must be a separate child, not the enemy/rig root.");
            // Calibrate in the actual armed pose rather than against the imported T-pose.
            Undo.RegisterFullObjectHierarchyUndo(animator.gameObject, "Pose Enemy for Weapon Attachment");
            Vector3 modelPosition = animator.transform.localPosition;
            Quaternion modelRotation = animator.transform.localRotation;
            Vector3 modelScale = animator.transform.localScale;
            idle.SampleAnimation(animator.gameObject, 0f);
            animator.transform.localPosition = modelPosition;
            animator.transform.localRotation = modelRotation;
            animator.transform.localScale = modelScale;
            foreach (Transform bone in animator.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
            Quaternion relativeRotation = Quaternion.Inverse(enemy.transform.rotation) * weapon.transform.rotation;
            Undo.SetTransformParent(weapon.transform, hand, "Attach Weapon to Right Hand");
            Undo.RecordObject(weapon.transform, "Position Weapon Grip");
            weapon.transform.position = hand.position;
            weapon.transform.rotation = enemy.transform.rotation * relativeRotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(weapon.transform);
        }
        enemyData.Update();
        enemyData.FindProperty("characterAnimator").objectReferenceValue = animator;
        enemyData.FindProperty("weapon").objectReferenceValue = weapon;
        enemyData.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
    }

    private static AnimationClip LoadClip(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (clip == null || !clip.humanMotion)
            throw new InvalidOperationException("Missing Humanoid animation: " + path);
        return clip;
    }

    private static AnimatorController GetController(string key, AnimationClip idle, AnimationClip shoot)
    {
        string path = Output + "/Enemy_" + key + ".controller";
        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (existing != null) return existing;
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Armed Idle", new Vector3(240, 70));
        idleState.motion = idle;
        machine.defaultState = idleState;
        AnimatorState shooting = machine.AddState("Shoot", new Vector3(500, 70));
        shooting.motion = shoot;
        AnimatorStateTransition fire = machine.AddAnyStateTransition(shooting);
        fire.AddCondition(AnimatorConditionMode.If, 0, "Shoot");
        fire.hasExitTime = false;
        fire.hasFixedDuration = true;
        fire.duration = 0.04f;
        fire.canTransitionToSelf = true;
        AnimatorStateTransition recover = shooting.AddTransition(idleState);
        recover.hasExitTime = true;
        recover.exitTime = 0.95f;
        recover.hasFixedDuration = true;
        recover.duration = 0.08f;
        EditorUtility.SetDirty(controller);
        return controller;
    }
}
