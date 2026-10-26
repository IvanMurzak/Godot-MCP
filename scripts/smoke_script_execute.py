#!/usr/bin/env python3
"""
Live smoke of `script-execute` (Code-Exec) against a running Godot editor with this addon (default toolFormat Rql).

Checks: an expression, a game type from the project's own C# assembly (with auto-import of its namespace), Godot
API, GD.Print / GD.PushWarning (Godot 4.5+ only: the engine logger) and Console captured into the result, the
plugin's live statics with one copy of the game assembly and GodotSharp, `await`, full-source mode, a compile error at
the snippet's own line, a runtime error, `include` of a helper, the output caps (a huge value or print flood comes back cut),
Godot structs / lazy sequences / a freed object as results, and that snippet assemblies unload (0 live contexts
after GC).

It does NOT launch anything (or use `godot-cli exec`, which does):

    GODOT_MCP_CONNECTION_MODE=Custom GODOT_MCP_HOST=http://localhost:8080 GODOT_MCP_START_SERVER=true \\
        godot --headless --path <project> --editor &
    python scripts/smoke_script_execute.py --host http://localhost:8080 --project <project>

`--project` gets a fixture game type (`res://SmokeExec/SmokeExecScore.cs`, namespace SmokeExec) and a snippet helper
(`.ai-game-dev/snippets/smoke/SmokeHelper.cs`) written before the run: build the project after the first run (or run
it once, build, start the editor, run again). `--prepare` only writes them (CI runs it before its C# build). Use a
disposable project. Exit code 0 = every case passed. Stdlib only.
"""
from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys
import time
import urllib.error
import urllib.request

GAME_TYPE = "namespace SmokeExec\n{\n    public static class SmokeExecScore\n    {\n        public static int Value => 77;\n    }\n}\n"
HELPER = "public static class SmokeHelper\n{\n    public static int Twice(int v) => v * 2;\n}\n"
FAILURES: list[str] = []


def execute(host: str, args: dict, timeout: float = 180) -> tuple[bool, str]:
    """(succeeded, the tool's text or error)."""
    req = urllib.request.Request(f"{host}/api/tools/script-execute", data=json.dumps(args).encode(),
                                 headers={"Content-Type": "application/json"}, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            body = json.loads(resp.read().decode() or "{}")
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            body = json.loads(raw or "{}")
        except json.JSONDecodeError:
            body = {"error": raw or str(e)}
    if "error" in body:
        return False, str(body["error"])
    if "structured" in body:
        return True, json.dumps(body["structured"])
    return True, "\n".join(c.get("text", "") for c in body.get("content") or [])


def wait_ready(host: str, timeout: float) -> bool:
    """Poll a trivial snippet until the editor answers (each probe bounded by the time left, at most 30 s)."""
    deadline = time.monotonic() + timeout
    while (left := deadline - time.monotonic()) > 0:
        try:
            if execute(host, {"csharpCode": "0"}, timeout=min(30, left))[0]:
                return True
        except (OSError, ValueError):  # server not reachable yet, the probe timed out, or a non-JSON reply
            pass
        time.sleep(1)
    return False


def case(host: str, name: str, args: dict, ok: bool, *expected: str) -> str:
    succeeded, text = execute(host, args)
    missing = [e for e in expected if e not in text]
    status = "PASS" if succeeded == ok and not missing else "FAIL"
    print(f"{status} {name}: {text.splitlines()[0] if text else ''}")
    if status == "FAIL":
        FAILURES.append(f"{name}: succeeded={succeeded} (want {ok}), missing {missing}; got:\n{text}")
    return text


def check_size(name: str, text: str, limit: int) -> None:
    if len(text) > limit:
        print(f"FAIL {name}: {len(text)} chars returned (limit {limit})")
        FAILURES.append(f"{name}: {len(text)} chars returned, more than {limit}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--host", help="MCP server base URL, e.g. http://localhost:8080 (required unless --prepare)")
    parser.add_argument("--project", required=True, type=pathlib.Path, help="the Godot project the editor has open")
    parser.add_argument("--prepare", action="store_true", help="only write the fixtures into --project, then exit")
    parser.add_argument("--wait", type=float, default=0, help="seconds to wait for the editor to answer script-execute")
    args = parser.parse_args()

    game = args.project / "SmokeExec" / "SmokeExecScore.cs"
    helper = args.project / ".ai-game-dev" / "snippets" / "smoke" / "SmokeHelper.cs"
    for path, text in ((game, GAME_TYPE), (helper, HELPER)):
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists():
            path.write_text(text, encoding="utf-8")
    if args.prepare:
        return 0
    if not args.host:
        parser.error("--host is required unless --prepare")
    host = args.host.rstrip("/")

    if args.wait > 0 and not wait_ready(host, args.wait):
        print(f"script-execute never answered on {host} within {args.wait:.0f}s", flush=True)
        return 1

    case(host, "expression", {"csharpCode": "1 + 2"}, True, "System.Int32: 3")
    case(host, "game type + auto-import", {"csharpCode": "SmokeExecScore.Value"}, True, "System.Int32: 77")
    version = case(host, "godot api", {"csharpCode": 'Engine.GetVersionInfo()["string"].AsString()'}, True,
                   "System.String: ", "4.")
    # GD.Print / GD.PushWarning reach `logs` only through the engine logger (OS.AddLogger, Godot 4.5+); older engines
    # capture Console output only (GodotPrintLogCapture), so there the snippet must just succeed.
    match = re.search(r"System\.String: (\d+)\.(\d+)", version)
    engine_logger = not match or (int(match[1]), int(match[2])) >= (4, 5)
    case(host, "GD.Print captured" if engine_logger else "GD.Print (not captured before Godot 4.5)",
         {"csharpCode": 'GD.Print("smoke-print"); GD.PushWarning("smoke-warn"); 5'}, True,
         "System.Int32: 5", *(("logs:", "smoke-print") if engine_logger else ()))
    case(host, "Console captured", {"csharpCode": 'Console.WriteLine("smoke-console"); 6'}, True,
         "System.Int32: 6", "logs:", "smoke-console")
    # The addon compiles into the game assembly: a live static set by the plugin, and one copy of it and of GodotSharp
    # (assemblies of contexts still unloading after a hot reload are not counted).
    case(host, "live statics, no duplicate assemblies", {"csharpCode":
         "using System.Runtime.Loader;\n"
         "var names = new[] { typeof(SmokeExecScore).Assembly.GetName().Name, \"GodotSharp\" };\n"
         "var live = AppDomain.CurrentDomain.GetAssemblies().Where(a => AssemblyLoadContext.All.Contains(AssemblyLoadContext.GetLoadContext(a)));\n"
         "var copies = names.Select(n => live.Count(a => a.GetName().Name == n));\n"
         "$\"{com.IvanMurzak.Godot.MCP.MainThreadDispatch.MainThreadDispatcher.Instance != null}/{string.Join(\",\", copies)}\""},
         True, "True/1,1")
    case(host, "await", {"csharpCode": "await Task.Delay(20);\nEngine.GetProcessFrames() > 0"}, True, "System.Boolean: true")
    case(host, "full-source mode", {"csharpCode": "public class Script { public static int Main() { return 7; } }"}, True, "System.Int32: 7")
    case(host, "compile error line", {"csharpCode": "var a = 1;\nvar b = a +;\nb"}, False, "Compilation failed", "snippet.cs(2,")
    case(host, "runtime error", {"csharpCode": 'throw new InvalidOperationException("smoke-boom");'}, False,
         "Execution failed (Runtime)", "smoke-boom", "snippet.cs:line 1")
    case(host, "include", {"csharpCode": "SmokeHelper.Twice(21)", "include": ["smoke"]}, True, "System.Int32: 42")
    # Code-Exec 0.3: includes stay inside the snippets folder; a bad `using` is reported as itself.
    case(host, "include outside the snippets folder is refused", {"csharpCode": "1", "include": ["../../project.godot"]},
         False, "InvalidRequest")
    case(host, "bad using is named", {"csharpCode": "1", "usings": ["No.Such.Smoke"]}, False, "using 'No.Such.Smoke'")
    # Output caps: a huge value or flood of prints reaches the agent cut (20000 chars each), with a note.
    text = case(host, "huge value is cut", {"csharpCode": "new string('y', 2_000_000)"}, True, "result cut:")
    check_size("huge value is cut", text, 25_000)
    text = case(host, "flood of prints is cut", {"csharpCode": "for (int i = 0; i < 20000; i++) Console.WriteLine(\"line \" + i + \" \" + new string('x', 50));\n1"},
                True, "System.Int32: 1", "log line(s) left out")
    check_size("flood of prints is cut", text, 25_000)
    # Values the reflector cannot show field by field: Godot structs as text, lazy sequences enumerated, a freed object.
    case(host, "Basis as text", {"csharpCode": "Basis.Identity"}, True, "(1, 0, 0)")
    case(host, "StringName as text", {"csharpCode": "new StringName(\"smoke-name\")"}, True, "smoke-name")
    case(host, "lazy sequence enumerated", {"csharpCode": "Enumerable.Range(0, 5).Select(i => i * i)"}, True, "16")
    case(host, "freed object", {"csharpCode": "var n = new Node(); n.Free(); n"}, True, "freed")
    for i in range(20):
        execute(host, {"csharpCode": f"var v{i} = {i};\nv{i} + 1"})
    case(host, "unload (0 live snippet contexts besides this one)", {"csharpCode":
         "GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();\n"
         "System.Runtime.Loader.AssemblyLoadContext.All.Count(c => c.Name != null && c.Name.StartsWith(\"CodeExec:\")) - 1"},
         True, "System.Int32: 0")

    if FAILURES:
        print(f"\n{len(FAILURES)} failed:\n" + "\n\n".join(FAILURES))
        return 1
    print("\nall script-execute cases passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
