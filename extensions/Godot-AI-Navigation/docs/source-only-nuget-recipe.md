# The source-only NuGet recipe (the centerpiece)

A Godot-MCP extension is distributed as a **source-only** NuGet package: it ships C# **source**, not a
compiled DLL. The source compiles **inside the consumer's Godot project**, against the **consumer's own
GodotSharp**. This is what lets one package work across Godot versions with no bundled engine and no
version lock.

This recipe is **verified** end-to-end with `dotnet` alone (no Godot binary): build the package → unit
test → `dotnet pack` → restore+build a consumer `Godot.NET.Sdk` project that references it → the injected
source compiles clean against the consumer's GodotSharp.

## Why not a normal (DLL) package?

The RQL module calls Godot's C# API (`Node`, `Resource`, `EditorInterface`, …) which lives in
**GodotSharp**. A pre-compiled DLL is built against **one** GodotSharp version and is therefore locked to
it — a consumer on a different Godot version gets binary-incompatibility. Shipping **source** sidesteps
this: the consumer is itself a `Godot.NET.Sdk` project, so its build supplies GodotSharp at compile time
and our source compiles against *that* exact version.

## The three levers (in the package csproj)

```xml
<Project Sdk="Godot.NET.Sdk/4.3.0">   <!-- min-version floor; consumer uses its own -->
  <PropertyGroup>
    <IncludeBuildOutput>false</IncludeBuildOutput>            <!-- (1) ship no DLL -->
    <DisableImplicitGodotSharpReferences>true</DisableImplicitGodotSharpReferences>  <!-- (3) -->
    <NoWarn>$(NoWarn);NU5128;NU1504</NoWarn>
    <EnableDefaultNoneItems>false</EnableDefaultNoneItems>
  </PropertyGroup>

  <!-- Only the MCP / reflection / query stack is a package dependency (the consumer already has it); pins = the core's. -->
  <ItemGroup>
    <PackageReference Include="com.IvanMurzak.ReflectorNet" Version="5.5.0" />
    <PackageReference Include="com.IvanMurzak.McpPlugin"   Version="8.13.1" />
    <PackageReference Include="com.IvanMurzak.Rql"         Version="0.3.0-dev.c5fed20" />
  </ItemGroup>

  <!-- (3) GodotSharp is needed to COMPILE locally but must NOT be a package dependency. -->
  <ItemGroup>
    <PackageReference Include="GodotSharp"            Version="4.3.0" PrivateAssets="all" />
    <PackageReference Include="GodotSharpEditor"      Version="4.3.0" PrivateAssets="all" Condition="'$(Configuration)' == 'Debug'" />
    <PackageReference Include="Godot.SourceGenerators" Version="4.3.0" PrivateAssets="all" />
  </ItemGroup>

  <!-- (2) Pack the .cs as plain files under src/ + the injection props under build/. -->
  <ItemGroup>
    <None Include="Editor/**/*.cs"  Pack="true" PackagePath="src/Editor/" />
    <None Include="build/<PackageId>.props" Pack="true" PackagePath="build/" />
    <None Include="README.md" Pack="true" PackagePath="" />
  </ItemGroup>
</Project>
```

**Lever 1 — no DLL.** `IncludeBuildOutput=false` keeps the compiled assembly out of the package; the
package's payload is the `.cs` files + the injection props.

**Lever 2 — inject source as `<Compile>` in the consumer.** The `.cs` are packed as plain files under
`src/`. The package ships `build/<PackageId>.props`, which **NuGet auto-imports** into any consuming
project (this is why the file MUST be named exactly `<PackageId>.props`). The props adds the packed `.cs`
as `<Compile>` items:

```xml
<Project>
  <ItemGroup>
    <Compile Include="$(MSBuildThisFileDirectory)..\src\**\*.cs"
             Exclude="@(Compile)"
             Link="<PackageId>\%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>
</Project>
```

`$(MSBuildThisFileDirectory)` resolves into the NuGet cache (`…/<package>/<version>/build/`), so
`..\src\**\*.cs` is the packed source. The consumer's own default glob never reaches the cache, so
`Exclude="@(Compile)"` is just belt-and-suspenders against double-inclusion.

**Lever 3 — declare NO GodotSharp dependency.** The authoring project uses `Godot.NET.Sdk` so the
`Godot` namespace resolves and `#if TOOLS` compiles locally — but GodotSharp must NOT leak into the
package's dependency list (the consumer provides its own). Two steps are required because a plain
`<PackageReference Update="GodotSharp" PrivateAssets="all">` in the project body **does not work** (the
SDK adds GodotSharp *after* the body is evaluated, so there is nothing to update yet):

1. `DisableImplicitGodotSharpReferences=true` drops the SDK's implicit `GodotSharp` + `GodotSharpEditor`.
2. Re-add `GodotSharp`/`GodotSharpEditor` explicitly with `PrivateAssets="all"` (kept for the local
   compile, excluded from the package deps). `Godot.SourceGenerators` has **no** SDK opt-out, so it is
   re-stated as an explicit `Include … PrivateAssets="all"` (this duplicates the implicit add → the
   harmless `NU1504`, suppressed via `NoWarn`).

Verify it worked by inspecting the generated `.nuspec` — its `<dependencies>` must list **only**
`com.IvanMurzak.McpPlugin`, `com.IvanMurzak.ReflectorNet` and `com.IvanMurzak.Rql`, with **no** `GodotSharp` /
`Godot.SourceGenerators`. CI asserts exactly this (`scripts/extensions.py` `verify_package`).

## Why `build/*.props` injection, not `contentFiles`?

NuGet's `contentFiles` with `buildAction="Compile"` is the other documented way to inject source. We use
the **`build/*.props`** mechanism instead because:

- **SDK-agnostic and deterministic.** It is plain MSBuild `<Compile Include>` — it does not depend on
  NuGet's `contentFiles`-Compile auto-injection behaving identically under `Godot.NET.Sdk` (which
  customizes globbing); the props is auto-imported the same way for every SDK.
- **One obvious file to read and override.** The whole injection is one short, visible props file rather
  than `contentFiles` metadata split between the nuspec and NuGet's restore-generated targets.
- **No double-compile risk.** `EnableDefaultNoneItems=false` + the explicit `<None Pack>` list controls
  exactly what ships; the consumer's project-dir glob never overlaps the cache.

`contentFiles` remains a valid alternative; if you switch, drop the props and pack the `.cs` under
`contentFiles/cs/any/…` with `buildAction="Compile"`, and re-verify the consumer build + the `.nuspec`
dependency list.

## Editor-only API: `#if TOOLS`

The RQL module, its adapters and sources sit behind `#if TOOLS`: RQL is the editor's data layer, so they
compile in the editor and are excluded from shipped games — exactly as in the core addon (Godot defines
`TOOLS` for the editor/Debug build and **not** for an exported game build). The agent guide and other
pure-managed helpers (validation rules, label maps) stay outside the guard so they are CI-unit-testable.
The core runs every RQL query on the editor main thread, so module code may touch `Node` / `Resource` /
`EditorInterface` directly.

## The consumer story

1. The consumer is a Godot **C#** project with the core `godot_mcp` addon enabled (which already
   references `com.IvanMurzak.McpPlugin`, `com.IvanMurzak.ReflectorNet` and `com.IvanMurzak.Rql`).
2. Installing the extension adds one line to the consumer's `.csproj`:
   `<PackageReference Include="com.IvanMurzak.Godot.MCP.<Feature>" Version="x.y.z" />`
   (done by the **Extensions dock**, `godot-cli install-extension`, or by hand).
3. Rebuild. NuGet imports `build/<PackageId>.props` → the extension's `.cs` compile into the consumer's
   single project assembly against the consumer's GodotSharp.
4. The core's RQL engine discovers the module (`IRqlModule`) in the project assembly — **no registry edit**.
   Its adapters, sources and guide appear in `rql-query` / `rql-mutate` / `rql-schema` and the `godot-rql`
   skill, inside the user's real Godot editor.

## Publishing & the shared catalog

`release.yml` publishes the source-only package to NuGet (version-gated) and cuts an atomic GitHub
Release. To make the extension appear in the in-editor **Extensions** dock and `godot-cli
install-extension`, append one entry to the shared catalog `addons/godot_mcp/extensions.catalog.json` in
**Godot-MCP** (schema in `extensions.catalog.md`): `name`, `description`, `packageId`
(`com.IvanMurzak.Godot.MCP.<Feature>`), optional `version`, `gitUrl`, and `tools` (empty for an RQL module). That
single source of truth is consumed by the dock, the CLI, and the app.
