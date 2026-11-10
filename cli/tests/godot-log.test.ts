import { describe, it, expect } from 'vitest';
import {
  cliMarkerLines,
  formatDiagnostic,
  parseGodotLog,
  parseMsbuildOutput,
  stripAnsi,
} from '../src/lib/godot-log.js';

// Shapes copied from real Godot 4.5.1 mono headless runs.
const PARSE_ERROR_LOG = `Godot Engine v4.5.1.stable.mono.official.f62fdbde1 - https://godotengine.org

SCRIPT ERROR: Parse Error: Expected expression after "+" operator.
   at: GDScript::reload (res://zz_broken.gd:4)
   GDScript backtrace (most recent call first):
       [0] _load_ok (res://.godot/godot-cli/check.gd:24)
ERROR: Failed to load script "res://zz_broken.gd" with error "Parse error".
   at: load (modules/gdscript/gdscript.cpp:3041)
[godot-cli] script-failed res://zz_broken.gd
[godot-cli] check-done files=2 failed=1 warned=0
`;

describe('parseGodotLog', () => {
  it('reads a GDScript parse error with res://file:line and drops the redundant "Failed to load" line', () => {
    const d = parseGodotLog(PARSE_ERROR_LOG);
    expect(d).toEqual([
      { severity: 'error', source: 'gdscript', message: 'Expected expression after "+" operator.', file: 'res://zz_broken.gd', line: 4, phase: 'load' },
    ]);
  });

  it('turns "(Warning treated as error.)" rows into warnings', () => {
    const d = parseGodotLog(
      'SCRIPT ERROR: Parse Error: The local variable "unused" is declared but never used. (Warning treated as error.)\n' +
        '   at: GDScript::reload (res://zz_warn.gd:4)\n' +
        'ERROR: Failed to load script "res://zz_warn.gd" with error "Parse error".\n' +
        '   at: load (modules/gdscript/gdscript.cpp:3041)\n',
    );
    expect(d).toHaveLength(1);
    expect(d[0]).toMatchObject({ severity: 'warning', source: 'gdscript', file: 'res://zz_warn.gd', line: 4 });
    expect(d[0].message).toBe('The local variable "unused" is declared but never used.');
  });

  it('classifies C# runtime warnings/errors pushed from .cs files as engine messages', () => {
    const d = parseGodotLog(
      'WARNING: [McpPlugin] could not acquire gate\n' +
        '   at: void com.Foo+<>c__DisplayClass3_0.<Route>b__1() (res://addons/godot_mcp/Runtime/Connection/GodotMcpLog.cs:76)\n' +
        'ERROR: Condition "!success" is true. Returning: ERR_FILE_CORRUPT\n' +
        '   at: load (core/io/image.cpp:10)\n',
    );
    expect(d.map((x) => [x.severity, x.source])).toEqual([['warning', 'engine'], ['error', 'engine']]);
    expect(d[0].file).toBe('res://addons/godot_mcp/Runtime/Connection/GodotMcpLog.cs');
  });

  it('treats addon load failures and errors in .gd files as script errors', () => {
    const d = parseGodotLog(
      "ERROR: Unable to load addon script from path: 'res://addons/foo/plugin.gd'.\n" +
        'USER ERROR: boom\n   at: _ready (res://main.gd:12)\n',
    );
    expect(d[0]).toMatchObject({ severity: 'error', source: 'gdscript', phase: 'load' });
    expect(d[1]).toMatchObject({ severity: 'error', source: 'gdscript', phase: 'runtime', file: 'res://main.gd', line: 12 });
  });

  it('reads absolute script paths and their failed-load follow-up (run-script outside the project)', () => {
    const d = parseGodotLog(
      'SCRIPT ERROR: Parse Error: Expected expression.\n   at: GDScript::reload (C:/tmp/broken.gd:4)\n' +
        'ERROR: Failed to load script "C:/tmp/broken.gd" with error "Parse error".\n   at: load (modules/gdscript/gdscript.cpp:3041)\n',
    );
    expect(d).toHaveLength(1);
    expect(d[0]).toMatchObject({ file: 'C:/tmp/broken.gd', line: 4 });
  });

  it('strips ANSI colour codes', () => {
    expect(stripAnsi('\x1b[90m\x1b[1mdone\x1b[22m\x1b[39m')).toBe('done');
    expect(parseGodotLog('\x1b[91mERROR: x\x1b[0m\n')).toHaveLength(1);
  });
});

describe('parseMsbuildOutput', () => {
  it('reads located errors/warnings once (MSBuild repeats them in its summary)', () => {
    const line = 'C:\\p\\ZzBroken.cs(4,33): error CS1002: ; expected [C:\\p\\Game.csproj]';
    const d = parseMsbuildOutput(`${line}\n  ${line}\nC:\\p\\A.cs(1,2): warning CS0168: unused [C:\\p\\Game.csproj]\n`);
    expect(d).toEqual([
      { severity: 'error', source: 'csharp', message: '; expected', file: 'C:\\p\\ZzBroken.cs', line: 4, column: 33, code: 'CS1002' },
      { severity: 'warning', source: 'csharp', message: 'unused', file: 'C:\\p\\A.cs', line: 1, column: 2, code: 'CS0168' },
    ]);
  });

  it('reads unlocated diagnostics', () => {
    expect(parseMsbuildOutput('MSBUILD : error MSB1009: Project file does not exist.')).toEqual([
      { severity: 'error', source: 'csharp', message: 'Project file does not exist.', code: 'MSB1009' },
    ]);
  });

  it('ignores ordinary build lines', () => {
    expect(parseMsbuildOutput('Build succeeded.\n    0 Warning(s)\n    0 Error(s)\n')).toEqual([]);
  });
});

describe('formatDiagnostic / cliMarkerLines', () => {
  it('formats Godot and C# locations', () => {
    expect(formatDiagnostic({ severity: 'error', source: 'gdscript', message: 'bad', file: 'res://a.gd', line: 3 })).toBe('res://a.gd:3: error: bad');
    expect(formatDiagnostic({ severity: 'error', source: 'csharp', message: '; expected', file: 'A.cs', line: 4, column: 33, code: 'CS1002' }))
      .toBe('A.cs(4,33): error CS1002: ; expected');
    expect(formatDiagnostic({ severity: 'warning', source: 'engine', message: 'x' })).toBe('warning: x');
  });

  it('extracts the [godot-cli] marker lines', () => {
    expect(cliMarkerLines(PARSE_ERROR_LOG)).toEqual(['script-failed res://zz_broken.gd', 'check-done files=2 failed=1 warned=0']);
  });
});
