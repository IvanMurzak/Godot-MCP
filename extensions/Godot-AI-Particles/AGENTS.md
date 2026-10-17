# AGENTS.md — Godot-AI-Particles

A **Godot-MCP extension** for Godot's built-in `GPUParticles3D` / `GPUParticles2D` nodes, shipped as an **RQL
module**: it adds no MCP tools, only what the core `rql-query` / `rql-mutate` / `rql-schema` tools need to work
with emitters (validated members, `Restart()`, an agent guide rendered into the core `godot-rql` skill). A
**source-only NuGet package** (`com.IvanMurzak.Godot.MCP.Particles`) compiled inside the consumer's Godot project
against the consumer's own GodotSharp; the packaging recipe is in `docs/source-only-nuget-recipe.md`.

## Layout

- `src/Godot-AI-Particles/Editor/Rql/` — the module:
  - `ParticlesModule.cs` — the `IRqlModule` (Order 100, `#if TOOLS`): the two adapters and the guide.
  - `Godot_GpuParticles3D_Adapter.cs`, `Godot_GpuParticles2D_Adapter.cs` — one adapter per type (`#if TOOLS`);
    their shared members live in `ParticlesMembers.cs`.
  - `ParticlesRules.cs` (validation) and `ParticlesGuide.cs` (the guide) — pure-managed, NOT behind `#if TOOLS`.
- `src/Godot-AI-Particles/build/com.IvanMurzak.Godot.MCP.Particles.props` — source-injection props, auto-imported
  by NuGet in the consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-Particles.Tests/` — xUnit (no Godot binary): rules, guide examples parse, the module's member
  tables (GodotSharp metadata, `TOOLS` defined), and the e2e manifest runs every guide example.
- `tests/e2e/e2e-tools.json` — the live calls (`rql-*`), driven by `run_tool_e2e.py` from the monorepo suite.
- `testbed/Particles-Testbed.csproj` — consumer project restoring the local-packed package (injection proof).
- `commands/` — `bump-version`, `update-core`, `init` (`.ps1` + `.py`), `get-version.ps1`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

## Critical invariants

- **RQL surface.** Everything is registered by `ParticlesModule` and documented in `ParticlesGuide` (topic
  "Particles"). Every guide example runs, in order, in `tests/e2e/e2e-tools.json` (xUnit-asserted).
- **Validated members replace the C# properties** of the same name (`Amount`, `Lifetime`, `Explosiveness`,
  `Randomness`) and read / write the Godot property by name, so one implementation serves both emitter types
  (they share no emitter base class). A rejected value fails the whole mutation; never clamp silently.
- The module uses only Godot, `com.IvanMurzak.Rql` and the BCL, never core-addon types (the authoring build has
  no core). Undo, dirty marking and saving come from the core's mutation hooks.

## Build / test (no Godot binary)

```bash
dotnet build src/Godot-AI-Particles/Godot-AI-Particles.csproj -c Debug   # the module (#if TOOLS included)
dotnet test  tests/Godot-AI-Particles.Tests/Godot-AI-Particles.Tests.csproj
```

The live suite (one headless editor for every module) is `python scripts/test_extensions.py` from the monorepo
root. `dotnet pack` reuses the **global NuGet cache** for an already-cached version: when re-packing the same
`Version`, clear `~/.nuget/packages/com.ivanmurzak.godot.mcp.particles/<ver>` (or pack a unique version).

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.Particles`. Every `.cs` starts with the Apache-2.0 header and
  `#nullable enable`.
- The package declares only the `com.IvanMurzak.McpPlugin` / `ReflectorNet` / `Rql` floors; **GodotSharp must
  never become a package dependency** (CI asserts the nuspec). Bump floors with `commands/update-core.ps1`.
