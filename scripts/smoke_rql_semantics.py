#!/usr/bin/env python3
"""
Live cases of RQL language and host behaviour the Godot editor relies on, run by scripts/smoke_rql.py against a running
editor: conditions must be booleans (a bare object is a type-mismatch with a hint), Godot class names in $type / $ref / `is`
(GPUParticles3D, CSGBox3D, not the C# GpuParticles3D / CsgBox3D), hints that name rql-mutate, group items
(group-by … | select key, items.avg(…)), lists compared by value, the max_targets range, and receipts that list only real
changes (the rest counted as unchanged).

Pure stdlib, Python 3.8+.
"""
from __future__ import annotations

import pathlib
import re

from smoke_rql_lib import check, must, mutate, query, tool
from smoke_rql_models import fails, has, lacks, new_scene, node


def run(host: str, project: pathlib.Path, d: str) -> None:
    new_scene(host, f"{d}/sem.tscn", "Node3D", "SemRoot", [
        ("GPUParticles3D", "Sparks"), ("CSGBox3D", "Crate"), ("MeshInstance3D", "Box"),
        ("OmniLight3D", "Lamp1"), ("OmniLight3D", "Lamp2"),
    ])
    boolean_conditions(host)
    class_names(host)
    mutate_hints(host)
    group_items(host)
    list_equality(host)
    target_range(host)
    unchanged_receipts(host)


def boolean_conditions(host: str) -> None:
    """A bare object as a condition is a type-mismatch whose hint shows the test to write; null still counts as false."""
    must("SB mesh", mutate(host, f'{node("Box")} | set Mesh = {{$type: "BoxMesh"}}'))
    fails("SB where mesh is a type-mismatch", query(host, "nodes | where mesh | select name"),
          "type-mismatch", "not a boolean", "mesh != null", "has(mesh)")
    has("SB where Mesh != null", must("SB explicit test", query(host, "nodes | where Mesh != null | select name")), "Box")
    text = must("SB null is false", query(host, "nodes | where name == \"SemRoot\" | where parent | count"))
    check("SB a null condition keeps nothing", re.search(r"^0$", text, re.M) is not None, text)


def class_names(host: str) -> None:
    """$type, $ref and `is` speak Godot's class names; the C# spelling still matches."""
    text = must("SC class filter", query(host, 'nodes | where class == "GPUParticles3D" | select name $type'))
    has("SC GPUParticles3D found, $type is the Godot name", text, "Sparks|GPUParticles3D")
    lacks("SC no C# name", text, "GpuParticles3D")
    for spelling in ("GPUParticles3D", "GpuParticles3D"):
        text = must(f"SC is {spelling}", query(host, f"nodes | where $ is {spelling} | count"))
        check(f"SC $ is {spelling} matches", re.search(r"^1$", text, re.M) is not None, text)
    has("SC $ref of a CSG node", must("SC ref", query(host, f'{node("Crate")} | select $ref')), "CSGBox3D#")
    text = must("SC $type comparison", query(host, 'nodes | where $type == "CSGBox3D" | count'))
    check("SC $type == CSGBox3D", re.search(r"^1$", text, re.M) is not None, text)


def mutate_hints(host: str) -> None:
    """A set in rql-query names rql-mutate; a member only some targets have names the class to filter to."""
    fails("SH set in rql-query names rql-mutate", query(host, f'{node("Lamp1")} | set Visible = false'),
          "permission-denied", "rql-mutate")
    fails("SH a member only OmniLight3D has", mutate(host, "nodes | set LightEnergy = 2"), "OmniLight3D", "$ is OmniLight3D")


def group_items(host: str) -> None:
    """group-by rows carry their items: aggregates over each group."""
    must("SG lamp heights", mutate(host, 'nodes | where class == "OmniLight3D" | sort name | set Position.Y = $index * 2 + 1'))
    text = must("SG group-by items.avg",
                query(host, 'nodes | where $ is Light3D | group-by class | select key, count, avgY = items.avg(Position.Y)'))
    has("SG two lamps at y 1 and 3 average 2", text, "OmniLight3D|2|2")


def list_equality(host: str) -> None:
    """Lists compare by value."""
    must("SL group", mutate(host, f'{node("Lamp1")} | call AddToGroup("lamps")'))
    text = must("SL groups == [\"lamps\"]", query(host, 'nodes | where groups == ["lamps"] | select name'))
    has("SL list equality by value", text, "Lamp1")
    lacks("SL only the grouped lamp", text, "Lamp2")


def target_range(host: str) -> None:
    """max_targets outside 1 … 1000 is refused before anything runs; omitted, the default applies."""
    for value in (0, 1001):
        fails(f"SR max_targets = {value} refused",
              tool(host, "rql-mutate", {"query": f'{node("Lamp2")} | set Visible = false', "max_targets": value}),
              "invalid-value", "from 1 to 1000")
    text = must("SR still visible", query(host, f'{node("Lamp2")} | select Visible'))
    has("SR nothing ran", text, "true")


def unchanged_receipts(host: str) -> None:
    """A set where most targets already hold the value lists only the real change and counts the rest."""
    must("SU hide one", mutate(host, f'{node("Lamp2")} | set Visible = false'))
    text = must("SU show all", mutate(host, "nodes | set Visible = true"))
    has("SU unchanged counted", text, "5 unchanged assignment(s) not listed")
    has("SU the real change listed", text, "Visible")
    check("SU one receipt row", text.count("|Visible|") == 1, text)
