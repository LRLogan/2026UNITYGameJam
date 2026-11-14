# Jackpot visuals

## Connect the imported lightning once

Open `Assets/Prefabs/Player.prefab` in Prefab Mode, select the player root, and run **Tools > Jackpot > Connect Zap to Selected Player**. Save the prefab. The command also supports selected scene players with Undo. Alternatively assign the imported `VFX_Zap_02_Blue` prefab to Revolver's Lightning Impact Prefab field.

Revolver automatically attaches the visual subscriber. Explosion particles and lightning arcs need no Inspector setup. Without the optional Zap reference, chain arcs still render. Both custom shaders live in Resources so builds retain them.

## Behavior and backend review

- Jackpot hits still heal the player, kill the direct target, award points, and randomly choose explosion or lightning. Misses keep their existing score penalty and spawn no jackpot impact.
- Explosion overlap queries deduplicate enemies with multiple colliders. The visual ring reads the actual configured explosion radius (currently 4m). Orange particles and a fading ring are visual only and do not apply damage again.
- Lightning selects distinct live enemies, at most three hops by default. The visual path copies the resolved impact/hop positions, starting at the muzzle. Imported Zap particles spawn at every strike. Collateral player damage remains capped to once per effect.
- Existing area-effect behavior is preserved: explosion and lightning range checks do not test line of sight through walls.
- Firing, reloading and spinning are now guarded while time is paused. Visual lifetimes use scaled time. Cosmetic line jitter uses its own random generator. Active bursts are bounded and destroyed on completion, component disable, or scene unload.

## WebGL and validation

Zap uses CPU ParticleSystems, not VFX Graph. Both its lightning shader and secondary flare/scorch shader were changed from target 4.5 to 3.5. Regenerating them in Amplify Shader Editor or updating the pack may overwrite this adjustment. Real-time lights are disabled on spawned Zap effects, and particle counts are capped. Explosion uses a texture-free URP target-3.0 shader and 32 CPU particles.

Editor and WebGL-defined C# compile checks passed; the setup tool also compiled. Actual shader compilation, retargeted imported effects, appearance, and browser performance are not yet verified. Test jackpot hits on a group, an isolated target, near the player, and near a wall; verify misses, pause/resume, repeated shots and scene reloads. Build WebGL and check both effect variants before considering compatibility confirmed.
