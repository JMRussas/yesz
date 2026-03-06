# YesZ Roadmap

## Phase Overview

| Phase | Name | Milestone | Depends on | Status |
|-------|------|-----------|------------|--------|
| 0 | Scaffold | Project builds, HelloCube opens window with 2D UI | — | Done |
| 1a | Fork: Depth Pipeline | Existing 2D rendering works with depth texture attached | 0 | Done (already in fork) |
| 1b | First 3D Render | HelloCube renders a spinning colored cube with depth-correct faces | 1a | Done |
| 2 | Materials & Texturing | HelloCube renders a textured cube | 1b | Done |
| 3a | Light Infrastructure | Light types compile, tests pass, API callable | 0 | Done |
| 3b | Lit Shading | HelloCube shows a lit cube with directional light shading | 2, 3a | Done |
| 3c | Multi-Light + Point Lights | HelloCube with directional + point light(s) | 3b | Done |
| 4a | glTF Parser | Unit tests parse a .glb file, produce correct Mesh3D data | — | Done |
| 4b | Model Rendering | HelloCube loads and displays a textured glTF model | 4a, 2 | Done |
| 4c | Multi-Mesh + Hierarchy | Complex multi-part glTF model renders correctly | 4b | Done |
| 5a | Skeleton & Animation Data | Unit tests parse skinned .glb, produce correct skeleton + keyframes | 4a | Done |
| 5b | Skinned Vertex Format | Skinned vertex type + shader variant compile and register | 1b, 5a | **Done** |
| 5c | Animation Playback | AnimationPlayer3D produces correct joint transforms at any time | 5a | **Done** |
| 5d | GPU Skinning | Sample app loads and displays an animated skinned glTF model | 5b, 5c, 2 | **Done** |
| 6a | Shadow Map Infrastructure | Depth-only render pass produces valid shadow texture | 3c, 1a | **Done** |
| 6b | Directional Shadows | Scene renders with directional light shadows + PCF | 6a | **Done** |
| 6c | Cascaded Shadow Maps | Large scenes show quality shadows at multiple distances | 6b | Not started |
| 7a | Render-to-Texture | Scene renders to offscreen texture, blits to screen | 2 | Not started |
| 7b | Tone Mapping + Gamma | HDR scene correctly tone-mapped to LDR display | 7a | Not started |
| 7c | Bloom + Effects | Bright areas glow, additional post-process effects work | 7b | Not started |
| 8a | Scene Graph | Nodes with transforms, add/remove/query, component data | 4c | Not started |
| 8b | Frustum Culling | Only visible objects submitted for rendering | 8a, 1b | Not started |
| 8c | Spatial Partitioning | Large scenes cull efficiently via octree/BVH | 8b | Not started |
| 9 | Instanced Rendering | Many copies of same mesh drawn efficiently | 1b | Not started |
| 10a | Bounding Volumes + Raycasting | AABB/sphere/OBB intersection + ray tests pass | 0 | Not started |
| 10b | Physics Integration | Rigid body dynamics via BepuPhysics or similar | 10a, 8a | Not started |

---

## Dependency Graph

**Critical paths** (longest chains of sequential work):

```
Core rendering:  0 → 1a → 1b → 2 → 3b → 3c
Shadows:         3c → 6a → 6b → 6c
Post-processing: 2 → 7a → 7b → 7c
Model loading:   4a → 4b → 4c
Scene mgmt:      4c → 8a → 8b → 8c
Skeletal anim:   4a → 5a → 5b → 5d   (5b also needs 1b; 5d also needs 5c, 2)
Physics:         10a → 10b   (10b also needs 8a)
```

**Longest end-to-end path:** `0 → 1a → 1b → 2 → 3b → 3c → 6a → 6b → 6c` (9 phases)

**Parallel opportunities:**

| Phase | Can start as early as | Runs in parallel with |
|-------|----------------------|-----------------------|
| 3a | After Phase 0 | 1a, 1b, 2 |
| 4a | Any time (no dependencies) | Everything |
| 5a | After 4a | 1a–3c, 4b, 4c |
| 5b, 5c | After 5a (and each other) | 5b ‖ 5c |
| 7a | After Phase 2 | 3a–3c, 4a–4c, 5a–5d |
| 9 | After Phase 1b | Most other phases |
| 10a | After Phase 0 | Everything |

**Fork changes by phase** (merge conflict risk):

| Phase | Fork change | File(s) |
|-------|------------|---------|
| 1a | Depth pipeline wiring | `WebGPUGraphicsDriver.{Shaders,RenderPass,Resources,cs}` |
| 1b | Projection injection (TBD) | Possibly `Graphics.cs` |
| 3b | `ShaderFlags.Lit` | `Shader.cs` |
| 5b | `ShaderFlags.Skinned` | `Shader.cs` |
| 6a | Depth-only render pass | `WebGPUGraphicsDriver.RenderPass.cs` |
| 7a | Render-to-texture blit | Possibly `WebGPUGraphicsDriver.RenderPass.cs` |

---

## Testing Strategy

### Philosophy

Test as you go — every phase should leave the test suite green and growing. The split is:

| Category | What's tested | How | When |
|----------|--------------|-----|------|
| **Unit tests** | Pure math, data types, struct layouts, parsers | xUnit, no GPU | Every phase that adds logic |
| **Regression tests** | Existing functionality still works | `dotnet test yesz.slnx` | Every phase, before and after |
| **Visual checks** | Rendering correctness, shading, depth | Run HelloCube, inspect visually | Phases with GPU output |
| **Diagnostic overlay** | Camera position, draw call count, depth buffer status | HelloCube HUD text | Phase 1b onward |

### Parallel test tracks

Phases that are 100% unit-testable can be developed test-first, in parallel with the GPU rendering pipeline:

```
GPU pipeline:     1a ──→ 1b ──→ 2 ──→ 3b ──→ 3c
                   ↕       ↕      ↕
Test-first work:  3a      4a    10a    5a, 5c
                  [lights] [glTF] [bounds] [skeleton, anim]
```

### Test naming convention

```
{ClassName}Tests.{MethodUnderTest}_{Scenario}_{Expected}
```

Examples:
- `MeshVertex3DTests.GetFormatDescriptor_Default_Returns48ByteStride`
- `Camera3DTests.ViewProjection_OriginLookingDownZ_MapsToClipCenter`
- `GlbReaderTests.Parse_ValidGlb_ExtractsJsonAndBinChunks`

### Winding order validation helper

For any phase that produces mesh geometry (`Mesh3DBuilder.CreateCube`, glTF extraction), validate triangle winding:

```csharp
static bool AllTrianglesWindCorrectly(MeshVertex3D[] vertices, ushort[] indices, Vector3[] expectedFaceNormals)
{
    for (int i = 0; i < indices.Length; i += 3)
    {
        var v0 = vertices[indices[i]].Position;
        var v1 = vertices[indices[i + 1]].Position;
        var v2 = vertices[indices[i + 2]].Position;
        var cross = Vector3.Cross(v1 - v0, v2 - v0);
        var faceNormal = expectedFaceNormals[i / 6]; // 2 triangles per quad face
        if (Vector3.Dot(cross, faceNormal) <= 0) return false;
    }
    return true;
}
```

This catches flipped faces that back-face culling would eat.

---

## Phase 0: Scaffold (Done)

Delivered: project structure, NoZ submodule, Camera3D, Transform3D, DesktopBootstrap, HelloCube (2D UI only), CI pipeline, docs.

- 4 source files, 9 tests (all passing)
- Graphics3D.Begin/End stubbed
- No fork changes

**Dependencies:** None
**Fork changes:** None
**Enables:** 1a, 3a, 4a (all can start after scaffold)

---

## Phase 1a: Fork — Depth Pipeline

**Milestone:** Existing 2D rendering still works with depth buffer infrastructure in place.

**Dependencies:** Phase 0
**Fork changes:** WebGPU driver internals (depth texture, pipeline state, render pass)
**Enables:** Phase 1b

### What changes

All changes are internal to the WebGPU driver. No `IGraphicsDriver` interface modifications.

| File | Change |
|------|--------|
| `WebGPUGraphicsDriver.Shaders.cs` | Add `ShaderFlags` to `ShaderInfo` struct |
| `WebGPUGraphicsDriver.Shaders.cs` | Add depth mode to `PsoKey` for pipeline variation |
| `WebGPUGraphicsDriver.Shaders.cs` | Build `DepthStencilState` from flags in `CreateRenderPipeline()` (replaces `DepthStencil = null`) |
| `WebGPUGraphicsDriver.cs` | Create depth texture in `CreateSwapChain()` matching surface dimensions + MSAA |
| `WebGPUGraphicsDriver.RenderPass.cs` | Wire depth attachment into `BeginScenePass()` and `ResumeScenePass()` |
| `WebGPUGraphicsDriver.Resources.cs` | Add depth texture to `RenderTextureInfo`; create in `CreateRenderTexture()`, destroy in `DestroyRenderTexture()` |

### Why no IGraphicsDriver changes

Phase 0 review planned `SetDepthTest()`, `SetCullMode()`, etc. on the interface. WebGPU backend analysis revealed these are unnecessary:

- **Depth test/write** — controlled by `DepthStencilState` in the render pipeline, determined at shader creation time via `ShaderFlags.Depth` / `ShaderFlags.DepthLess`
- **Cull mode** — controlled by `PrimitiveState.CullMode` in the render pipeline, also per-shader
- **Depth clear** — happens automatically via `LoadOp.Clear` on the depth attachment when the render pass begins

All three are pipeline state, not per-draw toggles. This means 3D shaders opt in to depth via their flags, and 2D shaders remain unaffected.

### Tests (unit)

No new YesZ unit tests — all changes are internal to the NoZ fork. Verification is regression-only.

| Test | What it proves |
|------|---------------|
| All 9 existing tests pass | Fork changes don't break YesZ's public API surface |

### Verification

- `dotnet build yesz.slnx` passes
- `dotnet test yesz.slnx` — all 9 existing tests pass (regression)
- HelloCube opens and renders 2D UI identically to Phase 0 (visual regression check)

---

## Phase 1b: First 3D Render

**Milestone:** HelloCube renders a spinning colored cube with depth-correct faces.

**Dependencies:** Phase 1a (depth pipeline — depth test must work for face occlusion)
**Fork changes:** `Graphics.cs` — expose `GetOrAddGlobals` as `internal` (currently `private static`)
**Enables:** Phase 2 (materials build on shader + vertex format), Phase 5b (skinned vertex format extends MeshVertex3D), Phase 8b (Camera3D provides frustum), Phase 9 (instancing extends DrawMesh)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `MeshVertex3D` | YesZ.Core | Vertex struct implementing `IVertex`: `Vector3 Position`, `Vector3 Normal`, `Vector2 UV`, `Color Color` |
| `Mesh3D` | YesZ.Core | Immutable container: `MeshVertex3D[]` vertices + `ushort[]` indices + driver mesh handle |
| `Camera3D` | YesZ.Core | Perspective projection: FOV, aspect ratio, near/far planes → `Matrix4x4` view-projection |
| Unlit 3D shader | YesZ.Rendering | WGSL vertex+fragment shader with `ShaderFlags.Depth \| ShaderFlags.DepthLess`, loaded as embedded resource |
| `Graphics3D.Begin(Camera3D)` | YesZ.Rendering | Injects perspective projection into NoZ globals via `GetOrAddGlobals()` |
| `Graphics3D.End()` | YesZ.Rendering | Restores 2D orthographic projection |
| `Graphics3D.DrawMesh(Mesh3D, Matrix4x4)` | YesZ.Rendering | Sets model matrix uniform, binds mesh, calls `Graphics.DrawElements()` |
| Procedural cube builder | YesZ.Core | `Mesh3DBuilder.CreateCube()` — hardcoded 24 vertices (4 per face for flat normals) + 36 indices |
| Spinning logic | HelloCube | Rotates cube via `Transform3D` each frame, passes world matrix to `DrawMesh` |

### MeshVertex3D vertex format

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct MeshVertex3D : IVertex
{
    public Vector3 Position;    // location 0 — 3× float32, offset 0
    public Vector3 Normal;      // location 1 — 3× float32, offset 12
    public Vector2 UV;          // location 2 — 2× float32, offset 24
    public Color   Color;       // location 3 — 4× float32, offset 32

    public static readonly int SizeInBytes = Marshal.SizeOf<MeshVertex3D>();  // 48 bytes
    public static readonly uint VertexHash = VertexFormatHash.Compute(GetFormatDescriptor().Attributes);

    public static VertexFormatDescriptor GetFormatDescriptor() => new()
    {
        Stride = SizeInBytes,
        Attributes =
        [
            new VertexAttribute(0, 3, VertexAttribType.Float, (int)Marshal.OffsetOf<MeshVertex3D>(nameof(Position))),
            new VertexAttribute(1, 3, VertexAttribType.Float, (int)Marshal.OffsetOf<MeshVertex3D>(nameof(Normal))),
            new VertexAttribute(2, 2, VertexAttribType.Float, (int)Marshal.OffsetOf<MeshVertex3D>(nameof(UV))),
            new VertexAttribute(3, 4, VertexAttribType.Float, (int)Marshal.OffsetOf<MeshVertex3D>(nameof(Color))),
        ]
    };
}
```

NoZ's `MeshVertex` uses 10 attributes (72 bytes, locations 0–9). `MeshVertex3D` uses 4 attributes (48 bytes, locations 0–3). Because `VertexFormatHash` is computed from `(location, components, type)` per attribute, these hash to different values — NoZ's pipeline cache naturally creates separate pipelines for 2D and 3D vertex formats.

**Color type:** NoZ uses `Color` (4× `float`, 16 bytes) not `Color32` (4× `byte`, 4 bytes) for its main vertex color. Follow the same convention for shader compatibility.

### Unlit 3D shader (WGSL)

```wgsl
// Globals (NoZ's per-frame uniform — bound at group 0 binding 0)
struct Globals {
    projection: mat4x4f,
    time: f32,
}
@group(0) @binding(0) var<uniform> globals: Globals;

// Model uniform (per-draw-call — bound at group 0 binding 1)
struct Model {
    model: mat4x4f,
}
@group(0) @binding(1) var<uniform> model: Model;

struct VertexInput {
    @location(0) position: vec3f,
    @location(1) normal: vec3f,
    @location(2) uv: vec2f,
    @location(3) color: vec4f,
}

struct VertexOutput {
    @builtin(position) clip_position: vec4f,
    @location(0) color: vec4f,
}

@vertex fn vs_main(in: VertexInput) -> VertexOutput {
    var out: VertexOutput;
    let world_pos = model.model * vec4f(in.position, 1.0);
    out.clip_position = globals.projection * world_pos;
    out.color = in.color;
    return out;
}

@fragment fn fs_main(in: VertexOutput) -> @location(0) vec4f {
    return in.color;
}
```

The shader uses `ShaderFlags.Depth | ShaderFlags.DepthLess` so the pipeline gets `DepthCompare = Less, DepthWriteEnabled = true` via Phase 1a's depth pipeline.

**Shader registration:** Created via `Graphics.Driver.CreateShader(name, vertexSource, fragmentSource, bindings)` where `bindings` is a `ShaderBindingType[]` describing bind group layout. For the unlit shader: `[UniformBuffer, UniformBuffer]` (globals + model).

### Projection injection detail

NoZ's `GetOrAddGlobals(in Matrix4x4 projection)` is `private static` on `Graphics`. It de-duplicates projection matrices and returns a `ushort` index into a GPU-side uniform buffer array. Each `GlobalsSnapshot` is 80 bytes (`Matrix4x4 Projection` + `float Time` + padding).

**Fork change:** Make `GetOrAddGlobals` `internal static` instead of `private static`. This is the minimal change — no new API, no new parameters, just access modifier.

```csharp
// Before (Graphics.cs)
private static ushort GetOrAddGlobals(in Matrix4x4 projection) { ... }

// After
internal static ushort GetOrAddGlobals(in Matrix4x4 projection) { ... }
```

`Graphics3D.Begin(Camera3D camera)` then calls:
```csharp
_savedGlobalsIndex = Graphics.CurrentGlobalsIndex;  // save 2D projection
var vpMatrix = camera.ViewMatrix * camera.ProjectionMatrix;
_globalsIndex = Graphics.GetOrAddGlobals(vpMatrix);
Graphics.SetCurrentGlobalsIndex(_globalsIndex);
```

`Graphics3D.End()` restores `_savedGlobalsIndex`.

**Note:** NoZ transposes the projection matrix during `UploadGlobals()` before sending to WebGPU. The `Camera3D` must produce a row-major `Matrix4x4` (matching `System.Numerics` convention) — NoZ handles the transpose.

### Model matrix uniform

Each `DrawMesh` call needs a per-object `Matrix4x4` world transform. This uses NoZ's existing `CreateUniformBuffer` / `UpdateUniformBuffer` / `BindUniformBuffer` path:

```csharp
// During Graphics3D initialization:
_modelUbo = Graphics.Driver.CreateUniformBuffer(64, BufferUsage.Dynamic, "Model3D");

// During DrawMesh:
var transposed = Matrix4x4.Transpose(worldMatrix);
Graphics.Driver.UpdateUniformBuffer(_modelUbo, MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(ref transposed)));
Graphics.Driver.BindUniformBuffer(_modelUbo, slot: 1);
Graphics.DrawElements(mesh.IndexCount, mesh.IndexOffset, order: 0);
```

**Uniform buffer binding:** Slot 0 = NoZ globals (auto-bound), Slot 1 = model matrix. The shader's `@group(0) @binding(1)` maps to slot 1.

### Draw command integration

`Graphics3D.DrawMesh()` integrates with NoZ's batch/sort system:

1. **Bind shader** — `Graphics.SetShader(shader3D)` sets the current shader in batch state
2. **Bind mesh** — `Graphics.Driver.BindMesh(mesh.Handle)` sets the current mesh. If mesh handle differs from NoZ's main mesh, draw commands use the external mesh's index buffer
3. **Upload model matrix** — update the model UBO and bind it
4. **Call DrawElements** — `Graphics.DrawElements(indexCount, 0, order)` creates a `DrawCommand` with a 64-bit sort key

The sort key determines draw order: `Pass(4) | Layer(12) | Group(16) | Order(16) | Index(16)`. 3D objects should use a dedicated sort group or order range so they don't interleave with 2D draws.

### Open design decisions

**1. Shader authoring pipeline**

NoZ shaders are binary assets built by the NoZ Editor. YesZ needs WGSL shaders created outside that pipeline.

| Option | Pros | Cons |
|--------|------|------|
| **Embedded resource** — `.wgsl` files as embedded resources, loaded at init | Clean separation, IDE syntax highlighting for WGSL, standard .NET pattern | Slight build complexity (EmbeddedResource in .csproj) |
| **Programmatic** — `CreateShader()` with WGSL strings at runtime | No file management, full control | Shader source in C# strings, no syntax highlighting, hard to read |
| **NoZ asset pipeline** — shader assets via NoZ Editor | Consistent with NoZ | Requires understanding NoZ's binary shader format, unnecessary dependency |

**Recommendation:** Embedded resource. Add `<EmbeddedResource Include="Shaders/**/*.wgsl" />` to YesZ.Rendering.csproj. Load via `Assembly.GetManifestResourceStream()`.

**2. Color format — `Color` (4× float) vs `Color32` (4× u8 normalized)**

| Option | Pros | Cons |
|--------|------|------|
| **`Color` (float32)** | Matches NoZ's `MeshVertex`, no conversion needed, HDR-capable | 16 bytes/vertex vs 4 bytes/vertex |
| **`Color32` (u8 normalized)** | Compact, sufficient for LDR vertex color | Inconsistent with NoZ's pattern, needs `VertexAttribType.UByte` with `Normalized = true` |

**Recommendation:** `Color` (float32). Consistency with NoZ matters more than 12 bytes/vertex at this stage. Optimization can come later if vertex bandwidth becomes a bottleneck.

**3. Mesh lifetime management**

| Option | Pros | Cons |
|--------|------|------|
| **Immutable Mesh3D** — create once, GPU buffer allocated immediately | Simple API, no dynamic resize complexity | Re-uploading requires creating a new mesh |
| **Dynamic Mesh3D** — pre-allocate max capacity, update vertex/index data each frame | Supports procedural geometry, particle systems | More complex API, needs `BufferUsage.Dynamic` |

**Recommendation:** Start immutable. Procedural/dynamic meshes are a future concern (Phase 9 instancing may need dynamic buffers, but that's a separate design).

### Gotchas addressed

- **Vertex format hash mismatch:** NoZ's pipeline cache keys include the vertex format hash (via `VertexFormatHash.Compute`). If `MeshVertex3D` attributes don't match the WGSL shader's `VertexInput` locations, the pipeline won't be found. Ensure location indices in the C# descriptor match the WGSL `@location(N)` annotations exactly.
- **Matrix transpose:** NoZ transposes matrices in `UploadGlobals()` before sending to WebGPU. The model matrix UBO must also be transposed before upload. WebGPU/WGSL expects column-major; `System.Numerics.Matrix4x4` is row-major.
- **Merge mask incompatibility:** `DrawElements` tries to merge adjacent commands with matching `(BatchState, SortKey & MergeMask)`. 3D draw calls with different model matrices should NOT merge — each has a unique UBO state. This is naturally prevented because `BindUniformBuffer` changes batch state, making consecutive DrawMesh calls non-mergeable.
- **External mesh vs main mesh:** NoZ's main mesh is a large shared vertex/index buffer for 2D draws. `Mesh3D` uses separate GPU buffers created via `CreateMesh<MeshVertex3D>()`. Draw commands referencing external meshes keep their original index offsets — they are NOT consolidated into `_sortedIndices`.
- **Depth clear value:** Phase 1a clears depth to `1.0f` (far plane). The perspective projection must map near plane to `0.0` and far plane to `1.0` (WebGPU convention, reversed from OpenGL). `Matrix4x4.CreatePerspectiveFieldOfView` uses `[0, 1]` depth range which is correct for WebGPU.

### Tests (unit)

**`MeshVertex3DTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `GetFormatDescriptor_Default_Returns48ByteStride` | `SizeInBytes` = 48, stride matches struct layout |
| `GetFormatDescriptor_Default_Has4Attributes` | Position(0), Normal(1), UV(2), Color(3) |
| `GetFormatDescriptor_OffsetsMatchMarshal` | Each attribute offset matches `Marshal.OffsetOf` (0, 12, 24, 32) |
| `GetFormatDescriptor_ComponentCounts` | Position=3, Normal=3, UV=2, Color=4 |
| `VertexHash_DiffersFromNoZMeshVertex` | 3D hash ≠ 2D hash — ensures separate pipeline cache entries |

**`Mesh3DBuilderTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `CreateCube_Returns24Vertices` | 4 per face × 6 faces |
| `CreateCube_Returns36Indices` | 6 per face × 6 faces |
| `CreateCube_AllNormalsAreUnitLength` | No denormalized normals |
| `CreateCube_FaceNormalsPointOutward` | Cross product of each triangle's edges dots positive with expected face normal |
| `CreateCube_PositionsWithinUnitCube` | All positions in `[-0.5, 0.5]` |
| `CreateCube_UVsWithinZeroOne` | All UV coords in `[0, 1]` |
| `CreateCube_WindingOrderCorrect` | All triangles wind CCW when viewed from outside — validates with winding helper |

**`Camera3DTests`** (YesZ.Core.Tests — extend existing)

| Test | What it proves |
|------|---------------|
| `ViewProjection_OriginAtNearPlane_MapsToClipCenter` | Point on camera's forward axis at near plane maps to clip `(0, 0, ~0)` |
| `ViewProjection_OffScreenPoint_ClipOutsideNDC` | Point behind the camera has `w < 0` or `z > 1` |
| `ProjectionMatrix_FOVChange_AffectsHorizontalExtent` | Wider FOV maps a wider range of world X to `[-1, 1]` clip X |

### Verification

- `dotnet build yesz.slnx` passes with 0 warnings, 0 errors
- `dotnet test yesz.slnx` — all existing + new tests pass
- HelloCube opens and displays a colored spinning cube in the center of the window
- Cube faces occlude correctly — back faces hidden by front faces (depth test working)
- 2D UI text ("YesZ / 3D is coming") still renders on top of the cube (2D draws happen after `Graphics3D.End()`)
- Window resize works — cube aspect ratio adjusts correctly
- No WebGPU validation errors in console
- Diagnostic overlay: add camera position + draw call count as 2D text in HelloCube for ongoing visual debugging

**Estimated scope:** ~6-8 files, L2 planning

---

## Phase 2: Materials & Texturing

**Milestone:** HelloCube renders a textured cube (e.g., crate texture loaded from a PNG file).
**Status:** Done

**Dependencies:** Phase 1b (vertex format with UVs, shader system, Graphics3D API)
**Fork changes:** None — uses existing `SetTexture`, `SetUniform`, `ShaderBindingType` APIs
**Enables:** Phase 3b (lit materials extend material uniform with lighting params), Phase 4b (glTF material → `Material3D` mapping), Phase 5d (skinned + textured rendering), Phase 7a (post-processing needs texture binding)

### Implementation notes (deviations from original design)

**No Model matrix in MaterialUniforms.** The original design put the model matrix in `MaterialUniforms` (96 bytes). However, NoZ's batch system is deferred — `SetUniform` is global driver state, not per-batch. The last `SetUniform` call wins for all batches at flush time. The MVP-in-globals pattern (model × view × proj baked into globals.projection) remains the correct per-object differentiation mechanism. `MaterialUniforms` contains only material params (baseColorFactor, metallic, roughness) at 32 bytes.

**~~One active material per frame~~ — RESOLVED.** Per-batch uniform snapshots were added to the NoZ fork (`Graphics.cs`, `Graphics.State.cs`). `SetUniform` data is now snapshotted in `AddBatchState()` and restored per-batch during `ExecuteBatches()`, mirroring the `GlobalsSnapshot` pattern. Multiple materials per frame now work correctly. Intended as upstream contribution to NoZ.

**Procedural checkerboard texture** instead of PNG file for the sample — avoids external asset dependencies while demonstrating the texture pipeline. File-based loading is available via `TextureLoader.LoadFromFile()`.

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `Material3D` | YesZ.Rendering | Material class: shader reference, base color texture, PBR scalar parameters, material UBO handle |
| `MaterialUniforms` | YesZ.Rendering | Uniform struct: `Matrix4x4 Model`, `Vector4 BaseColorFactor`, `float Metallic`, `float Roughness`, padding |
| Textured 3D shader | YesZ.Rendering | WGSL shader: samples base color texture × vertex color × `baseColorFactor` uniform |
| `Graphics3D.SetMaterial(Material3D)` | YesZ.Rendering | Binds material's shader, texture(s), and UBO before draw calls |
| `Graphics3D.DrawMesh(Mesh3D, Matrix4x4)` update | YesZ.Rendering | Uses current material's UBO for model matrix + material params instead of standalone model UBO |
| Texture loading utility | YesZ.Rendering | Load PNG/JPG → `TextureFormat.RGBA8` → `CreateTexture` → driver texture handle |
| Default white texture | YesZ.Rendering | 1×1 white pixel texture for untextured materials (avoids shader permutations) |
| Tests | YesZ.Rendering.Tests | Material creation, uniform buffer layout, texture slot binding |

### Material3D class design

```csharp
public class Material3D
{
    public nuint Shader { get; }              // Driver shader handle
    public nuint BaseColorTexture { get; set; } // Driver texture handle (default: white 1×1)
    public Vector4 BaseColorFactor { get; set; } = Vector4.One;  // RGBA multiplier
    public float Metallic { get; set; } = 0.0f;   // 0 = dielectric, 1 = metal
    public float Roughness { get; set; } = 0.5f;  // 0 = mirror, 1 = diffuse

    internal nuint MaterialUbo { get; }        // GPU uniform buffer handle
}
```

PBR parameters (`Metallic`, `Roughness`) are stored but NOT used by the unlit shader in this phase. They become active in Phase 3b when the lit shader reads them. Storing them now ensures `Material3D` doesn't need structural changes later.

### MaterialUniforms layout

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct MaterialUniforms
{
    public Matrix4x4 Model;         // 64 bytes — world transform (transposed before upload)
    public Vector4 BaseColorFactor;  // 16 bytes — RGBA color multiplier
    public float Metallic;           //  4 bytes
    public float Roughness;          //  4 bytes
    public float _pad0;              //  4 bytes — align to 16-byte boundary
    public float _pad1;              //  4 bytes
}
// Total: 96 bytes (WebGPU requires uniform buffer size to be multiple of 16)
```

**Why model matrix is in MaterialUniforms:** Combining model transform + material params into one UBO means one `UpdateUniformBuffer` + one `BindUniformBuffer` per draw call instead of two. The model matrix changes per-object while material params change per-material, but the uniform update cost is negligible for the volume of draws YesZ targets.

### Textured shader (WGSL)

```wgsl
struct Globals {
    projection: mat4x4f,
    time: f32,
}
@group(0) @binding(0) var<uniform> globals: Globals;

struct Material {
    model: mat4x4f,
    base_color_factor: vec4f,
    metallic: f32,
    roughness: f32,
}
@group(0) @binding(1) var<uniform> material: Material;
@group(0) @binding(2) var base_color_texture: texture_2d<f32>;
@group(0) @binding(3) var base_color_sampler: sampler;

struct VertexInput {
    @location(0) position: vec3f,
    @location(1) normal: vec3f,
    @location(2) uv: vec2f,
    @location(3) color: vec4f,
}

struct VertexOutput {
    @builtin(position) clip_position: vec4f,
    @location(0) color: vec4f,
    @location(1) uv: vec2f,
}

@vertex fn vs_main(in: VertexInput) -> VertexOutput {
    var out: VertexOutput;
    let world_pos = material.model * vec4f(in.position, 1.0);
    out.clip_position = globals.projection * world_pos;
    out.color = in.color;
    out.uv = in.uv;
    return out;
}

@fragment fn fs_main(in: VertexOutput) -> @location(0) vec4f {
    let tex_color = textureSample(base_color_texture, base_color_sampler, in.uv);
    return tex_color * in.color * material.base_color_factor;
}
```

**Shader bindings array:** `[UniformBuffer, UniformBuffer, Texture2D, Sampler]` — maps to `ShaderBindingType` values passed to `CreateShader()`.

### Texture loading detail

NoZ's `IGraphicsDriver` provides:
- `CreateTexture(width, height, format, data, name)` → `nuint` handle
- `BindTexture(handle, slot, filter)` — binds to a texture slot

Loading pipeline:
1. Read PNG/JPG bytes (from file or embedded resource)
2. Decode to RGBA8 pixel array via `StbImageSharp` (NuGet, pure C#, zero native deps)
3. Call `CreateTexture(width, height, TextureFormat.RGBA8, pixelSpan, name)`
4. Store the returned handle in `Material3D.BaseColorTexture`

**Image library choice:**

| Option | Pros | Cons |
|--------|------|------|
| **StbImageSharp** | Pure C#, cross-platform, no native deps, supports PNG/JPG/BMP/TGA | NuGet dependency |
| **System.Drawing** | Built into .NET | Windows-only, deprecated on non-Windows |
| **ImageSharp** | Full-featured, cross-platform | Heavy dependency, license considerations |

**Recommendation:** StbImageSharp — minimal, cross-platform, sufficient.

### Default white texture

Every `Material3D` starts with `BaseColorTexture` set to a shared 1×1 white pixel:

```csharp
// Created once during Graphics3D initialization
private static nuint _defaultWhiteTexture;
var white = new byte[] { 255, 255, 255, 255 };
_defaultWhiteTexture = Graphics.Driver.CreateTexture(1, 1, TextureFormat.RGBA8, white, "DefaultWhite");
```

This avoids needing separate "textured" and "untextured" shader variants. An untextured material simply samples the white pixel, which multiplies to identity.

### Open design decisions

**1. Texture coordinate convention — top-left vs bottom-left origin**

| Option | Pros | Cons |
|--------|------|------|
| **Top-left origin (V=0 at top)** | Matches image file layout, matches glTF convention, matches WebGPU | Opposite of OpenGL convention |
| **Bottom-left origin (V=0 at bottom)** | Matches OpenGL | Requires Y-flip on load or in shader |

**Recommendation:** Top-left origin. WebGPU and glTF both use top-left. No flip needed.

**2. Sampler management — per-material vs shared**

| Option | Pros | Cons |
|--------|------|------|
| **Shared samplers** — one linear, one point | Simple, matches NoZ's `TextureFilter` pattern | No per-material wrap/filter control |
| **Per-material samplers** | Full control (wrap mode, anisotropy) | More driver objects, overkill for now |

**Recommendation:** Shared samplers. NoZ's `BindTexture(handle, slot, filter)` already specifies `TextureFilter` per binding.

### Gotchas addressed

- **Bind group layout must match shader:** The `ShaderBindingType[]` passed to `CreateShader()` defines the bind group layout. Adding `Texture2D` and `Sampler` bindings means the bind group creation code in `WebGPUGraphicsDriver` must include texture view and sampler entries at the correct indices. This is already handled — NoZ's bind group builder iterates the shader's binding array.
- **RGBA vs BGRA:** NoZ's swap chain uses `BGRA8` but textures loaded from files should use `RGBA8` (matching PNG/JPG decode output). The WebGPU driver maps `TextureFormat.RGBA8` to `WGPUTextureFormat.Rgba8Unorm`.
- **Uniform buffer alignment:** WebGPU requires uniform buffer sizes to be multiples of 16 bytes. `MaterialUniforms` at 96 bytes is correctly aligned. Maintain 16-byte alignment if fields are added later.
- **Texture slot vs binding index:** NoZ's `BindTexture(handle, slot, filter)` uses a slot index mapping to the shader's binding array. Slot 0 = first `Texture2D` entry in bindings (not `@binding(0)`). Ensure slot indices match binding array order.

### Tests (unit)

**`MaterialUniformsTests`** (YesZ.Rendering.Tests — new file)

| Test | What it proves |
|------|---------------|
| `SizeOf_Returns96Bytes` | `Marshal.SizeOf<MaterialUniforms>()` = 96 (WebGPU 16-byte alignment) |
| `ModelOffset_Is0` | Model matrix at byte offset 0 |
| `BaseColorFactorOffset_Is64` | BaseColorFactor immediately after Model matrix |
| `MetallicOffset_Is80` | Metallic at byte offset 80 |
| `RoughnessOffset_Is84` | Roughness at byte offset 84 |

**`Material3DTests`** (YesZ.Rendering.Tests — new file)

| Test | What it proves |
|------|---------------|
| `DefaultBaseColorFactor_IsWhite` | `BaseColorFactor` = `(1, 1, 1, 1)` |
| `DefaultMetallic_IsZero` | Non-metallic by default |
| `DefaultRoughness_IsHalf` | `Roughness` = 0.5 |

### Verification

- `dotnet build yesz.slnx` passes with 0 warnings, 0 errors
- `dotnet test yesz.slnx` — all existing + new tests pass
- HelloCube displays a textured cube (e.g., wooden crate PNG)
- Texture is correctly mapped — no stretching, seams, or UV flipping
- Untextured material (default white texture) still works — vertex-colored cube renders identically to Phase 1b
- `BaseColorFactor` tints the texture when set to a non-white color
- Window resize works correctly
- No WebGPU validation errors

**Estimated scope:** ~5-8 files, L2 planning

---

## Phase 3a: Light Infrastructure (CPU-side)

**Milestone:** Light types compile, tests pass, Graphics3D light API is callable (no visual output).

**Dependencies:** Phase 0 (pure data types — can start any time after scaffold)
**Fork changes:** None
**Enables:** Phase 3b (light types feed into the lit shader)
**Parallel with:** Phase 1a, 1b, 2 (no rendering dependency — pure data types)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `DirectionalLight` | YesZ.Core | Struct: `Vector3 Direction` (normalized), `Vector3 Color` (linear RGB), `float Intensity` |
| `PointLight` | YesZ.Core | Struct: `Vector3 Position`, `Vector3 Color`, `float Intensity`, `float Range` |
| `AmbientLight` | YesZ.Core | Struct: `Vector3 Color`, `float Intensity` |
| `LightEnvironment` | YesZ.Core | Container: one `AmbientLight`, one `DirectionalLight`, up to N `PointLight`s, tracks dirty state |
| `Graphics3D.SetDirectionalLight()` | YesZ.Rendering | Stores directional light in current `LightEnvironment` |
| `Graphics3D.SetAmbientLight()` | YesZ.Rendering | Stores ambient light in current `LightEnvironment` |
| `Graphics3D.AddPointLight()` | YesZ.Rendering | Appends point light to current frame's light list (cleared each frame) |
| Light unit tests | YesZ.Core.Tests | Direction normalization, intensity clamping, range validation, default values |

### Light data structures

```csharp
public struct DirectionalLight
{
    public Vector3 Direction;  // Must be normalized (unit length)
    public Vector3 Color;      // Linear RGB, NOT sRGB (e.g., (1,1,1) = white)
    public float Intensity;    // Multiplier (typically 1.0-5.0)

    public Vector3 EffectiveColor => Color * Intensity;
}

public struct PointLight
{
    public Vector3 Position;   // World-space position
    public Vector3 Color;      // Linear RGB
    public float Intensity;    // Multiplier
    public float Range;        // Maximum influence distance (attenuation = 0 beyond this)
}

public struct AmbientLight
{
    public Vector3 Color;      // Linear RGB
    public float Intensity;    // Multiplier (typically 0.1-0.3)
}
```

**Why `Vector3` for color instead of `Color`:** GPU shaders work in linear RGB space. NoZ's `Color` struct (4× float RGBA) includes alpha, which is meaningless for lights. Using `Vector3` enforces the "no alpha on lights" invariant at the type level and matches the WGSL `vec3f` that lights will map to in Phase 3b.

### LightEnvironment design

```csharp
public class LightEnvironment
{
    public AmbientLight Ambient { get; set; } = new() { Color = Vector3.One, Intensity = 0.1f };
    public DirectionalLight Directional { get; set; } = new() { Direction = Vector3.Normalize(new(-0.5f, -1f, -0.5f)), Color = Vector3.One, Intensity = 1.0f };

    private readonly PointLight[] _pointLights = new PointLight[MaxPointLights];
    private int _pointLightCount;

    public const int MaxPointLights = 8;

    public void AddPointLight(in PointLight light) { ... }
    public void ClearPointLights() { _pointLightCount = 0; }
    public ReadOnlySpan<PointLight> PointLights => _pointLights.AsSpan(0, _pointLightCount);
}
```

`ClearPointLights()` is called at the start of each frame by `Graphics3D.Begin()`. Lights are re-registered each frame — no stale-state tracking needed.

### Gotchas addressed

- **Direction convention:** Light direction points FROM the light source TOWARD the scene (i.e., the direction light travels). In the shader, N·L uses `-direction` to get the vector from surface toward light. Document this clearly — some engines use the opposite convention.
- **Color space:** Light colors must be in linear RGB, not sRGB. If loading from user-facing config (e.g., JSON), convert sRGB → linear at load time. The shader operates entirely in linear space.
- **Zero-length direction:** `DirectionalLight.Direction` must be normalized. Validate and normalize in the setter to prevent NaN propagation if a zero vector is set.

### Tests (unit)

This phase is 100% unit-testable — no GPU dependency. Can be developed test-first in parallel with Phases 1a/1b.

**`DirectionalLightTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Direction_SetNonUnit_IsNormalized` | Setting `(1, 2, 3)` produces a unit-length vector |
| `Direction_SetZero_FallsBackToDefault` | Zero vector doesn't propagate NaN |
| `EffectiveColor_IsColorTimesIntensity` | `Color=(1,0,0)`, `Intensity=2` → `EffectiveColor=(2,0,0)` |
| `EffectiveColor_ZeroIntensity_IsBlack` | `Intensity=0` → `(0,0,0)` |

**`PointLightTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Range_MustBePositive` | Setting `Range=0` or negative throws or clamps |
| `DefaultRange_IsReasonable` | Non-zero default |

**`AmbientLightTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Default_HasWhiteColorLowIntensity` | `Color=(1,1,1)`, `Intensity=0.1` |

**`LightEnvironmentTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `AddPointLight_UnderMax_Succeeds` | Can add up to `MaxPointLights` (8) |
| `AddPointLight_OverMax_Throws` | 9th light throws `InvalidOperationException` |
| `ClearPointLights_ResetsCount` | After clear, `PointLights.Length` = 0 |
| `Default_HasReasonableAmbient` | Ambient intensity ~0.1, white |
| `Default_HasReasonableDirectional` | Direction is downward-ish, white, intensity ~1.0 |
| `PointLights_ReturnsCorrectSpan` | After adding 3 lights, span has length 3 with correct values |

### Verification

- `dotnet build yesz.slnx` passes
- `dotnet test yesz.slnx` — all existing + new light tests pass
- No visual output change (data types + API only, no GPU upload yet)

**Estimated scope:** ~2-3 files, L1

---

## Phase 3b: Lit Shading

**Milestone:** HelloCube shows a lit cube with visible shading from a directional light and ambient light.

**Dependencies:** Phase 2 (material system, MaterialUniforms, texture binding), Phase 3a (light data types)
**Fork changes:** None expected — lit vs unlit is determined by which shader the `Material3D` references, not by `ShaderFlags`
**Enables:** Phase 3c (multi-light extends the light UBO and shader loop)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `LightUniforms` | YesZ.Rendering | C# struct matching WGSL light uniform buffer layout |
| Light UBO upload | YesZ.Rendering | `Graphics3D` writes `LightEnvironment` → `LightUniforms` UBO each frame |
| Lit PBR shader | YesZ.Rendering | WGSL: Lambertian diffuse + GGX specular + ambient, samples base color texture |
| Normal matrix computation | YesZ.Rendering | `Matrix4x4.Invert()` + transpose of upper 3×3 of model matrix |
| `MaterialUniforms` update | YesZ.Rendering | Add `Matrix4x4 NormalMatrix` field to the material uniform buffer |
| Camera position uniform | YesZ.Rendering | Add `Vector3 CameraPosition` to globals or light UBO for specular view direction |
| Tests | YesZ.Core.Tests | Normal matrix computation for identity, rotation, non-uniform scale |

### LightUniforms layout

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct LightUniforms
{
    public Vector4 AmbientColor;        // 16 bytes — RGB + intensity packed as (r*i, g*i, b*i, 0)
    public Vector4 DirectionalDir;      // 16 bytes — normalized direction (xyz) + padding (w)
    public Vector4 DirectionalColor;    // 16 bytes — RGB × intensity (xyz) + padding (w)
    public Vector4 CameraPosition;      // 16 bytes — world-space camera pos (xyz) + padding (w)
}
// Total: 64 bytes
```

**Why pack intensity into color:** Avoids a separate intensity float per light in the UBO. Shader receives pre-multiplied `color * intensity` as a `vec3f`, which is exactly what the lighting equation needs. Saves UBO space and a shader multiply.

### Updated MaterialUniforms layout

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct MaterialUniforms
{
    public Matrix4x4 Model;             // 64 bytes — world transform
    public Matrix4x4 NormalMatrix;      // 64 bytes — inverse-transpose of model (for normals)
    public Vector4 BaseColorFactor;     // 16 bytes
    public float Metallic;              //  4 bytes
    public float Roughness;             //  4 bytes
    public float _pad0;                 //  4 bytes
    public float _pad1;                 //  4 bytes
}
// Total: 160 bytes
```

**Normal matrix:** The inverse-transpose of the model matrix's upper 3×3, stored as a full `mat4x4f` for UBO alignment. Non-uniform scale (e.g., stretching a cube into a box) distorts normals unless corrected by the inverse-transpose. For uniform scale or rotation-only, the normal matrix equals the model matrix's upper 3×3, so this is a no-op for simple cases.

### Lit PBR shader (WGSL)

```wgsl
struct Globals {
    projection: mat4x4f,
    time: f32,
}
@group(0) @binding(0) var<uniform> globals: Globals;

struct Material {
    model: mat4x4f,
    normal_matrix: mat4x4f,
    base_color_factor: vec4f,
    metallic: f32,
    roughness: f32,
}
@group(0) @binding(1) var<uniform> material: Material;
@group(0) @binding(2) var base_color_texture: texture_2d<f32>;
@group(0) @binding(3) var base_color_sampler: sampler;

struct Lights {
    ambient_color: vec4f,
    directional_dir: vec4f,
    directional_color: vec4f,
    camera_position: vec4f,
}
@group(0) @binding(4) var<uniform> lights: Lights;

struct VertexOutput {
    @builtin(position) clip_position: vec4f,
    @location(0) world_position: vec3f,
    @location(1) world_normal: vec3f,
    @location(2) uv: vec2f,
    @location(3) color: vec4f,
}

@vertex fn vs_main(in: VertexInput) -> VertexOutput {
    var out: VertexOutput;
    let world_pos = material.model * vec4f(in.position, 1.0);
    out.clip_position = globals.projection * world_pos;
    out.world_position = world_pos.xyz;
    out.world_normal = normalize((material.normal_matrix * vec4f(in.normal, 0.0)).xyz);
    out.uv = in.uv;
    out.color = in.color;
    return out;
}

@fragment fn fs_main(in: VertexOutput) -> @location(0) vec4f {
    let base_color = textureSample(base_color_texture, base_color_sampler, in.uv)
                     * in.color * material.base_color_factor;
    let N = normalize(in.world_normal);
    let V = normalize(lights.camera_position.xyz - in.world_position);
    let L = normalize(-lights.directional_dir.xyz);  // negate: direction is toward scene

    // Diffuse (Lambertian)
    let NdotL = max(dot(N, L), 0.0);
    let diffuse = lights.directional_color.xyz * NdotL;

    // Specular (Blinn-Phong — simplified, full GGX in future)
    let H = normalize(V + L);
    let NdotH = max(dot(N, H), 0.0);
    let shininess = mix(8.0, 256.0, 1.0 - material.roughness);
    let specular = lights.directional_color.xyz * pow(NdotH, shininess)
                   * mix(0.04, 1.0, material.metallic);

    let ambient = lights.ambient_color.xyz;
    let final_color = base_color.rgb * (ambient + diffuse) + specular;
    return vec4f(final_color, base_color.a);
}
```

**Why Blinn-Phong instead of full PBR (Cook-Torrance/GGX):** Blinn-Phong is visually adequate for the first lit render and much simpler to implement and debug. Full GGX (with Fresnel-Schlick, Smith-GGX geometry, and GGX normal distribution) can replace the specular term in a later pass without changing the material data or UBO layout.

### Shader permutation strategy

**Decision: separate shader objects, NOT uber-shader with `ShaderFlags`.**

| Option | Pros | Cons |
|--------|------|------|
| **Separate shader objects** — `Shaders3D.Unlit` and `Shaders3D.Lit` as distinct `CreateShader()` calls | Clean, no fork changes, each shader is simple and debuggable | Small amount of duplicated vertex transform code |
| **Uber-shader with `ShaderFlags.Lit`** | Single shader source, NoZ pipeline cache handles variants | Fork change to add flag, WGSL lacks preprocessor (`#ifdef`), conditional logic via runtime branching is wasteful |

WGSL has no preprocessor directives. Unlike HLSL/GLSL which support `#ifdef`, WGSL requires runtime branching for conditional code. Separate shader objects avoid this entirely and keep each shader readable. The `Material3D` selects which shader to use — `Shaders3D.Lit` or `Shaders3D.Unlit` — at material creation time.

### Gotchas addressed

- **Normal interpolation artifacts:** Vertex normals are interpolated across the triangle face. For a cube with flat shading (one normal per face), each face needs 4 unique vertices with the same normal — shared corner vertices would produce incorrect interpolated normals. Phase 1b's `Mesh3DBuilder.CreateCube()` already uses 24 vertices (4 per face) for this reason.
- **Light direction negation:** The shader negates the directional light direction to get the surface-to-light vector. If this is missed, the wrong faces are lit (back-face illumination).
- **Normal matrix for identity model:** When the model matrix is identity, the normal matrix is also identity. `Matrix4x4.Invert()` returns `false` for singular matrices — handle this by falling back to the model matrix's upper 3×3 (correct for rotation+uniform-scale).
- **sRGB color space:** The shader computes lighting in linear space, but the swap chain may expect sRGB. Without Phase 7b's tone mapping, colors may appear too dark or too bright. Accept this as a known limitation until tone mapping is implemented.
- **Specular highlights on back faces:** The `max(dot(N, L), 0.0)` clamp prevents negative diffuse, but specular can still produce highlights on back-facing surfaces if the half-vector happens to align. Add `NdotL > 0.0` guard around the specular term.

### Tests (unit)

**`NormalMatrixTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Identity_ProducesIdentity` | Inverse-transpose of identity is identity |
| `RotationOnly_EqualsModelMatrix` | For pure rotation, normal matrix = model matrix upper 3×3 |
| `NonUniformScale_DiffersFromModelMatrix` | `Scale(2,1,1)` → normal matrix ≠ model matrix (the whole reason normal matrices exist) |
| `SingularMatrix_FallsBackToModel` | When `Matrix4x4.Invert` fails, use model matrix upper 3×3 |

**`MaterialUniformsTests`** (YesZ.Rendering.Tests — extend from Phase 2)

| Test | What it proves |
|------|---------------|
| `SizeOf_Returns160Bytes` | Updated struct with NormalMatrix field is 160 bytes |
| `NormalMatrixOffset_Is64` | NormalMatrix immediately after Model |

**`LightUniformsTests`** (YesZ.Rendering.Tests — new file)

| Test | What it proves |
|------|---------------|
| `SizeOf_Returns64Bytes` | `Marshal.SizeOf<LightUniforms>()` = 64 |
| `AmbientColorOffset_Is0` | First field at offset 0 |
| `CameraPositionOffset_Is48` | Camera position at byte 48 |

### Verification

- HelloCube shows a cube with visible directional light shading (bright side / dark side)
- Ambient light provides base illumination (no pure-black faces)
- Specular highlight visible on the bright side when roughness is low
- Unlit materials still work (existing Phase 2 content unaffected — different shader)
- Rotating the cube changes which faces are lit

**Estimated scope:** ~4-5 files, L2

---

## Phase 3c: Multi-Light + Point Lights ✅

**Status:** Complete

**Milestone:** HelloCube with directional + point light(s) showing position-based attenuation.

**Dependencies:** Phase 3b (lit shader, light UBO system)
**Fork changes:** None
**Enables:** Phase 6a (shadow mapping needs multi-light infrastructure)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `LightUniforms` expansion | YesZ.Rendering | Extend light UBO with `PointLight` array (position, color, range) × `MaxPointLights` |
| Multi-light shader update | YesZ.Rendering | WGSL loop over active point lights, accumulate diffuse + specular per light |
| Attenuation function | YesZ.Rendering (WGSL) | Smooth distance-based falloff: `clamp(1.0 - (d/range)², 0, 1)²` |
| `Graphics3D` light upload | YesZ.Rendering | Pack `LightEnvironment.PointLights` into UBO array each frame |
| Light count uniform | YesZ.Rendering | `u32 pointLightCount` in light UBO controls shader loop iteration |
| Tests | YesZ.Core.Tests | Attenuation curves at various distances, multi-light contribution accumulation |

### Updated LightUniforms layout

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct PointLightData
{
    public Vector4 Position;   // xyz = world position, w = range
    public Vector4 Color;      // xyz = color × intensity, w = unused
}

[StructLayout(LayoutKind.Sequential)]
internal struct LightUniforms
{
    public Vector4 AmbientColor;                         //  16 bytes
    public Vector4 DirectionalDir;                       //  16 bytes
    public Vector4 DirectionalColor;                     //  16 bytes
    public Vector4 CameraPosition;                       //  16 bytes
    public uint PointLightCount;                          //   4 bytes
    public uint _pad0, _pad1, _pad2;                      //  12 bytes (align to 16)
    public PointLightData PointLight0;                    //  32 bytes
    public PointLightData PointLight1;                    //  32 bytes
    public PointLightData PointLight2;                    //  32 bytes
    public PointLightData PointLight3;                    //  32 bytes
    public PointLightData PointLight4;                    //  32 bytes
    public PointLightData PointLight5;                    //  32 bytes
    public PointLightData PointLight6;                    //  32 bytes
    public PointLightData PointLight7;                    //  32 bytes
}
// Total: 352 bytes (well within 64KB uniform limit)
```

**Why fixed fields instead of inline array:** C# `fixed` arrays of structs aren't supported. Unrolled `PointLight0`..`PointLight7` fields match the WGSL `array<PointLightData, 8>` layout. Helper method `SetPointLight(int index, PointLightData data)` uses `Unsafe.Add` for indexed access.

### Attenuation function (WGSL)

```wgsl
fn attenuate(distance: f32, range: f32) -> f32 {
    let ratio = clamp(distance / range, 0.0, 1.0);
    let falloff = 1.0 - ratio * ratio;
    return falloff * falloff;
}
```

This smooth quadratic falloff reaches exactly zero at `distance = range`, which avoids popping artifacts when objects leave a light's influence. It's physically plausible (energy decreases with distance squared) and has a finite range (unlike `1/d²` which never reaches zero).

### Multi-light shader loop (WGSL)

```wgsl
// In fragment shader, after computing N, V, base_color:
var total_diffuse = lights.ambient_color.xyz;
var total_specular = vec3f(0.0);

// Directional light
{
    let L = normalize(-lights.directional_dir.xyz);
    let NdotL = max(dot(N, L), 0.0);
    total_diffuse += lights.directional_color.xyz * NdotL;
    if (NdotL > 0.0) {
        let H = normalize(V + L);
        total_specular += lights.directional_color.xyz * pow(max(dot(N, H), 0.0), shininess);
    }
}

// Point lights
for (var i = 0u; i < lights.point_light_count; i++) {
    let light = lights.point_lights[i];
    let light_vec = light.position.xyz - in.world_position;
    let distance = length(light_vec);
    let L = light_vec / distance;
    let atten = attenuate(distance, light.position.w);  // w = range
    let NdotL = max(dot(N, L), 0.0);
    total_diffuse += light.color.xyz * NdotL * atten;
    if (NdotL > 0.0) {
        let H = normalize(V + L);
        total_specular += light.color.xyz * pow(max(dot(N, H), 0.0), shininess) * atten;
    }
}

let final_color = base_color.rgb * total_diffuse + total_specular * mix(0.04, 1.0, material.metallic);
```

### Open design decisions

**1. Max point light count**

| Count | UBO Size | GPU Cost | Use Case |
|-------|----------|----------|----------|
| 4 | 224 bytes | Minimal | Simple scenes, mobile-tier |
| **8** | 352 bytes | Low | Standard forward rendering, most games |
| 16 | 608 bytes | Moderate | Complex indoor scenes |

**Recommendation:** 8 — standard for forward rendering. Well within the 64KB uniform limit. Higher counts justify deferred or clustered forward rendering (future work).

**2. Spot lights**

| Option | Pros | Cons |
|--------|------|------|
| **Include in Phase 3c** | Complete light type set, flashlight/cone effects | More shader complexity, more UBO fields |
| **Defer to future phase** | Keep 3c focused, spot lights are less common | Missing standard light type |

**Recommendation:** Defer. Point + directional covers 90% of use cases. Spot lights add inner/outer cone angles and a direction vector per light — worth a dedicated sub-phase if needed.

### Gotchas addressed

- **Uniform loop bounds:** WGSL requires the loop bound to be uniform (same value for all fragment invocations). `lights.point_light_count` in a uniform buffer satisfies this. A non-uniform loop bound (e.g., from a storage buffer) would require different handling.
- **Division by zero in attenuation:** If `range = 0`, the `distance / range` division is undefined. Validate `range > 0` in `PointLight` or `LightEnvironment.AddPointLight()`.
- **Per-fragment light vector normalization:** `light_vec / distance` avoids calling `normalize()` separately and reuses the `length()` result. Micro-optimization but free.
- **Light contribution overflow:** With multiple bright lights, total color can exceed 1.0. This is correct in HDR rendering (Phase 7b will tone-map). Without tone mapping, clipping to white is acceptable as a known limitation.

### Tests (unit)

**`AttenuationTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `AtDistance0_Returns1` | No falloff at light center |
| `AtDistanceEqualToRange_Returns0` | Clean cutoff at max range |
| `AtHalfRange_ReturnsExpected` | `(1 - 0.25)² = 0.5625` — validates the `(1 - (d/r)²)²` formula |
| `BeyondRange_Returns0` | No negative or NaN values past range |
| `AtQuarterRange_HigherThanHalf` | Monotonically decreasing |

**`LightUniformsTests`** (YesZ.Rendering.Tests — extend from Phase 3b)

| Test | What it proves |
|------|---------------|
| `SizeOf_Returns352Bytes` | Expanded struct with 8 point lights = 352 bytes |
| `PointLightCountOffset_Is64` | Count field after the 4 base vec4s |
| `SetPointLight_IndexValid_WritesCorrectData` | `Unsafe.Add` helper writes to correct memory offset |
| `SetPointLight_IndexOutOfRange_Throws` | Index ≥ 8 throws |

### Verification

- HelloCube shows a cube lit by directional light + at least one point light
- Point light shows visible falloff (closer = brighter, at range = dark)
- Moving a point light position changes shading in real-time
- Adding multiple point lights produces additive illumination
- Point light at max range produces zero contribution (no popping)

**Estimated scope:** ~2-3 files, L2

---

## Phase 4a: glTF Parser + Mesh Extraction ✅

**Status:** Complete

**Milestone:** Unit tests parse a known `.glb` test file (e.g., "Box.glb"), produce correct vertex/index counts and data ranges.

**Dependencies:** None (pure data layer — can develop in parallel with everything)
**Fork changes:** None
**Enables:** Phase 4b (model rendering), Phase 5a (skeleton/animation parsing reuses glTF JSON model + accessor resolver)
**Parallel with:** Phases 1a, 1b, 2, 3 (no rendering dependency)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `GlbReader` | YesZ.Core | Parse `.glb` binary container: 12-byte header, JSON chunk, BIN chunk |
| `GltfDocument` | YesZ.Core | Root JSON model: `System.Text.Json` deserialization of all top-level glTF arrays |
| `GltfAccessor` / `GltfBufferView` | YesZ.Core | JSON POCOs for accessor and bufferView objects |
| `GltfMesh` / `GltfMeshPrimitive` | YesZ.Core | JSON POCOs for mesh and primitive objects |
| `GltfNode` / `GltfScene` | YesZ.Core | JSON POCOs for node hierarchy and scene root |
| `GltfMaterial` | YesZ.Core | JSON POCO for PBR metallic-roughness material |
| `AccessorReader` | YesZ.Core | Resolve accessor → bufferView → BIN chunk byte range → typed `ReadOnlySpan<T>` |
| `MeshExtractor` | YesZ.Core | Extract POSITION, NORMAL, TEXCOORD_0, indices from a mesh primitive → `Mesh3D` |
| Test `.glb` files | YesZ.Core.Tests | Embedded "Box.glb" and "BoxTextured.glb" from glTF sample models repo |
| Parser tests | YesZ.Core.Tests | Header validation, chunk parsing, accessor resolution, mesh extraction correctness |

### glTF binary container format (.glb)

```
Bytes 0-3:   magic "glTF" (0x46546C67)
Bytes 4-7:   version (2)
Bytes 8-11:  total length in bytes

Chunk 0 (JSON):
  Bytes 0-3:  chunk length
  Bytes 4-7:  chunk type (0x4E4F534A = "JSON")
  Bytes 8+:   UTF-8 JSON string (padded with spaces to 4-byte alignment)

Chunk 1 (BIN):
  Bytes 0-3:  chunk length
  Bytes 4-7:  chunk type (0x004E4942 = "BIN\0")
  Bytes 8+:   binary buffer data (padded with zeros to 4-byte alignment)
```

### GltfDocument JSON model

```csharp
public class GltfDocument
{
    public GltfAsset? Asset { get; set; }
    public GltfScene[]? Scenes { get; set; }
    public int? Scene { get; set; }                   // default scene index
    public GltfNode[]? Nodes { get; set; }
    public GltfMesh[]? Meshes { get; set; }
    public GltfAccessor[]? Accessors { get; set; }
    public GltfBufferView[]? BufferViews { get; set; }
    public GltfBuffer[]? Buffers { get; set; }
    public GltfMaterial[]? Materials { get; set; }
    public GltfTexture[]? Textures { get; set; }
    public GltfImage[]? Images { get; set; }
    public GltfSampler[]? Samplers { get; set; }
    public GltfSkin[]? Skins { get; set; }             // Phase 5a
    public GltfAnimation[]? Animations { get; set; }   // Phase 5a
}
```

All arrays are nullable — a valid glTF file may omit any section. Using `System.Text.Json` source generators (`[JsonSerializable(typeof(GltfDocument))]`) for AOT-compatible, allocation-efficient parsing.

### AccessorReader detail

The accessor → data pipeline:

```
accessor.bufferView → bufferViews[i].buffer    → buffers[j] (always 0 for .glb)
                      bufferViews[i].byteOffset → start in BIN chunk
                      bufferViews[i].byteLength → byte range in BIN chunk
accessor.byteOffset  → additional offset within the bufferView
accessor.count       → number of elements
accessor.type        → "SCALAR", "VEC2", "VEC3", "VEC4", "MAT4"
accessor.componentType → 5120(byte), 5121(ubyte), 5122(short), 5123(ushort), 5125(uint), 5126(float)
```

```csharp
public class AccessorReader
{
    private readonly byte[] _binChunk;
    private readonly GltfDocument _doc;

    /// Resolve an accessor index to a typed span of the BIN chunk data
    public ReadOnlySpan<T> Read<T>(int accessorIndex) where T : unmanaged
    {
        var accessor = _doc.Accessors![accessorIndex];
        var view = _doc.BufferViews![accessor.BufferView!.Value];
        int offset = (view.ByteOffset ?? 0) + (accessor.ByteOffset ?? 0);
        int count = accessor.Count;
        return MemoryMarshal.Cast<byte, T>(_binChunk.AsSpan(offset, count * Unsafe.SizeOf<T>()));
    }
}
```

**Stride handling:** If `bufferView.byteStride` is set (interleaved data), elements are NOT contiguous. The simple `MemoryMarshal.Cast` path only works for tightly packed (non-interleaved) data. Must check stride and copy element-by-element if stride ≠ element size. Most glTF exporters produce tightly packed data, but the spec allows interleaving.

### Mesh extraction detail

```csharp
public static Mesh3D ExtractPrimitive(GltfDocument doc, AccessorReader reader, GltfMeshPrimitive primitive)
{
    // Required attributes
    var positions = reader.Read<Vector3>(primitive.Attributes["POSITION"]);
    var indices = primitive.Indices.HasValue
        ? reader.ReadIndices(primitive.Indices.Value)  // ushort or uint → ushort
        : GenerateSequentialIndices(positions.Length);

    // Optional attributes (fallback to defaults if missing)
    var normals = primitive.Attributes.TryGetValue("NORMAL", out var nIdx)
        ? reader.Read<Vector3>(nIdx) : GenerateFlatNormals(positions, indices);
    var uvs = primitive.Attributes.TryGetValue("TEXCOORD_0", out var uvIdx)
        ? reader.Read<Vector2>(uvIdx) : new Vector2[positions.Length];

    // Build MeshVertex3D array
    var vertices = new MeshVertex3D[positions.Length];
    for (int i = 0; i < positions.Length; i++)
    {
        vertices[i] = new MeshVertex3D
        {
            Position = positions[i],
            Normal = normals[i],
            UV = uvs[i],
            Color = Color.White,  // glTF vertex color is optional, default white
        };
    }
    return new Mesh3D(vertices, indices.ToArray());
}
```

### Open design decisions

**1. Index format — `ushort` only vs `uint` support**

| Option | Pros | Cons |
|--------|------|------|
| **`ushort` only** | Matches NoZ's `IndexFormat.Uint16`, no driver changes | Limits meshes to 65,535 vertices |
| **`uint` support** | Handles large meshes (>65K vertices) | Requires adding `IndexFormat.Uint32` to NoZ driver, fork change |

**Recommendation:** `ushort` only for Phase 4a. Most glTF sample models and game-ready assets are under 65K vertices. If a loaded model has `uint` indices, log a warning and skip it. Add `uint` index support as a future fork change when needed.

**2. Coordinate system — right-hand vs left-hand**

| Option | Pros | Cons |
|--------|------|------|
| **Right-hand (glTF native)** | No coordinate conversion needed, glTF models render correctly | Must be consistent throughout YesZ |
| **Left-hand** | Matches some game engines (Unity, DirectX) | Requires negating Z or swapping winding order on every import |

**Recommendation:** Right-hand. glTF uses right-hand Y-up. WebGPU's clip space is left-hand (Z goes into screen), but `Matrix4x4.CreatePerspectiveFieldOfView` handles this. No vertex data conversion needed.

### Gotchas addressed

- **Interleaved bufferViews:** If `byteStride` is set, can't use `MemoryMarshal.Cast<byte, T>` directly. Must iterate with stride-based offset. Check `view.ByteStride` and fall back to element-by-element copy.
- **Missing NORMAL attribute:** glTF allows meshes without normals. Generate flat normals from triangle cross products as fallback. Without normals, lit rendering (Phase 3b) produces incorrect results.
- **Missing TEXCOORD_0:** Default to `(0, 0)` for all vertices. The default white texture from Phase 2 will produce correct output.
- **Component type for indices:** glTF indices can be `UNSIGNED_BYTE` (5121), `UNSIGNED_SHORT` (5123), or `UNSIGNED_INT` (5125). Must handle all three and convert to `ushort`, rejecting if any index exceeds 65,535.
- **glTF JSON naming:** glTF uses camelCase property names (`bufferView`, `byteOffset`). `System.Text.Json` requires `[JsonPropertyName("bufferView")]` attributes or a `JsonNamingPolicy.CamelCase` option.
- **Endianness:** glTF BIN chunk is always little-endian. .NET on Windows is also little-endian, so `MemoryMarshal.Cast` works directly. If targeting big-endian platforms, byte-swap would be needed.

### Tests (unit)

This phase is 100% unit-testable — no GPU dependency. Can be developed test-first in parallel with everything.

**Test data:** Embed "Box.glb" and "BoxTextured.glb" from the [glTF sample models](https://github.com/KhronosGroup/glTF-Sample-Models) as test project embedded resources.

**`GlbReaderTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Parse_ValidGlb_ExtractsJsonAndBinChunks` | Correct chunk splitting |
| `Parse_ValidGlb_HeaderHasMagicGlTF` | Magic bytes = `0x46546C67` |
| `Parse_ValidGlb_VersionIs2` | glTF 2.0 |
| `Parse_ValidGlb_TotalLengthMatchesFileSize` | Header length field is accurate |
| `Parse_TruncatedFile_Throws` | Less than 12 bytes → error |
| `Parse_WrongMagic_Throws` | Non-glTF file → error |
| `Parse_JsonChunk_IsValidUtf8` | JSON chunk deserializes without errors |

**`GltfDocumentTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Deserialize_Box_HasOneMesh` | `Meshes.Length == 1` |
| `Deserialize_Box_HasOneScene` | `Scenes.Length == 1` |
| `Deserialize_Box_HasAccessors` | Accessor array is populated |
| `Deserialize_BoxTextured_HasMaterial` | Material with `pbrMetallicRoughness` present |
| `Deserialize_BoxTextured_HasImage` | Image referenced from material |

**`AccessorReaderTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Read_Vec3Float_ReturnsCorrectCount` | POSITION accessor returns expected vertex count |
| `Read_Vec2Float_ReturnsCorrectCount` | TEXCOORD_0 accessor returns expected UV count |
| `Read_ScalarUShort_ReturnsCorrectCount` | Index accessor returns expected index count |
| `Read_Vec3Float_ValuesInRange` | All positions within `[-0.5, 0.5]` for Box.glb |
| `Read_WithByteStride_HandlesInterleaved` | Strided data produces same result as non-strided |

**`MeshExtractorTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `ExtractPrimitive_Box_Returns24Vertices` | 4 per face × 6 faces |
| `ExtractPrimitive_Box_Returns36Indices` | 6 per face × 6 faces |
| `ExtractPrimitive_Box_NormalsAreUnitLength` | All normals have length ≈ 1.0 |
| `ExtractPrimitive_Box_UVsInZeroOneRange` | All UVs in `[0, 1]` |
| `ExtractPrimitive_MissingNormals_GeneratesFlatNormals` | Fallback path produces valid normals |
| `ExtractPrimitive_MissingUVs_DefaultsToZero` | All UVs = `(0, 0)` |
| `ExtractPrimitive_UintIndicesOver65535_ThrowsOrWarns` | Large indices rejected gracefully |

### Verification

- `dotnet build yesz.slnx` passes
- `dotnet test yesz.slnx` — all existing + new parser tests pass
- No rendering output (data only)

**Estimated scope:** ~3-4 files, L2

---

## Phase 4b: Model Rendering ✅

**Status:** Complete

**Milestone:** HelloCube loads and displays a textured glTF model (e.g., "BoxTextured.glb").

**Dependencies:** Phase 4a (parser, mesh extraction), Phase 2 (Material3D, texture loading)
**Fork changes:** None
**Enables:** Phase 4c (multi-mesh extends the loader)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `GltfLoader` | YesZ.Rendering | High-level API: `.glb` bytes → `Model3D` (meshes + materials + textures, GPU-ready) |
| `Model3D` | YesZ.Rendering | Renderable model container: `Mesh3D[]`, `Material3D[]`, per-mesh material index |
| glTF material → `Material3D` | YesZ.Rendering | Map `pbrMetallicRoughness` properties to `Material3D` fields |
| Embedded texture extraction | YesZ.Rendering | Extract image from glTF BIN chunk (PNG/JPG bytes) → decode → `CreateTexture` |
| `Graphics3D.DrawModel(Model3D, Matrix4x4)` | YesZ.Rendering | Iterates model's meshes, sets material, calls `DrawMesh` for each |
| HelloCube model display | HelloCube | Load "BoxTextured.glb" as embedded resource, render with `DrawModel` |

### glTF material mapping

```
glTF pbrMetallicRoughness          →  Material3D
─────────────────────────────────────────────────
baseColorFactor [r,g,b,a]          →  BaseColorFactor (Vector4)
baseColorTexture.index             →  BaseColorTexture (load + CreateTexture)
metallicFactor (float, default 1)  →  Metallic
roughnessFactor (float, default 1) →  Roughness
```

**Texture resolution chain:**
```
material.pbrMetallicRoughness.baseColorTexture.index
  → textures[i].source
    → images[j].bufferView
      → bufferViews[k].byteOffset + byteLength in BIN chunk
        → raw PNG/JPG bytes
          → StbImageSharp.ImageResult.FromMemory()
            → RGBA8 pixel array
              → Graphics.Driver.CreateTexture()
```

If `baseColorTexture` is absent, the material uses the default white texture. The `baseColorFactor` alone controls color.

### GltfLoader pipeline

```csharp
public static class GltfLoader
{
    public static Model3D Load(byte[] glbData)
    {
        // 1. Parse .glb container
        var (json, bin) = GlbReader.Parse(glbData);
        var doc = JsonSerializer.Deserialize<GltfDocument>(json);
        var reader = new AccessorReader(doc, bin);

        // 2. Load textures (deduplicated by image index)
        var textures = LoadTextures(doc, bin);

        // 3. Load materials (reference textures by index)
        var materials = LoadMaterials(doc, textures);

        // 4. Extract meshes (first primitive of each mesh for Phase 4b)
        var meshes = new List<(Mesh3D mesh, int materialIndex)>();
        foreach (var gltfMesh in doc.Meshes ?? [])
        {
            var prim = gltfMesh.Primitives[0];  // Phase 4b: first primitive only
            var mesh = MeshExtractor.ExtractPrimitive(doc, reader, prim);
            meshes.Add((mesh, prim.Material ?? 0));
        }

        return new Model3D(meshes, materials);
    }
}
```

### Open design decisions

**1. Model3D ownership — who creates GPU resources?**

| Option | Pros | Cons |
|--------|------|------|
| **GltfLoader creates GPU resources** | Simple, load-and-render in one call | Loader depends on Graphics3D being initialized |
| **Two-stage: parse → upload** | Parsing testable without GPU, can cache parsed data | More complex API (parse result intermediate type) |

**Recommendation:** GltfLoader creates GPU resources directly. The two-stage approach is cleaner architecturally but adds complexity for no immediate benefit — we're not caching parsed data or loading on background threads yet.

**2. Model file distribution — embedded resource vs file path**

| Option | Pros | Cons |
|--------|------|------|
| **Embedded resource** | No file path management, works on all platforms | Binary bloats the assembly, no hot-reload |
| **File path** | Easy to swap models, hot-reload possible | Platform-specific path handling |

**Recommendation:** Support both. `GltfLoader.Load(byte[])` accepts raw bytes from any source. Samples use embedded resources; games use file paths.

### Gotchas addressed

- **Missing material:** If a mesh primitive has no `material` property, use material index 0 or a default material. The glTF spec says missing material = default white, non-metallic, rough=1.
- **Image MIME type:** glTF images specify `mimeType` (`image/png` or `image/jpeg`). StbImageSharp auto-detects format from magic bytes, so the MIME type is informational only.
- **sRGB texture data:** glTF base color textures are in sRGB color space. WebGPU's `Rgba8Unorm` format does NOT automatically convert sRGB → linear. For correct PBR, textures should use `Rgba8UnormSrgb` format (which is different from `Rgba8Unorm`). However, NoZ's `TextureFormat.RGBA8` maps to `Rgba8Unorm`. Accept this as a known limitation — colors will be slightly off until NoZ gains `RGBA8_SRGB` format support.
- **Texture deduplication:** Multiple materials may reference the same image. Load each image once and share the GPU texture handle.

### Tests (unit)

**`GltfLoaderTests`** (YesZ.Rendering.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Load_BoxTextured_ReturnsMeshWithTexture` | Material has non-default texture handle |
| `Load_Box_ReturnsCorrectMeshCount` | `Model3D.Meshes.Length` matches glTF mesh count |
| `Load_MissingMaterial_UsesDefault` | Missing `material` property → default white material |
| `Load_BaseColorFactor_MapsCorrectly` | glTF `[1, 0, 0, 1]` → `Material3D.BaseColorFactor = (1, 0, 0, 1)` |
| `Load_MetallicRoughness_MapsCorrectly` | glTF PBR values propagate to `Material3D` |

**Note:** `GltfLoaderTests` require Graphics3D to be initialized (GPU resource creation). If this is impractical in CI, these become integration tests run locally. The parser tests from Phase 4a provide the safety net.

### Verification

- `dotnet build yesz.slnx` passes
- `dotnet test yesz.slnx` — all existing + new tests pass
- HelloCube opens and displays a textured glTF model (e.g., "BoxTextured" with crate texture)
- Material properties (base color factor, texture) applied correctly
- Model renders with correct lighting if Phase 3b is complete, unlit otherwise
- A model without textures renders with `BaseColorFactor` color only (via default white texture)
- No WebGPU validation errors

**Estimated scope:** ~3-4 files, L2

---

## Phase 4c: Multi-Mesh + Node Hierarchy ✅

**Status:** Complete

**Milestone:** Complex multi-part glTF model renders correctly with all parts positioned per the node hierarchy.

**Dependencies:** Phase 4b (single-mesh model rendering)
**Fork changes:** None
**Enables:** Phase 8a (scene graph architecture informed by glTF node traversal patterns)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| Multi-primitive support | YesZ.Core | Handle meshes with multiple primitives (each with its own material and vertex data) |
| `GltfNode` transform parsing | YesZ.Core | Parse per-node TRS (translation/rotation/scale) or matrix from glTF JSON |
| `ModelNode` | YesZ.Rendering | Tree node: local `Matrix4x4` transform, optional mesh reference, children list |
| Transform composition | YesZ.Rendering | Recursive traversal: `worldTransform = parent.worldTransform * node.localTransform` |
| `Model3D` hierarchy | YesZ.Rendering | `Model3D` stores root `ModelNode`, all meshes/materials, provides `Draw(Matrix4x4 rootTransform)` |
| `Graphics3D.DrawModel()` update | YesZ.Rendering | Recursively draws all nodes with composed world transforms |
| Tests | YesZ.Core.Tests | Node hierarchy transform composition, TRS → matrix conversion |

### glTF node transform resolution

Each glTF node has either a `matrix` (16 floats, column-major) OR `translation` + `rotation` + `scale` (TRS) — never both:

```csharp
public Matrix4x4 ResolveLocalTransform(GltfNode node)
{
    if (node.Matrix != null)
    {
        // Column-major float[16] → row-major Matrix4x4
        return new Matrix4x4(
            node.Matrix[0],  node.Matrix[4],  node.Matrix[8],  node.Matrix[12],
            node.Matrix[1],  node.Matrix[5],  node.Matrix[9],  node.Matrix[13],
            node.Matrix[2],  node.Matrix[6],  node.Matrix[10], node.Matrix[14],
            node.Matrix[3],  node.Matrix[7],  node.Matrix[11], node.Matrix[15]
        );
    }

    var T = node.Translation ?? new float[] { 0, 0, 0 };
    var R = node.Rotation ?? new float[] { 0, 0, 0, 1 };  // xyzw quaternion, identity
    var S = node.Scale ?? new float[] { 1, 1, 1 };

    return Matrix4x4.CreateScale(S[0], S[1], S[2])
         * Matrix4x4.CreateFromQuaternion(new Quaternion(R[0], R[1], R[2], R[3]))
         * Matrix4x4.CreateTranslation(T[0], T[1], T[2]);
}
```

**Column-major to row-major:** glTF stores matrices in column-major order. `System.Numerics.Matrix4x4` is row-major. The constructor transposition above handles this. Alternatively, load the 16 floats sequentially and call `Matrix4x4.Transpose()`.

### Recursive model drawing

```csharp
public void DrawModel(Model3D model, Matrix4x4 rootTransform)
{
    DrawNode(model.RootNode, rootTransform, model);
}

private void DrawNode(ModelNode node, Matrix4x4 parentWorld, Model3D model)
{
    var world = node.LocalTransform * parentWorld;

    if (node.MeshIndex >= 0)
    {
        var meshGroup = model.MeshGroups[node.MeshIndex];
        foreach (var (mesh, materialIndex) in meshGroup.Primitives)
        {
            SetMaterial(model.Materials[materialIndex]);
            DrawMesh(mesh, world);
        }
    }

    foreach (var child in node.Children)
        DrawNode(child, world, model);
}
```

### Open design decisions

**1. Scene selection — default scene vs all scenes**

| Option | Pros | Cons |
|--------|------|------|
| **Default scene only** (`doc.Scene` index) | Simple, matches viewer behavior | Ignores other scenes in the file |
| **All scenes** | Access to everything | Multiple root nodes, more complex API |

**Recommendation:** Default scene only. If `doc.Scene` is null, use scene 0. Multiple scenes are rare in game assets.

**2. Mesh group storage — flat array vs tree**

| Option | Pros | Cons |
|--------|------|------|
| **Flat array + per-node index** | Simple iteration, cache-friendly | Node-mesh relationship is indirect |
| **Tree with embedded meshes** | Natural recursive traversal | Harder to batch across the tree |

**Recommendation:** Flat array. `Model3D.MeshGroups[]` stores all mesh groups; each `ModelNode` stores an index into this array (-1 for non-mesh nodes). Draw traversal is recursive, but mesh data access is flat.

### Gotchas addressed

- **TRS order:** glTF specifies TRS application order as `T × R × S` (scale first, then rotate, then translate). `System.Numerics` multiplies left-to-right for transforms applied to the right: `S * R * T`. Ensure the correct multiplication order.
- **Quaternion component order:** glTF quaternions are `[x, y, z, w]`, matching `System.Numerics.Quaternion(X, Y, Z, W)`. No swizzle needed.
- **Non-mesh nodes:** Many glTF nodes exist only for hierarchy (grouping, pivots). Skip rendering for nodes without a `mesh` property.
- **Column-major matrices:** glTF matrices are column-major float[16]. Must transpose when loading into row-major `Matrix4x4`. Getting this wrong produces sheared/inverted geometry.
- **Shared mesh across nodes:** Multiple nodes can reference the same `mesh` index with different transforms (e.g., left/right wheel). The mesh data is shared; only the world transform differs.

### Tests (unit)

**`NodeTransformTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `ResolveLocal_IdentityTRS_ProducesIdentity` | `T=(0,0,0)`, `R=(0,0,0,1)`, `S=(1,1,1)` → `Matrix4x4.Identity` |
| `ResolveLocal_TranslationOnly_CorrectMatrix` | `T=(1,2,3)` → `M41=1, M42=2, M43=3` |
| `ResolveLocal_RotationOnly_CorrectMatrix` | 90° Y rotation matches `Quaternion.CreateFromAxisAngle` |
| `ResolveLocal_ColumnMajorMatrix_TransposedCorrectly` | Column-major float[16] → row-major `Matrix4x4` matches expected |
| `ResolveLocal_TRS_OrderIsScaleRotateTranslate` | `S(2) * R(90°Y) * T(1,0,0)` produces correct composed result |
| `HierarchyComposition_ChildWorld_IsLocalTimesParent` | `child.WorldMatrix = child.Local * parent.World` |
| `HierarchyComposition_ThreeLevels_ComposesCorrectly` | Grandchild world = grandchild.local × child.local × root.local |

### Verification

- `dotnet build yesz.slnx` passes
- `dotnet test yesz.slnx` — all existing + new tests pass
- HelloCube loads a multi-part model (e.g., "DamagedHelmet" or a custom multi-node .glb)
- All parts positioned/rotated correctly per glTF node hierarchy
- Each primitive uses its own material (different textures/colors)
- Rotating the root transform rotates the entire model as a unit

**Estimated scope:** ~2-3 files, L2

---

## Phase 4: Out of Scope

The following are **not** part of Phase 4. They may become future phases if needed.

- **Morph targets / blend shapes** — vertex displacement animation (e.g., facial expressions). Requires per-morph-target vertex buffers and additive blending in the vertex shader. Orthogonal to skeletal animation.
- **glTF extensions** — KHR_draco_mesh_compression (compressed vertex data), KHR_texture_transform (UV transform matrix), KHR_materials_unlit (force unlit rendering), KHR_materials_pbrSpecularGlossiness (legacy PBR model). None are needed for core functionality.
- **External .gltf + .bin** — only self-contained `.glb` binary format is supported. Separate `.gltf` JSON + external `.bin` + external image files add file management complexity with no benefit for embedded game assets.
- **Sparse accessors** — glTF sparse accessor format for efficiently storing mostly-zero/mostly-default data. Rarely used in practice and adds accessor resolution complexity.
- **`uint` index format** — meshes with >65,535 vertices require 32-bit indices. NoZ uses `IndexFormat.Uint16` only. Adding `Uint32` support is a fork change deferred until a real model requires it.

Note: Skeletal animation was previously listed here — it is now Phase 5.

---

## Phase 5a: Skeleton & Animation Data (CPU-side) ✅

**Status:** Complete

**Milestone:** Unit tests parse a skinned `.glb` file (e.g., "RiggedSimple"), produce correct skeleton hierarchy, inverse bind matrices, and animation keyframe data.

**Dependencies:** Phase 4a (glTF JSON model, accessor resolver)
**Fork changes:** None
**Enables:** Phase 5b (skinned vertex format), Phase 5c (animation playback)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| glTF `skins` deserialization | YesZ.Core | Extend glTF JSON model with `skins[]` array (joints, inverseBindMatrices, skeleton) |
| glTF `animations` deserialization | YesZ.Core | Extend glTF JSON model with `animations[]` array (channels, samplers) |
| `Skeleton3D` | YesZ.Core | Joint hierarchy: parent indices, joint-to-node mapping, inverse bind matrices (`Matrix4x4[]`) |
| `AnimationClip3D` | YesZ.Core | Baked keyframe data: per-channel arrays of `(float time, TRS value)`, interpolation mode per channel |
| `AnimationChannel3D` | YesZ.Core | Target joint index + animated property (translation / rotation / scale) + keyframe array |
| glTF skin → `Skeleton3D` | YesZ.Core | Read joint node indices, resolve IBM accessor → `Matrix4x4[]`, build parent-index array from node `children` |
| glTF animation → `AnimationClip3D` | YesZ.Core | Read sampler input/output accessors → typed keyframe arrays, resolve channel targets to joint indices |
| Joint matrix math | YesZ.Core | `ComputeJointMatrices(Skeleton3D, Span<Matrix4x4> localPoses)` → `Span<Matrix4x4>` final joint matrices |
| Tests | YesZ.Core.Tests | Skeleton hierarchy traversal, IBM application, joint matrix computation, accessor parsing for skin/anim data |

### glTF skin parsing detail

The `skins` object maps joint nodes and bind-pose data:

```json
{
  "skins": [{
    "joints": [1, 2, 3, 7, 8],
    "inverseBindMatrices": 29,
    "skeleton": 1
  }]
}
```

- `joints[]` — ordered list of node indices. Order defines joint index 0..N-1 used by `JOINTS_0` vertex attribute
- `inverseBindMatrices` — accessor to `MAT4` array (column-major), one per joint. Omitted = identity
- `skeleton` — optional root node; closest common ancestor of all joints

Joint matrix computation per frame:

```
globalTransform[j] = globalTransform[parent[j]] * localTransform(node[j])
jointMatrix[j]     = globalTransform[joints[j]] * inverseBindMatrix[j]
```

### glTF animation parsing detail

```json
{
  "animations": [{
    "channels": [{ "sampler": 0, "target": { "node": 2, "path": "rotation" } }],
    "samplers": [{ "input": 4, "output": 5, "interpolation": "LINEAR" }]
  }]
}
```

| `target.path` | Output type | Per-keyframe size |
|---------------|-------------|-------------------|
| `translation` | `VEC3` float | 12 bytes |
| `rotation` | `VEC4` float (xyzw quaternion) | 16 bytes |
| `scale` | `VEC3` float | 12 bytes |

Interpolation modes: `LINEAR` (lerp / slerp), `STEP` (snap to previous), `CUBICSPLINE` (Hermite — 3× output values per keyframe: in-tangent, value, out-tangent).

### Tests (unit)

This phase is 100% unit-testable. Can be developed test-first after Phase 4a.

**Test data:** Embed "RiggedSimple.glb" from glTF sample models.

**`SkeletonParserTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Parse_RiggedSimple_CorrectJointCount` | `Skeleton3D.JointCount` matches glTF skin joints array |
| `Parse_RiggedSimple_ParentIndicesFormTree` | Root has parent = -1, children reference valid parents |
| `Parse_RiggedSimple_IBMsAreInvertible` | Each IBM × bind-pose global = identity (sanity check) |
| `Parse_NoIBM_DefaultsToIdentity` | Missing `inverseBindMatrices` accessor → all identity |

**`AnimationParserTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Parse_RiggedSimple_HasAnimation` | At least one `AnimationClip3D` extracted |
| `Parse_RiggedSimple_KeyframeTimestampsAscending` | Timestamps are monotonically increasing |
| `Parse_RiggedSimple_ChannelTargetsValidJoints` | All channel target indices < `JointCount` |
| `Parse_RotationChannel_HasVec4Keyframes` | Rotation output is quaternion (4 components) |
| `Parse_TranslationChannel_HasVec3Keyframes` | Translation output is 3 components |

**`JointMatrixTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Compute_BindPose_AllJointMatricesNearIdentity` | In bind pose, `global × IBM ≈ Identity` for all joints |
| `Compute_RotatedJoint_ProducesExpectedTransform` | Rotate one joint 90° → joint matrix reflects rotation |
| `Compute_HierarchyOrder_ChildInheritsParent` | Parent rotation propagates to child joint |

### Verification

- `dotnet build yesz.slnx` passes
- `dotnet test yesz.slnx` — all existing + new skeleton/animation tests pass
- No rendering output (data only)

**Estimated scope:** ~4-5 files, L2

---

## Phase 5b: Skinned Vertex Format + Shader Variant ✅

**Status:** Complete

**Milestone:** `SkinnedMeshVertex3D` compiles, registers with the graphics driver, and the skinned pipeline variant is selectable via `ShaderFlags`.

**Dependencies:** Phase 1b (MeshVertex3D, vertex format system, IVertex), Phase 5a (Skeleton3D, skin data)
**Fork changes:** `ShaderFlags.Skinned` added to `ShaderFlags` enum in `Shader.cs`
**Enables:** Phase 5d

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `SkinnedMeshVertex3D` | YesZ.Core | Vertex struct: `Vector3 Position`, `Vector3 Normal`, `Vector2 UV`, `Color Color`, `UShort4 JointIndices`, `Vector4 JointWeights` |
| `SkinnedMesh3D` | YesZ.Core | Mesh container: `SkinnedMeshVertex3D[]` vertices + `ushort[]` indices + `Skeleton3D` reference |
| glTF skinned mesh extraction | YesZ.Core | Parse `JOINTS_0` (VEC4 ubyte/ushort) and `WEIGHTS_0` (VEC4 float/unorm) from mesh primitive accessors |
| Fork: `ShaderFlags.Skinned` | engine/noz | New flag value `1 << 4` for skinned shader pipeline variant |
| Skinned shader source | YesZ.Rendering | WGSL vertex shader with joint matrix lookup + vertex blending (shader source only, not yet connected to rendering) |
| Tests | YesZ.Core.Tests | Vertex format layout validation, JOINTS/WEIGHTS accessor parsing, weight normalization |

### Vertex format

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct SkinnedMeshVertex3D : IVertex
{
    public Vector3 Position;        // location 0 — 3x float
    public Vector3 Normal;          // location 1 — 3x float
    public Vector2 UV;              // location 2 — 2x float
    public Color   Color;           // location 3 — 4x unorm8
    public ushort  Joint0, Joint1, Joint2, Joint3;  // location 4 — 4x uint16
    public Vector4 JointWeights;    // location 5 — 4x float
}
```

This extends `MeshVertex3D` with two additional attributes for skinning. The 4-influence limit (one set of JOINTS/WEIGHTS) covers the vast majority of glTF models.

### JOINTS_0 / WEIGHTS_0 parsing

| glTF Attribute | Accessor Type | Component Types | Notes |
|----------------|--------------|-----------------|-------|
| `JOINTS_0` | VEC4 | UNSIGNED_BYTE, UNSIGNED_SHORT | Indices into `skin.joints[]`, NOT into `nodes[]` |
| `WEIGHTS_0` | VEC4 | FLOAT, UNSIGNED_BYTE (normalized), UNSIGNED_SHORT (normalized) | Must sum to 1.0 per vertex — normalize at load time |

### Tests (unit)

**`SkinnedMeshVertex3DTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `GetFormatDescriptor_HasCorrectStride` | Stride accounts for all fields including joints + weights |
| `GetFormatDescriptor_Has6Attributes` | Position(0), Normal(1), UV(2), Color(3), Joints(4), Weights(5) |
| `GetFormatDescriptor_JointsAreUInt16` | Joint attribute type is `VertexAttribType.UShort` |
| `VertexHash_DiffersFromMeshVertex3D` | Skinned hash ≠ unskinned hash (separate pipeline) |

**`SkinDataExtractionTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Extract_RiggedSimple_VertexCountMatchesMesh` | Skinned vertex count = original vertex count |
| `Extract_RiggedSimple_JointIndicesInRange` | All joint indices < `Skeleton3D.JointCount` |
| `Extract_RiggedSimple_WeightsSumToOne` | Per-vertex weight sum ≈ 1.0 (within tolerance) |
| `Extract_UnnormalizedWeights_AreNormalized` | Weights that don't sum to 1.0 are corrected at load time |

### Verification

- `dotnet build yesz.slnx` passes
- `dotnet test yesz.slnx` — all existing + new tests pass

**Estimated scope:** ~3-4 files, L2

---

## Phase 5c: Animation Playback ✅

**Status:** Complete

**Milestone:** `AnimationPlayer3D` can sample any glTF animation clip and produce correct per-joint local transforms at any time value. LINEAR and STEP interpolation modes work. Pure math — no rendering or GPU involvement.

**Dependencies:** Phase 5a (Skeleton3D, AnimationClip3D)
**Fork changes:** None
**Enables:** Phase 5d
**Parallel with:** Phase 5b (no shared dependencies beyond 5a)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `AnimationPlayer3D` | YesZ.Core | Stateful player: current clip, elapsed time, playback speed, looping mode |
| Keyframe sampler | YesZ.Core | Binary search for bracket keyframes given a timestamp |
| LINEAR interpolation | YesZ.Core | `Vector3.Lerp` for translation/scale; quaternion slerp for rotation |
| STEP interpolation | YesZ.Core | Snap to previous keyframe value |
| CUBICSPLINE interpolation | YesZ.Core | Hermite spline with tangent scaling (`dt * tangent`) and post-normalization for quaternions |
| `QuaternionUtils` | YesZ.Core | `Slerp` with short-path selection (negate if `dot < 0`), `Normalize`, component-order conversion (`xyzw` ↔ `wxyz`) |
| Pose evaluation | YesZ.Core | Sample all channels at time `t` → `Span<TRS>` local poses, one per joint |
| Joint hierarchy traversal | YesZ.Core | Forward pass: compose `localMatrix × parentGlobalMatrix` for each joint in hierarchy order |
| Final joint matrix computation | YesZ.Core | `globalTransform[j] * inverseBindMatrix[j]` for each joint |
| Tests | YesZ.Core.Tests | Interpolation accuracy, slerp edge cases, cubicspline tangent scaling, looping wraparound, full-clip playback vs known-good data |

### Interpolation detail

**LINEAR** — the default and most common mode:
- Translation / scale: `lerp(v0, v1, t)` where `t = (time - t0) / (t1 - t0)`
- Rotation: `slerp(q0, q1, t)` — must check `dot(q0, q1)` and negate one quaternion if negative to ensure shortest arc

**STEP** — snap to previous:
- Return `output[i]` where `timestamps[i] <= time < timestamps[i+1]`

**CUBICSPLINE** — Hermite spline:

Output accessor has 3× keyframe count values. Per keyframe `i`, layout is: `[in_tangent | value | out_tangent]`.

```
t  = (time - t_i) / (t_{i+1} - t_i)
dt = t_{i+1} - t_i

p0 = value_i,              p1 = value_{i+1}
m0 = dt * out_tangent_i,   m1 = dt * in_tangent_{i+1}

result = (2t³ - 3t² + 1)*p0 + (t³ - 2t² + t)*m0 + (-2t³ + 3t²)*p1 + (t³ - t²)*m1
```

For rotation channels: **normalize the result** after Hermite evaluation (tangent arithmetic produces non-unit quaternions).

### Gotchas addressed

- **Missing channel = bind-pose, not identity.** If an animation doesn't drive a joint, use the node's original glTF TRS — not `Matrix4x4.Identity`
- **glTF quaternion order is `[x, y, z, w]`**, not `[w, x, y, z]` as in some engine conventions. `System.Numerics.Quaternion` uses `(X, Y, Z, W)` which matches glTF — no swizzle needed
- **Cubicspline tangents need `dt` scaling** before plugging into Hermite formula — easy to miss
- **Looping**: when `time > clip.Duration`, wrap with `time % duration` and re-sample. Handle boundary case where wrapped time lands exactly on the last keyframe

### Tests (unit)

This phase is 100% unit-testable. Can be developed test-first in parallel with Phase 5b.

**`InterpolationTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `LinearLerp_Midpoint_ReturnsAverage` | `Lerp((0,0,0), (2,2,2), 0.5) = (1,1,1)` |
| `LinearLerp_AtT0_ReturnsStart` | Boundary condition |
| `LinearLerp_AtT1_ReturnsEnd` | Boundary condition |
| `Slerp_IdentityToRotation_Midpoint` | Midpoint between identity and 90° is 45° |
| `Slerp_ShortPath_NegatesDotLessThanZero` | Opposite quaternions → takes shortest arc |
| `Slerp_NearlyIdentical_DoesNotNaN` | Very small angle doesn't produce NaN |
| `Step_BeforeKeyframe_ReturnsPrevious` | `t < t1` → value at `t0` |
| `Step_ExactlyAtKeyframe_ReturnsCurrent` | `t = t1` → value at `t1` |
| `CubicSpline_Midpoint_MatchesHermite` | Known tangents + values → verify Hermite formula |
| `CubicSpline_Rotation_ResultIsNormalized` | Post-Hermite quaternion is unit length |

**`AnimationPlayerTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Sample_AtT0_ReturnsFirstKeyframeValues` | Start of clip = first keyframe exactly |
| `Sample_AtDuration_ReturnsLastKeyframeValues` | End of clip = last keyframe exactly |
| `Sample_AtMidpoint_InterpolatesBetweenKeyframes` | Values between keyframes are interpolated |
| `Sample_Looping_WrapsCorrectly` | `time > duration` wraps to `time % duration` |
| `Sample_Looping_BoundaryCase_ExactDuration` | Wraps to t=0 (not t=duration) |
| `Sample_UnanimatedJoint_UsesBindPose` | Joints with no animation channel keep original TRS |
| `JointMatrices_AtBindPose_NearIdentity` | Full pipeline: sample → compose → apply IBM ≈ identity |
| `JointMatrices_AtKnownPose_MatchesExpected` | RiggedSimple at t=0.5 → hand-computed joint transforms |

**`QuaternionUtilsTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Normalize_UnitQuaternion_Unchanged` | Normalizing already-unit quat is idempotent |
| `Normalize_ScaledQuaternion_ReturnsUnit` | `(0, 0, 0, 2)` → `(0, 0, 0, 1)` |
| `Slerp_ShortPathSelection_ReturnsShortestArc` | 350° path → takes 10° arc instead |

### Verification

- `dotnet build yesz.slnx` passes
- `dotnet test yesz.slnx` — all existing + new animation tests pass
- No rendering output (data only)

**Estimated scope:** ~3-4 files, L2

---

## Phase 5d: GPU Skinning + Rendering ✅

**Status:** Complete

**Milestone:** Sample app loads a skinned `.glb` model (e.g., "RiggedSimple" or "CesiumMan") and displays it with animated skeletal motion.

**Dependencies:** Phase 5b (skinned vertex format, shader variant), Phase 5c (animation playback), Phase 2 (materials)
**Fork changes:** None
**Enables:** Future animation blending, IK, morph targets (not currently planned)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| Joint matrix upload | YesZ.Rendering | Per-frame upload of `Matrix4x4[]` joint matrices to GPU |
| Skinned WGSL shader | YesZ.Rendering | Vertex shader: blend position/normal by `weights × jointMatrices[indices]` |
| `Graphics3D.DrawSkinnedMesh()` | YesZ.Rendering | API: set skeleton pose, bind joint data, submit skinned draw calls |
| Skinned model loader | YesZ.Rendering | Extend `GltfLoader` to detect skinned meshes and produce `SkinnedMesh3D` + `Skeleton3D` + `AnimationClip3D[]` |
| Sample app | HelloCube or new sample | Load skinned `.glb`, run `AnimationPlayer3D`, render each frame |
| Integration tests | YesZ.Rendering.Tests | Joint matrix upload binding, skinned draw call submission |

### Skinning shader (WGSL)

```wgsl
// Joint matrices uploaded as uniform or via bone texture
@group(0) @binding(N) var<uniform> jointMatrices: array<mat4x4f, 128>;

@vertex fn vs_main(in: VertexInput) -> VertexOutput {
    let skin =
        in.weights.x * jointMatrices[u32(in.joints.x)] +
        in.weights.y * jointMatrices[u32(in.joints.y)] +
        in.weights.z * jointMatrices[u32(in.joints.z)] +
        in.weights.w * jointMatrices[u32(in.joints.w)];

    let worldPos = skin * vec4f(in.position, 1.0);
    let worldNormal = normalize((skin * vec4f(in.normal, 0.0)).xyz);
    // ... projection, pass to fragment shader
}
```

Normal transform uses `w=0` so translation doesn't affect normals. For non-uniform scale, the correct approach is the inverse-transpose of the upper 3×3 — same as the static mesh case from Phase 3b.

### Open design decision: bone data upload mechanism

Joint matrices need to reach the GPU each frame. Three options:

| Option | Pros | Cons | Fork changes |
|--------|------|------|-------------|
| **Uniform buffer** via `SetUniform()` | Already supported by NoZ, zero fork changes, simple WGSL (`array<mat4x4f, N>`) | 64KB limit = ~1024 joints theoretical, ~256 practical with other uniforms | None |
| **Bone texture** (NoZ pattern) | NoZ already uses RGBA32F bone texture for 2D skinning, proven pattern | 3D needs 4 texels/bone (vs 2 for 2D Matrix3x2), textureLoad syntax less clean than array access | None |
| **Storage buffer** | Clean WGSL (`var<storage, read>`), no size limit, modern approach | Requires adding `StorageBuffer` to `ShaderBindingType` enum + `CreateStorageBuffer` to driver | Yes — `IGraphicsDriver`, `WebGPUGraphicsDriver` |

**Recommendation:** Start with **uniform buffer** via `SetUniform()`. At `128 joints × 64 bytes = 8 KB`, it's well within the 64KB limit. Real character rigs are typically 30–150 joints. No fork changes, no new driver API, works today. If we hit the limit later, migrate to bone texture or storage buffer.

### Gotchas addressed

- **Skinned mesh node transform is ignored.** The glTF spec says: when a mesh is skinned, the node's own world transform is NOT applied. The joint matrices absorb full world placement. Applying both double-transforms everything
- **Joint indices index into `skin.joints[]`**, not into `nodes[]` directly. The vertex shader uses these to look up into the `jointMatrices` array
- **Weight normalization**: weights must sum to 1.0. Normalize at load time (Phase 5b) as a safety measure, even though the spec requires it

### Tests (unit)

Most of this phase is visual/integration, but the joint matrix upload can be validated.

**`SkinnedRenderingTests`** (YesZ.Rendering.Tests — new file)

| Test | What it proves |
|------|---------------|
| `JointMatrixBuffer_CorrectSize` | UBO size = `jointCount × 64` bytes, within 64KB limit |
| `JointMatrixBuffer_BindPose_AllNearIdentity` | Sanity: uploading bind-pose matrices → all ≈ identity |

### Verification

- Sample app opens and displays an animated skinned model
- Skeleton deforms mesh correctly — limbs bend at joints, no vertex popping
- Animation plays smoothly at correct speed (matches glTF clip duration)
- Looping animation wraps seamlessly
- Material/texture applied to skinned mesh (if model has them)
- Lighting applied correctly if Phase 3b is complete
- Non-skinned rendering still works (existing Phase 1b–4c content unaffected)

**Estimated scope:** ~4-5 files, L2

---

## Phase 5: Out of Scope

The following are **not** part of Phase 5. They may become future phases if needed.

- **Animation blending / crossfade** — NoZ's 2D `Animator` supports `CrossFade()` with `SmoothStep` blending. A 3D equivalent would blend between two animation poses. Conceptually straightforward (lerp/slerp between two sets of local TRS per joint) but adds state management complexity.
- **Animation state machine / animation graph** — finite state machine driving animation transitions (idle → walk → run). Common in game engines but a significant system on its own.
- **Inverse kinematics (IK)** — procedural joint positioning (e.g., feet on uneven ground, hands reaching for objects). Multiple solver types (CCD, FABRIK, two-bone analytical).
- **Root motion extraction** — deriving character movement from the animation's root bone translation rather than driving it procedurally.
- **Morph targets / blend shapes** — vertex displacement animation (e.g., facial expressions). Separate vertex attribute type and additive blending in shader. Orthogonal to skeletal animation.
- **>4 bone influences per vertex** — `JOINTS_1` / `WEIGHTS_1` attributes for vertices influenced by more than 4 joints. Rare in practice and doubles the skinning cost.
- **Animation compression / quantization** — smaller keyframe storage via quantized quaternions, curve fitting, or keyframe reduction. Optimization work, not core functionality.
- **GPU compute skinning** — compute shader approach instead of vertex shader skinning. Better for shared meshes drawn multiple times, but adds compute pipeline complexity.
- **Animation events / callbacks** — trigger game logic at specific animation times (footstep sounds, hit frames). NoZ's 2D system has per-frame event bytes — a 3D equivalent would need a similar mechanism.
- **Animation retargeting** — playing an animation authored for one skeleton on a different skeleton with different proportions.

---

## Phase 6a: Shadow Map Infrastructure ✅ Complete

**Milestone:** A depth-only render pass executes from a directional light's perspective and produces a valid shadow depth texture. No visual output change yet.

**Dependencies:** Phase 3c (multi-light system provides light direction), Phase 1a (depth pipeline — depth texture creation pattern reused)
**Fork changes:** `IGraphicsDriver` — add `CreateDepthOnlyTexture(width, height)` or extend `CreateRenderTexture` with depth-only format; `WebGPUGraphicsDriver.RenderPass.cs` — new depth-only pass variant
**Enables:** Phase 6b (shadow sampling uses the depth texture produced here)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `ShadowConfig` | YesZ.Rendering | Shadow map parameters: resolution, near/far, bias values |
| Light-space view matrix | YesZ.Core | `Matrix4x4.CreateLookAt()` from light direction → light-space view |
| Light-space orthographic projection | YesZ.Core | `Matrix4x4.CreateOrthographic()` sized to tightly fit the camera frustum |
| Frustum-to-light-space fitting | YesZ.Core | Project camera frustum corners into light space → compute AABB → ortho bounds |
| Shadow depth texture | YesZ.Rendering | `Depth24Plus` texture at configurable resolution (e.g., 2048×2048) |
| Depth-only render pass | YesZ.Rendering | New pass type: no color attachment, depth-only, renders scene geometry from light viewpoint |
| Depth-only shader | YesZ.Rendering | Minimal WGSL: vertex transform only (model × lightViewProj), no fragment output |
| `Graphics3D.RenderShadowPass()` | YesZ.Rendering | Orchestrates: create depth pass → bind depth shader → draw all shadow-casting meshes → end pass |
| Tests | YesZ.Core.Tests | Ortho projection fitting, frustum corner computation, light-space AABB |

### Light-space matrix computation

```csharp
public static (Matrix4x4 view, Matrix4x4 proj) ComputeLightSpaceMatrices(
    DirectionalLight light, Camera3D camera, float shadowDistance)
{
    // 1. Get camera frustum corners in world space (clipped to shadowDistance)
    var corners = camera.GetFrustumCorners(camera.NearPlane, shadowDistance);

    // 2. Compute frustum center
    var center = Vector3.Zero;
    foreach (var c in corners) center += c;
    center /= corners.Length;

    // 3. Light view matrix: look from above the center, along light direction
    var lightDir = Vector3.Normalize(light.Direction);
    var lightView = Matrix4x4.CreateLookAt(
        center - lightDir * shadowDistance,  // eye (behind the scene)
        center,                              // target
        Vector3.UnitY);                      // up (may need adjustment if light is vertical)

    // 4. Transform frustum corners to light space → compute tight AABB
    float minX = float.MaxValue, maxX = float.MinValue;
    float minY = float.MaxValue, maxY = float.MinValue;
    float minZ = float.MaxValue, maxZ = float.MinValue;
    foreach (var corner in corners)
    {
        var p = Vector3.Transform(corner, lightView);
        minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
        minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
        minZ = Math.Min(minZ, p.Z); maxZ = Math.Max(maxZ, p.Z);
    }

    // 5. Orthographic projection from the AABB
    var lightProj = Matrix4x4.CreateOrthographicOffCenter(minX, maxX, minY, maxY, minZ, maxZ);

    return (lightView, lightProj);
}
```

### Depth-only render pass (fork change)

NoZ's existing render passes always have a color attachment. A depth-only pass requires a new path:

```csharp
// New method in WebGPUGraphicsDriver
public void BeginDepthOnlyPass(TextureView* depthView, uint width, uint height)
{
    _state = default;
    _state.HasDepthAttachment = true;
    _state.PipelineDirty = true;
    _state.BindGroupDirty = true;

    var depthAttachment = new RenderPassDepthStencilAttachment
    {
        View = depthView,
        DepthLoadOp = LoadOp.Clear,
        DepthStoreOp = StoreOp.Store,
        DepthClearValue = 1.0f,
        DepthReadOnly = false,
        StencilLoadOp = LoadOp.Undefined,
        StencilStoreOp = StoreOp.Undefined,
        StencilReadOnly = true,
    };

    var desc = new RenderPassDescriptor
    {
        ColorAttachments = null,
        ColorAttachmentCount = 0,
        DepthStencilAttachment = &depthAttachment,
    };

    _currentRenderPass = _wgpu.CommandEncoderBeginRenderPass(_commandEncoder, in desc);
    _wgpu.RenderPassEncoderSetViewport(_currentRenderPass, 0, 0, width, height, 0, 1);
    _wgpu.RenderPassEncoderSetScissorRect(_currentRenderPass, 0, 0, width, height);
}
```

**Key difference from scene pass:** No `ColorAttachments`, `ColorAttachmentCount = 0`. The pipeline for this pass must have `fragment = null` (or a no-op fragment) and a depth-only format.

### Depth-only shader (WGSL)

```wgsl
struct LightSpace {
    view_proj: mat4x4f,
}
@group(0) @binding(0) var<uniform> light: LightSpace;

struct Model {
    model: mat4x4f,
}
@group(0) @binding(1) var<uniform> model_data: Model;

@vertex fn vs_main(@location(0) position: vec3f) -> @builtin(position) vec4f {
    return light.view_proj * model_data.model * vec4f(position, 1.0);
}

// No fragment shader needed — depth-only pass writes depth buffer automatically
```

### Open design decisions

**1. Shadow texture resolution**

| Resolution | Memory | Quality | Performance |
|------------|--------|---------|-------------|
| 1024×1024 | ~2 MB | Low, visible aliasing | Fast |
| **2048×2048** | ~8 MB | Good for most scenes | Moderate |
| 4096×4096 | ~32 MB | High, sharp shadows | Expensive |

**Recommendation:** Default 2048, configurable via `ShadowConfig.Resolution`.

**2. Depth texture format**

| Format | Bits | Precision | Notes |
|--------|------|-----------|-------|
| **Depth24Plus** | 24+ | Sufficient for most scenes | Matches Phase 1a's scene depth format |
| Depth32Float | 32 | Maximum precision | More memory, needed for large scenes or CSM |

**Recommendation:** `Depth24Plus` initially. Upgrade to `Depth32Float` if precision issues appear.

**3. Fork approach for depth-only pass**

| Option | Pros | Cons |
|--------|------|------|
| **New `BeginDepthOnlyPass` method** on `IGraphicsDriver` | Clean separation, purpose-built | New interface method (default impl for safety) |
| **Extend `CreateRenderTexture`** with depth-only option | Reuses existing pass infrastructure | Overloads the render texture concept |

**Recommendation:** New `BeginDepthOnlyPass` + `EndDepthOnlyPass` methods. Shadow passes are fundamentally different from render texture passes (no color target, different pipeline requirements).

### Gotchas addressed

- **Light direction is vertical:** If the directional light points straight down `(0, -1, 0)`, the `CreateLookAt` up vector `(0, 1, 0)` is parallel to the view direction, producing a degenerate matrix. Use `(0, 0, 1)` as the up vector when `|dot(lightDir, up)| > 0.99`.
- **Shadow map texture not in `TextureFormat` enum:** NoZ's `TextureFormat` has `RGBA8`, `BGRA8`, etc. but no depth format. The shadow depth texture must be created directly via `DeviceCreateTexture` with `WGPUTextureFormat.Depth24Plus`, bypassing NoZ's texture management. This is a fork-level concern.
- **No comparison sampler in NoZ:** `ShaderBindingType` has `Texture2D`, `Sampler`, etc. but no comparison sampler type needed for `textureSampleCompare()`. Phase 6b will need to either add `ComparisonSampler` to `ShaderBindingType` (fork change) or sample manually and compare in shader.
- **Pipeline without fragment shader:** WebGPU allows `fragment = null` in the pipeline descriptor for depth-only passes. NoZ's `CreateRenderPipeline` always sets a fragment stage. The depth-only path needs to handle this case.
- **Shadow pass before scene pass:** The shadow pass must execute BEFORE `BeginScenePass()` so the shadow texture is ready for sampling during the lit scene render.

### Tests (unit)

**`LightSpaceMatrixTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `ComputeLightSpace_DownwardLight_ViewLooksDown` | Light direction `(0,-1,0)` → view matrix looks down |
| `ComputeLightSpace_FrustumFitting_AABBTight` | Projected frustum corners fit inside ortho bounds with minimal waste |
| `ComputeLightSpace_NearFar_EncloseFrustum` | Near/far of ortho projection enclose all frustum corners |
| `ComputeLightSpace_VerticalLight_UsesAlternateUp` | Near-vertical light doesn't produce degenerate matrix |

**`FrustumCornerTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `GetFrustumCorners_Returns8Points` | Always 8 corners (4 near + 4 far) |
| `GetFrustumCorners_NearCorners_AtNearDistance` | Near corners are at `NearPlane` distance from camera |
| `GetFrustumCorners_FarCorners_AtFarDistance` | Far corners are at specified shadow distance |
| `GetFrustumCorners_IdentityCamera_Symmetrical` | Corners are symmetric around the view axis |

### Verification

- `dotnet build yesz.slnx` passes
- `dotnet test yesz.slnx` — all existing + new tests pass
- Shadow depth texture is created at configured resolution
- Depth pass executes without WebGPU validation errors
- No visual output change (shadow texture exists but isn't sampled yet)

**Estimated scope:** ~4-5 files, L2

---

## Phase 6b: Directional Light Shadows

**Milestone:** HelloCube scene renders with visible shadows cast by the directional light using percentage-closer filtering.

**Dependencies:** Phase 6a (shadow depth texture, depth-only pass, light-space matrices)
**Fork changes:** Possibly `ShaderBindingType.ComparisonSampler` or manual depth comparison in shader
**Enables:** Phase 6c (CSM builds on single-cascade shadow infrastructure)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `ShadowUniforms` | YesZ.Rendering | C# struct: `Matrix4x4 LightViewProj`, `float ShadowBias`, `float NormalBias`, `Vector2 TexelSize` |
| Shadow UBO upload | YesZ.Rendering | Upload light-space matrix + bias params each frame |
| Shadow map binding | YesZ.Rendering | Bind shadow depth texture as a readable texture in the lit shader |
| Shadow sampling (WGSL) | YesZ.Rendering | Transform fragment world position to light space → sample shadow map → shadow factor |
| PCF filtering (WGSL) | YesZ.Rendering | 3×3 kernel of depth comparisons, averaged for soft edges |
| Shadow bias | YesZ.Rendering | Constant bias + slope-scaled bias (`max(bias * (1 - NdotL), minBias)`) |
| Normal offset bias | YesZ.Rendering | Offset sample position along surface normal before light-space projection |
| Lit+shadow shader | YesZ.Rendering | New shader variant: lit shader + shadow sampling combined |
| `Graphics3D.EnableShadows()` | YesZ.Rendering | Toggle shadow map generation + sampling for the scene |

### Shadow sampling (WGSL)

```wgsl
struct ShadowData {
    light_view_proj: mat4x4f,
    shadow_bias: f32,
    normal_bias: f32,
    texel_size: vec2f,     // 1.0 / shadow_map_resolution
}
@group(0) @binding(5) var<uniform> shadow: ShadowData;
@group(0) @binding(6) var shadow_map: texture_depth_2d;
@group(0) @binding(7) var shadow_sampler: sampler_comparison;

fn compute_shadow(world_pos: vec3f, world_normal: vec3f, NdotL: f32) -> f32 {
    // Normal offset: push sample point along normal to reduce acne
    let offset_pos = world_pos + world_normal * shadow.normal_bias;

    // Project into light space
    let light_clip = shadow.light_view_proj * vec4f(offset_pos, 1.0);
    let light_ndc = light_clip.xyz / light_clip.w;

    // NDC [-1,1] → UV [0,1] (WebGPU: Y is flipped vs OpenGL)
    let shadow_uv = vec2f(light_ndc.x * 0.5 + 0.5, -light_ndc.y * 0.5 + 0.5);

    // Out-of-bounds check
    if (shadow_uv.x < 0.0 || shadow_uv.x > 1.0 || shadow_uv.y < 0.0 || shadow_uv.y > 1.0) {
        return 1.0;  // No shadow outside shadow map
    }

    // Slope-scaled bias
    let bias = max(shadow.shadow_bias * (1.0 - NdotL), shadow.shadow_bias * 0.1);
    let compare_depth = light_ndc.z - bias;

    // PCF 3×3
    var shadow_sum = 0.0;
    for (var y = -1; y <= 1; y++) {
        for (var x = -1; x <= 1; x++) {
            let offset = vec2f(f32(x), f32(y)) * shadow.texel_size;
            shadow_sum += textureSampleCompare(shadow_map, shadow_sampler, shadow_uv + offset, compare_depth);
        }
    }
    return shadow_sum / 9.0;
}
```

**`textureSampleCompare`:** WebGPU built-in that performs depth comparison and returns 0.0 (in shadow) or 1.0 (lit). Requires a `sampler_comparison` and `texture_depth_2d`. If NoZ doesn't support comparison samplers, fall back to `textureLoad` + manual comparison:

```wgsl
// Fallback without comparison sampler:
let shadow_depth = textureLoad(shadow_map, texel_coord, 0);
let in_shadow = select(1.0, 0.0, compare_depth > shadow_depth);
```

### Shadow bias detail

Two types of bias work together to eliminate shadow acne:

1. **Constant depth bias** (`shadow_bias ≈ 0.005`): Subtracts a fixed amount from the comparison depth. Simple but insufficient for grazing angles.

2. **Normal offset bias** (`normal_bias ≈ 0.05`): Offsets the world-space sample position along the surface normal BEFORE projecting to light space. This is more effective than depth bias alone because it addresses the geometric cause of acne (surface self-intersection from finite shadow map resolution).

**Tuning:** Too little bias → shadow acne (surface shadows itself). Too much bias → peter-panning (shadow detaches from object base). The slope-scaled formula `max(bias * (1 - NdotL), bias * 0.1)` increases bias at grazing angles where acne is worst.

### Open design decisions

**1. Comparison sampler support**

| Option | Pros | Cons | Fork changes |
|--------|------|------|-------------|
| **Add `ComparisonSampler` to `ShaderBindingType`** | Native `textureSampleCompare` with hardware PCF | Fork change to enum + bind group builder | Yes |
| **Manual comparison via `textureLoad`** | No fork changes, works today | No hardware PCF, more shader instructions | None |

**Recommendation:** Manual comparison initially (no fork changes). Performance is acceptable for a 3×3 PCF kernel. Add `ComparisonSampler` in a later optimization pass if GPU-side PCF is needed.

**2. Shadow map visibility — all objects vs tagged objects**

| Option | Pros | Cons |
|--------|------|------|
| **All objects cast + receive shadows** | Simple, no per-object configuration | Small objects wastefully rendered into shadow map |
| **Per-object shadow cast/receive flags** | Control over shadow quality and performance | More API surface, configuration complexity |

**Recommendation:** All objects initially. Per-object control adds complexity with limited benefit until scenes are large enough to profile.

### Gotchas addressed

- **Shadow acne** — the #1 shadow mapping artifact. Surface fragments self-shadow because the shadow map lacks precision. Fixed with depth bias + normal offset. Must tune bias per-scene; expose via `ShadowConfig`.
- **Peter-panning** — shadows detach from the base of objects when bias is too high. Indicates bias needs reduction. Normal offset bias is less prone to this than pure depth bias.
- **NDC Y-flip:** WebGPU NDC has Y pointing up, but UV coordinates have Y pointing down. The shadow UV conversion must negate Y: `shadow_uv.y = -ndc.y * 0.5 + 0.5`.
- **Depth range `[0, 1]`:** WebGPU depth is `[0, 1]`, not `[-1, 1]` like OpenGL. The orthographic projection must produce this range. `Matrix4x4.CreateOrthographic` does this correctly.
- **Shadow map edge bleeding:** Fragments near the shadow map border may sample outside the texture. Clamp UVs or return `1.0` (no shadow) for out-of-bounds coordinates.
- **Multiple draw calls with different UBOs:** The shadow pass uses a light-space projection UBO, while the lit pass uses the camera projection. Must switch UBOs between the shadow pass and scene pass.

### Tests (unit)

Phase 6b is primarily visual, but bias math is testable.

**`ShadowBiasTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `SlopeScaledBias_HeadOn_MinimumBias` | `NdotL = 1.0` → bias = `baseBias × 0.1` (minimum) |
| `SlopeScaledBias_GrazingAngle_MaximumBias` | `NdotL = 0.0` → bias = `baseBias` (maximum) |
| `SlopeScaledBias_45Degrees_IntermediateBias` | `NdotL = 0.707` → bias between min and max |

### Verification

- Objects cast shadows on other objects and on a ground plane
- Shadow edges are soft (PCF produces gradual transitions, not hard edges)
- No shadow acne (no self-shadowing noise on lit surfaces)
- No peter-panning (shadows touch the base of objects)
- Moving the directional light direction changes shadow direction
- Objects not in shadow are fully lit (shadow factor = 1.0)
- 2D UI renders correctly on top (unaffected by shadow pass)

**Estimated scope:** ~3-4 files, L2

---

## Phase 6c: Cascaded Shadow Maps

**Milestone:** Large scenes show quality shadows at multiple distances — sharp near the camera, progressively softer/lower-resolution far away.

**Dependencies:** Phase 6b (single-cascade shadow mapping)
**Fork changes:** None expected (reuses depth-only pass from 6a, multiple times per frame)
**Enables:** (Terminal phase for shadows)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `CascadeConfig` | YesZ.Rendering | Per-cascade config: split distance, resolution, bias values |
| Cascade split computation | YesZ.Core | Practical split scheme (log/uniform blend) for N cascades |
| Per-cascade light-space matrices | YesZ.Core | Each cascade fits a sub-frustum slice in light space with tight orthographic projection |
| Multi-cascade depth textures | YesZ.Rendering | Array of shadow maps (or texture array), one per cascade |
| Multi-pass shadow rendering | YesZ.Rendering | Run depth-only pass N times (once per cascade) before the scene pass |
| Cascade selection in shader | YesZ.Rendering | Fragment shader picks cascade based on view-space depth of the fragment |
| `CascadeShadowUniforms` | YesZ.Rendering | Array of light-space matrices + split distances, uploaded per frame |
| Tests | YesZ.Core.Tests | Split distance computation, per-cascade frustum fitting |

### Cascade split computation

```csharp
public static float[] ComputeCascadeSplits(float near, float far, int cascadeCount, float lambda = 0.75f)
{
    var splits = new float[cascadeCount + 1];
    splits[0] = near;
    splits[cascadeCount] = far;

    for (int i = 1; i < cascadeCount; i++)
    {
        float t = (float)i / cascadeCount;
        float log = near * MathF.Pow(far / near, t);       // logarithmic distribution
        float uniform = near + (far - near) * t;            // uniform distribution
        splits[i] = MathHelper.Lerp(uniform, log, lambda);  // practical split (weighted blend)
    }
    return splits;
}
```

**Lambda parameter:** `lambda = 0.75` blends 75% logarithmic + 25% uniform. Logarithmic gives more resolution near the camera (where it matters most). Pure logarithmic (`lambda = 1.0`) wastes the far cascade on a tiny depth range; pure uniform (`lambda = 0.0`) gives poor near-camera resolution. `0.75` is the industry-standard value.

### Cascade selection (WGSL)

```wgsl
struct CascadeShadowData {
    light_view_proj: array<mat4x4f, 4>,  // one per cascade
    split_depths: vec4f,                  // view-space Z split boundaries
    cascade_count: u32,
    shadow_bias: f32,
    normal_bias: f32,
    texel_size: f32,
}

fn get_cascade_index(view_depth: f32, splits: vec4f, count: u32) -> u32 {
    for (var i = 0u; i < count - 1u; i++) {
        if (view_depth < splits[i]) {
            return i;
        }
    }
    return count - 1u;
}

fn compute_cascaded_shadow(world_pos: vec3f, world_normal: vec3f, view_depth: f32, NdotL: f32) -> f32 {
    let cascade = get_cascade_index(view_depth, shadow.split_depths, shadow.cascade_count);
    let light_clip = shadow.light_view_proj[cascade] * vec4f(world_pos + world_normal * shadow.normal_bias, 1.0);
    // ... same PCF sampling as Phase 6b, but index into cascade texture array
    return pcf_sample(cascade, shadow_uv, compare_depth);
}
```

### Open design decisions

**1. Number of cascades**

| Count | Shadow passes/frame | Memory (2048²) | Quality |
|-------|--------------------:|----------------:|---------|
| 2 | 2 | ~16 MB | Acceptable for small scenes |
| **3** | 3 | ~24 MB | Good balance, industry standard |
| 4 | 4 | ~32 MB | Best quality, needed for open worlds |

**Recommendation:** Default 3, configurable. 3 cascades cover near (~5m), mid (~20m), and far (~100m) with good resolution distribution.

**2. Shadow map storage — individual textures vs texture array**

| Option | Pros | Cons |
|--------|------|------|
| **Individual textures** | Simple, no texture array support needed | N texture bindings, N sampler bindings |
| **Texture array** | Single binding, cascade selected by index in shader | Requires `texture_depth_2d_array` support, possible fork change |

**Recommendation:** Individual textures initially. Texture arrays are more elegant but require `texture_depth_2d_array` in WGSL and corresponding bind group layout changes. Keep it simple until performance profiling suggests otherwise.

**3. Cascade transition blending**

| Option | Pros | Cons |
|--------|------|------|
| **Hard cascade boundary** | Simple, no extra sampling | Visible seam between cascades |
| **Blend zone** | Smooth transition, no visible seams | 2× shadow samples in the blend zone |

**Recommendation:** Hard boundary initially. Blend zone is a visual polish pass — add later if seams are visible.

### Gotchas addressed

- **Cascade frustum must be tight:** Each cascade's orthographic projection must tightly fit the corresponding sub-frustum slice. Reusing the full frustum for all cascades wastes resolution. The per-cascade fitting logic from Phase 6a is called once per cascade with different near/far values.
- **Shadow map stabilization:** As the camera moves, the shadow map's world-space coverage shifts, causing shadow edges to shimmer. Fix by snapping the orthographic projection to texel boundaries: round the min/max corners to the nearest texel size. This is a visual polish that can be deferred.
- **View-space depth computation:** The cascade selection needs the fragment's depth in view space (not clip space). Compute as `dot(camera_forward, world_pos - camera_pos)` or pass view-space Z from vertex shader. Don't use `gl_FragCoord.z` — it's in NDC, not linear view space.
- **Performance scaling:** Each cascade adds one full shadow pass (all shadow-casting geometry re-rendered). With 3 cascades and 100 objects, that's 300 shadow draw calls + 100 scene draw calls. Profile and cull aggressively (only objects intersecting the cascade frustum need to be drawn into it).

### Tests (unit)

**`CascadeSplitTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Compute_3Cascades_Returns4Splits` | `cascadeCount + 1` split values |
| `Compute_3Cascades_FirstSplitIsNear` | `splits[0] = near` |
| `Compute_3Cascades_LastSplitIsFar` | `splits[cascadeCount] = far` |
| `Compute_3Cascades_SplitsAreMonotonicallyIncreasing` | Each split > previous |
| `Compute_Lambda0_UniformDistribution` | `lambda=0` → equal spacing |
| `Compute_Lambda1_LogarithmicDistribution` | `lambda=1` → logarithmic spacing |
| `Compute_DefaultLambda_NearSplitCloserThanFar` | λ=0.75 → more resolution near camera |
| `Compute_KnownInputs_MatchesExpected` | `near=0.1, far=100, cascades=3, λ=0.75` → verify exact values |

### Verification

- Shadows near camera are high-resolution and sharp
- Shadows far from camera are present but lower resolution (acceptable)
- No visible seams at cascade boundaries (or acceptable if hard-boundary mode)
- Adding more cascades improves far-shadow quality at the cost of performance
- Split distances are configurable and produce expected depth ranges
- Performance is interactive (shadow passes complete within frame budget)

**Estimated scope:** ~3-4 files, L2

---

## Phase 7a: Render-to-Texture + Fullscreen Pass

**Milestone:** The 3D scene renders to an offscreen HDR texture, then a fullscreen quad blits it to the swap chain. Visual output is identical to before (passthrough shader).

**Dependencies:** Phase 2 (texture binding, shader system)
**Fork changes:** Possibly extend `CreateRenderTexture` to support `RGBA16F` format, or create HDR texture directly via WebGPU API
**Enables:** Phase 7b (tone mapping operates on the HDR offscreen texture)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `RenderTarget3D` | YesZ.Rendering | Offscreen target: HDR color texture (`RGBA16F`) + depth texture (`Depth24Plus`), matched to window size |
| Fullscreen triangle | YesZ.Rendering | Single oversized triangle covering the viewport (3 vertices, no index buffer, no vertex buffer) |
| Passthrough shader | YesZ.Rendering | WGSL: samples offscreen texture at fragment UV, outputs directly |
| `PostProcessPipeline` | YesZ.Rendering | Manages offscreen target lifecycle, chains post-process passes, blits final result to swap chain |
| Resize handling | YesZ.Rendering | Detect window size change in `BeginFrame`, recreate offscreen textures if size differs |
| `Graphics3D` integration | YesZ.Rendering | `Graphics3D.Begin()` redirects 3D rendering to offscreen target; `Graphics3D.End()` triggers post-process + blit |

### Render pipeline change

**Before Phase 7a:**
```
BeginScenePass(clearColor) → [3D draws + 2D draws] → EndScenePass → Present
```

**After Phase 7a:**
```
BeginRenderTexturePass(offscreen) → [3D draws] → EndRenderTexturePass
→ BeginScenePass(clearColor) → [fullscreen blit] → [2D draws] → EndScenePass → Present
```

3D content renders to the offscreen HDR texture. The blit pass copies (or tone-maps in 7b) the result to the swap chain. 2D UI renders directly to the swap chain as before, on top of the blit result.

### Fullscreen triangle technique

A fullscreen-covering triangle is more efficient than a quad (2 triangles, 6 vertices):

```wgsl
// No vertex buffer needed — generate vertices from vertex_index
@vertex fn vs_fullscreen(@builtin(vertex_index) id: u32) -> VertexOutput {
    var out: VertexOutput;
    // Triangle vertices: (-1,-1), (3,-1), (-1,3) — covers entire [-1,1] clip space
    let x = f32(i32(id & 1u) * 4 - 1);
    let y = f32(i32(id >> 1u) * 4 - 1);
    out.position = vec4f(x, y, 0.0, 1.0);
    out.uv = vec2f((x + 1.0) * 0.5, (1.0 - y) * 0.5);  // UV: top-left = (0,0)
    return out;
}

@fragment fn fs_passthrough(in: VertexOutput) -> @location(0) vec4f {
    return textureSample(scene_texture, scene_sampler, in.uv);
}
```

**Why triangle over quad:** A single triangle draw call with 3 vertices (`RenderPassEncoderDraw(3, 1, 0, 0)`) covers the screen with zero overdraw. A quad requires 2 triangles (4 or 6 vertices) and has diagonal overdraw. No vertex buffer or index buffer allocation needed.

### RenderTarget3D design

```csharp
public class RenderTarget3D : IDisposable
{
    public nuint ColorTexture { get; private set; }   // RGBA16F
    public nuint DepthTexture { get; private set; }   // Depth24Plus
    public int Width { get; private set; }
    public int Height { get; private set; }

    public void Resize(int width, int height)
    {
        if (Width == width && Height == height) return;
        Dispose();  // Release old textures
        ColorTexture = CreateHDRColorTexture(width, height);
        DepthTexture = CreateDepthTexture(width, height);
        Width = width; Height = height;
    }
}
```

**Why RGBA16F for color:** 16-bit float per channel allows HDR values (>1.0) from PBR lighting without clamping. The tone mapping pass (Phase 7b) compresses this to LDR for the swap chain. Without HDR, bright specular highlights and multiple lights produce harsh white clipping.

### Open design decisions

**1. HDR texture format**

| Format | Bits/pixel | Precision | Notes |
|--------|-----------|-----------|-------|
| **RGBA16F** | 64 | 10-bit mantissa, sufficient for HDR | Standard for real-time HDR rendering |
| RGBA32F | 128 | Full 32-bit float | Overkill for rendering, 2× bandwidth |
| R11G11B10F | 32 | No alpha, reduced precision | Compact, but loses alpha channel |

**Recommendation:** `RGBA16F` — industry standard for HDR render targets.

**2. HDR texture creation — via NoZ API vs direct WebGPU**

| Option | Pros | Cons |
|--------|------|------|
| **Extend `TextureFormat` with `RGBA16F`** | Clean integration with NoZ texture system | Fork change to add enum value |
| **Direct `DeviceCreateTexture`** | No fork changes | Bypasses NoZ's texture management, manual lifecycle |

**Recommendation:** Extend `TextureFormat`. Adding `RGBA16F = 6` to the enum is a one-line fork change and keeps all textures managed consistently.

### Gotchas addressed

- **Fullscreen shader vertex format:** The fullscreen triangle shader generates vertices from `@builtin(vertex_index)` — no vertex buffer is bound. The pipeline must be created with an empty vertex buffer layout. If NoZ's pipeline creation always expects a vertex buffer, a special case or dummy vertex format is needed.
- **Render pass ordering:** The offscreen 3D pass must complete before the blit pass begins. This maps naturally to: 3D renders to render texture (NoZ's `BeginRenderTexturePass`/`EndRenderTexturePass`), then blit renders to scene pass.
- **2D/3D compositing:** 2D UI renders to the swap chain directly (scene pass), AFTER the fullscreen blit. Post-processing only affects 3D content. If 2D elements need to be under the 3D scene (e.g., background), they must render before the blit.
- **Resize synchronization:** Window resize changes the swap chain size. The offscreen target must resize to match, but must not resize mid-frame. Check size at `Graphics3D.Begin()` time.

### Tests (unit)

Phase 7a is primarily visual (passthrough should be pixel-identical). Minimal unit tests.

**`RenderTarget3DTests`** (YesZ.Rendering.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Resize_SameSize_NoOp` | Calling Resize with same dimensions doesn't recreate textures |
| `Resize_DifferentSize_UpdatesDimensions` | Width/Height properties update after resize |

### Verification

- Scene renders identically to before (passthrough blit is visually transparent)
- Window resize works correctly (offscreen target resizes, no artifacts)
- 2D UI still renders on top (unaffected by offscreen rendering)
- No WebGPU validation errors
- Performance overhead is minimal (one extra texture copy per frame)

**Estimated scope:** ~3-4 files, L2

---

## Phase 7b: Tone Mapping + Gamma Correction

**Milestone:** HDR scene values are correctly tone-mapped to LDR display. PBR lighting produces natural-looking highlights instead of white clipping.

**Dependencies:** Phase 7a (offscreen HDR render target, fullscreen blit infrastructure)
**Fork changes:** None
**Enables:** Phase 7c (bloom operates on HDR values before tone mapping)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| Tone mapping shader | YesZ.Rendering | Replaces passthrough blit with ACES filmic tone mapping |
| Gamma correction | YesZ.Rendering | Linear → sRGB conversion as final output step |
| `PostProcessUniforms` | YesZ.Rendering | Uniform buffer: `float Exposure`, `float Gamma`, tone map operator selection |
| Exposure control | YesZ.Rendering | Manual exposure multiplier applied before tone mapping |
| `PostProcessPipeline` update | YesZ.Rendering | Blit shader now includes tone mapping + gamma instead of passthrough |

### Tone mapping operators (WGSL)

```wgsl
struct PostProcess {
    exposure: f32,
    gamma: f32,
    _pad0: f32,
    _pad1: f32,
}
@group(0) @binding(0) var scene_texture: texture_2d<f32>;
@group(0) @binding(1) var scene_sampler: sampler;
@group(0) @binding(2) var<uniform> params: PostProcess;

// ACES filmic tone mapping (simplified fit by Krzysztof Narkowicz)
fn aces_tonemap(x: vec3f) -> vec3f {
    let a = 2.51;
    let b = 0.03;
    let c = 2.43;
    let d = 0.59;
    let e = 0.14;
    return clamp((x * (a * x + b)) / (x * (c * x + d) + e), vec3f(0.0), vec3f(1.0));
}

// Reinhard (simple, for comparison)
fn reinhard_tonemap(x: vec3f) -> vec3f {
    return x / (x + vec3f(1.0));
}

// Linear → sRGB gamma correction
fn linear_to_srgb(c: vec3f) -> vec3f {
    let low = c * 12.92;
    let high = 1.055 * pow(c, vec3f(1.0 / 2.4)) - vec3f(0.055);
    return select(high, low, c <= vec3f(0.0031308));
}

@fragment fn fs_tonemap(in: VertexOutput) -> @location(0) vec4f {
    var hdr = textureSample(scene_texture, scene_sampler, in.uv).rgb;

    // Apply exposure
    hdr *= params.exposure;

    // Tone map HDR → [0, 1]
    let ldr = aces_tonemap(hdr);

    // Gamma correction (linear → sRGB)
    let final_color = linear_to_srgb(ldr);

    return vec4f(final_color, 1.0);
}
```

### Why ACES over Reinhard

| Operator | Pros | Cons |
|----------|------|------|
| **ACES filmic** | Film-like response curve, preserves color saturation in highlights, industry standard (Unreal/Unity) | Slightly desaturates midtones |
| Reinhard | Simple, preserves all color info | Highlights become washed out, no toe region |
| Uncharted 2 | Good contrast | Needs manual white point tuning |

**Recommendation:** ACES as default. Optionally expose operator selection via `PostProcessConfig`.

### Gamma correction detail

WebGPU swap chains are typically `Bgra8Unorm` (NOT `Bgra8UnormSrgb`). This means the swap chain does NOT auto-apply sRGB gamma. The shader must explicitly convert linear → sRGB before output.

If the swap chain is `Bgra8UnormSrgb`, the hardware applies gamma automatically and the shader should output linear values. Detect the format and conditionally apply gamma.

The accurate sRGB transfer function (used above) is a piecewise curve: linear below 0.0031308, power curve above. The simplified `pow(c, 1/2.2)` is close but not exact — use the piecewise version for correctness.

### Open design decisions

**1. Swap chain sRGB format**

| Option | Pros | Cons |
|--------|------|------|
| **`Bgra8Unorm` + shader gamma** | Full control over gamma curve, works everywhere | Extra shader math |
| **`Bgra8UnormSrgb` + linear output** | Hardware gamma conversion, slightly faster | Less control, may not be available on all devices |

**Recommendation:** `Bgra8Unorm` + shader gamma. NoZ currently uses `Bgra8Unorm` for the swap chain, so shader-based gamma is the path of least resistance.

### Gotchas addressed

- **Double gamma:** If gamma correction is applied in both the shader AND via an sRGB swap chain format, the result is doubly-gamma'd (too bright, washed out). Ensure exactly one gamma conversion happens.
- **Exposure before tone mapping:** Exposure must be applied BEFORE tone mapping. Applying after would affect the already-compressed LDR values and not produce the correct HDR-to-LDR mapping.
- **Alpha channel:** Tone mapping and gamma should NOT affect alpha. Output alpha as 1.0 (or pass through from the HDR texture if needed for transparency compositing).
- **Negative values:** PBR shading can theoretically produce negative color values (numerical errors). Clamp to zero before tone mapping to avoid NaN propagation.

### Tests (unit)

Tone mapping and gamma are pure math — fully testable as CPU-side reference implementations.

**`ToneMappingTests`** (YesZ.Rendering.Tests — new file)

| Test | What it proves |
|------|---------------|
| `ACES_Black_ReturnsBlack` | `ACES(0, 0, 0) = (0, 0, 0)` |
| `ACES_White_ReturnsCompressed` | `ACES(1, 1, 1) ≈ (0.59, 0.59, 0.59)` — not 1.0 |
| `ACES_BrightHDR_ApproachesOne` | `ACES(10, 10, 10)` close to but not exceeding 1.0 |
| `ACES_Monotonic_BrighterInputBrighterOutput` | `ACES(2) > ACES(1) > ACES(0.5)` |
| `ACES_NeverExceedsOne` | Output always in `[0, 1]` for any positive input |
| `Reinhard_White_ReturnsHalf` | `Reinhard(1) = 0.5` (classic Reinhard property) |

**`GammaCorrectionTests`** (YesZ.Rendering.Tests — new file)

| Test | What it proves |
|------|---------------|
| `LinearToSRGB_Black_ReturnsBlack` | `sRGB(0) = 0` |
| `LinearToSRGB_White_ReturnsWhite` | `sRGB(1) = 1` |
| `LinearToSRGB_MidGray_ReturnsHigher` | `sRGB(0.5) ≈ 0.735` (gamma brightens midtones) |
| `LinearToSRGB_LowValue_UsesLinearSegment` | `sRGB(0.001) ≈ 0.001 × 12.92` (piecewise linear below threshold) |
| `LinearToSRGB_Negative_ClampsToZero` | No NaN for negative inputs |

### Verification

- Bright areas show gradual rolloff instead of harsh white clipping
- Dark areas retain detail and aren't crushed to black
- Exposure control visibly brightens/darkens the scene (exposure=1.0 is neutral)
- Colors look natural — warm highlights, preserved saturation
- A/B comparison: same scene with and without tone mapping shows clear improvement

**Estimated scope:** ~1-2 files, L1

---

## Phase 7c: Bloom + Effects

**Milestone:** Bright areas in the scene produce a visible glow effect. Specular highlights and bright lights bleed softly into surrounding pixels.

**Dependencies:** Phase 7b (tone mapping provides the HDR→LDR pipeline; bloom operates on HDR values before tone mapping)
**Fork changes:** None
**Enables:** Future effects (SSAO, FXAA, DOF — not currently planned)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| Bright-pass filter shader | YesZ.Rendering | Extract pixels above luminance threshold into a half-resolution texture |
| Gaussian blur shaders | YesZ.Rendering | Separable horizontal + vertical blur passes (two shader variants) |
| Bloom composite shader | YesZ.Rendering | Additive blend of blurred bright areas back into the scene |
| `BloomConfig` | YesZ.Rendering | `float Threshold`, `float Intensity`, `int BlurPasses` — exposed via config |
| Half-resolution textures | YesZ.Rendering | Bright-pass and blur textures at half width/height (performance optimization) |
| `PostProcessPipeline` update | YesZ.Rendering | Chain: scene HDR → bright-pass → blur H → blur V → composite + tone map |

### Bloom pipeline

```
Scene HDR texture (full resolution, RGBA16F)
    │
    ▼
Bright-pass filter (half resolution)
    Extract pixels where luminance(rgb) > threshold
    │
    ▼
Gaussian blur horizontal (half resolution)
    Separable blur, 9-13 taps
    │
    ▼
Gaussian blur vertical (half resolution)
    Separable blur, 9-13 taps
    │  (repeat blur H→V for additional softness)
    ▼
Composite: bloom_result + scene HDR (full resolution)
    Additive blend: scene + bloom * intensity
    │
    ▼
Tone mapping + gamma (full resolution)
    Output to swap chain
```

### Bright-pass filter (WGSL)

```wgsl
struct BloomParams {
    threshold: f32,
    intensity: f32,
    texel_size: vec2f,   // 1.0 / half_resolution
}
@group(0) @binding(2) var<uniform> bloom: BloomParams;

fn luminance(c: vec3f) -> f32 {
    return dot(c, vec3f(0.2126, 0.7152, 0.0722));  // ITU-R BT.709
}

@fragment fn fs_bright_pass(in: VertexOutput) -> @location(0) vec4f {
    let hdr = textureSample(scene_texture, scene_sampler, in.uv).rgb;
    let lum = luminance(hdr);
    let contrib = max(lum - bloom.threshold, 0.0) / max(lum, 0.001);
    return vec4f(hdr * contrib, 1.0);
}
```

**Soft threshold:** The `(lum - threshold) / lum` formula produces a soft ramp above the threshold instead of a hard cutoff, preventing visible edges around bloom regions.

### Separable Gaussian blur (WGSL)

```wgsl
// Horizontal blur pass (vertical pass is identical but offsets along Y instead of X)
@fragment fn fs_blur_h(in: VertexOutput) -> @location(0) vec4f {
    let weights = array<f32, 5>(0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);
    var result = textureSample(blur_input, blur_sampler, in.uv).rgb * weights[0];

    for (var i = 1; i < 5; i++) {
        let offset = vec2f(f32(i) * bloom.texel_size.x, 0.0);
        result += textureSample(blur_input, blur_sampler, in.uv + offset).rgb * weights[i];
        result += textureSample(blur_input, blur_sampler, in.uv - offset).rgb * weights[i];
    }
    return vec4f(result, 1.0);
}
```

**9-tap kernel:** 5 unique weights × 2 samples per weight (+ and - offset) + 1 center sample = 9 texture fetches. Gaussian weights sum to ~1.0. Separable means running H and V passes independently produces the same result as a 2D kernel at O(n) cost instead of O(n²).

### Open design decisions

**1. Blur iterations**

| Iterations | Bloom radius | Performance | Visual |
|-----------|-------------|-------------|--------|
| 1 (H+V) | Small, subtle | Fast | Tight halo around bright areas |
| **2 (H+V+H+V)** | Medium | Moderate | Standard bloom effect |
| 3+ | Large, dramatic | Slower | Dreamy/ethereal |

**Recommendation:** Default 2 iterations, configurable via `BloomConfig.BlurPasses`.

**2. Bloom resolution**

| Resolution | Performance | Quality |
|-----------|-------------|---------|
| **Half** | Fast, adequate | Standard — slight softness acceptable for glow |
| Quarter | Very fast | Blocky, noticeable artifacts |
| Full | Expensive | Best quality but wastes GPU on already-blurred data |

**Recommendation:** Half resolution. Bloom is inherently blurry — rendering at half res and upsampling is visually indistinguishable from full res for most scenes.

### Gotchas addressed

- **Bloom before tone mapping:** Bloom must operate on HDR values, not tone-mapped LDR values. If applied after tone mapping, the threshold has no room to differentiate bright areas (everything is in [0,1]).
- **Energy conservation:** The composite step adds bloom ON TOP of the original scene. Without care, this can brighten the scene unnaturally. Scale bloom intensity conservatively (0.1–0.5 range) and let the tone mapper handle the final brightness.
- **Half-resolution texel alignment:** When sampling the half-res blur result at full resolution during compositing, use bilinear filtering to avoid blocky upsampling artifacts.
- **Feedback loop:** Don't accidentally feed the composite result back into the bloom pipeline. The bright-pass always samples the original HDR scene texture, not the composited output.

### Tests (unit)

**`BloomMathTests`** (YesZ.Rendering.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Luminance_White_Returns1` | `lum(1, 1, 1) = 1.0` |
| `Luminance_Red_Returns0_2126` | Green-weighted BT.709 coefficients |
| `Luminance_Green_Returns0_7152` | Green channel dominates |
| `Luminance_Black_ReturnsZero` | Zero input → zero output |
| `BrightPass_BelowThreshold_ReturnsBlack` | `lum(0.5) < threshold(1.0)` → no contribution |
| `BrightPass_AboveThreshold_ReturnsSoftRamp` | Output proportional to excess luminance |
| `GaussianWeights_SumToApproximately1` | All 9-tap weights sum within 0.01 of 1.0 |

### Verification

- Bright light sources and specular highlights produce visible glow extending beyond object edges
- Bloom intensity and threshold are adjustable via `BloomConfig`
- No bloom on dark areas (threshold works correctly — only bright areas glow)
- Bloom looks soft and natural (no visible block artifacts from half-resolution)
- Performance is acceptable (blur passes complete within frame budget)
- Disabling bloom (intensity=0) produces identical output to Phase 7b

**Estimated scope:** ~2-3 files, L2

---

## Phase 8a: Scene Graph

**Milestone:** Nodes can be created, parented, queried, and carry component data (mesh, light, camera). A simple scene builds and renders via the graph with correct transform propagation.

**Dependencies:** Phase 4c (node hierarchy concepts, transform composition patterns from glTF)
**Fork changes:** None
**Enables:** Phase 8b (frustum culling queries the scene graph), Phase 10b (physics binds to scene nodes)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `SceneNode` | YesZ.Core | Node: name, local `Transform3D`, parent reference, children list, enabled flag, component slots |
| `Scene` | YesZ.Core | Root node + global node registry (add, remove, find by name, find by component type) |
| `IComponent` interface | YesZ.Core | Base interface for node-attached data (mesh, light, camera) |
| `MeshRendererComponent` | YesZ.Rendering | Component: `Mesh3D` reference + `Material3D` reference |
| `LightComponent` | YesZ.Rendering | Component: light type + parameters (directional, point, ambient) |
| `CameraComponent` | YesZ.Rendering | Component: `Camera3D` settings (FOV, near/far, active flag) |
| Dirty-flag transform propagation | YesZ.Core | Local transform change → mark self + descendants dirty → recompute world matrix on demand |
| `SceneRenderer` | YesZ.Rendering | Walk scene graph → collect renderables → set lights → submit draw calls via `Graphics3D` |
| Tests | YesZ.Core.Tests | Hierarchy operations, transform propagation, dirty-flag invalidation, find-by-type |

### SceneNode design

```csharp
public class SceneNode
{
    public string Name { get; set; }
    public bool Enabled { get; set; } = true;

    // Transform
    public Vector3 LocalPosition { get; set; }
    public Quaternion LocalRotation { get; set; } = Quaternion.Identity;
    public Vector3 LocalScale { get; set; } = Vector3.One;

    public Matrix4x4 LocalMatrix => Matrix4x4.CreateScale(LocalScale)
                                  * Matrix4x4.CreateFromQuaternion(LocalRotation)
                                  * Matrix4x4.CreateTranslation(LocalPosition);

    public Matrix4x4 WorldMatrix { get { if (_worldDirty) RecomputeWorld(); return _worldMatrix; } }

    // Hierarchy
    public SceneNode? Parent { get; private set; }
    public IReadOnlyList<SceneNode> Children => _children;
    public void AddChild(SceneNode child) { ... }
    public void RemoveChild(SceneNode child) { ... }

    // Components
    private readonly Dictionary<Type, IComponent> _components = new();
    public T? GetComponent<T>() where T : class, IComponent => _components.GetValueOrDefault(typeof(T)) as T;
    public void AddComponent<T>(T component) where T : class, IComponent => _components[typeof(T)] = component;
    public void RemoveComponent<T>() where T : class, IComponent => _components.Remove(typeof(T));
}
```

### Dirty-flag transform propagation

```csharp
private bool _worldDirty = true;
private Matrix4x4 _worldMatrix;

private void MarkDirty()
{
    if (_worldDirty) return;  // Already dirty — no need to propagate further
    _worldDirty = true;
    foreach (var child in _children)
        child.MarkDirty();
}

private void RecomputeWorld()
{
    _worldMatrix = Parent != null
        ? LocalMatrix * Parent.WorldMatrix   // Parent computes recursively if needed
        : LocalMatrix;
    _worldDirty = false;
}
```

**Cost:** Setting `LocalPosition` on a root node with 100 descendants touches 101 dirty flags (O(n) in subtree size). Recomputation happens lazily — only nodes whose `WorldMatrix` is actually read recompute. In practice, only rendered nodes' world matrices are read, so disabled subtrees pay zero recomputation cost.

### SceneRenderer pipeline

```csharp
public class SceneRenderer
{
    public void Render(Scene scene, Graphics3D g3d)
    {
        // 1. Find active camera
        var cameraNode = scene.FindFirstByComponent<CameraComponent>();
        var camera = cameraNode.GetComponent<CameraComponent>()!.Camera;

        // 2. Collect lights from scene
        g3d.Begin(camera);
        foreach (var lightNode in scene.FindAllByComponent<LightComponent>())
        {
            if (!lightNode.Enabled) continue;
            var light = lightNode.GetComponent<LightComponent>()!;
            light.ApplyTo(g3d, lightNode.WorldMatrix);
        }

        // 3. Collect and draw renderables
        foreach (var node in scene.FindAllByComponent<MeshRendererComponent>())
        {
            if (!node.Enabled) continue;
            var renderer = node.GetComponent<MeshRendererComponent>()!;
            g3d.SetMaterial(renderer.Material);
            g3d.DrawMesh(renderer.Mesh, node.WorldMatrix);
        }

        g3d.End();
    }
}
```

### Immediate-mode coexistence

The scene graph provides retained-mode rendering (build once, render automatically). Immediate-mode `Graphics3D.DrawMesh()` still works independently for:
- Debug visualizations (bounding boxes, gizmos, wireframes)
- Overlay/HUD elements in 3D space
- Prototyping before committing to the scene graph

Both modes operate within the same `Graphics3D.Begin()` / `End()` bracket and submit to the same NoZ draw command system.

### Open design decisions

**1. Component system — ECS vs simple dictionary**

| Option | Pros | Cons |
|--------|------|------|
| **Dictionary<Type, IComponent>** | Simple, familiar, good for dozens of nodes | O(n) component iteration for find-by-type queries |
| **Full ECS (archetype-based)** | Cache-friendly iteration, scales to thousands | Massive complexity, premature for YesZ's scale |
| **Component array per node** | Simple, no dictionary overhead | Type lookup requires casting/iteration |

**Recommendation:** Dictionary. YesZ scenes will have tens to hundreds of nodes, not thousands. ECS is overkill and can be introduced later if needed.

**2. Scene loading — manual construction vs glTF scene import**

| Option | Pros | Cons |
|--------|------|------|
| **Manual API only** | Simple, full control | Verbose for complex scenes |
| **glTF scene → Scene graph** | Load `.glb` directly into scene | Tight coupling with glTF loader |

**Recommendation:** Both. Provide `Scene.FromGltf(Model3D)` that converts Phase 4c's node hierarchy into scene nodes. Manual construction is always available.

### Gotchas addressed

- **Circular parent references:** `AddChild` must check that the new child isn't an ancestor of the current node. Otherwise, `MarkDirty` and `RecomputeWorld` produce infinite recursion.
- **Orphaned nodes:** Removing a child should set its `Parent` to null and mark it dirty (it now has a different world matrix — identity if root, or relative to a new parent).
- **Disabled parent hides children:** `SceneRenderer` must check `Enabled` for all ancestors, not just the node itself. The simplest approach: skip the entire subtree when a disabled node is encountered during traversal.
- **Component ownership:** Components reference `Mesh3D` and `Material3D` by reference, not by value. Multiple nodes can share the same mesh/material (e.g., 10 trees using one tree mesh). Disposal responsibility belongs to the resource loader, not the scene graph.
- **Find-by-component performance:** Linear scan of all nodes is O(n). For scene sizes up to ~200 nodes, this is fast enough. If profiling shows it's a bottleneck, add per-component-type index lists.

### Tests (unit)

Scene graph is pure data structures — fully testable.

**`SceneNodeTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Default_WorldMatrix_IsIdentity` | Root node with no transform → identity |
| `SetPosition_MarksDirty` | Setting `LocalPosition` invalidates world matrix |
| `DirtyFlag_Propagates_ToChildren` | Parent dirty → all descendants dirty |
| `DirtyFlag_DoesNotPropagate_ToParent` | Child dirty → parent stays clean |
| `WorldMatrix_ChildOfTranslatedParent_IncludesParent` | Parent at `(5,0,0)`, child at `(0,1,0)` → child world = `(5,1,0)` |
| `WorldMatrix_ThreeLevelHierarchy_ComposesCorrectly` | Root → child → grandchild all contribute |
| `AddChild_SetsParent` | Child's `Parent` property points to parent |
| `RemoveChild_ClearsParent` | Removed child's `Parent` is null |
| `AddChild_ToOwnAncestor_Throws` | Circular hierarchy prevention |
| `Disabled_Node_IsSkippedByTraversal` | Disabled node and descendants not returned by renderable query |

**`SceneComponentTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `AddComponent_GetComponent_RoundTrips` | Add then get returns same instance |
| `GetComponent_NotAdded_ReturnsNull` | Missing component → null |
| `RemoveComponent_ThenGet_ReturnsNull` | Removed component is gone |
| `FindByComponent_ReturnsAllMatching` | Scene-wide query finds all nodes with component type |
| `FindByComponent_SkipsDisabled` | Disabled nodes not in results |

### Verification

- Build a scene with parent/child/grandchild hierarchy — all transforms compose correctly
- Moving a parent node moves all descendants
- Disabling a parent node hides it and all descendants
- Scene with mesh + light + camera renders identically to equivalent immediate-mode code
- Adding/removing children works correctly (parent updated, dirty flags propagated)
- Find-by-component returns correct results

**Estimated scope:** ~4-5 files, L2

---

## Phase 8b: Frustum Culling

**Milestone:** Only objects inside the camera frustum are submitted for rendering. Objects behind or outside the camera are skipped, reducing draw calls.

**Dependencies:** Phase 8a (scene graph provides the renderable list), Phase 1b (`Camera3D` provides view-projection matrix for frustum extraction)
**Fork changes:** None
**Enables:** Phase 8c (spatial partitioning accelerates frustum queries)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `BoundingBox` | YesZ.Core | AABB: `Vector3 Min`, `Vector3 Max`, contains/intersects tests, merge, transform |
| `BoundingSphere` | YesZ.Core | Center + radius, contains/intersects, from AABB |
| `Frustum` | YesZ.Core | 6 planes extracted from view-projection matrix (left, right, top, bottom, near, far) |
| Frustum-AABB test | YesZ.Core | Test AABB against all 6 frustum planes → inside/outside/intersecting |
| Frustum-sphere test | YesZ.Core | Fast early-out: sphere vs 6 planes (cheaper than AABB test) |
| Auto-bounds computation | YesZ.Core | Compute AABB from `Mesh3D` vertex positions at load time, store on `MeshRendererComponent` |
| World-space bounds | YesZ.Core | Transform local AABB by node's `WorldMatrix` → world-space AABB for culling |
| `SceneRenderer` culling integration | YesZ.Rendering | Filter nodes by frustum test before submitting draw calls |
| Tests | YesZ.Core.Tests | Frustum plane extraction, AABB/sphere intersection, edge cases (behind camera, partial overlap, fully inside) |

### Frustum plane extraction

```csharp
public struct Frustum
{
    // Planes point inward (normal faces into the frustum)
    public Plane Left, Right, Bottom, Top, Near, Far;

    public static Frustum FromViewProjection(Matrix4x4 vp)
    {
        // Gribb-Hartmann method: extract planes from rows of the VP matrix
        return new Frustum
        {
            Left   = Plane.Normalize(new(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41)),
            Right  = Plane.Normalize(new(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41)),
            Bottom = Plane.Normalize(new(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42)),
            Top    = Plane.Normalize(new(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42)),
            Near   = Plane.Normalize(new(vp.M13, vp.M23, vp.M33, vp.M43)),                     // WebGPU: [0,1] depth
            Far    = Plane.Normalize(new(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43)),
        };
    }
}
```

**Near plane formula:** WebGPU uses `[0, 1]` depth range (not `[-1, 1]` like OpenGL). The near plane formula differs from the OpenGL version: `Near = row3` (not `row4 + row3`).

### Frustum-AABB intersection

```csharp
public enum IntersectionResult { Outside, Inside, Intersecting }

public static IntersectionResult TestAABB(in Frustum frustum, in BoundingBox box)
{
    var result = IntersectionResult.Inside;

    ReadOnlySpan<Plane> planes = [frustum.Left, frustum.Right, frustum.Bottom, frustum.Top, frustum.Near, frustum.Far];
    foreach (var plane in planes)
    {
        // Find the positive vertex (farthest along the plane normal)
        var pVertex = new Vector3(
            plane.Normal.X >= 0 ? box.Max.X : box.Min.X,
            plane.Normal.Y >= 0 ? box.Max.Y : box.Min.Y,
            plane.Normal.Z >= 0 ? box.Max.Z : box.Min.Z
        );

        // Find the negative vertex (closest along the plane normal)
        var nVertex = new Vector3(
            plane.Normal.X >= 0 ? box.Min.X : box.Max.X,
            plane.Normal.Y >= 0 ? box.Min.Y : box.Max.Y,
            plane.Normal.Z >= 0 ? box.Min.Z : box.Max.Z
        );

        if (Plane.DotCoordinate(plane, pVertex) < 0)
            return IntersectionResult.Outside;  // Entirely outside this plane

        if (Plane.DotCoordinate(plane, nVertex) < 0)
            result = IntersectionResult.Intersecting;  // Straddles this plane
    }

    return result;
}
```

**P-vertex / N-vertex test:** For each frustum plane, find the corner of the AABB most aligned with the plane normal (P-vertex) and least aligned (N-vertex). If the P-vertex is outside, the entire box is outside. If the N-vertex is inside, the box is fully inside for this plane. This is the standard optimized AABB-frustum test.

### Culled scene renderer update

```csharp
public void Render(Scene scene, Graphics3D g3d)
{
    var camera = ...;
    var frustum = Frustum.FromViewProjection(camera.ViewProjectionMatrix);
    g3d.Begin(camera);
    // ... lights ...

    foreach (var node in scene.FindAllByComponent<MeshRendererComponent>())
    {
        if (!node.Enabled) continue;
        var renderer = node.GetComponent<MeshRendererComponent>()!;
        var worldBounds = renderer.LocalBounds.Transform(node.WorldMatrix);

        if (Frustum.TestAABB(frustum, worldBounds) == IntersectionResult.Outside)
            continue;  // Culled — don't submit draw call

        g3d.SetMaterial(renderer.Material);
        g3d.DrawMesh(renderer.Mesh, node.WorldMatrix);
    }

    g3d.End();
}
```

### Gotchas addressed

- **Near plane depth range:** WebGPU uses `[0, 1]` depth, so the near plane extraction formula differs from OpenGL's `[-1, 1]` range. Using the OpenGL formula produces incorrect near-plane culling.
- **AABB transform is not tight:** Transforming an AABB by an arbitrary rotation produces a larger AABB (the AABB of the rotated AABB). This is conservative — objects may not be culled even when they're technically outside the frustum. Acceptable for correctness (never culls visible objects).
- **False negatives in frustum test:** The P-vertex/N-vertex test can produce false "intersecting" results for boxes that are actually outside (e.g., box outside a corner of the frustum but inside all 6 half-spaces individually). This is a known limitation of plane-based testing. It's conservative (never incorrectly culls) and acceptable for real-time use.
- **Camera node transform:** The camera's view matrix must account for the camera node's world transform. `Camera3D.ViewMatrix = Matrix4x4.Invert(cameraNode.WorldMatrix)`.

### Tests (unit)

Frustum math and AABB intersection are fully testable. If Phase 10a isn't complete yet, `BoundingBox` is introduced here.

**`FrustumTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `FromViewProjection_Identity_ProducesUnitCubePlanes` | Identity VP → 6 planes forming `[-1,1]` cube |
| `FromViewProjection_Perspective_Has6NormalizedPlanes` | All plane normals are unit length |
| `FromViewProjection_NearPlane_UsesWebGPUDepthRange` | Near plane formula uses `[0,1]` depth (not `[-1,1]`) |

**`BoundingBoxTests`** (YesZ.Core.Tests — new file, or extend from Phase 10a)

| Test | What it proves |
|------|---------------|
| `ContainsPoint_Inside_ReturnsTrue` | Point within AABB |
| `ContainsPoint_Outside_ReturnsFalse` | Point outside AABB |
| `Intersects_Overlapping_ReturnsTrue` | Two overlapping AABBs |
| `Intersects_Disjoint_ReturnsFalse` | Two non-overlapping AABBs |
| `Transform_RotatedAABB_ProducesLargerAABB` | Conservative: rotated AABB's AABB ≥ original |

**`FrustumCullingTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `TestAABB_FullyInside_ReturnsInside` | Small box at origin, wide frustum |
| `TestAABB_BehindCamera_ReturnsOutside` | Box at z=-10, camera looks at z=+∞ |
| `TestAABB_StraddlingPlane_ReturnsIntersecting` | Box partially inside |
| `TestAABB_FarBeyondFarPlane_ReturnsOutside` | Box past far clip |
| `TestAABB_AtFrustumEdge_ReturnsIntersecting` | Conservative: edge cases kept |

### Verification

- Objects behind the camera are not drawn (verify by counting draw calls)
- Objects partially inside the frustum still render (conservative test keeps them)
- Rotating the camera changes which objects are culled
- Performance improves measurably with many objects (e.g., 100 objects, only 30 visible = 30 draw calls)
- Culling ON vs OFF produces identical visual output (no visible objects incorrectly culled)

**Estimated scope:** ~3-4 files, L2

---

## Phase 8c: Spatial Partitioning

**Milestone:** Large scenes with hundreds of objects cull efficiently via a spatial data structure, scaling sub-linearly with object count.

**Dependencies:** Phase 8b (frustum culling, bounding volumes)
**Fork changes:** None
**Enables:** (Terminal phase for scene management)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `LooseOctree<T>` | YesZ.Core | Loose octree with configurable depth and world bounds |
| Insert / remove / update | YesZ.Core | Add object with AABB, remove by handle, update AABB when object moves |
| Frustum query | YesZ.Core | Traverse octree, collect all objects in nodes that intersect the frustum |
| `SceneRenderer` integration | YesZ.Rendering | Replace flat scene traversal with octree frustum query |
| Auto-population | YesZ.Rendering | Scene graph add/remove/move automatically updates the octree |
| Tests | YesZ.Core.Tests | Insert, query, remove, update, performance with 1000 objects |

### Loose octree design

A loose octree extends each node's bounds by a factor (typically 2×), which allows objects to be stored in a single node rather than multiple:

```csharp
public class LooseOctree<T>
{
    private readonly int _maxDepth;         // e.g., 5 (32 cells per axis at max depth)
    private readonly float _looseness;      // e.g., 2.0 (nodes are 2× their strict size)
    private readonly BoundingBox _worldBounds;

    private OctreeNode _root;

    public int Insert(T item, BoundingBox bounds) { ... }
    public void Remove(int handle) { ... }
    public void Update(int handle, BoundingBox newBounds) { ... }
    public void QueryFrustum(Frustum frustum, List<T> results) { ... }
}

private struct OctreeNode
{
    public BoundingBox StrictBounds;     // Octant boundary
    public BoundingBox LooseBounds;      // StrictBounds expanded by looseness factor
    public List<(T item, BoundingBox bounds)>? Items;
    public int ChildMask;                // Bitmask: which of 8 children exist
    public OctreeNode[]? Children;       // [0..7], allocated on demand
}
```

**Insertion:** An object is placed in the deepest node whose loose bounds fully contain the object's AABB. If the object is too large, it stays in a shallower (larger) node.

**Frustum query:** Traverse from root. If a node's loose bounds don't intersect the frustum, skip the entire subtree. Otherwise, test each item in the node against the frustum and recurse into children.

### Open design decisions

**1. Data structure choice**

| Option | Pros | Cons | Best for |
|--------|------|------|----------|
| **Loose octree** | Simple, good for mixed static/dynamic, predictable memory | Objects can sit in non-leaf nodes | General-purpose 3D scenes |
| BVH (surface area heuristic) | Tight fits, excellent for static geometry | Expensive rebuild for dynamic objects | Ray tracing, static scenes |
| Uniform grid | Cache-friendly, simple implementation | Wastes memory in sparse scenes | Dense, evenly-distributed scenes |

**Recommendation:** Loose octree. Best balance of simplicity and performance for YesZ's target scale (tens to hundreds of objects, mix of static and dynamic).

**2. Rebuild strategy for moving objects**

| Option | Pros | Cons |
|--------|------|------|
| **Remove + re-insert** | Simple, always correct | O(log n) per moving object per frame |
| **Lazy re-insert** | Deferred batch update, less per-frame churn | Complex, stale data between updates |
| **Check if still fits** | Only re-insert if object left its node's loose bounds | Fast when objects move slowly |

**Recommendation:** Check-if-still-fits. Moving objects often stay within their node's loose bounds (that's the whole point of looseness). Only re-insert when the AABB leaves the node's loose bounds. This is O(1) per frame for slowly-moving objects and O(log n) only when re-insertion is needed.

### Performance characteristics

| Operation | Complexity | Notes |
|-----------|-----------|-------|
| Insert | O(depth) ≈ O(log n) | Descend from root to appropriate node |
| Remove | O(1) | Handle-based direct removal |
| Update (no re-insert) | O(1) | AABB still fits in current node |
| Update (re-insert) | O(depth) | Object left its node's loose bounds |
| Frustum query | O(k + visited nodes) | k = number of results, visited nodes << total nodes |
| Brute force comparison | O(n) | Phase 8b's approach: test every object |

For 500 objects with 50 visible: brute force = 500 frustum tests, octree ≈ 50-100 frustum tests (depending on spatial distribution).

### Gotchas addressed

- **World bounds must encompass all content:** Objects outside the root's world bounds can't be inserted. Set world bounds generously (e.g., 1000×1000×1000) or dynamically expand.
- **Degenerate cases:** All objects at the same position → all in the same leaf → no culling benefit. Unlikely in practice but test for it.
- **Thread safety:** The octree is not thread-safe. All operations (insert, remove, query) must happen on the main thread. If background loading adds objects, queue them for main-thread insertion.
- **Memory management:** Allocating `List<T>` and `OctreeNode[]` per node can fragment the heap for deep trees. Use array pools or a flat node array with index-based children for GC-friendly allocation.

### Tests (unit)

**`LooseOctreeTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Insert_SingleObject_QueryReturnsIt` | Basic insert + query round-trip |
| `Insert_1000Random_QueryFrustum_MatchesBruteForce` | Correctness: octree query = linear scan for same frustum |
| `Remove_SingleObject_QueryReturnsEmpty` | Object is fully removed |
| `Remove_All_TreeIsEmpty` | No stale data |
| `Update_ObjectMovesWithinNode_StillFound` | No re-insert needed, still in query results |
| `Update_ObjectLeavesNode_ReinsertedCorrectly` | Object moves to a different node, still found |
| `QueryFrustum_NoObjects_ReturnsEmpty` | Empty tree → empty results |
| `QueryFrustum_AllObjectsOutside_ReturnsEmpty` | All objects behind camera → 0 results |
| `QueryFrustum_SubsetVisible_ReturnsSubset` | Partial visibility returns only visible objects |
| `Insert_OutsideWorldBounds_Handled` | Doesn't crash — goes to root node |

### Verification

- Scene with 500+ objects renders at interactive frame rates
- Culling performance scales sub-linearly with object count (measure frustum query time vs brute force)
- Moving objects update correctly in the octree (visual output unchanged)
- Rendering output is identical to brute-force frustum culling (Phase 8b) — pixel-perfect
- Edge case: object spanning the root boundary is handled correctly

**Estimated scope:** ~2-3 files, L2

---

## Phase 9: Instanced Rendering

**Milestone:** Many copies of the same mesh render efficiently with per-instance transforms via a single draw call. 1000 instances at the cost of ~1 draw call.

**Dependencies:** Phase 1b (basic 3D rendering, vertex format system, mesh creation)
**Fork changes:** `WebGPUGraphicsDriver` — expose `instanceCount` parameter in `DrawElements`/`DrawIndexed`; add second vertex buffer slot with per-instance step mode
**Enables:** Particle systems, vegetation, crowds, debris (future)

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `InstanceBuffer<T>` | YesZ.Rendering | Generic GPU buffer for per-instance data, dynamic resize, per-frame upload |
| `InstanceData` | YesZ.Rendering | Default instance struct: `Matrix4x4 World` (64 bytes per instance) |
| Instance vertex layout | YesZ.Rendering | Second vertex buffer slot (slot 1) with `StepMode.Instance` in pipeline |
| `Graphics3D.DrawMeshInstanced()` | YesZ.Rendering | API: draw N instances of a mesh with per-instance data buffer |
| Instanced shader variant | YesZ.Rendering | WGSL: reads model matrix from per-instance vertex attributes instead of uniform |
| Fork: expose instance count | engine/noz | `DrawElements` passes `instanceCount` to `RenderPassEncoderDrawIndexed` (currently hardcoded to 1) |
| Fork: second vertex buffer | engine/noz | Pipeline creation supports 2 vertex buffer layouts; `DrawElements` binds instance buffer to slot 1 |
| Tests | YesZ.Rendering.Tests | Instance buffer upload, draw call count verification, per-instance transform correctness |

### WebGPU instancing architecture

WebGPU supports instancing natively via `RenderPassEncoderDrawIndexed`:

```csharp
// Current NoZ code (hardcoded instanceCount = 1):
_wgpu.RenderPassEncoderDrawIndexed(_currentRenderPass,
    (uint)indexCount, 1 /*instanceCount*/, (uint)firstIndex, baseVertex, 0 /*firstInstance*/);

// After fork change:
_wgpu.RenderPassEncoderDrawIndexed(_currentRenderPass,
    (uint)indexCount, (uint)instanceCount, (uint)firstIndex, baseVertex, (uint)firstInstance);
```

### Per-instance data via vertex buffer

Instead of uploading model matrices via uniform buffers (limited to ~256 matrices at 64 bytes each within the 64KB uniform limit), use a second vertex buffer with `StepMode.Instance`:

```csharp
// Instance data struct (matches WGSL vertex attributes)
[StructLayout(LayoutKind.Sequential)]
public struct InstanceData
{
    public Vector4 Row0;  // Model matrix row 0 — location 4
    public Vector4 Row1;  // Model matrix row 1 — location 5
    public Vector4 Row2;  // Model matrix row 2 — location 6
    public Vector4 Row3;  // Model matrix row 3 — location 7
}
// 64 bytes per instance
```

**Why 4× `Vector4` instead of `Matrix4x4`:** WGSL vertex attributes max at `vec4f`. A `mat4x4f` must be split into 4 `vec4f` attributes at consecutive locations. The shader reconstructs the matrix:

```wgsl
struct InstanceInput {
    @location(4) model_row0: vec4f,
    @location(5) model_row1: vec4f,
    @location(6) model_row2: vec4f,
    @location(7) model_row3: vec4f,
}

@vertex fn vs_instanced(vertex: VertexInput, instance: InstanceInput) -> VertexOutput {
    let model = mat4x4f(instance.model_row0, instance.model_row1, instance.model_row2, instance.model_row3);
    let world_pos = model * vec4f(vertex.position, 1.0);
    // ...
}
```

### Pipeline vertex buffer layout (fork change)

```csharp
// Two vertex buffer layouts in the pipeline descriptor:
// Buffer 0: per-vertex data (MeshVertex3D) — StepMode.Vertex
// Buffer 1: per-instance data (InstanceData) — StepMode.Instance

var bufferLayouts = new VertexBufferLayout[2];
bufferLayouts[0] = new VertexBufferLayout
{
    ArrayStride = (ulong)meshStride,
    StepMode = VertexStepMode.Vertex,
    Attributes = meshAttributes,
    AttributeCount = (nuint)meshAttributeCount,
};
bufferLayouts[1] = new VertexBufferLayout
{
    ArrayStride = 64,  // sizeof(InstanceData)
    StepMode = VertexStepMode.Instance,
    Attributes = instanceAttributes,  // locations 4-7, each vec4f
    AttributeCount = 4,
};
```

NoZ's current `CreateRenderPipeline` only creates one `VertexBufferLayout`. This fork change adds support for a second buffer layout when the shader has instance attributes.

### InstanceBuffer\<T\> design

```csharp
public class InstanceBuffer<T> : IDisposable where T : unmanaged
{
    private WGPUBuffer* _buffer;
    private int _capacity;
    private int _count;

    public InstanceBuffer(int initialCapacity = 256) { ... }

    public void Clear() { _count = 0; }
    public void Add(in T data) { if (_count == _capacity) Grow(); _data[_count++] = data; }
    public void Upload() { /* QueueWriteBuffer entire used range */ }
    public int Count => _count;

    private void Grow()
    {
        var newCapacity = _capacity * 2;
        // Recreate GPU buffer at new size, copy CPU-side data
        ...
    }
}
```

**Upload strategy:** Write the entire used range each frame via `QueueWriteBuffer`. For 1000 instances at 64 bytes = 64 KB per frame — well within GPU bandwidth.

### Open design decisions

**1. Instance data format — transform only vs extended data**

| Option | Bytes/instance | Pros | Cons |
|--------|---------------|------|------|
| **Matrix4x4 only** | 64 | Minimal, covers most use cases | No per-instance color or material variation |
| Matrix4x4 + Color | 80 | Per-instance tinting (foliage variation) | 25% more bandwidth |
| Matrix4x4 + Color + UV offset | 96 | Per-instance sprite sheet selection | Complex, rarely needed for 3D |

**Recommendation:** Matrix4x4 only. Per-instance color/material variation is a future extension. Start simple.

**2. Instance buffer ownership — per-mesh vs per-draw-call**

| Option | Pros | Cons |
|--------|------|------|
| **Per-mesh** | One buffer for all instances of a mesh, reuse across frames | Doesn't support multiple materials per mesh |
| **Per-draw-call** | Full flexibility, different materials per instance group | More buffers, more upload overhead |

**Recommendation:** Per-draw-call (caller creates and fills the buffer). This is the most flexible and matches the `DrawMeshInstanced(mesh, material, instanceBuffer)` API pattern.

### Gotchas addressed

- **Vertex attribute locations must not collide:** `MeshVertex3D` uses locations 0-3. Instance data starts at location 4. If additional per-vertex attributes are added later (e.g., tangent at location 4), instance locations must shift. Define instance attribute locations as `MeshVertex3D.AttributeCount` + offset.
- **Pipeline cache key must include instance flag:** A mesh drawn with instancing uses a different pipeline (2 buffer layouts) than the same mesh drawn without instancing. The `PsoKey` must include an "instanced" flag.
- **Empty instance buffer:** Drawing with `instanceCount = 0` is valid in WebGPU (no-op). But uploading an empty buffer may cause issues — guard against zero-count draws.
- **Normal matrix for instances:** The per-instance model matrix works for position, but normals need the inverse-transpose. For rotation-only transforms (no non-uniform scale), the model matrix upper 3×3 is correct. For general transforms, either: (a) require callers to provide normal matrices (doubles instance data), or (b) compute inverse-transpose in the vertex shader (expensive). Recommend option (a) as a future extension, accept rotation-only for now.
- **NoZ draw command compatibility:** NoZ's `DrawElements` currently creates draw commands with `instanceCount = 1`. The fork change must thread the instance count through the draw command system. Alternatively, bypass NoZ's batching and call `RenderPassEncoderDrawIndexed` directly for instanced draws.

### Tests (unit)

**`InstanceBufferTests`** (YesZ.Rendering.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Add_UnderCapacity_IncrementsCount` | Count tracks correctly |
| `Add_AtCapacity_Grows` | Capacity doubles when full |
| `Clear_ResetsCountToZero` | Count = 0 after clear, capacity unchanged |
| `Add_1000Instances_CountIs1000` | Large count works |
| `InstanceData_SizeIs64Bytes` | `Marshal.SizeOf<InstanceData>()` = 64 |
| `InstanceData_Row0Offset_Is0` | First row at byte 0 |
| `InstanceData_Row3Offset_Is48` | Fourth row at byte 48 |

### Verification

- Render 1000 copies of the same mesh with distinct transforms in a single draw call
- Each instance is positioned/rotated correctly (no shared transforms)
- Performance is significantly better than 1000 individual `DrawMesh` calls (measure frame time)
- Works with lit and textured materials
- Instance count of 0 produces no visible output (no errors)
- Instance buffer resize works correctly (starts at 256, grows to 1024 when needed)

**Estimated scope:** ~3-4 files, L2

---

## Phase 10a: Bounding Volumes + Raycasting

**Milestone:** AABB, sphere, and OBB intersection tests pass. Ray-mesh intersection returns correct hit point, normal, and distance. All pure math — no GPU dependency.

**Dependencies:** Phase 0 (pure math — can develop in parallel with everything)
**Fork changes:** None
**Enables:** Phase 10b (physics uses bounding volumes for collision shapes), game-level mouse picking/selection
**Parallel with:** All other phases (no rendering dependency)

**Note:** `BoundingBox` and `BoundingSphere` may already exist from Phase 8b. If so, this phase extends them with OBB, raycasting, and additional tests. Plan accordingly during L2 planning.

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| `BoundingBox` | YesZ.Core | AABB: `Vector3 Min/Max`, contains point, contains box, intersects box, merge, transform, from points |
| `BoundingSphere` | YesZ.Core | `Vector3 Center` + `float Radius`, contains/intersects tests, from AABB, from points (Ritter's algorithm) |
| `OrientedBoundingBox` | YesZ.Core | `Vector3 Center`, `Vector3 HalfExtents`, `Quaternion Rotation`, SAT-based intersection |
| `Ray` | YesZ.Core | `Vector3 Origin` + `Vector3 Direction` (normalized), point-at-distance |
| Ray-AABB intersection | YesZ.Core | Slab method — returns `(bool hit, float distance)` |
| Ray-sphere intersection | YesZ.Core | Geometric/analytic solution — returns `(bool hit, float distance)` |
| Ray-OBB intersection | YesZ.Core | Transform ray into OBB local space → slab test |
| Ray-triangle intersection | YesZ.Core | Möller-Trumbore algorithm — returns `(bool hit, float distance, float u, float v)` |
| Ray-mesh intersection | YesZ.Core | Test ray against all mesh triangles, return nearest hit with interpolated normal |
| Auto-bounds | YesZ.Core | Compute AABB/sphere from `Mesh3D` vertex positions |
| Screen-to-ray | YesZ.Core | `Camera3D.ScreenPointToRay(Vector2 screenPos)` — unproject screen coordinates to world ray |
| Tests | YesZ.Core.Tests | All intersection tests for known geometric configurations, edge cases |

### Ray-AABB intersection (slab method)

```csharp
public static bool Intersect(in Ray ray, in BoundingBox box, out float distance)
{
    float tMin = 0f;
    float tMax = float.MaxValue;

    for (int i = 0; i < 3; i++)
    {
        float origin = i == 0 ? ray.Origin.X : i == 1 ? ray.Origin.Y : ray.Origin.Z;
        float dir = i == 0 ? ray.Direction.X : i == 1 ? ray.Direction.Y : ray.Direction.Z;
        float min = i == 0 ? box.Min.X : i == 1 ? box.Min.Y : box.Min.Z;
        float max = i == 0 ? box.Max.X : i == 1 ? box.Max.Y : box.Max.Z;

        if (MathF.Abs(dir) < 1e-8f)
        {
            // Ray parallel to slab — miss if origin not within slab
            if (origin < min || origin > max) { distance = 0; return false; }
        }
        else
        {
            float invD = 1f / dir;
            float t1 = (min - origin) * invD;
            float t2 = (max - origin) * invD;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            if (tMin > tMax) { distance = 0; return false; }
        }
    }

    distance = tMin;
    return true;
}
```

### Ray-triangle intersection (Möller-Trumbore)

```csharp
public static bool IntersectTriangle(in Ray ray, Vector3 v0, Vector3 v1, Vector3 v2,
    out float t, out float u, out float v)
{
    const float epsilon = 1e-8f;
    var edge1 = v1 - v0;
    var edge2 = v2 - v0;
    var h = Vector3.Cross(ray.Direction, edge2);
    var a = Vector3.Dot(edge1, h);

    if (MathF.Abs(a) < epsilon) { t = u = v = 0; return false; }  // parallel

    var f = 1f / a;
    var s = ray.Origin - v0;
    u = f * Vector3.Dot(s, h);
    if (u < 0f || u > 1f) { t = v = 0; return false; }

    var q = Vector3.Cross(s, edge1);
    v = f * Vector3.Dot(ray.Direction, q);
    if (v < 0f || u + v > 1f) { t = 0; return false; }

    t = f * Vector3.Dot(edge2, q);
    return t > epsilon;  // Hit must be in front of ray
}
```

**Barycentric coordinates (u, v):** Can interpolate vertex attributes (normal, UV) at the hit point: `hitNormal = (1-u-v)*n0 + u*n1 + v*n2`.

### Screen-to-ray conversion

```csharp
public Ray ScreenPointToRay(Vector2 screenPos, int screenWidth, int screenHeight)
{
    // Convert screen coordinates to NDC [-1, 1]
    float ndcX = (2f * screenPos.X / screenWidth) - 1f;
    float ndcY = 1f - (2f * screenPos.Y / screenHeight);  // Y-flip for screen coords

    // Unproject near and far points
    var invVP = Matrix4x4.Invert(ViewProjectionMatrix);
    var nearPoint = Vector4.Transform(new Vector4(ndcX, ndcY, 0f, 1f), invVP);
    var farPoint = Vector4.Transform(new Vector4(ndcX, ndcY, 1f, 1f), invVP);

    nearPoint /= nearPoint.W;  // Perspective divide
    farPoint /= farPoint.W;

    var origin = new Vector3(nearPoint.X, nearPoint.Y, nearPoint.Z);
    var direction = Vector3.Normalize(new Vector3(farPoint.X - nearPoint.X, farPoint.Y - nearPoint.Y, farPoint.Z - nearPoint.Z));
    return new Ray(origin, direction);
}
```

### Open design decisions

**1. Ray-mesh acceleration**

| Option | Pros | Cons |
|--------|------|------|
| **Brute force (all triangles)** | Simple, correct, no preprocessing | O(n) per ray, slow for large meshes |
| **BVH per mesh** | O(log n) per ray, fast for large meshes | Build time, memory overhead per mesh |

**Recommendation:** Brute force for Phase 10a. Most game meshes are under 10K triangles — testing all triangles is fast enough for per-frame picking. BVH is an optimization for when profiling shows ray-mesh is a bottleneck.

### Gotchas addressed

- **Ray direction must be normalized:** The slab method and Möller-Trumbore assume a unit-length direction. Non-normalized directions produce incorrect distance values.
- **Ray origin inside volume:** A ray starting inside an AABB returns `tMin = 0` (hit at the origin). This is correct for picking (mouse ray starts at camera position) but may need special handling for physics raycasts.
- **Floating-point edge cases:** Triangle intersection at exact edge/vertex positions can produce inconsistent results due to floating-point precision. Use an epsilon tolerance (`1e-8f`).
- **OBB intersection via local-space transform:** Transform the ray into the OBB's local coordinate system (undo rotation + translation), then perform a standard AABB slab test with `(-halfExtents, +halfExtents)`. Simpler and faster than SAT for ray tests.
- **Winding order sensitivity:** Möller-Trumbore is sensitive to triangle winding. For double-sided intersection, check `abs(a) < epsilon` instead of `a < epsilon`.

### Tests (unit)

This phase is 100% unit-testable — no GPU dependency. Can be developed test-first in parallel with everything. Target: 30+ test cases.

**`RayAABBTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Hit_RayTowardUnitCube_ReturnsCorrectDistance` | Ray from `(0,0,-5)` → `(0,0,1)` hits at `t = 4.5` |
| `Hit_RayOriginInsideAABB_ReturnsT0` | Inside → `distance = 0` |
| `Miss_RayParallelToSlab_ReturnsFalse` | Ray along X axis misses Y/Z slabs |
| `Miss_RayAwayFromBox_ReturnsFalse` | Ray pointing opposite direction |
| `Hit_RayGrazesEdge_ReturnsTrueWithTolerance` | Edge-grazing hit detected |

**`RaySphereTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Hit_RayThroughCenter_ReturnsFrontIntersection` | `t = center.z - radius` |
| `Hit_RayTangent_ReturnsGrazingDistance` | Tangent ray returns single intersection |
| `Miss_RayMissesSphere_ReturnsFalse` | No intersection |
| `Hit_RayOriginInsideSphere_ReturnsT0` | Inside → immediate hit |

**`RayTriangleTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Hit_RayHitsTriangleCenter_ReturnsDistance` | Center hit with valid barycentric coords |
| `Hit_RayHitsTriangleEdge_ReturnsTrueWithTolerance` | Edge hit detected (epsilon handling) |
| `Miss_RayParallelToTriangle_ReturnsFalse` | Parallel → no intersection |
| `Miss_RayBehindTriangle_ReturnsFalse` | Negative `t` → rejected |
| `Hit_BarycentricCoords_SumToOne` | `u + v ≤ 1`, both ≥ 0 |
| `ZeroAreaTriangle_ReturnsFalse` | Degenerate triangle → no hit |

**`RayMeshTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Hit_RayHitsMesh_ReturnsNearestTriangle` | Multiple hits → smallest `t` returned |
| `Hit_InterpolatedNormal_IsCorrect` | Barycentric interpolation of vertex normals |
| `Miss_RayMissesMesh_ReturnsFalse` | No triangle hit |

**`BoundingSphereTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `FromAABB_CenterIsAABBCenter` | Sphere center = AABB midpoint |
| `FromAABB_RadiusEnclosesCorners` | Radius ≥ distance from center to any corner |
| `FromPoints_EnclosesAllPoints` | Ritter's algorithm result contains all inputs |
| `Intersects_Overlapping_ReturnsTrue` | Two overlapping spheres |
| `Intersects_Disjoint_ReturnsFalse` | Two separated spheres |

**`OBBTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `RayIntersect_HitsRotatedBox_ReturnsDistance` | Ray hits 45°-rotated OBB |
| `RayIntersect_MissesRotatedBox_ReturnsFalse` | Ray misses rotated OBB |
| `IdentityRotation_MatchesAABBResult` | OBB with no rotation = AABB behavior |

**`ScreenToRayTests`** (YesZ.Core.Tests — new file)

| Test | What it proves |
|------|---------------|
| `ScreenCenter_ProducesRayAlongForward` | Center of screen → ray parallel to camera forward |
| `ScreenCorner_ProducesAngledRay` | Corner pixel → ray at frustum edge angle |
| `RayDirection_IsNormalized` | Output direction has unit length |

### Verification

- All intersection tests pass for known geometric configurations
- Edge cases handled: ray origin inside volume, parallel rays, degenerate triangles
- `dotnet test yesz.slnx` — all tests pass

**Estimated scope:** ~2-3 files, L2

---

## Phase 10b: Physics Integration

**Milestone:** Rigid bodies with gravity and collision response simulate correctly. Objects fall, bounce, stack, and rest.

**Dependencies:** Phase 10a (bounding volumes for collider shapes), Phase 8a (scene graph for physics-renderable binding)
**Fork changes:** None (external library)
**Enables:** Game-level physics gameplay, character controllers

### What's built

| Component | Project | Description |
|-----------|---------|-------------|
| BepuPhysics2 NuGet dependency | new `YesZ.Physics` project | Reference `BepuPhysics` + `BepuUtilities` packages |
| `PhysicsWorld` | YesZ.Physics | Wraps BepuPhysics2 `Simulation`, manages body creation/destruction, steps simulation |
| `RigidBodyComponent` | YesZ.Physics | Scene node component: body type (static/dynamic/kinematic), shape, mass, material properties |
| Collider shapes | YesZ.Physics | Box, sphere, capsule, convex hull — map YesZ shapes to BepuPhysics2 shapes |
| Physics ↔ scene sync | YesZ.Physics | After `PhysicsWorld.Step()`: copy body transforms → scene node `LocalPosition`/`LocalRotation` |
| Fixed-timestep accumulator | YesZ.Physics | `PhysicsWorld.Update(float deltaTime)` — accumulates time and steps at fixed rate (e.g., 1/60s) |
| Debug visualization | YesZ.Rendering | Optional wireframe rendering of collider shapes via `Graphics3D.DrawMesh()` |
| Tests | YesZ.Physics.Tests | Gravity, collision detection, resting contact, stacking stability |

### PhysicsWorld design

```csharp
public class PhysicsWorld : IDisposable
{
    private Simulation _simulation;
    private BufferPool _bufferPool;
    private readonly float _fixedTimestep;
    private float _accumulator;

    public PhysicsWorld(float fixedTimestep = 1f / 60f)
    {
        _fixedTimestep = fixedTimestep;
        _bufferPool = new BufferPool();
        _simulation = Simulation.Create(
            _bufferPool,
            new NarrowPhaseCallbacks(),      // collision response config
            new PoseIntegratorCallbacks(),   // gravity + damping
            new SolveDescription(8, 1));     // velocity iterations, substeps
    }

    public BodyHandle AddDynamic(Vector3 position, Quaternion rotation, IConvexShape shape, float mass)
    {
        var bodyDesc = BodyDescription.CreateDynamic(
            new RigidPose(ToBepuVector(position), ToBepuQuaternion(rotation)),
            shape.ComputeInertia(mass),
            _simulation.Shapes.Add(shape),
            _fixedTimestep);  // activity threshold
        return _simulation.Bodies.Add(bodyDesc);
    }

    public StaticHandle AddStatic(Vector3 position, Quaternion rotation, IConvexShape shape)
    {
        var staticDesc = new StaticDescription(
            new RigidPose(ToBepuVector(position), ToBepuQuaternion(rotation)),
            _simulation.Shapes.Add(shape));
        return _simulation.Statics.Add(staticDesc);
    }

    public void Update(float deltaTime)
    {
        _accumulator += deltaTime;
        while (_accumulator >= _fixedTimestep)
        {
            _simulation.Timestep(_fixedTimestep);
            _accumulator -= _fixedTimestep;
        }
    }
}
```

### Fixed-timestep accumulator detail

```
Frame delta: 16.67ms (60 FPS)    Physics step: 16.67ms (60 Hz)
Frame delta: 8.33ms (120 FPS)    Physics step: accumulate 2 frames, then step once
Frame delta: 33.33ms (30 FPS)    Physics step: step twice per frame (2 × 16.67ms)
```

The accumulator prevents physics from running faster or slower than intended regardless of render frame rate. Without it, variable-timestep physics produces non-deterministic results (different behavior at 30 FPS vs 60 FPS).

**Render interpolation:** After physics steps, the remaining accumulator time (`_accumulator / _fixedTimestep`) can be used to interpolate render transforms between the current and previous physics pose. This eliminates visual jitter when the render frame rate doesn't match the physics rate. Implement as an optional feature.

### Physics ↔ scene graph synchronization

```csharp
public void SyncToScene(Scene scene)
{
    foreach (var node in scene.FindAllByComponent<RigidBodyComponent>())
    {
        var rb = node.GetComponent<RigidBodyComponent>()!;
        if (rb.BodyType == BodyType.Static) continue;  // Static bodies don't move

        ref var pose = ref _simulation.Bodies.GetBodyReference(rb.BodyHandle).Pose;
        node.LocalPosition = ToSystemVector(pose.Position);
        node.LocalRotation = ToSystemQuaternion(pose.Orientation);
    }
}
```

**Sync direction:** Physics → scene graph (one-way). The physics engine is the source of truth for dynamic body positions. Game code modifies bodies via forces/impulses, not by setting scene node transforms directly. Kinematic bodies are the exception — their transforms are set by game code and pushed to physics.

### Type conversion between System.Numerics and BepuPhysics2

BepuPhysics2 uses its own math types (`BepuUtilities.Vector3`, `BepuUtilities.Quaternion`). Conversion helpers are needed:

```csharp
internal static class BepuConvert
{
    public static BepuVector3 ToBepuVector(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
    public static System.Numerics.Vector3 ToSystemVector(BepuVector3 v) => new(v.X, v.Y, v.Z);
    public static BepuQuaternion ToBepuQuaternion(System.Numerics.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
    public static System.Numerics.Quaternion ToSystemQuaternion(BepuQuaternion q) => new(q.X, q.Y, q.Z, q.W);
}
```

### Open design decisions

**1. Physics library**

| Library | Language | Performance | .NET Integration | License |
|---------|----------|-------------|------------------|---------|
| **BepuPhysics2** | Pure C# | Excellent (SIMD, multithreaded) | NuGet, native .NET | Apache 2.0 |
| Jolt Physics | C++ (bindings) | Excellent | JoltPhysicsSharp NuGet | MIT |
| Bullet | C++ (bindings) | Good | BulletSharp NuGet | zlib |

**Recommendation:** BepuPhysics2. Pure C#, no native interop issues, active development by Norbo, excellent SIMD performance. The API is low-level but well-documented.

**2. Separate `YesZ.Physics` project vs inline in `YesZ.Core`**

| Option | Pros | Cons |
|--------|------|------|
| **Separate `YesZ.Physics`** | Physics dependency is optional, non-physics apps don't pull in BepuPhysics2 | Additional project, cross-project references |
| Inline in `YesZ.Core` | Simpler project structure | Forces BepuPhysics2 dependency on all YesZ users |

**Recommendation:** Separate project. Not every YesZ application needs physics. Keep the dependency optional.

**3. Collision callbacks — events vs polling**

| Option | Pros | Cons |
|--------|------|------|
| **Event callbacks** | Immediate notification, game logic reacts to collisions | BepuPhysics2 callbacks are on the physics thread, need marshaling |
| **Polling** | Game code queries collision state each frame | Missed transient contacts, more game-side code |

**Recommendation:** Event callbacks via BepuPhysics2's `INarrowPhaseCallbacks`. Queue collision events during physics step, dispatch on main thread after sync.

### Gotchas addressed

- **BepuPhysics2 coordinate system:** BepuPhysics2 is coordinate-system agnostic. Set gravity to `(0, -9.81, 0)` for Y-up (matching YesZ/glTF convention).
- **Buffer pool lifecycle:** BepuPhysics2 uses `BufferPool` for memory management. Must be disposed properly. Call `_bufferPool.Clear()` in `PhysicsWorld.Dispose()`.
- **Sleeping bodies:** BepuPhysics2 deactivates bodies at rest (sleeping). This is desirable for performance but means `SyncToScene` can skip sleeping bodies. Access body state via `Bodies.GetBodyReference(handle).Activity.SleepCandidate`.
- **Scale not supported by physics:** Physics shapes have fixed sizes. Non-uniform scale on a scene node does NOT affect the physics shape. If a node's scale changes, the physics shape must be recreated. Document this limitation.
- **Kinematic body movement:** Kinematic bodies must be moved via `Bodies.GetBodyReference(handle).Pose.Position = ...` before the physics step. They participate in collision detection but are not affected by forces.
- **Substep count:** BepuPhysics2's `SolveDescription(8, 1)` means 8 velocity iterations and 1 substep. Increase substeps for more stable stacking at the cost of performance.

### Tests (unit)

**`PhysicsWorldTests`** (YesZ.Physics.Tests — new test project)

| Test | What it proves |
|------|---------------|
| `FreeFall_MatchesKinematics` | Box at `y=5`, step 1s → `y ≈ 5 - 0.5×9.81×1² = 0.095` |
| `FreeFall_HitsFloor_Stops` | Dynamic box on static floor reaches rest at `y ≈ 0.5` (box half-height) |
| `Collision_TwoBodies_SeparateAfterImpact` | Two boxes moving toward each other bounce apart |
| `Static_NeverMoves` | Static body stays at initial position after impacts |
| `FixedTimestep_AccumulatesCorrectly` | `Update(0.017)` × 4 → 1 physics step at `dt=1/60` |
| `FixedTimestep_LargeDelta_MultipleSteps` | `Update(0.1)` → 6 steps at `dt=1/60` |

**`PhysicsSceneSyncTests`** (YesZ.Physics.Tests — new file)

| Test | What it proves |
|------|---------------|
| `Step_UpdatesSceneNodePosition` | After physics step, scene node `LocalPosition` matches body position |
| `Step_UpdatesSceneNodeRotation` | Scene node `LocalRotation` matches body rotation |
| `AddBody_ThenRemoveNode_BodyCleanedUp` | No orphaned physics bodies |

### Verification

- A box dropped from height `y=5` falls under gravity and lands on a static floor at `y=0`
- Two dynamic boxes collide and push each other apart
- A stack of 5 boxes is stable (doesn't collapse for at least 5 seconds)
- Static objects (floor, walls) don't move regardless of impacts
- Kinematic body can be moved programmatically and pushes dynamic bodies
- Physics and rendering stay in sync (no visible jitter at 60 FPS)
- Performance: 100 active bodies simulate at 60 Hz without frame drops

**Estimated scope:** ~5-7 files, L2

---

## Future Considerations

The following are **not** on the current roadmap but may become phases as the engine matures:

| Area | Description | Likely depends on |
|------|-------------|-------------------|
| **SSAO** | Screen-space ambient occlusion for contact shadows | 7a (post-process pipeline) |
| **FXAA / TAA** | Anti-aliasing as a post-process pass | 7a |
| **Depth of Field** | Camera focus blur effect | 7a |
| **3D Particles** | GPU-driven particle systems (fire, smoke, sparks) | 9 (instancing), 2 (materials) |
| **Terrain** | Heightmap-based terrain with LOD | 8c (spatial partitioning) |
| **Water / Ocean** | Reflective/refractive water surfaces | 7a (render-to-texture for reflections) |
| **Deferred Rendering** | G-buffer based rendering for many-light scenes | 7a (render targets), 3c (lighting) |
| **Animation Blending** | Crossfade, additive blending, animation layers | 5d (skeletal animation) |
| **Animation State Machine** | FSM/graph driving animation transitions | Animation blending |
| **Morph Targets** | Blend shapes for facial animation | 5d, 4a (glTF parsing) |
| **IK** | Inverse kinematics solvers (CCD, FABRIK, two-bone) | 5d |
| **Audio (3D spatial)** | Positional audio with distance attenuation | 8a (scene graph) |
| **Networking** | Multiplayer state sync | Application-level |
| **Editor / Scene Serialization** | Visual scene editor, save/load scenes | 8a (scene graph) |

---

## Key Constraints

- **3D draws go through NoZ's batch/sort system.** Graphics3D must use `Graphics.DrawElements()` to queue commands — no raw WebGPU draw calls. This ensures 2D/3D coexistence.
- **Fork changes are additive only.** New fields on internal structs, conditional depth state. Never modify existing NoZ behavior.
- **Each phase has a testable milestone.** No phase is "done" until its milestone is demonstrable.
- **Max 64 bones per skeleton** — NoZ's `Skeleton.MaxBones` limit. Sufficient for most game characters. Exceeding this would require changes to NoZ's bone texture layout.
