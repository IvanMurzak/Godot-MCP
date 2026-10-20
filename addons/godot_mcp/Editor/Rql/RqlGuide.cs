/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System.Linq;
using System.Text.RegularExpressions;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// Agent-facing texts of the RQL tools (rql-query / rql-mutate / rql-schema) and the <c>godot-rql</c> skill — the Godot
    /// adaptation of Unity-MCP's RqlGuide. The guide travels in the tool descriptions (the agent loads them before first
    /// use); the one-line blurbs are what the always-visible tool catalog shows.
    /// <para>
    /// Pure strings (no Godot API, not behind <c>#if TOOLS</c>) so the xUnit suite parses every example, and
    /// <c>Godot-Tests/rql-guide-examples.json</c> mirrors <see cref="QueryExamples"/> / <see cref="MutateExamples"/> so
    /// the live CI smoke (<c>scripts/smoke_rql.py</c>) runs each one against a real editor. The examples are written
    /// against that smoke's fixture scene and run in the listed order, so keep them executable top to bottom.
    /// </para>
    /// </summary>
    internal static class RqlGuide
    {
        public const string QueryBlurb =
            "Query the live Godot editor (scene nodes, resources, files, scenes, classes, console, project settings, input map) with RQL in ONE call: filter, " +
            "project, sort, count, walk the scene tree. Far fewer calls/tokens than reading nodes one by one.";

        public const string MutateBlurb =
            "Change the live Godot editor with RQL set/call: bulk edits in one all-or-nothing, undoable call; add, duplicate, " +
            "reparent and remove nodes; connect signals, groups; project settings, input map, autoloads, layer names; TileSets, Themes, " +
            "shader parameters, MeshLibraries; " +
            "create, move and delete resources; open and save scenes. dry_run previews.";

        public const string SchemaBlurb =
            "List RQL sources and the node classes present in the edited scene; pass a type name for its members and Godot properties.";

        public const string Language =
            "RQL: source | stage | stage ...\n" +
            "Sources: nodes (every node of the edited scene, depth-first, root included), root (the edited scene root), " +
            "selection, scenes (open scenes), files (every res:// file the editor knows), classes (Godot classes, ClassDB), " +
            "console (captured editor log entries), editor (editor state and operations), project (project settings: " +
            "settings[\"section/key\"], name, mainScene; input map, autoload and global group calls), settings (every project " +
            "setting: name section value type changed), inputActions (name events deadzone builtIn), autoloads (name path " +
            "singleton order), groups (name nodes count global), layers (kind index name: the project's layer names). " +
            "Resources: godot.resources(\"StandardMaterial3D\") or godot.resources(\"Texture2D\", \"res://art\"), " +
            "godot.resource(\"res://materials/red.tres\"). ref(\"Node3D#1234\") resolves a $ref; godot.find(\"Player\") or " +
            "godot.find(\"Level/Player\") a node by unique name or path below the scene root; godot.group(\"enemies\") the nodes of a group; " +
            "godot.callable(\"Player\", \"_on_hit\") a Callable.\n" +
            "Stages:\n" +
            "  where <predicate>   filter\n" +
            "  select a b.c alias=(expr)   project columns (wrap computed expressions in parentheses)\n" +
            "  sort <expr> [desc], ...   stable sort\n" +
            "  first N | skip N | count\n" +
            "  get <path>   replace each item by a member (lists are flattened)\n" +
            "  recurse children [max-depth=N] [include-self=false]   walk the scene tree (input items included by default)\n" +
            "  distinct [expr] | group-by expr | sum expr | min expr | max expr | avg expr\n" +
            "Expressions: == != < <= > >= and or not + - * / %; contains, starts-with, ends-with (case-insensitive); " +
            "matches \"regex\"; x in [a, b]; x is null; x is not null; $ is Node3D (includes subclasses); has(member). " +
            "Literals: numbers, \"strings\", true/false/null, lists [a, b], objects {x: 1, y: 2}.\n" +
            "Paths: a.b.c, list[0], map[\"key\"]; .member on a list maps over its elements. List methods: .any(pred) .all(pred) " +
            ".count([pred]) .sum(e) .min(e) .max(e) .first([pred]); inside them bare names refer to the element, falling back to the outer item.\n" +
            "Meta: $ (current item), $ref (stable id such as Node3D#1234), $type, $path, $index, $depth. Enums compare to strings; " +
            "== on strings is case-sensitive.\n" +
            "Godot: every node has name, path (below the scene root, \".\" = root), class (the Godot class, e.g. \"OmniLight3D\"), " +
            "parent, owner, children (list), childCount, index, script (res:// path), groups (list) and id; signals, connections " +
            "(persistent: signal target method flags) and incoming are read on request (select connections, get connections). " +
            "Members are the C# properties " +
            "(Position, Rotation, Scale, Visible, Text …), matched case-insensitively, so position works too. Every Godot property, " +
            "including GDScript script variables and GDExtension properties, is in props[\"snake_name\"] (e.g. props[\"light_energy\"], " +
            "props[\"speed\"] on a scripted node). Vectors / colors print as (x, y, z); write \"(1, 2, 3)\", [1, 2, 3] or " +
            "{x: 1, y: 2, z: 3}; colors also \"#ff8800\". A resource path \"res://…\" works wherever a resource is expected, " +
            "a node path or name wherever a node is expected. Freed nodes read as null. Input events are text: \"Key:Space\", " +
            "\"Key:Ctrl+S\", \"PhysicalKey:W\", \"Mouse:Left\", \"JoypadButton:A\", \"JoypadMotion:LeftX-\". A TileSet has sources " +
            "(id kind atlas texture tileCount), physicsLayers, terrainSets, customDataLayers; an atlas source has tiles (coords size data). " +
            "A Theme has maps keyed \"ControlType/item\": colors, constants, fonts, fontSizes, icons, styleboxes. " +
            "A ShaderMaterial has uniforms (name type default value overridden) and params[\"uniform\"] (typed by the shader; null reverts " +
            "to the default); a MeshLibrary has items (id name mesh meshTransform shapes navigationMesh preview). Layer masks by name: " +
            "layerNames[\"collision_mask\"] on any object with layer properties is a list of layer names (a number for an unnamed layer). " +
            "Callables are \"Player::_on_hit\" (target::method); RIDs are read-only \"RID(…)\"; a Projection is 16 numbers. " +
            "Navigate with paths or get; select only " +
            "projects output columns.\n" +
            "Results come back as compact rows (header line, then col|col). Long results are truncated — refine with where/first or use count. " +
            "Unknown members produce warnings with suggestions; rql-schema(type) lists a type's members and Godot properties.";

        /// <summary>
        /// rql-query examples, one per line ("  &lt;query&gt;"); run in order by the live CI smoke against its fixture scene.
        /// Const (not built at run time) because the tool's [Description] attribute embeds them.
        /// </summary>
        public const string QueryExampleText =
            "  nodes | where name contains \"Enemy\" | select $ref name class path\n" +
            "  nodes | where $ is Node3D and Position.Y > 0 | select name Position\n" +
            "  nodes | where class == \"OmniLight3D\" | select name props[\"light_energy\"] props[\"light_color\"]\n" +
            "  root | recurse children | where childCount == 0 | select path class\n" +
            "  godot.resources(\"StandardMaterial3D\") | select path props[\"albedo_color\"]\n" +
            "  files | where ext == \"tscn\" | select path\n" +
            "  scenes | select name path active\n" +
            "  classes | where parent == \"Light3D\" | select name\n" +
            "  console | where logType == \"Error\" | sort sequence desc | first 5 | select message\n" +
            "  editor | select isPlaying editedScene selection\n" +
            "  godot.group(\"enemies\") | select name path\n" +
            "  nodes | where name == \"Player\" | get incoming | select source signal method\n" +
            "  settings | where section == \"display\" and changed | select name value\n" +
            "  inputActions | where builtIn == false | select name events\n" +
            "  autoloads | select name path singleton\n" +
            "  godot.resources(\"TileSet\") | get sources | select id kind texture tileCount\n" +
            "  godot.resource(\"res://materials/water.tres\") | get uniforms | select name type default value overridden\n" +
            "  nodes | where class == \"StaticBody3D\" | select name layerNames[\"collision_layer\"] layerNames[\"collision_mask\"]\n" +
            "  layers | where kind == \"3d_physics\" and name != \"\" | select index name\n" +
            "  godot.resources(\"MeshLibrary\") | get items | select id name mesh";

        /// <summary>
        /// rql-mutate examples, one per line ("  &lt;query&gt;   &lt;what it does&gt;", at least three spaces before the note);
        /// run in order by the live CI smoke.
        /// </summary>
        public const string MutateExampleText =
            "  nodes | where name == \"Player\" | set Position = \"(0, 1, 0)\"   move a node\n" +
            "  nodes | where class == \"OmniLight3D\" | set props[\"light_energy\"] = props[\"light_energy\"] * 2, props[\"light_color\"] = \"#ffcc88\"   Godot properties by their snake_case name\n" +
            "  nodes | where name starts-with \"Enemy\" | set Visible = false   hide every enemy\n" +
            "  godot.resource(\"res://materials/red.tres\") | set props[\"albedo_color\"] = \"(1, 0, 0, 1)\"   edit a resource file (saved to disk)\n" +
            "  root | call AddChild(\"Node3D\", \"Props\")   new child node of a class; the receipt's result column holds its $ref\n" +
            "  nodes | where name == \"Props\" | call InstantiateScene(\"res://scenes/enemy.tscn\")   instance a scene as a child\n" +
            "  nodes | where name == \"Crate\" | call Duplicate()   copy next to it (returns the copy)\n" +
            "  nodes | where name == \"Crate\" | call Reparent(godot.find(\"Props\"))   reparent (keeps the global transform)\n" +
            "  nodes | where name == \"Crate\" | call MoveChild(0)   reorder among its siblings (-1 = last)\n" +
            "  nodes | where name == \"Old\" | call Free()   remove a node (undoable, like Delete in the Scene dock)\n" +
            "  editor | set selection = [godot.find(\"Player\")]   change the editor selection\n" +
            "  editor | call SaveScene()   write the edited scene to its .tscn\n" +
            "  editor | call CreateResource(\"res://materials/blue.tres\", \"StandardMaterial3D\")   new resource file\n" +
            "  editor | call MoveResource(\"res://materials/blue.tres\", \"res://materials/old/blue.tres\")   move / rename a file (its .import / .uid sidecars move with it)\n" +
            "  editor | call DeleteResource(\"res://materials/old/blue.tres\")   delete a file and its sidecars\n" +
            "  editor | call NewScene(\"res://scenes/new.tscn\", \"Node2D\", \"Main\")   create, save and open a scene (path, root class, root name)\n" +
            "  editor | call OpenScene(\"res://scenes/level.tscn\")   open a scene file\n" +
            "  editor | call Reimport([\"res://materials/red.tres\"])   re-import files (no argument: full filesystem scan)\n" +
            "  editor | call ClearConsole()   clear the captured editor log\n" +
            "  nodes | where name starts-with \"Enemy\" | call AddToGroup(\"enemies\")   add nodes to a group (saved with the scene; set groups = [...] replaces them)\n" +
            "  nodes | where name == \"Enemy1\" | call Connect(\"tree_exited\", godot.find(\"Player\"), \"_on_enemy_died\")   connect a signal to a method of the target's script (saved with the scene)\n" +
            "  nodes | where name == \"Enemy1\" | get connections | where method == \"_on_enemy_died\" | call Disconnect()   remove a connection\n" +
            "  project | set settings[\"display/window/size/viewport_width\"] = 1280   any project setting by its path (saved to project.godot)\n" +
            "  project | call AddInputAction(\"jump\", [\"Key:Space\", \"JoypadButton:A\"])   new input action with its events\n" +
            "  inputActions | where name == \"jump\" | call AddEvent(\"Key:W\")   add an event to an input action (RemoveEvent, set events = [...], set deadzone)\n" +
            "  project | call AddAutoload(\"Game\", \"res://scenes/game.gd\")   register an autoload singleton\n" +
            "  godot.resource(\"res://tiles/terrain.tres\") | call AddAtlasSource(\"res://tiles/terrain.png\")   add a texture to a TileSet (returns the source id)\n" +
            "  godot.resource(\"res://tiles/terrain.tres\") | get sources | get atlas | call CreateTilesFromTexture()   create tiles in the non-transparent grid cells\n" +
            "  godot.resource(\"res://ui/theme.tres\") | set colors[\"Button/font_color\"] = \"#ffcc00\", constants[\"HBoxContainer/separation\"] = 8   edit Theme items\n" +
            "  layers | where kind == \"3d_physics\" and index == 2 | set name = \"enemies\"   name a layer (saved to project.godot)\n" +
            "  nodes | where name == \"Wall\" | set layerNames[\"collision_mask\"] = [1, \"enemies\"]   a layer mask by layer names (numbers for unnamed layers)\n" +
            "  godot.resource(\"res://materials/water.tres\") | set params[\"speed\"] = 2, params[\"tint\"] = \"#3388ff\"   shader uniforms of a ShaderMaterial (null or call ClearParam(name) reverts one)\n" +
            "  godot.resource(\"res://meshes/tiles.tres\") | call CreateItem(\"Floor\", \"res://meshes/floor.tres\")   add a MeshLibrary item (set its shapes, navigationMesh … on the returned row)";

        /// <summary>The queries of <see cref="QueryExampleText"/>, in order.</summary>
        public static string[] QueryExamples => Examples(QueryExampleText);

        /// <summary>The queries of <see cref="MutateExampleText"/> (without their notes), in order.</summary>
        public static string[] MutateExamples => Examples(MutateExampleText);

        private static string[] Examples(string text) =>
            text.Split('\n').Select(l => Regex.Split(l.Trim(), @"\s{3,}")[0]).Where(l => l.Length > 0).ToArray();

        public const string MutateStages =
            "  set path = expr, ...   assign members (paths may go through structs: set Position.Y = Position.Y + 1)\n" +
            "  call Method(args)   invoke a method the tools expose\n";

        public const string MutateExamplesBlock =
            "Examples:\n" + MutateExampleText + "\n" +
            "Editor state: editor | set isPlaying = true / false runs / stops the main scene; editor | call PlayScene(\"res://…\") runs one scene.\n";

        public const string QueryDescription =
            "Run a read-only RQL query against the live Godot editor and return compact rows. One precise query beats many " +
            "exploratory calls. Changes need rql-mutate.\n\n" + Language + "\n\nExamples:\n" + QueryExampleText;

        public const string MutateDescription =
            "Change the live Godot editor with an RQL query whose last stage is set or call. All-or-nothing: every target and " +
            "value is checked before anything is written; a failure changes nothing. Undoable in the editor (Ctrl+Z). Scene " +
            "edits mark the scene unsaved (save with editor | call SaveScene()); edited resource files (res://….tres) are saved " +
            "to disk automatically. dry_run=true returns the receipt ($ref, member, old, new) without changing anything. Do " +
            "exactly the change requested; a read-only member fails with readonly-member instead of being approximated.\n" +
            MutateStages + MutateExamplesBlock +
            "Object literals build values: set Position = {x: 1, y: 2, z: 3}. At most 20 targets per call.\n\n" +
            QuerySyntaxShort;

        /// <summary>
        /// rql-mutate's reminder of the query part; the full language guide is in rql-query's description (sent once per
        /// request instead of twice).
        /// </summary>
        public const string QuerySyntaxShort =
            "Query part: the RQL of rql-query (full guide and the sources in its description); godot.resources(\"Texture2D\"), " +
            "godot.resource(path), godot.find(\"Name\"), ref(\"Node3D#1234\"). Stages: where, select, sort, first N, get <path>, " +
            "recurse children. Operators: == != < > and or not, contains, starts-with, $ is Node3D. Paths a.b.c, list[0], " +
            "props[\"key\"]. Vectors/colors as \"(1, 2, 3)\" or \"#ff8800\"; enums as strings.";

        public const string SchemaDescription =
            "Without a type: RQL sources, functions, and the node classes present in the edited scene (with counts), plus the " +
            "Node members. With a type name (e.g. \"Node3D\", \"OmniLight3D\", \"StandardMaterial3D\", \"Vector3\", \"editor\"): that " +
            "type's members (name type; ro = read-only), methods, and its Godot properties (props[\"name\"]). Use it before " +
            "querying an unfamiliar class.";
    }
}
#endif
