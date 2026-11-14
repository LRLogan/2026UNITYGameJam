# One-shot forest placement

Tool: `Assets/Editor/ForestScatterWindow.cs`.

1. Exit Play mode and Prefab Mode; make your intended scene the active scene.
2. Ensure actual ground has enabled non-trigger colliders and is on the **Ground** layer. Ground mesh renderers should also be on Ground. Rocks, buildings, water, and trees should not use Ground. Other mesh renderers and solid colliders are treated as obstacles, even if their objects have no special tag. Terrain-painted trees are also considered.
3. In the Hierarchy, Ctrl-select one complete, upright tree root per desired tree type. Select trees individually, not the forest parent or a tree's separate mesh parts. Their scale must be positive. Source objects are duplicated with their components and scene configuration; use decorative trees without scene-specific gameplay references. The copies are scene clones, not guaranteed linked prefab instances.
4. Open **Tools > Forest > Scatter Trees**, then **Use Selected Trees as Sources**. Check that the displayed objects are the intended types.
5. Start with 300 new trees, 0.5 meters Extra Clearance, 0.12 Height Variation (plus/minus 12%), and 30 meters Forest Patch Size. The tool samples the bounding area of all active Ground colliders in the active scene. Increase patch size for broader clusters and clearings. Random Seed changes the arrangement.
6. Click **Scatter Into Scene (Undo Supported)**. Trees appear under a new **Scattered Forest** root. Y rotation is randomized; only height varies, leaving trunk widths unchanged. Existing trees and other scene content are preserved.
7. Inspect the result, then **save the scene**. These are normal scene objects placed in Edit mode and remain after entering and leaving Play mode. Ctrl+Z undoes the whole batch. To try a different layout, undo before rerunning. Running again without undo adds another batch while avoiding prior batches; retain their group name so Ground-layer copies remain recognized.

Placement checks the nearest solid surface, slope, eight footprint samples, mesh-renderer bounds and collider bounds. Conservative full-canopy bounds avoid tree overlap but may leave extra space near irregular rocks, large combined meshes, and overhangs. Fewer trees than requested is expected when there are insufficient safe positions. Cancel keeps the successfully placed portion as one undoable batch. Exceptions roll back the batch.

Correct layer assignments matter: a rock labeled Ground can be mistaken for terrain. Bounds are conservative rather than exact mesh intersections; inspect uneven terrain for floating trunk bases. This tool does not bake navigation or optimize/batch trees for WebGL. Start with a modest count and profile the build before adding thousands.

Validation: installation and source structure checked. Editor compilation, actual placement, Undo/Redo and Play-mode persistence still need verification in Unity; no Editor access was used and no scene YAML was changed.
