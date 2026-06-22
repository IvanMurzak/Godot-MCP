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
 * The exact NuGet `PackageReference`s the consumer's project `.csproj` needs,
 * single-sourced from the addon's `Godot-MCP.csproj` pins. Order matches the
 * addon csproj (ReflectorNet first, then McpPlugin).
 */
export const ADDON_PACKAGE_REFERENCES: readonly AddonPackageReference[] = [
  { id: 'com.IvanMurzak.ReflectorNet', version: '5.3.1' },
  { id: 'com.IvanMurzak.McpPlugin', version: '6.10.0' },
] as const;

/** The resource path of the addon manifest a consumer project loads the plugin from. */
export const ADDON_PLUGIN_CFG_RESOURCE = 'res://addons/godot_mcp/plugin.cfg';
