// The NuGet PackageReferences a consumer's Godot C# project MUST declare so the
// `addons/godot_mcp/` sources compile into the project assembly (Godot globs every
// `*.cs` under the project into one assembly — see the addon README
// "Consumer-install story").
//
// FALLBACK ONLY: `install-plugin` takes the versions from the addon it installs
// (`addon-nuget-deps.ts`: the addon's generated `nuget-deps.json`, or the
// checkout's `Godot-MCP.csproj` for `--source`). These constants are used only for
// addon zips that predate that manifest. They mirror the addon's own reused pins in
// `Godot-MCP.csproj` (`com.IvanMurzak.ReflectorNet`, `com.IvanMurzak.McpPlugin`) at
// this CLI's version — the SAME values, never a bump of those pins. `com.IvanMurzak.Rql` (the RQL data layer of
// the editor tools, PolyForm Shield 1.0.0) is a direct dependency of the addon sources since 1.0; Code-Exec
// (`com.IvanMurzak.CodeExec` + the source-only `.Roslyn` adapter, compiled against the pinned
// `Microsoft.CodeAnalysis.CSharp`) runs `script-execute`. Those four are `editorOnly`: only `#if TOOLS` code
// uses them, so exported games ship neither RQL nor Roslyn. The three Code-Exec ones are also `inGameOptIn`: a game
// that opts in to in-game `script-execute` (`GodotMcpCodeExecInGame=true`) keeps them in its exports. A parity test
// (`cli/tests/addon-deps-parity.test.ts`) reads `Godot-MCP.csproj` and FAILS the
// build if these constants ever drift from the addon's pins, so the scaffold can
// never silently install a mismatched version.
//
// No top-level side effects; pure data + pure string transforms only.

/** A single NuGet `<PackageReference>` the consumer csproj must declare. */
export interface AddonPackageReference {
  /** The NuGet package id (the `Include=` attribute). */
  id: string;
  /** The pinned version (the `Version=` attribute). */
  version: string;
  /**
   * Only editor (`#if TOOLS`) code uses the package: it is written with `EDITOR_ONLY_CONDITION`, so Godot's
   * export builds leave it out (an exported game ships no RQL / Roslyn).
   */
  editorOnly?: boolean;
  /**
   * With `editorOnly`: a game that opts in to in-game `script-execute` (MSBuild property `GodotMcpCodeExecInGame=true`)
   * keeps the package in its export builds, so it is written with `CODE_EXEC_IN_GAME_CONDITION` instead. A CLI that
   * predates this flag reads such a manifest entry as plain editor-only (safe: no in-game code execution).
   */
  inGameOptIn?: boolean;
}

/**
 * The MSBuild condition of an editor-only `<PackageReference>`: every configuration except Godot's two export
 * builds (`ExportDebug` / `ExportRelease`, the ones without `TOOLS`). The Godot SDK applies the same rule to
 * GodotSharpEditor. Godot-MCP.csproj marks its editor-only packages with this exact attribute.
 */
export const EDITOR_ONLY_CONDITION = "'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'";

/** The MSBuild property a game sets to `true` to keep Code-Exec and Roslyn in its exports (in-game `script-execute`). */
export const CODE_EXEC_IN_GAME_PROPERTY = 'GodotMcpCodeExecInGame';

/**
 * The MSBuild condition of an `inGameOptIn` package: the editor-only condition, or the game opted in to in-game
 * `script-execute`. Godot-MCP.csproj marks its Code-Exec packages with this exact attribute.
 */
export const CODE_EXEC_IN_GAME_CONDITION = `'$(${CODE_EXEC_IN_GAME_PROPERTY})' == 'true' or (${EDITOR_ONLY_CONDITION})`;

/** The canonical Condition `install-plugin` writes for a reference (undefined: none). */
export function canonicalCondition(ref: AddonPackageReference): string | undefined {
  if (!ref.editorOnly) return undefined;
  return ref.inGameOptIn ? CODE_EXEC_IN_GAME_CONDITION : EDITOR_ONLY_CONDITION;
}

/**
 * A single `<EmbeddedResource>` the consumer csproj must declare so an asset the
 * addon reads at editor runtime via `GetManifestResourceStream` is embedded into
 * the consumer's compiled project assembly (Godot globs every `*.cs` into one
 * assembly, but does NOT carry the addon's own `Godot-MCP.csproj` `<EmbeddedResource>`
 * — the addon ships as source, not a NuGet package).
 */
export interface AddonEmbeddedResource {
  /** The file to embed, relative to the consumer project root (the `Include=` attribute). */
  include: string;
  /** The manifest-resource name the addon resolves it by (the `LogicalName=` attribute). */
  logicalName: string;
}

/**
 * The NuGet `PackageReference`s the addon of THIS CLI's version needs (fallback when
 * the installed addon carries no `nuget-deps.json`), mirroring `Godot-MCP.csproj`.
 * Order matches the addon csproj (ReflectorNet, McpPlugin, Rql, then Code-Exec and its Roslyn).
 */
export const ADDON_PACKAGE_REFERENCES: readonly AddonPackageReference[] = [
  { id: 'com.IvanMurzak.ReflectorNet', version: '5.5.0' },
  { id: 'com.IvanMurzak.McpPlugin', version: '8.13.1' },
  { id: 'com.IvanMurzak.Rql', version: '0.3.0-dev.7dd3b29', editorOnly: true },
  { id: 'com.IvanMurzak.CodeExec', version: '0.3.1', editorOnly: true, inGameOptIn: true },
  { id: 'com.IvanMurzak.CodeExec.Roslyn', version: '0.3.1', editorOnly: true, inGameOptIn: true },
  { id: 'Microsoft.CodeAnalysis.CSharp', version: '4.12.0', editorOnly: true, inGameOptIn: true },
] as const;

/**
 * The exact `<EmbeddedResource>`s the consumer's project `.csproj` needs, single-sourced
 * from the addon's `Godot-MCP.csproj`. The extension catalog is read at editor runtime by
 * the pure-managed `GodotExtensionRegistry` via `GetManifestResourceStream` (no `res://` /
 * filesystem fallback), so without this embed a consumer that installs via `install-plugin`
 * gets an EMPTY Extensions panel. A parity test (`cli/tests/addon-deps-parity.test.ts`)
 * reads `Godot-MCP.csproj` and FAILS the build if these constants ever drift from the addon's
 * own `<EmbeddedResource>` (Include path + LogicalName), mirroring the NuGet-pin tripwire.
 */
export const ADDON_EMBEDDED_RESOURCES: readonly AddonEmbeddedResource[] = [
  {
    include: 'addons/godot_mcp/extensions.catalog.json',
    logicalName: 'Godot-MCP.extensions.catalog.json',
  },
] as const;

/** The resource path of the addon manifest a consumer project loads the plugin from. */
export const ADDON_PLUGIN_CFG_RESOURCE = 'res://addons/godot_mcp/plugin.cfg';
