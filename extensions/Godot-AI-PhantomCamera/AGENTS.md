# AGENTS.md — Godot-AI-PhantomCamera

A **Godot-MCP extension** wrapping the third-party [**Phantom
Camera**](https://github.com/ramokz/phantom-camera) addon (Cinemachine-style virtual cameras). Shipped as a
**source-only NuGet package** (`com.IvanMurzak.Godot.MCP.PhantomCamera`) that compiles inside the consumer's
Godot project against the consumer's own GodotSharp. Created from
[`Godot-AI-Tools-Template`](https://github.com/IvanMurzak/Godot-MCP/tree/main/templates/extension). The packaging recipe is
the load-bearing detail — read `docs/source-only-nuget-recipe.md`.

## Addon-dependent (Class B) — the load-bearing rules

Phantom Camera is a third-party GDScript addon whose classes are NOT in GodotSharp, and the package must not
depend on it:

- **No compile-time dependency.** Reference Phantom Camera classes **by string name only**, resolved at
  runtime. `dotnet build -c Debug` MUST exit 0 with the addon ABSENT (the no-dependency gate).
- **Resolution:** Phantom Camera registers GDScript `class_name` types in the **global script-class list**,
  not `ClassDB`: resolve via `ProjectSettings.GetGlobalClassList()` + `GD.Load`. `ClassDB.ClassExists` returns
  `false` for a `class_name` type even when installed (the #1 trap).
- **Presence gate first.** Every editor tool's first action inside `MainThread.Instance.Run(...)` checks
  `AddonInterop.GlobalClassExists(PhantomCameraNames.PhantomCamera3D)` and, when missing, returns the
  structured `installed: false` result (`AddonGateResult`) with an install hint — never a raw throw (an opaque
  HTTP 500 to the LLM).
- **Names are the contract.** Drive addon objects via `GodotObject.Set/Get/Call` with GDScript **snake_case**
  member names (not C# PascalCase); enums are plain ints. Keep these name constants centralized in
  `Runtime/PhantomCamera/PhantomCameraEnums.cs` and unit-tested — there are no compile-time types to lean on.
- The addon is **never** vendored, submoduled, or downloaded by this repo; installing it is the consumer's
  job. CI installs the pinned `ramokz/phantom-camera` @ `v0.11.0.2` into `testbed/addons/phantom_camera/` only
  for the e2e leg (`ADDON_*` in `.github/workflows/test.yml`).

## Layout

- `src/Godot-AI-PhantomCamera/` — the source-only package (`Godot.NET.Sdk`):
  - `Runtime/Tools/` — `Tool_PhantomCamera.cs` (the `[AiToolType]` partial class), `Tool_PhantomCamera.Ids.cs`
    (all tool-id consts, pinned by tests), and the pure-managed `Tool_PhantomCamera.Defaults.cs`.
  - `Runtime/Interop/` — `AddonInterop.cs` (name resolution + instantiation; entirely behind `#if TOOLS`,
    E2E-verified, never unit-tested), `AddonGate.cs` (the `AddonGateResult` shape + `NotInstalled(...)`
    factory; pure-managed, unit-tested).
  - `Runtime/PhantomCamera/` — `PhantomCameraEnums.cs` (`PhantomCameraNames` snake_case names + follow/look-at
    enum ints), `PhantomCameraDefaults`, `PhantomCameraInfo`; all unit-tested.
  - `Editor/Tools/` — one file per editor tool (`Tool_PhantomCamera.<Name>.cs`) + shared helpers in
    `Tool_PhantomCamera.Editor.cs`; all behind `#if TOOLS`.
  - `build/com.IvanMurzak.Godot.MCP.PhantomCamera.props` — source-injection props, auto-imported by NuGet in
    the consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-PhantomCamera.Tests/` — xUnit specs for the pure-managed sources only (no Godot binary).
- `tests/e2e/` — live `run-tool` E2E: the tool list `e2e-tools.json`, driven by `run_tool_e2e.py`.
- `testbed/PhantomCamera-Testbed.csproj` — consumer `Godot.NET.Sdk` project restoring the local-packed
  package; its `dotnet build` is the source-injection proof.
- `commands/` — `bump-version`, `update-core`, `init` (`.ps1` + `.py`), `get-version.ps1`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

- `docs/` — `source-only-nuget-recipe.md` (the packaging recipe + consumer story); `ci.md` (workflows, version
  gate, multi-Godot matrix, NuGet publishing setup).

## Tools

Ids live in `Runtime/Tools/Tool_PhantomCamera.Ids.cs` (the source of truth). Current tools:
`phantomcamera-defaults` (pure-managed), `phantomcamera-host-create`, `phantomcamera-create`,
`phantomcamera-set-follow`, `phantomcamera-set-look-at`, `phantomcamera-set-priority`, `phantomcamera-get`.

The addon **requires** a `PhantomCameraHost` companion (under the `Camera3D`), or a PhantomCamera does nothing
— `phantomcamera-host-create` ensures one exists.

## Build / test (no Godot binary, addon absent)

```bash
dotnet build src/Godot-AI-PhantomCamera/Godot-AI-PhantomCamera.csproj  # source-only package compiles tools
dotnet test  tests/Godot-AI-PhantomCamera.Tests/Godot-AI-PhantomCamera.Tests.csproj
dotnet pack  src/Godot-AI-PhantomCamera/Godot-AI-PhantomCamera.csproj -p:Version=0.0.0-ci -o local-nuget
dotnet build testbed/PhantomCamera-Testbed.csproj                      # consumes the local package (injection proof)
```

`Godot.NET.Sdk` supplies GodotSharp from NuGet, so build/test/pack and the source-injection proof need no
Godot install (the testbed build is a faithful proxy for `godot --build-solutions`). CI proves the recipe
across Godot 4.4 / 4.5 (the `test-pull-request.yml` matrix). `dotnet pack` reuses the **global NuGet cache**
for an already-cached version: when re-packing the same `Version`, clear
`~/.nuget/packages/com.ivanmurzak.godot.mcp.phantomcamera/<ver>` (or pack a unique version) before
re-restoring the testbed, or you silently build the stale cached source.

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.PhantomCamera`. Every `.cs` starts with the Apache-2.0 header.
- Pure-managed code (tool ids, the `AddonGateResult` shape, name/enum-int constants, defaults) → `Runtime/`
  (outside `#if TOOLS`, unit-testable); editor-driving tools + `AddonInterop` → `#if TOOLS` (every Godot call
  via `MainThread.Instance.Run(...)`, E2E-verified).
- The package declares ONLY the `com.IvanMurzak.McpPlugin` / `com.IvanMurzak.ReflectorNet` min-version deps;
  **GodotSharp and the Phantom Camera addon must never become a package dependency** (CI asserts the nuspec).
  Keep the MCP pins in lockstep with the core Godot-MCP addon; bump with `commands/update-core.ps1`.
- One `[AiToolType] partial class Tool_PhantomCamera`; one `[AiTool]` method per partial-class file. Add every
  new pure-managed source to the test csproj `<Compile Include>` list so it is unit-tested.
