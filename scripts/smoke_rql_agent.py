#!/usr/bin/env python3
"""
Live cases of what wave-3 agent runs could not do or got wrong, run by scripts/smoke_rql.py against a running editor:
undo / redo through the editor (editor | call Undo() / Redo()), the receipt warning of a degree-sized Rotation, script-only
Godot properties by name (props["rotation_degrees"]), enum labels without their note and C# enum names with digits, the
rename hint for siblings that swap names, inherited scenes (NewScene with a base scene), ReloadScene, and the gameLog source
of a game run from the editor.

Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import pathlib
import time

from smoke_rql_lib import check, file_text, must, mutate, q, query, tool
from smoke_rql_models import fails, has, lacks


def run(host: str, project: pathlib.Path, d: str) -> None:
    undo_redo(host, d)
    radians(host)
    script_only_props(host)
    enum_names(host)
    rename_hint(host)
    inherited_and_reload(host, project, d)
    game_log(host, d)


def undo_redo(host: str, d: str) -> None:
    """editor | call Undo() / Redo() step the editor's own history and name the action (the mutation's query)."""
    must("AU new scene", mutate(host, f'editor | call NewScene({q(d + "/ag_undo.tscn")}, "Node3D", "U")'))
    must("AU add", mutate(host, 'root | call AddChild("Node3D", "Alpha")'))
    must("AU rename", mutate(host, 'godot.find("Alpha") | set name = "Beta"'))
    text = must("AU undo", mutate(host, "editor | call Undo()"))
    has("AU undo names the action", text, "undone: RQL: godot.find(", "set name =")
    has("AU the rename is undone", must("AU read", query(host, "root | get children | select name")), "Alpha")
    has("AU redo", must("AU redo call", mutate(host, "editor | call Redo()")), "redone: RQL:")
    has("AU the rename is back", must("AU read 2", query(host, "root | get children | select name")), "Beta")
    fails("AU nothing to redo", mutate(host, "editor | call Redo()"), "nothing to redo")
    must("AU undo rename", mutate(host, "editor | call Undo()"))
    has("AU undo the AddChild", must("AU undo 2", mutate(host, "editor | call Undo()")), "AddChild")
    has("AU no child left", must("AU count", query(host, "root | get children | count")), "0")


def radians(host: str) -> None:
    """Rotation is radians: a degree-sized value warns on the receipt and names RotationDegrees."""
    must("AR light", mutate(host, 'root | call AddChild("DirectionalLight3D", "Sun")'))
    has("AR degree-sized Rotation warns", must("AR set", mutate(host, 'godot.find("Sun") | set Rotation = "(-45, 30, 0)"')),
        "warning radians", "RotationDegrees = (-45, 30, 0)")
    lacks("AR radians do not warn", must("AR set 2", mutate(host, 'godot.find("Sun") | set Rotation = "(-0.785, 0.5, 0)"')), "radians")
    has("AR one axis warns", must("AR set 3", mutate(host, 'godot.find("Sun") | set Rotation.X = 90')), "RotationDegrees.X = 90")


def script_only_props(host: str) -> None:
    """A Godot property scripts see but the inspector does not list (rotation_degrees) is reachable by name in props."""
    must("AP write rotation_degrees", mutate(host, 'godot.find("Sun") | set props["rotation_degrees"] = "(-45, 30, 0)"'))
    has("AP read back", must("AP read", query(host, 'godot.find("Sun") | select RotationDegrees props["rotation_degrees"]')),
        "(-45, 30, 0)|(-45, 30, 0)")
    lacks("AP not listed among the keys", must("AP keys", query(host, 'godot.find("Sun") | select props')), "rotation_degrees")
    fails("AP an unknown property is still refused", mutate(host, 'godot.find("Sun") | set props["no_such_property"] = 1'), "")


def enum_names(host: str) -> None:
    """Enum-hinted ints take an inspector label without its "(note)" and the C# enum name with digits."""
    has("AE label without its note", must("AE set", mutate(host, 'godot.find("Sun") | set props["directional_shadow_mode"] = "PSSM 4 Splits"')), "|2")
    has("AE C# name with a digit", must("AE set 2", mutate(host, 'godot.find("Sun") | set props["directional_shadow_mode"] = "Parallel2Splits"')), "|1")


def rename_hint(host: str) -> None:
    """Siblings that take each other's names are refused with the two-step way out."""
    must("AN P1", mutate(host, 'root | call AddChild("Node3D", "P1")'))
    must("AN P2", mutate(host, 'root | call AddChild("Node3D", "P2")'))
    fails("AN a swap names the two-step rename",
          mutate(host, 'root | get children | where name starts-with "P" | sort name desc | set name = "P" + ($index + 1)'), "temporary names")


def inherited_and_reload(host: str, project: pathlib.Path, d: str) -> None:
    """NewScene with a base scene path makes an inherited scene (two levels); ReloadScene drops unsaved changes."""
    base, mid, top = f"{d}/ag_base.tscn", f"{d}/ag_mid.tscn", f"{d}/ag_top.tscn"
    must("AI base", mutate(host, f'editor | call NewScene({q(base)}, "Node3D", "Enemy")'))
    must("AI base child", mutate(host, 'root | call AddChild("MeshInstance3D", "Body")'))
    must("AI base save", mutate(host, "editor | call SaveScene()"))
    has("AI inherited scene", must("AI mid", mutate(host, f"editor | call NewScene({q(mid)}, {q(base)})")), "(Enemy)")
    has("AI the file instances the base", file_text(project, mid), 'instance=ExtResource("1_base")', base)
    has("AI second level", must("AI top", mutate(host, f'editor | call NewScene({q(top)}, {q(mid)}, "Boss")')), "(Boss)")
    has("AI base nodes visible", must("AI read", query(host, "root | get children | select name")), "Body")
    fails("AI a missing base is refused", mutate(host, f'editor | call NewScene({q(d + "/ag_bad.tscn")}, {q(d + "/ag_missing.tscn")})'), "no scene")
    fails("AI a missing base is refused on a dry run",
          mutate(host, f'editor | call NewScene({q(d + "/ag_bad.tscn")}, {q(d + "/ag_missing.tscn")})', dry_run=True), "no scene")
    must("AI an unsaved change", mutate(host, 'godot.find("Body") | set Position.Y = 3'))
    has("AI reload", must("AI reload call", mutate(host, "editor | call ReloadScene()")), "(Boss)")
    has("AI the change is gone", must("AI read 2", query(host, 'godot.find("Body") | select Position')), "(0, 0, 0)")


def game_log(host: str, d: str) -> None:
    """gameLog: what a game run from the editor printed, its errors with their location."""
    tag = str(time.time_ns())[-8:]
    gd = f"{d}/ag_game.gd"
    code = (f'extends Node\n\nfunc _ready():\n\tprint("agent-log-{tag}")\n\tpush_error("agent-error-{tag}")\n'
            "\tawait get_tree().create_timer(0.5).timeout\n\tget_tree().quit()\n")
    must("AG script", tool(host, "script-create", {"scriptPath": gd, "content": code}))
    must("AG scene", mutate(host, f'editor | call NewScene({q(d + "/ag_game.tscn")}, "Node", "Game")'))
    must("AG attach", tool(host, "script-attach-to-node", {"nodeRef": ".", "scriptPath": gd}))
    must("AG save", mutate(host, "editor | call SaveScene()"))
    must("AG play", mutate(host, f'editor | call PlayScene({q(d + "/ag_game.tscn")})'))
    text = ""
    for _ in range(40):
        time.sleep(0.5)
        ok, text = query(host, "gameLog | select LogType Message StackTrace")
        if ok and f"agent-error-{tag}" in text:
            break
    must("AG stop", mutate(host, "editor | call StopScene()"))
    if f"agent-log-{tag}" not in text:
        # A runner without a display may not start the game at all; the parser is covered by the xUnit suite.
        print(f"[SKIP] AG gameLog: the game wrote no log here ({text[:200]!r})", flush=True)
        return
    has("AG the print", text, f"Log|agent-log-{tag}")
    has("AG the error and where", text, f"Error|agent-error-{tag}", "ag_game.gd")
    check("AG schema lists gameLog", "gameLog" in must("AG schema", tool(host, "rql-schema", {})))
