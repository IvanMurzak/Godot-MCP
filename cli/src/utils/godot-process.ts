import { execFileSync, execSync } from 'child_process';
import { platform } from 'os';
import * as path from 'path';
import { verbose } from './ui.js';

export interface GodotProcess {
  pid: number;
  projectPath: string;
  commandLine: string;
}

/**
 * Extract the `--path <project>` value from a Godot editor command line.
 * Quote-aware: a double-quoted path (Windows CIM `CommandLine` shape for paths
 * with spaces) is consumed whole, otherwise the first whitespace-delimited
 * token is used. Returns the resolved absolute path, or null when absent.
 */
function extractProjectPath(commandLine: string): string | null {
  const quoted = commandLine.match(/--path\s+"([^"]+)"/i);
  if (quoted) return path.resolve(quoted[1].trim());
  const unquoted = commandLine.match(/--path\s+(\S+)/i);
  if (unquoted) return path.resolve(unquoted[1].trim());
  return null;
}

/**
 * Check if a Godot editor process is running with the given project path.
 * Returns process info if found, null otherwise.
 */
export function findGodotProcess(projectPath: string): GodotProcess | null {
  const isWindows = platform() === 'win32';
  const resolvedTarget = path.resolve(projectPath);
  const normalizedTarget = isWindows ? resolvedTarget.toLowerCase() : resolvedTarget;
  const processes = listGodotProcesses();

  for (const proc of processes) {
    const normalizedProc = isWindows ? proc.projectPath.toLowerCase() : proc.projectPath;
    if (normalizedProc === normalizedTarget) {
      verbose(`Found Godot process PID ${proc.pid} with project: ${proc.projectPath}`);
      return proc;
    }
  }

  verbose(`No Godot process found for project: ${projectPath}`);
  return null;
}

/**
 * List all running Godot editor processes with their project paths. Matches
 * any process whose name contains "godot" (covers `Godot.exe`, `Godot_mono.exe`,
 * `godot_mono_console.exe`, the macOS `Godot` binary, and Linux extracted
 * binaries) and that carries a `--path` argument.
 */
function listGodotProcesses(): GodotProcess[] {
  const os = platform();
  const results: GodotProcess[] = [];

  try {
    if (os === 'win32') {
      const psCommand =
        "Get-CimInstance Win32_Process -Filter \"Name LIKE '%godot%'\" | Select-Object ProcessId,CommandLine | ForEach-Object { $_.ProcessId.ToString() + '|||' + $_.CommandLine }";
      const output = execFileSync(
        'powershell.exe',
        ['-NoProfile', '-Command', psCommand],
        { encoding: 'utf-8', timeout: 10000, stdio: ['pipe', 'pipe', 'pipe'] },
      );
      const lines = output.split('\n').filter((l) => l.trim().length > 0);

      for (const line of lines) {
        const sepIdx = line.indexOf('|||');
        if (sepIdx === -1) continue;

        const pid = parseInt(line.substring(0, sepIdx).trim(), 10);
        const commandLine = line.substring(sepIdx + 3).trim();
        if (!Number.isFinite(pid) || pid === 0) continue;
        if (!/--path/i.test(commandLine)) continue;

        const projectPath = extractProjectPath(commandLine);
        if (projectPath) {
          results.push({ pid, projectPath, commandLine });
        }
      }
    } else {
      const output = execSync(
        "ps -eo pid,args | grep -i '[g]odot' || true",
        { encoding: 'utf-8', timeout: 5000, stdio: ['pipe', 'pipe', 'pipe'] },
      );
      const lines = output.split('\n').filter((l) => l.trim().length > 0);

      for (const line of lines) {
        const match = line.trim().match(/^(\d+)\s+(.*)$/);
        if (!match) continue;

        const pid = parseInt(match[1], 10);
        const commandLine = match[2];
        if (!/--path/i.test(commandLine)) continue;

        const projectPath = extractProjectPath(commandLine);
        if (projectPath) {
          results.push({ pid, projectPath, commandLine });
        }
      }
    }
  } catch (err) {
    verbose(`Failed to list Godot processes: ${err instanceof Error ? err.message : String(err)}`);
  }

  verbose(`Found ${results.length} Godot process(es)`);
  return results;
}
