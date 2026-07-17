# Godot-Tests — headless CI testbed

A deliberately minimal Godot 4 (C#/mono) project used **only by CI** to verify that
the `godot_mcp` addon loads cleanly across a matrix of Godot engine versions
(**4.3 / 4.4 / 4.5**, mono). It is the Godot analog of Unity-MCP's
`Unity-Tests/<version>/` projects, and mirrors the infra repo's
`Godot-Test-Project/` testbed — but self-contained inside this repo so CI needs no
external checkout or junction.

## What it contains

- `project.godot` — enables `res://addons/godot_mcp/plugin.cfg` in `[editor_plugins]`.
- `Godot-Tests.csproj` — `Godot.NET.Sdk` + the **same frozen NuGet pins** as
  `../Godot-MCP.csproj` (`com.IvanMurzak.ReflectorNet` 5.3.2,
  `com.IvanMurzak.McpPlugin` 7.1.1), so the addon's C# compiles into the project
  assembly. Excluded from `../Godot-MCP.sln` so the `dotnet build (.NET 8)` gate is
  unaffected.
- No game content, no scenes, no committed addon copy.

## How CI uses it

`.github/workflows/test_godot_plugin.yml` (reusable, input `godotVersion`):

1. installs the requested Godot **mono** binary + .NET 8,
2. **copies** `../addons/godot_mcp` into `Godot-Tests/addons/godot_mcp` (a dev
   junction does not survive checkout in CI; the copy is `.gitignore`d here),
3. imports the project once (`<godot> --headless --path Godot-Tests --import --quit`)
   to materialize the `.godot/` layout on a cold checkout,
4. builds the project C# with **`dotnet build Godot-Tests/Godot-Tests.csproj`** —
   the addon `*.cs` globs into the project assembly, so a clean build proves the
   addon compiles against the frozen pins. (`godot --build-solutions` is a silent
   no-op on a cold headless checkout, so `dotnet build` is used directly.)
5. boots the editor headless (`<godot> --headless --path Godot-Tests --editor --quit`)
   and **fails the job** unless `[Godot-MCP] plugin loaded` appears with no
   `FileNotFoundException` / addon-load exception.

`.github/workflows/test_pull_request.yml` fans this out once per matrix version
alongside the `dotnet build (.NET 8)` + xUnit job and the `godot-mcp-cli` node
tests. The live matrix only runs in the PR's own GitHub CI run (the runners
download Godot).

## Runtime integration harness (issue #186)

Beyond the addon-LOAD smoke above, `Harness/` holds a RUNTIME INTEGRATION harness
that boots a **headless GAME** (not the editor) and exercises the whole end-to-end
path the `#if TOOLS`-excluded xUnit suite cannot reach — live SignalR connect +
handshake + tool dispatch, and in-game runtime-error capture:

- `Harness/Main.tscn` — `project.godot`'s `run/main_scene`; its root node carries
  `RuntimeHarness.cs`.
- `Harness/RuntimeHarness.cs` — gated by `GODOT_MCP_HARNESS=1` (a normal load/run is
  untouched). On boot it `GodotMcpRuntime.Initialize(b => b.WithRuntimeErrorCapture())
  .Build()` + `.Connect()`s to a LOCAL `gamedev-mcp-server`, raises a GDScript runtime
  fault + `push_error`/`push_warning` + a C# unobserved-Task exception, reads them back
  via `RuntimeErrorCollector.Current.QuerySince(...)`, writes a structured JSON result
  (path = `GODOT_MCP_HARNESS_RESULT`), and quits with a pass/fail exit code.
- `Harness/Faulty.gd` — a small call chain whose innermost frame calls a nonexistent
  method, producing a genuine engine GDScript runtime error with a deep multi-frame
  backtrace (issue #163) on Godot 4.5+.

`.github/workflows/test_godot_runtime_harness.yml` (reusable, input `godotVersion`)
downloads the released `gamedev-mcp-server` (`v<ServerVersion>`), runs it
`authorization=none` on a fixed port, builds the testbed C# with the SDK matching the
matrix Godot version (so the engine 4.5+ logger compiles in on 4.5 and is stubbed on
4.3/4.4), boots the headless harness game, POSTs `ping` to the server while the game
holds the connection (proving connect + handshake + dispatch), and asserts the result
via `scripts/assert_runtime_harness.py`. The **4.5.1** leg asserts the engine GDScript
backtrace + `push_error`/`push_warning`; the **4.3/4.4** legs assert graceful stub
degradation (no engine logger, frames null) while the C# channel still captures.
`test_pull_request.yml` and `release.yml` both fan it out across 4.3 / 4.4 / 4.5.

## Local run

Mirror the CI smoke against the infra testbed instead (it junctions the live
addon) — see `../CLAUDE.md` "Testbed runbook". To run this exact project locally,
copy the addon in first:

```bash
cp -r ../addons/godot_mcp addons/godot_mcp
"$GODOT" --headless --path . --import --quit          # generate .godot/ (cold start)
dotnet build Godot-Tests.csproj --configuration Debug # -> .godot/mono/temp/bin/Debug/Godot-MCP-Tests.dll
"$GODOT" --headless --path . --editor --quit          # expect: [Godot-MCP] plugin loaded
```
