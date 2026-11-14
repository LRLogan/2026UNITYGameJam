# Enemy idle and shooting setup

## Apply once in Unity

1. Exit Play mode. Open `Assets/Prefabs/Enemy.prefab` in Prefab Mode and select its Enemy root in the Hierarchy. This updates the shared prefab used by future spawns. Alternatively, select specific scene enemies to create instance-only overrides.
2. Run **Tools > Enemies > Apply Idle and Shooting to Selected**.
3. Save the prefab/scene. Enter Play mode: an enemy should hold a looping armed idle, fire when its existing weapon cooldown/range allows, and return to idle after recoil. Leave detection range and verify that firing stops.
4. Check weapon placement. The tool anchors a previously unattached weapon at the right hand in the armed idle pose, preserving its forward orientation. Different weapon meshes have different grip pivots: adjust the EnemyWeapon child's local position/rotation if the grip or support hand does not line up. Copy any adjustments made during Play mode back into Edit mode before saving. Rerunning setup preserves weapons already attached under the right hand.

## What changes

- `Enemy.cs` references the character Animator and sends its Shoot trigger only when `EnemyWeapon.TryShoot` actually returns true. Existing damage, aiming, cooldown and death behavior remain authoritative. Root motion is disabled.
- `Assets/Editor/EnemyAnimationSetup.cs` uses Unity APIs to create a controller under `Assets/EnemyAnimations`, assign the existing Humanoid Avatar, and connect armed idle/shoot clips from the imported Human Animations pack. Rifle is the default for shotguns/rifles; pistol/handgun and assault-rifle names select their matching clips. Humanoid avatars named HumanF select female clips; others use male clips.
- The saved Enemy prefab had only a static MeshRenderer for its humanoid. The command restores the complete rig from that mesh's own source model, keeps matching body materials, and disables the static renderer without deleting it. Existing valid humanoid Animators are reused.
- Scene/prefab edits support Undo. Generated controller assets remain available if scene changes are undone. Existing generated controllers are reused, preserving subsequent manual edits.

If the tool reports an invalid Avatar, select the model FBX and set **Rig > Animation Type: Humanoid**, configure its Avatar if needed, Apply, and rerun. Custom controller states can be edited in the Animator window; the runtime expects a Trigger parameter named `Shoot`.

## Validation

Runtime sources and the new Editor tool compiled using Unity 6000.3.19f1's Roslyn compiler and the project's generated assembly references. The runtime compile reported only an existing CS0414 unused-field warning for PlayerCore.groundDeceleration. No scene or prefab YAML was manually edited. Actual asset generation, Humanoid retargeting, weapon grip, Undo, and Play-mode visuals remain to be checked in Unity; the Editor was not operated by the assistant.
