#!/usr/bin/env python3
"""
Live cases of what wave-4 agent runs (a seeded, messy project to audit and repair) could not do, run by scripts/smoke_rql.py
against a running editor: the elements of a Godot array property (props["events"]) as the values they hold, the undo / redo
history an agent can read before stepping it, which file loads which (files: deps, missingDeps, usedBy), how a scene instance
differs from its scene file (overrides), import options (files: importSettings), and script-update of a GDScript that declares its own class_name.

Every change is checked where it cannot lie: the .tres / .tscn / .gd text Godot wrote, plus a read-back query.
Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import math
import pathlib
import re
import struct

from smoke_rql_lib import check, disk, file_has, file_text, must, mutate, q, query, tool
from smoke_rql_models import fails, has, lacks, new_scene, node


def run(host: str, project: pathlib.Path, d: str) -> None:
    array_elements(host, project, d)
    undo_history(host, d)
    file_references(host, project, d)
    instance_overrides(host, project, d)
    class_name_update(host, project, d)
    import_settings(host, project, d)


def write(project: pathlib.Path, res_path: str, text: str) -> None:
    path = disk(project, res_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def array_elements(host: str, project: pathlib.Path, d: str) -> None:
    """A Godot array property streams the values it holds (not Variant structs), and writes through them reach the file."""
    sc = f"{d}/w4_shortcut.tres"
    write(project, sc, '[gd_resource type="Shortcut" load_steps=3 format=3]\n\n'
                       '[sub_resource type="InputEventKey" id="k1"]\nkeycode = 65\n\n'
                       '[sub_resource type="InputEventKey" id="k2"]\nkeycode = 66\n\n'
                       '[resource]\nevents = [SubResource("k1"), SubResource("k2")]\n')
    must("W4A reimport", mutate(host, f"editor | call Reimport([{q(sc)}])"))
    text = must("W4A read elements", query(host, f'godot.resource({q(sc)}) | get props["events"] | select k=(props["keycode"])'))
    lacks("W4A no Variant warning", text, "on Variant")
    has("W4A element members", text, "65", "66")
    has("W4A filter on an element member",
        must("W4A where", query(host, f'godot.resource({q(sc)}) | get props["events"] | where props["keycode"] == 66 | count')), "1")
    must("W4A write through an element",
         mutate(host, f'godot.resource({q(sc)}) | get props["events"] | where props["keycode"] == 66 | set props["keycode"] = 67'))
    check("W4A saved", file_has(project, sc, r"^keycode = 67$") and file_has(project, sc, r"^keycode = 65$"), file_text(project, sc))


def undo_history(host: str, d: str) -> None:
    """editor | select undoHistory / redoHistory list what Undo() / Redo() would step, newest first."""
    must("W4U new scene", mutate(host, f'editor | call NewScene({q(d + "/w4_undo.tscn")}, "Node3D", "U")'))
    must("W4U add", mutate(host, 'root | call AddChild("Node3D", "Alpha")'))
    must("W4U rename", mutate(host, 'godot.find("Alpha") | set name = "Beta"'))
    text = must("W4U read history", query(host, "editor | select undoHistory"))
    check("W4U newest first", 0 <= text.find("set name") < text.find("AddChild"), text)
    must("W4U undo", mutate(host, "editor | call Undo()"))
    has("W4U redo lists the undone rename", must("W4U read redo", query(host, "editor | select redoHistory")), "set name")
    lacks("W4U undo no longer lists it", must("W4U read undo", query(host, "editor | select undoHistory")), "set name")


def file_references(host: str, project: pathlib.Path, d: str) -> None:
    """files: deps (what a file loads), missingDeps (broken references), usedBy (what loads it; empty = unused)."""
    used, unused, gone, mat = f"{d}/w4_used.png", f"{d}/w4_unused.png", f"{d}/w4_gone.png", f"{d}/w4_mat.tres"
    png = bytes.fromhex("89504e470d0a1a0a0000000d4948445200000001000000010802000000907753de0000000c4944415408d763f8cfc000000301"
                        "0100c9fe92ef0000000049454e44ae426082")
    for p in (used, unused):
        path = disk(project, p)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(png)
    write(project, mat, '[gd_resource type="StandardMaterial3D" load_steps=3 format=3]\n\n'
                        f'[ext_resource type="Texture2D" path="{used}" id="1_a"]\n'
                        f'[ext_resource type="Texture2D" path="{gone}" id="2_b"]\n\n'
                        '[resource]\nalbedo_texture = ExtResource("1_a")\nnormal_texture = ExtResource("2_b")\n')
    must("W4F reimport", mutate(host, f"editor | call Reimport([{q(used)}, {q(unused)}, {q(mat)}])"))
    has("W4F deps", must("W4F read deps", query(host, f"files | where path == {q(mat)} | select deps")), used, gone)
    text = must("W4F read missing", query(host, f"files | where path == {q(mat)} | select missingDeps"))
    has("W4F missing dep", text, gone)
    lacks("W4F an existing dep is not missing", text, used)
    has("W4F usedBy", must("W4F read usedBy", query(host, f"files | where path == {q(used)} | select usedBy")), mat)
    has("W4F unused texture", must("W4F unused", query(host, f'files | where dir == {q(d + "/")} and ext == "png" and usedBy.count() == 0 | select path')),
        unused)
    lacks("W4F deps on request only", must("W4F default row", query(host, f"files | where path == {q(mat)} | select $")), "deps")


def instance_overrides(host: str, project: pathlib.Path, d: str) -> None:
    """overrides: how a scene instance differs from its scene file (a transform as position / scale; a child's material)."""
    house, level = f"{d}/w4_house.tscn", f"{d}/w4_level.tscn"
    mat_a, mat_b = f"{d}/w4_a.tres", f"{d}/w4_b.tres"
    must("W4O material a", mutate(host, f'editor | call CreateResource({q(mat_a)}, "StandardMaterial3D")'))
    must("W4O material b", mutate(host, f'editor | call CreateResource({q(mat_b)}, "StandardMaterial3D")'))
    new_scene(host, house, "Node3D", "House", [("MeshInstance3D", "Body")])
    must("W4O body", mutate(host, f'{node("Body")} | set Mesh = {{$type: "BoxMesh"}}, MaterialOverride = {q(mat_a)}, Position = "(0, 1.5, 0)"'))
    must("W4O save house", mutate(host, "editor | call SaveScene()"))
    new_scene(host, level, "Node3D", "Level", [])
    for name in ("H1", "H2", "H3"):
        must(f"W4O instance {name}", mutate(host, f'root | call InstantiateScene({q(house)}, {q(name)})'))
    must("W4O scale H2", mutate(host, f'{node("H2")} | set Scale = "(1.2, 1.2, 1.2)"'))
    must("W4O material H3", mutate(host, f'nodes | where path == "H3/Body" | set MaterialOverride = {q(mat_b)}'))
    text = must("W4O read", query(host, 'nodes | where SceneFilePath != "" | get overrides | select node property value template'))
    has("W4O scale override", text, ".|scale|(1.2, 1.2, 1.2)|(1, 1, 1)")
    has("W4O child material override", text, f"Body|material_override|")
    has("W4O template value", text, mat_a)
    lacks("W4O an untouched instance has none",
          must("W4O H1", query(host, f'{node("H1")} | get overrides | select property')), "scale", "material_override")
    must("W4O revert to the template", mutate(host, f'nodes | where path == "H3/Body" | set MaterialOverride = {q(mat_a)}'))
    lacks("W4O reverted", must("W4O H3", query(host, f'{node("H3")} | get overrides | select property')), "material_override")
    must("W4O save level", mutate(host, "editor | call SaveScene()"))
    lacks("W4O no override saved for the reverted material", file_text(project, level), mat_b)


def class_name_update(host: str, project: pathlib.Path, d: str) -> None:
    """script-update rewrites a GDScript that declares the class_name it registers; a real error names its line."""
    path = f"{d}/w4_item.gd"
    name = "W4Item" + re.sub(r"\D", "", d)[-9:]
    must("W4S create", tool(host, "script-create", {"scriptPath": path, "content": f"class_name {name}\nextends Resource\n\n@export var price: float = 0.0\n"}))
    must("W4S update keeps the class_name",
         tool(host, "script-update", {"scriptPath": path, "content": f"class_name {name}\nextends Resource\n\n@export var price: float = 1.0\n"}))
    check("W4S written", file_has(project, path, r"price: float = 1\.0"), file_text(project, path))
    fails("W4S a real error is refused with its line",
          tool(host, "script-update", {"scriptPath": path, "content": f"class_name {name}\nextends Resource\n\nfunc broken(:\n"}), "line 4")
    check("W4S file untouched by the refused update", file_has(project, path, r"price: float = 1\.0"), file_text(project, path))


def import_settings(host: str, project: pathlib.Path, d: str) -> None:
    """files: importSettings["option"] reads an imported file's .import [params]; a write changes it and re-imports the file."""
    wav = f"{d}/w4_loop.wav"
    rate, n = 8000, 800
    samples = b"".join(struct.pack("<h", int(3000 * math.sin(2 * math.pi * 220 * i / rate))) for i in range(n))
    path = disk(project, wav)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b"RIFF" + struct.pack("<I", 36 + len(samples)) + b"WAVEfmt " + struct.pack("<IHHIIHH", 16, 1, 1, rate, rate * 2, 2, 16) +
                     b"data" + struct.pack("<I", len(samples)) + samples)
    must("W4I import", mutate(host, f"editor | call Reimport([{q(wav)}])"))
    has("W4I read an option", must("W4I read", query(host, f'files | where path == {q(wav)} | select importSettings["edit/loop_mode"]')), "0")
    must("W4I set loop mode", mutate(host, f'files | where path == {q(wav)} | set importSettings["edit/loop_mode"] = 2'))
    check("W4I .import written", file_has(project, wav + ".import", r"^edit/loop_mode=2$"), file_text(project, wav + ".import"))
    has("W4I re-imported", must("W4I loop", query(host, f"godot.resource({q(wav)}) | select LoopMode")), "Forward")
    fails("W4I an unknown option names the options", mutate(host, f'files | where path == {q(wav)} | set importSettings["edit/nope"] = 1'),
          "edit/loop_mode")
    fails("W4I a native file has none", mutate(host, f'files | where path == {q(d + "/w4_mat.tres")} | set importSettings["x"] = 1'), "not an imported file")
