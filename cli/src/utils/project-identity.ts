/**
 * The single canonical derivation of a project's **routing pin** and its **deterministic local
 * port** — now a THIN RE-EXPORT of the shared `@baizor/gamedev-cli-core` `project-identity` module
 * (auth-fixes design 02 §T3 / T7, defect B5). cli-core is the ONE TypeScript source of truth, gated
 * byte-for-byte against the same golden vectors as the C# reference
 * `com.IvanMurzak.McpPlugin.AgentConfig.ProjectIdentity` — so the Godot CLI, the Godot addon, and the
 * other two engine CLIs all derive identical pins/ports.
 *
 * Two normalizations ship side by side:
 *   - **v1** (`derivePin` / `derivePort` / `normalize` / …) — the legacy algorithm, kept verbatim so
 *     old `.mcp.json` pins keep matching during the dual-hash transition (decision M1). Separators
 *     are NOT converted: `C:\a` and `C:/a` hash differently.
 *   - **v2** (`derivePinV2` / `derivePortV2` / `normalizeV2` / …) — adds ONE step (convert `\`→`/`)
 *     so a Windows root reported with backslashes and the same root with forward slashes hash
 *     IDENTICALLY. This is the **B5 fix**: the pin `enroll` / `setup-mcp` write now matches the
 *     forward-slash hash the plugin sends, even from a Windows `path.resolve` backslash root.
 *
 * The local reimplementation this file used to carry was removed in the cli-core migration (task
 * i1-godot-cli-migration) so the two can never silently diverge.
 */

export {
  MIN_PORT,
  MAX_PORT,
  PORT_RANGE,
  PIN_LENGTH,
  toLowerInvariant,
  // v1 (legacy, separator-sensitive)
  normalize,
  derivePin,
  derivePort,
  deriveProjectPathHash,
  deriveProjectIdentity,
  // v2 (B5 fix — `\`→`/` normalization; what configurators now emit)
  normalizeV2,
  derivePinV2,
  derivePortV2,
  deriveProjectPathHashV2,
  deriveProjectIdentityV2,
} from '@baizor/gamedev-cli-core';

export type { ProjectIdentity } from '@baizor/gamedev-cli-core';
