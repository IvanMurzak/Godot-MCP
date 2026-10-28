#!/usr/bin/env python3
"""
Live smoke of C# assembly reloads behind the plugin's reload gate (`script-build` + tool calls in flight) against a
running Godot editor with this addon.

Godot reloads the project assembly ~0.5 s after the built DLL changes, whatever the plugin is doing; a tool call in
flight at that moment used to keep the old assembly alive ("Failed to unload assemblies ... Giving up", plugin offline
until the editor restarts). Each round here writes a new C# class with `script-create`, builds it with `script-build`
(the result arrives after Godot's reload, through the requestId) while other threads keep calling tools, then checks
that the plugin answers again and that the new class is loaded (`script-execute` uses it). A call answered 503 must
carry the gate's retryable message; a call lost with the connection while it goes down is only counted. A broken script must come back as compiler errors without a reload. With
`--editor-log` the editor's output must show no failed unload.

It does NOT launch anything. Start the editor with godot-cli (it sets DOTNET_ThreadPool_ThreadsToKeepAlive=-1, the
dotnet/runtime#135441 mitigation, see README "Known issue"), or by hand with that variable:

    godot-cli open <project> --headless
    DOTNET_ThreadPool_ThreadsToKeepAlive=-1 GODOT_MCP_CONNECTION_MODE=Custom GODOT_MCP_HOST=http://localhost:8080         godot --headless --path <project> --editor &
    python scripts/smoke_reload.py --host http://localhost:8080 --project <project> --rounds 10

`--project` gets `res://SmokeReload/*.cs` files (removed again at the end, followed by one more build). Use a
disposable project. Exit code 0 = every check passed. Stdlib only.
"""
from __future__ import annotations

import argparse
import json
import pathlib
import shutil
import sys
import threading
import time
import urllib.error
import urllib.request

FAILURES: list[str] = []
GATE_REFUSAL = "Retry in a few seconds"
FOLDER = "SmokeReload"


def call(host: str, kind: str, name: str, args: dict, timeout: float = 120) -> tuple[int, bool, str]:
    """(HTTP status, succeeded, the tool's text or error)."""
    req = urllib.request.Request(f"{host}/api/{kind}/{name}", data=json.dumps(args).encode(),
                                 headers={"Content-Type": "application/json"}, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            status, raw = resp.status, resp.read().decode()
    except urllib.error.HTTPError as e:
        status, raw = e.code, e.read().decode()
    except (OSError, ValueError) as e:
        return -1, False, repr(e)
    try:
        body = json.loads(raw or "{}")
    except json.JSONDecodeError:
        body = {"error": raw}
    if "error" in body:
        return status, False, str(body["error"])
    if "structured" in body:
        return status, True, json.dumps(body["structured"])
    return status, True, "\n".join(c.get("text", "") for c in body.get("content") or [])


def tool(host: str, name: str, args: dict, timeout: float = 120) -> tuple[bool, str]:
    _, ok, text = call(host, "tools", name, args, timeout)
    return ok, text


def wait_connected(host: str, timeout: float) -> float | None:
    """Seconds until `ping` answers, or None."""
    start = time.monotonic()
    while (left := timeout - (time.monotonic() - start)) > 0:
        _, ok, text = call(host, "system-tools", "ping", {"message": "reload-smoke"}, timeout=min(10, left))
        if ok and "reload-smoke" in text:
            return time.monotonic() - start
        time.sleep(0.5)
    return None


def check(name: str, ok: bool, detail: str = "") -> bool:
    print(f"{'PASS' if ok else 'FAIL'} {name}{': ' + detail.splitlines()[0] if detail else ''}")
    if not ok:
        FAILURES.append(f"{name}: {detail}")
    return ok


def build(host: str, name: str, *expected: str) -> None:
    """script-build, which answers after the build (and Godot's reload when the DLL changed)."""
    started = time.monotonic()
    _, text = tool(host, "script-build", {}, timeout=300)
    check(f"{name} ({time.monotonic() - started:.1f} s)", all(e in text for e in expected), text)


class Background:
    """Threads calling cheap tools (and creating C# files) for as long as the round lasts."""

    def __init__(self, host: str, workers: int, tag: str):
        self.host, self.tag = host, tag
        self.stop = threading.Event()
        self.results: list[tuple[int, bool, str]] = []
        self.lock = threading.Lock()
        self.threads = [threading.Thread(target=self.run, args=(w,), daemon=True) for w in range(workers)]

    def run(self, worker: int) -> None:
        i = 0
        while not self.stop.is_set():
            i += 1
            if worker == 0 and i <= 5:
                # A C# script-create in flight while the reload starts: the call that used to take the plugin down.
                # (A few per round: each one is compiled by every later build.)
                result = call(self.host, "tools", "script-create", {
                    "scriptPath": f"res://{FOLDER}/Bg{self.tag}_{i}.cs",
                    "content": f"public static class Bg{self.tag}_{i} {{ public const int Value = {i}; }}\n"}, timeout=60)
            else:
                result = call(self.host, "tools", "script-read", {"scriptPath": f"res://{FOLDER}/Round{self.tag}.cs"}, timeout=60)
            with self.lock:
                self.results.append(result)
            time.sleep(0.02)

    def __enter__(self) -> "Background":
        for t in self.threads:
            t.start()
        return self

    def __exit__(self, *_: object) -> None:
        self.stop.set()
        for t in self.threads:
            t.join(timeout=90)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", required=True)
    parser.add_argument("--project", required=True, type=pathlib.Path)
    parser.add_argument("--rounds", type=int, default=10)
    parser.add_argument("--workers", type=int, default=3, help="threads calling tools during each build")
    parser.add_argument("--wait", type=float, default=180, help="seconds to wait for the first connection")
    parser.add_argument("--reconnect", type=float, default=60, help="seconds a round may take to answer again")
    parser.add_argument("--editor-log", type=pathlib.Path, help="the editor's output: must show no failed unload")
    args = parser.parse_args()
    host = args.host.rstrip("/")

    if wait_connected(host, args.wait) is None:
        print(f"FAIL the editor did not answer within {args.wait:.0f} s")
        return 1
    run = str(int(time.time()))[-6:]
    refused = transient = 0
    try:
        for n in range(1, args.rounds + 1):
            tag = f"{run}_{n}"
            ok, text = tool(host, "script-create", {
                "scriptPath": f"res://{FOLDER}/Round{tag}.cs",
                # Round 1 carries a compiler warning: a successful script-build lists it (file, line and code).
                "content": f"public static class Round{tag} {{ public const int Value = {n};"
                           + (" static void W() { int unusedLocal = 1; }" if n == 1 else "") + " }\n"})
            check(f"round {n}: script-create Round{tag}", ok, text)
            with Background(host, args.workers, tag) as background:
                time.sleep(0.3)
                build(host, f"round {n}: script-build reloads", "C# build succeeded", "Godot reloaded the C# assembly",
                      *(("Warnings:", f"Round{tag}.cs", "CS0219") if n == 1 else ()))
                time.sleep(0.5)
            for status, ok, text in background.results:
                if ok:
                    continue
                if status == 503 and GATE_REFUSAL in text:
                    refused += 1
                elif status == 503:
                    check(f"round {n}: a 503 carries the gate's retryable message", False, text)
                else:
                    transient += 1
                    print(f"  note: a call during round {n} failed ({status}): {text[:160]}")
            took = wait_connected(host, args.reconnect)
            check(f"round {n}: plugin answers after the reload", took is not None,
                  f"in {took:.1f} s" if took is not None else f"no answer in {args.reconnect:.0f} s")
            ok, text = tool(host, "script-execute", {"csharpCode": f"return Round{tag}.Value;"})
            check(f"round {n}: the class built this round is loaded", ok and text.strip().endswith(f": {n}"), text)

        ok, text = tool(host, "script-create", {"scriptPath": f"res://{FOLDER}/Broken{run}.cs",
                                                "content": "public class Broken { void F() { int x = } }\n"})
        check("broken script written", ok, text)
        build(host, "script-build reports compiler errors", "C# build failed", "CS1525")
        check("plugin still answers after the failed build", wait_connected(host, args.reconnect) is not None)
    finally:
        # Leave the project as it was: no smoke scripts, and an assembly built without them.
        shutil.rmtree(args.project / FOLDER, ignore_errors=True)
        uid = args.project / f"{FOLDER}.uid"
        if uid.exists():
            uid.unlink()
        if wait_connected(host, args.reconnect) is not None:
            build(host, "final build without the smoke scripts", "C# build succeeded")
            check("plugin answers after the final build", wait_connected(host, args.reconnect) is not None)

    print(f"calls refused by the reload gate: {refused}; other failed calls during reloads: {transient}")
    if args.editor_log:
        log = args.editor_log.read_text(encoding="utf-8", errors="replace") if args.editor_log.exists() else ""
        for bad in ("Failed to unload assemblies", "Giving up on assembly reloading"):
            check(f"editor log has no '{bad}'", bad not in log)
    if FAILURES:
        print(f"\n{len(FAILURES)} check(s) failed:")
        for failure in FAILURES:
            print(f"- {failure}")
        return 1
    print("all reload checks passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
