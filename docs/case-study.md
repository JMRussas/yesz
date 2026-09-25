# Case study: adding 3D rendering to a 2D engine

**Scope:** YesZ adds a C# / WebGPU 3D layer to [NoZ](https://github.com/nozgames/noz-cs). The upstream engine is the foundation; this repository contributes the 3D extension and its integration. The pinned engine submodule is a fork with required additions.

## Problem and constraints

Perspective cameras, mesh loading, lighting, and shadows need to coexist with NoZ's 2D UI and graphics context. Reusing the engine avoids rebuilding its platform layer, but coupling 3D rendering to the 2D batch implementation makes both systems harder to evolve.

## Decisions

- **Collect commands, then execute passes.** `DrawMesh` records scene commands; `End` executes shadow and scene passes. The 3D layer uses driver calls instead of NoZ's 2D batch queue. [Graphics3D](../src/YesZ.Rendering/Graphics3D.cs).
- **Separate the additional driver contract.** `IGraphicsDriver3D` exposes 3D-specific operations, making the boundary explicit and narrowing upstream changes. [Refactor PR](https://github.com/JMRussas/yesz/pull/17).
- **Use a depth texture array for cascaded shadows.** Array layers replace separate depth textures and repeated shader dispatch paths. See the same refactor and [shadow configuration tests](../tests/YesZ.Rendering.Tests/ShadowConfigTests.cs).
- **Test math and binary contracts separately from visual output.** Camera, transforms, glTF accessors, skinning, and uniform layouts have automated coverage. [Core tests](../tests/YesZ.Core.Tests) and [rendering tests](../tests/YesZ.Rendering.Tests).

## A rendering failure corrected during the refactor

Unlit draws upload a per-object model-view-projection matrix into `viewproj`. A following lit draw needs the camera view-projection matrix instead. Leaving the previous value renders with the wrong transform. The refactor tracks that state and re-uploads the appropriate matrix before lit draws. [Implementation and review history](https://github.com/JMRussas/yesz/pull/17).

## Evidence and limits

[CI](../.github/workflows/ci.yml) checks out the engine submodule, builds, and runs tests on Windows. These tests support mathematical and integration contracts; they do not establish pixel-perfect rendering across GPU drivers or a frame-rate benchmark.

The architectural outcome is a narrower boundary with the 2D engine and explicit ownership of 3D passes. Performance and cross-platform claims require further measurements.

## Run the sample

```bash
git clone --recurse-submodules https://github.com/JMRussas/yesz.git
cd yesz
dotnet build yesz.slnx
dotnet test yesz.slnx
dotnet run --project samples/HelloCube/HelloCube.csproj
```

Requires .NET 10 and a graphics environment supported by the pinned NoZ/WebGPU backend. See the [current render flow](../.claude/architecture.md).
