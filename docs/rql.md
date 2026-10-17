# RQL in Godot-MCP

The data layer of the editor tools is RQL (Reflection Query Language, the NuGet package
[`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql)): one query language instead of one tool
per operation, the same one Unity-MCP 1.0 uses. McpPlugin and its tool plumbing are unchanged; ReflectorNet stays the
data layer of the in-game runtime (`GodotMcpRuntime`) and of the editor's `Json` tool format.

RQL is licensed under the [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/): free
to use, including in commercial products, except to provide a product that competes with the licensor's products.
The addon itself stays Apache-2.0.

## Tools

| Before (≤ 0.28) | Now |
|---|---|
| `node-find`, `node-get-data` | `rql-query` on `nodes` / `root` / `selection`: `nodes \| where class == "OmniLight3D" \| select $ref name path props["light_energy"]` |
| `node-modify` | `rql-mutate` `set`: C# members (`Position`, `Visible`, …) and every Godot property in `props["snake_name"]` (GDScript variables and GDExtension properties included) |
| `node-create`, `node-duplicate`, `node-delete`, `node-set-parent`, `node-reorder` | node calls `AddChild(class, name)`, `InstantiateScene(path)`, `Duplicate()`, `Free()`, `Reparent(node)`, `MoveChild(index)` |
| `scene-create`, `scene-open`, `scene-save`, `scene-list-opened`, `scene-get-data` | `editor \| call NewScene / OpenScene / SaveScene / SaveAllScenes`, the `scenes` source (`call Open / Save`), `root \| recurse children` |
| `resource-find`, `resource-get-data`, `resource-modify` | `godot.resources("Texture2D", "res://art")`, `godot.resource("res://…")` + `set` (the file is saved) |
| `resource-create`, `resource-move`, `resource-delete` | `editor \| call CreateResource / MoveResource / DeleteResource` (`.import` / `.uid` sidecars follow the file) |
| `filesystem-list`, `filesystem-reimport` | the `files` source (`path name dir ext type uid`), `editor \| call Reimport(files?)`, `CreateFolder` |
| `editor-application-get-state` / `-set-state` | `editor` members `isPlaying` (writable), `playingScene`, `editedScene`, `godotVersion`; `call PlayScene / StopScene` |
| `editor-selection-get` / `-set` | `editor` member `selection` (writable), the `selection` source |
| `console-get-logs`, `console-clear-logs` (editor) | the `console` source (`where LogType == "Error"`, `Sequence > N`), `editor \| call ClearConsole()` |
| `reflection-method-find`, `reflection-method-call` (editor) | `call CallMethod("name", [args])` on any Godot object, node / resource calls, `rql-schema` |
| — | `rql-schema`: sources, functions, the node classes in the edited scene; one type's members, methods and Godot properties (ClassDB) |

Kept as tools: `script-*`, `screenshot-*`, `ping`, `godot-skill-*`, `runtime-errors-*`. The **in-game runtime** tool
surface is unchanged: `console-*`, `reflection-*`, `runtime-errors-*` and `ping` stay registered in a game build (the
editor removes `console-*` / `reflection-*` from the plugin it builds — `RqlInstaller.EditorReplacedTools`).

With the default `toolFormat` `Rql` (config key `toolFormat`, env `GODOT_MCP_TOOL_FORMAT`), the other tools take RQL
values as arguments (`"Level/Player"`, `ref("Node3D#12")`, `godot.find("Name")` for a `NodeRef`) and return compact
text; `Json` keeps ReflectorNet's JSON schemas and structured results. `toolTextEscaping` (`Minimal` / `Ascii` /
`Json`) controls how quoted text in those results is escaped.

## Writes: undo and saving

- **All-or-nothing.** Every target and value is checked before anything is written; a failure changes nothing.
  `dry_run: true` returns the receipt (`$ref member old new`) without changing anything.
- **Undo.** One `EditorUndoRedoManager` action per `rql-mutate` (Ctrl+Z): a `set` snapshots the stored Godot properties
  of every changed object and records the differences as do/undo properties; the structural node calls register the
  same do/undo methods the Scene dock does (the removed node of `Free()` stays alive in the undo history). An action
  that touches scene nodes and global objects (file resources) is split in two, one per undo history.
- **Saving** follows the replaced tools: a change to a node of the edited scene marks the scene unsaved — write it
  with `editor | call SaveScene()` (like Ctrl+S; `node-modify` did not save either); a change to a resource stored in
  its own file (`res://….tres`) is saved to that file after the mutation, like `resource-modify` (only when a property
  actually changed). Built-in (scene-embedded) resources mark the scene unsaved. Problems saving become `not-saved`
  warnings on the receipt.

## Code

- `addons/godot_mcp/Editor/Rql/` (namespace `com.IvanMurzak.Godot.MCP.Editor.Rql`):
  - `RqlGodot*.cs` — the engine over the live editor: factory (`AccessorMode.Compiled`, `$ref` = `Class#instance id`
    via `GodotObject.InstanceFromId`, liveness via `IsInstanceValid`), sources and functions, mutation hooks, node
    operations. One engine per editor session; `Reset()` on plugin teardown.
  - `EditorApi/` — the `editor` source (state, scenes, resource files, files, console, classes) and its view types.
  - `Adapters/` — one adapter per file: GodotObject (`id`, `class`, `props`, `CallMethod`), Node, Node2D, Node3D,
    Control, Resource; StringName / NodePath / Variant; Vector2/3/4(I), Color, Quaternion, Basis, Transform2D/3D,
    Rect2(I), Aabb (pure C#, unit-tested); NodeRef (tool arguments).
  - `Modules/GodotCoreModule.cs`, `Modules/ToolTypesModule.cs` — the core module; modules are discovered in every loaded
    assembly (`GodotAssemblyUtils.AllAssemblies`), so an extension's `IRqlModule` in the consumer project assembly is
    found as soon as it is installed (its `ctx.AddGuide` text appears in the `godot-rql` skill).
  - `RqlGodotSchema.cs`, `GodotTypes.cs`, `RqlGuide.cs` (agent texts; every example runs in CI), `Skill_Rql.cs`
    (`godot-rql`), `ToolFormat/` (`RqlToolBinding`, McpPlugin `IToolBinding`), `RqlInstaller.cs`, `RqlReimport.cs`.
- `addons/godot_mcp/Editor/Tools/Tool_Rql*.cs` — the three tools.
- Installation: `GodotMcpPlugin` calls `RqlInstaller.Install` before the connection starts; the connection (Runtime/)
  invokes the editor-assigned `GodotMcpConnection.PluginBuilt` hook after building each plugin.

## Hot reload

The RQL assemblies are NuGet dependencies, which the addon's resolver loads into the default (non-collectible) load
context. RQL keeps process-wide caches keyed by `Type` / `MemberInfo`; once a query touched a type of the collectible
project assembly they would keep it alive and a C# rebuild could not unload it. Teardown therefore drops the engine,
the tool signatures and the type index, and `RqlStaticCaches.Clear()` empties those caches by reflection (RQL 0.1 has
no public API for it; the xUnit suite fails if the fields it clears disappear). Module and type discovery skip the
previous copy of the project assembly that the process may still list during a reload. The dock-reload CI legs prove
the reload still unloads.

## Tests

- `Godot-MCP.Tests/Rql*Tests.cs` (xUnit, no Godot binary): every guide example parses, the struct adapters, the tool
  binding (schemas, RQL arguments, compact results, escaping), type names, the cache release, the config setting.
- `scripts/smoke_rql.py` (CI: `test_godot_runtime_harness.yml`, every Godot version of the matrix) against a live
  headless editor: read-back of every change from the `.tscn` / `.tres` Godot wrote (the former
  `smoke_property_patches.py` cases), `AddChild` + `SaveScene` round-trip, resource move / delete with sidecars, and
  every example of the guide (`Godot-Tests/rql-guide-examples.json`) in order.
- The extension suite (`test_extensions.yml`) runs its editor with `GODOT_MCP_TOOL_FORMAT=Json` until the extensions
  migrate to RQL modules; its scene setup uses `rql-mutate` (`editor | call NewScene(…)`).
