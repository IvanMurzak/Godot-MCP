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
  `../Godot-MCP.csproj` (`com.IvanMurzak.ReflectorNet` 5.3.1,
  `com.IvanMurzak.McpPlugin` 6.9.1), so the addon's C# compiles into the project
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
