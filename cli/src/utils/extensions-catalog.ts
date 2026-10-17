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
 * The third-party Godot addon a CLASS-B extension wraps — mirrors the JSON
 * `addonRequired` block. Class-A extensions (which wrap a BUILT-IN Godot feature)
 * OMIT this entirely (`addonRequired` is absent / `undefined`). When present it is
 * pure presentation metadata so the dock/app/CLI can surface "requires the <name>
 * addon" + a link; it does NOT affect install logic (the package is still installed
 * by `packageId` alone — the addon is the consumer's own runtime responsibility).
 * `name` is the anchor; `assetLibId` (the Godot AssetLib id, stored as a string),
 * `repo`, and `license` are optional.
 */
export interface ExtensionAddonRequirement {
  readonly name: string;
  readonly assetLibId: string | null;
  readonly repo: string | null;
  readonly license: string | null;
}

/**
 * One installable extension — the CLI analog of the C# `GodotExtensionDescriptor`.
 * `packageId` is the INSTALL IDENTITY (the `<PackageReference Include="...">`).
 * `version` is `null` for a floating (unpinned) reference.
 * `addonRequired` is present ONLY for CLASS-B (addon-dependent) extensions; Class-A
 * entries omit it.
 */
export interface ExtensionDescriptor {
  readonly name: string;
  readonly description: string;
  readonly packageId: string;
  readonly version: string | null;
  readonly gitUrl: string | null;
  readonly tools: readonly ExtensionTool[];
  readonly addonRequired?: ExtensionAddonRequirement | null;
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
      'RQL module for Godot GPU particles (2D & 3D): validated emitter settings and Restart() for the core rql-query / rql-mutate tools, plus an agent guide.',
    packageId: 'com.IvanMurzak.Godot.MCP.Particles',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Particles/README.md',
    tools: [],
  },
  {
    name: 'Tilemap Tools',
    description:
      'RQL module for Godot TileMapLayer (4.3+): used-cell rows and SetCell / EraseCell / Clear for the core rql-query / rql-mutate tools, plus an agent guide.',
    packageId: 'com.IvanMurzak.Godot.MCP.Tilemap',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Tilemap/README.md',
    tools: [],
  },
  {
    name: 'Navigation Tools',
    description:
      'RQL module for Godot navigation (2D & 3D): validated agent, region and link settings, region Bake() and polygon outlines for the core rql-query / rql-mutate tools, plus an agent guide.',
    packageId: 'com.IvanMurzak.Godot.MCP.Navigation',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Navigation/README.md',
    tools: [],
  },
  {
    name: 'Animation Tools',
    description:
      'RQL module for Godot AnimationPlayer: libraries, animations, tracks and keys for the core rql-query / rql-mutate tools, plus an agent guide.',
    packageId: 'com.IvanMurzak.Godot.MCP.Animation',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Animation/README.md',
    tools: [],
  },
  {
    name: 'CSG Tools',
    description:
      'RQL module for Godot CSG (constructive solid geometry): validated box / sphere / cylinder / torus dimensions and rootShape for the core rql-query / rql-mutate tools, plus an agent guide.',
    packageId: 'com.IvanMurzak.Godot.MCP.CSG',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-CSG/README.md',
    tools: [],
  },
  {
    name: 'GridMap Tools',
    description:
      'RQL module for Godot GridMap (3D tile maps): validated settings, cell rows and SetCell / ClearCell / Clear for the core rql-query / rql-mutate tools, plus an agent guide.',
    packageId: 'com.IvanMurzak.Godot.MCP.GridMap',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-GridMap/README.md',
    tools: [],
  },
  {
    name: 'PhantomCamera Tools',
    description:
      'RQL module for the Godot Phantom Camera addon: a phantomcamera source that lists virtual cameras and hosts with their modes by name, and validated SetFollow / SetLookAt calls.',
    packageId: 'com.IvanMurzak.Godot.MCP.PhantomCamera',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-PhantomCamera/README.md',
    tools: [],
    addonRequired: {
      name: 'Phantom Camera',
      assetLibId: '1822',
      repo: 'ramokz/phantom-camera',
      license: 'MIT',
    },
  },
  {
    name: 'Beehave Tools',
    description:
      'RQL module for the Godot Beehave addon (behaviour trees): a beehave source that lists trees and their nodes with their Beehave classes; build trees with the core AddChild.',
    packageId: 'com.IvanMurzak.Godot.MCP.Beehave',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Beehave/README.md',
    tools: [],
    addonRequired: {
      name: 'Beehave',
      assetLibId: '1349',
      repo: 'bitbrain/beehave',
      license: 'MIT',
    },
  },
  {
    name: 'Dialogic Tools',
    description:
      'RQL module for the Godot Dialogic addon: a dialogic source that lists timelines and characters, creates them and appends timeline text.',
    packageId: 'com.IvanMurzak.Godot.MCP.Dialogic',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Dialogic/README.md',
    tools: [],
    addonRequired: {
      name: 'Dialogic',
      assetLibId: null,
      repo: 'dialogic-godot/dialogic',
      license: 'MIT',
    },
  },
  {
    name: 'Terrain3D Tools',
    description:
      'RQL module for the Terrain3D addon (TokisanGames heightmap terrain): terrain3d source with validated ChangeRegionSize, addon check and material creation for the core rql-query / rql-mutate tools, plus an agent guide.',
    packageId: 'com.IvanMurzak.Godot.MCP.Terrain3D',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Terrain3D/README.md',
    tools: [],
    addonRequired: {
      name: 'Terrain3D',
      assetLibId: '3892',
      repo: 'TokisanGames/Terrain3D',
      license: 'MIT',
    },
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
