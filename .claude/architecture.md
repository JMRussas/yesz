# YesZ architecture

YesZ adds a 3D layer alongside NoZ's 2D rendering and UI. The pinned NoZ fork supplies the shared graphics context and platform implementation, including additive 3D driver operations.

## Layers

```text
Game / HelloCube sample
  ├── NoZ: 2D UI and sprites
  └── YesZ: camera, meshes, materials, lights, animation
        └── Graphics3D: command collection and 3D passes
              └── IGraphicsDriver + IGraphicsDriver3D
                    └── NoZ fork: WebGPU backend and SDL platform
```

## Current render flow

`Graphics3D` owns its draw-command lists. It does not submit 3D draws through NoZ's 2D batch queue.

1. `Begin(camera)` records the camera and clears the frame's command state.
2. `DrawMesh` collects scene draw commands and eligible shadow casters.
3. `End()` performs enabled shadow-depth passes into the cascade depth texture array, then the 3D scene pass.
4. Draws upload the uniforms required by their material path: unlit uses a per-object MVP; lit uses camera VP plus per-object model and normal matrices.
5. The prepass flag lets NoZ's subsequent 2D pass preserve the 3D color output with `LoadOp.Load` and overlay its UI.

See [Graphics3D.cs](../src/YesZ.Rendering/Graphics3D.cs) for ordering and state transitions. The previous batch-based design was replaced in [PR #17](https://github.com/JMRussas/yesz/pull/17).

## Integration boundary

`IGraphicsDriver3D` separates operations needed for 3D scene passes and depth texture arrays. This remains a fork integration, not a claim of compatibility with an unmodified upstream engine. Use the repository's pinned submodule revision.

## Projects and verification

| Project | Responsibility |
|---|---|
| YesZ.Core | 3D math, transforms, camera, mesh data, glTF and animation |
| YesZ.Rendering | Materials, lights, shadow configuration, shaders, pass execution |
| YesZ.Desktop | Desktop launcher using NoZ's platform and graphics backend |
| HelloCube | Interactive sample |
| YesZ.Core.Tests | Math, glTF, animation, lighting-data tests |
| YesZ.Rendering.Tests | Uniform layouts and rendering contracts |

Automated checks establish these contracts, not cross-driver visual correctness or throughput. See the [case study](../docs/case-study.md) and [roadmap](roadmap.md).
