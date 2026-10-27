#!/usr/bin/env python3
"""
Live cases of what an agent reads back after a change, run by scripts/smoke_rql.py against a running editor: the console
source without the echo of the agent's own tool calls and without a second copy of every plugin error, and files written
into new folders (scenes, resources, scripts) visible to the files source, godot.resources and the class list at once.

Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import pathlib
import re
import secrets

from smoke_rql_lib import check, must, mutate, q, query, tool
from smoke_rql_models import fails, has, lacks


def run(host: str, project: pathlib.Path, d: str) -> None:
    console(host)
    new_folders(host, d)
    recreated_scene(host, d)


def recreated_scene(host: str, d: str) -> None:
    """NewScene onto the path of a deleted scene that is still open shows the new scene, not the old tab."""
    path = f"{d}/fb_recreated.tscn"
    must("R1 first scene", mutate(host, f'editor | call NewScene({q(path)}, "Node3D", "First")'))
    must("R1 old child", mutate(host, 'root | call AddChild("Node3D", "OldChild")'))
    must("R1 save", mutate(host, "editor | call SaveScene()"))
    # The old scene stays open in a background tab (like a tab the editor restored at startup).
    must("R1 another scene", mutate(host, f'editor | call NewScene({q(d + "/fb_other.tscn")}, "Node3D", "Other")'))
    must("R1 delete the open scene's file", mutate(host, f"editor | call DeleteResource({q(path)}, true)"))
    must("R1 new scene at the same path", mutate(host, f'editor | call NewScene({q(path)}, "Node2D", "Second")'))
    text = must("R1 read", query(host, "root | select name class childCount"))
    has("R1 the new root, no old child", text, "Second|Node2D|0")


def console(host: str) -> None:
    """The console source holds the editor's log, not the tool-call echo; a plugin error is captured once."""
    tag = secrets.token_hex(4)
    fails("C1 a failing query (its error is echoed by the framework)", query(host, f'godot.find("NoSuchNode{tag}")'), "no node")
    must("C1 an engine error", tool(host, "script-execute", {"csharpCode": f'GD.PushError("feedback-probe-{tag}"); 1'}))
    text = must("C1 recent console", query(host, "console | sort Sequence desc | first 200 | select LogType Message"))
    lacks("C1 no tool-call echo", text, "McpToolManager: Run tool", "Response to AI", f"NoSuchNode{tag}")
    # An engine error reaches the console through the engine logger (OS.AddLogger, Godot 4.5+): there exactly once;
    # older engines do not capture it at all, so there it must just not be doubled.
    version = must("C1 version", tool(host, "script-execute", {"csharpCode": 'Engine.GetVersionInfo()["string"].AsString()'}))
    match = re.search(r"(\d+)\.(\d+)", version)
    engine_logger = not match or (int(match[1]), int(match[2])) >= (4, 5)
    count = text.count(f"feedback-probe-{tag}")
    check("C1 the engine error once", count == 1 if engine_logger else count <= 1, text)


def new_folders(host: str, d: str) -> None:
    """A file written into a folder the editor never scanned is visible right away."""
    tag = secrets.token_hex(3)
    scene, res, folder = f"{d}/nf_{tag}/scenes/a.tscn", f"{d}/nf_{tag}/mats/m.tres", f"{d}/nf_{tag}/empty"
    must("F1 NewScene in a new folder", mutate(host, f'editor | call NewScene({q(scene)}, "Node3D", "A")'))
    has("F1 files sees it", must("F1 files", query(host, f"files | where path == {q(scene)} | select path")), scene)
    must("F2 CreateResource in a new folder", mutate(host, f'editor | call CreateResource({q(res)}, "StandardMaterial3D")'))
    has("F2 godot.resources(type, folder) finds it",
        must("F2 resources", query(host, f'godot.resources("StandardMaterial3D", {q(d + "/nf_" + tag + "/mats")}) | select path')), res)
    must("F3 CreateFolder", mutate(host, f"editor | call CreateFolder({q(folder)})"))
    must("F3 the folder is known", query(host, f'godot.resources("Resource", {q(folder)}) | count'))
    cls = "Nf" + tag.capitalize()
    gd = f"{d}/nf_{tag}/scripts/{cls.lower()}.gd"
    must("F4 class_name script in a new folder", tool(host, "script-create", {"scriptPath": gd, "content": f"class_name {cls}\nextends Node3D\n"}))
    must("F4 the class is usable at once", mutate(host, f'root | call AddChild({q(cls)}, "Scripted")'))
    has("F4 the node has the script", must("F4 read", query(host, 'godot.find("Scripted") | select script')), gd)
