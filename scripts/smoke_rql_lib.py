#!/usr/bin/env python3
"""Shared helpers of the live RQL smoke (scripts/smoke_rql.py, scripts/smoke_rql_models.py). Pure stdlib, Python 3.8+."""
from __future__ import annotations

import json
import pathlib
import re
import urllib.error
import urllib.request

FAILURES: list[str] = []
ROOT = pathlib.Path(__file__).resolve().parent.parent


def tool(host: str, name: str, args: dict) -> tuple[bool, str]:
    """Call a tool over the server's direct HTTP API; (succeeded, text of the result or the error)."""
    req = urllib.request.Request(
        f"{host}/api/tools/{name}",
        data=json.dumps(args).encode(),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            body = json.loads(resp.read().decode() or "{}")
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            body = json.loads(raw or "{}")
        except json.JSONDecodeError:
            body = {"error": raw or str(e)}
    if "error" in body:
        return False, str(body["error"])
    if "structured" in body:
        return True, json.dumps(body["structured"])
    return True, "\n".join(c.get("text", "") for c in body.get("content") or [])


def query(host: str, q: str) -> tuple[bool, str]:
    return tool(host, "rql-query", {"query": q})


def mutate(host: str, q: str, dry_run: bool = False) -> tuple[bool, str]:
    return tool(host, "rql-mutate", {"query": q, "dry_run": dry_run})


def check(name: str, ok: bool, detail: str = "") -> None:
    print(f"[{'PASS' if ok else 'FAIL'}] {name}" + (f" -- {detail[:800]}" if detail and not ok else ""), flush=True)
    if not ok:
        FAILURES.append(name)


def must(name: str, result: tuple[bool, str]) -> str:
    ok, text = result
    check(name, ok, text)
    return text


def disk(project: pathlib.Path, res_path: str) -> pathlib.Path:
    return project / res_path.replace("res://", "")


def file_has(project: pathlib.Path, res_path: str, pattern: str) -> bool:
    path = disk(project, res_path)
    return path.exists() and re.search(pattern, path.read_text(encoding="utf-8"), re.MULTILINE) is not None


def file_text(project: pathlib.Path, res_path: str) -> str:
    path = disk(project, res_path)
    return path.read_text(encoding="utf-8") if path.exists() else "<missing>"


def node_line(name: str, parent: str, type_: str | None = None, instance: bool = False) -> str:
    """Regex of a `[node ...]` header; tolerant of attribute order and of Godot 4.6+'s `unique_id=`."""
    # The header may hold lists with "]" (groups=["enemies"]): match to the end of the line.
    head = rf'^\[node name="{re.escape(name)}"(?=[^\n]*\bparent="{re.escape(parent)}")'
    if type_ is not None:
        head += rf'(?=[^\n]*\btype="{re.escape(type_)}")'
    if instance:
        head += r'(?=[^\n]*\binstance=ExtResource\("[^"]+"\))'
    return head + r'[^\n]*\]$'


def q(text: str) -> str:
    """An RQL string literal."""
    return json.dumps(text)
