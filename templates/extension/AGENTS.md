# AGENTS.md — Godot-AI-Tools-Template

**Template repo** for Godot-MCP extension packages (the Godot analog of `Unity-AI-Tools-Template`).
`commands/init.ps1` / `init.py` turn its placeholder boilerplate into a real extension, so every change
here propagates to future extensions — keep placeholder tokens consistent.

## Placeholders (replaced by `init`)

| Token | Example value | Used in |
| --- | --- | --- |
| `YOUR_FEATURE` | `Particles` | namespaces, `Tool_<Feature>`, folder/file names, the package id suffix |
| `YOUR_TOOL_PREFIX` | `particles` | tool ids (`<prefix>-echo`, …) |
| `YOUR_DISPLAY_NAME` | `Particles Tools` | package `Title`, docs |
| `YOUR_DESCRIPTION` | `AI MCP tools for Godot Particles.` | package `Description` |
| `YOUR_GITHUB_USERNAME_REPOSITORY` | `IvanMurzak/Godot-AI-Particles` | repo URLs |

The package id is always `com.IvanMurzak.Godot.MCP.<Feature>`, written literally as
`com.IvanMurzak.Godot.MCP.YOUR_FEATURE`. `init` also renames `build/<id>.props` and activates CI
(`*.yml-sample` → `*.yml`).

## Layout

- `src/Godot-AI-YOUR_FEATURE/` — the source-only package (`Godot.NET.Sdk`).
  - `Runtime/Tools/` — `[AiToolType]` root + pure-managed tools (outside `#if TOOLS`, unit-testable).
  - `Editor/Tools/` — editor-driving tools (behind `#if TOOLS`, main-thread-marshalled, E2E-verified only).
  - `build/com.IvanMurzak.Godot.MCP.YOUR_FEATURE.props` — source-injection props (must stay `<PackageId>.props`).
- `tests/Godot-AI-YOUR_FEATURE.Tests/` — xUnit specs for pure-managed sources only; add each new one to
  the test csproj `<Compile Include>` list.
- `tests/e2e/` — live `run-tool` E2E driver (`run_tool_e2e.py`) + per-extension tool list (`e2e-tools.json`).
- `testbed/` — consumer `Godot.NET.Sdk` project restoring the local-packed package (injection proof).
- `commands/` — `init`, `bump-version`, `update-core` (`.ps1` + `.py`), `get-version.ps1`.
- `.github/workflows/*.yml-sample` — CI, inert until `init`. Never commit live `.github/workflows/*.yml` here.
- `docs/` — `source-only-nuget-recipe.md` (the packaging recipe, the centerpiece, + consumer story);
  `ci.md` (workflows, version gate, multi-Godot matrix, the `NUGET_API_KEY` secret).
- `README.md` — user walkthrough: scaffold → init → write → build/test → publish → install.

## Build / test (no Godot binary)

```bash
dotnet build src/Godot-AI-YOUR_FEATURE/Godot-AI-YOUR_FEATURE.csproj   # source-only package compiles tools
dotnet test  tests/Godot-AI-YOUR_FEATURE.Tests/Godot-AI-YOUR_FEATURE.Tests.csproj
dotnet pack  src/Godot-AI-YOUR_FEATURE/Godot-AI-YOUR_FEATURE.csproj -p:Version=0.0.0-ci -o local-nuget
dotnet build testbed/YOUR_FEATURE-Testbed.csproj                      # consumes the local package (injection proof)
```

`Godot.NET.Sdk` supplies GodotSharp from NuGet, so build/test/pack and the source-injection proof need no
Godot install (the testbed build is a faithful proxy for `godot --build-solutions`).

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.<Feature>` (mirrors the core addon).
- One `[AiToolType] partial class Tool_<Feature>`; one `[AiTool]` method per partial-class file.
- Core MCP pins (`com.IvanMurzak.McpPlugin`, `com.IvanMurzak.ReflectorNet`) must match the core Godot-MCP
  addon; bump everywhere with `commands/update-core.ps1`. GodotSharp must never become a package dep (see
  the recipe; CI asserts it).
