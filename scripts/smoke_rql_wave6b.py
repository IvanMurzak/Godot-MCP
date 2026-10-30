#!/usr/bin/env python3
"""
Live cases of the RQL 2f4e263 adoption (literal kinds, typed arrays of resources, vector functions, key warnings, receipt notes, misspelt parameter
names), run by scripts/smoke_rql.py against a running editor: a typed Resource literal builds a value, a Node literal is
refused with Godot's advice (godot.find / editor.CreateNode), distance() / normalize() / [x, 0, z] * k work in a mutation,
a failing sort key and a sparse group-by key warn, a long receipt never points at a json format the tools do not have, and
a misspelt argument name is suggested. Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import pathlib

from smoke_rql_lib import disk, must, mutate, q, query, tool
from smoke_rql_models import fails, has, lacks, value_is


def run(host: str, project: pathlib.Path, d: str) -> None:
    must("W6B scene", mutate(host, f'editor | call NewScene({q(d + "/w6b.tscn")}, "Node3D", "Root")'))
    literal_kinds(host)
    vectors(host)
    key_warnings(host)
    receipts_and_names(host)
    typed_arrays(host, project, d)


def literal_kinds(host: str) -> None:
    must("W6B mesh node", mutate(host, 'root | call AddChild("MeshInstance3D", "M")'))
    must("W6B typed Resource literal", mutate(host, 'godot.find("M") | set Mesh = {$type: "BoxMesh", Size: [1, 2, 1]}'))
    value_is("W6B the literal built a BoxMesh", must("W6B read mesh", query(host, 'godot.find("M") | select Mesh.class')), "BoxMesh")
    value_is("W6B with its members", must("W6B read size", query(host, 'godot.find("M") | select Mesh.Size')), "(1, 2, 1)")
    # Nodes stay engine objects: RQL refuses the literal itself and ends with the addon's advice.
    fails("W6B a Node literal is refused with Godot's advice",
          mutate(host, 'godot.find("M") | call Reparent({$type: "Node3D"})', dry_run=True),
          "engine object", "godot.find(", "editor.CreateNode")


def vectors(host: str) -> None:
    must("W6B vector node", mutate(host, 'root | call AddChild("Node3D", "V")'))
    must("W6B place", mutate(host, 'godot.find("V") | set Position = [3, 0, 4]'))
    must("W6B normalize * distance", mutate(host, 'godot.find("V") | set Position = normalize(Position) * distance(Position, [0, 0, 0]) * 2'))
    value_is("W6B normalize(v) * distance(v, 0) * 2 doubles it", must("W6B read", query(host, 'godot.find("V") | select Position')), "(6, 0, 8)")
    must("W6B list arithmetic", mutate(host, 'godot.find("V") | set Position = [Position.X, 0, Position.Z] * 0.5'))
    value_is("W6B [x, 0, z] * k", must("W6B read 2", query(host, 'godot.find("V") | select Position')), "(3, 0, 4)")
    value_is("W6B length() of a vector", must("W6B length", query(host, 'godot.find("V") | select l = Position.length()')), "5")


def key_warnings(host: str) -> None:
    has("W6B a failing sort key warns", must("W6B sort", query(host, 'root | recurse children | sort (Position.X / 0) | select name')),
        "sort key", "failed for")
    has("W6B an all-missing group-by key offers $type", query(host, "root | recurse children | group-by type")[1], "$type")


def receipts_and_names(host: str) -> None:
    planned = must("W6B 60 nodes, dry run", mutate(host, 'range(0, 60) | call editor.CreateNode("Node3D", {name: "D" + $})', dry_run=True))
    has("W6B a long dry-run receipt says the targets were checked", planned, "10 more row(s) not shown", "every target was checked")
    lacks("W6B and never that changes were made", planned, "every change was made")
    receipt = must("W6B 60 nodes", mutate(host, 'range(0, 60) | call editor.CreateNode("Node3D", {name: "R" + $})'))
    has("W6B a long receipt counts the rest", receipt, "10 more row(s) not shown")
    lacks("W6B and names no json format", receipt, "json format")
    has("W6B and says every change was made", receipt, "every change was made")
    fails("W6B a misspelt argument name is suggested", mutate(host, 'editor | call CreateNode({klass: "Node3D"})', dry_run=True),
          "did you mean 'className'")


def typed_arrays(host: str, project: pathlib.Path, d: str) -> None:
    # Wave 7: an Array[Resource] property ("24/17:Resource") took no object literals or refs, and copying one typed array
    # into another must keep working.
    script = f"{d}/w6b_holder.gd"
    disk(project, script).write_text("extends Node3D\n@export var items: Array[Resource] = []\n@export var others: Array[Resource] = []\n",
                                     encoding="utf-8")
    must("W6Y register", mutate(host, "editor | call Reimport()"))
    must("W6Y holder", mutate(host, 'root | call AddChild("Node3D", "H")'))
    must("W6Y attach", tool(host, "script-attach-to-node", {"nodeRef": "H", "scriptPath": script}))
    must("W6Y typed array of literals", mutate(host, 'godot.find("H") | set props["items"] = [{$type: "BoxMesh"}, {$type: "SphereMesh"}]'))
    must("W6Y append a literal", mutate(host, 'godot.find("H") | set props["items"] = append(props["items"], {$type: "CylinderMesh"})'))
    value_is("W6Y three typed elements", must("W6Y read", query(host, 'godot.find("H") | select n = len(props["items"])')), "3")
    must("W6Y copy into another typed array", mutate(host, 'godot.find("H") | set props["others"] = props["items"]'))
    has("W6Y the copy holds the same meshes", must("W6Y read copy", query(host, 'godot.find("H") | get props["others"] | select class')),
        "CylinderMesh")
