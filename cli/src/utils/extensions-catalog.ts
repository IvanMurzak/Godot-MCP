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
  {
    name: 'Navigation Tools',
    description:
      'AI MCP tools for Godot navigation (2D & 3D): create regions, agents, and links, set region meshes, configure agents, and inspect navigation nodes.',
    packageId: 'com.IvanMurzak.Godot.MCP.Navigation',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-AI-Navigation',
    tools: [
      { name: 'navigation-defaults', description: 'Return the recommended starter config (radius, distances, max speed) for a 2D/3D NavigationAgent.' },
      { name: 'navigation-region-create', description: 'Create a NavigationRegion2D/NavigationRegion3D node (a navigable area) in the edited scene.' },
      { name: 'navigation-region-set-mesh', description: "Assign a region's navigation resource (NavigationPolygon in 2D, NavigationMesh in 3D)." },
      { name: 'navigation-agent-create', description: 'Create a NavigationAgent2D/NavigationAgent3D node (pathfinding + avoidance) in the edited scene.' },
      { name: 'navigation-agent-configure', description: "Update a NavigationAgent's scalar properties (clamped to valid ranges)." },
      { name: 'navigation-link-create', description: 'Create a NavigationLink2D/NavigationLink3D node (an off-mesh connection between two points).' },
      { name: 'navigation-get', description: "Read a navigation node's scalar config (read-only)." },
    ],
  },
  {
    name: 'Animation Tools',
    description:
      'AI MCP tools for Godot AnimationPlayer: create players, libraries, and animations, add tracks, insert keyframes, and inspect them.',
    packageId: 'com.IvanMurzak.Godot.MCP.Animation',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-AI-Animation',
    tools: [
      { name: 'animation-defaults', description: 'Return the recommended starter config (length, loop mode) for a new animation.' },
      { name: 'animation-player-create', description: 'Create an AnimationPlayer node in the currently edited Godot scene.' },
      { name: 'animation-library-add', description: 'Add a new empty AnimationLibrary to an existing AnimationPlayer.' },
      { name: 'animation-create', description: "Create an Animation in an AnimationPlayer's library (auto-created when missing)." },
      { name: 'animation-add-track', description: 'Add a value or 3D transform track to an existing Animation.' },
      { name: 'animation-insert-key', description: 'Insert a keyframe on an animation track.' },
      { name: 'animation-get', description: "Read an AnimationPlayer's libraries, animations, and a clip's tracks (read-only)." },
    ],
  },
  {
    name: 'CSG Tools',
    description:
      'AI MCP tools for Godot CSG (Constructive Solid Geometry): create box/sphere/cylinder primitives and combiners, set boolean operations, and inspect them.',
    packageId: 'com.IvanMurzak.Godot.MCP.CSG',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-AI-CSG',
    tools: [
      { name: 'csg-defaults', description: 'Return the recommended starter config (size / radius / height / segments) for a CSG node of the requested kind.' },
      { name: 'csg-box-create', description: 'Create a CsgBox3D primitive in the currently edited Godot scene.' },
      { name: 'csg-sphere-create', description: 'Create a CsgSphere3D primitive in the currently edited Godot scene.' },
      { name: 'csg-cylinder-create', description: 'Create a CsgCylinder3D primitive in the currently edited Godot scene.' },
      { name: 'csg-combiner-create', description: 'Create a CsgCombiner3D container that groups child CSG shapes for boolean ops.' },
      { name: 'csg-set-operation', description: "Set an existing CSG node's boolean operation (Union / Intersection / Subtraction)." },
      { name: 'csg-get', description: "Read a CSG node's scalar config (read-only)." },
    ],
  },
  {
    name: 'GridMap Tools',
    description:
      'AI MCP tools for Godot GridMap (3D tile-based maps): create, set/clear cells, assign a MeshLibrary, and inspect.',
    packageId: 'com.IvanMurzak.Godot.MCP.GridMap',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-AI-GridMap',
    tools: [
      { name: 'gridmap-defaults', description: 'Return the recommended starter configuration for a new GridMap node.' },
      { name: 'gridmap-create', description: 'Create a GridMap node in the currently edited Godot scene.' },
      { name: 'gridmap-set-mesh-library', description: 'Assign a MeshLibrary resource to an existing GridMap node.' },
      { name: 'gridmap-set-cell', description: 'Set a single cell of a GridMap to a MeshLibrary item.' },
      { name: 'gridmap-clear-cell', description: 'Clear a single cell of a GridMap.' },
      { name: 'gridmap-clear', description: 'Clear all cells of a GridMap.' },
      { name: 'gridmap-get', description: "Read a GridMap's scalar config (read-only)." },
    ],
  },
  {
    name: 'PhantomCamera Tools',
    description:
      'AI MCP tools for the Godot Phantom Camera addon (Cinemachine-style virtual cameras): create hosts and cameras, set follow/look-at targets and priority, and inspect them.',
    packageId: 'com.IvanMurzak.Godot.MCP.PhantomCamera',
    version: null,
    gitUrl: 'https://github.com/IvanMurzak/Godot-AI-PhantomCamera',
    tools: [
      { name: 'phantomcamera-defaults', description: 'Return the recommended starter config (priority, follow/look-at mode, damping) for a Phantom Camera.' },
      { name: 'phantomcamera-host-create', description: 'Ensure a PhantomCameraHost exists under a Camera3D in the edited scene (required by the addon).' },
      { name: 'phantomcamera-create', description: 'Create a PhantomCamera3D node (a virtual camera) in the currently edited Godot scene.' },
      { name: 'phantomcamera-set-follow', description: "Set an existing PhantomCamera3D's follow mode and/or follow target." },
      { name: 'phantomcamera-set-look-at', description: "Set an existing PhantomCamera3D's look-at mode and/or look-at target." },
      { name: 'phantomcamera-set-priority', description: "Set an existing PhantomCamera3D's priority (higher wins; raising it switches the active camera)." },
      { name: 'phantomcamera-get', description: "Read an existing PhantomCamera3D's scalar config (read-only)." },
    ],
    addonRequired: {
      name: 'Phantom Camera',
      assetLibId: '1822',
      repo: 'ramokz/phantom-camera',
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
