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
| `editor-application-get-state` / `-set-state` | `editor` members `isPlaying` (writable), `playingScene`, `editedScene`, `godotVersion`; `call PlayScene / StopScene`; what the game run from the editor prints and its errors: the `gameLog` source (`Sequence LogType Message StackTrace`, read from the game's log file, `debug/file_logging/log_path`) |
| — | `editor \| call Undo()` / `Redo()`: the editor's own Undo / Redo (the Scene menu items behind Ctrl+Z), returning the action (`undone: RQL: <query>`; an rql-mutate's undo action is named after its query); an error when there is nothing to undo / redo |
| — | `editor \| call ReloadScene(path?)` (Scene → Reload Saved Scene: unsaved changes dropped), `NewScene(path, "res://base.tscn", name?)` (an inherited scene of a base scene) |
| `editor-selection-get` / `-set` | `editor` member `selection` (writable), the `selection` source |
| `console-get-logs`, `console-clear-logs` (editor) | the `console` source (`where LogType == "Error"`, `Sequence > N`; the framework's echo of each tool call is printed to the Output but not captured, and a plugin error is captured once), `editor \| call ClearConsole()` |
| `reflection-method-find`, `reflection-method-call` (editor) | `call CallMethod("name", [args])` on any Godot object, node / resource calls, `rql-schema` |
| — | `rql-schema`: sources, functions, the node classes in the edited scene; one type's members, methods and Godot properties (ClassDB) |

## Data models beyond nodes and resources

What the Godot editor keeps outside plain properties, each with read, write, undo and saving:

| Model | Read | Change | Saved to |
|---|---|---|---|
| **Groups** | node `groups` (persistent groups, internal `_…` excluded; a group a tool script joined without `persistent` is not listed), `godot.group("enemies")`, the `groups` source (`name nodes count global description`) | `call AddToGroup(g)` / `RemoveFromGroup(g)` (return whether anything changed), `set groups = [...]` (a listed edit-time group becomes persistent) | the scene (`groups=[…]` in the node header) |
| **Signals** | node `signals` (`name args connections`), `connections` (persistent ones: `signal target method flags deferred oneShot`), `incoming` | `call Connect(signal, target, method, deferred?, oneShot?)` (the method must exist in the target's script or class), `Disconnect(signal, target, method)`, a connection row's `Disconnect()` | the scene (`[connection …]`) |
| **Project settings** | the `settings` source (`name section value type changed`), `project` (`settings["section/key"]`, `name`, `mainScene`) | `project \| set settings["display/window/size/viewport_width"] = 1280` (typed like the Project Settings dialog), `call SetSetting(name, value)` (custom), `ClearSetting(name)` | `project.godot` |
| **Input map** | the `inputActions` source (`name events deadzone builtIn`); events as text: `Key:Space`, `Key:Ctrl+S`, `PhysicalKey:W`, `Mouse:Left`, `Mouse:Shift+WheelUp`, `JoypadButton:A`, `JoypadMotion:LeftX-` | `project \| call AddInputAction(name, events?, deadzone?)` / `RemoveInputAction` / `AddInputEvent` / `RemoveInputEvent`; rows: `call AddEvent / RemoveEvent / Remove`, `set events = [...]`, `set deadzone` | `project.godot` (`[input]`, all devices) |
| **Autoloads** | the `autoloads` source (`name path singleton order`) | `project \| call AddAutoload(name, path, singleton?)` / `RemoveAutoload`; rows: `set path`, `set singleton`, `call Remove()` | `project.godot` (`[autoload]`) |
| **Global groups** | the `groups` source (`global`, `description`) | `project \| call AddGlobalGroup(name, description?)` / `RemoveGlobalGroup` | `project.godot` (`[global_group]`) |
| **TileSet** | `sources` (`id kind atlas scenes texture tileCount`), `physicsLayers`, `navigationLayers`, `occlusionLayers`, `terrainSets` (`index mode terrains`), `customDataLayers` (`index name type`); an atlas source's `tiles` (`coords size alternatives data`) and `gridSize` | `call AddAtlasSource(texture, tileSize?)`, `RemoveSource(id)`, `AddPhysicsLayer()`, `AddNavigationLayer()`, `AddOcclusionLayer()`, `AddTerrainSet(mode?)`, `AddTerrain(set, name, color?)`, `AddCustomDataLayer(name, type)`; atlas: `CreateTile(coords, size?)`, `RemoveTile(coords)`, `CreateTilesFromTexture()`; tile settings on `data` (TileData: `Modulate`, `ZIndex`, `props["custom_data_0"]` …); layer settings in `props["physics_layer_0/collision_layer"]` | its `.tres` (or the scene that embeds it) |
| **Theme** | `types`, `items` (`type kind name value`), maps keyed `"ControlType/item"`: `colors`, `constants`, `fonts`, `fontSizes`, `icons`, `styleboxes` | `set colors["Button/font_color"] = "#ffcc00"` (a new key adds the item, `null` removes it), `call ClearItem(kind, key)`, `ClearType(type)` | its `.tres` |
| **Shader parameters** | ShaderMaterial `uniforms` (`name type group default value overridden`; defaults from the rendering server, else the literal in the code — the headless renderer keeps none), `params["uniform"]`; a Shader's (or VisualShader's) `uniforms` | `set params["speed"] = 2, params["tint"] = "#3388ff"` (typed by the uniform, validated against the shader's uniform list, a sampler takes a texture of its class), `null` or `call ClearParam(name)` reverts to the default, `ClearParams()` | the material's `.tres` (`shader_parameter/…`) |
| **Layer names** | the `layers` source (`kind index bit name setting` for 2d/3d render, physics, navigation and avoidance); every object's `layerNames["<property>"]` for its layer-hinted properties (`collision_layer`, `collision_mask`, `visibility_layer`, `light_mask`, `layers`, `cull_mask`, `navigation_layers`, a TileSet's `physics_layer_0/collision_mask` …) as a list of names, an unnamed layer as its number | rows: `set name = "enemies"`; objects: `set layerNames["collision_mask"] = ["world", 3]` (names or numbers; bits above the kind's count are kept); the raw ints (`CollisionMask`, `props["collision_mask"]`) keep working | `project.godot` (`[layer_names]`); the scene / resource |
| **MeshLibrary** | `items` (`id name mesh meshTransform shapes navigationMesh navigationMeshTransform navigationLayers preview`), an item's `shapes` (`index shape transform`) | `call CreateItem(name, mesh?, id?)`, `RemoveItem(id)` (a removed item comes back with all its settings on undo); rows: `set name / mesh / meshTransform / navigationMesh / navigationLayers / preview …`, `call AddShape(shape, transform?)`, `ClearShapes()` | its `.tres` |
| **Plane** | `"(a, b, c, d)"` — normal and distance (e.g. `WorldBoundaryShape3D.Plane`) | text, list or object literal | — |
| **Projection** | 16 numbers column by column (e.g. a `mat4` uniform) | text or list | — |
| **RID** | `"RID(1234)"`, `Id`, `IsValid` (a World3D's `Space`, `Texture2DRD.TextureRdRid` …) | read-only: an opaque server handle | — |
| **Callable** | `"Player::_on_hit"` (`target::method`; the target as a node path, a `res://` path or `Class#id`), `target`, `method`, `valid` | that text, `[target, method]`, `{target: …, method: …}` or `godot.callable(target, method)`; the target must have the method (e.g. `MultiplayerSpawner.SpawnFunction`, a `CallMethod` argument) | — (a Callable property is not stored) |

Structural changes register their own do/undo operations (connect/disconnect, add/remove group, add/remove a TileSet
source, layer or tile — a removed tile comes back with its data — Theme items and MeshLibrary items); shader parameters,
layer masks and MeshLibrary item settings are stored properties and go through the property snapshot; project settings are snapshotted
like any object, and undoing them writes `project.godot` again. Changes to a sub-resource stored inside a resource
file (a TileSet's atlas source, also one created in the same session) save that file.

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
- **Target limit.** A `set` / `call` stage changes at most 200 objects by default; `max_targets` (1–1000) raises it for
  an intended bulk change, which stays one all-or-nothing call and one undo action (`RqlTargetLimit`). A misspelt member
  that leaves a mutation with no target is an `unknown-member` error; a member some Godot type declares (a polymorphic
  `nodes` stream with no node of that type) stays a warning (`RqlGodot.Names.cs`, the engine's `IsKnownMember`).
- **Undo.** One `EditorUndoRedoManager` action per `rql-mutate` (Ctrl+Z): a `set` snapshots the stored Godot properties
  of every changed object and records the differences as do/undo properties. The changed object is the nearest node,
  resource or project-settings object enclosing the write; a plain `Object` held by a node property is snapshotted
  itself, one beneath a view row is a change of the object the row stands for (a tile's `TileData` → its atlas
  source). Undo steps a setter registers itself (Theme items, `groups`) join the action only when the `set` commits:
  a `set` the engine rolls back leaves no undo entry (`RqlSetStageBuffer`). The structural node calls register the
  same do/undo methods the Scene dock does (the removed node of `Free()` stays alive in the undo history). An action
  that touches scene nodes and global objects (file resources, project settings) is split in two, one per undo
  history; every step is forced into its action's history (`ForceFixedHistory`), so a path-less resource cannot
  scatter it.
- **Saving** follows the replaced tools: a change to a node of the edited scene marks the scene unsaved — write it
  with `editor | call SaveScene()` (like Ctrl+S; `node-modify` did not save either); a change to a resource stored in
  its own file (`res://….tres`) is saved to that file after the mutation, like `resource-modify` (only when a property
  actually changed), and undo / redo of that change save the file again. Built-in (scene-embedded) resources mark the
  scene unsaved. Problems saving become `not-saved` warnings on the receipt. A file written into a folder the editor
  filesystem never scanned (`NewScene`, `CreateResource`, `SaveScene(path)`, `CreateFolder`, `script-create`) triggers a
  full scan after the write (`RqlReimport.Register`): `UpdateFile` ignores such a file, so the `files` source,
  `godot.resources(type, folder)` and a new GDScript `class_name` would not see it (a second such file in the same
  frame is registered when that scan lands; a `.gdignore` or dot-named folder is never scanned for). `NewScene` onto the path of a scene
  that is still open in a background tab (its file was deleted while another scene was edited) replaces the cached
  scene and reloads that tab, so the new scene is shown, not the old nodes; `OpenScene` fails when the editor ends up
  on another scene than the one asked for.
- **Refused instead of silently changed**: a rename Godot would alter (a character it does not allow in node names,
  `. : @ / " %`, or a sibling's name); `SaveScene(path)` onto another existing file; `InstantiateScene` of a scene that
  contains the edited one; `MoveResource` that changes the extension; `DeleteResource` of a file a scene or resource still
  uses, the edited scene's unsaved nodes included (`DeleteResource(path, true)` deletes anyway); moving or deleting
  `res://project.godot`, `res://.godot/` or this addon; `CallMethod` on a method of a non-`@tool` script (it does not run
  in the editor), with the wrong number of arguments or with an argument that does not convert to the type the method
  declares; a multi-target `Connect` where one target lacks the signal or method; `Free`, `Reparent`, `MoveChild`,
  `Duplicate` or a rename of a node that belongs to a scene instance (Godot saves only property overrides of those; the
  Scene dock refuses the same edits). Paths stay inside the edited scene: the root's `parent` is null and
  `godot.find("..")` finds nothing (a miss suggests similar node paths).
- **Batch checks** (RQL `MutationPolicy.ValidateBatch`, run on every `set` / `call` stage before anything changes, dry
  runs included; a refusal is `invalid-value`): a `Free()` of a node inside a scene instance is refused up front unless
  an ancestor is freed by the same call (`godot.find("Enemy") | recurse children | call Free()` removes the instance with
  its nodes). Hints never advise `budgets.*` (the tools take no budgets) and a `ref("Name")` hint names `godot.find("…")`.
- **Targets and map entries**: `$index` / `$path` in a `set` value or `call` argument are the
  target's (`nodes | where name starts-with "Enemy" | sort name | set Position.X = $index * 2`); a `set` on the entries
  `get` streams from a map writes through it (`godot.resource("res://ui/theme.tres") | get colors | where Key
  starts-with "Button/" | set Value = "#ffffff"` is the key-path write `colors["Button/…"]`, undo and save included).
- **Scene instances**: `InstantiateScene` instances like the Scene dock (`GenEditState.Instance`), so the `.tscn` stores only
  the overrides and the instance keeps following its scene file (type, script, groups). A change to a node inside an
  instance (a property, a group, a child added to it) turns on that instance's Editable Children, undoably, because Godot
  saves changes inside an instance only then.
- **Values as an agent writes them**: `CallMethod` converts each argument like a `set` to the type the Godot method
  declares (`"(0, 5)"` for a Vector2i, `"res://…"` for a resource, an enum by name:
  `CallMethod("set_anchors_preset", ["FullRect"])`); an enum-hinted int property takes its inspector label
  (`props["grow_horizontal"] = "Both"`), its number, or (Godot 4.4+) its setter's enum constant (`"GROW_DIRECTION_BOTH"`); an object literal
  naming a class builds a new embedded resource: `set Shape = {"$type": "BoxShape3D", Size: "(1, 2, 1)"}` (or
  `{class: "BoxShape3D", …}`; a class Godot cannot instantiate is refused). Script classes (GDScript `class_name`) work in
  those literals, in `CreateResource(path, "ItemData")` and in `godot.resources("ItemData")`.
- **Rotation is radians** (`Rotation`, `GlobalRotation` of Node3D, Node2D, Control): a written angle beyond a full turn
  (`set Rotation = "(-45, 30, 0)"`) is applied as given, and the receipt warns (`radians`) with the `RotationDegrees` write that
  was probably meant. Script-only Godot properties (usage none, such as `rotation_degrees`, `global_position`) are reachable
  by name in `props["…"]` (not listed among its keys). An enum-hinted int also takes its label without the note
  (`"PSSM 4 Splits"` for `"PSSM 4 Splits (Quality)"`) and (Godot 4.4+) its C# enum name (`"Parallel4Splits"`).
- **Printed structs** are tidied for reading (components below 1e-6 print as 0, at most 7 significant digits, no
  exponent): float noise such as `-4.371139E-08` in a rotated basis never reaches the agent; comparisons and stored values
  stay exact. An object literal compares like the text form: `where Position == {x: 0, y: 1, z: 0}`.

## Code

- `addons/godot_mcp/Editor/Rql/` (namespace `com.IvanMurzak.Godot.MCP.Editor.Rql`):
  - `RqlGodot*.cs` — the engine over the live editor: factory (`AccessorMode.Compiled`, `$ref` = `Class#instance id`
    via `GodotObject.InstanceFromId`, liveness via `IsInstanceValid`), sources and functions, mutation hooks, node
    operations. One engine per editor session; `Reset()` on plugin teardown.
  - `EditorApi/` — the `editor` source (state, scenes, resource files, files, console, classes) and its view types.
  - `Adapters/` — one adapter per file: GodotObject (`id`, `class`, `props`, `CallMethod`), Node, Node2D, Node3D,
    Control, Resource, ProjectSettings (the `project` source), TileSet, TileSetAtlasSource, Theme, Shader, ShaderMaterial
    (`ShaderUniformDefaults.cs`: the pure parser of literal uniform defaults, unit-tested), MeshLibrary; StringName /
    NodePath / Variant / Callable (`CallableText.cs`: the pure text form, unit-tested); Vector2/3/4(I), Color, Quaternion,
    Basis, Transform2D/3D, Rect2(I), Aabb, Plane, Projection, Rid (pure C#, unit-tested; `-0` prints as `0`); NodeRef (tool
    arguments).
  - `RqlGodot.Signals.cs` — groups and signal connections of the edited scene's nodes.
  - `RqlReferences.cs` — who still uses a file (saved scenes and resources, the edited scene's live nodes): `DeleteResource`'s check.
  - `RqlSetStageBuffer.cs`, `RqlMutationOwner.cs` — the mutation hooks' pure parts (setter undo steps held until their
    `set` commits; which object a change is snapshotted on), unit-tested.
  - `Settings/` — project settings, input map (`InputEventText.cs`: the pure event grammar, unit-tested;
    `InputEvents.cs`: ⇄ Godot `InputEvent`s), autoloads and groups (`RqlProject.cs`), layer names (`LayerNames.cs`: the pure
    mask ⇄ name mapping, unit-tested; `RqlLayers.cs`: the `layers` source and the `layerNames` map).
  - `Modules/TestHooksModule.cs` — `test.undo` / `test.redo` / `test.action` / `test.undoManager` (true only when the
    undo manager is reachable; the smoke's probe) / `test.addToGroup(node, group, persistent)`, only with
    `GODOT_MCP_RQL_TEST_HOOKS=1` (the live smoke drives undo with them; never on in a user's editor).
  - `Modules/GodotCoreModule.cs`, `Modules/ToolTypesModule.cs` — the core module; modules are discovered in every loaded
    assembly (`GodotAssemblyUtils.AllAssemblies`), so an extension's `IRqlModule` in the consumer project assembly is
    found as soon as it is installed (its `ctx.AddGuide` text appears in the `godot-rql` skill).
  - `RqlGodotSchema.cs`, `GodotTypes.cs`, `RqlGuide.cs` (agent texts; every example runs in CI), `Skill_Rql.cs`
    (`godot-rql`), `ToolFormat/` (`RqlToolBinding`, McpPlugin `IToolBinding`), `RqlInstaller.cs`, `RqlReimport.cs`.
- `addons/godot_mcp/Editor/Tools/Tool_Rql*.cs` — the three tools.
- Installation: `GodotMcpPlugin` calls `RqlInstaller.Install` and hands the configuration it returns to the
  `GodotMcpConnection` constructor (`configurePlugin`), which invokes it after building each plugin.

## Hot reload

The RQL assemblies are NuGet dependencies, which the addon's resolver loads into the default (non-collectible) load
context. RQL keeps process-wide caches keyed by `Type` / `MemberInfo`; once a query touched a type of the collectible
project assembly they would keep it alive and a C# rebuild could not unload it. Teardown therefore drops the engine,
the tool signatures and the type index, and `RqlStaticCaches.Clear()` empties those caches by reflection (RQL 0.1 has
no public API for it; the xUnit suite fails if the fields it clears disappear). Module and type discovery skip the
previous copy of the project assembly that the process may still list during a reload. The dock-reload CI legs prove
the reload still unloads.

## Tests

- `Godot-MCP.Tests/Rql*Tests.cs` (xUnit, no Godot binary): every guide example parses, the struct adapters (Plane,
  Projection, Rid and negative zero included), the input-event text grammar, layer names ⇄ bits, shader uniform defaults,
  the Callable text form, the tool binding (schemas, RQL arguments, compact results, escaping), type names, the cache
  release, the config setting, and the mutation hooks' pure parts over a real engine (a rolled-back `set` drops its
  setters' undo steps, calls keep theirs; owner selection).
- `scripts/smoke_rql.py` (CI: `test_godot_runtime_harness.yml`, every Godot version of the matrix) against a live
  headless editor: read-back of every change from the `.tscn` / `.tres` Godot wrote (the former
  `smoke_property_patches.py` cases), `AddChild` + `SaveScene` round-trip, resource move / delete with sidecars, and
  every example of the guide (`Godot-Tests/rql-guide-examples.json`) in order. `scripts/smoke_rql_models.py` adds
  ~500 checks: every existing adapter on real properties (Node2D, Node3D, Control, Resource, NodePath, StringName,
  Variant, NodeRef, the struct types), groups, signal connections, project settings, the input map, autoloads, global
  groups, TileSets and Themes — each change read back from the `.tscn` / `.tres` / `project.godot` Godot wrote,
  validation errors, dry runs, and (with `GODOT_MCP_RQL_TEST_HOOKS=1` in the editor) undo and redo of every kind of
  change; without the hooks (or without a reachable undo manager, probed by `test.undoManager()`) those checks print
  `SKIP` (CI passes `--require-test-hooks`, which fails instead).
  `scripts/smoke_rql_adapters.py` does the same for shader parameters (a text shader and a VisualShader), layer names,
  MeshLibrary items and the Projection / RID / Callable values. `scripts/smoke_rql_hazards.py` covers the edits that used
  to corrupt data or answer misleadingly (the refusals above, undo of a transform-keeping Reparent, undo of a resource
  file edit, object-literal compares, tidy struct text).
- `scripts/smoke_rql_agent.py` (run by `smoke_rql.py`): what agent runs could not do or got wrong — editor Undo / Redo, the
  radians warning, script-only props, enum labels, the sibling-rename hint, inherited scenes and ReloadScene, the `gameLog`
  of a game run from the editor (skipped where the runner cannot start the game).
- The extension suite (`test_extensions.yml`, `scripts/extension_live.py`) runs every extension's live manifest in
  the default `Rql` format: each module's guide examples in order, read-backs, and rejected values (`expectFailure`).

## Extensions are RQL modules

The `extensions/Godot-AI-*` packages add no MCP tools. Each ships an `IRqlModule` (`Editor/Rql/<Feature>Module.cs`,
Order 100) that compiles into the consumer project assembly, where the core discovers it: adapters for the Godot types
it covers (validated members that replace C# properties, virtual members, methods), sources for state no node owns,
and a guide (`ctx.AddGuide`) that the `godot-rql` skill renders. A module uses only Godot, `com.IvanMurzak.Rql` and
the BCL — never core-addon types — so the package still builds on its own; undo, dirty marking and saving come from the
core's mutation hooks, which record the Godot object a `set` writes or a `call` targets. Extensions that wrap a
GDScript or GDExtension addon (Beehave, Dialogic, PhantomCamera, Terrain3D) cannot target its classes with an adapter
(they have no C# type), so they expose a source (`beehave`, `dialogic`, `phantomcamera`, `terrain3d`) with rows and
validated calls where the addon needs them, and otherwise write through `props["…"]` and the core node calls. Every guide
example is unit-tested to parse and runs live in the extension suite (`tests/e2e/e2e-tools.json`). The scaffold is
`templates/extension/` (`python scripts/new_extension.py <Feature>`); the Unity-MCP counterparts are its extensions'
`Editor/Scripts/Rql/*Module.cs`.
