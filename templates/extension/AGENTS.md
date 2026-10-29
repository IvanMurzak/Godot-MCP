# AGENTS.md — Godot-AI-Tools-Template

**Template** for Godot-MCP extension packages (the Godot analog of `Unity-AI-Tools-Template`). An extension
is an **RQL module**: it teaches the core `rql-query` / `rql-mutate` / `rql-schema` tools about a Godot feature
(adapters, sources, methods, a guide) instead of adding MCP tools. `scripts/new_extension.py` (monorepo) or
`commands/init.py` turn the placeholder boilerplate into a real extension, so every change here
propagates to future extensions — keep placeholder tokens consistent.

## Placeholders (replaced by `init`)

| Token | Example value | Used in |
| --- | --- | --- |
| `YOUR_FEATURE` | `Particles` | namespaces, `<Feature>Module` / `Guide` / `Api`, folder/file names, the package id suffix |
| `YOUR_SOURCE` | `particles` | the RQL source name of the sample module (`--source-name`) |
| `YOUR_DISPLAY_NAME` | `Particles Tools` | package `Title`, the guide topic, docs |
| `YOUR_DESCRIPTION` | `RQL module for Godot Particles.` | package `Description` |
| `YOUR_GITHUB_USERNAME_REPOSITORY` | `IvanMurzak/Godot-AI-Particles` | repo URLs |

The package id is always `com.IvanMurzak.Godot.MCP.<Feature>`, written literally as
`com.IvanMurzak.Godot.MCP.YOUR_FEATURE`. `init` also renames `build/<id>.props` and activates CI
(`*.yml-sample` → `*.yml`).

## Layout

- `src/Godot-AI-YOUR_FEATURE/` — the source-only package (`Godot.NET.Sdk`).
  - `Editor/Rql/YOUR_FEATUREModule.cs` — the `IRqlModule` (behind `#if TOOLS`): adapters, sources, guide.
  - `Editor/Rql/YOUR_FEATUREApi.cs` — the sample `YOUR_SOURCE` source (a view object, `#if TOOLS`).
  - `Editor/Rql/YOUR_FEATUREGuide.cs` — the agent guide; pure strings, NOT behind `#if TOOLS` (unit-tested).
  - `build/com.IvanMurzak.Godot.MCP.YOUR_FEATURE.props` — source-injection props (must stay `<PackageId>.props`).
- `tests/Godot-AI-YOUR_FEATURE.Tests/` — xUnit over the module sources (`TOOLS` defined, GodotSharp metadata only); the
  guide checks come from `extensions/Shared/Tests/GuideExampleTests.cs`, linked by the test csproj.
- `tests/e2e/e2e-tools.json` — the live calls that run every guide example (driver: `scripts/extension_e2e.py`).
- `testbed/` — consumer `Godot.NET.Sdk` project restoring the local-packed package (injection proof).
- `commands/` — `init.py`, `bump-version.py` (`--get` prints the version), `update-core.py`. A monorepo module
  gets no copy (`scripts/new_extension.py` skips it): use `scripts/extension_release.py` and `scripts/update_core.py`.
- `.github/workflows/*.yml-sample` — CI, inert until `init`. Never commit live `.github/workflows/*.yml` here.
- `docs/` — `source-only-nuget-recipe.md` (the packaging recipe + consumer story); `ci.md`.

## Build / test (no Godot binary)

```bash
dotnet build src/Godot-AI-YOUR_FEATURE/Godot-AI-YOUR_FEATURE.csproj   # the module compiles (Debug: #if TOOLS too)
dotnet test  tests/Godot-AI-YOUR_FEATURE.Tests/Godot-AI-YOUR_FEATURE.Tests.csproj
dotnet pack  src/Godot-AI-YOUR_FEATURE/Godot-AI-YOUR_FEATURE.csproj -p:Version=0.0.0-ci -o local-nuget
dotnet build testbed/YOUR_FEATURE-Testbed.csproj                      # consumes the local package (injection proof)
```

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.<Feature>`. One adapter per file, named `Godot_<Type>_Adapter`.
- The module uses only Godot, `com.IvanMurzak.Rql` and the BCL — never core-addon types: the authoring build
  compiles without the core, and the core discovers the module by `IRqlModule` in the consumer assembly.
- Undo and saving come from the core: a `set` or a call on a node / resource records its stored Godot
  properties, so put a method on the object it changes. Validate values in adapters (`validate:`) instead of
  clamping them: a rejected value fails the whole mutation.
- Every guide example runs in `tests/e2e/e2e-tools.json` (xUnit-asserted, in order); keep them runnable.
- Core pins (`McpPlugin`, `ReflectorNet`, `Rql`) equal the core addon's (`core_pin_drift` fails CI otherwise); bump with
  `commands/update-core.py` (`scripts/update_core.py` in the monorepo). GodotSharp must never become a package dep (see the recipe; CI asserts it).
