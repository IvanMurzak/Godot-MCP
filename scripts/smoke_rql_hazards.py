#!/usr/bin/env python3
"""
Live cases of edits that used to corrupt data or answer misleadingly, run by scripts/smoke_rql.py against a running
editor: undo of a Reparent that kept the global transform, renames Godot would silently change, the scene root's
parent and ".." paths reaching the editor's own UI, deleting a file that is still used, protected project files,
save-as onto another scene, a scene instancing itself, undo of a resource file edit, a non-tool script's method, a
script attached to the wrong node class, comparing a struct to an object literal, float noise in printed structs,
calls refused before any runs (RQL 0.2 call validators: dry runs report them, multi-target calls are all-or-nothing), and
scene instances (overrides only, Editable Children for changes inside an instance, no edits Godot would not save).

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
    call_validators(host, d)
    instances(host, project, d)
    instance_copies(host, project, d)
    inherited(host, project, d)


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
    fails("H2 two siblings given one new name in one set", mutate(host, 'root | get children | set name = "Same"'), "Same")
    text = must("H2 nothing renamed", query(host, "root | select children.name"))
    has("H2 rolled back", text, "[Renamed,N2]")


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
    has("H8 color tidy", must("H8 color read", query(host, f'{node("Glow")} | select LightColor')), "(1, 0.533333, 0, 1)")  # RQL 0.2 tuple display: 6 significant digits


def call_validators(host: str, d: str) -> None:
    """RQL 0.2 call validators: a call that would fail is refused for every target before the first one runs, dry runs too."""
    new_scene(host, f"{d}/hz_validate.tscn", "Node3D", "ValRoot", [("Node3D", "A"), ("Node3D", "B")])
    fails("H9 a dry run reports a refused Free", mutate(host, "root | call Free()", dry_run=True), "invalid-value", "scene root")
    fails("H9 a dry run reports an unknown class", mutate(host, 'root | call AddChild("NoSuchNodeClass")', dry_run=True),
          "invalid-value", "unknown node class")
    # A (first in the scene) could move under B, B cannot move under itself: without the validator A would already have moved.
    fails("H9 a multi-target Reparent is all-or-nothing",
          mutate(host, 'nodes | where name == "A" or name == "B" | call Reparent(godot.find("B"))'), "invalid-value", "under itself")
    lacks("H9 A stayed under the root", must("H9 read A", query(host, 'nodes | where name == "A" | select path')), "B/A")
    # Sorted by name, A comes before the root: without the validator A would already be last when the root is refused.
    fails("H9 a multi-target MoveChild is all-or-nothing",
          mutate(host, 'nodes | where name == "A" or name == "ValRoot" | sort name | call MoveChild(-1)'), "invalid-value", "no siblings")
    has("H9 A kept its place", must("H9 read order", query(host, "root | get children | select name index")), "A|0", "B|1")
    fails("H9 a dry run reports a refused Duplicate", mutate(host, "root | call Duplicate()", dry_run=True), "invalid-value", "cannot be duplicated")
    fails("H9 NewScene onto an existing file", mutate(host, f'editor | call NewScene({q(d + "/hz_validate.tscn")})', dry_run=True),
          "invalid-value", "already exists")
    fails("H9 DeleteResource of a missing file", mutate(host, f'editor | call DeleteResource({q(d + "/hz_nothing.tres")})', dry_run=True),
          "invalid-value", "no file")


def instances(host: str, project: pathlib.Path, d: str) -> None:
    """
    Scene instances: InstantiateScene saves only overrides (no pinned type / script / groups); a change inside an instance
    turns on Editable Children so it is saved; removing, renaming, moving, reordering or duplicating a node of an instance
    (which Godot would never save) is refused.
    """
    base, level = f"{d}/hz_inst_base.tscn", f"{d}/hz_inst_level.tscn"
    gd = f"{d}/hz_inst.gd"
    new_scene(host, base, "Node3D", "Enemy", [("OmniLight3D", "Eye"), ("Node3D", "Hat")])
    must("H10 script", tool(host, "script-create", {"scriptPath": gd, "content": "@tool\nextends Node3D\n@export var health: int = 100\n"}))
    must("H10 attach", tool(host, "script-attach-to-node", {"nodeRef": ".", "scriptPath": gd}))
    must("H10 base group", mutate(host, 'root | call AddToGroup("enemies")'))
    must("H10 save base", mutate(host, "editor | call SaveScene()"))
    new_scene(host, level, "Node3D", "Level", [])
    must("H10 instance", mutate(host, f'root | call InstantiateScene({q(base)}, "E1")'))
    must("H10 instance 2", mutate(host, f'root | call InstantiateScene({q(base)}, "E2")'))
    must("H10 override on the instance root", mutate(host, 'godot.find("E1") | set props["health"] = 50'))
    must("H10 save", mutate(host, "editor | call SaveScene()"))
    text = file_text(project, level)
    check("H10 instance saved without a pinned type, groups or script",
          file_has(project, level, r'^\[node name="E2" parent="\." instance=ExtResource\("[^"]+"\)\]$')
          and "script = " not in text, text)
    check("H10 the override is saved", file_has(project, level, r"^health = 50$"), text)
    # A property inside an instance: saved because the instance becomes editable.
    must("H10 set inside the instance", mutate(host, 'godot.find("E1/Eye") | set LightColor = "#ff0000"'))
    must("H10 save 2", mutate(host, "editor | call SaveScene()"))
    text = file_text(project, level)
    check("H10 the instance is editable", file_has(project, level, r'^\[editable path="E1"\]$'), text)
    check("H10 the inner override is saved", file_has(project, level, r'^\[node name="Eye" parent="E1"')
          and file_has(project, level, r"^light_color = Color\(1, 0, 0, 1\)$"), text)
    check("H10 the other instance stays closed", not file_has(project, level, r'^\[editable path="E2"\]$'), text)
    # A child added under a node of an instance is saved too.
    must("H10 add under an inner node", mutate(host, 'godot.find("E2/Hat") | call AddChild("Node3D", "Feather")'))
    must("H10 save 3", mutate(host, "editor | call SaveScene()"))
    check("H10 the new inner child is saved", file_has(project, level, r'^\[node name="Feather" type="Node3D" parent="E2/Hat"')
          and file_has(project, level, r'^\[editable path="E2"\]$'), file_text(project, level))
    for label, query_text, verb in (
            ("Free", 'godot.find("E1/Hat") | call Free()', "removed"),
            ("rename", 'godot.find("E1/Hat") | set name = "Cap"', "renamed"),
            ("Reparent", 'godot.find("E1/Hat") | call Reparent(godot.find("."))', "moved"),
            ("MoveChild", 'godot.find("E1/Hat") | call MoveChild(0)', "reordered"),
            ("Duplicate", 'godot.find("E1/Hat") | call Duplicate()', "duplicated")):
        fails(f"H10 {label} of a node of an instance is refused", mutate(host, query_text), "instance", base, verb)
    has("H10 nothing moved", must("H10 read", query(host, 'godot.find("E1") | get children | select name')), "Eye", "Hat")
    must("H10 the instance itself can go", mutate(host, 'godot.find("E2") | call Free()', dry_run=True))
    if not HOOKS["on"]:
        skip_undo("H10 undo of Editable Children")
        return
    must("H10 set inside E1 again", mutate(host, 'godot.find("E1/Hat") | set Position = "(0, 2, 0)"'))
    undo(host, "scene")
    must("H10 save after undo", mutate(host, "editor | call SaveScene()"))
    check("H10 undo keeps E1 editable (it was before)", file_has(project, level, r'^\[editable path="E1"\]$'), file_text(project, level))
    must("H10 instance 3", mutate(host, f'root | call InstantiateScene({q(base)}, "E3")'))
    must("H10 set inside E3", mutate(host, 'godot.find("E3/Eye") | set OmniRange = 9'))
    has("H10 E3 editable now", must("H10 E3 read", query(host, 'godot.find("E3/Eye") | select OmniRange')), "9")
    undo(host, "scene")
    must("H10 save after E3 undo", mutate(host, "editor | call SaveScene()"))
    check("H10 undo turns Editable Children off again", not file_has(project, level, r'^\[editable path="E3"\]$')
          and file_has(project, level, r'^\[node name="E3"'), file_text(project, level))


def instance_copies(host: str, project: pathlib.Path, d: str) -> None:
    """Duplicate of an instance stays an instance with the original's overrides; Free over an instance and its nodes works."""
    base, level = f"{d}/hz_inst_base.tscn", f"{d}/hz_copies.tscn"  # the base scene instances() saved
    new_scene(host, level, "Node3D", "Copies", [("Node3D", "Holder")])
    must("H11 instance", mutate(host, f'root | call InstantiateScene({q(base)}, "A")'))
    must("H11 overrides", mutate(host, 'godot.find("A") | set props["health"] = 7, Position = "(1, 0, 0)"'))
    must("H11 inner override", mutate(host, 'godot.find("A/Eye") | set OmniRange = 4'))
    must("H11 own child inside", mutate(host, 'godot.find("A/Hat") | call AddChild("Node3D", "Pin")'))
    must("H11 duplicate", mutate(host, 'godot.find("A") | call Duplicate()'))
    must("H11 no-op set inside another instance", mutate(host, f'root | call InstantiateScene({q(base)}, "Closed")'))
    must("H11 same value", mutate(host, 'godot.find("Closed/Eye") | set Visible = true'))
    must("H11 own node into an inner node", mutate(host, 'godot.find("Holder") | call Reparent(godot.find("Closed/Hat"))'))
    must("H11 save", mutate(host, "editor | call SaveScene()"))
    text = file_text(project, level)
    check("H11 the copy is a plain instance (no pinned type, groups or script)",
          file_has(project, level, r'^\[node name="A2" parent="\." instance=ExtResource\("[^"]+"\)\]$'), text)
    check("H11 the copy keeps the overrides", file_has(project, level, r'^\[node name="Eye" parent="A2"[^\n]*\]\nomni_range = 4\.0$')
          and text.count("health = 7") == 2, text)
    check("H11 the copy keeps the node added inside", file_has(project, level, r'^\[node name="Pin" type="Node3D" parent="A2/Hat"'), text)
    check("H11 the copy is editable", file_has(project, level, r'^\[editable path="A2"\]$'), text)
    check("H11 the inner node is not re-saved whole", "light_color" not in text, text)
    check("H11 a node moved into an instance is saved there", file_has(project, level, r'^\[node name="Holder" type="Node3D" parent="Closed/Hat"'), text)
    must("H11 Free an instance with all its nodes", mutate(host, 'godot.find("A2") | recurse children | call Free()'))
    fails("H11 Free of an inner node alone", mutate(host, 'godot.find("A/Hat") | call Free()'), "instance", "removed")
    text = must("H11 read", query(host, "root | get children | select name"))
    lacks("H11 A2 is gone", text, "A2")
    has("H11 A kept", text, "A")


def inherited(host: str, project: pathlib.Path, d: str) -> None:
    """Nodes an inherited scene takes from its base: properties can change, structural edits are refused."""
    base, child = f"{d}/hz_inst_base.tscn", f"{d}/hz_inherited.tscn"
    # What Scene > New Inherited Scene saves: a root that instances the base scene.
    disk(project, child).write_text('[gd_scene load_steps=2 format=3]\n\n'
                                    f'[ext_resource type="PackedScene" path="{base}" id="1_base"]\n\n'
                                    '[node name="Child" instance=ExtResource("1_base")]\n', encoding="utf-8")
    must("H12 rescan", mutate(host, "editor | call Reimport()"))
    must("H12 open", mutate(host, f"editor | call OpenScene({q(child)})"))
    must("H12 a property of a base node", mutate(host, 'godot.find("Eye") | set OmniRange = 6'))
    for label, query_text, verb in (
            ("Free", 'godot.find("Hat") | call Free()', "removed"),
            ("rename", 'godot.find("Hat") | set name = "Cap"', "renamed"),
            ("MoveChild", 'godot.find("Hat") | call MoveChild(0)', "reordered")):
        fails(f"H12 {label} of a base node is refused", mutate(host, query_text), "inherits", base, verb)
    must("H12 own node", mutate(host, 'root | call AddChild("Node3D", "Mine")'))
    must("H12 own node can go", mutate(host, 'godot.find("Mine") | call Free()'))
    must("H12 save", mutate(host, "editor | call SaveScene()"))
    check("H12 the base override is saved", file_has(project, child, r"^omni_range = 6\.0$"), file_text(project, child))
