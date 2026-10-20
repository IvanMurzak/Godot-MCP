#!/usr/bin/env python3
"""
Live cases of the RQL adapters for ShaderMaterial / Shader uniforms, layer names (the `layers` source and the
`layerNames["…"]` map), MeshLibrary items and the small value types (Projection, RID, Callable), run by
scripts/smoke_rql.py against a running editor after scripts/smoke_rql_models.py (whose helpers and undo-hook state
it reuses).

Every change is checked where it cannot lie: the .tscn / .tres / project.godot text Godot wrote, plus a read-back query;
undo / redo through the test-only test.undo / test.redo functions (GODOT_MCP_RQL_TEST_HOOKS=1). Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import pathlib
import re

from smoke_rql_lib import check, disk, file_has, file_text, must, mutate, q, query, tool
from smoke_rql_models import HOOKS, fails, has, ini_section, lacks, new_scene, node, redo, result_is, setting_line, skip_undo, undo, write_png

SHADER_CODE = (
    "shader_type spatial;\n"
    "uniform vec4 tint : source_color = vec4(1.0, 0.5, 0.25, 1.0);\n"
    "uniform float speed : hint_range(0.0, 10.0) = 2.5;\n"
    "group_uniforms extra;\n"
    "uniform int count = 3;\n"
    "uniform sampler2D tex;\n"
    "group_uniforms;\n"
    "uniform vec3 dir = vec3(0.0, 1.0, 0.0);\n"
    "uniform ivec2 cell = ivec2(2);\n"
    "uniform bool on = true;\n"
    "uniform mat4 lens;\n"
    "void fragment() { ALBEDO = tint.rgb * speed; }\n"
)

# A VisualShader with one connected float parameter (an unconnected parameter generates no uniform).
VISUAL_SHADER = """[gd_resource type="VisualShader" load_steps=2 format=3]

[sub_resource type="VisualShaderNodeFloatParameter" id="1"]
parameter_name = "rough"
default_value_enabled = true
default_value = 0.25

[resource]
nodes/fragment/2/node = SubResource("1")
nodes/fragment/2/position = Vector2(0, 0)
nodes/fragment/connections = PackedInt32Array(2, 0, 0, 3)
"""


def run(host: str, project: pathlib.Path, d: str) -> None:
    schema(host)
    shader_params(host, project, d)
    layer_names(host, project, d)
    mesh_library(host, project, d)
    value_types(host, project, d)


def schema(host: str) -> None:
    text = must("A0 rql-schema overview", tool(host, "rql-schema", {}))
    check("A0 schema lists source 'layers'", "  layers —" in text, text[:600])
    check("A0 schema lists godot.callable", "godot.callable(" in text, text[:600])
    for typ, needles in (("ShaderMaterial", ("uniforms", "params", "ClearParam", "ClearParams")),
                         ("Shader", ("uniforms",)),
                         ("MeshLibrary", ("items", "CreateItem", "RemoveItem")),
                         ("StaticBody3D", ("layerNames",))):
        text = must(f"A0 rql-schema {typ}", tool(host, "rql-schema", {"type": typ}))
        has(f"A0 {typ} members", text, *needles)


# ------------------------------------------------------------------ shader parameters


def write_shader(host: str, project: pathlib.Path, path: str) -> None:
    """A .gdshader with SHADER_CODE (CreateResource writes .tres / .res only): the file, then the editor refreshes it."""
    disk(project, path).parent.mkdir(parents=True, exist_ok=True)
    disk(project, path).write_text(SHADER_CODE, encoding="utf-8")
    must(f"shader file {path}", mutate(host, f"editor | call Reimport([{q(path)}])"))
    text = must(f"shader loads {path}", query(host, f"godot.resource({q(path)}) | select class"))
    has(f"shader {path} is a Shader", text, "Shader")


def shader_params(host: str, project: pathlib.Path, d: str) -> None:
    shader, mat, bare, png = (f"{d}/{n}" for n in ("water.gdshader", "water.tres", "bare.tres", "shader_tex.png"))
    write_png(disk(project, png), 16, 16, {(0, 0)}, 16)
    must("SH0 import the texture", mutate(host, f"editor | call Reimport([{q(png)}])"))
    write_shader(host, project, shader)
    must("SH0 CreateResource ShaderMaterial", mutate(host, f'editor | call CreateResource({q(mat)}, "ShaderMaterial")'))
    must("SH0 assign the shader", mutate(host, f"godot.resource({q(mat)}) | set Shader = {q(shader)}"))
    m = f"godot.resource({q(mat)})"

    # Read: the uniform list with types, groups and defaults (from the code: the headless renderer keeps none).
    text = must("SH1 uniforms", query(host, f"{m} | get uniforms | select name type group default value overridden"))
    has("SH1 names", text, "tint", "speed", "count", "tex", "dir", "cell", "on", "lens")
    has("SH1 types", text, "Color", "Float", "Int", "Texture2D", "Vector3", "Vector2I", "Bool", "Projection")
    has("SH1 defaults", text, "(1, 0.5, 0.25, 1)", "2.5", "(0, 1, 0)", "(2, 2)")
    has("SH1 group", text, "extra")
    text = must("SH1 one uniform", query(host, f'{m} | get uniforms | where name == "count" | select group default value overridden'))
    has("SH1 count row", text, "extra|3|3|false")
    text = must("SH1 Shader uniforms", query(host, f"godot.resource({q(shader)}) | get uniforms | select name type default"))
    has("SH1 Shader rows", text, "speed|Float|2.5", "tint|Color|(1, 0.5, 0.25, 1)")

    # Write every kind of uniform; the material file holds them.
    must("SH2 set params", mutate(
        host, f'{m} | set params["speed"] = 4, params["tint"] = "#ff0000", params["count"] = 7, params["dir"] = [1, 0, 0], '
              f'params["cell"] = "(3, 4)", params["on"] = false, params["lens"] = "(1, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)", '
              f'params["tex"] = {q(png)}'))
    text = must("SH2 read back", query(host, f'{m} | select s=(params["speed"]) t=(params["tint"]) c=(params["count"]) l=(params["lens"]) x=(params["tex"].path)'))
    has("SH2 values", text, "4|(1, 0, 0, 1)|7|(1, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)", png)
    text = must("SH2 overridden", query(host, f'{m} | get uniforms | where overridden | count'))
    has("SH2 eight overridden", text, "8")
    for name, pattern in (("speed", r"^shader_parameter/speed = 4\.0$"), ("tint", r"^shader_parameter/tint = Color\(1, 0, 0, 1\)$"),
                          ("count", r"^shader_parameter/count = 7$"), ("dir", r"^shader_parameter/dir = Vector3\(1, 0, 0\)$"),
                          ("cell", r"^shader_parameter/cell = Vector2i\(3, 4\)$"), ("on", r"^shader_parameter/on = false$"),
                          ("lens (Projection)", r"^shader_parameter/lens = Projection\(1, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1\)$"),
                          ("tex", r'^shader_parameter/tex = ExtResource\("[^"]+"\)$')):
        check(f"SH2 file has {name}", file_has(project, mat, pattern), file_text(project, mat))

    # Validation: nothing is written when one value is wrong.
    fails("SH3 unknown uniform", mutate(host, f'{m} | set params["sped"] = 1'), "no uniform 'sped'", "speed")
    fails("SH3 wrong value type", mutate(host, f'{m} | set params["speed"] = "fast"'))
    fails("SH3 an int takes no fraction", mutate(host, f'{m} | set params["count"] = 1.5'))
    fails("SH3 a sampler takes a texture", mutate(host, f'{m} | set params["tex"] = {q(mat)}'), "Texture2D")
    fails("SH3 Projection needs 16 numbers", mutate(host, f'{m} | set params["lens"] = "(1, 0, 0, 1)"'), "16 numbers")
    fails("SH3 all or nothing", mutate(host, f'{m} | set params["speed"] = 6, params["nope"] = 1'), "nope")
    must("SH3 empty material", mutate(host, f'editor | call CreateResource({q(bare)}, "ShaderMaterial")'))
    fails("SH3 a material without a shader", mutate(host, f'godot.resource({q(bare)}) | set params["speed"] = 1'), "no shader")
    check("SH3 nothing written", file_has(project, mat, r"^shader_parameter/speed = 4\.0$"), file_text(project, mat))
    must("SH4 dry run", mutate(host, f'{m} | set params["speed"] = 9', dry_run=True))
    check("SH4 dry run wrote nothing", file_has(project, mat, r"^shader_parameter/speed = 4\.0$"), file_text(project, mat))

    # Revert to the default.
    text = must("SH5 ClearParam", mutate(host, f'{m} | call ClearParam("speed")'))
    result_is("SH5 ClearParam true", text, "true")
    text = must("SH5 ClearParam again", mutate(host, f'{m} | call ClearParam("speed")'))
    result_is("SH5 ClearParam false at the default", text, "false")
    fails("SH5 ClearParam unknown", mutate(host, f'{m} | call ClearParam("sped")'), "no uniform")
    must("SH5 null reverts", mutate(host, f'{m} | set params["count"] = null'))
    text = must("SH5 read", query(host, f'{m} | select s=(params["speed"]) c=(params["count"])'))
    has("SH5 defaults back", text, "2.5|3")
    content = file_text(project, mat)
    check("SH5 file no longer holds the old values",
          re.search(r"^shader_parameter/speed = 4\.0$", content, re.M) is None and re.search(r"^shader_parameter/count = 7$", content, re.M) is None,
          content)

    if HOOKS["on"]:
        must("SH6 change for undo", mutate(host, f'{m} | set params["speed"] = 5, params["tint"] = "#00ff00"'))
        undo(host, "global")
        text = must("SH6 read", query(host, f'{m} | select s=(params["speed"]) t=(params["tint"])'))
        has("SH6 undo restored both", text, "2.5|(1, 0, 0, 1)")
        redo(host, "global")
        text = must("SH6 redo read", query(host, f'{m} | select s=(params["speed"]) t=(params["tint"])'))
        has("SH6 redo re-applied", text, "5|(0, 1, 0, 1)")
        text = must("SH6 ClearParams", mutate(host, f"{m} | call ClearParams()"))
        result_is("SH6 ClearParams counts the overridden ones", text, "7")
        undo(host, "global")
        text = must("SH6 read after undoing ClearParams", query(host, f'{m} | select s=(params["speed"]) d=(params["dir"]) x=(params["tex"].path)'))
        has("SH6 undo restored the cleared values", text, "5|(1, 0, 0)", png)
    else:
        skip_undo("SH6")

    # A VisualShader: the same uniforms, read from the generated code.
    vs, vmat = f"{d}/visual.tres", f"{d}/visual_mat.tres"
    disk(project, vs).write_text(VISUAL_SHADER, encoding="utf-8")
    must("SH7 VisualShader file", mutate(host, f"editor | call Reimport([{q(vs)}])"))
    text = must("SH7 VisualShader uniforms", query(host, f"godot.resource({q(vs)}) | get uniforms | select name type default"))
    has("SH7 VisualShader parameter", text, "rough|Float|0.25")
    must("SH7 material", mutate(host, f'editor | call CreateResource({q(vmat)}, "ShaderMaterial")'))
    must("SH7 assign the VisualShader", mutate(host, f"godot.resource({q(vmat)}) | set Shader = {q(vs)}"))
    must("SH7 set the parameter", mutate(host, f'godot.resource({q(vmat)}) | set params["rough"] = 0.75'))
    check("SH7 file", file_has(project, vmat, r"^shader_parameter/rough = 0\.75$"), file_text(project, vmat))


# ------------------------------------------------------------------ layer names


def layer_names(host: str, project: pathlib.Path, d: str) -> None:
    for kind, index in (("3d_physics", 2), ("3d_physics", 3), ("3d_render", 4)):
        mutate(host, f'layers | where kind == {q(kind)} and index == {index} | set name = ""')
    text = must("LY1 layers source", query(host, "layers | count"))
    has("LY1 every layer of every kind (20 + 32 * 5 + 20)", text, "200")
    text = must("LY1 a row", query(host, 'layers | where kind == "3d_physics" and index == 3 | select bit name setting'))
    has("LY1 bit and setting", text, "4|", "layer_names/3d_physics/layer_3")
    must("LY2 name layers", mutate(host, 'layers | where kind == "3d_physics" and index == 2 | set name = "rql_enemies"'))
    must("LY2 name another", mutate(host, 'layers | where kind == "3d_physics" and index == 3 | set name = "rql_world"'))
    must("LY2 name a render layer", mutate(host, 'layers | where kind == "3d_render" and index == 4 | set name = "rql_ui"'))
    check("LY2 project.godot", setting_line(project, "layer_names/3d_physics/layer_2") == '"rql_enemies"', ini_section(project, "layer_names"))
    check("LY2 project.godot render", setting_line(project, "layer_names/3d_render/layer_4") == '"rql_ui"', ini_section(project, "layer_names"))

    sc = f"{d}/layers.tscn"
    new_scene(host, sc, "Node3D", "LayerRoot", [("StaticBody3D", "Body"), ("MeshInstance3D", "Vis"), ("Camera3D", "Cam"),
                                               ("NavigationRegion3D", "Nav"), ("Sprite2D", "Sprite2"), ("Node3D", "Plain")])
    text = must("LY3 read the default masks", query(host, f'{node("Body")} | select l=(layerNames["collision_layer"]) m=(layerNames["collision_mask"])'))
    has("LY3 layer 1, unnamed", text, '["1"]|["1"]')
    must("LY4 mask by names and numbers", mutate(host, f'{node("Body")} | set layerNames["collision_mask"] = ["rql_world", "rql_enemies", 7]'))
    text = must("LY4 read", query(host, f'{node("Body")} | select CollisionMask m=(layerNames["collision_mask"])'))
    has("LY4 raw int and names", text, "70|", "rql_world", "rql_enemies", '"7"')
    must("LY4 one name as text", mutate(host, f'{node("Body")} | set layerNames["collision_layer"] = "RQL_Enemies"'))
    text = must("LY4 read layer", query(host, f'{node("Body")} | select CollisionLayer'))
    has("LY4 case-insensitive name", text, "2")
    text = must("LY4 where by layer name", query(host, 'nodes | where layerNames["collision_mask"].any($ == "rql_enemies") | select name'))
    has("LY4 where", text, "Body")
    must("LY5 render layers", mutate(host, f'{node("Vis")} | set layerNames["layers"] = [1, "rql_ui"]'))
    must("LY5 camera cull mask", mutate(host, f'{node("Cam")} | set layerNames["cull_mask"] = ["rql_ui"]'))
    must("LY5 navigation layers", mutate(host, f'{node("Nav")} | set layerNames["navigation_layers"] = [2, 3]'))
    must("LY5 2D visibility layer", mutate(host, f'{node("Sprite2")} | set layerNames["visibility_layer"] = [3]'))
    text = must("LY5 read", query(host, f'{node("Vis")} | select Layers'))
    has("LY5 render mask", text, "9")
    fails("LY6 unknown layer name", mutate(host, f'{node("Body")} | set layerNames["collision_mask"] = ["rql_nobody"]'), "rql_nobody", "rql_enemies")
    fails("LY6 render layers stop at 20", mutate(host, f'{node("Vis")} | set layerNames["layers"] = [21]'), "1-20")
    fails("LY6 a bare number is ambiguous", mutate(host, f'{node("Body")} | set layerNames["collision_mask"] = 3'), "[3]")
    fails("LY6 not a layer property", mutate(host, f'{node("Body")} | set layerNames["collision_priority"] = [1]'), "collision_layer", "collision_mask")
    fails("LY6 an object without layers", mutate(host, f'{node("Plain")} | set layerNames["layers"] = [1]'), "no layer properties")
    must("LY7 dry run", mutate(host, f'{node("Body")} | set layerNames["collision_mask"] = [1]', dry_run=True))
    text = must("LY7 read", query(host, f'{node("Body")} | select CollisionMask'))
    has("LY7 dry run changed nothing", text, "70")
    must("LY7 raw ints still work", mutate(host, f'{node("Cam")} | set CullMask = 1'))
    text = must("LY7 read raw", query(host, f'{node("Cam")} | select c=(layerNames["cull_mask"])'))
    has("LY7 raw write reads by name", text, '["1"]')
    must("LY7 restore the cull mask", mutate(host, f'{node("Cam")} | set layerNames["cull_mask"] = ["rql_ui"]'))
    if HOOKS["on"]:
        must("LY8 change for undo", mutate(host, f'{node("Body")} | set layerNames["collision_mask"] = [1]'))
        undo(host, "scene")
        text = must("LY8 read", query(host, f'{node("Body")} | select CollisionMask'))
        has("LY8 undo restored the mask", text, "70")
        redo(host, "scene")
        text = must("LY8 redo read", query(host, f'{node("Body")} | select CollisionMask'))
        has("LY8 redo re-applied", text, "1")
        undo(host, "scene")
        must("LY8 rename for undo", mutate(host, 'layers | where kind == "3d_physics" and index == 2 | set name = "rql_renamed"'))
        undo(host, "global")
        check("LY8 undo restored the name in project.godot", setting_line(project, "layer_names/3d_physics/layer_2") == '"rql_enemies"',
              ini_section(project, "layer_names"))
    else:
        skip_undo("LY8")
    must("LY9 save", mutate(host, "editor | call SaveScene()"))
    for name, pattern in (("collision_layer", r"^collision_layer = 2$"), ("collision_mask", r"^collision_mask = 70$"),
                          ("render layers", r"^layers = 9$"), ("cull mask", r"^cull_mask = 8$"),
                          ("navigation layers", r"^navigation_layers = 6$"), ("visibility layer", r"^visibility_layer = 4$")):
        check(f"LY9 file has {name}", file_has(project, sc, pattern), file_text(project, sc))

    # A resource's layer property: a TileSet physics layer.
    tiles = f"{d}/layer_tiles.tres"
    must("LY10 TileSet", mutate(host, f'editor | call CreateResource({q(tiles)}, "TileSet")'))
    must("LY10 physics layer", mutate(host, f"godot.resource({q(tiles)}) | call AddPhysicsLayer()"))
    text = must("LY10 TileSet layer properties", query(host, f'godot.resource({q(tiles)}) | select l=(layerNames["physics_layer_0/collision_mask"])'))
    has("LY10 read", text, '["1"]')
    must("LY10 write", mutate(host, f'godot.resource({q(tiles)}) | set layerNames["physics_layer_0/collision_mask"] = [1, 4]'))
    check("LY10 TileSet file", file_has(project, tiles, r"^physics_layer_0/collision_mask = 9$"), file_text(project, tiles))

    for kind, index in (("3d_physics", 2), ("3d_physics", 3), ("3d_render", 4)):
        must(f"LY11 unname {kind} {index}", mutate(host, f'layers | where kind == {q(kind)} and index == {index} | set name = ""'))
    check("LY11 names removed from project.godot", "rql_" not in ini_section(project, "layer_names"), ini_section(project, "layer_names"))


# ------------------------------------------------------------------ MeshLibrary


def mesh_library(host: str, project: pathlib.Path, d: str) -> None:
    lib, box, shape, nav, icon = (f"{d}/{n}" for n in ("library.tres", "box.tres", "box_shape.tres", "nav.tres", "preview.png"))
    for path, cls in ((lib, "MeshLibrary"), (box, "BoxMesh"), (shape, "BoxShape3D"), (nav, "NavigationMesh")):
        must(f"ML0 CreateResource {cls}", mutate(host, f"editor | call CreateResource({q(path)}, {q(cls)})"))
    write_png(disk(project, icon), 16, 16, {(0, 0)}, 16)
    must("ML0 import the preview", mutate(host, f"editor | call Reimport([{q(icon)}])"))
    ml = f"godot.resource({q(lib)})"
    text = must("ML1 CreateItem", mutate(host, f'{ml} | call CreateItem("Floor", {q(box)})'))
    has("ML1 returns the item", text, "id:0", "name:Floor")
    text = must("ML1 CreateItem with an id", mutate(host, f'{ml} | call CreateItem("Wall", null, 5)'))
    has("ML1 id 5", text, "id:5")
    fails("ML1 a taken id", mutate(host, f'{ml} | call CreateItem("Again", null, 5)'), "already has an item 5")
    fails("ML1 a name is required", mutate(host, f'{ml} | call CreateItem("")'), "name")
    fails("ML1 the mesh must be a Mesh", mutate(host, f'{ml} | call CreateItem("Bad", {q(shape)})'))
    floor = f'{ml} | get items | where name == "Floor"'
    must("ML2 item settings", mutate(
        host, f'{floor} | set meshTransform = "(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0.5, 0)", navigationMesh = {q(nav)}, '
              f'navigationMeshTransform = "(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0)", navigationLayers = 3, preview = {q(icon)}'))
    text = must("ML2 AddShape", mutate(host, f'{floor} | call AddShape({q(shape)})'))
    result_is("ML2 one shape", text, "1")
    text = must("ML2 AddShape with a transform", mutate(host, f'{floor} | call AddShape({q(shape)}, "(1, 0, 0, 0, 1, 0, 0, 0, 1, 2, 0, 0)")'))
    result_is("ML2 two shapes", text, "2")
    text = must("ML3 read", query(host, f"{floor} | select id name mesh meshTransform navigationLayers preview"))
    has("ML3 row", text, "0|Floor|", box, "(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0.5, 0)", "|3|", icon)
    text = must("ML3 shapes", query(host, f"{floor} | get shapes | select index shape transform"))
    has("ML3 shape rows", text, shape, "(1, 0, 0, 0, 1, 0, 0, 0, 1, 2, 0, 0)")
    text = must("ML3 layer names of an item", mutate(host, f'{ml} | set layerNames["item/0/navigation_layers"] = [1, 2, 3]'))
    text = must("ML3 read the item mask", query(host, f"{floor} | select navigationLayers"))
    has("ML3 mask by layer numbers", text, "7")
    content = file_text(project, lib)
    for name, pattern in (("name", r'^item/0/name = "Floor"$'), ("mesh", r'^item/0/mesh = ExtResource\("[^"]+"\)$'),
                          ("mesh transform", r"^item/0/mesh_transform = Transform3D\(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0\.5, 0\)$"),
                          ("shapes", r'^item/0/shapes = \[ExtResource\("[^"]+"\), Transform3D\(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0\), '
                                     r'ExtResource\("[^"]+"\), Transform3D\(1, 0, 0, 0, 1, 0, 0, 0, 1, 2, 0, 0\)\]$'),
                          ("navigation mesh", r'^item/0/navigation_mesh = ExtResource\("[^"]+"\)$'),
                          ("navigation transform", r"^item/0/navigation_mesh_transform = Transform3D\(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0\)$"),
                          ("navigation layers", r"^item/0/navigation_layers = 7$"),
                          ("preview", r'^item/0/preview = ExtResource\("[^"]+"\)$'), ("second item", r'^item/5/name = "Wall"$')):
        check(f"ML3 file has {name}", re.search(pattern, content, re.M) is not None, content)
    must("ML4 ClearShapes", mutate(host, f"{floor} | call ClearShapes()"))
    check("ML4 shapes cleared in the file", file_has(project, lib, r"^item/0/shapes = \[\]$"), file_text(project, lib))
    must("ML4 put the shapes back", mutate(host, f'{floor} | call AddShape({q(shape)})'))
    must("ML5 RemoveItem", mutate(host, f"{ml} | call RemoveItem(5)"))
    check("ML5 removed from the file", "item/5/" not in file_text(project, lib), file_text(project, lib))
    fails("ML5 RemoveItem unknown", mutate(host, f"{ml} | call RemoveItem(99)"), "no item 99")
    must("ML6 dry run", mutate(host, f'{ml} | call CreateItem("Dry")', dry_run=True))
    text = must("ML6 read", query(host, f"{ml} | get items | select name"))
    lacks("ML6 dry run created nothing", text, "Dry")
    if HOOKS["on"]:
        must("ML7 create for undo", mutate(host, f'{ml} | call CreateItem("Temp", {q(box)}, 8)'))
        undo(host, "global")
        text = must("ML7 read", query(host, f"{ml} | get items | select id name"))
        lacks("ML7 undo removed the new item", text, "Temp")
        redo(host, "global")
        text = must("ML7 redo read", query(host, f"{ml} | get items | select id name mesh"))
        has("ML7 redo re-created it with its mesh", text, "8|Temp|", box)
        must("ML7 remove for undo", mutate(host, f"{ml} | call RemoveItem(0)"))
        undo(host, "global")
        text = must("ML7 read restored", query(host, f"{ml} | get items | where id == 0 | select name mesh navigationLayers preview"))
        has("ML7 undo restored the item and its settings", text, "Floor|", box, "|7|", icon)
        text = must("ML7 shapes restored", query(host, f"{ml} | get items | where id == 0 | get shapes | count"))
        has("ML7 undo restored its shapes", text, "1")
        must("ML7 rename for undo", mutate(host, f'{floor} | set name = "Ground"'))
        undo(host, "global")
        text = must("ML7 read name", query(host, f"{ml} | get items | where id == 0 | select name"))
        has("ML7 undo restored the name", text, "Floor")
    else:
        skip_undo("ML7")


# ------------------------------------------------------------------ Projection, RID, Callable


def value_types(host: str, project: pathlib.Path, d: str) -> None:
    rd = f"{d}/texture_rd.tres"
    must("VT1 Texture2DRD", mutate(host, f'editor | call CreateResource({q(rd)}, "Texture2DRD")'))
    text = must("VT1 RID read", query(host, f"godot.resource({q(rd)}) | select TextureRdRid v=(TextureRdRid.IsValid)"))
    has("VT1 RID text", text, "RID(0)|false")
    text = must("VT1 where on a RID", query(host, f'godot.resource({q(rd)}) | where TextureRdRid == "RID(0)" | count'))
    has("VT1 RID compares to its text", text, "1")
    fails("VT1 RIDs are read-only", mutate(host, f'godot.resource({q(rd)}) | set TextureRdRid = "RID(5)"'), "cannot be written")

    sc = f"{d}/callables.tscn"
    new_scene(host, sc, "Node3D", "CallRoot", [("MultiplayerSpawner", "Spawner"), ("Node3D", "Target"), ("Timer", "Clock")])
    script = f"{d}/target.gd"
    must("VT2 script", tool(host, "script-create", {"scriptPath": script, "content": "extends Node3D\n\nfunc _spawn(data):\n\treturn null\n\nfunc _on_tick():\n\tpass\n"}))
    must("VT2 attach", tool(host, "script-attach-to-node", {"nodeRef": "Target", "scriptPath": script}))
    spawner = node("Spawner")
    text = must("VT2 an empty Callable", query(host, f"{spawner} | select SpawnFunction"))
    has("VT2 shows it is not valid", text, "target:null", "valid:false")
    must("VT3 write as text", mutate(host, f'{spawner} | set SpawnFunction = "Target::_spawn"'))
    text = must("VT3 read", query(host, f"{spawner} | select SpawnFunction t=(SpawnFunction.target.name) m=(SpawnFunction.method) v=(SpawnFunction.valid)"))
    has("VT3 text and members", text, "Target::_spawn|Target|_spawn|true")
    must("VT3 write as a list", mutate(host, f'{spawner} | set SpawnFunction = ["Target", "hide"]'))
    must("VT3 write as an object", mutate(host, f'{spawner} | set SpawnFunction = {{target: godot.find("Target"), method: "show"}}'))
    text = must("VT3 read the object form", query(host, f"{spawner} | select SpawnFunction"))
    has("VT3 object form", text, "Target::show")
    must("VT3 a Class#id target", mutate(host, f"{spawner} | set SpawnFunction = godot.callable(godot.find(\"Target\"), \"_spawn\")"))
    text = must("VT3 where on a Callable", query(host, f'{spawner} | where SpawnFunction == "Target::_spawn" | count'))
    has("VT3 compares to its text", text, "1")
    fails("VT4 missing method", mutate(host, f'{spawner} | set SpawnFunction = "Target::_nope"'), "no method '_nope'")
    fails("VT4 not the text form", mutate(host, f'{spawner} | set SpawnFunction = "Target"'), "<target>::<method>")
    fails("VT4 unknown node", mutate(host, f'{spawner} | set SpawnFunction = "NoSuchNode::hide"'), "NoSuchNode")
    text = must("VT5 godot.callable", query(host, 'godot.callable("Target", "_on_tick") | select target method valid'))
    has("VT5 function result", text, "Target", "_on_tick", "true")
    fails("VT5 godot.callable missing method", query(host, 'godot.callable("Target", "nope")'), "no method")
    # A Callable as a method argument: a persistent connection made through CallMethod (flag 2 = CONNECT_PERSIST).
    must("VT6 connect with a Callable argument",
         mutate(host, f'{node("Clock")} | call CallMethod("connect", ["timeout", godot.callable("Target", "_on_tick"), 2])'))
    text = must("VT6 the connection", query(host, f'{node("Clock")} | get connections | select signal method'))
    has("VT6 listed", text, "timeout|_on_tick")
    must("VT6 save", mutate(host, "editor | call SaveScene()"))
    check("VT6 saved with the scene",
          re.search(r'^\[connection signal="timeout" from="Clock" to="Target" method="_on_tick"\]$', file_text(project, sc), re.M) is not None,
          file_text(project, sc))


# ------------------------------------------------------------------ guide examples


def guide_fixture(host: str, project: pathlib.Path) -> None:
    """What the guide's examples of these adapters need: a ShaderMaterial, a MeshLibrary and a mesh, a StaticBody3D 'Wall'."""
    shader, water, tiles, floor = "res://materials/water.gdshader", "res://materials/water.tres", "res://meshes/tiles.tres", "res://meshes/floor.tres"
    for path in (shader, water, tiles, floor):
        if disk(project, path).exists():
            must(f"G0 clean {path}", mutate(host, f"editor | call DeleteResource({q(path)})"))
    write_shader(host, project, shader)
    must("G0 water material", mutate(host, f'editor | call CreateResource({q(water)}, "ShaderMaterial")'))
    must("G0 water material shader", mutate(host, f"godot.resource({q(water)}) | set Shader = {q(shader)}"))
    must("G0 mesh library", mutate(host, f'editor | call CreateResource({q(tiles)}, "MeshLibrary")'))
    must("G0 floor mesh", mutate(host, f'editor | call CreateResource({q(floor)}, "BoxMesh")'))
    mutate(host, 'layers | where kind == "3d_physics" and index == 2 | set name = ""')
    must("G0 Wall", mutate(host, 'root | call AddChild("StaticBody3D", "Wall")'))


def guide_checks(host: str, project: pathlib.Path) -> None:
    """Read-back of the guide's examples of these adapters (the level scene was saved by smoke_rql_models.guide_checks)."""
    check("G layer named", setting_line(project, "layer_names/3d_physics/layer_2") == '"enemies"', ini_section(project, "layer_names"))
    level = file_text(project, "res://scenes/level.tscn")
    wall = re.search(r'^\[node name="Wall"[^\n]*\n((?:[^\[\n][^\n]*\n?)*)', level, re.M)
    check("G Wall collision mask = layers 1 and 2", wall is not None and re.search(r"^collision_mask = 3$", wall.group(1), re.M) is not None, level)
    water = file_text(project, "res://materials/water.tres")
    check("G water params", "shader_parameter/speed = 2.0" in water and re.search(r"^shader_parameter/tint = Color\(0\.2", water, re.M) is not None, water)
    tiles = file_text(project, "res://meshes/tiles.tres")
    check("G MeshLibrary item", 'item/0/name = "Floor"' in tiles and re.search(r'^item/0/mesh = ExtResource\("[^"]+"\)$', tiles, re.M) is not None, tiles)
    must("G cleanup layer name", mutate(host, 'layers | where kind == "3d_physics" and index == 2 | set name = ""'))
