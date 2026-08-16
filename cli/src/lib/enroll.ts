import * as fs from 'fs';
import * as path from 'path';
import { commitToolsOnlyLogin, decodeJwtSubject, godotAdapter } from '@baizor/gamedev-cli-core';
import { DEFAULT_CLOUD_BASE_URL } from '../utils/connection.js';
import { redeemEnrollment } from '../utils/enroll.js';
import { openMachineStore } from '../utils/machine-store.js';
import { deriveProjectIdentityV2 } from '../utils/project-identity.js';
import {
  writeProjectMarker,
  getProjectMarkerPath,
  isLocalhostUrl,
  upsertPinIntoAgentConfigs,
} from '../utils/project-marker.js';
import { emitProgress } from './progress.js';
import type { EnrollPluginOptions, EnrollPluginResult } from './types.js';

/**
 * Redeem a D13 enrollment code and complete the agent-first onboarding for a
 * project (design 06 / 09 workflow 1A, step 3), with NO browser hop:
 *
 *  1. POST the code to the cloud AS `enroll/redeem` endpoint → an `mcp:plugin`
 *     JWT + refresh token + the server-target URL the code was minted for.
 *  2. Persist the plugin credential to the SHARED machine store
 *     (`~/.ai-game-dev/credentials.json`, 0600 / DPAPI) so the editor plugin
 *     auto-adopts it (D12) — zero editor clicks.
 *  3. Write the project marker (`.ai-game-dev/project.json`) with the server
 *     target, the D14 pin, and — for a localhost target — the deterministic
 *     local port.
 *  4. Upsert the D14 pin into any existing project-local agent config entry so a
 *     server the user added manually before enrolling becomes pinned.
 *
 * Library-safe: never throws past the boundary; returns a `{ kind }` union. On a
 * redeem failure NOTHING is written (the machine store + marker + configs are
 * left as found).
 */
export async function enrollPlugin(opts: EnrollPluginOptions): Promise<EnrollPluginResult> {
  const warnings: string[] = [];

  try {
    const projectPath = path.resolve(opts.godotProjectPath);
    if (!fs.existsSync(projectPath)) {
      throw new Error(`Project path does not exist: ${projectPath}`);
    }

    const baseUrl = (opts.baseUrl ?? DEFAULT_CLOUD_BASE_URL).replace(/\/$/, '');

    emitProgress(opts.onProgress, { phase: 'enroll-redeeming', message: 'Redeeming enrollment code' });
    const redeem = await redeemEnrollment({ code: opts.code, baseUrl, fetchImpl: opts.fetchImpl });
    if (!redeem.success) {
      // A redeem failure is terminal but non-throwing — nothing was written.
      throw new Error(redeem.message);
    }

    const { access_token, refresh_token, expires_in, server_url } = redeem.credential;
    const serverTarget = server_url;

    // 2. Machine store: commit the enroll-minted `mcp:plugin` credential as a PLUGIN family via
    // the shared cli-core tools-only commit (unified-machine-auth F10 — enroll is the code-based
    // plugin-only mint), under the 04 §2 cross-process lock, stamped with this CLI's own client
    // id. The D6/F7 account-switch guard applies with NO confirmation channel here (library
    // path), so a KNOWN-subject mismatch fails closed instead of silently replacing the account.
    const store = openMachineStore(opts.storeBaseDir);
    const expiresAt =
      typeof expires_in === 'number' && expires_in > 0
        ? new Date(Date.now() + expires_in * 1000).toISOString()
        : undefined;
    const subject = decodeJwtSubject(access_token);
    const commit = await commitToolsOnlyLogin({
      store,
      clientId: godotAdapter.clientId, // 'godot-cli'
      credentials: {
        accessToken: access_token,
        refreshToken: refresh_token,
        ...(expiresAt ? { expiresAt } : {}),
        serverTarget,
        ...(subject !== undefined ? { subject } : {}),
      },
      onWarning: (message) => warnings.push(message),
    });
    if (commit.status === 'switch-declined') {
      throw new Error(
        `This machine is signed in as a different account (${commit.storedSubject}); the enrollment ` +
          'credential was revoked and nothing was written. Sign out first (or enroll on a clean machine).',
      );
    }
    if (commit.status === 'aborted') {
      throw new Error('The machine credential store changed concurrently; re-run the enrollment.');
    }
    const credentialsPath = store.credentialsPath;

    emitProgress(opts.onProgress, { phase: 'enroll-redeemed', message: 'Credential planted', serverTarget });

    // 3. Project marker: server target + pin (+ derived port for a localhost target). The pin/port
    // are derived with the cli-core **v2** algorithm (`\`→`/` normalization — defect B5): on a
    // Windows `path.resolve` backslash root the pin now matches the forward-slash hash the plugin
    // sends, so routing works. v1 hashed the backslash form to a DIFFERENT pin (the B5 break).
    // Derive pin + port from ONE v2 hash of the project root (deriveProjectIdentityV2 hashes once).
    const { pin, port: derivedPort } = deriveProjectIdentityV2(projectPath);
    const localhost = isLocalhostUrl(serverTarget);
    const port = localhost ? derivedPort : undefined;
    writeProjectMarker(projectPath, {
      serverTarget,
      pin,
      ...(port !== undefined ? { port } : {}),
    });
    const markerPath = getProjectMarkerPath(projectPath);

    // 4. Upsert the pin into any existing project-local agent config entry.
    const pinnedConfigs = upsertPinIntoAgentConfigs(projectPath, pin);

    emitProgress(opts.onProgress, { phase: 'done', message: 'Enrollment complete.' });

    return {
      kind: 'success',
      success: true,
      serverTarget,
      pin,
      port,
      credentialsPath,
      markerPath,
      pinnedConfigs,
      warnings,
    };
  } catch (err: unknown) {
    return {
      kind: 'failure',
      success: false,
      warnings,
      error: err instanceof Error ? err : new Error(String(err)),
    };
  }
}
