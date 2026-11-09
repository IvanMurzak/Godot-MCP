#!/usr/bin/env python3
"""
Live smoke of `savePath` on the screenshot tools against a running Godot editor with this addon. An operator check, not a CI
gate: saving needs a capture, so it needs a windowed editor with a GPU (`--headless` yields an empty image). Only the
refusals, which come before the capture, also run headless (`--refusals-only`).

It does NOT launch anything:

    GODOT_MCP_CONNECTION_MODE=Custom GODOT_MCP_HOST=http://localhost:8080 GODOT_MCP_START_SERVER=true \\
        godot --path <project> --editor &
    python scripts/smoke_screenshot_save.py --host http://localhost:8080 --project <project>

Use a disposable project outside every git repository: it writes res://smoke_shots/ and a scene, a folder beside the project,
and a junction / symlink inside it. Exit code 0 = every case passed. Stdlib only.
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import pathlib
import subprocess
import sys
import time
import urllib.error
import urllib.request

from smoke_rql_lib import FAILURES, check, must, mutate, q

PNG_MAGIC = b"\x89PNG\r\n\x1a\n"
DIR = "smoke_shots"
OUTSIDE_NAME = "smoke_shots_outside"


def call(host: str, name: str, args: dict) -> tuple[bool, str, bytes | None]:
    """(succeeded, the text or error, the image bytes when the result has an image)."""
    req = urllib.request.Request(f"{host}/api/tools/{name}", data=json.dumps(args).encode(),
                                 headers={"Content-Type": "application/json"}, method="POST")
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
        return False, str(body["error"]), None
    content = body.get("content") or []
    image = next((base64.b64decode(c["data"]) for c in content if c.get("type") == "image"), None)
    return True, "\n".join(c.get("text", "") for c in content), image


def snapshot(*roots: pathlib.Path) -> set[pathlib.Path]:
    """Every file below the roots, except Godot's own cache and the editor's scan results for the files we saved."""
    found = set()
    for root in roots:
        for dirpath, dirnames, filenames in os.walk(root):  # does not follow links
            dirnames[:] = [d for d in dirnames if d != ".godot"]
            found.update(pathlib.Path(dirpath, f) for f in filenames)
    return found


def make_link(link: pathlib.Path, target: pathlib.Path) -> None:
    if link.is_symlink() or link.exists():
        return
    if os.name == "nt":
        subprocess.run(["cmd", "/c", "mklink", "/J", str(link), str(target)], check=True, capture_output=True)
    else:
        link.symlink_to(target, target_is_directory=True)


def wait_ready(host: str, timeout: float) -> bool:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            if mutate(host, 'editor | call Reimport(["res://project.godot"])')[0] or call(host, "rql-query", {"query": "project | take 1"})[0]:
                return True
        except (OSError, ValueError):
            pass
        time.sleep(1)
    return False


def refusals(host: str, project: pathlib.Path, tool: str, args: dict, outside: pathlib.Path) -> None:
    """Every bad path: an error that says why, no image, and no new file in or beside the project."""
    root = str(project.resolve())
    (project / DIR).mkdir(exist_ok=True)
    existing = project / DIR / "existing.png"
    if not existing.exists():
        existing.write_bytes(b"not a real png")
    before_bytes = existing.read_bytes()
    make_link(project / DIR / "link_out", outside)

    bad = {
        "../outside.png": "inside the project folder",
        "..\\outside.png": "inside the project folder",
        f"{DIR}/../../outside.png": "inside the project folder",
        "res://../outside.png": "inside the project folder",
        str(project.resolve().parent / "outside.png"): "inside the project folder",
        root + "-sibling/outside.png": "inside the project folder",
        root + "/../outside.png": "inside the project folder",
        root: "inside the project folder",
        "/outside.png": "inside the project folder",
        "\\\\server\\share\\outside.png": "inside the project folder",
        f"{DIR}/link_out/outside.png": "A link on this path leads out",
        "user://outside.png": "user://",
        "file:///outside.png": "only a res:// path",
        "res:///outside.png": "relative to the project root",
        ".": "names a folder",
        "./": "names a folder",
        "..": "names a folder",
        "res://": "names a folder",
        f"{DIR}/": "names a folder",
        f"{DIR}/.": "names a folder",
        f"{DIR}/sub/..": "names a folder",
        ".png": "no file name",
        f"{DIR}/..png": "no file name",
        ".. /outside.png": "ends in a dot or a space",
        f"{DIR}/shot.png.": "ends in a dot or a space",
        "C:outside.png": "character a file name cannot use",
        f"{DIR}/shot.png:stream": "character a file name cannot use",
        f"{DIR}/shot.jpg": "must end in .png",
        f"{DIR}/shot.png.txt": "must end in .png",
        ".godot/shot.png": "cache folder",
        "res://.godot/imported/shot.png": "cache folder",
        "addons/godot_mcp/shot.png": "Godot-MCP addon",
        f"{DIR}/existing.png": "never overwrites",
        f"res://{DIR}/existing.png": "never overwrites",
        f"{DIR}/existing": "never overwrites",
    }
    before = snapshot(project, outside)
    for path, reason in bad.items():
        ok, text, image = call(host, tool, {**args, "savePath": path})
        check(f"{tool} refuses {path!r}", not ok and image is None and text.find(f"Cannot save to") >= 0 and reason in text, text)
    check(f"{tool}: no new file after the refusals", snapshot(project, outside) == before, str(snapshot(project, outside) - before))
    check(f"{tool}: the existing file is untouched", existing.read_bytes() == before_bytes)


def saves(host: str, project: pathlib.Path, tool: str, args: dict, tag: str) -> None:
    """Each accepted form writes the PNG, returns the same image and states the path; no savePath writes nothing."""
    root = project.resolve().as_posix()
    forms = {
        f"{DIR}/{tag}/relative.png": f"{DIR}/{tag}/relative.png",
        f"res://{DIR}/{tag}/res.png": f"{DIR}/{tag}/res.png",
        f"{DIR}\\{tag}\\backslash.PNG": f"{DIR}/{tag}/backslash.PNG",
        f"{DIR}/{tag}/noext": f"{DIR}/{tag}/noext.png",
        f"{DIR}/{tag}//./sub/../collapsed.png": f"{DIR}/{tag}/collapsed.png",
        f"{root}/{DIR}/{tag}/absolute.png": f"{DIR}/{tag}/absolute.png",
    }
    for requested, relative in forms.items():
        target = project / relative
        ok, text, image = call(host, tool, {**args, "savePath": requested})
        check(f"{tool} saves {requested!r}", ok and f"Saved to {root}/{relative}." in text, text)
        check(f"{tool} {requested!r}: the image is still returned", image is not None and image.startswith(PNG_MAGIC))
        check(f"{tool} {requested!r}: the file is that image", target.exists() and target.read_bytes() == image)

    before = snapshot(project)
    ok, text, image = call(host, tool, args)
    check(f"{tool} without savePath: an image", ok and image is not None and image.startswith(PNG_MAGIC), text)
    check(f"{tool} without savePath: no 'Saved to'", "Saved to" not in text and "import" not in text, text)
    check(f"{tool} without savePath: no file", {p for p in snapshot(project) - before if p.suffix != ".import"} == set())

    # A second save refuses and keeps the first file.
    first = (project / f"{DIR}/{tag}/relative.png").read_bytes()
    ok, text, image = call(host, tool, {**args, "savePath": f"{DIR}/{tag}/relative.png"})
    check(f"{tool}: saving twice is refused", not ok and "never overwrites" in text and image is None, text)
    check(f"{tool}: the first file stays", (project / f"{DIR}/{tag}/relative.png").read_bytes() == first)


def imports(project: pathlib.Path, tag: str, timeout: float = 30) -> None:
    """The editor's own scan imports what was saved; the tool never waits for it."""
    sidecar = project / DIR / tag / "relative.png.import"
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline and not sidecar.exists():
        time.sleep(0.5)
    check(f"{tag}: the editor imported the PNG (.import beside it)", sidecar.exists())


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--host", required=True, help="MCP server base URL")
    parser.add_argument("--project", required=True, type=pathlib.Path, help="the Godot project the editor has open")
    parser.add_argument("--wait", type=float, default=0, help="seconds to wait for the editor to answer")
    parser.add_argument("--refusals-only", action="store_true", help="only the refusals (no GPU needed)")
    args = parser.parse_args()
    host = args.host.rstrip("/")
    project = args.project.resolve()
    outside = project.parent / OUTSIDE_NAME
    outside.mkdir(exist_ok=True)

    if args.wait > 0 and not wait_ready(host, args.wait):
        print(f"the editor never answered on {host} within {args.wait:.0f}s", flush=True)
        return 1

    scene = f"res://{DIR}/scene.tscn"
    must("scene", mutate(host, f'editor | call NewScene({q(scene)}, "Node3D", "Root")'))
    must("camera", mutate(host, 'root | call AddChild("Camera3D", "SmokeCam")'))
    must("target", mutate(host, 'root | call AddChild("CSGBox3D", "SmokeBox")'))
    must("camera position", mutate(host, 'nodes | where name == "SmokeCam" | set Position.Z = 4'))
    must("save scene", mutate(host, "editor | call SaveScene()"))

    tools = [
        ("screenshot-viewport", {"mode": "3d"}, "viewport"),
        ("screenshot-camera", {"nodeRef": {"path": "SmokeCam"}, "width": 320, "height": 180}, "camera"),
        ("screenshot-isolated", {"nodeRef": {"path": "SmokeBox"}, "resolution": 128}, "isolated"),
    ]
    for tool, tool_args, _ in tools:
        refusals(host, project, tool, tool_args, outside)
    if not args.refusals_only:
        for tool, tool_args, tag in tools:
            saves(host, project, tool, tool_args, tag)
        for _, _, tag in tools:
            imports(project, tag)
        must("the editor still answers", call(host, "rql-query", {"query": "project | take 1"})[:2])

    print(f"{len(FAILURES)} failure(s)" + (": " + "; ".join(FAILURES) if FAILURES else ""), flush=True)
    return 1 if FAILURES else 0


if __name__ == "__main__":
    sys.exit(main())
