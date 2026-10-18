#!/usr/bin/env python3
"""
Live cases of the RQL data models beyond nodes / resources, run by scripts/smoke_rql.py against a running editor:
groups, signal connections, project settings, the input map, autoloads, global groups, TileSets, Themes, the existing
object / value / struct adapters on real Godot properties, and undo of every kind of change.

Every change is checked where it cannot lie: the .tscn / .tres / project.godot text Godot wrote, plus a read-back query.
Undo is checked through the test-only functions test.undo / test.redo (TestHooksModule; the editor must run with
GODOT_MCP_RQL_TEST_HOOKS=1, otherwise those checks are reported as SKIP). Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import pathlib
import re
import struct
import zlib

from smoke_rql_lib import check, disk, file_has, file_text, must, mutate, node_line, q, query, tool

HOOKS = {"on": False}


# ------------------------------------------------------------------ helpers


def fails(name: str, result: tuple[bool, str], *needles: str) -> str:
    """The call must fail, and its error must mention every needle (case-insensitive)."""
    ok, text = result
    low = text.lower()
    missing = [n for n in needles if n.lower() not in low]
    check(name, not ok and not missing, ("succeeded: " if ok else f"error lacks {missing}: ") + text)
    return text


def has(name: str, text: str, *needles: str) -> None:
    missing = [n for n in needles if n not in text]
    check(name, not missing, f"missing {missing} in: {text}")


def result_is(name: str, text: str, value: str) -> None:
    """A call receipt's result column (the last cell of a row) is exactly `value` (case-insensitive)."""
    check(name, re.search(rf"\|{re.escape(value)}$", text, re.M | re.I) is not None, text)


def lacks(name: str, text: str, *needles: str) -> None:
    present = [n for n in needles if n in text]
    check(name, not present, f"unexpected {present} in: {text}")


def undo(host: str, history: str) -> bool:
    """Undo the last action of a history ("scene" / "global") through the test hooks; False (and a SKIP) without them."""
    if not HOOKS["on"]:
        return False
    ok, text = query(host, f"test.undo({q(history)})")
    check(f"undo ({history}) ran", ok and "true" in text.lower(), text)
    return ok


def redo(host: str, history: str) -> bool:
    if not HOOKS["on"]:
        return False
    ok, text = query(host, f"test.redo({q(history)})")
    check(f"redo ({history}) ran", ok and "true" in text.lower(), text)
    return ok


def skip_undo(name: str) -> None:
    print(f"[SKIP] {name} -- undo checks need GODOT_MCP_RQL_TEST_HOOKS=1 in the editor", flush=True)


def ini_section(project: pathlib.Path, section: str) -> str:
    """The text of one [section] of project.godot (empty when it is absent)."""
    text = (project / "project.godot").read_text(encoding="utf-8")
    m = re.search(rf"^\[{re.escape(section)}\]\s*$(.*?)(?=^\[[A-Za-z0-9_]+\]\s*$|\Z)", text, re.M | re.S)
    return m.group(1) if m else ""


def setting_line(project: pathlib.Path, path: str) -> str | None:
    """The raw value project.godot holds for a setting path ("display/window/size/viewport_width"), or None."""
    section, _, key = path.partition("/")
    m = re.search(rf"^{re.escape(key)}=(.*?)(?=^\S+=|\Z)", ini_section(project, section), re.M | re.S)
    return m.group(1).strip() if m else None


def write_png(path: pathlib.Path, width: int, height: int, opaque: set[tuple[int, int]], cell: int) -> None:
    """An RGBA PNG whose cell-sized squares in `opaque` are white and opaque, the rest fully transparent."""
    rows = bytearray()
    for y in range(height):
        rows.append(0)
        for x in range(width):
            on = (x // cell, y // cell) in opaque
            rows += bytes((255, 255, 255, 255)) if on else bytes((0, 0, 0, 0))

    def chunk(kind: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(rows))) + chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


def new_scene(host: str, path: str, root_class: str, root_name: str, children: list[tuple[str, str]]) -> None:
    must(f"{root_name} NewScene", mutate(host, f"editor | call NewScene({q(path)}, {q(root_class)}, {q(root_name)})"))
    for cls, name in children:
        must(f"{root_name} AddChild {name}", mutate(host, f"root | call AddChild({q(cls)}, {q(name)})"))


def node(name: str) -> str:
    return f"nodes | where name == {q(name)}"


# ------------------------------------------------------------------ cases


def run(host: str, project: pathlib.Path, d: str) -> None:
    ok, _ = query(host, 'test.action("global")')
    HOOKS["on"] = ok
    print(f"[INFO] undo test hooks {'on' if ok else 'OFF (set GODOT_MCP_RQL_TEST_HOOKS=1)'}", flush=True)
    schema(host)
    existing_adapters(host, project, d)
    undo_of_node_edits(host, project, d)
    groups(host, project, d)
    signals(host, project, d)
    project_settings(host, project)
    input_map(host, project)
    autoloads(host, project, d)
    global_groups(host, project)
    tilesets(host, project, d)
    themes(host, project, d)


def schema(host: str) -> None:
    text = must("M0 rql-schema overview", tool(host, "rql-schema", {}))
    for source in ("project", "settings", "inputActions", "autoloads", "groups"):
        check(f"M0 schema lists source '{source}'", f"  {source} —" in text, text[:600])
    check("M0 schema lists godot.group", "godot.group(" in text, text[:600])
    text = must("M0 rql-schema Node", tool(host, "rql-schema", {"type": "Node"}))
    has("M0 Node lists groups, signals, connections and the new calls", text, "groups", "signals", "connections", "incoming",
        "Connect", "Disconnect", "AddToGroup", "RemoveFromGroup")
    for typ, needles in (("TileSet", ("sources", "AddAtlasSource", "AddTerrainSet", "AddCustomDataLayer")),
                         ("TileSetAtlasSource", ("tiles", "CreateTile", "CreateTilesFromTexture")),
                         ("Theme", ("colors", "constants", "styleboxes", "ClearItem")),
                         ("project", ("settings", "AddInputAction", "AddAutoload"))):
        text = must(f"M0 rql-schema {typ}", tool(host, "rql-schema", {"type": typ}))
        has(f"M0 {typ} members", text, *needles)


def existing_adapters(host: str, project: pathlib.Path, d: str) -> None:
    """Node2D / Node3D / Control / Resource / NodePath / StringName / Variant / NodeRef and the struct adapters, on real properties."""
    # Node2D + 2D structs.
    s2 = f"{d}/adapters2d.tscn"
    new_scene(host, s2, "Node2D", "Root2D", [("Sprite2D", "Sprite"), ("Node2D", "Holder")])
    sprite = node("Sprite")
    must("E1 Node2D members", mutate(host, f'{sprite} | set Position = "(10, 20)", RotationDegrees = 90, Scale = "(2, 3)", Skew = 0.5'))
    text = must("E1 read back", query(host, f"{sprite} | select Position Rotation RotationDegrees Scale Skew GlobalPosition"))
    has("E1 Node2D values", text, "(10, 20)", "1.5707", "(2, 3)", "0.5")
    must("E2 Vector2I frame_coords + Rect2 region + hex Color", mutate(
        host, f'{sprite} | set props["hframes"] = 4, props["vframes"] = 2, props["frame_coords"] = "(2, 1)", '
              f'props["region_enabled"] = true, props["region_rect"] = "(1, 2, 30, 40)", Modulate = "#336699"'))
    text = must("E2 read back", query(host, f'{sprite} | select fc=(props["frame_coords"]) rr=(props["region_rect"]) m=(Modulate)'))
    has("E2 values", text, "(2, 1)", "(1, 2, 30, 40)", "(0.2, 0.4, 0.6, 1)")
    fails("E2 Vector2I refuses a fraction", mutate(host, f'{sprite} | set props["frame_coords"] = "(1.5, 1)"'))
    must("E3 Transform2D", mutate(host, f'{node("Holder")} | set Transform = "(1, 0, 0, 1, 3, 4)"'))
    text = must("E3 read back", query(host, f'{node("Holder")} | select Position'))
    has("E3 Position follows the transform", text, "(3, 4)")
    must("E3 save", mutate(host, "editor | call SaveScene()"))
    for name, pattern in (("position", r"^position = Vector2\(10, 20\)$"), ("rotation", r"^rotation = 1\.5707\d*$"),
                          ("scale", r"^scale = Vector2\(2, 3\)$"), ("skew", r"^skew = 0\.5$"),
                          ("frame (from frame_coords (2, 1) of 4 columns)", r"^frame = 6$"),
                          ("region_rect", r"^region_rect = Rect2\(1, 2, 30, 40\)$"),
                          ("modulate", r"^modulate = Color\(0\.2, 0\.4, 0\.6, 1\)$"),
                          ("Holder position", r"^position = Vector2\(3, 4\)$")):
        check(f"E1-3 file has {name}", file_has(project, s2, pattern), file_text(project, s2))

    # Node3D + 3D structs, Plane (WorldBoundaryShape3D), Aabb, Basis, Quaternion.
    s3 = f"{d}/adapters3d.tscn"
    new_scene(host, s3, "Node3D", "Root3D", [("Node3D", "Parent"), ("MeshInstance3D", "Mesh"), ("StaticBody3D", "Floor"),
                                             ("RemoteTransform3D", "Remote"), ("AudioStreamPlayer", "Audio")])
    must("E4 Parent offset", mutate(host, f'{node("Parent")} | set Position = "(1, 0, 0)"'))
    must("E4 child of Parent", mutate(host, f'{node("Parent")} | call AddChild("Node3D", "Child")'))
    must("E4 GlobalPosition", mutate(host, f'{node("Child")} | set GlobalPosition = "(5, 0, 0)"'))
    text = must("E4 read back", query(host, f'{node("Child")} | select Position GlobalPosition'))
    has("E4 local position = global - parent", text, "(4, 0, 0)", "(5, 0, 0)")
    must("E5 RotationDegrees", mutate(host, f'{node("Mesh")} | set RotationDegrees = "(0, 90, 0)"'))
    text = must("E5 Quaternion read", query(host, f'{node("Mesh")} | select Quaternion'))
    has("E5 quaternion of 90 degrees about Y", text, "0.7071")
    must("E5 Quaternion write", mutate(host, f'{node("Mesh")} | set Quaternion = "(0, 0, 0, 1)", Visible = false, '
                                             f'props["custom_aabb"] = "(0, 0, 0, 1, 2, 3)"'))
    text = must("E5 read back", query(host, f'{node("Mesh")} | select RotationDegrees Visible a=(props["custom_aabb"])'))
    has("E5 values", text, "(0, 0, 0)", "false", "(0, 0, 0, 1, 2, 3)")
    must("E6 Basis", mutate(host, f'{node("Parent")} | set Basis = "(2, 0, 0, 0, 2, 0, 0, 0, 2)"'))
    text = must("E6 read back", query(host, f'{node("Parent")} | select Scale'))
    has("E6 Scale from the basis", text, "(2, 2, 2)")
    must("E7 CollisionShape3D + WorldBoundaryShape3D", mutate(host, f'{node("Floor")} | call AddChild("CollisionShape3D", "Shape")'))
    must("E7 shape resource", mutate(host, f'{node("Shape")} | set props["shape"] = {{"$type": "WorldBoundaryShape3D"}}'))
    ok, text = query(host, f'{node("Shape")} | select c=(props["shape"].class)')
    if "WorldBoundaryShape3D" not in text:
        # Object literals with $type may not construct a Godot resource: create one as a file instead.
        plane_res = f"{d}/boundary.tres"
        must("E7 CreateResource WorldBoundaryShape3D", mutate(host, f'editor | call CreateResource({q(plane_res)}, "WorldBoundaryShape3D")'))
        must("E7 assign shape", mutate(host, f'{node("Shape")} | set props["shape"] = {q(plane_res)}'))
    must("E7 Plane", mutate(host, f'{node("Shape")} | set props["shape"].Plane = "(0, 1, 0, 2)"'))
    text = must("E7 read back", query(host, f'{node("Shape")} | select p=(props["shape"].Plane)'))
    has("E7 Plane value", text, "(0, 1, 0, 2)")
    fails("E7 Plane needs four numbers", mutate(host, f'{node("Shape")} | set props["shape"].Plane = "(0, 1, 0)"'), "(a, b, c, d)")
    must("E8 NodePath", mutate(host, f'{node("Remote")} | set RemotePath = "../Mesh"'))
    text = must("E8 read back", query(host, f'{node("Remote")} | select RemotePath'))
    has("E8 NodePath as text", text, "../Mesh")
    action = f"{d}/adapters_action.tres"
    must("E9 InputEventAction", mutate(host, f'editor | call CreateResource({q(action)}, "InputEventAction")'))
    must("E9 StringName", mutate(host, f'godot.resource({q(action)}) | set Action = "rql_fire"'))
    text = must("E9 read back", query(host, f"godot.resource({q(action)}) | select Action"))
    has("E9 StringName as text", text, "rql_fire")
    check("E9 file has the StringName", file_has(project, action, r'^action = &"rql_fire"$'), file_text(project, action))
    must("E10 Variant (metadata)", mutate(host, f'{node("Audio")} | call CallMethod("set_meta", ["note", "hello"])'))
    text = must("E10 read back", query(host, f'{node("Audio")} | select n=(props["metadata/note"])'))
    has("E10 metadata value", text, "hello")
    must("E10 save", mutate(host, "editor | call SaveScene()"))
    for name, pattern in (("child position", r"^transform = Transform3D\(1, 0, 0, 0, 1, 0, 0, 0, 1, 4, 0, 0\)$"),
                          ("visible", r"^visible = false$"),
                          ("custom_aabb", r"^custom_aabb = AABB\(0, 0, 0, 1, 2, 3\)$"),
                          ("parent basis", r"^transform = Transform3D\(2, 0, 0, 0, 2, 0, 0, 0, 2, 1, 0, 0\)$"),
                          ("remote_path", r'^remote_path = NodePath\("\.\./Mesh"\)$'),
                          ("metadata", r'^metadata/note = "hello"$')):
        check(f"E4-10 file has {name}", file_has(project, s3, pattern), file_text(project, s3))
    plane_files = [file_text(project, s3)] + [file_text(project, f"{d}/boundary.tres")]
    check("E7 file has plane", any(re.search(r"^plane = Plane\(0, 1, 0, 2\)$", t, re.M) for t in plane_files), plane_files[0])

    # Control.
    ui = f"{d}/adapters_ui.tscn"
    new_scene(host, ui, "Control", "UI", [("Button", "Btn")])
    btn = node("Btn")
    must("E11 Control", mutate(host, f'{btn} | set Position = "(5, 6)", Size = "(100, 40)", SizeFlagsHorizontal = 3, '
                                     f'props["theme_override_colors/font_color"] = "#ff0000", Text = "Go"'))
    text = must("E11 read back", query(host, f'{btn} | select Position Size l=(OffsetLeft) r=(OffsetRight) t=(Text)'))
    has("E11 values", text, "(5, 6)", "(100, 40)", "105", "Go")
    must("E11 anchors", mutate(host, f"{btn} | set AnchorRight = 1"))
    must("E11 save", mutate(host, "editor | call SaveScene()"))
    for name, pattern in (("offset_left", r"^offset_left = 5\.0$"), ("offset_top", r"^offset_top = 6\.0$"),
                          ("size_flags_horizontal", r"^size_flags_horizontal = 3$"), ("anchor_right", r"^anchor_right = 1\.0$"),
                          ("font color override", r"^theme_override_colors/font_color = Color\(1, 0, 0, 1\)$"),
                          ("text", r'^text = "Go"$')):
        check(f"E11 file has {name}", file_has(project, ui, pattern), file_text(project, ui))

    # Resource: path, uid, Save().
    mat = f"{d}/adapters_mat.tres"
    must("E12 CreateResource", mutate(host, f'editor | call CreateResource({q(mat)}, "StandardMaterial3D")'))
    text = must("E12 path + uid", query(host, f"godot.resource({q(mat)}) | select path uid ResourcePath"))
    has("E12 path", text, mat)
    has("E12 uid", text, "uid://")
    fails("E12 ResourcePath is read-only", mutate(host, f'godot.resource({q(mat)}) | set ResourcePath = "res://x.tres"'))
    text = must("E12 Save()", mutate(host, f"godot.resource({q(mat)}) | call Save()"))
    has("E12 Save returns the path", text, mat)

    # NodeRef tool arguments: an instance id, ref("…") and godot.find("…").
    script = f"{d}/noderef.gd"
    must("E13 script-create", tool(host, "script-create", {"scriptPath": script, "content": "extends Button\n"}))
    ok, text = query(host, f"{btn} | select id")
    ids = re.findall(r"\b\d{5,}\b", text)
    check("E13 node id read", bool(ids), text)
    if ids:
        must("E13 attach by instance id", tool(host, "script-attach-to-node", {"nodeRef": ids[-1], "scriptPath": script}))
        text = must("E13 attached", query(host, f"{btn} | select script"))
        has("E13 script path", text, script)
    must("E13 detach by godot.find", tool(host, "script-attach-to-node", {"nodeRef": 'godot.find("Btn")', "scriptPath": ""}))
    text = must("E13 detached", query(host, f"{btn} | select script"))
    lacks("E13 no script", text, script)
    fails("E13 unknown node", tool(host, "script-attach-to-node", {"nodeRef": "NoSuchNode", "scriptPath": script}))


def undo_of_node_edits(host: str, project: pathlib.Path, d: str) -> None:
    """Every kind of node edit is one undo action that restores the scene exactly (and redo re-applies it)."""
    if not HOOKS["on"]:
        skip_undo("U node edits")
        return
    sc = f"{d}/undo.tscn"
    new_scene(host, sc, "Node3D", "UndoRoot", [("Node3D", "A"), ("Node3D", "B"), ("Node3D", "C")])
    must("U0 save", mutate(host, "editor | call SaveScene()"))

    must("U1 set", mutate(host, f'{node("A")} | set Position = "(1, 2, 3)", name = "A2"'))
    undo(host, "scene")
    text = must("U1 read", query(host, "nodes | select name Position"))
    has("U1 undo restores name", text, "A")
    lacks("U1 undo restores name (no A2)", text, "A2")
    lacks("U1 undo restores position", text, "(1, 2, 3)")
    redo(host, "scene")
    text = must("U1 redo read", query(host, f'{node("A2")} | select Position'))
    has("U1 redo re-applies", text, "(1, 2, 3)")
    undo(host, "scene")

    must("U2 AddChild", mutate(host, f'{node("B")} | call AddChild("OmniLight3D", "Lamp")'))
    undo(host, "scene")
    text = must("U2 read", query(host, "nodes | select name"))
    lacks("U2 undo removes the new node", text, "Lamp")

    must("U3 Free", mutate(host, f'{node("C")} | call Free()'))
    undo(host, "scene")
    text = must("U3 read", query(host, "nodes | select name"))
    has("U3 undo restores the freed node", text, "C")

    must("U4 Reparent", mutate(host, f'{node("C")} | call Reparent(godot.find("B"))'))
    undo(host, "scene")
    text = must("U4 read", query(host, f'{node("C")} | select path'))
    has("U4 undo restores the parent", text, "C")
    lacks("U4 not under B", text, "B/C")

    must("U5 MoveChild", mutate(host, f'{node("C")} | call MoveChild(0)'))
    undo(host, "scene")
    text = must("U5 read", query(host, f'{node("C")} | select index'))
    has("U5 undo restores the index", text, "2")

    must("U6 Duplicate", mutate(host, f'{node("B")} | call Duplicate()'))
    undo(host, "scene")
    text = must("U6 read", query(host, "nodes | count"))
    has("U6 undo removes the copy", text, "4")

    must("U7 save after undo", mutate(host, "editor | call SaveScene()"))
    scene = file_text(project, sc)
    check("U7 file has A, no Lamp", re.search(node_line("A", ".", "Node3D"), scene, re.M) is not None and "Lamp" not in scene, scene)
    check("U7 A has no transform (undo restored the origin)", re.search(r"^transform = ", scene, re.M) is None, scene)


def groups(host: str, project: pathlib.Path, d: str) -> None:
    sc = f"{d}/groups.tscn"
    new_scene(host, sc, "Node3D", "GroupRoot", [("Node3D", "GA"), ("Node3D", "GB"), ("Node3D", "GC")])
    text = must("GR1 AddToGroup", mutate(host, f'{node("GA")} | call AddToGroup("enemies")'))
    result_is("GR1 returns true", text, "true")
    text = must("GR1 again", mutate(host, f'{node("GA")} | call AddToGroup("enemies")'))
    result_is("GR1 again returns false", text, "false")
    must("GR2 set groups", mutate(host, f'{node("GB")} | set groups = ["enemies", "loot"]'))
    text = must("GR2 read", query(host, f'{node("GB")} | select groups'))
    has("GR2 groups", text, "enemies", "loot")
    text = must("GR3 godot.group", query(host, 'godot.group("enemies") | select name'))
    has("GR3 members", text, "GA", "GB")
    lacks("GR3 non-member", text, "GC")
    text = must("GR3 groups source", query(host, 'groups | where name == "enemies" | select count global'))
    has("GR3 count", text, "2")
    text = must("GR3 where on groups", query(host, 'nodes | where groups.count() > 0 | select name'))
    has("GR3 where", text, "GA", "GB")
    text = must("GR4 RemoveFromGroup", mutate(host, f'{node("GB")} | call RemoveFromGroup("loot")'))
    result_is("GR4 returns true", text, "true")
    text = must("GR4 RemoveFromGroup missing", mutate(host, f'{node("GC")} | call RemoveFromGroup("loot")'))
    result_is("GR4 missing returns false", text, "false")
    fails("GR5 a group name cannot contain '/'", mutate(host, f'{node("GC")} | call AddToGroup("a/b")'), "/")
    fails("GR5 set groups refuses a bad name", mutate(host, f'{node("GC")} | set groups = ["ok", "x/y"]'), "/")
    text = must("GR5 nothing applied", query(host, f'{node("GC")} | select groups'))
    lacks("GR5 no partial groups", text, "ok")
    text = must("GR6 dry run", mutate(host, f'{node("GC")} | call AddToGroup("dry")', dry_run=True))
    text = must("GR6 read", query(host, f'{node("GC")} | select groups'))
    lacks("GR6 dry run changed nothing", text, "dry")
    if HOOKS["on"]:
        must("GR7 add for undo", mutate(host, f'{node("GC")} | call AddToGroup("temp")'))
        undo(host, "scene")
        text = must("GR7 read", query(host, f'{node("GC")} | select groups'))
        lacks("GR7 undo removed the group", text, "temp")
        must("GR7 set for undo", mutate(host, f'{node("GA")} | set groups = []'))
        undo(host, "scene")
        text = must("GR7 read set", query(host, f'{node("GA")} | select groups'))
        has("GR7 undo restored the groups", text, "enemies")
    else:
        skip_undo("GR7")
    must("GR8 save", mutate(host, "editor | call SaveScene()"))
    scene = file_text(project, sc)
    check("GR8 GA saved with its group", re.search(r'^\[node name="GA"[^\]]*groups=\["enemies"\]', scene, re.M) is not None, scene)
    check("GR8 GB saved with its group", re.search(r'^\[node name="GB"[^\]]*groups=\["enemies"\]', scene, re.M) is not None, scene)
    check("GR8 GC saved without groups", re.search(r'^\[node name="GC"[^\]]*groups=', scene, re.M) is None, scene)


def signals(host: str, project: pathlib.Path, d: str) -> None:
    sc = f"{d}/signals.tscn"
    new_scene(host, sc, "Node3D", "SigRoot", [("Timer", "Clock"), ("Node3D", "Receiver"), ("Node3D", "Other")])
    receiver = f"{d}/receiver.gd"
    must("SG0 script", tool(host, "script-create", {"scriptPath": receiver,
                                                    "content": "extends Node3D\n\nsignal hit(amount: int)\n\nfunc _on_ping():\n\tpass\n\nfunc _on_tick():\n\tpass\n"}))
    must("SG0 attach", tool(host, "script-attach-to-node", {"nodeRef": "Receiver", "scriptPath": receiver}))
    text = must("SG1 signals", query(host, f'{node("Clock")} | get signals | select name args'))
    has("SG1 Timer signals", text, "timeout", "tree_entered")
    text = must("SG1 script signal", query(host, f'{node("Receiver")} | get signals | where name == "hit" | select args'))
    has("SG1 script signal args", text, "amount: Int")
    text = must("SG2 Connect", mutate(host, f'{node("Clock")} | call Connect("timeout", godot.find("Receiver"), "_on_ping")'))
    has("SG2 receipt", text, "timeout")
    text = must("SG2 connections", query(host, f'{node("Clock")} | get connections | select signal target method deferred oneShot'))
    has("SG2 connection row", text, "timeout", "_on_ping", "false")
    text = must("SG2 incoming", query(host, f'{node("Receiver")} | get incoming | select source signal method'))
    has("SG2 incoming row", text, "timeout", "_on_ping")
    text = must("SG2 signal row counts it", query(host, f'{node("Clock")} | get signals | where name == "timeout" | select connections'))
    has("SG2 count", text, "1")
    fails("SG3 already connected", mutate(host, f'{node("Clock")} | call Connect("timeout", godot.find("Receiver"), "_on_ping")'), "already")
    fails("SG4 missing method", mutate(host, f'{node("Clock")} | call Connect("timeout", godot.find("Receiver"), "_nope")'), "no method")
    fails("SG5 unknown signal", mutate(host, f'{node("Clock")} | call Connect("timeot", godot.find("Receiver"), "_on_ping")'), "no signal", "timeout")
    fails("SG5 target outside the scene", mutate(host, f'{node("Clock")} | call Connect("timeout", "NoSuchNode", "_on_ping")'))
    must("SG6 flags", mutate(host, f'{node("Clock")} | call Connect("renamed", godot.find("Receiver"), "_on_tick", true, true)'))
    text = must("SG6 read", query(host, f'{node("Clock")} | get connections | where signal == "renamed" | select deferred oneShot flags'))
    has("SG6 deferred + one-shot", text, "true")
    must("SG7 built-in method target", mutate(host, f'{node("Clock")} | call Connect("timeout", godot.find("Other"), "hide")'))
    must("SG8 Disconnect via the row", mutate(host, f'{node("Clock")} | get connections | where method == "hide" | call Disconnect()'))
    text = must("SG8 read", query(host, f'{node("Clock")} | get connections | select method'))
    lacks("SG8 gone", text, "hide")
    fails("SG8 Disconnect twice", mutate(host, f'{node("Clock")} | call Disconnect("timeout", godot.find("Other"), "hide")'), "not connected")
    if HOOKS["on"]:
        must("SG9 connect for undo", mutate(host, f'{node("Other")} | call Connect("visibility_changed", godot.find("Receiver"), "_on_tick")'))
        undo(host, "scene")
        text = must("SG9 read", query(host, f'{node("Other")} | get connections | select method'))
        lacks("SG9 undo removed the connection", text, "_on_tick")
        redo(host, "scene")
        text = must("SG9 redo read", query(host, f'{node("Other")} | get connections | select method'))
        has("SG9 redo re-connected", text, "_on_tick")
        must("SG9 disconnect for undo", mutate(host, f'{node("Other")} | call Disconnect("visibility_changed", godot.find("Receiver"), "_on_tick")'))
        undo(host, "scene")
        text = must("SG9 read after undoing the disconnect", query(host, f'{node("Other")} | get connections | select method'))
        has("SG9 undo restored the connection", text, "_on_tick")
        must("SG9 cleanup", mutate(host, f'{node("Other")} | call Disconnect("visibility_changed", godot.find("Receiver"), "_on_tick")'))
    else:
        skip_undo("SG9")
    must("SG10 save", mutate(host, "editor | call SaveScene()"))
    scene = file_text(project, sc)
    check("SG10 timeout connection saved",
          re.search(r'^\[connection signal="timeout" from="Clock" to="Receiver" method="_on_ping"\]$', scene, re.M) is not None, scene)
    check("SG10 flagged connection saved",
          re.search(r'^\[connection signal="renamed" from="Clock" to="Receiver" method="_on_tick" flags=\d+\]$', scene, re.M) is not None, scene)
    check("SG10 disconnected ones not saved", 'method="hide"' not in scene and 'signal="visibility_changed"' not in scene, scene)
    # Re-open the scene: the connections come back from the file.
    must("SG11 reopen", mutate(host, f"editor | call OpenScene({q(sc)})"))
    text = must("SG11 read", query(host, f'{node("Clock")} | get connections | select signal method'))
    has("SG11 connections survive a reload", text, "_on_ping", "_on_tick")


def project_settings(host: str, project: pathlib.Path) -> None:
    text = must("PS1 settings source", query(host, 'settings | where name == "application/config/name" | select value type section'))
    has("PS1 row", text, "String", "application")
    width = "display/window/size/viewport_width"
    must("PS2 set int setting", mutate(host, f'project | set settings[{q(width)}] = 1280'))
    check("PS2 project.godot", setting_line(project, width) == "1280", ini_section(project, "display"))
    text = must("PS2 read back", query(host, f'settings | where name == {q(width)} | select value changed'))
    has("PS2 value + changed", text, "1280", "true")
    color = "rendering/environment/defaults/default_clear_color"
    must("PS3 typed (Color from hex)", mutate(host, f'project | set settings[{q(color)}] = "#336699"'))
    check("PS3 project.godot", (setting_line(project, color) or "").startswith("Color(0.2, 0.4, 0.6, 1"), ini_section(project, "rendering"))
    fails("PS4 unknown setting", mutate(host, 'project | set settings["no/such/setting"] = 1'), "SetSetting")
    fails("PS4 wrong type", mutate(host, f'project | set settings[{q(width)}] = "wide"'))
    check("PS4 nothing changed", setting_line(project, width) == "1280", ini_section(project, "display"))
    before = (project / "project.godot").stat().st_mtime_ns
    must("PS5 dry run", mutate(host, f'project | set settings[{q(width)}] = 1999', dry_run=True))
    check("PS5 dry run did not write", (project / "project.godot").stat().st_mtime_ns == before and setting_line(project, width) == "1280")
    must("PS6 SetSetting custom", mutate(host, 'project | call SetSetting("rql_smoke/difficulty/level", 3)'))
    check("PS6 custom setting saved", setting_line(project, "rql_smoke/difficulty/level") == "3", ini_section(project, "rql_smoke"))
    must("PS6 SetSetting same type", mutate(host, 'project | call SetSetting("rql_smoke/difficulty/level", 4)'))
    fails("PS6 SetSetting other type", mutate(host, 'project | call SetSetting("rql_smoke/difficulty/level", "hard")'), "settings[")
    text = must("PS6 typed write through the map", mutate(host, 'project | set settings["rql_smoke/difficulty/level"] = 5'))
    check("PS6 custom setting updated", setting_line(project, "rql_smoke/difficulty/level") == "5", ini_section(project, "rql_smoke"))
    must("PS7 ClearSetting custom", mutate(host, 'project | call ClearSetting("rql_smoke/difficulty/level")'))
    check("PS7 custom setting removed", setting_line(project, "rql_smoke/difficulty/level") is None, ini_section(project, "rql_smoke"))
    fails("PS7 ClearSetting unknown", mutate(host, 'project | call ClearSetting("rql_smoke/none")'), "no project setting")
    must("PS8 ClearSetting built-in returns it to its default", mutate(host, f'project | call ClearSetting({q(color)})'))
    check("PS8 default not written", setting_line(project, color) is None, ini_section(project, "rendering"))
    text = must("PS9 name + mainScene", query(host, "project | select name mainScene"))
    original_name = setting_line(project, "application/config/name")
    must("PS9 set name", mutate(host, 'project | set name = "RQL Smoke Project"'))
    check("PS9 name saved", setting_line(project, "application/config/name") == '"RQL Smoke Project"', ini_section(project, "application"))
    fails("PS9 empty name", mutate(host, 'project | set name = ""'), "empty")
    fails("PS9 mainScene must exist", mutate(host, 'project | set mainScene = "res://no/such.tscn"'), "no scene")
    if original_name is not None:
        must("PS9 restore name", mutate(host, f"project | set name = {original_name}"))
    if HOOKS["on"]:
        must("PS10 change for undo", mutate(host, f'project | set settings[{q(width)}] = 1600'))
        check("PS10 saved", setting_line(project, width) == "1600")
        undo(host, "global")
        check("PS10 undo restored the file", setting_line(project, width) == "1280", ini_section(project, "display"))
        text = must("PS10 read", query(host, f'settings | where name == {q(width)} | select value'))
        has("PS10 undo restored the value", text, "1280")
        redo(host, "global")
        check("PS10 redo re-applied the file", setting_line(project, width) == "1600", ini_section(project, "display"))
        must("PS10 custom for undo", mutate(host, 'project | call SetSetting("rql_smoke/undo/flag", true)'))
        undo(host, "global")
        check("PS10 undo removed a new custom setting", setting_line(project, "rql_smoke/undo/flag") is None, ini_section(project, "rql_smoke"))
    else:
        skip_undo("PS10")
    must("PS11 restore width", mutate(host, f'project | set settings[{q(width)}] = 1152'))


def input_map(host: str, project: pathlib.Path) -> None:
    must("IN0 cleanup", mutate(host, 'inputActions | where name starts-with "rql_" | call Remove()'))
    must("IN1 AddInputAction", mutate(host, 'project | call AddInputAction("rql_jump", ["Key:Space", "JoypadButton:A"])'))
    raw = setting_line(project, "input/rql_jump") or ""
    check("IN1 saved with a key event", "InputEventKey" in raw and '"keycode":32' in raw.replace(" ", ""), raw or ini_section(project, "input"))
    check("IN1 saved with a joypad event", "InputEventJoypadButton" in raw and '"button_index":0' in raw.replace(" ", ""), raw)
    check("IN1 saved with all devices", '"device":-1' in raw.replace(" ", ""), raw)
    text = must("IN2 events", query(host, 'inputActions | where name == "rql_jump" | select events deadzone builtIn'))
    has("IN2 event texts", text, "Key:Space", "JoypadButton:A", "false")
    jump = 'inputActions | where name == "rql_jump"'
    for event in ("Mouse:Left", "Mouse:Shift+WheelUp", "JoypadMotion:LeftY-", "JoypadMotion:TriggerRight", "PhysicalKey:W", "Key:Ctrl+S", "JoypadButton:12"):
        must(f"IN3 AddEvent {event}", mutate(host, f"{jump} | call AddEvent({q(event)})"))
    text = must("IN3 read back", query(host, f"{jump} | select events"))
    has("IN3 canonical texts", text, "Mouse:Left", "Mouse:Shift+WheelUp", "JoypadMotion:LeftY-", "JoypadMotion:TriggerRight+",
        "PhysicalKey:W", "Ctrl+S", "JoypadButton:DpadDown")
    raw = setting_line(project, "input/rql_jump") or ""
    check("IN3 saved physical key", '"physical_keycode":87' in raw.replace(" ", ""), raw)
    check("IN3 saved ctrl modifier", '"ctrl_pressed":true' in raw.replace(" ", ""), raw)
    check("IN3 saved axis", '"axis":1' in raw.replace(" ", "") and '"axis_value":-1' in raw.replace(" ", ""), raw)
    fails("IN4 duplicate event", mutate(host, f'{jump} | call AddEvent("key:space")'), "already")
    fails("IN4 unknown key", mutate(host, f'{jump} | call AddEvent("Key:NoSuchKey")'), "unknown key")
    fails("IN4 unknown kind", mutate(host, f'{jump} | call AddEvent("Bogus:1")'), "kind")
    fails("IN4 stick without direction", mutate(host, f'{jump} | call AddEvent("JoypadMotion:LeftX")'), "direction")
    fails("IN4 unknown mouse button", mutate(host, f'{jump} | call AddEvent("Mouse:Thumb")'), "mouse button")
    must("IN5 deadzone", mutate(host, f"{jump} | set deadzone = 0.35"))
    check("IN5 deadzone saved", '"deadzone":0.35' in (setting_line(project, "input/rql_jump") or "").replace(" ", ""))
    fails("IN5 deadzone range", mutate(host, f"{jump} | set deadzone = 2"), "between 0 and 1")
    must("IN6 replace events", mutate(host, f'{jump} | set events = ["Key:Enter", "Mouse:Right"]'))
    text = must("IN6 read", query(host, f"{jump} | select events deadzone"))
    has("IN6 events replaced", text, "Key:Enter", "Mouse:Right", "0.35")
    lacks("IN6 old events gone", text, "Key:Space")
    fails("IN6 replace with a bad event changes nothing", mutate(host, f'{jump} | set events = ["Key:Q", "Nope:1"]'))
    text = must("IN6 read after the failure", query(host, f"{jump} | select events"))
    has("IN6 events kept", text, "Key:Enter")
    must("IN7 RemoveEvent", mutate(host, f'{jump} | call RemoveEvent("Mouse:Right")'))
    must("IN7 project RemoveInputEvent", mutate(host, 'project | call RemoveInputEvent("rql_jump", "Key:Enter")'))
    text = must("IN7 read", query(host, f"{jump} | select events"))
    lacks("IN7 removed", text, "Mouse:Right", "Key:Enter")
    fails("IN7 remove missing", mutate(host, f'{jump} | call RemoveEvent("Key:Z")'), "no event")
    must("IN7 project AddInputEvent", mutate(host, 'project | call AddInputEvent("rql_jump", "Key:Up")'))
    fails("IN8 duplicate action", mutate(host, 'project | call AddInputAction("rql_jump")'), "already exists")
    fails("IN8 unknown action", mutate(host, 'project | call AddInputEvent("rql_nope", "Key:Up")'), "no input action")
    text = must("IN9 built-in actions", query(host, 'inputActions | where name == "ui_accept" | select builtIn events'))
    has("IN9 ui_accept", text, "true", "Key:")
    must("IN10 action without events, custom deadzone", mutate(host, 'project | call AddInputAction("rql_empty", [], 0.75)'))
    text = must("IN10 read", query(host, 'inputActions | where name == "rql_empty" | select deadzone events'))
    has("IN10 deadzone", text, "0.75")
    if HOOKS["on"]:
        must("IN11 remove for undo", mutate(host, 'project | call RemoveInputAction("rql_jump")'))
        check("IN11 removed from the file", setting_line(project, "input/rql_jump") is None, ini_section(project, "input"))
        undo(host, "global")
        check("IN11 undo restored the action in the file", "InputEventKey" in (setting_line(project, "input/rql_jump") or ""), ini_section(project, "input"))
        text = must("IN11 read", query(host, "inputActions | where name == \"rql_jump\" | select events"))
        has("IN11 undo restored its events", text, "Key:Up")
        must("IN11 event for undo", mutate(host, f'{jump} | call AddEvent("Key:Down")'))
        undo(host, "global")
        text = must("IN11 read event undo", query(host, f"{jump} | select events"))
        lacks("IN11 undo removed the event", text, "Key:Down")
    else:
        skip_undo("IN11")
    must("IN12 Remove rows", mutate(host, 'inputActions | where name starts-with "rql_" | call Remove()'))
    check("IN12 removed from the file", setting_line(project, "input/rql_jump") is None and setting_line(project, "input/rql_empty") is None,
          ini_section(project, "input"))


def autoloads(host: str, project: pathlib.Path, d: str) -> None:
    must("AL0 cleanup", mutate(host, 'autoloads | where name starts-with "Rql" | call Remove()'))
    script = f"{d}/rql_state.gd"
    must("AL0 script", tool(host, "script-create", {"scriptPath": script, "content": "extends Node\n"}))
    must("AL1 AddAutoload", mutate(host, f'project | call AddAutoload("RqlState", {q(script)})'))
    check("AL1 saved", setting_line(project, "autoload/RqlState") == f'"*{script}"', ini_section(project, "autoload"))
    text = must("AL1 read", query(host, 'autoloads | where name == "RqlState" | select path singleton order'))
    has("AL1 row", text, script, "true")
    must("AL2 singleton off", mutate(host, 'autoloads | where name == "RqlState" | set singleton = false'))
    check("AL2 saved without *", setting_line(project, "autoload/RqlState") == f'"{script}"', ini_section(project, "autoload"))
    fails("AL2 path must exist", mutate(host, 'autoloads | where name == "RqlState" | set path = "res://no/such.gd"'), "no file")
    must("AL3 second autoload", mutate(host, f'project | call AddAutoload("RqlSecond", {q(script)}, false)'))
    text = must("AL3 order", query(host, 'autoloads | where name starts-with "Rql" | select name order'))
    check("AL3 RqlState before RqlSecond", text.find("RqlState") < text.find("RqlSecond"), text)
    fails("AL4 duplicate", mutate(host, f'project | call AddAutoload("RqlState", {q(script)})'), "already exists")
    fails("AL4 class name", mutate(host, f'project | call AddAutoload("Node", {q(script)})'), "Godot class")
    fails("AL4 bad identifier", mutate(host, f'project | call AddAutoload("1bad", {q(script)})'), "not a valid")
    fails("AL4 not a script", mutate(host, 'project | call AddAutoload("RqlBad", "res://project.godot")'), "not a script")
    if HOOKS["on"]:
        must("AL5 remove for undo", mutate(host, 'autoloads | where name == "RqlState" | call Remove()'))
        check("AL5 removed", setting_line(project, "autoload/RqlState") is None, ini_section(project, "autoload"))
        undo(host, "global")
        check("AL5 undo restored it", setting_line(project, "autoload/RqlState") == f'"{script}"', ini_section(project, "autoload"))
    else:
        skip_undo("AL5")
    must("AL6 cleanup", mutate(host, 'autoloads | where name starts-with "Rql" | call Remove()'))
    check("AL6 removed", "Rql" not in ini_section(project, "autoload"), ini_section(project, "autoload"))


def global_groups(host: str, project: pathlib.Path) -> None:
    mutate(host, 'project | call RemoveGlobalGroup("rql_hazards")')
    must("GG1 AddGlobalGroup", mutate(host, 'project | call AddGlobalGroup("rql_hazards", "Things that hurt")'))
    check("GG1 saved", setting_line(project, "global_group/rql_hazards") == '"Things that hurt"', ini_section(project, "global_group"))
    text = must("GG1 groups source", query(host, 'groups | where name == "rql_hazards" | select global description count'))
    has("GG1 row", text, "true", "Things that hurt")
    fails("GG2 duplicate", mutate(host, 'project | call AddGlobalGroup("rql_hazards")'), "already exists")
    must("GG3 RemoveGlobalGroup", mutate(host, 'project | call RemoveGlobalGroup("rql_hazards")'))
    check("GG3 removed", setting_line(project, "global_group/rql_hazards") is None, ini_section(project, "global_group"))
    fails("GG3 remove missing", mutate(host, 'project | call RemoveGlobalGroup("rql_hazards")'), "no global group")


def tilesets(host: str, project: pathlib.Path, d: str) -> None:
    png, tiles = f"{d}/tiles.png", f"{d}/tiles.tres"
    write_png(disk(project, png), 64, 32, {(0, 0), (1, 0), (3, 1)}, 16)
    must("TS0 import the texture", mutate(host, f"editor | call Reimport([{q(png)}])"))
    must("TS0 CreateResource TileSet", mutate(host, f'editor | call CreateResource({q(tiles)}, "TileSet")'))
    ts = f"godot.resource({q(tiles)})"
    text = must("TS1 AddAtlasSource", mutate(host, f"{ts} | call AddAtlasSource({q(png)})"))
    result_is("TS1 returns source id 0", text, "0")
    text = must("TS1 sources", query(host, f"{ts} | get sources | select id kind texture tileCount"))
    has("TS1 source row", text, "atlas", png)
    atlas = f"{ts} | get sources | get atlas"
    text = must("TS2 grid", query(host, f"{atlas} | select gridSize"))
    has("TS2 grid size", text, "(4, 2)")
    text = must("TS2 CreateTilesFromTexture", mutate(host, f"{atlas} | call CreateTilesFromTexture()"))
    result_is("TS2 created 3 tiles", text, "3")
    text = must("TS2 tiles", query(host, f"{atlas} | get tiles | select coords size"))
    has("TS2 tile coords", text, "(0, 0)", "(1, 0)", "(3, 1)")
    lacks("TS2 transparent cell skipped", text, "(2, 0)")
    must("TS3 CreateTile", mutate(host, f'{atlas} | call CreateTile("(2, 0)")'))
    fails("TS3 overlapping tile", mutate(host, f'{atlas} | call CreateTile("(2, 0)")'), "no room")
    fails("TS3 outside the grid", mutate(host, f'{atlas} | call CreateTile("(9, 9)")'), "no room")
    must("TS3 RemoveTile", mutate(host, f'{atlas} | call RemoveTile("(2, 0)")'))
    fails("TS3 RemoveTile missing", mutate(host, f'{atlas} | call RemoveTile("(2, 0)")'), "no tile")
    must("TS4 tile data", mutate(host, f'{atlas} | get tiles | where coords == "(0, 0)" | set data.Modulate = "(1, 0, 0, 1)", data.ZIndex = 2'))
    text = must("TS5 AddPhysicsLayer", mutate(host, f"{ts} | call AddPhysicsLayer()"))
    must("TS5 physics layer setting", mutate(host, f'{ts} | set props["physics_layer_0/collision_layer"] = 5'))
    text = must("TS6 AddTerrainSet", mutate(host, f'{ts} | call AddTerrainSet("MatchSides")'))
    must("TS6 AddTerrain", mutate(host, f'{ts} | call AddTerrain(0, "grass", "#00ff00")'))
    fails("TS6 AddTerrain bad set", mutate(host, f'{ts} | call AddTerrain(7, "rock")'), "no terrain set")
    fails("TS6 bad mode", mutate(host, f'{ts} | call AddTerrainSet("Diagonal")'), "terrain mode")
    must("TS7 AddCustomDataLayer", mutate(host, f'{ts} | call AddCustomDataLayer("damage", "Int")'))
    fails("TS7 duplicate layer", mutate(host, f'{ts} | call AddCustomDataLayer("damage", "Int")'), "already")
    fails("TS7 bad type", mutate(host, f'{ts} | call AddCustomDataLayer("speed", "Number")'), "value type")
    must("TS7 custom data on a tile", mutate(host, f'{atlas} | get tiles | where coords == "(1, 0)" | set data.props["custom_data_0"] = 7'))
    must("TS8 navigation + occlusion", mutate(host, f"{ts} | call AddNavigationLayer()"))
    must("TS8 occlusion", mutate(host, f"{ts} | call AddOcclusionLayer()"))
    text = must("TS9 layers read", query(host, f"{ts} | select physicsLayers navigationLayers occlusionLayers"))
    has("TS9 counts", text, "1")
    text = must("TS9 terrain sets", query(host, f"{ts} | get terrainSets | select index mode terrains"))
    has("TS9 terrain set row", text, "MatchSides", "grass")
    text = must("TS9 custom layers", query(host, f"{ts} | get customDataLayers | select name type"))
    has("TS9 custom layer row", text, "damage", "Int")
    fails("TS10 RemoveSource unknown", mutate(host, f"{ts} | call RemoveSource(5)"), "no source")
    content = file_text(project, tiles)
    for name, pattern in (("atlas sub-resource", r'^\[sub_resource type="TileSetAtlasSource"'),
                          ("texture reference", r'^texture = ExtResource\("[^"]+"\)$'),
                          ("tile 0:0", r"^0:0/0 = 0$"), ("tile 1:0", r"^1:0/0 = 0$"), ("tile 3:1", r"^3:1/0 = 0$"),
                          ("tile modulate", r"^0:0/0/modulate = Color\(1, 0, 0, 1\)$"),
                          ("tile z index", r"^0:0/0/z_index = 2$"),
                          ("tile custom data", r"^1:0/0/custom_data_0 = 7$"),
                          ("source", r'^sources/0 = SubResource\("[^"]+"\)$'),
                          ("physics layer", r"^physics_layer_0/collision_layer = 5$"),
                          ("terrain set mode", r"^terrain_set_0/mode = 2$"),
                          ("terrain name", r'^terrain_set_0/terrain_0/name = "grass"$'),
                          ("terrain color", r"^terrain_set_0/terrain_0/color = Color\(0, 1, 0, 1\)$"),
                          ("custom data name", r'^custom_data_layer_0/name = "damage"$'),
                          ("custom data type", r"^custom_data_layer_0/type = 2$")):
        check(f"TS file has {name}", re.search(pattern, content, re.M) is not None, content)
    check("TS file has no removed tile 2:0", re.search(r"^2:0/", content, re.M) is None, content)
    if HOOKS["on"]:
        must("TS11 add source for undo", mutate(host, f"{ts} | call AddAtlasSource({q(png)})"))
        undo(host, "global")
        text = must("TS11 read", query(host, f"{ts} | get sources | count"))
        has("TS11 undo removed the source", text, "1")
        must("TS11 remove tile for undo", mutate(host, f'{atlas} | call RemoveTile("(0, 0)")'))
        undo(host, "global")
        text = must("TS11 read tile", query(host, f'{atlas} | get tiles | where coords == "(0, 0)" | select d=(data.Modulate) z=(data.ZIndex)'))
        has("TS11 undo restored the tile and its data", text, "(1, 0, 0, 1)", "2")
        must("TS11 terrain for undo", mutate(host, f'{ts} | call AddTerrain(0, "sand")'))
        undo(host, "global")
        text = must("TS11 read terrains", query(host, f"{ts} | get terrainSets | select terrains"))
        lacks("TS11 undo removed the terrain", text, "sand")
    else:
        skip_undo("TS11")


def themes(host: str, project: pathlib.Path, d: str) -> None:
    theme, box, font, icon = (f"{d}/{n}" for n in ("theme.tres", "theme_box.tres", "theme_font.tres", "theme_icon.png"))
    must("TH0 CreateResource Theme", mutate(host, f'editor | call CreateResource({q(theme)}, "Theme")'))
    must("TH0 StyleBoxFlat", mutate(host, f'editor | call CreateResource({q(box)}, "StyleBoxFlat")'))
    must("TH0 FontVariation", mutate(host, f'editor | call CreateResource({q(font)}, "FontVariation")'))
    write_png(disk(project, icon), 16, 16, {(0, 0)}, 16)
    must("TH0 import icon", mutate(host, f"editor | call Reimport([{q(icon)}])"))
    th = f"godot.resource({q(theme)})"
    must("TH1 colors + constants + font sizes", mutate(
        host, f'{th} | set colors["Button/font_color"] = "#ff0000", constants["HBoxContainer/separation"] = 12, '
              f'fontSizes["Label/font_size"] = 24'))
    must("TH2 resources by path", mutate(
        host, f'{th} | set styleboxes["Button/normal"] = {q(box)}, fonts["Label/font"] = {q(font)}, icons["CheckBox/checked"] = {q(icon)}'))
    text = must("TH3 read back", query(host, f'{th} | select c=(colors["Button/font_color"]) k=(constants["HBoxContainer/separation"]) '
                                             f'f=(fontSizes["Label/font_size"]) s=(styleboxes["Button/normal"].path) types'))
    has("TH3 values", text, "(1, 0, 0, 1)", "12", "24", box, "Button", "HBoxContainer", "Label", "CheckBox")
    text = must("TH3 items", query(host, f'{th} | get items | where type == "Button" | select kind name'))
    has("TH3 items rows", text, "color", "font_color", "stylebox", "normal")
    content = file_text(project, theme)
    for name, pattern in (("color", r"^Button/colors/font_color = Color\(1, 0, 0, 1\)$"),
                          ("constant", r"^HBoxContainer/constants/separation = 12$"),
                          ("font size", r"^Label/font_sizes/font_size = 24$"),
                          ("stylebox", r'^Button/styles/normal = ExtResource\("[^"]+"\)$'),
                          ("font", r'^Label/fonts/font = ExtResource\("[^"]+"\)$'),
                          ("icon", r'^CheckBox/icons/checked = ExtResource\("[^"]+"\)$')):
        check(f"TH file has {name}", re.search(pattern, content, re.M) is not None, content)
    fails("TH4 key without a type", mutate(host, f'{th} | set colors["font_color"] = "#00ff00"'), "ControlType/item")
    fails("TH4 wrong value type", mutate(host, f'{th} | set constants["HBoxContainer/separation"] = "wide"'))
    fails("TH4 not a stylebox", mutate(host, f'{th} | set styleboxes["Button/hover"] = {q(font)}'))
    must("TH5 null clears an object item", mutate(host, f'{th} | set icons["CheckBox/checked"] = null'))
    text = must("TH5 ClearItem", mutate(host, f'{th} | call ClearItem("color", "Button/font_color")'))
    result_is("TH5 ClearItem true", text, "true")
    text = must("TH5 ClearItem again", mutate(host, f'{th} | call ClearItem("color", "Button/font_color")'))
    result_is("TH5 ClearItem false", text, "false")
    fails("TH5 ClearItem bad kind", mutate(host, f'{th} | call ClearItem("colour", "Button/font_color")'), "kind")
    content = file_text(project, theme)
    check("TH5 cleared items are gone from the file", "Button/colors/font_color" not in content and "CheckBox/icons/checked" not in content, content)
    text = must("TH6 ClearType", mutate(host, f'{th} | call ClearType("Label")'))
    result_is("TH6 removed two", text, "2")
    check("TH6 Label items gone", "Label/" not in file_text(project, theme), file_text(project, theme))
    if HOOKS["on"]:
        must("TH7 change for undo", mutate(host, f'{th} | set constants["HBoxContainer/separation"] = 30'))
        undo(host, "global")
        text = must("TH7 read", query(host, f'{th} | select k=(constants["HBoxContainer/separation"])'))
        has("TH7 undo restored the constant", text, "12")
        must("TH7 new item for undo", mutate(host, f'{th} | set colors["Panel/bg"] = "#000000"'))
        undo(host, "global")
        text = must("TH7 read new", query(host, f"{th} | select types"))
        lacks("TH7 undo removed the new item (and its type)", text, "Panel")
        must("TH7 clear for undo", mutate(host, f'{th} | call ClearType("HBoxContainer")'))
        undo(host, "global")
        text = must("TH7 read clear", query(host, f'{th} | select k=(constants["HBoxContainer/separation"])'))
        has("TH7 undo restored the cleared type", text, "12")
    else:
        skip_undo("TH7")


def guide_fixture(host: str, project: pathlib.Path) -> None:
    """What the guide's new examples need: a script with _on_enemy_died on Player, game.gd, a TileSet + texture, a Theme."""
    for event in ('project | call RemoveInputAction("jump")', 'project | call RemoveAutoload("Game")'):
        mutate(host, event)
    must("G0 player script", tool(host, "script-create", {"scriptPath": "res://scenes/player.gd",
                                                          "content": "extends Node3D\n\nfunc _on_enemy_died():\n\tpass\n"}))
    must("G0 attach player script", tool(host, "script-attach-to-node", {"nodeRef": "Player", "scriptPath": "res://scenes/player.gd"}))
    must("G0 game script", tool(host, "script-create", {"scriptPath": "res://scenes/game.gd", "content": "extends Node\n"}))
    write_png(disk(project, "res://tiles/terrain.png"), 32, 16, {(0, 0), (1, 0)}, 16)
    must("G0 import terrain.png", mutate(host, 'editor | call Reimport(["res://tiles/terrain.png"])'))
    for path, cls in (("res://tiles/terrain.tres", "TileSet"), ("res://ui/theme.tres", "Theme")):
        if disk(project, path).exists():
            must(f"G0 clean {path}", mutate(host, f"editor | call DeleteResource({q(path)})"))
        must(f"G0 {cls}", mutate(host, f"editor | call CreateResource({q(path)}, {q(cls)})"))


def guide_checks(host: str, project: pathlib.Path) -> None:
    """Read-back of the guide's new examples (they ran after the original ones, on the level scene)."""
    must("G save level", mutate(host, "editor | call SaveScene()"))
    level = file_text(project, "res://scenes/level.tscn")
    check("G enemies grouped", re.search(r'^\[node name="Enemy1"[^\]]*groups=\["enemies"\]', level, re.M) is not None, level)
    check("G connection made and removed again", 'method="_on_enemy_died"' not in level, level)
    check("G viewport width", setting_line(project, "display/window/size/viewport_width") == "1280", ini_section(project, "display"))
    raw = (setting_line(project, "input/jump") or "").replace(" ", "")
    check("G jump action has Space, A and W", '"keycode":32' in raw and '"button_index":0' in raw and '"keycode":87' in raw, raw)
    check("G Game autoload", setting_line(project, "autoload/Game") == '"*res://scenes/game.gd"', ini_section(project, "autoload"))
    tiles = file_text(project, "res://tiles/terrain.tres")
    check("G TileSet has its atlas and two tiles", "sources/0 = SubResource" in tiles and re.search(r"^0:0/0 = 0$", tiles, re.M)
          and re.search(r"^1:0/0 = 0$", tiles, re.M), tiles)
    theme = file_text(project, "res://ui/theme.tres")
    check("G Theme items", "Button/colors/font_color = Color(1, 0.8, 0, 1)" in theme and "HBoxContainer/constants/separation = 8" in theme, theme)
    # Leave the shared test project as it was.
    for cleanup in ('project | call RemoveInputAction("jump")', 'project | call RemoveAutoload("Game")',
                    'project | set settings["display/window/size/viewport_width"] = 1152'):
        must(f"G cleanup {cleanup}", mutate(host, cleanup))
