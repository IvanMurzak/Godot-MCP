#!/usr/bin/env python3
"""
Live cases of several statements in one call (`;`, RQL 0.4.0), run by scripts/smoke_rql.py against a running editor: a read
runs every statement; a change commits one undo action per statement before the next one runs (a later statement finds the
node an earlier one made, Undo() in it undoes the earlier statement, SaveScene() in it writes the earlier change); the first
failing statement stops the rest and the earlier ones stay; a dry run of several changes nothing. Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import pathlib

from smoke_rql_lib import check, file_has, file_text, must, mutate, q, query
from smoke_rql_models import fails, has, value_is


def run(host: str, project: pathlib.Path, d: str) -> None:
    scene = f"{d}/st_scene.tscn"
    must("ST new scene", mutate(host, f'editor | call NewScene({q(scene)}, "Node3D", "St")'))

    # Each statement sees the earlier ones and is its own undo action (two entries in the history, newest first).
    text = must("ST two changes", mutate(host, 'root | call AddChild("Node3D", "StA"); godot.find("StA") | set Position.X = 3'))
    has("ST one block per statement", text, "[1/2] root | call AddChild", "[2/2] godot.find(\"StA\")")
    value_is("ST the second statement found the new node", must("ST read X", query(host, 'godot.find("StA") | select Position.X')), "3")
    history = must("ST history", query(host, "editor | select undoHistory"))
    check("ST one undo action per statement, newest first",
          0 <= history.find("Position.X = 3") < history.find('AddChild("Node3D", "StA")'), history)

    # Undo() in a later statement undoes the earlier statement (its action is committed before the next one runs).
    must("ST change then undo", mutate(host, 'godot.find("StA") | set Position.Y = 5; editor | call Undo()'))
    value_is("ST Undo() undid the earlier statement", must("ST read Y", query(host, 'godot.find("StA") | select Position.Y')), "0")

    # The first failing statement stops the rest; the earlier change stays.
    fails("ST a failing statement", mutate(host, 'godot.find("StA") | set name = "StB"; root | set NoSuchMember = 1; '
                                                 'root | call AddChild("Node3D", "StC")'), "[3/3]", "not run")
    value_is("ST the earlier change stays", must("ST StB", query(host, 'nodes | where name == "StB" | count')), "1")
    value_is("ST the later statement did not run", must("ST StC", query(host, 'nodes | where name == "StC" | count')), "0")

    # A dry run of several statements changes nothing.
    must("ST dry run", mutate(host, 'godot.find("StB") | set Visible = false; root | call AddChild("Node3D", "StD")', dry_run=True))
    value_is("ST dry run left Visible", must("ST visible", query(host, 'godot.find("StB") | select Visible')), "true")
    value_is("ST dry run made no node", must("ST StD", query(host, 'nodes | where name == "StD" | count')), "0")

    # SaveScene() in a later statement writes the earlier change.
    must("ST change then save", mutate(host, 'godot.find("StB") | set Visible = false; editor | call SaveScene()'))
    check("ST the save wrote the earlier change", file_has(project, scene, r"^visible = false$"), file_text(project, scene))

    # A read runs every statement, also after one that fails.
    ok, text = query(host, 'nodes | where name == "StB" | count; nodes | where; root | select name')
    check("ST a read with a bad statement fails", not ok, text)
    has("ST the read ran the statements around it", text, "[1/3]", "[3/3] root | select name", "St")
