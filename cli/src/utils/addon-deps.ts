// The NuGet PackageReferences a consumer's Godot C# project MUST declare so the
// `addons/godot_mcp/` sources compile into the project assembly (Godot globs every
// `*.cs` under the project into one assembly — see the addon README
// "Consumer-install story"). `install-plugin` writes these into the generated
// consumer `.csproj` so a from-scratch terminal install actually builds.
//
// SINGLE SOURCE OF TRUTH: these values mirror the addon's own reused pins in
// `Godot-MCP.csproj` (`com.IvanMurzak.ReflectorNet`, `com.IvanMurzak.McpPlugin`).
// They are NOT a bump of those pins — they are the SAME values, copied so the
// scaffold installs exactly what the addon was built against. A parity test
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
 * The exact NuGet `PackageReference`s the consumer's project `.csproj` needs,
 * single-sourced from the addon's `Godot-MCP.csproj` pins. Order matches the
 * addon csproj (ReflectorNet first, then McpPlugin).
 */
export const ADDON_PACKAGE_REFERENCES: readonly AddonPackageReference[] = [
  { id: 'com.IvanMurzak.ReflectorNet', version: '5.3.2' },
  { id: 'com.IvanMurzak.McpPlugin', version: '7.2.0' },
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
