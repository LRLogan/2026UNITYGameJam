# Cartoon water for URP / WebGL

Shader: `GameJam/Cartoon Water WebGL`.

## Setup in Unity

1. Wait for Unity to import `CartoonWaterWebGL.shader`.
2. In the Project window, right-click and choose **Create > Material**. Name it `CartoonWater`.
3. Select that material and choose **GameJam > Cartoon Water WebGL** from its Shader dropdown.
4. Create a **GameObject > 3D Object > Plane**, or use an existing horizontal water mesh. Position it at your water level, with its front face pointing upward. Drag the material onto it.
5. Enter Play mode to see the moving pattern. In Scene view, animated material preview also requires scene effects/animated materials to be enabled.
6. Tune Deep Water and Light Water colors, Wave Scale, Animation Speed, Fleck Coverage, Fleck Size, and Color Patch Scale. Set Fleck Coverage to zero to remove white flecks. Pattern size uses world units, so scaling the plane increases coverage without stretching the pattern.
7. Save the scene and include it in your Web build's scene list. Keep the material referenced by the scene renderer so the shader is included in the build.
8. Build and Run the WebGL player. Check the Unity Console for shader errors and inspect the result in your target browser, especially at shallow camera angles and on mobile hardware.

## Design and limits

- Single opaque unlit pass, shader target 3.0, no textures, compute shaders, depth texture, opaque texture, renderer features, or runtime scripts. Matches this project's URP setup, including the Mobile quality preset selected for WebGL.
- Soft noise-driven color patches, simulated wave normals, sparse irregular white flecks in two drifting layers, viewing-angle highlights, and URP fog. Randomized fleck placement, orientation, size, and occupancy break up regular spacing. The geometry stays flat, so ordinary plane meshes work without subdivision or expanded renderer bounds.
- Intended for horizontal XZ water viewed from above. Colors are artistic bands, not measurements of water depth. This does not provide transparency, shoreline/intersection foam, reflections, underwater rendering, buoyancy, or cast/received shadows.
- The existing WebGPU water package is separate; this shader does not require or modify it. Apply this material to a regular MeshRenderer for independent use.
- Source reviewed only: Unity shader import, visual appearance, and an actual WebGL browser build must still be checked in the Editor. No performance measurement or platform compile has been performed here.

## Reference-style settings / upgrading an existing material

Existing material colors are preserved by Unity when the shader changes. For the lighter blue reference style, set Deep Water to hex `3385BF`, Light Water to `52BFCC`, and Fleck Color to `E0F5F2` in the material Inspector. Start with Fleck Coverage `0.12`, Fleck Size `0.85`, Wave Scale `1.2`, and Color Patch Scale `0.18`. Lower Color Patch Scale for larger soft patches; lower Animation Speed for calmer water. The old Ripple Coverage property is retained internally and is now labeled Fleck Coverage.

The reference's white outlines around land and rocks are shoreline/intersection foam. This shader revision matches the surface patches and flecks only; it does not generate those outlines. It still requires no camera depth texture. The noise and two fleck layers cost more fragment arithmetic than the original sine ribbons; check the actual browser frame rate on target hardware.
