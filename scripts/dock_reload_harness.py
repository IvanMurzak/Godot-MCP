#!/usr/bin/env python3
"""
In-session C# rebuild dock-survival harness — the godotengine/godot#51626 guard.

WHAT IT PROVES
  The "AI Game Developer" editor dock must SURVIVE a Godot in-session C# rebuild ("Build Project").
  On such a rebuild Godot hot-reloads the addon's collectible AssemblyLoadContext: the addon's
  Teardown frees the dock, and (UNFIXED) the re-instantiated EditorPlugin never re-adds it, so the
  dock silently disappears until the addon is disabled/re-enabled. A plain `--editor --quit` boot
  does NOT exercise this — only an IN-SESSION rebuild does, which is what this harness triggers.

HOW IT WORKS (no extra Godot CLI flag — a real in-session reload)
  1. Boots a HEADLESS editor (`--editor`, held open) with the DEV-ONLY dev-control bridge enabled
     (GODOT_MCP_DEV_CONTROL=1) so it can be polled over loopback HTTP.
  2. Polls `GET /state` and asserts the dock is in the editor dock tree (`diagInsideTree == true`).
  3. Triggers a GENUINE in-session rebuild by rewriting the project assembly on disk (`dotnet build`
     after touching a source file). Godot's HotReloadAssemblyWatcher (a 0.5s editor Timer) detects
     the newer DLL and runs `reload_assemblies` → ALC unload → the addon's reload re-entry.
  4. Waits for the reload (the editor log prints "assembly unloading" then a SECOND "plugin loaded"),
     then polls `GET /state` again and asserts the dock is STILL in-tree (`diagInsideTree == true`).

  On the UNFIXED addon the dev-control bridge (which lives on the freed dock) never comes back, so
  step 4's `/state` poll never succeeds within the deadline → the harness fails RED. On the FIXED
  addon the reloaded plugin re-registers a fresh dock + bridge and `/state` reports the dock back.

UPSTREAM NOISE (tolerated, never gated on)
  Godot's own reload emits "An item with the same key has already been added" for some [Tool] classes
  in generic-heavy assemblies (godotengine/godot #79519 / #87877 / #98094 / #110784). It is non-fatal
  engine noise — this harness does NOT gate on it.

USAGE
  python scripts/dock_reload_harness.py \
      --godot /path/to/Godot_..._console.exe \
      --project Godot-Tests \
      --csproj Godot-Tests/Godot-Tests.csproj \
      --port 9920

  Exit 0 = dock present before AND after the in-session rebuild. Non-zero = the dock did not survive
  (or never appeared). Pure stdlib (urllib/subprocess) — runs anywhere Python 3.8+ does.
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request


def log(msg: str) -> None:
    print(msg, flush=True)


def get_state(base_url: str, timeout: float):
    """GET /state -> the parsed "dock" object, or None on any error (bridge down)."""
    url = base_url.rstrip("/") + "/state"
    try:
        with urllib.request.urlopen(url, timeout=timeout) as resp:
            if resp.status != 200:
                return None
            return json.loads(resp.read().decode()).get("dock", {})
    except (urllib.error.URLError, OSError, json.JSONDecodeError):
        return None


def wait_for_dock(base_url: str, deadline_s: float, timeout: float):
    """Poll /state until the dock reports diagInsideTree == true, or the deadline passes."""
    end = time.time() + deadline_s
    last = None
    while time.time() < end:
        dock = get_state(base_url, timeout)
        if dock is not None:
            last = dock
            if str(dock.get("diagInsideTree")).lower() == "true":
                return True, dock
        time.sleep(1.0)
    return False, last


def count_marker(log_path: str, marker: str) -> int:
    try:
        with open(log_path, "r", encoding="utf-8", errors="replace") as f:
            return f.read().count(marker)
    except OSError:
        return 0


def trigger_in_session_rebuild(csproj: str, touch_file: str, config: str) -> None:
    """Rewrite the project assembly on disk so Godot's HotReloadAssemblyWatcher reloads it in-session."""
    # Touch a source file so the incremental build actually re-emits the assembly (a newer mtime is
    # what is_assembly_reloading_needed() checks). os.utime to "now" is enough.
    now = time.time()
    os.utime(touch_file, (now, now))
    log(f"[harness] touched {touch_file}; rebuilding to produce a newer assembly ...")
    proc = subprocess.run(
        ["dotnet", "build", csproj, "--configuration", config],
        capture_output=True, text=True,
    )
    if proc.returncode != 0:
        log("[harness] rebuild FAILED:")
        log(proc.stdout[-2000:])
        log(proc.stderr[-2000:])
        raise SystemExit(3)
    log("[harness] rebuild OK (assembly rewritten).")


def main() -> int:
    p = argparse.ArgumentParser(description="In-session C# rebuild dock-survival harness (godot#51626).")
    p.add_argument("--godot", required=True, help="path to the Godot mono editor binary (console variant preferred).")
    p.add_argument("--project", required=True, help="path to the Godot project dir to open (--path).")
    p.add_argument("--csproj", required=True, help="path to the project's .csproj to `dotnet build` for the rebuild.")
    p.add_argument("--touch-file", default=None,
                   help="source file to touch before the rebuild (default: <project>/addons/godot_mcp/Editor/GodotMcpPlugin.cs).")
    p.add_argument("--config", default="Debug", help="dotnet build configuration (default Debug).")
    p.add_argument("--port", type=int, default=9920, help="dev-control bridge port (default 9920).")
    p.add_argument("--log-file", default=None, help="editor log path (default <project>/harness-editor.log).")
    p.add_argument("--boot-deadline", type=float, default=90.0, help="seconds to wait for the dock on first boot.")
    p.add_argument("--reload-deadline", type=float, default=90.0, help="seconds to wait for the dock to return after the rebuild.")
    p.add_argument("--request-timeout", type=float, default=8.0, help="per-request timeout seconds.")
    args = p.parse_args()

    base_url = f"http://127.0.0.1:{args.port}"
    log_file = args.log_file or os.path.join(args.project, "harness-editor.log")
    touch_file = args.touch_file or os.path.join(args.project, "addons", "godot_mcp", "Editor", "GodotMcpPlugin.cs")

    if not os.path.isfile(touch_file):
        log(f"FATAL: touch file not found: {touch_file}")
        return 2

    env = dict(os.environ)
    env["GODOT_MCP_DEV_CONTROL"] = "1"
    env["GODOT_MCP_DEV_CONTROL_PORT"] = str(args.port)

    log(f"[harness] booting headless editor: {args.godot} --headless --path {args.project} --editor")
    log_fh = open(log_file, "w", encoding="utf-8")
    # Hold the editor OPEN (no --quit): the in-session reload needs a live session.
    editor = subprocess.Popen(
        [args.godot, "--headless", "--path", args.project, "--editor"],
        stdout=log_fh, stderr=subprocess.STDOUT, env=env,
    )

    rc = 1
    try:
        # 1) First boot: assert the dock is in the editor dock tree.
        log("[harness] waiting for the dock on first boot ...")
        ok, dock = wait_for_dock(base_url, args.boot_deadline, args.request_timeout)
        if not ok:
            log("::error::dock NOT present on first boot — the bridge/dock never came up.")
            log(f"[harness] last /state dock: {dock!r}")
            return 1
        log(f"[harness] BOOT OK: dock in-tree (diagParentType={dock.get('diagParentType')}, "
            f"diagParentPath={dock.get('diagParentPath')}).")
        boot_loaded = count_marker(log_file, "[Godot-MCP] plugin loaded")

        # 2) Trigger a genuine in-session rebuild (rewrite the DLL on disk).
        trigger_in_session_rebuild(args.csproj, touch_file, args.config)

        # 3) Wait for the reload to actually happen (the addon logs "assembly unloading"), THEN assert the
        #    dock comes back. We poll the log for the unload marker first so we never falsely pass on the
        #    pre-reload state, then poll /state for diagInsideTree == true.
        log("[harness] waiting for the in-session ALC reload to fire ...")
        unload_deadline = time.time() + args.reload_deadline
        reloaded = False
        while time.time() < unload_deadline:
            if count_marker(log_file, "[Godot-MCP] assembly unloading") >= 1:
                reloaded = True
                break
            time.sleep(1.0)
        if not reloaded:
            log("::error::the in-session ALC reload never fired (no 'assembly unloading' in the editor log) — "
                "the HotReloadAssemblyWatcher did not pick up the rebuilt assembly.")
            return 1
        log("[harness] reload fired (ALC unloading). Asserting the dock SURVIVES ...")

        # 4) The decisive assertion: the dock must be in-tree again after the reload.
        ok, dock = wait_for_dock(base_url, args.reload_deadline, args.request_timeout)
        if not ok:
            # Distinguish the two failure causes: dock is None means /state never answered (the
            # dev-control bridge — which lives on the freed dock — never re-bound after the reload),
            # vs a dock dict whose diagInsideTree is not true (the bridge re-bound but the dock itself
            # did not re-register). Both are the godot#51626 regression, but the cause differs.
            if dock is None:
                log("::error::the dev-control bridge never answered /state after the reload — the "
                    "dock+bridge were freed by the ALC unload and never re-registered (godot#51626 "
                    "'missing AI Game Developer tab' regression).")
            else:
                log("::error::dock did NOT survive the in-session C# rebuild — the bridge re-bound but "
                    "diagInsideTree is not true after the reload (the godot#51626 'missing AI Game "
                    "Developer tab' regression).")
            log(f"[harness] last /state dock: {dock!r}")
            return 1

        after_loaded = count_marker(log_file, "[Godot-MCP] plugin loaded")
        log(f"[harness] AFTER RELOAD: dock in-tree (diagParentType={dock.get('diagParentType')}). "
            f"plugin-loaded count {boot_loaded} -> {after_loaded}.")
        log("[harness] PASS — the dock survived the in-session C# rebuild.")
        rc = 0
        return rc
    finally:
        # Always stop the editor + flush the log.
        try:
            # terminate() is cross-platform (SIGTERM on POSIX, TerminateProcess on Windows) so the
            # documented Windows local-repro path works as well as the ubuntu-only CI gate.
            editor.terminate()
            editor.wait(timeout=15)
        except Exception:
            try:
                editor.kill()
            except Exception:
                pass
        try:
            log_fh.close()
        except Exception:
            pass
        # Echo a tail of the editor log for CI diagnosis.
        try:
            with open(log_file, "r", encoding="utf-8", errors="replace") as f:
                tail = f.read().splitlines()[-60:]
            log("----- editor log (tail) -----")
            for line in tail:
                log(line)
        except OSError:
            pass


if __name__ == "__main__":
    raise SystemExit(main())
