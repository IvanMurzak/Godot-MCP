// Pure, behaviorally-identical TS port of the dock's extension-install logic
// (`addons/godot_mcp/Runtime/Extensions/ExtensionInstallPlanner.cs` +
// `InstalledStateDetector.CompareVersions`). It computes the add / update / no-op
// plan AND the resulting `.csproj` text from a descriptor + the current `.csproj`,
// preserving every other `<PackageReference>` and unrelated XML.
//
// Parity contract (verified by `cli/tests/extension-install.test.ts` against the
// SAME scenario set as `Godot-MCP.Tests/ExtensionInstallTests.cs`):
//   - absent reference            → ADD (append; version attribute only when pinned)
//   - present, descriptor newer   → UPDATE (bump version, honoring attr vs child form)
//   - present, no version, pinned → UPDATE (set the descriptor's pin)
//   - present, equal/newer        → NO-OP
//   - descriptor unpinned, present→ NO-OP
//   - version compare is numeric + tolerant (1.10.0 > 1.2.0; trailing 0; suffix-tolerant)
//
// The C# planner uses System.Xml.Linq; this uses scoped text edits (like the CLI's
// existing `csproj-deps.ts`) so the consumer's formatting is preserved verbatim. The
// add/update/no-op DECISION and the resulting parsed PackageReferences are identical.
//
// No top-level side effects; pure string transforms only.

import type { ExtensionDescriptor } from './extensions-catalog.js';
import { hasVersion } from './extensions-catalog.js';

export type ExtensionInstallAction = 'add' | 'update' | 'noop';

export interface ExtensionInstallPlan {
  /** The add / update / no-op decision. */
  action: ExtensionInstallAction;
  /** The full `.csproj` text after applying the action (=== input for a no-op). */
  resultingCsproj: string;
  /** The package id the plan targets. */
  packageId: string;
  /** Version currently referenced: `null` when not installed; `''` when referenced without a version. */
  fromVersion: string | null;
  /** The descriptor's target version, or `null` when the descriptor has no pin. */
  toVersion: string | null;
}

/** Thrown by {@link planExtensionInstall} on a genuinely unparseable `.csproj` (the lib turns it into a structured failure). */
export class CsprojParseError extends Error {}

/** Escape a string for use inside a RegExp source. */
function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * Compare two dotted numeric version strings component-by-component as integers
 * (so `1.10.0` > `1.2.0`, unlike an ordinal string compare). A trailing pre-release
 * / build suffix on a component (`1.0.0-rc1`) is tolerated — only the leading integer
 * of each component is read; a non-numeric component reads as 0. Missing trailing
 * components are treated as 0 (`1.0` === `1.0.0`). Returns >0 when `a` is newer, <0
 * when older, 0 when equal. Exact port of C# `InstalledStateDetector.CompareVersions`.
 */
export function compareVersions(a: string, b: string): number {
  const pa = splitComponents(a);
  const pb = splitComponents(b);
  const n = Math.max(pa.length, pb.length);
  for (let i = 0; i < n; i++) {
    const ca = i < pa.length ? pa[i] : 0;
    const cb = i < pb.length ? pb[i] : 0;
    if (ca !== cb) return ca < cb ? -1 : 1;
  }
  return 0;
}

function splitComponents(version: string | null | undefined): number[] {
  if (version === null || version === undefined || version.trim() === '') return [];
  return version
    .trim()
    .split('.')
    .map((part) => leadingInt(part));
}

function leadingInt(component: string): number {
  let end = 0;
  while (end < component.length && component[end] >= '0' && component[end] <= '9') end++;
  if (end === 0) return 0;
  const value = Number.parseInt(component.slice(0, end), 10);
  return Number.isNaN(value) ? 0 : value;
}

/**
 * Parse every `<PackageReference>` in the `.csproj` into a `{ packageId → version }`
 * map (ids lower-cased — NuGet ids are case-insensitive; value is the `Version`
 * attribute, else a child `<Version>` element, else `''`). Returns an EMPTY map for
 * empty/whitespace input. Port of C# `InstalledStateDetector.ParsePackageReferences`
 * (the CLI never needs the bad-XML tolerance branch — the planner validates first).
 */
export function parsePackageReferences(csprojText: string | null | undefined): Map<string, string> {
  const map = new Map<string, string>();
  if (csprojText === null || csprojText === undefined || csprojText.trim() === '') return map;

  const elementRe = /<PackageReference\b[^>]*?(?:\/>|>[\s\S]*?<\/PackageReference>)/gi;
  let m: RegExpExecArray | null;
  while ((m = elementRe.exec(csprojText)) !== null) {
    const element = m[0];
    const include = /\bInclude\s*=\s*"([^"]*)"/i.exec(element);
    if (!include || include[1].trim() === '') continue;
    map.set(include[1].trim().toLowerCase(), readElementVersion(element));
  }
  return map;
}

/** Read the version off a single matched `<PackageReference>` element (attribute form, else child element; `''` when unversioned). */
function readElementVersion(element: string): string {
  const attr = /\bVersion\s*=\s*"([^"]*)"/i.exec(element);
  if (attr && attr[1].trim() !== '') return attr[1].trim();
  const child = /<Version>\s*([^<]*?)\s*<\/Version>/i.exec(element);
  if (child) return child[1].trim();
  return '';
}

/** Match the whole `<PackageReference>` element (either attribute order, self-close or open/close) for one id. */
function packageRefElementRegex(id: string): RegExp {
  const escId = escapeRegExp(id);
  return new RegExp(
    `<PackageReference\\b[^>]*?\\bInclude\\s*=\\s*"${escId}"[^>]*?(?:/>|>[\\s\\S]*?</PackageReference>)`,
    'i',
  );
}

function detectIndent(text: string): string {
  const m = text.match(/\n([ \t]*)<PackageReference\b/);
  return m ? m[1] : '    ';
}

function detectEol(text: string): '\n' | '\r\n' {
  const crlf = (text.match(/\r\n/g) ?? []).length;
  const lf = (text.match(/\n/g) ?? []).length - crlf;
  return crlf > lf ? '\r\n' : '\n';
}

function renderRef(descriptor: ExtensionDescriptor): string {
  return hasVersion(descriptor)
    ? `<PackageReference Include="${descriptor.packageId}" Version="${descriptor.version}" />`
    : `<PackageReference Include="${descriptor.packageId}" />`;
}

/** Index of the `</ItemGroup>` closing the FIRST `<ItemGroup>` holding a `<PackageReference>`, or -1. */
function findPackageReferenceItemGroupClose(text: string): number {
  const itemGroupRe = /<ItemGroup\b[^>]*>([\s\S]*?)<\/ItemGroup>/gi;
  let m: RegExpExecArray | null;
  while ((m = itemGroupRe.exec(text)) !== null) {
    if (/<PackageReference\b/i.test(m[1])) {
      const closeTag = '</ItemGroup>';
      return m.index + m[0].length - closeTag.length;
    }
  }
  return -1;
}

/** Append a new `<PackageReference>` for the descriptor, joining the first package ItemGroup or a fresh one. */
function appendReference(text: string, descriptor: ExtensionDescriptor): string {
  const indent = detectIndent(text);
  const eol = detectEol(text);
  const element = `${indent}${renderRef(descriptor)}`;

  const pkgItemGroupClose = findPackageReferenceItemGroupClose(text);
  if (pkgItemGroupClose !== -1) {
    return `${text.slice(0, pkgItemGroupClose)}${element}${eol}${text.slice(pkgItemGroupClose)}`;
  }

  const groupIndent = indent.length >= 2 ? indent.slice(0, Math.floor(indent.length / 2)) : '  ';
  const newGroup = `${groupIndent}<ItemGroup>${eol}${element}${eol}${groupIndent}</ItemGroup>${eol}`;
  const closeIdx = text.lastIndexOf('</Project>');
  if (closeIdx === -1) return `${text}${eol}${newGroup}`;
  return `${text.slice(0, closeIdx)}${newGroup}${text.slice(closeIdx)}`;
}

/** Set the version on an existing element span, honoring whichever form it already uses (attribute, child element, or unversioned). */
function setElementVersion(element: string, version: string): string {
  // Existing Version="x" attribute → replace its value.
  if (/\bVersion\s*=\s*"[^"]*"/i.test(element)) {
    return element.replace(/\bVersion\s*=\s*"[^"]*"/i, `Version="${version}"`);
  }
  // Existing <Version>x</Version> child element → replace inner text (keep the child form).
  if (/<Version>[\s\S]*?<\/Version>/i.test(element)) {
    return element.replace(/<Version>[\s\S]*?<\/Version>/i, `<Version>${version}</Version>`);
  }
  // No version present → add a Version attribute to the open tag.
  if (/\/>\s*$/.test(element)) {
    // self-close: `<PackageReference Include="id" />` → `... Version="x" />`
    return element.replace(/\s*\/>\s*$/, ` Version="${version}" />`);
  }
  // open/close pair with no child version: add the attribute to the open tag's first `>`.
  return element.replace(/>/, ` Version="${version}">`);
}

/**
 * Compute the install plan for `descriptor` against `csprojText`. Port of
 * `ExtensionInstallPlanner.Plan`. Throws {@link CsprojParseError} only on a genuinely
 * malformed `.csproj` (no recognizable `<Project ...>` root) — the caller maps that to
 * a structured failure rather than corrupting the file.
 */
export function planExtensionInstall(
  descriptor: ExtensionDescriptor,
  csprojText: string,
): ExtensionInstallPlan {
  if (!/<Project\b[\s\S]*?>/i.test(csprojText) || !/<\/Project>/i.test(csprojText)) {
    throw new CsprojParseError('The consumer .csproj is not valid XML; refusing to edit it.');
  }

  const elementRe = packageRefElementRegex(descriptor.packageId);
  const match = csprojText.match(elementRe);

  // --- No existing reference → ADD ---
  if (!match) {
    return {
      action: 'add',
      resultingCsproj: appendReference(csprojText, descriptor),
      packageId: descriptor.packageId,
      fromVersion: null,
      toVersion: hasVersion(descriptor) ? descriptor.version : null,
    };
  }

  const element = match[0];
  const currentVersion = readElementVersion(element);

  // Descriptor pins no version → leave the existing reference untouched.
  if (!hasVersion(descriptor)) {
    return {
      action: 'noop',
      resultingCsproj: csprojText,
      packageId: descriptor.packageId,
      fromVersion: currentVersion,
      toVersion: null,
    };
  }

  const target = descriptor.version as string;
  const needsBump = currentVersion === '' || compareVersions(target, currentVersion) > 0;
  if (!needsBump) {
    return {
      action: 'noop',
      resultingCsproj: csprojText,
      packageId: descriptor.packageId,
      fromVersion: currentVersion,
      toVersion: target,
    };
  }

  const updatedElement = setElementVersion(element, target);
  return {
    action: 'update',
    // Function replacement so any `$` in the version is never treated as a replacement pattern.
    resultingCsproj: csprojText.replace(elementRe, () => updatedElement),
    packageId: descriptor.packageId,
    fromVersion: currentVersion,
    toVersion: target,
  };
}
