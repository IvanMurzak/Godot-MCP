import * as fs from 'fs';
import * as path from 'path';
import {
  DEFAULT_SERVER_VERSION,
  detectHostRid,
  serverDownloadUrl,
  serverAssetName,
  serverExecutableFileName,
  sha256SumsUrl,
  assertTrustedServerUrl,
  verifyZipChecksum,
  checksumFailureReason,
  sha256Hex,
} from '../utils/server-source.js';
import { parseZip, type ZipEntry } from '../utils/unzip.js';
import { emitProgress } from './progress.js';
import type { InstallServerOptions, InstallServerResult, ProgressCallback } from './types.js';

/** Relative path of the CLI-managed server dir inside a project (design 06: "the CLI's managed directory"). */
export const MANAGED_SERVER_REL_DIR = path.join('.ai-game-dev', 'server');

/** Absolute path of the CLI-managed server directory for a project. */
export function getManagedServerDir(projectPath: string): string {
  return path.join(projectPath, MANAGED_SERVER_REL_DIR);
}

/** Absolute path of the managed server executable for a project (the file the `configure` proxy spawns). */
export function getManagedServerExecutablePath(
  projectPath: string,
  platform: NodeJS.Platform = process.platform,
): string {
  return path.join(getManagedServerDir(projectPath), serverExecutableFileName(platform));
}

/**
 * Install the shared `gamedev-mcp-server` binary into the CLI-managed directory
 * `<project>/.ai-game-dev/server/` — the `install-plugin --with-server` flow (D12).
 *
 *  1. Download path (default): resolve the host RID, download the RID-matched
 *     `gamedev-mcp-server-<rid>.zip` from github.com only (fail-closed host
 *     trust), download the release `SHA256SUMS`, and VERIFY the zip's SHA256
 *     against the manifest BEFORE extraction (fail-closed — any non-`verified`
 *     verdict aborts and leaves the project untouched). Then extract + swap.
 *  2. Local path (`--server-source`): copy a trusted local server dir / `.zip`
 *     into the managed dir with no network + no checksum (the local artifact is
 *     trusted). Offline / dev / CI.
 *
 * Library-safe: no stdout noise, no `process.exit`, no throws past the boundary;
 * returns a `{ kind }` union. Materializes into a temp sibling and swaps only on
 * full success, so a partial download/extract is rolled back.
 */
export async function installServer(opts: InstallServerOptions): Promise<InstallServerResult> {
  const warnings: string[] = [];
  const platform = process.platform;

  try {
    const projectPath = path.resolve(opts.godotProjectPath);
    if (!fs.existsSync(projectPath)) {
      throw new Error(`Project path does not exist: ${projectPath}`);
    }
    const serverDir = getManagedServerDir(projectPath);

    if (opts.source !== undefined && opts.source !== '') {
      return installFromLocal(projectPath, serverDir, opts.source, platform, warnings, opts.onProgress);
    }

    return await installFromDownload(projectPath, serverDir, platform, warnings, opts);
  } catch (err: unknown) {
    return {
      kind: 'failure',
      success: false,
      warnings,
      error: err instanceof Error ? err : new Error(String(err)),
    };
  }
}

async function installFromDownload(
  projectPath: string,
  serverDir: string,
  platform: NodeJS.Platform,
  warnings: string[],
  opts: InstallServerOptions,
): Promise<InstallServerResult> {
  const rid = (opts.rid ?? detectHostRid()).trim();
  if (rid.includes('unknown')) {
    throw new Error(
      `Cannot detect a supported host RID (got "${rid}"). Use --server-source <path> to install a server binary manually.`,
    );
  }
  const version = (opts.version ?? DEFAULT_SERVER_VERSION).trim();
  if (version === '') {
    throw new Error('No server version to download. Pass --server-version <x.y.z> or --server-source <path>.');
  }

  const url = serverDownloadUrl(version, rid);
  assertTrustedServerUrl(url);
  const assetName = serverAssetName(rid);

  const fetchImpl = opts.fetchImpl ?? globalThis.fetch;

  emitProgress(opts.onProgress, {
    phase: 'server-downloading',
    message: `Downloading ${assetName} (${version}) from ${url}`,
    url,
  });
  const zipResponse = await fetchImpl(url);
  if (!zipResponse.ok) {
    throw new Error(
      `Failed to download server ${version} for ${rid}: HTTP ${zipResponse.status} ${zipResponse.statusText} from ${url}. ` +
        `Verify the release v${version} exists with a ${assetName} asset, or use --server-source <path>.`,
    );
  }
  const archive = Buffer.from(await zipResponse.arrayBuffer());

  // Fail-closed integrity: download SHA256SUMS + verify BEFORE extraction.
  const sumsUrl = sha256SumsUrl(version);
  assertTrustedServerUrl(sumsUrl);
  emitProgress(opts.onProgress, { phase: 'server-verifying', message: `Verifying against ${sumsUrl}` });
  const sumsResponse = await fetchImpl(sumsUrl);
  if (!sumsResponse.ok) {
    throw new Error(
      `Refusing to install an unverified server: could not download ${sumsUrl} (HTTP ${sumsResponse.status}). ` +
        `The release must publish a SHA256SUMS manifest.`,
    );
  }
  const sumsText = await sumsResponse.text();
  const actualDigest = sha256Hex(archive);
  const verdict = verifyZipChecksum(sumsText, assetName, actualDigest);
  if (verdict !== 'verified') {
    throw new Error(
      `Refusing to install server ${version} for ${rid}: ${checksumFailureReason(verdict, assetName)} (fail-closed).`,
    );
  }

  emitProgress(opts.onProgress, { phase: 'server-extracting', message: 'Extracting verified server zip' });
  const entries = parseZip(archive);
  stageThenSwap(projectPath, serverDir, (staging) => extractServerEntries(entries, staging));

  const executablePath = resolveExecutable(serverDir, platform);
  if (executablePath === null) {
    throw new Error(
      `Server zip extracted but no ${serverExecutableFileName(platform)} was found under ${serverDir} — the archive is not a valid server release.`,
    );
  }
  makeExecutable(executablePath, platform);

  emitProgress(opts.onProgress, {
    phase: 'server-materialized',
    message: `Downloaded + verified server to ${serverDir}`,
    serverDir,
    source: 'download',
  });

  return {
    kind: 'success',
    success: true,
    source: 'download',
    rid,
    version,
    serverDir,
    executablePath,
    downloadUrl: url,
    warnings,
  };
}

function installFromLocal(
  projectPath: string,
  serverDir: string,
  source: string,
  platform: NodeJS.Platform,
  warnings: string[],
  onProgress?: ProgressCallback,
): InstallServerResult {
  const resolved = path.resolve(source);
  if (!fs.existsSync(resolved)) {
    throw new Error(`--server-source ${resolved} does not exist.`);
  }

  emitProgress(onProgress, { phase: 'server-extracting', message: `Installing server from ${resolved}` });

  const stat = fs.statSync(resolved);
  if (stat.isFile() && resolved.toLowerCase().endsWith('.zip')) {
    const entries = parseZip(fs.readFileSync(resolved));
    stageThenSwap(projectPath, serverDir, (staging) => extractServerEntries(entries, staging));
  } else if (stat.isDirectory()) {
    stageThenSwap(projectPath, serverDir, (staging) => {
      fs.cpSync(resolved, staging, { recursive: true });
    });
  } else {
    throw new Error(`--server-source ${resolved} must be a directory or a .zip archive.`);
  }

  const executablePath = resolveExecutable(serverDir, platform);
  if (executablePath === null) {
    throw new Error(
      `--server-source ${resolved} did not yield a ${serverExecutableFileName(platform)} under ${serverDir}.`,
    );
  }
  makeExecutable(executablePath, platform);

  emitProgress(onProgress, {
    phase: 'server-materialized',
    message: `Copied server from ${resolved}`,
    serverDir,
    source: 'local',
  });

  return {
    kind: 'success',
    success: true,
    source: 'local',
    rid: detectHostRid(),
    version: '',
    serverDir,
    executablePath,
    warnings,
  };
}

/**
 * Write every zip entry into `staging` (no `addons/` prefix stripping — the
 * server zip expands to the binary + deps at the archive root). Path-safety:
 * reject zip-slip `../` traversal escaping the staging dir.
 */
function extractServerEntries(entries: ZipEntry[], staging: string): void {
  for (const entry of entries) {
    if (entry.path === '') continue;
    const target = path.resolve(staging, entry.path);
    if (target !== staging && !target.startsWith(staging + path.sep)) {
      throw new Error(`Refusing to extract entry escaping the server dir: ${entry.path}`);
    }
    if (entry.isDirectory) {
      fs.mkdirSync(target, { recursive: true });
      continue;
    }
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, entry.bytes);
  }
}

/** Locate the server executable at the managed-dir root, else one level down. Null when absent. */
function resolveExecutable(serverDir: string, platform: NodeJS.Platform): string | null {
  const exeName = serverExecutableFileName(platform);
  const atRoot = path.join(serverDir, exeName);
  if (fs.existsSync(atRoot)) return atRoot;
  // Self-contained builds sometimes nest under a single top dir; search one level.
  let children: string[];
  try {
    children = fs.readdirSync(serverDir);
  } catch {
    return null;
  }
  for (const child of children) {
    const candidate = path.join(serverDir, child, exeName);
    if (fs.existsSync(candidate)) return candidate;
  }
  return null;
}

/** Mark the executable runnable on POSIX (no-op on Windows). Best-effort. */
function makeExecutable(executablePath: string, platform: NodeJS.Platform): void {
  if (platform === 'win32') return;
  try {
    fs.chmodSync(executablePath, 0o755);
  } catch {
    // Best-effort; never fail the install over a chmod.
  }
}

/**
 * Fill a fresh staging sibling, then atomically swap it into `serverDir`. A mid-
 * fill failure discards staging and leaves any existing managed server untouched.
 */
function stageThenSwap(projectPath: string, serverDir: string, fill: (staging: string) => void): void {
  const serverReal = path.resolve(serverDir);
  const staging = path.resolve(projectPath, `.gamedev-server-staging-${process.pid}-${Date.now()}`);
  fs.rmSync(staging, { recursive: true, force: true });
  fs.mkdirSync(staging, { recursive: true });
  try {
    fill(staging);
    fs.rmSync(serverReal, { recursive: true, force: true });
    fs.mkdirSync(path.dirname(serverReal), { recursive: true });
    fs.renameSync(staging, serverReal);
  } finally {
    fs.rmSync(staging, { recursive: true, force: true });
  }
}
