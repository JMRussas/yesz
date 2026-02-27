# YesZ

3D extension of the NoZ game engine. Adds perspective cameras, 3D mesh rendering, glTF model loading, PBR materials, and lighting to NoZ's 2D foundation.

## Build & Run

```bash
dotnet build yesz.slnx          # Build all projects
dotnet test yesz.slnx           # Run all tests (9 currently)
dotnet run --project samples/HelloCube/HelloCube.csproj   # Run sample
```

## Project Structure

```
yesz/
  engine/
    noz/                         Forked NoZ engine (submodule → JMRussas/noz-cs)
  src/
    YesZ.Core/                   3D math, transforms, camera (no rendering dependency)
    YesZ.Rendering/              3D render pipeline, Graphics3D, materials, lighting
    YesZ.Desktop/                Desktop launcher (wraps NoZ SDLPlatform + WebGPU)
  samples/
    HelloCube/                   Minimal sample — window + UI + (Phase 1G: spinning cube)
  tests/
    YesZ.Core.Tests/             Transform3D, Camera3D tests (xUnit)
    YesZ.Rendering.Tests/        Rendering pipeline tests (xUnit)
  yesz.slnx                     Solution file (XML format)
```

## Deep-Dive Docs

| Doc | When to read |
|-----|-------------|
| [.claude/architecture.md](.claude/architecture.md) | System architecture, layer diagram, NoZ integration |
| [.claude/maintenance.md](.claude/maintenance.md) | Upstream merge procedures, fork change log |
| [.claude/noz-internals.md](.claude/noz-internals.md) | NoZ engine architecture reference |
| [.claude/phase1-plan.md](.claude/phase1-plan.md) | Phase 1 sub-phases, fork changes, design decisions |

## Conventions

- **Namespace**: `YesZ` for core, `YesZ.Rendering` for rendering, `YesZ.Desktop` for platform
- **Naming**: PascalCase for classes/types, camelCase for locals, follows NoZ conventions
- **Target**: .NET 10.0, same as NoZ
- **Config**: No hardcoded values — expose via constructor params or config objects
- **3D/2D coexistence**: `Graphics3D.Begin()` / `Graphics3D.End()` brackets 3D rendering; NoZ's 2D UI renders after `End()`
- **Fork changes**: All modifications to `engine/noz/` must be documented in `.claude/maintenance.md`
- **Testing**: xUnit, test pure math/logic without GPU dependencies

## Git Workflow

| Setting | Value |
|---------|-------|
| workflow | pr |
| base_branch | main |
| branch_protection | no |
| ci_gate | required |
| squash_merge | yes |

## Submodule Management

```bash
# Update NoZ submodule to latest upstream
cd engine/noz
git fetch upstream
git merge upstream/main
git push origin main
cd ../..
git add engine/noz
git commit -m "chore: merge upstream NoZ changes"
```
