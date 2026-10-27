#!/usr/bin/env python3
"""
Live cases of values an agent writes the way it reads them, run by scripts/smoke_rql.py against a running editor:
CallMethod arguments converted to the types the Godot method declares (a vector as text, a res:// path, an enum by
name; a wrong one refused before any call), enum-hinted int properties by name, new embedded resources from object
literals ("$type" or class, Godot or script classes), resource files of a script class_name, a multi-target Connect
that is all-or-nothing, and the suggestions of godot.find.

Every change is checked where it cannot lie: the .tscn / .tres text Godot wrote, plus a read-back query.
Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import pathlib
import re

from smoke_rql_lib import check, file_has, file_text, must, mutate, q, query, tool
from smoke_rql_models import fails, has, lacks, new_scene, node, result_is


def run(host: str, project: pathlib.Path, d: str) -> None:
    call_arguments(host, project, d)
    enum_props(host, project, d)
    literals(host, project, d)
    script_class_resources(host, project, d)
    connect_validator(host, d)
    find_suggestions(host, d)


def call_arguments(host: str, project: pathlib.Path, d: str) -> None:
    """CallMethod converts each argument to the declared type; a value that does not convert is refused, nothing runs."""
    tex = f"{d}/v_tex.tres"
    sc = f"{d}/v_calls.tscn"
    new_scene(host, sc, "Node2D", "CallRoot", [("Sprite2D", "Spr"), ("Sprite2D", "Spr2"), ("Control", "Ui")])
    must("V1 texture file", mutate(host, f'editor | call CreateResource({q(tex)}, "GradientTexture2D")'))
    must("V1 vector as text", mutate(host, f'{node("Spr")} | call CallMethod("set_position", ["(3, 4)"])'))
    has("V1 position set", must("V1 read", query(host, f'{node("Spr")} | select Position')), "(3, 4)")
    must("V1 vector as list", mutate(host, f'{node("Spr2")} | call CallMethod("set_position", [[5, 6]])'))
    has("V1 list position set", must("V1 read list", query(host, f'{node("Spr2")} | select Position')), "(5, 6)")
    must("V1 resource by path", mutate(host, f'{node("Spr")} | call CallMethod("set_texture", [{q(tex)}])'))
    has("V1 texture set", must("V1 read texture", query(host, f'{node("Spr")} | select Texture')), tex)
    must("V1 enum by name", mutate(host, f'{node("Ui")} | call CallMethod("set_anchors_preset", ["FullRect"])'))
    has("V1 anchors full rect", must("V1 read anchors", query(host, f'{node("Ui")} | select AnchorRight AnchorBottom')), "1|1")
    fails("V1 a value that does not convert", mutate(host, f'{node("Spr")} | call CallMethod("set_position", ["nope"])'),
          "set_position argument 1", "Vector2")
    fails("V1 a resource of the wrong class", mutate(host, f'{node("Spr")} | call CallMethod("set_material", [{q(tex)}])'),
          "set_material argument 1")
    fails("V1 an unknown enum name", mutate(host, f'{node("Ui")} | call CallMethod("set_anchors_preset", ["Sideways"])'),
          "not one of", "PRESET_FULL_RECT")
    # Spr (first) has set_flip_h, the Control does not: the validator refuses before Spr flips.
    fails("V1 multi-target is all-or-nothing",
          mutate(host, 'nodes | where name == "Spr" or name == "Ui" | call CallMethod("set_flip_h", [true])'), "has no method")
    has("V1 Spr not flipped", must("V1 read flip", query(host, f'{node("Spr")} | select FlipH')), "false")
    must("V1 save", mutate(host, "editor | call SaveScene()"))
    check("V1 saved", file_has(project, sc, r"^position = Vector2\(3, 4\)$") and file_has(project, sc, r"^texture = ExtResource\("),
          file_text(project, sc))


def enum_props(host: str, project: pathlib.Path, d: str) -> None:
    """An enum-hinted int property takes the inspector's name or the constant."""
    sc = f"{d}/v_enums.tscn"
    new_scene(host, sc, "Control", "EnumRoot", [("Button", "B")])
    must("V2 by inspector name", mutate(host, f'{node("B")} | set props["grow_horizontal"] = "Both"'))
    version = re.search(r"(\d+)\.(\d+)", must("V2 version", query(host, "editor | select godotVersion")))
    # The setter's enum constants (GROW_DIRECTION_BEGIN, not the label "Top") need ClassDB.class_get_property_setter: 4.4+.
    constant = "GROW_DIRECTION_BEGIN" if version and (int(version.group(1)), int(version.group(2))) >= (4, 4) else "Top"
    must("V2 by constant (4.4+) or label", mutate(host, f'{node("B")} | set props["grow_vertical"] = {q(constant)}'))
    must("V2 by number", mutate(host, f'{node("B")} | set props["focus_mode"] = 0'))
    has("V2 read", must("V2 read", query(host, f'{node("B")} | select props["grow_horizontal"] props["grow_vertical"] props["focus_mode"]')), "2|0|0")
    fails("V2 an unknown name", mutate(host, f'{node("B")} | set props["grow_horizontal"] = "Sideways"'), "not one of", "Both")
    must("V2 save", mutate(host, "editor | call SaveScene()"))
    check("V2 saved", file_has(project, sc, r"^grow_horizontal = 2$") and file_has(project, sc, r"^grow_vertical = 0$"), file_text(project, sc))


def literals(host: str, project: pathlib.Path, d: str) -> None:
    """Object literals naming a class build embedded sub-resources; an abstract class is refused."""
    sc = f"{d}/v_literals.tscn"
    new_scene(host, sc, "Node3D", "LitRoot", [("CollisionShape3D", "Col"), ("MeshInstance3D", "Mesh"), ("WorldEnvironment", "Env"),
                                              ("MeshInstance3D", "Over")])
    # A nested value of the target's own type (Material in a Material) and a literal as a CallMethod argument.
    must("V3 same-type nesting", mutate(host, f'{node("Over")} | set MaterialOverride = {{class: "StandardMaterial3D", '
                                              'NextPass: {class: "StandardMaterial3D", AlbedoColor: "#00ff00"}}'))
    must("V3 literal argument", mutate(host, f'{node("Over")} | call CallMethod("set_mesh", [{{"$type": "SphereMesh", Radius: 2}}])'))
    fails("V3 abstract class refused", mutate(host, f'{node("Col")} | set Shape = {{"$type": "Shape3D"}}'), "cannot be instantiated")
    must("V3 $type", mutate(host, f'{node("Col")} | set Shape = {{"$type": "BoxShape3D", Size: "(1, 2, 1)"}}'))
    must("V3 class + nested", mutate(host, f'{node("Mesh")} | set Mesh = {{class: "BoxMesh", Material: {{class: "StandardMaterial3D", AlbedoColor: "#ff0000"}}}}'))
    must("V3 concrete property type", mutate(host, f'{node("Env")} | set Environment = {{BackgroundMode: "Sky", Sky: {{SkyMaterial: {{"$type": "ProceduralSkyMaterial"}}}}}}'))
    fails("V3 unrelated class refused", mutate(host, f'{node("Col")} | set Shape = {{class: "BoxMesh"}}'), "BoxMesh")
    must("V3 save", mutate(host, "editor | call SaveScene()"))
    text = file_text(project, sc)
    check("V3 box shape saved", re.search(r'^\[sub_resource type="BoxShape3D" id="[^"]+"\]\nsize = Vector3\(1, 2, 1\)$', text, re.M) is not None, text)
    check("V3 nested material saved", re.search(r'^\[sub_resource type="StandardMaterial3D" id="[^"]+"\]\nalbedo_color = Color\(1, 0, 0, 1\)$', text, re.M) is not None
          and "material = SubResource(" in text, text)
    check("V3 environment saved", "background_mode = 2" in text and 'type="ProceduralSkyMaterial"' in text, text)
    check("V3 nested same-type material saved", "next_pass = SubResource(" in text and "albedo_color = Color(0, 1, 0, 1)" in text, text)
    check("V3 literal argument saved", re.search(r'^\[sub_resource type="SphereMesh" id="[^"]+"\]\nradius = 2\.0$', text, re.M) is not None, text)


def script_class_resources(host: str, project: pathlib.Path, d: str) -> None:
    """A script class_name works for CreateResource, godot.resources and object literals."""
    cls = "HzItem" + re.sub(r"\D", "", d)[-9:]  # class_name is global: one per run
    gd, item, sc = f"{d}/v_item.gd", f"{d}/v_item.tres", f"{d}/v_holder.tscn"
    holder_gd = f"{d}/v_holder.gd"
    # The run folder must be known to the editor filesystem before the class_name script lands in it, or Godot never
    # registers the class; a scan before script-create (never after it: that hung Godot 4.4, see smoke_rql_models).
    must("V4 scan the run folder", mutate(host, "editor | call Reimport()"))
    must("V4 class script", tool(host, "script-create", {"scriptPath": gd, "content":
         f"@tool\nclass_name {cls}\nextends Resource\n@export var price: int = 10\n@export var label: String = \"\"\n@export var icon: Resource\n"}))
    tex = f"{d}/v_icon.tres"
    must("V4 icon file", mutate(host, f'editor | call CreateResource({q(tex)}, "GradientTexture2D")'))
    must("V4 holder script", tool(host, "script-create", {"scriptPath": holder_gd, "content": "@tool\nextends Node\n@export var item: Resource\n"}))
    must("V4 CreateResource of a class_name", mutate(host, f"editor | call CreateResource({q(item)}, {q(cls)})"))
    must("V4 edit it", mutate(host, f'godot.resource({q(item)}) | set props["price"] = 25, props["label"] = "Sword"'))
    check("V4 saved with its script", file_has(project, item, r'^script = ExtResource\(') and file_has(project, item, r"^price = 25$"),
          file_text(project, item))
    has("V4 godot.resources by class_name", must("V4 find", query(host, f"godot.resources({q(cls)}) | select path props[\"price\"]")), f"{item}|25")
    new_scene(host, sc, "Node", "HolderRoot", [("Node", "Holder")])
    must("V4 attach", tool(host, "script-attach-to-node", {"nodeRef": "Holder", "scriptPath": holder_gd}))
    # icon is a Resource like item itself: the res:// path must still load (no re-entry skip of the adapter).
    must("V4 literal of a class_name", mutate(host, f'{node("Holder")} | set props["item"] = {{class: {q(cls)}, price: 9, Label: "Dagger", icon: {q(tex)}}}'))
    must("V4 save", mutate(host, "editor | call SaveScene()"))
    text = file_text(project, sc)
    check("V4 embedded script resource saved", "price = 9" in text and 'label = "Dagger"' in text and "item = SubResource(" in text
          and re.search(r"^icon = ExtResource\(", text, re.M) is not None, text)
    fails("V4 unknown property of the class", mutate(host, f'{node("Holder")} | set props["item"] = {{class: {q(cls)}, nope: 1}}'), "no property")


def connect_validator(host: str, d: str) -> None:
    """A multi-target Connect is checked for every target before the first connection."""
    gd = f"{d}/v_target.gd"
    new_scene(host, f"{d}/v_connect.tscn", "Node", "ConnRoot", [("Button", "A"), ("Node", "B"), ("Node", "Target")])
    must("V5 script", tool(host, "script-create", {"scriptPath": gd, "content": "extends Node\nfunc _on_pressed():\n\tpass\n"}))
    must("V5 attach", tool(host, "script-attach-to-node", {"nodeRef": "Target", "scriptPath": gd}))
    # A (a Button) has "pressed", B (a Node) does not: nothing may connect.
    fails("V5 multi-target Connect is all-or-nothing",
          mutate(host, 'nodes | where name == "A" or name == "B" | call Connect("pressed", godot.find("Target"), "_on_pressed")'),
          "invalid-value", "no signal 'pressed'")
    result_is("V5 A not connected", must("V5 read", query(host, 'godot.find("A") | select name n=(connections.count())')), "0")
    fails("V5 dry run reports a missing method", mutate(host, 'godot.find("A") | call Connect("pressed", godot.find("Target"), "_nope")', dry_run=True),
          "invalid-value", "no method")


def find_suggestions(host: str, d: str) -> None:
    """A godot.find miss suggests the paths of similar names."""
    new_scene(host, f"{d}/v_find.tscn", "Node3D", "FindRoot", [("Node3D", "Geometry")])
    must("V6 child", mutate(host, 'godot.find("Geometry") | call AddChild("Node3D", "Floor")'))
    fails("V6 typo", query(host, 'godot.find("Flor") | select name'), "no node at 'Flor'", "similar: Geometry/Floor")
    fails("V6 wrong parent", query(host, 'godot.find("World/Floor") | select name'), "similar: Geometry/Floor")
    lacks("V6 no suggestion for a far name", fails("V6 far", query(host, 'godot.find("Zebra") | select name'), "no node"), "similar")
