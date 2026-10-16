# AGENTS.md — Godot-AI-Dialogic

A **Godot-MCP extension** wrapping the third-party [**Dialogic**](https://github.com/dialogic-godot/dialogic)
addon (dialogue / visual-novel authoring). Shipped as a **source-only NuGet package**
(`com.IvanMurzak.Godot.MCP.Dialogic`) that compiles inside the consumer's Godot project against the consumer's
own GodotSharp. Created from
[`Godot-AI-Tools-Template`](https://github.com/IvanMurzak/Godot-MCP/tree/main/templates/extension). The packaging recipe is
the load-bearing detail — read `docs/source-only-nuget-recipe.md`.

## Addon-dependent (Class B) — the load-bearing rules

Dialogic is a third-party GDScript addon whose classes are NOT in GodotSharp, and the package must not depend
on it:

- **No compile-time dependency.** Reference Dialogic classes **by string name only**, resolved at runtime.
  `dotnet build -c Debug` MUST exit 0 with the addon ABSENT (the no-dependency gate).
- **Resolution:** Dialogic registers GDScript `class_name` types in the **global script-class list**, not
  `ClassDB`: resolve via `ProjectSettings.GetGlobalClassList()` + `GD.Load`. `ClassDB.ClassExists` returns
  `false` for a `class_name` type even when installed (the #1 trap).
- **Presence gate first.** Every editor tool's first action inside `MainThread.Instance.Run(...)` checks
  `AddonInterop.GlobalClassExists(DialogicAddon.PresenceClass)` (`DialogicTimeline`) and, when missing,
  returns the structured `installed: false` result (`AddonGateResult`) with an install hint — never a raw
  throw (an opaque HTTP 500 to the LLM).
- **Names are the contract.** Drive addon objects via `GodotObject.Set/Get/Call` with GDScript **snake_case**
  member names (not C# PascalCase); event types and resource extensions are plain strings. Keep these name
  constants centralized in `Runtime/Dialogic/` (`DialogicAddon`, `DialogicResourceKind`, …) and unit-tested —
  there are no compile-time types to lean on.
- The addon is **never** vendored, submoduled, or downloaded by this repo; installing it is the consumer's
  job. CI installs the pinned `dialogic-godot/dialogic` @ `2.0-alpha-19` into `testbed/addons/dialogic/` only
  for the e2e leg (`ADDON_*` in `.github/workflows/test.yml`).

**Scope: resource-level authoring.** The tools author Dialogic **timeline** (`.dtl`) and **character**
(`.dch`) resources — not the game-runtime API (`Dialogic.start(...)`). Dialogic's timeline/event internals are
the most version-volatile of the Class-B addons, so keep the family to create / append / read.

## Layout

- `src/Godot-AI-Dialogic/` — the source-only package (`Godot.NET.Sdk`):
  - `Runtime/Tools/` — `Tool_Dialogic.cs` (the `[AiToolType]` partial class), `Tool_Dialogic.Ids.cs` (all
    tool-id consts, pinned by tests), and the pure-managed `Tool_Dialogic.Defaults.cs`.
  - `Runtime/Interop/AddonInterop.cs` — name resolution + instantiation; entirely behind `#if TOOLS`,
    E2E-verified, never unit-tested.
  - `Runtime/Dialogic/` — `AddonGateResult` (+ `NotInstalled(...)`), `DialogicAddon` (addon name, presence
    class, snake_case names, install hint), `DialogicResourceKind` (`.dtl`/`.dch`), `DialogicTimelineText`,
    `DialogicColor`, `DialogicDefaults`, `DialogicResourceInfo`; all unit-tested.
  - `Editor/Tools/` — one file per editor tool (`Tool_Dialogic.<Name>.cs`) + shared helpers in
    `Tool_Dialogic.Editor.cs`; all behind `#if TOOLS`.
  - `build/com.IvanMurzak.Godot.MCP.Dialogic.props` — source-injection props, auto-imported by NuGet in the
    consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-Dialogic.Tests/` — xUnit specs for the pure-managed sources only (no Godot binary).
- `tests/e2e/` — live `run-tool` E2E: the tool list `e2e-tools.json`, driven by `run_tool_e2e.py`.
- `testbed/Dialogic-Testbed.csproj` — consumer `Godot.NET.Sdk` project restoring the local-packed package; its
  `dotnet build` is the source-injection proof.
- `commands/` — `bump-version`, `update-core`, `init` (`.ps1` + `.py`), `get-version.ps1`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

- `docs/` — `source-only-nuget-recipe.md` (the packaging recipe + consumer story); `ci.md` (workflows, version
  gate, multi-Godot matrix, NuGet publishing setup).

## Tools

Ids live in `Runtime/Tools/Tool_Dialogic.Ids.cs` (the source of truth). Current tools: `dialogic-defaults`
(pure-managed), `dialogic-timeline-create`, `dialogic-character-create`, `dialogic-timeline-add-text`,
`dialogic-get`.

## Build / test (no Godot binary, addon absent)

```bash
dotnet build src/Godot-AI-Dialogic/Godot-AI-Dialogic.csproj  # source-only package compiles tools
dotnet test  tests/Godot-AI-Dialogic.Tests/Godot-AI-Dialogic.Tests.csproj
dotnet pack  src/Godot-AI-Dialogic/Godot-AI-Dialogic.csproj -p:Version=0.0.0-ci -o local-nuget
dotnet build testbed/Dialogic-Testbed.csproj                 # consumes the local package (injection proof)
```

`Godot.NET.Sdk` supplies GodotSharp from NuGet, so build/test/pack and the source-injection proof need no
Godot install (the testbed build is a faithful proxy for `godot --build-solutions`). CI proves the recipe
across Godot 4.4 / 4.5 (the central monorepo SDK matrix). `dotnet pack` reuses the **global NuGet cache**
for an already-cached version: when re-packing the same `Version`, clear
`~/.nuget/packages/com.ivanmurzak.godot.mcp.dialogic/<ver>` (or pack a unique version) before re-restoring the
testbed, or you silently build the stale cached source.

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.Dialogic`. Every `.cs` starts with the Apache-2.0 header.
- Pure-managed code (tool ids, the `AddonGateResult` shape, name/enum-int constants, defaults) → `Runtime/`
  (outside `#if TOOLS`, unit-testable); editor-driving tools + `AddonInterop` → `#if TOOLS` (every Godot call
  via `MainThread.Instance.Run(...)`, E2E-verified).
- The package declares ONLY the `com.IvanMurzak.McpPlugin` / `com.IvanMurzak.ReflectorNet` min-version deps;
  **GodotSharp and the Dialogic addon must never become a package dependency** (CI asserts the nuspec). Keep
  the MCP pins in lockstep with the core Godot-MCP addon; bump with `commands/update-core.ps1`.
- One `[AiToolType] partial class Tool_Dialogic`; one `[AiTool]` method per partial-class file. Add every new
  pure-managed source to the test csproj `<Compile Include>` list so it is unit-tested.
