import * as fs from 'fs';
import * as path from 'path';
import { ADDON_PACKAGE_REFERENCES, EDITOR_ONLY_CONDITION, type AddonPackageReference } from './addon-deps.js';

// Resolve the NuGet PackageReferences the INSTALLED addon needs, so `install-plugin`
// pins the consumer csproj to what that addon release was built against — not to the
// constants baked into this CLI build (they differ whenever the addon version differs
// from the CLI version, e.g. `--version <other>` or a kept newer addon).
//
// Sources, in order:
//  1. `--source` installs only: `Godot-MCP.csproj` of the source checkout (two levels
//     above `addons/godot_mcp/`) — the single source of truth in the repo.
//  2. `addons/godot_mcp/nuget-deps.json` inside the installed addon. The release
//     workflow generates it from Godot-MCP.csproj (`scripts/write-nuget-deps.mjs`) and
//     packs it into the addon zip; it is never committed.
//  3. The CLI's built-in `ADDON_PACKAGE_REFERENCES` (addon zips released before the
//     manifest existed), with a warning.

/** File name of the generated manifest inside `addons/godot_mcp/`. */
const ADDON_NUGET_DEPS_FILE = 'nuget-deps.json';

/** The addon's own project file, at the root of a Godot-MCP checkout. */
const ADDON_CSPROJ_FILE = 'Godot-MCP.csproj';

export type AddonNugetDepsOrigin = 'source-csproj' | 'addon-manifest' | 'builtin';

export interface ResolvedAddonNugetDeps {
  refs: readonly AddonPackageReference[];
  origin: AddonNugetDepsOrigin;
  /** The file the refs were read from (absent for `builtin`). */
  file?: string;
  warnings: string[];
}

// Values are written verbatim into the consumer csproj as XML attributes, so only
// NuGet-shaped ids and versions are accepted (no quotes, `<`, `&`, whitespace).
const NUGET_ID_RE = /^[A-Za-z0-9_][A-Za-z0-9_.-]*$/;
const NUGET_VERSION_RE = /^[0-9A-Za-z][0-9A-Za-z.+-]*$/;

function validRef(id: unknown, version: unknown, editorOnly: boolean): AddonPackageReference | null {
  return typeof id === 'string' && typeof version === 'string' && NUGET_ID_RE.test(id) && NUGET_VERSION_RE.test(version)
    ? { id, version, ...(editorOnly && { editorOnly }) }
    : null;
}

/**
 * Every `<PackageReference Include=".." Version="..">` in a csproj text, in document order. A reference whose
 * `Condition` is `EDITOR_ONLY_CONDITION` is editor-only.
 */
export function parseCsprojPackageReferences(csprojText: string): AddonPackageReference[] {
  const refs: AddonPackageReference[] = [];
  for (const element of csprojText.match(/<PackageReference\b[^>]*>/gi) ?? []) {
    const ref = validRef(
      /\bInclude\s*=\s*"([^"]+)"/i.exec(element)?.[1],
      /\bVersion\s*=\s*"([^"]+)"/i.exec(element)?.[1],
      /\bCondition\s*=\s*"([^"]*)"/i.exec(element)?.[1] === EDITOR_ONLY_CONDITION,
    );
    if (ref !== null) refs.push(ref);
  }
  return refs;
}

/**
 * Parse a `nuget-deps.json` manifest (`{ "packageReferences": [{ "id", "version", "editorOnly"? }] }`).
 * Manifests that predate `editorOnly` mark nothing editor-only, which those addons need (their RQL sources
 * compile into export builds). Throws on malformed JSON, a missing/empty list, or an invalid entry.
 */
export function parseAddonNugetDeps(text: string): AddonPackageReference[] {
  const parsed: unknown = JSON.parse(text);
  const list = (parsed as { packageReferences?: unknown } | null)?.packageReferences;
  if (!Array.isArray(list) || list.length === 0) {
    throw new Error('"packageReferences" must be a non-empty array');
  }
  return list.map((entry: unknown, i) => {
    const e = entry as { id?: unknown; version?: unknown; editorOnly?: unknown } | null;
    const ref = validRef(e?.id, e?.version, e?.editorOnly === true);
    if (ref === null) throw new Error(`packageReferences[${i}] is not a valid { id, version } pair`);
    return ref;
  });
}

/**
 * Resolve the addon's NuGet PackageReferences for an installed addon dir. Never throws:
 * an unreadable/malformed source is reported in `warnings` and the next source is tried.
 * `sourceDir` is the `--source` addon dir (`.../addons/godot_mcp`), when there is one.
 */
export function resolveAddonNugetDeps(addonDir: string, sourceDir?: string): ResolvedAddonNugetDeps {
  const warnings: string[] = [];

  if (sourceDir !== undefined) {
    const csproj = path.resolve(sourceDir, '..', '..', ADDON_CSPROJ_FILE);
    if (fs.existsSync(csproj)) {
      try {
        const refs = parseCsprojPackageReferences(fs.readFileSync(csproj, 'utf-8'));
        if (refs.length > 0) return { refs, origin: 'source-csproj', file: csproj, warnings };
        warnings.push(`${csproj} declares no <PackageReference>; ignoring it.`);
      } catch (err) {
        warnings.push(`Could not read ${csproj}: ${err instanceof Error ? err.message : String(err)}`);
      }
    }
  }

  const manifest = path.join(addonDir, ADDON_NUGET_DEPS_FILE);
  if (fs.existsSync(manifest)) {
    try {
      const refs = parseAddonNugetDeps(fs.readFileSync(manifest, 'utf-8'));
      return { refs, origin: 'addon-manifest', file: manifest, warnings };
    } catch (err) {
      warnings.push(`Ignoring malformed ${manifest}: ${err instanceof Error ? err.message : String(err)}`);
    }
  }

  warnings.push(
    `The installed addon carries no readable ${ADDON_NUGET_DEPS_FILE}; pinned the NuGet packages to this godot-cli build's ` +
      `defaults (${ADDON_PACKAGE_REFERENCES.map((r) => `${r.id} ${r.version}`).join(', ')}). ` +
      'If the addon version differs from the godot-cli version, check these against the addon release.',
  );
  return { refs: ADDON_PACKAGE_REFERENCES, origin: 'builtin', warnings };
}
