# YesZ

[![CI](https://github.com/JMRussas/yesz/actions/workflows/ci.yml/badge.svg)](https://github.com/JMRussas/yesz/actions/workflows/ci.yml)

**[Engineering case study](docs/case-study.md)** — decisions, failure analysis, verification, and limits.

3D extension of the [NoZ game engine](https://github.com/JMRussas/noz-cs). Adds perspective cameras, 3D mesh rendering, glTF model loading, PBR materials, and lighting to NoZ's 2D foundation.

Built in C# on .NET 10 with WebGPU rendering.

## Build & Run

```bash
git clone --recurse-submodules https://github.com/JMRussas/yesz.git
cd yesz
dotnet build yesz.slnx          # Build all projects
dotnet test yesz.slnx           # Run all tests
dotnet run --project samples/HelloCube/HelloCube.csproj   # Run sample
```

## Requirements

- .NET 10 SDK

```
winget install Microsoft.DotNet.SDK.10
```

## Project Structure

```
yesz/
  engine/
    noz/                         NoZ 2D engine (submodule)
  src/
    YesZ.Core/                   3D math, transforms, camera
    YesZ.Rendering/              3D render pipeline, materials, lighting
    YesZ.Desktop/                Desktop launcher (SDLPlatform + WebGPU)
  samples/
    HelloCube/                   Minimal sample — window + UI + spinning cube
  tests/
    YesZ.Core.Tests/             Transform3D, Camera3D tests (xUnit)
    YesZ.Rendering.Tests/        Rendering pipeline tests (xUnit)
```

## Architecture

YesZ layers on a pinned NoZ fork with additive driver changes, including `IGraphicsDriver3D`. The upstream NoZ engine supplies the 2D and platform foundation; YesZ supplies the 3D extension. 3D rendering is bracketed with `Graphics3D.Begin()` / `Graphics3D.End()` — NoZ's 2D UI renders after `End()`, so 2D and 3D coexist in the same scene.

See [.claude/architecture.md](.claude/architecture.md) for the full layer diagram and NoZ integration details.

## License

[AGPL-3.0-or-later](LICENSE)
