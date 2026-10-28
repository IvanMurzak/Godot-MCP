#!/usr/bin/env python3
"""
Live cases of the RQL 0.3 features the editor adopts, run by scripts/smoke_rql.py against a running editor: bulk create
(range(0, 300) | call editor.CreateNode(...) and a list literal | call editor.InstantiateScene(...): every item checked before
the first node is made, one undo step, a receipt row with each node's $ref), component-wise vector arithmetic
(set Scale = Scale * 1.5), absent property-map keys reading null, and distinct merging scales that print alike.
Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import pathlib
import re

from smoke_rql_lib import check, file_has, must, mutate, q, query, tool
from smoke_rql_models import fails, has, value_is

GRID = 300


def run(host: str, project: pathlib.Path, d: str) -> None:
    coin = f"{d}/bulk_coin.tscn"
    must("BC coin scene", mutate(host, f'editor | call NewScene({q(coin)}, "Node3D", "Coin")'))
    must("BC bulk scene", mutate(host, f'editor | call NewScene({q(d + "/bulk.tscn")}, "Node3D", "Bulk")'))
    bulk_create(host)
    validated_first(host)
    instantiate(host, coin)
    arithmetic(host)
    absent_keys(host)
    distinct_scales(host)
    must("BC save", mutate(host, "editor | call SaveScene()"))
    scene = f"{d}/bulk.tscn"
    check("BC the saved scene has the cubes and their mesh", file_has(project, scene, 'name="Cube299" type="MeshInstance3D" parent="."')
          and file_has(project, scene, 'type="BoxMesh"'))


def count(host: str, where: str) -> int:
    text = must(f"BC count {where}", query(host, f"nodes | where {where} | count"))
    found = re.search(r"\d+", text)
    return int(found.group(0)) if found else -1


def mutate_max(host: str, text: str, max_targets: int) -> tuple[bool, str]:
    return tool(host, "rql-mutate", {"query": text, "max_targets": max_targets})


def bulk_create(host: str) -> None:
    """300 cubes on a 15-wide grid in one call: one receipt row per node with its $ref, one undo step."""
    receipt = must("BC create 300 cubes", mutate_max(host,
        f'range(0, {GRID}) | call editor.CreateNode("MeshInstance3D", {{name: "Cube" + $, '
        'position: [($ % 15) * 2, 0, floor($ / 15) * 2], mesh: {class: "BoxMesh"}})', GRID))
    check("BC receipt has a $ref row per node", len(re.findall(r"MeshInstance3D#\d+", receipt)) >= GRID, receipt[:400])
    check("BC 300 cubes in the scene", count(host, 'name starts-with "Cube"') == GRID)
    value_is("BC the last cube sits on the grid", must("BC read Cube299",
        query(host, 'nodes | where name == "Cube299" | select Position')), "(28, 0, 38)")
    has("BC the cubes got their mesh", must("BC read a mesh", query(host, 'nodes | where name == "Cube7" | select Mesh')), "BoxMesh")
    must("BC one undo", mutate(host, "editor | call Undo()"))
    check("BC one undo removes all 300", count(host, 'name starts-with "Cube"') == 0)
    must("BC redo", mutate(host, "editor | call Redo()"))
    check("BC redo brings all 300 back", count(host, 'name starts-with "Cube"') == GRID)


def validated_first(host: str) -> None:
    """A bad item fails the call before any node is made."""
    fails("BC unknown property refused", mutate(host, 'range(0, 3) | call editor.CreateNode("Node3D", {name: "Bad" + $, nosuch: 1})'),
          "no property 'nosuch'", "nothing was called")
    fails("BC a value of the wrong type refused", mutate(host, 'range(0, 3) | call editor.CreateNode("Node3D", {name: "Bad" + $, position: "up"})'),
          "position")
    fails("BC unknown class refused", mutate(host, 'range(0, 3) | call editor.CreateNode("NoSuchNode3D", {name: "Bad" + $})'), "unknown node class")
    fails("BC duplicate names refused", mutate(host, 'range(0, 2) | call editor.CreateNode("Node3D", {name: "Bad"})'), "several items")
    fails("BC a global transform refused", mutate(host, 'range(0, 2) | call editor.CreateNode("Node3D", {name: "Bad" + $, global_position: [1, 0, 0]})'),
          "global_position", "position")
    fails("BC a taken name refused", mutate(host, 'range(0, 1) | call editor.CreateNode("Node3D", {name: "Cube0"})'), "already has a child named")
    check("BC nothing was created", count(host, 'name starts-with "Bad"') == 0)
    dry = must("BC dry run", tool(host, "rql-mutate", {"query": 'range(0, 2) | call editor.CreateNode("Node3D", {name: "Dry" + $})', "dry_run": True}))
    has("BC dry run receipt", dry, "(dry-run)")
    check("BC dry run created nothing", count(host, 'name starts-with "Dry"') == 0)


def instantiate(host: str, coin: str) -> None:
    """One scene instance per list entry, under a parent given by name."""
    must("BC a parent", mutate(host, 'root | call AddChild("Node3D", "Coins")'))
    must("BC instance 2 coins", mutate(host,
        f'[["CoinA", 0], ["CoinB", 3]] | call editor.InstantiateScene({q(coin)}, {{name: $[0], parent: "Coins", position: [$[1], 1, 0]}})'))
    text = must("BC read coins", query(host, 'nodes | where parent.name == "Coins" | sort name | select name Position SceneFilePath'))
    has("BC coins are instances under Coins", text, "CoinA", "CoinB", "(3, 1, 0)", coin)


def arithmetic(host: str) -> None:
    """Vectors compute component-wise; a quaternion does not."""
    must("BC Scale * 1.5", mutate_max(host, 'nodes | where name starts-with "Cube" | set Scale = Scale * 1.5', GRID))
    value_is("BC scaled", must("BC read scale", query(host, 'nodes | where name == "Cube0" | select Scale')), "(1.5, 1.5, 1.5)")
    must("BC Position + list", mutate(host, 'nodes | where name == "Cube0" | set Position = Position + [0, 1, 0]'))
    value_is("BC moved up", must("BC read position", query(host, 'nodes | where name == "Cube0" | select Position')), "(0, 1, 0)")
    fails("BC quaternion arithmetic refused", mutate(host, 'nodes | where name == "Cube0" | set Quaternion = Quaternion * 2'),
          "no component-wise arithmetic")


def absent_keys(host: str) -> None:
    """props["k"] of a property the node does not have reads null, so == null keeps the node."""
    total = count(host, "$ is Node")
    check("BC props[missing] == null keeps every node", total > 0 and count(host, 'props["no_such_property"] == null') == total)
    check("BC has() still tests presence", count(host, 'has(props["no_such_property"])') == 0)


def distinct_scales(host: str) -> None:
    """Scales that print alike are one distinct value."""
    must("BC set near-equal scales", mutate(host, 'nodes | where name == "Cube1" | set Scale = [1, 1, 1]'))
    must("BC set near-equal scales 2", mutate(host, 'nodes | where name == "Cube2" | set Scale = [0.99999994, 1, 1]'))
    text = must("BC distinct", query(host, 'nodes | where name in ["Cube1", "Cube2"] | select Scale | distinct'))
    check("BC distinct merges them", text.count("(1, 1, 1)") == 1, text)
