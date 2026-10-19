#!/usr/bin/env python3
"""
Live smoke of the RQL tools (rql-query / rql-mutate / rql-schema) against a running Godot editor.

Every change is verified where it cannot lie: the `.tres` / `.tscn` text Godot itself wrote to disk (the
read-back assertions of the former smoke_property_patches.py, ported from node-modify / resource-modify /
scene-* / resource-move|delete to RQL). Then every example of the agent guide
(Godot-Tests/rql-guide-examples.json, kept equal to RqlGuide.cs by the xUnit suite) runs in order against
its fixture scene and must succeed.

It does NOT launch anything. Point it at a local `gamedev-mcp-server` whose Godot editor (with this addon,
default toolFormat Rql) has the project at `--project` open:

    gamedev-mcp-server port=8080 client-transport=streamableHttp authorization=none &
    GODOT_MCP_CONNECTION_MODE=Custom GODOT_MCP_HOST=http://localhost:8080 \\
        godot --headless --path <project> --editor &
    python scripts/smoke_rql.py --host http://localhost:8080 --project <project>

The guide fixture uses fixed paths (res://scenes/, res://materials/): run it on a disposable project.
Exit code 0 = every case passed. Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys
import time

from smoke_rql_lib import FAILURES, ROOT, check, disk, file_has, file_text, must, mutate, node_line, q, query, tool  # noqa: F401
import smoke_rql_models


def wait_ready(host: str, timeout: float) -> bool:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        ok, _ = tool(host, "rql-schema", {})
        if ok:
            return True
        time.sleep(1)
    return False


def schema_checks(host: str) -> None:
    text = must("S1 rql-schema overview", tool(host, "rql-schema", {}))
    for source in ("nodes", "root", "selection", "scenes", "files", "classes", "console", "editor"):
        check(f"S1 schema lists source '{source}'", f"  {source} —" in text, text[:400])
    check("S1 schema lists godot.find", "godot.find(" in text, text[:400])
    text = must("S2 rql-schema OmniLight3D", tool(host, "rql-schema", {"type": "OmniLight3D"}))
    check("S2 lists the Godot props of the class", "props of Godot class OmniLight3D" in text and "omni_range" in text, text)
    text = must("S3 rql-schema editor", tool(host, "rql-schema", {"type": "editor"}))
    check("S3 lists the editor calls", "NewScene" in text and "SaveScene" in text and "Reimport" in text, text)


def property_round_trips(host: str, project: pathlib.Path, d: str) -> None:
    """The former smoke_property_patches.py cases, as RQL."""
    mat, tex, grad, font, fallback = (f"{d}/{n}" for n in ("mat.tres", "tex.tres", "grad.tres", "font.tres", "fallback.tres"))
    for path, cls in ((mat, "StandardMaterial3D"), (tex, "GradientTexture2D"), (grad, "Gradient"),
                      (font, "FontVariation"), (fallback, "FontVariation")):
        must(f"R0 CreateResource {cls}", mutate(host, f"editor | call CreateResource({q(path)}, {q(cls)})"))
    res = f"godot.resource({q(mat)})"

    # R1: a C# (PascalCase) member lands in the file (saved by rql-mutate itself).
    must("R1 set Metallic", mutate(host, f"{res} | set Metallic = 0.33"))
    check("R1 file has metallic = 0.33", file_has(project, mat, r"^metallic = 0\.33$"), file_text(project, mat))
    # R2: a Godot property by its snake_case name; the value read back is the value written.
    must("R2 set props[albedo_color]", mutate(host, f'{res} | set props["albedo_color"] = "(0.2, 0.4, 0.6, 1)"'))
    check("R2 file has albedo_color", file_has(project, mat, r"^albedo_color = Color\(0\.2, 0\.4, 0\.6, 1\)$"), file_text(project, mat))
    text = must("R2 read back", query(host, f'{res} | select props["albedo_color"]'))
    check("R2 read back (0.2, 0.4, 0.6, 1)", "(0.2, 0.4, 0.6, 1)" in text, text)
    # R3: C# and Godot names in one set.
    must("R3 mixed names", mutate(host, f'{res} | set props["emission_energy_multiplier"] = 2.5, Rim = 0.25'))
    check("R3 emission_energy_multiplier = 2.5", file_has(project, mat, r"^emission_energy_multiplier = 2\.5$"), file_text(project, mat))
    check("R3 rim = 0.25", file_has(project, mat, r"^rim = 0\.25$"), file_text(project, mat))
    # R4: a resource reference given as a res:// path, then read back.
    must("R4 albedo_texture by path", mutate(host, f'{res} | set props["albedo_texture"] = {q(tex)}'))
    check("R4 file references the texture", file_has(project, mat, r'^albedo_texture = ExtResource\("[^"]+"\)$'), file_text(project, mat))
    text = must("R4 read back", query(host, f'{res} | select tex=(props["albedo_texture"].path)'))
    check("R4 read back the texture path", tex in text, text)
    # R5: the same value again changes nothing and does not re-save the file.
    before = disk(project, mat).stat().st_mtime_ns
    time.sleep(1.1)
    must("R5 same value", mutate(host, f"{res} | set Metallic = 0.33"))
    check("R5 not re-saved", disk(project, mat).stat().st_mtime_ns == before)
    # R6: an incompatible value is an error and nothing changes.
    ok, text = mutate(host, f'{res} | set props["roughness"] = "abc"')
    check("R6 string into float is an error", not ok, text)
    # R7: packed arrays.
    must("R7 packed arrays", mutate(host, f'godot.resource({q(grad)}) | set props["offsets"] = [0, 0.5, 1], '
                                         'props["colors"] = ["(1, 0, 0, 1)", "(0, 1, 0, 1)", "(0, 0, 1, 1)"]'))
    check("R7 offsets", file_has(project, grad, r"^offsets = PackedFloat32Array\(0, 0\.5, 1\)$"), file_text(project, grad))
    check("R7 colors", file_has(project, grad, r"^colors = PackedColorArray\(1, 0, 0, 1, 0, 1, 0, 1, 0, 0, 1, 1\)$"), file_text(project, grad))
    # R8: a typed Array[Font] of resource references; R9: an element it refuses is an error and keeps the content.
    must("R8 typed array", mutate(host, f'godot.resource({q(font)}) | set props["fallbacks"] = [godot.resource({q(fallback)})]'))
    check("R8 file has Array[Font]", file_has(project, font, r'^fallbacks = Array\[Font\]\(\[ExtResource\("[^"]+"\)\]\)$'), file_text(project, font))
    ok, text = mutate(host, f'godot.resource({q(font)}) | set props["fallbacks"] = [5]')
    check("R9 wrong element type is an error", not ok, text)
    check("R9 file keeps the fallback", file_has(project, font, r'^fallbacks = Array\[Font\]\(\[ExtResource\("[^"]+"\)\]\)$'), file_text(project, font))

    # Nodes: a scene with an OmniLight3D, edited, saved, read back from the .tscn.
    scene = f"{d}/scene.tscn"
    must("N0 NewScene", mutate(host, f'editor | call NewScene({q(scene)}, "Node3D", "Root")'))
    text = must("N0 AddChild Lamp", mutate(host, 'root | call AddChild("OmniLight3D", "Lamp")'))
    check("N0 the receipt names the new node", "OmniLight3D#" in text, text)
    lamp = 'nodes | where name == "Lamp"'
    must("N1 C# members", mutate(host, f'{lamp} | set LightEnergy = 3.5, Position.X = 4, LightColor = "(1, 0.5, 0, 1)"'))
    must("N2 Godot names", mutate(host, f'{lamp} | set props["omni_range"] = 7, ShadowEnabled = true'))
    ok, text = mutate(host, f'{lamp} | set props["omni_range"] = [1, 2]')
    check("N3 list into float is an error", not ok, text)
    must("N4 large int", mutate(host, f'{lamp} | set props["light_cull_mask"] = 4294967294'))
    must("N5 struct path + Godot name", mutate(host, f'{lamp} | set Position.X = 7, props["omni_range"] = 9'))
    ok, text = mutate(host, f'{lamp} | set LightIndirectEnergy = 2.25, NoSuchProperty = 1')
    check("N6 an unknown member fails the whole set (all-or-nothing)", not ok, text)
    text = must("N6 read back", query(host, f"{lamp} | select LightIndirectEnergy"))
    check("N6 nothing was applied", "2.25" not in text, text)
    must("N6 valid member alone", mutate(host, f"{lamp} | set LightIndirectEnergy = 2.25"))
    text = must("N7 dry run", mutate(host, f'{lamp} | set props["light_specular"] = 0.125', dry_run=True))
    check("N7 dry run shows the receipt", "0.125" in text, text)
    must("N7 set", mutate(host, f'{lamp} | set props["light_specular"] = 0.125'))
    text = must("N7 read back", query(host, f'{lamp} | select e=(props["light_energy"]) s=(props["light_specular"]) p=(Position)'))
    check("N7 read back the values", "3.5" in text and "0.125" in text and "(7, 0, 0)" in text, text)
    must("N8 SaveScene", mutate(host, "editor | call SaveScene()"))
    for name, pattern in (
        ("N0 node saved", node_line("Lamp", ".", "OmniLight3D")),
        ("N1 light_energy = 3.5", r"^light_energy = 3\.5$"),
        ("N6 light_indirect_energy = 2.25", r"^light_indirect_energy = 2\.25$"),
        ("N5 position.x = 7", r"^transform = Transform3D\(1, 0, 0, 0, 1, 0, 0, 0, 1, 7, 0, 0\)$"),
        ("N1 light_color", r"^light_color = Color\(1, 0\.5, 0, 1\)$"),
        ("N5 omni_range = 9", r"^omni_range = 9\.0$"),
        ("N2 shadow_enabled = true", r"^shadow_enabled = true$"),
        ("N7 light_specular = 0.125", r"^light_specular = 0\.125$"),
        ("N4 light_cull_mask = 4294967294", r"^light_cull_mask = 4294967294$"),
    ):
        check(name, file_has(project, scene, pattern), file_text(project, scene))

    # R10: delete / move keep the resource cache in step with the files.
    cached, moved = f"{d}/cached.tres", f"{d}/moved.tres"
    must("R10 create", mutate(host, f'editor | call CreateResource({q(cached)}, "StandardMaterial3D")'))
    must("R10 edit (loads + caches it)", mutate(host, f"godot.resource({q(cached)}) | set Metallic = 0.25"))
    must("R10 delete", mutate(host, f"editor | call DeleteResource({q(cached)})"))
    must("R10 create after delete", mutate(host, f'editor | call CreateResource({q(cached)}, "StandardMaterial3D")'))
    must("R10 edit again", mutate(host, f"godot.resource({q(cached)}) | set Metallic = 0.5"))
    must("R10 move", mutate(host, f"editor | call MoveResource({q(cached)}, {q(moved)})"))
    must("R10 create at the old path after move", mutate(host, f'editor | call CreateResource({q(cached)}, "StandardMaterial3D")'))
    must("R10 edit the moved file", mutate(host, f"godot.resource({q(moved)}) | set Roughness = 0.75"))
    check("R10 moved resource keeps its pre-move change", file_has(project, moved, r"^metallic = 0\.5$"), file_text(project, moved))
    check("R10 moved resource takes the new change", file_has(project, moved, r"^roughness = 0\.75$"), file_text(project, moved))

    # R12: moving a resource also moves its built-in sub-resources ('<path>::<id>') in the cache.
    with_sub, moved_sub = f"{d}/with_sub.tres", f"{d}/moved_sub.tres"
    disk(project, with_sub).write_text(
        '[gd_resource type="StandardMaterial3D" load_steps=2 format=3]\n\n'
        '[sub_resource type="GradientTexture2D" id="GradientTexture2D_sub"]\n\n'
        '[resource]\nalbedo_texture = SubResource("GradientTexture2D_sub")\n', encoding="utf-8")
    must("R12 reimport the hand-written file", mutate(host, f"editor | call Reimport([{q(with_sub)}])"))
    must("R12 load (caches it)", query(host, f"godot.resource({q(with_sub)}) | select path"))
    must("R12 move", mutate(host, f"editor | call MoveResource({q(with_sub)}, {q(moved_sub)})"))
    text = must("R12 read back", query(host, f'godot.resource({q(moved_sub)}) | select tex=(props["albedo_texture"].path)'))
    check("R12 sub-resource moved with its owner", f"{moved_sub}::GradientTexture2D_sub" in text, text)

    # R11: a deleted script is not served from the cache anymore (script-* tools take RQL arguments).
    script = f"{d}/lamp.gd"
    must("R11 script-create", tool(host, "script-create", {"scriptPath": script, "content": "extends OmniLight3D\n"}))
    must("R11 attach", tool(host, "script-attach-to-node", {"nodeRef": "Lamp", "scriptPath": script}))
    text = must("R11 read script path", query(host, f"{lamp} | select script"))
    check("R11 the node reports its script", script in text, text)
    must("R11 detach", tool(host, "script-attach-to-node", {"nodeRef": "Lamp", "scriptPath": ""}))
    must("R11 script-delete", tool(host, "script-delete", {"scriptPath": script}))
    ok, text = tool(host, "script-attach-to-node", {"nodeRef": "Lamp", "scriptPath": script})
    check("R11 deleted script cannot be attached", not ok, text)

    # R13: MoveResource / DeleteResource carry the '.uid' sidecar with the file.
    mover, moved_script = f"{d}/mover.gd", f"{d}/mover_moved.gd"
    must("R13 script-create", tool(host, "script-create", {"scriptPath": mover, "content": "extends Node\n"}))
    uid_file = disk(project, mover + ".uid")
    if not uid_file.exists():
        uid_file.write_text("uid://smokeprobe" + time.strftime("%H%M%S") + "\n", encoding="utf-8")
    uid = uid_file.read_text(encoding="utf-8").strip()
    must("R13 move", mutate(host, f"editor | call MoveResource({q(mover)}, {q(moved_script)})"))
    moved_uid = disk(project, moved_script + ".uid")
    check("R13 .uid moved with the script", moved_uid.exists() and moved_uid.read_text(encoding="utf-8").strip() == uid)
    check("R13 no orphaned .uid at the old path", not uid_file.exists())
    must("R13 delete", mutate(host, f"editor | call DeleteResource({q(moved_script)})"))
    check("R13 .uid deleted with the script", not moved_uid.exists())

    # R14: moving the open scene makes the editor save it at the new path, not recreate the old file.
    tab, tab_moved = f"{d}/tab.tscn", f"{d}/tab_moved.tscn"
    must("R14 NewScene", mutate(host, f'editor | call NewScene({q(tab)}, "Node3D", "Tab")'))
    must("R14 move the open scene", mutate(host, f"editor | call MoveResource({q(tab)}, {q(tab_moved)})"))
    must("R14 save", mutate(host, "editor | call SaveScene()"))
    check("R14 saving does not recreate the old scene file", not disk(project, tab).exists())
    check("R14 the scene is at its new path", disk(project, tab_moved).exists())

    # R15: deleting the open scene does not let the next save recreate it (SaveScene refuses: the file is gone).
    deleted_open_scene(host, project, d)

    # R16: an orphaned sidecar at the destination is not adopted by the moved file.
    plain, adopted = f"{d}/plain.tres", f"{d}/adopted.tres"
    must("R16 create", mutate(host, f'editor | call CreateResource({q(plain)}, "Resource")'))
    disk(project, adopted + ".uid").write_text("uid://bogus_orphan\n", encoding="utf-8")
    must("R16 move", mutate(host, f"editor | call MoveResource({q(plain)}, {q(adopted)})"))
    check("R16 orphaned destination sidecar removed", not disk(project, adopted + ".uid").exists())


def deleted_open_scene(host: str, project: pathlib.Path, d: str) -> None:
    gone = f"{d}/gone.tscn"
    must("R15 NewScene", mutate(host, f'editor | call NewScene({q(gone)}, "Node3D", "Gone")'))
    must("R15 delete the open scene", mutate(host, f"editor | call DeleteResource({q(gone)})"))
    # Godot 4.5+ closes the deleted scene (SaveScene then saves whichever scene is edited); before that it stays open and
    # SaveScene refuses because its file is gone. Either way the deleted file must not come back.
    mutate(host, "editor | call SaveScene()")
    check("R15 saving does not recreate a deleted open scene", not disk(project, gone).exists())


def guide_examples(host: str, project: pathlib.Path) -> None:
    """Every example of the agent guide, in order, against the fixture scene its examples are written for."""
    examples = json.loads((ROOT / "Godot-Tests" / "rql-guide-examples.json").read_text(encoding="utf-8"))
    for path in ("res://scenes/new.tscn", "res://scenes/level.tscn", "res://scenes/enemy.tscn", "res://scenes/player.gd", "res://scenes/game.gd",
                 "res://materials/red.tres", "res://materials/blue.tres", "res://materials/old/blue.tres"):
        if disk(project, path).exists():
            must(f"G0 clean {path}", mutate(host, f"editor | call DeleteResource({q(path)})"))
    must("G0 enemy scene", mutate(host, 'editor | call NewScene("res://scenes/enemy.tscn", "Node3D", "Enemy")'))
    must("G0 level scene", mutate(host, 'editor | call NewScene("res://scenes/level.tscn", "Node3D", "Level")'))
    for cls, name in (("Node3D", "Player"), ("Node3D", "Enemy1"), ("Node3D", "Enemy2"), ("OmniLight3D", "Lamp"),
                      ("Node3D", "Crate"), ("Node", "Old")):
        must(f"G0 AddChild {name}", mutate(host, f"root | call AddChild({q(cls)}, {q(name)})"))
    must("G0 material", mutate(host, 'editor | call CreateResource("res://materials/red.tres", "StandardMaterial3D")'))
    smoke_rql_models.guide_fixture(host, project)
    must("G0 save", mutate(host, "editor | call SaveScene()"))

    for i, example in enumerate(examples["query"], 1):
        must(f"GQ{i} {example}", query(host, example))
    for i, example in enumerate(examples["mutate"], 1):
        must(f"GM{i} {example}", mutate(host, example))

    level = "res://scenes/level.tscn"
    for name, pattern in (
        ("G player moved", r"^transform = Transform3D\(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0\)$"),
        ("G enemies hidden", r"^visible = false$"),
        ("G lamp energy doubled", r"^light_energy = 2\.0$"),
        ("G lamp color", r"^light_color = Color\(1, 0\.8, 0\.53"),
        ("G Props added", node_line("Props", ".", "Node3D")),
        ("G enemy instanced under Props", node_line("Enemy", "Props", instance=True)),
        ("G Crate reparented under Props", node_line("Crate", "Props", "Node3D")),
        ("G duplicate kept next to the original", node_line("Crate2", ".", "Node3D")),
    ):
        check(name, file_has(project, level, pattern), file_text(project, level))
    check("G Old removed", '[node name="Old"' not in file_text(project, level), file_text(project, level))
    level_text = file_text(project, level)
    crate = re.search(node_line("Crate", "Props"), level_text, re.MULTILINE)
    enemy = re.search(node_line("Enemy", "Props"), level_text, re.MULTILINE)
    check("G Crate is the first child of Props (MoveChild(0))", bool(crate and enemy and crate.start() < enemy.start()), level_text)
    check("G red material saved", file_has(project, "res://materials/red.tres", r"^albedo_color = Color\(1, 0, 0, 1\)$"),
          file_text(project, "res://materials/red.tres"))
    check("G blue material deleted", not disk(project, "res://materials/old/blue.tres").exists()
          and not disk(project, "res://materials/blue.tres").exists())
    check("G new scene created", disk(project, "res://scenes/new.tscn").exists())
    text = must("G edited scene back to level", query(host, "editor | select editedScene"))
    check("G OpenScene switched back", level in text, text)
    smoke_rql_models.guide_checks(host, project)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--host", default="http://localhost:8080")
    ap.add_argument("--project", required=True, type=pathlib.Path, help="Godot project root open in the editor")
    ap.add_argument("--dir", default="res://rql_smoke", help="res:// folder of the read-back cases; each run uses a fresh subfolder")
    ap.add_argument("--wait", type=float, default=120, help="seconds to wait for the editor's tools to answer")
    ap.add_argument("--require-test-hooks", action="store_true",
                    help="fail instead of skipping the undo checks when the editor runs without GODOT_MCP_RQL_TEST_HOOKS=1")
    a = ap.parse_args()
    host, project = a.host.rstrip("/"), a.project
    if not wait_ready(host, a.wait):
        print(f"rql-schema never answered on {host} within {a.wait:.0f}s", flush=True)
        return 1
    d = f"{a.dir.rstrip('/')}/{time.strftime('%Y%m%d-%H%M%S')}-{time.time_ns() // 1_000_000 % 1000:03d}"
    schema_checks(host)
    property_round_trips(host, project, d)
    smoke_rql_models.run(host, project, d, a.require_test_hooks)
    guide_examples(host, project)
    print(f"\n{len(FAILURES)} failure(s)" + (": " + ", ".join(FAILURES) if FAILURES else ""), flush=True)
    return 1 if FAILURES else 0


if __name__ == "__main__":
    sys.exit(main())
