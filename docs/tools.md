**English** · [中文](tools.zh-CN.md) · [日本語](tools.ja.md) · [Español](tools.es.md) · [فارسی](tools.fa.md) · [हिन्दी](tools.hi.md) · [العربية](tools.ar.md)

# Tools reference

Godot-MCP ships **24 built-in tools** in **9 families**. The editor's data layer is [RQL](rql.md) (Reflection Query Language): `rql-query`, `rql-mutate` and `rql-schema` read and change scene nodes, Godot properties, resources, files, scenes and editor state in one query instead of one tool per operation.

With the default `toolFormat` (`Rql`) the other tools take RQL values as arguments and return compact text; `Json` keeps ReflectorNet's JSON schemas and results. Screenshots return a PNG image. Every editor tool is available as soon as the addon is enabled.

The **console**, **reflection** and **runtime-errors** families serve a *running game* (see [Runtime usage](runtime.md)). In the editor, use the RQL `console` source and RQL `call` / `CallMethod` instead.

| Family | Tools | What it does |
| --- | --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` | Query and change the live editor. All-or-nothing and undoable. See [rql.md](rql.md). |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` | C# (`.cs`) and GDScript (`.gd`) files, attaching a script to a node, GDScript validation, building the project's C#, and running C# in the editor. |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | Capture the editor viewport, a camera, or one node rendered alone, as a PNG the LLM can see. |
| **console** | `console-get-logs`, `console-clear-logs` | In a running game: read and clear the collected logs. |
| **reflection** | `reflection-method-find`, `reflection-method-call` | In a running game: find and call C# methods in any loaded assembly via ReflectorNet. |
| **runtime-errors** | `runtime-errors-get`, `runtime-errors-clear` | In a running game: poll GDScript runtime errors, `push_error` / `push_warning`, shader errors and C# exceptions. **Off by default.** |
| **ping** | `ping` | Readiness probe. System tool. |
| **session** | `session-close` | Save and quit an editor that `godot-cli` started. System tool. |
| **skills** | `godot-skill-create`, `godot-skill-generate` | Write a new C# tool file into the project; regenerate the `SKILL.md` files. System tools. |

**System tools** are served on the MCP server's `/api/system-tools/` HTTP endpoint (for example `godot-cli run-system-tool ping`). They are not listed to AI agents in `tools/list`.

## rql

- `rql-query`: a read-only query, returned as compact rows. `nodes | where class == "OmniLight3D" | select name props["light_energy"]`
- `rql-mutate`: a query that ends in `set` or `call`. All-or-nothing, undoable (Ctrl+Z), and `dry_run` previews it. Scene edits mark the scene unsaved; edited resource files and `project.godot` are saved.
  - `nodes | where name starts-with "Enemy" | set Visible = false`
  - `root | call AddChild("Node3D", "Props")`
  - `nodes | where name == "Button" | call Connect("pressed", godot.find("UI"), "_on_pressed")`
  - `project | call AddInputAction("jump", ["Key:Space"])`
  - `editor | call SaveScene()`
- `rql-schema`: the sources, functions and node classes in the edited scene; with a type name, its members, methods and Godot properties. `rql-schema "<extension>"` prints an installed extension's guide.

Sources include `nodes`, `root`, `selection`, `scenes`, `files`, `folders`, `classes`, `console`, `editor`, `project`, `settings`, `inputActions`, `autoloads`, `groups`, `layers` and `tools`. The full language, including TileSets, Themes, ShaderMaterial parameters and MeshLibraries, is in [rql.md](rql.md) and the `godot-rql` skill.

## script

- `script-read`, `script-create`, `script-update`, `script-delete`: CRUD on `.cs` and `.gd` files.
- `script-attach-to-node`: attach a script to a node.
- `script-validate`: parse and compile GDScript files and return structured diagnostics.
- `script-build`: build the project's C# (`dotnet build`, Debug) so new scripts load. The build runs in the background; when it succeeds Godot reloads the C# assembly, the plugin reconnects, and the result (warnings or errors) arrives after the reload. See [C# assembly reloads](custom-tools.md#c-assembly-reloads).
- `script-execute`: compile and run C# in the editor with Roslyn, via [Code-Exec](https://github.com/IvanMurzak/Code-Exec). Plain statements with the last expression as the result (own `using`s, local classes, `await`), or a class with a static method. The game's compiled types are visible; `include` adds helper `.cs` files from `<project>/.ai-game-dev/snippets`. Returns the value and what the code printed (`Console`; `GD.Print`, push_warning and push_error when the project builds with Godot.NET.Sdk 4.5+). From a terminal: `godot-cli exec '1 + 2'`.

## screenshot

- `screenshot-viewport`: the editor viewport.
- `screenshot-camera`: the view from a specific camera.
- `screenshot-isolated`: one node rendered alone, from a chosen angle.

All three return the image in the conversation. Pass `savePath` to also write a file: `res://Screenshots/shot.png`, `Screenshots/shot.png` (relative to the project root) or an absolute path inside the project. Missing folders are created and `.png` is added when there is no extension. A `savePath` is refused (no image, no file) when it is outside the project (`..`, `user://`), goes through a symlink or junction, points inside `res://.godot/` or this addon, is not a `.png`, or names an existing file: it never overwrites. Godot imports the PNG like any project file; a folder with a `.gdignore` file avoids that.

## console, reflection, runtime-errors (running game)

- `console-get-logs` / `console-clear-logs`: read (with filtering) and clear the logs the plugin collected, including its own connection diagnostics.
- `reflection-method-find` / `reflection-method-call`: find C# methods (private ones too) across every loaded assembly, and call one with arguments.
- `runtime-errors-get`: captured in-game errors, oldest first, newest kept. Pass the previous `highestSequence` as `sinceSequence` to get only new errors. Returns `available:false` when capture was never enabled.
- `runtime-errors-clear`: clear the buffer; the sequence counter keeps counting.

Runtime-error capture is off until a game calls `builder.WithRuntimeErrorCapture()`. Details: [Capturing in-game runtime errors](runtime.md#capturing-in-game-runtime-errors).

## ping, session, skills (system tools)

- `ping`: echoes a message back, or returns `pong`. `curl -X POST <host>/api/system-tools/ping -d '{}'`.
- `session-close`: save and quit an editor started by `godot-cli exec` / `open --headless` (open scenes with a file and the resources they changed). It needs that session's nonce from `.ai-game-dev/session.json`, so it never closes your own editor. `godot-cli close --owned-only` calls it.
- `godot-skill-create`: write a new C# tool file into the project. It becomes callable after the project is rebuilt.
- `godot-skill-generate`: regenerate every `SKILL.md` from the tools the editor has registered, into the selected AI agent's skills folder.

## Visibility

Each tool, prompt and resource has a **Visible** switch in the dock's Tools / Prompts / Resources windows (`godot-cli configure` does the same from a terminal). It only decides whether MCP clients see the item in `tools/list`, which keeps their context small. It is not access control: a hidden tool still runs when a client calls it by name.

## Extensions

Extensions add RQL sources, adapters and an agent guide to the `rql-*` tools and the `godot-rql` skill. They add no MCP tools of their own. The list is in the [README](../README.md#extensions).

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
godot-cli build ./MyGodotProject
```

The installer adds only the extension's NuGet package; install a required third-party addon into the project yourself. The dock's **Extensions** section has **Install All**. `godot-cli update` updates the addon and re-resolves floating extensions (`Version="*"`) to their latest versions; an extension you pinned to a version is kept. If an extension needs a newer McpPlugin than the addon pins, the restore fails with `NU1605` until you update the addon or pin the extension.
