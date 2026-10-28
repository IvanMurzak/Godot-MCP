#!/usr/bin/env python3
"""
Live cases of what wave-5 agent runs (building a small game end to end) could not do, run by scripts/smoke_rql.py against a
running editor: script-validate reporting GDScript warnings (an unused variable), an autoload added with rql-mutate being
known to the editor's GDScript compiler at once (and gone again after Undo), and a clear error for a call without its query.
The C# compiler warnings of script-build are checked by scripts/smoke_reload.py. Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import pathlib

from smoke_rql_lib import check, must, mutate, q, query, tool
from smoke_rql_models import fails, has, lacks, value_is

TAB = "\t"


def run(host: str, project: pathlib.Path, d: str) -> None:
    missing_query(host)
    gdscript_warnings(host, d)
    autoload_registry(host, d)


def missing_query(host: str) -> None:
    """A call without its query names the argument instead of an internal ArgumentNullException."""
    fails("W5Q rql-query without a query", tool(host, "rql-query", {}), "missing-argument", "query")
    fails("W5Q rql-mutate without a query", tool(host, "rql-mutate", {}), "missing-argument", "query")


def gdscript_warnings(host: str, d: str) -> None:
    """script-validate lists the GDScript warnings (shown only in the script editor before) without failing ok."""
    path = f"{d}/w5_warn.gd"
    src = f"extends Node\n\nfunc _ready() -> void:\n{TAB}var unused_counter := 0\n{TAB}var unused_total := 1\n{TAB}print(1)\n"
    must("W5W script-create", tool(host, "script-create", {"scriptPath": path, "content": src}))
    text = must("W5W validate", tool(host, "script-validate", {"scriptPath": path}))
    if "Coarse" in text:
        print("[SKIP] W5W GDScript warnings need the Godot 4.5+ error hook", flush=True)
        return
    has("W5W both unused variables are warnings", text, "unused_counter", "unused_total", "Warning")
    flat = text.replace(" ", "").replace('"', "").lower()
    check("W5W ok stays true and both warnings are counted", "ok:true" in flat and "warningcount:2" in flat, text)
    lacks("W5W the probe's error marker is not shown", text, "Warning treated as error")
    level = must("W5W the warning level is restored", query(host, 'settings | where name == "debug/gdscript/warnings/unused_variable" | select value'))
    value_is("W5W unused_variable stays at Warn (1)", level, "1")


def autoload_registry(host: str, d: str) -> None:
    """An autoload added through rql-mutate is a GDScript singleton for the editor right away; Undo removes it again."""
    name = "W5SmokeAutoload"
    singleton, user = f"{d}/w5_singleton.gd", f"{d}/w5_user.gd"
    must("W5A singleton script", tool(host, "script-create", {"scriptPath": singleton, "content": f"extends Node\n\nfunc ping() -> int:\n{TAB}return 1\n"}))
    must("W5A add the autoload", mutate(host, f"project | call AddAutoload({q(name)}, {q(singleton)})"))
    must("W5A a script using it", tool(host, "script-create", {"scriptPath": user, "content": f"extends Node\n\nfunc _ready() -> void:\n{TAB}print({name}.ping())\n"}))
    text = must("W5A validate", tool(host, "script-validate", {"scriptPath": user}))
    lacks("W5A the singleton resolves", text, "not found", "not declared")
    must("W5A undo the autoload", mutate(host, "editor | call Undo()"))
    text = must("W5A validate after undo", tool(host, "script-validate", {"scriptPath": user}))
    check("W5A undone: the singleton is unknown again", "not declared" in text or "not found" in text, text)
    must("W5A redo", mutate(host, "editor | call Redo()"))
    text = must("W5A validate after redo", tool(host, "script-validate", {"scriptPath": user}))
    lacks("W5A redone: the singleton resolves again", text, "not found", "not declared")
    must("W5A remove the autoload", mutate(host, f"project | call RemoveAutoload({q(name)})"))
