#!/usr/bin/env python3
"""
Live cases of the wave-7 fixes, run by scripts/smoke_rql.py against a running editor: usedBy and DeleteResource see a file
named as a res:// string (a script's path constant, project.godot's main scene); two scene switches in one call both take
effect (OpenScene selects an open tab although Godot ignores a second switch in a frame, ReloadScene reloads before it
switches); Reimport of a new file in a folder the editor never scanned scans for it and reports it queued, and the
editor has imported it by the next call. Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import pathlib
import time

from smoke_rql_lib import check, disk, file_text, must, mutate, q, query
from smoke_rql_models import fails, has, lacks, value_is, write_png


def run(host: str, project: pathlib.Path, d: str) -> None:
    string_references(host, project, d)
    switches_in_one_call(host, project, d)
    reimport_new_folder(host, project, d)


def string_references(host: str, project: pathlib.Path, d: str) -> None:
    # F11: an exported `NextLevel = "res://…/level2.tscn"` was the only reference to a level, so a cleanup deleted it.
    level, loader = f"{d}/w7_level2.tscn", f"{d}/w7_loader.gd"
    must("W7 level", mutate(host, f'editor | call NewScene({q(level)}, "Node3D", "Level2")'))
    disk(project, loader).write_text(f'extends Node\n\nconst NEXT_LEVEL := "{level}"\n', encoding="utf-8")
    must("W7 register the loader", mutate(host, "editor | call Reimport()"))
    has("W7 usedBy names the script that holds the path as a string",
        must("W7 usedBy", query(host, f"files | where path == {q(level)} | select usedBy")), loader)
    fails("W7 DeleteResource refuses a file named by a string", mutate(host, f"editor | call DeleteResource({q(level)})"),
          "still used by", loader)
    main = must("W7 main scene", query(host, "project | select mainScene")).strip().splitlines()[-1]
    if main.startswith("uid://"):  # Godot 4.4+ may store the main scene by uid
        main = must("W7 main scene path", query(host, f"files | where uid == {q(main)} | select path")).strip().splitlines()[-1]
    if main.startswith("res://"):
        has("W7 the main scene is used by project.godot",
            must("W7 main usedBy", query(host, f"files | where path == {q(main)} | select usedBy")), "res://project.godot")
    must("W7 forced delete", mutate(host, f"editor | call DeleteResource({q(level)}, true)"))
    must("W7 delete the loader", mutate(host, f"editor | call DeleteResource({q(loader)})"))


def switches_in_one_call(host: str, project: pathlib.Path, d: str) -> None:
    # F1: Godot ignores OpenSceneFromPath / ReloadSceneFromPath until the next editor frame once a call switched scenes.
    a, b = f"{d}/w7_a.tscn", f"{d}/w7_b.tscn"
    must("W7 scene a", mutate(host, f'editor | call NewScene({q(a)}, "Node3D", "A")'))
    must("W7 scene b", mutate(host, f'editor | call NewScene({q(b)}, "Node3D", "B")'))
    must("W7 two OpenScene calls in one mutation", mutate(host, f"[{q(a)}, {q(b)}] | call editor.OpenScene($)"))
    value_is("W7 the second switch took effect", must("W7 edited", query(host, "editor | select editedScene")), b)
    # b changed on disk while a is edited: ReloadScene(b) reloads the background tab, then switches to it (switching first
    # left the old nodes: the reload was ignored in the frame of the switch).
    must("W7 back to a", mutate(host, f"editor | call OpenScene({q(a)})"))
    path = disk(project, b)
    path.write_text(path.read_text(encoding="utf-8").rstrip("\n") + '\n\n[node name="Added" type="Node" parent="."]\n', encoding="utf-8")
    must("W7 ReloadScene of a background tab", mutate(host, f"editor | call ReloadScene({q(b)})"))
    value_is("W7 b is edited", must("W7 edited after reload", query(host, "editor | select editedScene")), b)
    value_is("W7 b was reloaded from its file", must("W7 Added", query(host, 'root | get children | where name == "Added" | count')), "1")
    # A second reload in the same call cannot happen: refused with the way out instead of leaving the old nodes.
    fails("W7 a second reload in one call is refused", mutate(host, f"[{q(a)}, {q(b)}] | call editor.ReloadScene($)"),
          "own rql-mutate")
    # A second new scene in one call could be saved but not opened: refused before anything is written.
    n1, n2 = f"{d}/w7_n1.tscn", f"{d}/w7_n2.tscn"
    fails("W7 a second NewScene in one call is refused", mutate(host, f"[{q(n1)}, {q(n2)}] | call editor.NewScene($)"), "own rql-mutate")
    check("W7 and its file was not written", not disk(project, n2).exists())


def reimport_new_folder(host: str, project: pathlib.Path, d: str) -> None:
    # F6: a targeted Reimport of a file in a folder the editor never scanned refused it ("run Reimport() without files").
    image = f"{d}/w7_new/deep/dot.png"
    write_png(disk(project, image), 16, 16, {(0, 0)}, 16)
    text = must("W7 Reimport a file in an unscanned folder", mutate(host, f"editor | call Reimport([{q(image)}])"))
    has("W7 the file is queued", text, "Queued 1 file(s)", image)
    lacks("W7 and not refused", text, "NOT imported")
    imported = ""
    for _ in range(25):  # the scan lands, and imports the file, on the next editor frames
        imported = must("W7 imported", query(host, f"files | where path == {q(image)} | select type"))
        if "Texture2D" in imported:
            break
        time.sleep(0.2)
    has("W7 the editor imported it right after the call", imported, "Texture2D")
    check("W7 its .import sidecar exists", disk(project, image + ".import").exists(), file_text(project, image + ".import"))
