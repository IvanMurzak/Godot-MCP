#!/usr/bin/env python3
"""
Live cases of what wave-6 agent runs (all extensions installed, MCP-only DeepSeek) could not do, run by scripts/smoke_rql.py
against a running editor: the tools source (hidden tools included), extension guides through rql-schema (when an extension
is installed), empty folders (folders source, editor.DeleteFolder), Godot property names in object literals, and reading
groups without engine errors in the editor console. Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import pathlib
import re

from smoke_rql_lib import check, must, mutate, q, query, tool
from smoke_rql_models import fails, has, value_is


def run(host: str, project: pathlib.Path, d: str) -> None:
    tools_source(host)
    extension_guides(host)
    folders(host, project, d)
    changed_on_disk(host, project, d)
    literal_names(host, d)
    groups_without_errors(host, d)


def tools_source(host: str) -> None:
    text = must("W6T tools source", query(host, 'tools | where name == "rql-query" | select name visible'))
    has("W6T rql-query listed and visible", text, "rql-query|true")
    has("W6T schema names the hidden-tool rule", must("W6T schema", tool(host, "rql-schema", {"type": "tools"})), "visible")


def extension_guides(host: str) -> None:
    overview = must("W6G schema overview", tool(host, "rql-schema", {}))
    if "installed extensions" not in overview:
        print("[SKIP] W6G no extension installed in this project", flush=True)
        return
    topic = re.search(r"installed extensions[^\n]*\n  ([^\n]+?) — ", overview)
    check("W6G overview lists a topic", topic is not None, overview[:600])
    if topic:
        guide = must("W6G topic guide", tool(host, "rql-schema", {"type": topic.group(1).lower()}))
        has("W6G guide printed", guide, "(extension)")


def folders(host: str, project: pathlib.Path, d: str) -> None:
    empty, full = f"{d}/w6_empty", f"{d}/w6_full"
    must("W6F create empty", mutate(host, f"editor | call CreateFolder({q(empty)})"))
    must("W6F create full", mutate(host, f"editor | call CreateFolder({q(full)})"))
    must("W6F a file in full", mutate(host, f'editor | call CreateResource({q(full + "/m.tres")}, "StandardMaterial3D")'))
    rows = must("W6F folders", query(host, f'folders | where path starts-with {q(d + "/w6_")} | select path empty'))
    has("W6F empty folder reads empty", rows, "w6_empty/|true")
    has("W6F folder with a file reads not empty", rows, "w6_full/|false")
    fails("W6F a non-empty folder is refused", mutate(host, f"editor | call DeleteFolder({q(full)})"), "not empty")
    # A file the editor does not list (a hidden placeholder, a native library) makes the folder not empty either.
    hidden = f"{d}/w6_hidden"
    must("W6F create hidden", mutate(host, f"editor | call CreateFolder({q(hidden)})"))
    (project / hidden.replace("res://", "") / ".gitkeep").write_text("", encoding="utf-8")
    rows = must("W6F folders again", query(host, f'folders | where path starts-with {q(hidden)} | select path fileCount empty'))
    has("W6F a folder with only a file the editor does not list reads not empty", rows, "w6_hidden/|0|false")
    fails("W6F and DeleteFolder refuses it", mutate(host, f"editor | call DeleteFolder({q(hidden)})"), "not empty")
    fails("W6F DeleteResource on a folder names DeleteFolder", mutate(host, f"editor | call DeleteResource({q(empty + '/')})"), "DeleteFolder")
    fails("W6F a missing folder is refused", mutate(host, f'editor | call DeleteFolder({q(d + "/w6_nope")})'), "no folder")
    must("W6F dry run", tool(host, "rql-mutate", {"query": f"editor | call DeleteFolder({q(empty)})", "dry_run": True}))
    check("W6F dry run keeps it", (project / empty.replace("res://", "")).is_dir())
    must("W6F delete empty", mutate(host, f"editor | call DeleteFolder({q(empty)})"))
    check("W6F deleted on disk", not (project / empty.replace("res://", "")).exists())


def changed_on_disk(host: str, project: pathlib.Path, d: str) -> None:
    # Wave 7: a .tres an earlier query loaded and that then changed on disk stayed stale after Reimport([path]).
    path = f"{d}/w6_disk.tres"
    file = project / path.replace("res://", "")
    material = '[gd_resource type="StandardMaterial3D" format=3]\n\n[resource]\nalbedo_color = Color({}, 1)\n'
    file.write_text(material.format("1, 0, 0"), encoding="utf-8")
    must("W6D register", mutate(host, "editor | call Reimport()"))
    value_is("W6D first read", must("W6D read", query(host, f"godot.resource({q(path)}) | select AlbedoColor")), "(1, 0, 0, 1)")
    file.write_text(material.format("0, 0, 1"), encoding="utf-8")
    must("W6D reimport the changed file", mutate(host, f"editor | call Reimport([{q(path)}])"))
    value_is("W6D the loaded resource shows the file's new data",
             must("W6D read again", query(host, f"godot.resource({q(path)}) | select AlbedoColor")), "(0, 0, 1, 1)")
    file.write_text('[gd_resource type="StandardMaterial3D" format=3]\n\n[resource]\nalbedo_color = Color(0, 1\n', encoding="utf-8")
    has("W6D a broken file is reported", must("W6D reimport the broken file", mutate(host, f"editor | call Reimport([{q(path)}])")), "NOT refreshed")
    value_is("W6D and the loaded copy is kept",
             must("W6D read after the broken file", query(host, f"godot.resource({q(path)}) | select AlbedoColor")), "(0, 0, 1, 1)")


def literal_names(host: str, d: str) -> None:
    must("W6L scene", mutate(host, f'editor | call NewScene({q(d + "/w6_lit.tscn")}, "Node3D", "Root")'))
    must("W6L mesh", mutate(host, 'root | call AddChild("MeshInstance3D", "M")'))
    must("W6L snake_case keys", mutate(host, 'godot.find("M") | set Mesh = {class: "BoxMesh", size: "(2, 1, 1)", '
                                          'material: {class: "StandardMaterial3D", albedo_color: "#ff0000", metallic_specular: 0.2}}'))
    value_is("W6L albedo_color applied", must("W6L read", query(host, 'godot.find("M") | select Mesh.Material.AlbedoColor')), "(1, 0, 0, 1)")


def groups_without_errors(host: str, d: str) -> None:
    """A node whose children belong to the scene root: reading its groups packed it alone and Godot logged
    '!is_ancestor_of' errors into the console agents read."""
    must("W6E parent", mutate(host, 'root | call AddChild("Node3D", "Holder")'))
    must("W6E child", mutate(host, 'godot.find("Holder") | call AddChild("Node3D", "Kid")'))
    must("W6E group", mutate(host, 'godot.find("Holder") | call AddToGroup("w6")'))
    must("W6E save", mutate(host, "editor | call SaveScene()"))
    version = must("W6E version", query(host, "editor | select godotVersion"))
    if re.search(r"4\.[34]\.", version):
        print("[SKIP] W6E the console captures engine errors from Godot 4.5 on (OS.AddLogger)", flush=True)
        return
    must("W6E clear console", mutate(host, "editor | call ClearConsole()"))
    has("W6E groups read", must("W6E groups", query(host, 'godot.find("Holder") | select groups')), "w6")
    must("W6E groups source", query(host, "groups | select name count"))
    errors = must("W6E console", query(host, 'console | where LogType == "Error" and Message contains "is_ancestor_of" | count'))
    check("W6E no is_ancestor_of errors", errors.strip().splitlines()[-1].strip() == "0", errors)
