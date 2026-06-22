// Pure helpers for resolving WHERE the `addons/godot_mcp/` files come from when
// `install-plugin` materializes them: the trusted GitHub-release download URL and
// the host-trust check. Mirrors the addon's own trusted-source pattern in
// `addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs` (github.com only,
// `releases/download/v<version>/<asset>`). No side effects; unit-testable.

/** GitHub-release host the addon zip is downloaded from. NOTHING else is trusted. */
export const TRUSTED_DOWNLOAD_HOST = 'github.com';

/** The `owner/repo` the addon releases live under. */
export const ADDON_RELEASE_REPO = 'IvanMurzak/Godot-MCP';

/**
 * The release-asset name for an addon version: `godot-mcp-addon-<version>.zip`.
 * Matches the asset the release workflow attaches (`release.yml` → "Package addon
 * zip": `zip_name="godot-mcp-addon-${version}.zip"`). The `<version>` is the bare
 * version WITHOUT a leading `v` (the `v` only appears in the tag). Pure.
 */
export function addonAssetName(version: string): string {
  return `godot-mcp-addon-${stripLeadingV(version)}.zip`;
}

/**
 * The git release TAG for an addon version: the version with a leading `v`
 * (`0.11.1` → `v0.11.1`). The release workflow tags every release `v<version>`
 * (`release.yml` → `tag=v${version}`); an already-`v`-prefixed input is passed
 * through unchanged so a caller cannot double-prefix. Pure.
 */
export function releaseTag(version: string): string {
  const v = (version ?? '').trim();
  return v.startsWith('v') ? v : `v${v}`;
}

/** Drop a single leading `v`/`V` from a version string. Pure. */
export function stripLeadingV(version: string): string {
  const v = (version ?? '').trim();
  return /^v/i.test(v) ? v.slice(1) : v;
}

/**
 * The download URL for an addon version's zip:
 * `https://github.com/IvanMurzak/Godot-MCP/releases/download/v<version>/godot-mcp-addon-<version>.zip`.
 * Mirrors `GodotMcpServerView.DownloadUrl`. Pure string build. The host is always
 * `github.com` by construction; `assertTrustedDownloadUrl` re-checks a
 * caller-supplied URL before any network call.
 */
export function addonDownloadUrl(version: string): string {
  const bare = stripLeadingV(version);
  return `https://${TRUSTED_DOWNLOAD_HOST}/${ADDON_RELEASE_REPO}/releases/download/${releaseTag(version)}/${addonAssetName(bare)}`;
}

/**
 * Fail-closed host trust check: throw unless `url` is an HTTPS URL whose host is
 * EXACTLY `github.com` (no subdomains, no `github.com.evil.test`, no http). This
 * is the security boundary the task requires — reject non-github hosts. Returns
 * the parsed URL on success. Pure (throws on the unhappy path; the caller turns
 * it into a structured failure so nothing escapes the public boundary).
 */
export function assertTrustedDownloadUrl(url: string): URL {
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    throw new Error(`Refusing to download addon: malformed URL "${url}".`);
  }
  if (parsed.protocol !== 'https:') {
    throw new Error(
      `Refusing to download addon: only https is allowed, got "${parsed.protocol}" (${url}).`,
    );
  }
  if (parsed.hostname.toLowerCase() !== TRUSTED_DOWNLOAD_HOST) {
    throw new Error(
      `Refusing to download addon from untrusted host "${parsed.hostname}". Only ${TRUSTED_DOWNLOAD_HOST} is trusted.`,
    );
  }
  return parsed;
}
