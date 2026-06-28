// The CLI's typed mirror of the SHARED extension catalog — the single source of
// truth `addons/godot_mcp/extensions.catalog.json` (see that file's sibling
// `extensions.catalog.md`). It is the CLI half of the "one catalog consumed by the
// dock, the CLI, and the app" contract: the dock parses the JSON via an embedded
// resource (C# `GodotExtensionCatalog`); the CLI mirrors it here so the published
// npm package stays self-contained (no runtime `../addons` dependency).
//
// SINGLE SOURCE OF TRUTH: this constant MUST stay byte-equivalent to the JSON. The
// parity test `cli/tests/extensions-catalog-parity.test.ts` reads the addon JSON and
// FAILS the build if this mirror drifts — exactly the discipline `addon-deps.ts` /
// `addon-deps-parity.test.ts` use for the NuGet pins. Adding an extension =
// appending an entry to BOTH the JSON and this array (the test enforces it).
//
// No top-level side effects; pure data + pure lookups only.

/** One tool a catalog extension contributes — mirrors the JSON `tools[]` entry. */
export interface ExtensionTool {
  readonly name: string;
  readonly description: string;
}

/**
 * One installable extension — the CLI analog of the C# `GodotExtensionDescriptor`.
 * `packageId` is the INSTALL IDENTITY (the `<PackageReference Include="...">`).
 * `version` is `null` for a floating (unpinned) reference.
 */
export interface ExtensionDescriptor {
  readonly name: string;
  readonly description: string;
  readonly packageId: string;
  readonly version: string | null;
  readonly gitUrl: string | null;
  readonly tools: readonly ExtensionTool[];
}

/**
 * The extension catalog, single-sourced from `addons/godot_mcp/extensions.catalog.json`.
 * Ships EMPTY until the first Godot-MCP extension package is published on nuget.org —
 * `install-extension <id>` then reports "unknown extension" for every id, which is the
 * correct behavior (there is nothing to install yet). The parity test keeps this in
 * lockstep with the JSON so adding an entry there forces an update here.
 */
export const EXTENSIONS_CATALOG: readonly ExtensionDescriptor[] = [
  {
    name: 'Particles Tools',
    description:
      'AI MCP tools for Godot GpuParticles (2D & 3D): create, configure, start/stop, and inspect emitters.',
    packageId: 'com.IvanMurzak.Godot.MCP.Particles',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-AI-Particles',
    tools: [
      { name: 'particles-defaults', description: 'Return the recommended starter config for a 2D/3D emitter.' },
      { name: 'particles-create', description: 'Create a GpuParticles2D/GpuParticles3D node in the edited scene.' },
      { name: 'particles-configure', description: "Update an emitter's scalar properties (clamped to valid ranges)." },
      { name: 'particles-set-emitting', description: 'Start or stop emission, optionally restarting first.' },
      { name: 'particles-get', description: "Read an emitter's scalar config (read-only)." },
    ],
  },
  {
    name: 'Tilemap Tools',
    description:
      'AI MCP tools for Godot TileMapLayer (4.3+): create layers, assign tilesets, set/erase cells, and inspect used cells.',
    packageId: 'com.IvanMurzak.Godot.MCP.Tilemap',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-AI-Tilemap',
    tools: [
      { name: 'tilemap-create', description: 'Create a TileMapLayer node in the currently edited Godot scene.' },
      { name: 'tilemap-set-tileset', description: 'Assign a TileSet resource to an existing TileMapLayer.' },
      { name: 'tilemap-set-cell', description: 'Set a single cell on a TileMapLayer (by map + atlas coords).' },
      { name: 'tilemap-erase-cell', description: 'Erase a single cell on a TileMapLayer (set it back to empty).' },
      { name: 'tilemap-get-used-cells', description: 'List the used (non-empty) cells of a TileMapLayer (read-only).' },
      { name: 'tilemap-clear', description: 'Clear all cells on a TileMapLayer (the assigned TileSet is kept).' },
    ],
  },
] as const;

/** True when a descriptor carries a concrete version pin (drives the up-to-date / update decision). */
export function hasVersion(descriptor: ExtensionDescriptor): boolean {
  return descriptor.version !== null && descriptor.version.trim() !== '';
}

/**
 * Resolve a user-supplied `<id>` to a catalog descriptor. Matches by `packageId`
 * first (ordinal-ignore-case, like NuGet's case-insensitive ids — the install
 * identity), then falls back to an exact case-insensitive `name` match for
 * convenience. Returns `null` when absent or `id` is empty.
 */
export function findExtension(
  id: string | undefined | null,
  catalog: readonly ExtensionDescriptor[] = EXTENSIONS_CATALOG,
): ExtensionDescriptor | null {
  if (id === undefined || id === null) return null;
  const needle = id.trim();
  if (needle === '') return null;

  const byPackageId = catalog.find(
    (d) => d.packageId.toLowerCase() === needle.toLowerCase(),
  );
  if (byPackageId) return byPackageId;

  return catalog.find((d) => d.name.toLowerCase() === needle.toLowerCase()) ?? null;
}
