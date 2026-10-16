#!/usr/bin/env python3
"""
Read-back smoke for the property-patch tools (`resource-modify`, `node-modify`, `resource-get-data`).

Every case calls a tool and then verifies the result where it cannot lie: the `.tres` / `.tscn` text
that Godot itself wrote to disk. It guards three regressions (issues #370 and the follow-up):

  * a write the tool reported as a success but that never landed (e.g. `System.Double` into a
    `float` property, on both the path-patch and the node surface);
  * a property entry returned by `resource-get-data` that cannot be sent back to
    `resource-modify` as-is (snake_case name + `Godot.Variant` value);
  * a rejected or unchanged write that is still counted as a change.

It does NOT launch anything. Point it at a running local `gamedev-mcp-server` whose Godot editor
(with this addon) has the project at `--project` open:

    gamedev-mcp-server port=8080 client-transport=streamableHttp authorization=none &
    GODOT_MCP_CONNECTION_MODE=Custom GODOT_MCP_HOST=http://localhost:8080 \\
        godot --headless --path <project> --editor --quit-after 600000 &
    python scripts/smoke_property_patches.py --host http://localhost:8080 --project <project>

Exit code 0 = every case passed. Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys
import time
import urllib.error
import urllib.request

FAILURES: list[str] = []


def call(host: str, tool: str, args: dict) -> object:
    req = urllib.request.Request(
        f"{host}/api/tools/{tool}",
        data=json.dumps(args).encode(),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            body = json.loads(resp.read().decode())
    except urllib.error.HTTPError as e:
        body = json.loads(e.read().decode() or "{}") or {"error": str(e)}
    if "error" in body:
        raise RuntimeError(f"{tool}: {body['error']}")
    return body["structured"]["result"]


def log_types(logs: list) -> dict:
    out: dict = {}
    for entry in logs:
        out.setdefault(entry["Type"], []).append(entry["Message"])
    return out


def check(name: str, ok: bool, detail: str = "") -> None:
    print(f"[{'PASS' if ok else 'FAIL'}] {name}" + (f" -- {detail}" if detail and not ok else ""))
    if not ok:
        FAILURES.append(name)


def expect_logs(name: str, logs: list, errors: bool, saved_marker: str | None) -> None:
    types = log_types(logs)
    has_errors = bool(types.get("Error"))
    check(f"{name}: errors={errors}", has_errors == errors, json.dumps(types)[:600])
    if saved_marker is not None:
        saved = any(saved_marker in m for m in types.get("Success", []))
        check(f"{name}: reported saved", saved, json.dumps(types)[:600])


def file_has(project: pathlib.Path, res_path: str, pattern: str) -> bool:
    text = (project / res_path.replace("res://", "")).read_text(encoding="utf-8")
    return re.search(pattern, text, re.MULTILINE) is not None


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--host", default="http://localhost:8080")
    ap.add_argument("--project", required=True, type=pathlib.Path, help="Godot project root open in the editor")
    ap.add_argument("--dir", default="res://smoke_property_patches", help="res:// folder; each run uses a fresh subfolder")
    a = ap.parse_args()
    host, project = a.host.rstrip("/"), a.project
    # A fresh folder per run: right after resource-delete, resource-create can still see the cached resource.
    d = f"{a.dir.rstrip('/')}/{time.strftime('%Y%m%d-%H%M%S')}"

    mat, tex, scene = f"{d}/mat.tres", f"{d}/tex.tres", f"{d}/scene.tscn"
    call(host, "resource-create", {"resourcePath": mat, "typeClassName": "StandardMaterial3D"})
    call(host, "resource-create", {"resourcePath": tex, "typeClassName": "GradientTexture2D"})
    ref = {"resourcePath": mat}
    saved = f"Resource '{mat}' modified and saved."

    def modify(**kw) -> list:
        return call(host, "resource-modify", {"resourceRef": ref, **kw})

    # R1: a mismatched numeric typeName on a C# (PascalCase) path lands instead of being dropped.
    logs = modify(pathPatches=[{"path": "Metallic", "value": {"typeName": "System.Double", "value": 0.33}}])
    expect_logs("R1 Metallic System.Double", logs, errors=False, saved_marker=saved)
    check("R1 file has metallic = 0.33", file_has(project, mat, r"^metallic = 0\.33$"))

    # R2: a get-data entry (snake_case name + Godot.Variant value) goes back verbatim.
    data = call(host, "resource-get-data", {"resourceRef": ref})
    props = {p["name"]: p for p in data.get("props") or []}
    check("R2 get-data lists albedo_color", "albedo_color" in props, ", ".join(list(props)[:10]))
    entry = dict(props.get("albedo_color", {}))
    entry["value"] = {"variantType": "Color", "value": [0.2, 0.4, 0.6, 1]}
    logs = modify(pathPatches=[{"path": entry.get("name", "albedo_color"), "value": entry}])
    expect_logs("R2 albedo_color round-trip", logs, errors=False, saved_marker=saved)
    check("R2 file has albedo_color", file_has(project, mat, r"^albedo_color = Color\(0\.2, 0\.4, 0\.6, 1\)$"))

    # R3: jsonPatch accepts Godot names next to C# names.
    logs = modify(jsonPatch=json.dumps({"emission_energy_multiplier": 2.5, "Rim": 0.25}))
    expect_logs("R3 jsonPatch mixed names", logs, errors=False, saved_marker=saved)
    check("R3 file has emission_energy_multiplier", file_has(project, mat, r"^emission_energy_multiplier = 2\.5$"))
    check("R3 file has rim = 0.25", file_has(project, mat, r"^rim = 0\.25$"))

    # R4: a sub-resource reference, then the get-data entry for it sent back as-is.
    logs = modify(pathPatches=[{"path": "albedo_texture", "value": {"typeName": "Godot.Texture2D", "value": {"resourcePath": tex}}}])
    expect_logs("R4 albedo_texture ref", logs, errors=False, saved_marker=saved)
    check("R4 file references the texture", file_has(project, mat, r'^albedo_texture = ExtResource\("[^"]+"\)$'))
    props = {p["name"]: p for p in call(host, "resource-get-data", {"resourceRef": ref}).get("props") or []}
    tex_entry = props.get("albedo_texture", {})
    check("R4 get-data returns the texture path", tex in json.dumps(tex_entry), json.dumps(tex_entry)[:300])

    # R5: the same value again is not a change and does not re-save.
    logs = modify(pathPatches=[{"path": "metallic", "value": {"typeName": "Godot.Variant", "value": 0.33}}])
    expect_logs("R5 same value", logs, errors=False, saved_marker=None)
    check("R5 not re-saved", not any(saved in m for m in log_types(logs).get("Success", [])), json.dumps(logs)[:400])

    # R6: an incompatible value is an error, not a success.
    logs = modify(pathPatches=[{"path": "roughness", "value": {"typeName": "Godot.Variant", "value": "abc"}}])
    expect_logs("R6 string into float", logs, errors=True, saved_marker=None)

    # R7: collections round-trip. Packed arrays edited from their get-data entries.
    grad = f"{d}/grad.tres"
    call(host, "resource-create", {"resourcePath": grad, "typeClassName": "Gradient"})
    gprops = {p["name"]: p for p in call(host, "resource-get-data", {"resourceRef": {"resourcePath": grad}}).get("props") or []}
    offsets, colors = dict(gprops.get("offsets", {})), dict(gprops.get("colors", {}))
    check("R7 get-data inlines packed arrays", offsets.get("value", {}).get("value") == [0, 1], json.dumps(offsets)[:300])
    offsets["value"] = {"variantType": "PackedFloat32Array", "value": [0, 0.5, 1]}
    colors["value"] = {"variantType": "PackedColorArray", "value": [[1, 0, 0, 1], [0, 1, 0, 1], [0, 0, 1, 1]]}
    logs = call(host, "resource-modify", {"resourceRef": {"resourcePath": grad}, "pathPatches": [
        {"path": "offsets", "value": offsets}, {"path": "colors", "value": colors}]})
    expect_logs("R7 packed arrays", logs, errors=False, saved_marker=f"Resource '{grad}' modified and saved.")
    check("R7 file has offsets", file_has(project, grad, r"^offsets = PackedFloat32Array\(0, 0\.5, 1\)$"))
    check("R7 file has colors", file_has(project, grad, r"^colors = PackedColorArray\(1, 0, 0, 1, 0, 1, 0, 1, 0, 0, 1, 1\)$"))

    # R8: a typed Array[Font] of resource references and an int-keyed Dictionary; then the get-data entry back.
    font, fallback = f"{d}/font.tres", f"{d}/fallback.tres"
    for path in (font, fallback):
        call(host, "resource-create", {"resourcePath": path, "typeClassName": "FontVariation"})
    fref = {"resourcePath": font}
    logs = call(host, "resource-modify", {"resourceRef": fref, "pathPatches": [
        {"path": "fallbacks", "value": {"typeName": "Godot.Variant", "value": {"variantType": "Array", "value": [
            {"variantType": "Object", "value": {"resourcePath": fallback}}]}}},
        {"path": "variation_opentype", "value": {"typeName": "Godot.Variant", "value": {"variantType": "Dictionary", "value": [
            {"key": 2003265652, "value": 700}]}}}]})
    expect_logs("R8 typed array + dictionary", logs, errors=False, saved_marker=f"Resource '{font}' modified and saved.")
    check("R8 file has Array[Font]", file_has(project, font, r'^fallbacks = Array\[Font\]\(\[ExtResource\("[^"]+"\)\]\)$'))
    check("R8 file has the dictionary", file_has(project, font, r"^2003265652: 700$"))
    fprops = {p["name"]: p for p in call(host, "resource-get-data", {"resourceRef": fref}).get("props") or []}
    logs = call(host, "resource-modify", {"resourceRef": fref, "pathPatches": [
        {"path": "fallbacks", "value": fprops.get("fallbacks", {})}, {"path": "variation_opentype", "value": fprops.get("variation_opentype", {})}]})
    expect_logs("R8 get-data entries sent back unchanged", logs, errors=False, saved_marker=None)
    check("R8 unchanged entries are not re-saved", not any("modified and saved" in m for m in log_types(logs).get("Success", [])), json.dumps(logs)[:400])

    # R9: an element the typed array refuses is an error and keeps the existing content.
    logs = call(host, "resource-modify", {"resourceRef": fref, "pathPatches": [
        {"path": "fallbacks", "value": {"typeName": "Godot.Variant", "value": {"variantType": "Array", "value": [5]}}}]})
    expect_logs("R9 wrong element type", logs, errors=True, saved_marker=None)
    check("R9 file keeps the fallback", file_has(project, font, r'^fallbacks = Array\[Font\]\(\[ExtResource\("[^"]+"\)\]\)$'))

    # Nodes: build a scene with an OmniLight3D, patch it, save, read the .tscn.
    call(host, "scene-create", {"resourcePath": scene, "rootTypeClassName": "Node3D", "rootName": "Root"})
    call(host, "scene-open", {"resourcePath": scene})
    lamp = call(host, "node-create", {"typeClassName": "OmniLight3D", "name": "Lamp"})
    node = {"nodeRef": {"instanceId": lamp["instanceId"]}}

    logs = call(host, "node-modify", {**node, "pathPatches": [
        {"path": "LightEnergy", "value": {"typeName": "System.Double", "value": 3.5}},
        {"path": "position/x", "value": {"typeName": "System.Double", "value": 4}},
        {"path": "LightColor", "value": {"typeName": "Godot.Variant", "value": {"variantType": "Color", "value": [1, 0.5, 0, 1]}}},
    ]})
    expect_logs("N1 node pathPatches", logs, errors=False, saved_marker=None)
    logs = call(host, "node-modify", {**node, "jsonPatch": json.dumps({"omni_range": 7, "ShadowEnabled": True})})
    expect_logs("N2 node jsonPatch", logs, errors=False, saved_marker=None)
    logs = call(host, "node-modify", {**node, "pathPatches": [{"path": "omni_range", "value": {"typeName": "Godot.Variant", "value": [1, 2]}}]})
    expect_logs("N3 array into float", logs, errors=True, saved_marker=None)

    # A one-off change of a large integer is still a change (no float tolerance between ints).
    logs = call(host, "node-modify", {**node, "pathPatches": [{"path": "light_cull_mask", "value": {"typeName": "Godot.Variant", "value": 4294967294}}]})
    expect_logs("N4 large int change", logs, errors=False, saved_marker=None)
    check("N4 counted as a change", not any("No modifications" in m for m in log_types(logs).get("Warning", [])), json.dumps(logs)[:400])

    # N7: node-get-data entries round-trip into node-modify.
    nprops = {p["name"]: p for p in call(host, "node-get-data", node).get("props") or []}
    check("N7 node-get-data lists light_energy", "light_energy" in nprops, ", ".join(list(nprops)[:12]))
    logs = call(host, "node-modify", {**node, "pathPatches": [{"path": "light_energy", "value": nprops.get("light_energy", {})}]})
    check("N7 an unchanged entry is not a change", any("No modifications" in m for m in log_types(logs).get("Warning", [])), json.dumps(logs)[:400])
    entry = {**nprops.get("light_specular", {}), "value": {"variantType": "Float", "value": 0.125}}
    logs = call(host, "node-modify", {**node, "pathPatches": [{"path": "light_specular", "value": entry}]})
    expect_logs("N7 edited entry", logs, errors=False, saved_marker=None)

    call(host, "scene-save", {})
    check("N1 light_energy = 3.5", file_has(project, scene, r"^light_energy = 3\.5$"))
    check("N1 position.x = 4", file_has(project, scene, r"^transform = Transform3D\(1, 0, 0, 0, 1, 0, 0, 0, 1, 4, 0, 0\)$"))
    check("N1 light_color", file_has(project, scene, r"^light_color = Color\(1, 0\.5, 0, 1\)$"))
    check("N2 omni_range = 7", file_has(project, scene, r"^omni_range = 7\.0$"))
    check("N2 shadow_enabled = true", file_has(project, scene, r"^shadow_enabled = true$"))
    check("N7 light_specular = 0.125", file_has(project, scene, r"^light_specular = 0\.125$"))
    check("N4 light_cull_mask = 4294967294", file_has(project, scene, r"^light_cull_mask = 4294967294$"))

    print(f"\n{len(FAILURES)} failure(s)" + (": " + ", ".join(FAILURES) if FAILURES else ""))
    return 1 if FAILURES else 0


if __name__ == "__main__":
    sys.exit(main())
