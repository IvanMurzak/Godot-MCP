import {
  ADDON_PACKAGE_REFERENCES,
  ADDON_EMBEDDED_RESOURCES,
  type AddonPackageReference,
  type AddonEmbeddedResource,
} from './addon-deps.js';

/**
 * Idempotently reconcile a Godot consumer `.csproj`'s `<PackageReference>`s so it
 * declares exactly the addon's two required NuGet pins (see `addon-deps.ts`).
 *
 * Pure: operates on the csproj TEXT and returns the new text + what changed; never
 * touches the filesystem. Exported for unit tests. Handles, idempotently:
 *  - a project with NO `<ItemGroup>` at all (appends one before `</Project>`);
 *  - a project that already declares both pins at the right version (no change);
 *  - a project that declares one or both at a DIFFERENT version (reconciles in place);
 *  - a project that declares only one of the two (adds the missing one).
 *
 * It deliberately does a minimal, well-scoped text edit rather than a full XML
 * parse/serialize, to preserve the rest of the consumer's csproj formatting
 * verbatim. The `<PackageReference Include="..." />` shape it reads/writes is the
 * single self-closing form the scaffold and the addon README both use.
 */

export type CsprojPatchChange =
  | { id: string; action: 'added'; version: string }
  | { id: string; action: 'updated'; from: string; version: string }
  | { id: string; action: 'unchanged'; version: string };

export interface CsprojPatchResult {
  /** The new csproj text (identical to input when nothing changed). */
  text: string;
  /** True when the text was modified. */
  changed: boolean;
  /** One entry per required package describing what happened to it. */
  changes: CsprojPatchChange[];
}

export type CsprojEmbedChange =
  | { include: string; action: 'added'; logicalName: string }
  | { include: string; action: 'updated'; from: string; logicalName: string }
  | { include: string; action: 'unchanged'; logicalName: string };

export interface CsprojEmbedResult {
  /** The new csproj text (identical to input when nothing changed). */
  text: string;
  /** True when the text was modified. */
  changed: boolean;
  /** One entry per required embedded resource describing what happened to it. */
  changes: CsprojEmbedChange[];
}

/** Escape a string for use inside a RegExp source. */
function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * Match an existing `<PackageReference Include="<id>" Version="<ver>" />` element
 * for a specific package id, capturing the whole element and its version, in
 * either attribute order (`Include` before or after `Version`). The element may
 * also be a `<PackageReference ...></PackageReference>` open/close pair.
 */
function packageRefRegex(id: string): RegExp {
  const escId = escapeRegExp(id);
  // <PackageReference ... Include="id" ... Version="x" ... /> (Include before Version)
  // or Version before Include. Two alternations cover both orders; either may end
  // with a self-close `/>` or an explicit `></PackageReference>`.
  return new RegExp(
    `<PackageReference\\b[^>]*?\\bInclude\\s*=\\s*"${escId}"[^>]*?\\bVersion\\s*=\\s*"([^"]*)"[^>]*?(?:/>|></PackageReference>)` +
      `|` +
      `<PackageReference\\b[^>]*?\\bVersion\\s*=\\s*"([^"]*)"[^>]*?\\bInclude\\s*=\\s*"${escId}"[^>]*?(?:/>|></PackageReference>)`,
    'i',
  );
}

/**
 * Match an existing `<EmbeddedResource ... Include="<include>" ... />` element for a
 * specific file path, capturing the whole element (in either attribute order, and
 * whether self-closed `/>` or an explicit `></EmbeddedResource>` pair). The
 * `LogicalName` is extracted from the captured element text separately so the regex
 * stays order-agnostic and tolerant of an absent `LogicalName`.
 */
function embeddedResourceRegex(include: string): RegExp {
  const escInclude = escapeRegExp(include);
  return new RegExp(
    `<EmbeddedResource\\b[^>]*?\\bInclude\\s*=\\s*"${escInclude}"[^>]*?(?:/>|></EmbeddedResource>)`,
    'i',
  );
}

/** Render the canonical self-closing element for a required reference. */
function renderRef(ref: AddonPackageReference): string {
  return `<PackageReference Include="${ref.id}" Version="${ref.version}" />`;
}

/** Render the canonical self-closing element for a required embedded resource. */
function renderEmbed(res: AddonEmbeddedResource): string {
  return `<EmbeddedResource Include="${res.include}" LogicalName="${res.logicalName}" />`;
}

/**
 * Detect the indentation used by the FIRST existing `<PackageReference>` /
 * `<EmbeddedResource>` (or fall back to 4 spaces) so appended/inserted elements
 * match the consumer's style.
 */
function detectIndent(text: string): string {
  const m = text.match(/\n([ \t]*)<(?:PackageReference|EmbeddedResource)\b/);
  return m ? m[1] : '    ';
}

/**
 * Detect the dominant line ending of the csproj so inserted/appended XML matches
 * it (a CRLF csproj should not gain bare-LF lines). Returns `\r\n` when CRLF
 * line endings outnumber bare-LF ones, else `\n`.
 */
function detectEol(text: string): '\n' | '\r\n' {
  const crlf = (text.match(/\r\n/g) ?? []).length;
  const lf = (text.match(/\n/g) ?? []).length - crlf;
  return crlf > lf ? '\r\n' : '\n';
}

export function addAddonPackageReferences(
  csprojText: string,
  requiredRefs: readonly AddonPackageReference[] = ADDON_PACKAGE_REFERENCES,
): CsprojPatchResult {
  const changes: CsprojPatchChange[] = [];
  let text = csprojText;
  const indent = detectIndent(text);
  const eol = detectEol(text);

  // First pass: reconcile any references that already exist (possibly at a
  // different version). Collect the ones still missing for the append pass.
  const missing: AddonPackageReference[] = [];

  for (const ref of requiredRefs) {
    const re = packageRefRegex(ref.id);
    const match = text.match(re);
    if (!match) {
      missing.push(ref);
      continue;
    }
    const existingVersion = match[1] ?? match[2] ?? '';
    if (existingVersion === ref.version) {
      changes.push({ id: ref.id, action: 'unchanged', version: ref.version });
      continue;
    }
    // Reconcile in place: replace the whole element with the canonical pin.
    text = text.replace(re, renderRef(ref));
    changes.push({ id: ref.id, action: 'updated', from: existingVersion, version: ref.version });
  }

  // Second pass: add the missing references. Prefer an existing <ItemGroup> that
  // already holds a <PackageReference> (group them together); else append a fresh
  // <ItemGroup> just before </Project>.
  if (missing.length > 0) {
    const block = missing.map((ref) => `${indent}${renderRef(ref)}`).join(eol);

    const pkgItemGroupClose = findPackageReferenceItemGroupClose(text);
    if (pkgItemGroupClose !== -1) {
      // Insert the missing refs just before that ItemGroup's closing tag.
      text = `${text.slice(0, pkgItemGroupClose)}${block}${eol}${text.slice(pkgItemGroupClose)}`;
    } else {
      // No PackageReference ItemGroup — append a new one before </Project>.
      const groupIndent = indent.length >= 2 ? indent.slice(0, Math.floor(indent.length / 2)) : '  ';
      const newGroup = `${groupIndent}<ItemGroup>${eol}${block}${eol}${groupIndent}</ItemGroup>${eol}`;
      const closeIdx = text.lastIndexOf('</Project>');
      if (closeIdx === -1) {
        // Not a recognizable csproj; append best-effort at EOF.
        text = `${text}${eol}${newGroup}`;
      } else {
        text = `${text.slice(0, closeIdx)}${newGroup}${text.slice(closeIdx)}`;
      }
    }

    for (const ref of missing) {
      changes.push({ id: ref.id, action: 'added', version: ref.version });
    }
  }

  // Restore the required-order in `changes` to match `requiredRefs` for stable output.
  const orderedChanges = requiredRefs
    .map((ref) => changes.find((c) => c.id === ref.id))
    .filter((c): c is CsprojPatchChange => c !== undefined);

  return {
    text,
    changed: orderedChanges.some((c) => c.action !== 'unchanged'),
    changes: orderedChanges,
  };
}

/**
 * Idempotently reconcile a Godot consumer `.csproj`'s `<EmbeddedResource>`s so it
 * declares exactly the addon's required embeds (see `addon-deps.ts`
 * `ADDON_EMBEDDED_RESOURCES`). Pure (operates on csproj TEXT, never touches the
 * filesystem) and exported for unit tests. Sibling of `addAddonPackageReferences`.
 * Handles, idempotently:
 *  - a project with NO `<EmbeddedResource>` ItemGroup (groups into one, else appends
 *    a fresh `<ItemGroup>` before `</Project>`);
 *  - a project that already embeds the resource with the right LogicalName (no change);
 *  - a project that embeds the same Include with a DIFFERENT LogicalName (reconciles
 *    in place);
 * Re-running `install-plugin` therefore never duplicates the `<EmbeddedResource>`.
 */
export function addAddonEmbeddedResources(
  csprojText: string,
  requiredResources: readonly AddonEmbeddedResource[] = ADDON_EMBEDDED_RESOURCES,
): CsprojEmbedResult {
  const changes: CsprojEmbedChange[] = [];
  let text = csprojText;
  const indent = detectIndent(text);
  const eol = detectEol(text);

  // First pass: reconcile any embeds that already exist (possibly with a different
  // LogicalName). Collect the ones still missing for the append pass.
  const missing: AddonEmbeddedResource[] = [];

  for (const res of requiredResources) {
    const re = embeddedResourceRegex(res.include);
    const match = text.match(re);
    if (!match) {
      missing.push(res);
      continue;
    }
    const logicalMatch = match[0].match(/\bLogicalName\s*=\s*"([^"]*)"/i);
    const existingLogical = logicalMatch ? logicalMatch[1] : '';
    if (existingLogical === res.logicalName) {
      changes.push({ include: res.include, action: 'unchanged', logicalName: res.logicalName });
      continue;
    }
    // Reconcile in place: replace the whole element with the canonical embed.
    text = text.replace(re, renderEmbed(res));
    changes.push({ include: res.include, action: 'updated', from: existingLogical, logicalName: res.logicalName });
  }

  // Second pass: add the missing embeds. Prefer an existing <ItemGroup> that already
  // holds an <EmbeddedResource> (group them together); else append a fresh
  // <ItemGroup> just before </Project>. Deliberately NOT grouped with the
  // PackageReference ItemGroup — the addon's own csproj keeps the two in separate
  // groups, and this keeps the scaffold's layout identical.
  if (missing.length > 0) {
    const block = missing.map((res) => `${indent}${renderEmbed(res)}`).join(eol);

    const embedItemGroupClose = findItemGroupCloseContaining(text, /<EmbeddedResource\b/i);
    if (embedItemGroupClose !== -1) {
      text = `${text.slice(0, embedItemGroupClose)}${block}${eol}${text.slice(embedItemGroupClose)}`;
    } else {
      const groupIndent = indent.length >= 2 ? indent.slice(0, Math.floor(indent.length / 2)) : '  ';
      const newGroup = `${groupIndent}<ItemGroup>${eol}${block}${eol}${groupIndent}</ItemGroup>${eol}`;
      const closeIdx = text.lastIndexOf('</Project>');
      if (closeIdx === -1) {
        text = `${text}${eol}${newGroup}`;
      } else {
        text = `${text.slice(0, closeIdx)}${newGroup}${text.slice(closeIdx)}`;
      }
    }

    for (const res of missing) {
      changes.push({ include: res.include, action: 'added', logicalName: res.logicalName });
    }
  }

  // Restore required-order in `changes` to match `requiredResources` for stable output.
  const orderedChanges = requiredResources
    .map((res) => changes.find((c) => c.include === res.include))
    .filter((c): c is CsprojEmbedChange => c !== undefined);

  return {
    text,
    changed: orderedChanges.some((c) => c.action !== 'unchanged'),
    changes: orderedChanges,
  };
}

/**
 * Return the index of the `</ItemGroup>` that closes the FIRST `<ItemGroup>`
 * containing a `<PackageReference>`, or -1 when none exists. Used to group the
 * added pins with the consumer's existing package references.
 */
function findPackageReferenceItemGroupClose(text: string): number {
  return findItemGroupCloseContaining(text, /<PackageReference\b/i);
}

/**
 * Return the index of the `</ItemGroup>` that closes the FIRST `<ItemGroup>` whose
 * body matches `childTagRe`, or -1 when none exists. Used to group an added element
 * with the consumer's existing items of the same kind. `childTagRe` MUST NOT be a
 * global regex (a stateful `lastIndex` would corrupt the `.test` below).
 */
function findItemGroupCloseContaining(text: string, childTagRe: RegExp): number {
  const itemGroupRe = /<ItemGroup\b[^>]*>([\s\S]*?)<\/ItemGroup>/gi;
  let m: RegExpExecArray | null;
  while ((m = itemGroupRe.exec(text)) !== null) {
    if (childTagRe.test(m[1])) {
      // The close tag starts at the end of the match minus its length.
      const closeTag = '</ItemGroup>';
      return m.index + m[0].length - closeTag.length;
    }
  }
  return -1;
}
