#!/usr/bin/env python3
"""
Live cases of edits that used to corrupt data or answer misleadingly, run by scripts/smoke_rql.py against a running
editor: undo of a Reparent that kept the global transform, renames Godot would silently change, the scene root's
parent and ".." paths reaching the editor's own UI, deleting a file that is still used, protected project files,
save-as onto another scene, a scene instancing itself, undo of a resource file edit, a non-tool script's method, a
script attached to the wrong node class, comparing a struct to an object literal, and float noise in printed structs.

Every change is checked where it cannot lie: the .tscn / .tres / project.godot text Godot wrote, plus a read-back query.
Undo uses the test-only test.undo / test.redo (GODOT_MCP_RQL_TEST_HOOKS=1); without them those checks print SKIP.
Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import pathlib

from smoke_rql_lib import check, disk, file_has, file_text, must, mutate, q, query, tool
from smoke_rql_models import HOOKS, fails, has, lacks, new_scene, node, redo, skip_undo, undo


def run(host: str, project: pathlib.Path, d: str) -> None:
    reparent_undo(host, d)
    renames(host, d)
    scene_boundary(host, d)
    files(host, project, d)
    scenes(host, project, d)
    resource_undo_saves(host, project, d)
    scripts(host, d)
    structs(host, d)


def reparent_undo(host: str, d: str) -> None:
    """Undoing a Reparent that kept the global transform restores the old local transform (it was applied twice)."""
    if not HOOKS["on"]:
        skip_undo("H1 reparent undo")
        return
    new_scene(host, f"{d}/hz_reparent.tscn", "Node3D", "HzRoot", [("Node3D", "Parent")])
    must("H1 parent transform", mutate(host, f'{node("Parent")} | set Position = "(10, 0, 0)"'))
    must("H1 child", mutate(host, f'{node("Parent")} | call AddChild("Node3D", "Kid")'))
    must("H1 child position", mutate(host, f'{node("Kid")} | set Position = "(1, 0, 0)"'))
    must("H1 reparent to root", mutate(host, f'{node("Kid")} | call Reparent(godot.find("."))'))
    text = must("H1 global kept", query(host, f'{node("Kid")} | select path Position GlobalPosition'))
    has("H1 under the root, global (11, 0, 0)", text, "Kid|(11, 0, 0)|(11, 0, 0)")
    undo(host, "scene")
    text = must("H1 undo read", query(host, f'{node("Kid")} | select path Position GlobalPosition'))
    has("H1 undo restores parent and local transform", text, "Parent/Kid|(1, 0, 0)|(11, 0, 0)")
    redo(host, "scene")
    text = must("H1 redo read", query(host, f'{node("Kid")} | select path Position GlobalPosition'))
    has("H1 redo moves it back under the root", text, "Kid|(11, 0, 0)|(11, 0, 0)")
    fails("H1 Reparent(null) names the missing parent", mutate(host, f'{node("Kid")} | call Reparent(null)'), "new parent")


def renames(host: str, d: str) -> None:
    """A rename Godot would change ("a.b" -> "a_b", a sibling's name -> "N3") is refused instead of a lying receipt."""
    new_scene(host, f"{d}/hz_names.tscn", "Node3D", "NameRoot", [("Node3D", "N1"), ("Node3D", "N2")])
    fails("H2 a sibling's name is refused", mutate(host, f'{node("N1")} | set name = "N2"'), "sibling", "N2")
    fails("H2 an invalid character is refused", mutate(host, f'{node("N1")} | set name = "x.y:z"'), "'.'", "':'")
    text = must("H2 names unchanged", query(host, "root | select children.name"))
    has("H2 N1 and N2 kept", text, "[N1,N2]")
    must("H2 a valid rename", mutate(host, f'{node("N1")} | set name = "Renamed"'))
    must("H2 the same name again is a no-op", mutate(host, f'{node("Renamed")} | set name = "Renamed"'))
    text = must("H2 read", query(host, "root | select children.name"))
    has("H2 renamed", text, "[Renamed,N2]")


def scene_boundary(host: str, d: str) -> None:
    """Nothing reaches outside the edited scene: the root has no parent, ".." does not climb into the editor UI."""
    text = must("H3 root parent", query(host, "root | select parent"))
    has("H3 the root's parent is null", text, "parent\nnull")
    fails("H3 godot.find('..') stays in the scene", query(host, 'godot.find("..")'), "no node")
    fails("H3 godot.find('N2/../..') stays in the scene", query(host, 'godot.find("N2/../..")'), "no node")
    text = must("H3 a '..' path inside the scene", query(host, 'godot.find("N2/../Renamed") | select name'))
    has("H3 resolves inside", text, "Renamed")


def files(host: str, project: pathlib.Path, d: str) -> None:
    """Delete refuses a file still in use (also by unsaved edits); moves keep the extension; project files are protected."""
    mat, mesh = f"{d}/hz_used.tres", f"{d}/hz_unused.tres"
    new_scene(host, f"{d}/hz_files.tscn", "Node3D", "FileRoot", [("MeshInstance3D", "Box")])
    must("H4 create material", mutate(host, f'editor | call CreateResource({q(mat)}, "StandardMaterial3D")'))
    must("H4 create unused", mutate(host, f'editor | call CreateResource({q(mesh)}, "Gradient")'))
    must("H4 assign (unsaved)", mutate(host, f'{node("Box")} | set MaterialOverride = {q(mat)}'))
    fails("H4 delete refused while the unsaved scene uses it", mutate(host, f"editor | call DeleteResource({q(mat)})"),
          "still used", "Box")
    check("H4 file kept", disk(project, mat).exists(), mat)
    must("H4 save", mutate(host, "editor | call SaveScene()"))
    must("H4 clear the node's reference", mutate(host, f'{node("Box")} | set MaterialOverride = null'))
    fails("H4 delete refused while the saved scene file uses it", mutate(host, f"editor | call DeleteResource({q(mat)})"),
          "still used")
    must("H4 save without the reference", mutate(host, "editor | call SaveScene()"))
    must("H4 delete once unused", mutate(host, f"editor | call DeleteResource({q(mat)})"))
    check("H4 file gone", not disk(project, mat).exists(), mat)
    fails("H4 move changing the extension", mutate(host, f'editor | call MoveResource({q(mesh)}, {q(mesh[:-5] + ".res")})'),
          "extension")
    upper = f"{d}/HZ_UNUSED.tres"
    must("H4 case-only rename", mutate(host, f"editor | call MoveResource({q(mesh)}, {q(upper)})"))
    names = [p.name for p in disk(project, d).iterdir()]
    check("H4 renamed by case on disk", "HZ_UNUSED.tres" in names and "hz_unused.tres" not in names, str(names))
    must("H4 delete forced", mutate(host, f"editor | call DeleteResource({q(upper)}, true)"))
    for path in ("res://project.godot", "res://addons/godot_mcp/plugin.cfg"):
        fails(f"H4 delete {path} refused", mutate(host, f"editor | call DeleteResource({q(path)})"), "file tools")
        check(f"H4 {path} kept", disk(project, path).exists(), path)
    fails("H4 move project.godot refused", mutate(host, 'editor | call MoveResource("res://project.godot", "res://p2.godot")'), "file tools")
    fails("H4 godot.resources with an unknown class", query(host, 'godot.resources("NoSuchClass")'), "unknown-type")


def scenes(host: str, project: pathlib.Path, d: str) -> None:
    """Save-as never replaces another scene; a scene cannot instance itself (Godot then saves only its root)."""
    other, cyc = f"{d}/hz_other.tscn", f"{d}/hz_cycle.tscn"
    new_scene(host, other, "Node3D", "Other", [("Node3D", "OtherKid")])
    must("H5 save other", mutate(host, "editor | call SaveScene()"))
    new_scene(host, cyc, "Node3D", "Cycle", [("Node3D", "CycleKid")])
    fails("H5 save-as onto another scene refused", mutate(host, f"editor | call SaveScene({q(other)})"), "already exists")
    check("H5 the other scene is intact", file_has(project, other, r'name="OtherKid"'), file_text(project, other))
    fails("H5 instancing the edited scene into itself refused", mutate(host, f"root | call InstantiateScene({q(cyc)})"), "contain itself")
    must("H5 instance the other scene", mutate(host, f'root | call InstantiateScene({q(other)}, "Inst")'))
    must("H5 save", mutate(host, "editor | call SaveScene()"))
    check("H5 the instance is saved", file_has(project, cyc, r'name="Inst"'), file_text(project, cyc))
    must("H5 open other", mutate(host, f"editor | call OpenScene({q(other)})"))
    fails("H5 instancing a scene that contains this one refused", mutate(host, f"root | call InstantiateScene({q(cyc)})"), "contain itself")


def resource_undo_saves(host: str, project: pathlib.Path, d: str) -> None:
    """Undo and redo of a resource file edit write the file again (the edit itself saved it)."""
    if not HOOKS["on"]:
        skip_undo("H6 resource undo saves")
        return
    res = f"{d}/hz_undo.tres"
    must("H6 create", mutate(host, f'editor | call CreateResource({q(res)}, "StandardMaterial3D")'))
    must("H6 set", mutate(host, f"godot.resource({q(res)}) | set Metallic = 0.7"))
    check("H6 saved", file_has(project, res, r"^metallic = 0\.7$"), file_text(project, res))
    undo(host, "global")
    check("H6 undo saved the file again", not file_has(project, res, r"^metallic = 0\.7$"), file_text(project, res))
    redo(host, "global")
    check("H6 redo saved the file again", file_has(project, res, r"^metallic = 0\.7$"), file_text(project, res))


def scripts(host: str, d: str) -> None:
    """A non-tool script's method does not run in the editor; a script extending another class is not attached."""
    gd, gd3 = f"{d}/hz_plain.gd", f"{d}/hz_node3d.gd"
    new_scene(host, f"{d}/hz_scripts.tscn", "Node2D", "ScriptRoot", [("Sprite2D", "Spr")])
    must("H7 create script", tool(host, "script-create", {"scriptPath": gd, "content": "extends Sprite2D\nfunc hi():\n\treturn 1\n"}))
    must("H7 create Node3D script", tool(host, "script-create", {"scriptPath": gd3, "content": "extends Node3D\n"}))
    must("H7 attach", tool(host, "script-attach-to-node", {"nodeRef": "Spr", "scriptPath": gd}))
    fails("H7 a non-tool script's method", mutate(host, f'{node("Spr")} | call CallMethod("hi", [])'), "@tool")
    fails("H7 wrong argument count", mutate(host, f'{node("Spr")} | call CallMethod("set_position", [])'), "argument")
    must("H7 a native method", mutate(host, f'{node("Spr")} | call CallMethod("set_visible", [false])'))
    has("H7 applied", must("H7 read", query(host, f'{node("Spr")} | select Visible')), "false")
    fails("H7 attach a Node3D script to a Sprite2D", tool(host, "script-attach-to-node", {"nodeRef": "Spr", "scriptPath": gd3}), "Node3D")
    has("H7 script unchanged", must("H7 script read", query(host, f'{node("Spr")} | select script')), "hz_plain.gd")
    must("H7 save", mutate(host, "editor | call SaveScene()"))


def structs(host: str, d: str) -> None:
    """Object literals compare like text; printed structs carry no float noise."""
    new_scene(host, f"{d}/hz_structs.tscn", "Node3D", "StructRoot", [("Node3D", "Turned"), ("OmniLight3D", "Glow")])
    must("H8 rotate + scale", mutate(host, f'{node("Turned")} | set Position = "(0, 3, 0)", RotationDegrees = "(0, 90, 0)", Scale = "(2, 2, 2)"'))
    text = must("H8 object literal ==", query(host, "nodes | where Position == {x: 0, y: 3, z: 0} | select name"))
    has("H8 matches like the text form", text, "Turned")
    text = must("H8 object literal !=", query(host, "nodes | where $ is Node3D and Position != {x: 0, y: 3, z: 0} | select name"))
    lacks("H8 != excludes it", text, "Turned")
    text = must("H8 read transform", query(host, f'{node("Turned")} | select Transform Basis'))
    lacks("H8 no exponent noise", text, "E-0")
    has("H8 tidy basis", text, "(0, 0, -2, 0, 2, 0, 2, 0, 0, 0, 3, 0)")
    must("H8 hex color", mutate(host, f'{node("Glow")} | set LightColor = "#ff8800"'))
    has("H8 color tidy", must("H8 color read", query(host, f'{node("Glow")} | select LightColor')), "(1, 0.5333334, 0, 1)")
