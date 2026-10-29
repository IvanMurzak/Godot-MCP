# AGENTS.md — Godot-AI-PhantomCamera

A **Godot-MCP extension** for the third-party [**Phantom Camera**](https://github.com/ramokz/phantom-camera) addon
(Cinemachine-style virtual cameras), shipped as an **RQL module**: it adds no MCP tools, only what the core
`rql-query` / `rql-mutate` / `rql-schema` tools need to work with phantom cameras (the `phantomcamera` source with
validated mode calls and an agent guide rendered into the core `godot-rql` skill). A **source-only NuGet package**
(`com.IvanMurzak.Godot.MCP.PhantomCamera`) compiled inside the consumer's Godot project against the consumer's own
GodotSharp; the packaging recipe is in `docs/source-only-nuget-recipe.md`.

## Layout

- `src/Godot-AI-PhantomCamera/Editor/Rql/` — the module:
  - `PhantomCameraModule.cs` — the `IRqlModule` (Order 100, `#if TOOLS`): the `phantomcamera` source and the guide.
  - `PhantomCameraApi.cs` (the source: `installed`, `hint`, `cameras`, `hosts`), `PhantomCameraRow.cs` (camera row;
    `SetFollow` / `SetLookAt`), `PhantomCameraHostRow.cs`, `PhantomCameraScripts.cs` (global script classes of a
    node) — `#if TOOLS`.
  - `PhantomCameraNames.cs` (addon class / property names, install hint), `PhantomCameraModes.cs` (mode enums, names),
    `PhantomCameraRules.cs` (validation) and `PhantomCameraGuide.cs` (the guide) — pure-managed, NOT behind `#if TOOLS`.
- `src/Godot-AI-PhantomCamera/build/com.IvanMurzak.Godot.MCP.PhantomCamera.props` — source-injection props,
  auto-imported by NuGet in the consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-PhantomCamera.Tests/` — xUnit (no Godot binary): rules and mode names, guide examples parse, the
  module's view types (GodotSharp metadata, `TOOLS` defined), and the e2e manifest runs every guide example.
- `tests/e2e/e2e-tools.json` — the live calls (`rql-*`), driven by `scripts/extension_e2e.py` from the monorepo suite, which
  installs the pinned addon (`extension.json` `testAddon`) and registers its `PhantomCameraManager` autoload.
- `testbed/PhantomCamera-Testbed.csproj` — consumer project restoring the local-packed package (injection proof).
- Versions and core pins are set from the monorepo root: `scripts/extension_release.py bump` / `version`,
  `scripts/update_core.py`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

## Critical invariants

- **No compile-time dependency on Phantom Camera.** Its classes are GDScript `class_name`s: no C# type (so no adapter
  can target them), not in ClassDB, `GetClass()` returns the native base. They are named by string only
  (`PhantomCameraNames`) and recognized through the node's script chain against `ProjectSettings.GetGlobalClassList()`.
  `dotnet build -c Debug` must pass with the addon absent, and configuring the module never touches Godot.
- **Rows are read-only views; plain writes go through `node`** (`get node | set props["priority"] = 50`): the
  core's write, undoable, marks the scene unsaved. Creation is the core `AddChild` (global script classes).
- **`SetFollow` / `SetLookAt`** validate everything (`PhantomCameraRules`) before writing, set the mode BEFORE the
  target (the addon's target setters return early while the mode is None) and verify the target took. They are calls
  on the row, so they are NOT undoable: they call `MarkSceneAsUnsaved` themselves — keep that documented.
- **Absent addon:** `installed` false, `hint` = the install hint, empty lists; never throw.
- Every guide example runs, in order, in `tests/e2e/e2e-tools.json` (xUnit-asserted). The module uses only Godot,
  `com.IvanMurzak.Rql` and the BCL, never core-addon types (the authoring build has no core).

## Build / test (no Godot binary, addon absent)

```bash
dotnet build src/Godot-AI-PhantomCamera/Godot-AI-PhantomCamera.csproj -c Debug   # the module (#if TOOLS included)
dotnet test  tests/Godot-AI-PhantomCamera.Tests/Godot-AI-PhantomCamera.Tests.csproj
```

The live suite (one headless editor for every module, the pinned addon installed) is
`python scripts/test_extensions.py` from the monorepo root (Godot 4.4+: `minGodotVersion`). `dotnet pack` reuses
the **global NuGet cache** for an already-cached version: when re-packing the same `Version`, clear
`~/.nuget/packages/com.ivanmurzak.godot.mcp.phantomcamera/<ver>` (or pack a unique version).

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.PhantomCamera`. Every `.cs` starts with the Apache-2.0 header and
  `#nullable enable`.
- The package declares only the `com.IvanMurzak.McpPlugin` / `ReflectorNet` / `Rql` pins (equal to the core's); **GodotSharp and the
  Phantom Camera addon must never become package dependencies** (CI asserts the nuspec). Bump them with
  `scripts/update_core.py`.
