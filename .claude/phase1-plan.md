# Phase 1 — 3D Rendering Foundation

Goal: spinning cube in HelloCube with depth-correct rendering and 2D UI overlay.

## Sub-phases

### 1A: Depth Buffer (NoZ fork)

Create a depth texture and attach it to render passes. No behavioral change to existing 2D rendering.

**Files:**
- `engine/noz/platform/webgpu/WebGPUGraphicsDriver.cs` — add `_depthTexture`, `_depthTextureView` fields; create Depth32Float texture in `CreateSwapChain()`; destroy on shutdown and resize
- `engine/noz/platform/webgpu/WebGPUGraphicsDriver.RenderPass.cs` — add `RenderPassDepthStencilAttachment` to `BeginScenePass()` (LoadOp.Clear, depth=1.0) and `ResumeScenePass()` (LoadOp.Load)

**MSAA:** depth texture must match `SampleCount` from the color attachment. NoZ already tracks this in `CachedState.CurrentPassSampleCount`.

**Validation:** existing HelloCube renders identically (depth buffer attached but no shader uses it).

---

### 1B: Pipeline Depth via ShaderFlags (NoZ fork)

Wire `ShaderFlags.Depth` / `DepthLess` through to pipeline creation. When a shader has these flags, the render pipeline gets depth testing and back-face culling.

**Files:**
- `engine/noz/engine/src/graphics/Shader.cs` — pass `Flags` through to the driver (new `CreateShader` overload or additional parameter)
- `engine/noz/engine/src/platform/IGraphicsDriver.cs` — update `CreateShader` signature to accept `ShaderFlags`
- `engine/noz/platform/webgpu/WebGPUGraphicsDriver.Shaders.cs`:
  - Store flags per shader
  - In `CreateRenderPipeline()`: when Depth flag set → add `DepthStencilState` (format=Depth32Float, compare=Less, depthWriteEnabled=true)
  - When Depth flag set → set `CullMode = Back` in primitive state (currently `None`)
  - Extend `PsoKey` with depth flag for pipeline cache correctness

**Design decision:** depth is shader-driven, not runtime state. No `SetDepthTest()` / `SetCullMode()` methods on `IGraphicsDriver`. This keeps the fork minimal and matches NoZ's pattern (blend mode is also per-pipeline). Runtime depth control can be added later if needed.

**Validation:** existing 2D shaders (sprite, text, UI) have no Depth flag → behavior unchanged.

---

### 1C: Projection API (NoZ fork)

Expose a public way to set the current pass projection matrix so Graphics3D can swap orthographic → perspective mid-frame.

**Files:**
- `engine/noz/engine/src/graphics/Graphics.cs` (or `Graphics.State.cs`) — add `public static void SetPassProjection(in Matrix4x4 projection)` that updates `_passProjections[(int)_currentPass]` and marks batch state dirty

**Why fork:** `_passProjections` is private with no public setter. The ortho projection is set internally during `BeginFrame()`. Graphics3D needs to override it for perspective, then restore it in `End()`.

**Conflict risk:** Low — additive public method on a static class.

---

### 1D: Vertex3D + Mesh3D (YesZ)

Define the 3D vertex format and mesh wrapper. Pure data structures, no GPU dependency in tests.

**Files:**
- `src/YesZ.Rendering/Vertex3D.cs`:
  ```csharp
  [StructLayout(LayoutKind.Sequential)]
  public struct Vertex3D : IVertex
  {
      public Vector3 Position;   // @location(0) vec3<f32>
      public Vector3 Normal;     // @location(1) vec3<f32>
      public Vector2 UV;         // @location(2) vec2<f32>
      public Color Color;        // @location(3) vec4<f32>
  }
  ```
- `src/YesZ.Rendering/Mesh3D.cs` — wraps `IGraphicsDriver.CreateMesh<Vertex3D>()`, holds vertex/index data, provides `Upload()` and `Dispose()`
- `tests/YesZ.Rendering.Tests/Vertex3DTests.cs` — verify format descriptor (stride, attribute count, offsets)

---

### 1E: 3D Shader (YesZ)

Write a WGSL shader for 3D rendering. Loaded at runtime, not through NoZ's asset pipeline.

**Files:**
- `src/YesZ.Rendering/Shaders/basic3d.wgsl` (embedded resource):
  ```wgsl
  // Vertex: transform position by ViewProjection from globals
  // Fragment: output vertex color (flat shading for Phase 1)
  ```
- `src/YesZ.Rendering/Shader3D.cs` — loads WGSL source from embedded resource, calls `Graphics.Driver.CreateShader()` with ShaderFlags.Depth | ShaderFlags.DepthLess

**Why runtime loading:** NoZ shaders are binary assets compiled by the NoZ Editor. YesZ doesn't use the editor pipeline. We call `IGraphicsDriver.CreateShader()` directly with WGSL source strings.

**Shader must declare** the same globals layout as NoZ (projection matrix + time) so it works with the existing `GlobalsSnapshot` uniform buffer.

---

### 1F: Graphics3D Implementation (YesZ)

Wire everything together. Graphics3D.Begin/End manages state transitions between 2D and 3D rendering.

**Files:**
- `src/YesZ.Rendering/Graphics3D.cs` — replace stubs:
  ```
  Begin(camera):
    1. Graphics.PushState()
    2. Graphics.SetShader(3d_shader)
    3. Save current projection, set perspective via Graphics.SetPassProjection()
    4. Set 3D mesh via Graphics.SetMesh()

  End():
    1. Restore saved projection via Graphics.SetPassProjection()
    2. Graphics.PopState()

  DrawMesh(mesh3d, transform):
    1. Upload mesh if dirty
    2. Compute MVP = transform.LocalMatrix * camera.ViewProjectionMatrix
    3. Submit draw via Graphics.DrawElements()
  ```

**Sort layer:** 3D content should use the default sort layer (0). NoZ UI uses a high sort layer (UIConfig.UILayer = 1000). This means 3D draws automatically sort before UI with no special handling.

**Draw flow:** 3D draws go through NoZ's existing deferred batching. Graphics3D calls `Graphics.SetShader()`, `Graphics.SetMesh()`, `Graphics.DrawElements()` — same as 2D, just with a different shader and vertex format.

---

### 1G: Spinning Cube (YesZ sample)

Prove it works end-to-end.

**Files:**
- `src/YesZ.Rendering/MeshBuilder3D.cs` (or inline in sample) — helper to build cube mesh (24 vertices with per-face normals, 36 indices)
- `samples/HelloCube/HelloCubeApp.cs` — update:
  ```
  LoadAssets(): create camera, create cube mesh, init Graphics3D shader
  Update(): rotate cube, Graphics3D.Begin(camera) → DrawMesh(cube) → End()
  UpdateUI(): keep existing "YesZ" label overlay
  ```

**Validation:** cube renders with correct depth (back faces hidden), rotates smoothly, 2D UI overlays on top.

---

## Dependency Graph

```
1A (depth texture) ──┐
                     ├── 1B (pipeline depth) ── 1C (projection API) ── 1F (Graphics3D) ── 1G (cube)
                     │                                                       ↑
1D (Vertex3D/Mesh3D) ────────────────────────────────────────────────────────┘
1E (3D shader) ──────────────────────────────────────────────────────────────┘
```

1D and 1E can be done in parallel with the fork work (1A–1C).

---

## Fork Changes Summary

All fork changes ship as one commit to the noz fork branch.

| File | Change | Conflict Risk |
|------|--------|---------------|
| `engine/noz/engine/src/platform/IGraphicsDriver.cs` | Update `CreateShader` to accept `ShaderFlags` | Low — signature change |
| `engine/noz/engine/src/graphics/Shader.cs` | Pass `Flags` to `Graphics.Driver.CreateShader()` | Low |
| `engine/noz/engine/src/graphics/Graphics.cs` | Add `SetPassProjection(in Matrix4x4)` | Low — additive |
| `engine/noz/platform/webgpu/WebGPUGraphicsDriver.cs` | Depth texture lifecycle | Medium |
| `engine/noz/platform/webgpu/WebGPUGraphicsDriver.Shaders.cs` | DepthStencilState + CullMode in pipeline, PsoKey extension | Medium |
| `engine/noz/platform/webgpu/WebGPUGraphicsDriver.RenderPass.cs` | Depth attachment on scene passes | Medium |

**Not needed (revised from Phase 0 plan):** `SetDepthTest()`, `SetDepthWrite()`, `SetCullMode()`, `ClearDepth()`, `CullMode` enum on `IGraphicsDriver`. Depth is shader-driven instead.

---

## Open Questions

1. **Depth clear value:** WebGPU uses 1.0 = far. Confirm NoZ doesn't have an inverted-Z convention anywhere.
2. **Render texture depth:** Should `BeginRenderTexturePass` also get a depth attachment? Not needed for Phase 1 (no off-screen 3D), but worth considering for the fork change scope.
3. **Shader globals layout:** Need to verify the 3D shader's uniform block matches NoZ's `GlobalsSnapshot` exactly (projection as column-major Matrix4x4 at offset 0, time at offset 64).

## Gotchas

- Depth texture must be recreated on window resize (same path as swap chain recreation)
- MSAA depth texture sample count must match color attachment
- NoZ's sprite shader outputs `Z = 0.0` — 2D content always passes depth test against cleared depth (1.0), so no conflict with 3D
- `PsoKey` cache must include depth flag or pipelines with different depth states will collide
- The 3D shader must output clip-space Z (not hardcoded 0.0) for depth testing to work
