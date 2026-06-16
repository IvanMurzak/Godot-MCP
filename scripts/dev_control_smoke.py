#!/usr/bin/env python3
"""
DEV-ONLY dock smoke test — drives EVERY interactive control of the "AI Game Developer"
Godot editor dock through the dev-control bridge (Connection/DevControl/), across all
connection modes (Custom / Cloud), the Custom-mode Authorization "transport" (none /
required), and all injected connection / server states.

Why this exists: dock buttons connect their Godot signals to C# handler delegates. If a
delegate is not kept strongly referenced, the editor's GC/assembly-reload can collect it,
after which clicking the button raises
    managed_callable.cpp: Parameter "delegate_handle.value" is null
    Can't get method on CallableCustom "Delegate::Invoke"
    Error calling from signal 'pressed' ... Method not found
and the click SILENTLY does nothing. This harness clicks every button via the bridge and
(optionally) scans the editor log for those signatures — so the regression can never come
back unnoticed.

It does NOT launch the editor; point it at an already-running bridge:

    # 1) boot the testbed editor with the bridge on (see the Godot-MCP testbed runbook):
    GODOT_MCP_DEV_CONTROL=1 GODOT_MCP_DEV_CONTROL_PORT=9920 \
        Godot_..._console.exe --editor --path <infra>/Godot-Test-Project
    # 2) run the smoke against it:
    python scripts/dev_control_smoke.py --base-url http://127.0.0.1:9920 \
        --log-file "<infra>/Godot-Test-Project/.godot/editor.log"

Exit code 0 = every step behaved + no forbidden log line; non-zero otherwise.
Pure stdlib (urllib) — no third-party deps, so it runs anywhere Python 3.8+ does.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
import urllib.error
import urllib.request

# Log signatures that mean a signal-handler delegate was collected (the bug this guards).
FORBIDDEN_LOG = [
    "delegate_handle.value",
    'CallableCustom "Delegate::Invoke"',
    "Error calling from signal",
    "Method not found",
]


class Smoke:
    def __init__(self, base_url: str, timeout: float):
        self.base = base_url.rstrip("/")
        self.timeout = timeout
        self.passed = 0
        self.failed = 0
        self.notes: list[str] = []

    # --- HTTP -------------------------------------------------------------------------------
    def _request(self, method: str, path: str, body: dict | None):
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(self.base + path, data=data, method=method)
        if data is not None:
            req.add_header("Content-Type", "application/json")
        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as resp:
                return resp.status, resp.read().decode()
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode()

    # --- assertions -------------------------------------------------------------------------
    def step(self, label: str, method: str, path: str, body: dict | None, accept):
        """Run one request; PASS when status is in `accept`. A 500 (handler crash) is ALWAYS a hard fail."""
        status, text = self._request(method, path, body)
        ok = status in accept and status != 500
        mark = "PASS" if ok else "FAIL"
        if ok:
            self.passed += 1
        else:
            self.failed += 1
        snippet = text if len(text) <= 160 else text[:157] + "..."
        print(f"  [{mark}] {label:<42} {method} {path} -> {status}  {snippet}")
        return status, text

    def state(self):
        _, text = self._request("GET", "/state", None)
        try:
            return json.loads(text).get("dock", {})
        except json.JSONDecodeError:
            return {}

    def assert_state(self, label: str, key: str, expected_substr: str):
        dock = self.state()
        actual = str(dock.get(key, ""))
        ok = expected_substr.lower() in actual.lower()
        mark = "PASS" if ok else "FAIL"
        self.passed += 1 if ok else 0
        self.failed += 0 if ok else 1
        print(f"  [{mark}] {label:<42} state.{key} = {actual!r} (want ~{expected_substr!r})")

    # --- the matrix -------------------------------------------------------------------------
    def run(self):
        ACCEPT_CLICK = {200, 409}   # 200 = clicked; 409 = button not present in this mode/state (NOT a crash)
        ACCEPT_OK = {200}
        ACCEPT_REJECT = {400, 404, 409}

        print("\n== health / state ==")
        self.step("health", "GET", "/health", None, ACCEPT_OK)
        self.step("state", "GET", "/state", None, ACCEPT_OK)

        print("\n== inject connection states ==")
        for s in ("connected", "connecting", "disconnected"):
            self.step(f"inject connection={s}", "POST", "/inject/connection-status", {"status": s}, ACCEPT_OK)
        self.step("inject connection=connected", "POST", "/inject/connection-status", {"status": "connected"}, ACCEPT_OK)

        print("\n== inject server states ==")
        for s in ("stopped", "starting", "running", "stopping", "external"):
            self.step(f"inject server={s}", "POST", "/inject/server-status", {"status": s}, ACCEPT_OK)

        print("\n== CUSTOM mode: transport none/required + buttons ==")
        self.step("mode=custom", "POST", "/control/set-segment", {"control": "mode", "option": "custom"}, ACCEPT_OK)
        self.step("server-url", "POST", "/control/server-url", {"url": "http://localhost:5300"}, ACCEPT_OK)
        self.assert_state("server-url applied", "serverUrl", "localhost:5300")
        self.step("auth=none", "POST", "/control/set-segment", {"control": "auth", "option": "none"}, ACCEPT_OK)
        self.step("auth=required", "POST", "/control/set-segment", {"control": "auth", "option": "required"}, ACCEPT_OK)
        self.step("click generate-token (New)", "POST", "/control/click", {"target": "generate-token"}, ACCEPT_CLICK)
        self.step("click generate (skills)", "POST", "/control/click", {"target": "generate"}, ACCEPT_CLICK)
        self.step("click connect", "POST", "/control/click", {"target": "connect"}, ACCEPT_CLICK)
        self.step("click start-server", "POST", "/control/click", {"target": "start-server"}, ACCEPT_CLICK)

        print("\n== CLOUD mode: Authorize / Revoke (the buttons that surfaced the bug) ==")
        self.step("mode=cloud", "POST", "/control/set-segment", {"control": "mode", "option": "cloud"}, ACCEPT_OK)
        # Authorize starts the device-auth flow (handler MUST fire — that's the regression). A second click cancels it.
        self.step("click authorize (start)", "POST", "/control/click", {"target": "authorize"}, ACCEPT_CLICK)
        self.step("click authorize (cancel)", "POST", "/control/click", {"target": "authorize"}, ACCEPT_CLICK)
        self.step("click revoke", "POST", "/control/click", {"target": "revoke"}, ACCEPT_CLICK)

        print("\n== agent configurators: select + per-agent buttons ==")
        for agent in ("claude-code", "cursor", "vscode", "claude-desktop"):
            st, _ = self.step(f"select agent={agent}", "POST", "/control/select-agent", {"agent": agent}, {200, 404})
            if st == 200:
                self.assert_state(f"agent={agent} applied", "selectedAgent", "")  # just confirm /state still serves
        self.step("click configure", "POST", "/control/click", {"target": "configure"}, ACCEPT_CLICK)
        self.step("click reveal", "POST", "/control/click", {"target": "reveal"}, ACCEPT_CLICK)
        self.step("click copy", "POST", "/control/click", {"target": "copy"}, ACCEPT_CLICK)
        self.step("click remove", "POST", "/control/click", {"target": "remove"}, ACCEPT_CLICK)

        print("\n== re-exercise the bug button after the whole matrix (rooting must persist) ==")
        self.step("mode=cloud again", "POST", "/control/set-segment", {"control": "mode", "option": "cloud"}, ACCEPT_OK)
        self.step("click authorize again", "POST", "/control/click", {"target": "authorize"}, ACCEPT_CLICK)
        self.step("click authorize cancel", "POST", "/control/click", {"target": "authorize"}, ACCEPT_CLICK)

        print("\n== negative paths (must be rejected, never 500) ==")
        self.step("bad route", "GET", "/nope", None, {404})
        self.step("bad click target", "POST", "/control/click", {"target": "explode"}, ACCEPT_REJECT)
        self.step("bad segment control", "POST", "/control/set-segment", {"control": "transport", "option": "http"}, ACCEPT_REJECT)
        self.step("bad segment option", "POST", "/control/set-segment", {"control": "mode", "option": "offline"}, ACCEPT_REJECT)

    # --- log scan ---------------------------------------------------------------------------
    def scan_log(self, log_file: str):
        print(f"\n== scanning editor log for delegate-collection signatures: {log_file} ==")
        try:
            with open(log_file, "r", encoding="utf-8", errors="replace") as f:
                content = f.read()
        except OSError as e:
            self.notes.append(f"log scan skipped ({e})")
            print(f"  [WARN] could not read log: {e}")
            return
        hits = [pat for pat in FORBIDDEN_LOG if pat in content]
        if hits:
            self.failed += 1
            print(f"  [FAIL] forbidden log signatures present: {hits}")
        else:
            self.passed += 1
            print("  [PASS] no delegate_handle / Delegate::Invoke / signal-call errors in the log")


def wait_for_health(base_url: str, deadline_s: float) -> bool:
    end = time.time() + deadline_s
    url = base_url.rstrip("/") + "/health"
    while time.time() < end:
        try:
            with urllib.request.urlopen(url, timeout=2) as resp:
                if resp.status == 200:
                    return True
        except (urllib.error.URLError, OSError):
            time.sleep(1.0)
    return False


def main() -> int:
    p = argparse.ArgumentParser(description="DEV-ONLY Godot dock dev-control smoke test")
    p.add_argument("--base-url", default="http://127.0.0.1:9920", help="bridge base URL (default 127.0.0.1:9920)")
    p.add_argument("--log-file", default=None, help="editor log to scan for delegate-collection errors")
    p.add_argument("--timeout", type=float, default=12.0, help="per-request timeout seconds")
    p.add_argument("--wait", type=float, default=60.0, help="seconds to wait for the bridge /health before giving up")
    args = p.parse_args()

    print(f"dev-control smoke -> {args.base_url}")
    if not wait_for_health(args.base_url, args.wait):
        print(f"FATAL: bridge /health never came up at {args.base_url} within {args.wait}s", file=sys.stderr)
        return 2

    smoke = Smoke(args.base_url, args.timeout)
    smoke.run()
    if args.log_file:
        smoke.scan_log(args.log_file)

    print(f"\n==== RESULT: {smoke.passed} passed, {smoke.failed} failed ====")
    for n in smoke.notes:
        print(f"  note: {n}")
    return 0 if smoke.failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
