#!/usr/bin/env python3
# ┌──────────────────────────────────────────────────────────────────┐
# │  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
# │  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
# │  Copyright (c) 2026 Ivan Murzak                                  │
# │  Licensed under the Apache License, Version 2.0.                 │
# │  See the LICENSE file in the project root for more information.  │
# └──────────────────────────────────────────────────────────────────┘
"""
Assert the structured result file produced by the issue-#186 runtime-integration harness
(Godot-Tests/Harness/RuntimeHarness.cs). The harness ALREADY decides pass/fail and encodes
it in its process exit code + the result's `ok` field; this asserter re-checks every
per-version expectation INDEPENDENTLY so CI fails with a readable, field-by-field diagnosis
(not just a bare non-zero exit) and so a future harness regression that flips `ok` true
without actually capturing what it claims is still caught here.

Usage:
    python scripts/assert_runtime_harness.py --result <path> --engine-logger-expected {0|1} \
        [--require-connect {0|1}] [--require-roundtrip {0|1}]

Per-version contract (the issue's acceptance criteria):
  * ALWAYS: capture installed + available; the C# unobserved-Task exception captured WITH a
    stack trace; (when --require-connect=1) the live SignalR transport connected; and the
    Godot.Variant converter round-trip (issue #292) — every documented wire shape materializes,
    an unreadable payload raises instead of degrading to a silent Nil, and the issue's own
    ProjectSettings.SetSetting repro reads its value back. (The Node-ref converter's resolve path
    is NOT covered here: its resolver is installed under #if TOOLS and this is a game process.)
  * --engine-logger-expected 1 (Godot 4.5+): the engine GDScript runtime error captured WITH
    a multi-frame backtrace (>= 2 frames, issue #163) + push_error + push_warning.
  * --engine-logger-expected 0 (Godot 4.3 / 4.4): the engine channel is ABSENT (graceful stub
    degradation) — no engine rows, no engine frames — while the C# channel still captures.

Pure stdlib — runs anywhere Python 3.8+ does. Exit 0 = every assertion held; 1 = a failure
(each failed assertion is printed); 2 = the result file is missing/unparseable.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path


#: Minimum number of Godot.Variant wire shapes the harness must exercise (issue #292).
#:
#: DELIBERATELY an independent literal, not read from RuntimeHarness.VariantRoundTripMinimumChecks.
#: This asserter's whole charter (see the module docstring) is to re-check the harness's claims
#: INDEPENDENTLY, so that a harness edit which flips `ok` true without actually doing the work is
#: still caught. Deriving this number from the harness source would make one edit defeat both
#: gates — the opposite of defence in depth. It is a FLOOR, so it does not need to track the C#
#: constant upward; it only needs to stop the assertion going vacuous.
VARIANT_MIN_CHECKS = 11


def _b(value) -> bool:
    return value is True


def main() -> int:
    ap = argparse.ArgumentParser(description="Assert the Godot-MCP runtime-harness result.")
    ap.add_argument("--result", required=True, help="Path to the harness result JSON.")
    ap.add_argument("--engine-logger-expected", required=True, choices=["0", "1"],
                    help="1 when the Godot 4.5+ engine logger channel must be present, 0 for 4.3/4.4.")
    ap.add_argument("--require-connect", default="1", choices=["0", "1"],
                    help="1 (default) to require the live SignalR connection.")
    ap.add_argument("--require-roundtrip", default="0", choices=["0", "1"],
                    help="1 to require the result to record a successful tool roundtrip (default 0: the "
                         "roundtrip is asserted by the workflow's own server POST, not by the harness).")
    args = ap.parse_args()

    result_path = Path(args.result)
    if not result_path.is_file():
        print(f"::error::harness result file not found: {result_path}")
        return 2

    try:
        data = json.loads(result_path.read_text(encoding="utf-8"))
    except Exception as ex:  # noqa: BLE001
        print(f"::error::could not parse harness result {result_path}: {ex}")
        return 2

    engine_expected = args.engine_logger_expected == "1"
    require_connect = args.require_connect == "1"

    failures: list[str] = []

    def check(cond: bool, msg: str) -> None:
        if not cond:
            failures.append(msg)

    # --- Always-required (version-independent) -----------------------------------------------
    check(data.get("fatalError") in (None, ""), f"harness reported a fatal error: {data.get('fatalError')!r}")
    check(_b(data.get("captureInstalled")), "runtime error capture did not install")
    check(_b(data.get("captureAvailable")), "RuntimeErrorCollector.Current was not available")
    check(_b(data.get("hasCSharpException")), "the C# (unobserved-Task) exception was not captured")
    check(_b(data.get("cSharpExceptionHasStackTrace")), "the captured C# exception had no stack trace")

    # --- Godot.Variant converter (issue #292) ------------------------------------------------
    # This runs against a REAL Godot, which is the only place it CAN run: materializing a non-Nil
    # Variant calls godotsharp_* P/Invoke and faults the plain xUnit host, so the unit suite covers
    # only the pure parser and the failure paths.
    variant_failures = data.get("variantRoundTripFailures") or []
    check(not variant_failures,
          f"Godot.Variant round-trip failures: {variant_failures}")
    checked = data.get("variantRoundTripChecked", 0)
    check(isinstance(checked, int) and checked >= VARIANT_MIN_CHECKS,
          f"Godot.Variant round-trip exercised only {checked} shape(s), expected >= {VARIANT_MIN_CHECKS} "
          "— the assertion must not go vacuous")
    check(_b(data.get("variantRoundTripRejectsGarbage")),
          "an unreadable Godot.Variant payload did NOT raise — that is the issue-#292 silent-Nil regression")
    check(_b(data.get("variantProjectSettingRoundTrip")),
          "ProjectSettings.SetSetting through a wire Godot.Variant did not read back the value "
          "(the verbatim issue-#292 repro)")

    if require_connect:
        check(_b(data.get("connected")), "the live SignalR transport did not reach Connected")
        check(_b(data.get("initialConnectReturned")), "Connect() did not return true (initial connect failed)")

    # --- Per-version engine-channel expectations ---------------------------------------------
    check(_b(data.get("engineLoggerExpected")) == engine_expected,
          f"engineLoggerExpected in result ({data.get('engineLoggerExpected')}) != "
          f"--engine-logger-expected ({engine_expected}) — the leg's SDK build / version are mismatched")

    if engine_expected:
        # Godot 4.5+: engine GDScript runtime error WITH the #163 multi-frame backtrace + push diagnostics.
        check(_b(data.get("hasEngineRuntimeError")),
              "engine GDScript runtime error was NOT captured (4.5+ engine logger expected)")
        frame_count = data.get("engineRuntimeErrorFrameCount", 0)
        check(isinstance(frame_count, int) and frame_count >= 2,
              f"engine GDScript runtime error backtrace was not deep enough "
              f"(frames={frame_count}, expected >= 2 — issue #163)")
        check(_b(data.get("engineRuntimeErrorHasStackTrace")),
              "engine GDScript runtime error carried no formatted stack trace")
        check(_b(data.get("hasPushError")), "push_error was NOT captured (4.5+ engine logger expected)")
        check(_b(data.get("hasPushWarning")), "push_warning was NOT captured (4.5+ engine logger expected)")
    else:
        # Godot 4.3 / 4.4: the engine channel must degrade gracefully to ABSENT.
        check(not _b(data.get("hasEngineRuntimeError")),
              "engine GDScript runtime error was captured on a < 4.5 leg (engine logger should be absent)")
        check(data.get("engineRuntimeErrorFrameCount", 0) == 0,
              "engine backtrace frames present on a < 4.5 leg (engine logger should be absent)")
        check(not _b(data.get("hasPushError")),
              "push_error was captured on a < 4.5 leg (engine logger should be absent)")
        check(not _b(data.get("hasPushWarning")),
              "push_warning was captured on a < 4.5 leg (engine logger should be absent)")

    # --- The harness's own verdict must agree ------------------------------------------------
    check(_b(data.get("ok")), "the harness's own 'ok' verdict was false")

    print(f"Godot version: {data.get('godotVersion')!r}; engineLoggerExpected={engine_expected}; "
          f"requireConnect={require_connect}")
    print(json.dumps(data, indent=2))

    if failures:
        print(f"::error::runtime harness assertion FAILED ({len(failures)} issue(s)):")
        for f in failures:
            print(f"::error::  - {f}")
        return 1

    print("Runtime harness assertions PASSED.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
