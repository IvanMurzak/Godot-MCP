// Godot / MSBuild log parsing. Godot's headless exit code is not a verdict (a GDScript parse error during --import, or an
// addon that fails to load, still exits 0), so the headless commands judge a run from its log: error lines, markers
// the run must print, and the report files it writes. Pure functions, no I/O.

export type DiagnosticSeverity = 'error' | 'warning';

/** `gdscript`: GDScript parse/load errors; `csharp`: compiler diagnostics; `engine`: any other engine line. */
export type DiagnosticSource = 'gdscript' | 'csharp' | 'engine';

export interface Diagnostic {
  severity: DiagnosticSeverity;
  source: DiagnosticSource;
  message: string;
  /** `res://…` for Godot lines, the file path MSBuild printed for C#. */
  file?: string;
  line?: number;
  column?: number;
  /** Compiler code, e.g. `CS1002`. */
  code?: string;
  /** `load`: the script did not parse/compile/load; `runtime`: it loaded and then reported an error. */
  phase?: 'load' | 'runtime';
}

/** The first line every Godot run prints: proof the engine started at all. */
export const GODOT_BANNER_RE = /^Godot Engine v\d+\.\d+/m;

/** Printed by the Godot-MCP addon when its EditorPlugin entered the editor. */
export const ADDON_LOADED_MARKER = '[Godot-MCP] plugin loaded';

/** Marker prefix of the scripts godot-cli runs inside Godot (`check`). */
export const CLI_MARKER = '[godot-cli] ';

// eslint-disable-next-line no-control-regex
const ANSI_RE = /\x1B\[[0-9;]*[A-Za-z]/g;

export function stripAnsi(text: string): string {
  return text.replace(ANSI_RE, '');
}

const HEADER_RE = /^(USER SCRIPT ERROR|USER ERROR|USER WARNING|SCRIPT ERROR|SCRIPT WARNING|ERROR|WARNING): (.*)$/;
const AT_RE = /^\s+at: .*?\(((?:res|user):\/\/[^():]+|[A-Za-z]:[\\/][^():]+|\/[^():]+):(\d+)\)\s*$/;
const WARNING_AS_ERROR = '(Warning treated as error.)';
const FAILED_LOAD_RE = /Failed to load script "([^"]+)"/;
/** Engine errors that mean a script or an addon did not load (the rest are runtime noise). */
const LOAD_FAILURE_RE = /Failed to load script|Unable to load addon script|Cannot load (?:C#|GDScript)|Failed to instantiate|Could not find type|Unable to load \.NET|\.NET: Failed/i;

/**
 * Diagnostics in a Godot log: `SCRIPT ERROR:` (GDScript parse/compile, with `res://file:line` from the following
 * `at:` line; "Warning treated as error" rows become warnings), `ERROR:` / `WARNING:` lines. Engine errors that mean a
 * script or addon failed to load are `gdscript` errors; other engine errors are `engine` and do not fail a run.
 */
export function parseGodotLog(text: string): Diagnostic[] {
  const lines = stripAnsi(text).split(/\r?\n/);
  const diagnostics: Diagnostic[] = [];
  // Files a SCRIPT ERROR row already reported (with its line): their "Failed to load script" follow-up is dropped.
  const reportedFiles = new Set<string>();

  for (let i = 0; i < lines.length; i++) {
    const header = HEADER_RE.exec(lines[i]);
    if (!header) continue;
    const kind = header[1];
    let message = header[2].trim();
    let file: string | undefined;
    let line: number | undefined;
    const at = i + 1 < lines.length ? AT_RE.exec(lines[i + 1]) : null;
    if (at) {
      file = at[1];
      line = Number(at[2]);
    }

    if (kind === 'SCRIPT ERROR' || kind === 'USER SCRIPT ERROR') {
      const asWarning = message.endsWith(WARNING_AS_ERROR);
      if (asWarning) message = message.slice(0, -WARNING_AS_ERROR.length).trim();
      const loadError = /^(Parse|Compile) Error: /.test(message);
      message = message.replace(/^(Parse|Compile) Error: /, '');
      if (file && loadError) reportedFiles.add(file);
      diagnostics.push({
        severity: asWarning ? 'warning' : 'error',
        source: 'gdscript',
        message,
        ...(file ? { file } : {}),
        ...(line ? { line } : {}),
        phase: loadError ? 'load' : 'runtime',
      });
      continue;
    }

    if (kind.endsWith('WARNING')) {
      diagnostics.push({ severity: 'warning', source: isScriptFile(file) && file!.endsWith('.gd') ? 'gdscript' : 'engine', message, ...(file ? { file } : {}), ...(line ? { line } : {}) });
      continue;
    }

    // ERROR / USER ERROR
    const failedLoad = FAILED_LOAD_RE.exec(message);
    if (failedLoad) {
      // Already reported with its line by the SCRIPT ERROR above it.
      if (reportedFiles.has(failedLoad[1])) continue;
      diagnostics.push({ severity: 'error', source: 'gdscript', message, file: failedLoad[1], phase: 'load' });
      continue;
    }
    const loadFailure = LOAD_FAILURE_RE.test(message);
    const scriptLevel = loadFailure || (file !== undefined && file.endsWith('.gd'));
    diagnostics.push({
      severity: 'error',
      source: scriptLevel ? 'gdscript' : 'engine',
      message,
      ...(file ? { file } : {}),
      ...(line ? { line } : {}),
      ...(scriptLevel ? { phase: loadFailure ? 'load' : 'runtime' } : {}),
    });
  }
  return diagnostics;
}

function isScriptFile(file: string | undefined): boolean {
  return file !== undefined && /\.(gd|cs)$/i.test(file);
}

const MSBUILD_LOCATED_RE = /^\s*(.+?)\((\d+)(?:,(\d+))?(?:,\d+,\d+)?\)\s*:\s*(error|warning)\s+([A-Z]+\d+)\s*:\s*(.*?)(?:\s+\[[^\]]+\])?\s*$/;
const MSBUILD_UNLOCATED_RE = /^\s*(.+?)\s*:\s*(error|warning)\s+([A-Z]+\d+)\s*:\s*(.*?)(?:\s+\[[^\]]+\])?\s*$/;

/**
 * Diagnostics in `dotnet build` output: `file(line,col): error CS1002: … [proj]` and unlocated
 * `MSBUILD : error MSB1009: …`. MSBuild repeats each one in its summary; duplicates are dropped.
 */
export function parseMsbuildOutput(text: string): Diagnostic[] {
  const seen = new Set<string>();
  const diagnostics: Diagnostic[] = [];
  for (const raw of stripAnsi(text).split(/\r?\n/)) {
    let d: Diagnostic | undefined;
    const located = MSBUILD_LOCATED_RE.exec(raw);
    if (located) {
      d = {
        severity: located[4] as DiagnosticSeverity,
        source: 'csharp',
        message: located[6],
        file: located[1].trim(),
        line: Number(located[2]),
        ...(located[3] ? { column: Number(located[3]) } : {}),
        code: located[5],
      };
    } else {
      const unlocated = MSBUILD_UNLOCATED_RE.exec(raw);
      if (unlocated) d = { severity: unlocated[2] as DiagnosticSeverity, source: 'csharp', message: unlocated[4], code: unlocated[3] };
    }
    if (!d) continue;
    const key = `${d.severity}|${d.file ?? ''}|${d.line ?? ''}|${d.column ?? ''}|${d.code}|${d.message}`;
    if (seen.has(key)) continue;
    seen.add(key);
    diagnostics.push(d);
  }
  return diagnostics;
}

/** `file:line: error: message` (C#: `file(line,col): error CS0000: message`). */
export function formatDiagnostic(d: Diagnostic): string {
  if (d.source === 'csharp' && d.file) {
    const loc = d.line !== undefined ? `(${d.line}${d.column !== undefined ? `,${d.column}` : ''})` : '';
    return `${d.file}${loc}: ${d.severity}${d.code ? ` ${d.code}` : ''}: ${d.message}`;
  }
  const loc = d.file ? `${d.file}${d.line !== undefined ? `:${d.line}` : ''}: ` : '';
  return `${loc}${d.severity}${d.code ? ` ${d.code}` : ''}: ${d.message}`;
}

/** The lines of `text` that start with the godot-cli marker, without it. */
export function cliMarkerLines(text: string): string[] {
  return stripAnsi(text)
    .split(/\r?\n/)
    .filter((l) => l.startsWith(CLI_MARKER))
    .map((l) => l.slice(CLI_MARKER.length).trim());
}
