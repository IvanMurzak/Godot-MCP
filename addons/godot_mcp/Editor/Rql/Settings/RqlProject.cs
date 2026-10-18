/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Tools;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Settings
{
    /// <summary>
    /// A view row (input action, autoload) whose writes change a Godot object: the mutation hooks treat a write or call on
    /// the row as a change of <see cref="Owner"/>, so it gets the owner's undo snapshot and saving.
    /// </summary>
    public interface IRqlViewOf
    {
        GodotObject Owner { get; }
    }

    /// <summary>
    /// The project settings (<c>project.godot</c>) behind the <c>project</c>, <c>settings</c>, <c>inputActions</c>,
    /// <c>autoloads</c> and <c>groups</c> sources. Every change goes through <see cref="ProjectSettings.SetSetting"/> on the
    /// ProjectSettings singleton, which the mutation hooks snapshot like any Godot object (one undo action, see
    /// <c>RqlGodot.Save.cs</c>) and then save to <c>project.godot</c>.
    /// </summary>
    public static class RqlProject
    {
        public const string InputPrefix = "input/";
        public const string AutoloadPrefix = "autoload/";
        public const string GlobalGroupPrefix = "global_group/";

        private static GodotObject? _instance;

        /// <summary>The ProjectSettings singleton object (one per process).</summary>
        public static GodotObject Instance => _instance != null && GodotObject.IsInstanceValid(_instance) ? _instance : _instance = Engine.GetSingleton("ProjectSettings");

        /// <summary>Whether <paramref name="o"/> is the ProjectSettings singleton (no native call for nodes and resources).</summary>
        public static bool Is(GodotObject o) => !(o is Node) && !(o is Resource) && o.GetInstanceId() == Instance.GetInstanceId();

        // ------------------------------------------------------------ settings

        /// <summary>Every project setting the editor knows (built-in and custom), in the editor's order.</summary>
        public static List<RqlSetting> Settings() =>
            GodotVariants.Properties(Instance).Select(kv => new RqlSetting(kv.Key, kv.Value.Type)).ToList();

        /// <summary>Set (or create) a setting; a null value removes a custom setting (a built-in one returns to its default).</summary>
        public static void Set(string name, Variant value)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("a setting name is required, e.g. \"application/config/name\"");
            if (name.IndexOf('/') <= 0) throw new ArgumentException($"'{name}' is not a setting path: settings are named \"section/key\", e.g. \"application/config/name\"");
            ProjectSettings.SetSetting(name, value);
        }

        /// <summary>The value of a setting, or null when there is none.</summary>
        public static object? Get(string name) => ProjectSettings.HasSetting(name) ? GodotVariants.ToClr(ProjectSettings.GetSetting(name)) : null;

        // ------------------------------------------------------------ input map

        /// <summary>Every input action of the project (the built-in ui_* actions included).</summary>
        public static List<RqlInputAction> InputActions() =>
            SettingNames(InputPrefix).Select(n => new RqlInputAction(n.Substring(InputPrefix.Length))).ToList();

        public static RqlInputAction AddInputAction(string name, string[]? events, double? deadzone)
        {
            RequireName(name, "an input action");
            if (ProjectSettings.HasSetting(InputPrefix + name)) throw new ArgumentException($"input action '{name}' already exists");
            var list = new global::Godot.Collections.Array();
            foreach (var text in events ?? Array.Empty<string>()) list.Add(InputEvents.Make(text));
            var action = new global::Godot.Collections.Dictionary
            {
                ["deadzone"] = deadzone ?? InputEvents.DefaultDeadzone,
                ["events"] = list,
            };
            Set(InputPrefix + name, action);
            return new RqlInputAction(name);
        }

        public static void RemoveInputAction(string name) => Set(InputPrefix + RequireInputAction(name), default);

        internal static string RequireInputAction(string name)
        {
            if (!ProjectSettings.HasSetting(InputPrefix + name))
                throw new ArgumentException($"no input action '{name}'" + RqlGodot.Similar(SettingNames(InputPrefix).Select(n => n.Substring(InputPrefix.Length)), name));
            return name;
        }

        // ------------------------------------------------------------ autoloads

        /// <summary>The project's autoloads, in load order.</summary>
        public static List<RqlAutoload> Autoloads() =>
            SettingNames(AutoloadPrefix).Select(n => new RqlAutoload(n.Substring(AutoloadPrefix.Length)))
                .OrderBy(a => a.order).ToList();

        public static RqlAutoload AddAutoload(string name, string path, bool singleton)
        {
            RequireName(name, "an autoload");
            if (!name.IsValidIdentifier())
                throw new ArgumentException($"'{name}' is not a valid autoload name (letters, digits and '_', not starting with a digit)");
            if (ProjectSettings.HasSetting(AutoloadPrefix + name)) throw new ArgumentException($"autoload '{name}' already exists");
            if (ClassDB.ClassExists(name)) throw new ArgumentException($"'{name}' is the name of a Godot class; an autoload cannot use it");
            if (ProjectSettings.GetGlobalClassList().Any(c => c["class"].AsString() == name))
                throw new ArgumentException($"'{name}' is a global script class (class_name); an autoload cannot use it");
            Set(AutoloadPrefix + name, AutoloadValue(RequireAutoloadPath(path), singleton));
            return new RqlAutoload(name);
        }

        public static void RemoveAutoload(string name) => Set(AutoloadPrefix + RequireAutoload(name), default);

        internal static string RequireAutoload(string name)
        {
            if (!ProjectSettings.HasSetting(AutoloadPrefix + name)) throw new ArgumentException($"no autoload '{name}'");
            return name;
        }

        internal static string RequireAutoloadPath(string path)
        {
            var p = ResPathNormalizer.RequireResFilePath(path, nameof(path));
            if (!ScriptLang_.IsScriptExtension(p) && !ResPathNormalizer.IsScenePath(p))
                throw new ArgumentException($"'{p}' is not a script or scene (.gd, .cs, .tscn, .scn)");
            if (!FileAccess.FileExists(p)) throw new ArgumentException($"no file at '{p}'");
            return p;
        }

        internal static string AutoloadValue(string path, bool singleton) => (singleton ? "*" : "") + path;

        // ------------------------------------------------------------ groups

        /// <summary>
        /// Every group used in the edited scene plus the project's global groups (Project Settings → Global Groups), by name.
        /// </summary>
        public static List<RqlGroup> Groups()
        {
            // One walk of the scene: each group gets its nodes.
            var groups = new SortedDictionary<string, List<Node>>(StringComparer.Ordinal);
            foreach (var node in RqlGodot.AllNodes())
                foreach (var g in RqlGodot.GroupsOf(node))
                {
                    if (!groups.TryGetValue(g, out var members)) groups[g] = members = new List<Node>();
                    members.Add(node);
                }
            foreach (var n in SettingNames(GlobalGroupPrefix))
                if (!groups.ContainsKey(n.Substring(GlobalGroupPrefix.Length))) groups[n.Substring(GlobalGroupPrefix.Length)] = new List<Node>();
            return groups.Select(kv => new RqlGroup(kv.Key, kv.Value)).ToList();
        }

        public static void AddGlobalGroup(string name, string? description)
        {
            RequireGroupName(name);
            if (ProjectSettings.HasSetting(GlobalGroupPrefix + name)) throw new ArgumentException($"global group '{name}' already exists");
            Set(GlobalGroupPrefix + name, description ?? "");
        }

        public static void RemoveGlobalGroup(string name)
        {
            if (!ProjectSettings.HasSetting(GlobalGroupPrefix + name)) throw new ArgumentException($"no global group '{name}'");
            Set(GlobalGroupPrefix + name, default);
        }

        internal static void RequireGroupName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("a group name is required");
            if (name.IndexOf('/') >= 0) throw new ArgumentException($"a group name cannot contain '/': '{name}'");
        }

        // ------------------------------------------------------------ helpers

        /// <summary>The names of the settings below <paramref name="prefix"/>, in the editor's order.</summary>
        internal static IEnumerable<string> SettingNames(string prefix) =>
            GodotVariants.Properties(Instance).Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal) && k.Length > prefix.Length);

        private static void RequireName(string name, string what)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException($"{what} needs a name");
            if (name.IndexOf('/') >= 0 || name.IndexOf(':') >= 0 || name.IndexOf('"') >= 0)
                throw new ArgumentException($"{what} name cannot contain '/', ':' or '\"': '{name}'");
        }
    }

    /// <summary>A project setting: its path, value (read-only here: write it with <c>project | set settings["…"] = …</c>) and type.</summary>
    public sealed class RqlSetting
    {
        private readonly Variant.Type _type;

        public RqlSetting(string name, Variant.Type type) { this.name = name; _type = type; }

        [RqlDescription("setting path, e.g. \"application/config/name\" (the section before the first '/')")]
        public string name { get; }

        [RqlDescription("first part of the path, e.g. \"application\"")]
        public string section => name.Substring(0, Math.Max(0, name.IndexOf('/')));

        [RqlDescription("current value; change it with project | set settings[\"<name>\"] = …")]
        public object? value => RqlProject.Get(name);

        [RqlDescription("Godot value type, e.g. \"Int\", \"String\", \"Vector2I\"")]
        public string type => _type.ToString();

        [RqlDescription("true when the value differs from the default (it is written to project.godot)")]
        public bool changed
        {
            get
            {
                var ps = RqlProject.Instance;
                return ps.PropertyCanRevert(name) && !RqlGodot.Same(ps.Get(name), ps.PropertyGetRevert(name));
            }
        }
    }

    /// <summary>An input action of the project: its deadzone and events (text, see <see cref="InputEventText"/>).</summary>
    public sealed class RqlInputAction : IRqlViewOf
    {
        public RqlInputAction(string name) { this.name = name; }

        GodotObject IRqlViewOf.Owner => RqlProject.Instance;

        [RqlDescription("action name, e.g. \"jump\" (ui_* are Godot's built-in actions)")]
        public string name { get; }

        [RqlDescription("true for Godot's built-in ui_* actions")]
        public bool builtIn => name.StartsWith("ui_", StringComparison.Ordinal);

        [RqlDescription("analog deadzone (0..1)")]
        public double deadzone
        {
            get => Action().TryGetValue("deadzone", out var d) ? d.AsDouble() : InputEvents.DefaultDeadzone;
            set
            {
                if (value < 0 || value > 1) throw new ArgumentException($"deadzone must be between 0 and 1, got {value.ToString(CultureInfo.InvariantCulture)}");
                var action = Action().Duplicate(true);
                action["deadzone"] = value;
                RqlProject.Set(RqlProject.InputPrefix + name, action);
            }
        }

        [RqlDescription("the events that trigger the action, e.g. [\"Key:W\", \"JoypadButton:A\"]; assign a list to replace them")]
        public List<string> events
        {
            get => Events().Select(InputEvents.Text).ToList();
            set => WriteEvents((value ?? new List<string>()).Select(InputEvents.Make));
        }

        [RqlCallable("add an input event, e.g. \"Key:Space\", \"Mouse:Left\", \"JoypadButton:A\", \"JoypadMotion:LeftX-\"; returns the events")]
        public List<string> AddEvent(string input)
        {
            var made = InputEvents.Make(input);
            var text = InputEvents.Text(made);
            var current = Events();
            if (current.Any(e => InputEventText.SameText(InputEvents.Text(e), text)))
                throw new ArgumentException($"input action '{name}' already has the event '{text}'");
            WriteEvents(current.Append(made));
            return events;
        }

        [RqlCallable("remove an input event by its text (as listed in events); returns the events")]
        public List<string> RemoveEvent(string input)
        {
            var current = Events();
            var key = InputEventText.TryParse(input, out _, out _) ? InputEvents.Text(InputEvents.Make(input)) : input;
            var kept = current.Where(e => !InputEventText.SameText(InputEvents.Text(e), key)).ToList();
            if (kept.Count == current.Count)
                throw new ArgumentException($"input action '{name}' has no event '{input}' (it has: {string.Join(", ", current.Select(InputEvents.Text))})");
            WriteEvents(kept);
            return events;
        }

        [RqlCallable("remove this input action from the project")]
        public void Remove() => RqlProject.RemoveInputAction(name);

        private global::Godot.Collections.Dictionary Action()
        {
            RqlProject.RequireInputAction(name);
            var v = ProjectSettings.GetSetting(RqlProject.InputPrefix + name);
            return v.VariantType == Variant.Type.Dictionary ? v.AsGodotDictionary() : new global::Godot.Collections.Dictionary();
        }

        private List<InputEvent> Events() =>
            Action().TryGetValue("events", out var list) && list.VariantType == Variant.Type.Array
                ? list.AsGodotArray().Select(e => e.As<InputEvent>()).Where(e => e != null).ToList()
                : new List<InputEvent>();

        private void WriteEvents(IEnumerable<InputEvent> events)
        {
            var action = Action().Duplicate(false);
            var array = new global::Godot.Collections.Array();
            foreach (var e in events) array.Add(e);
            action["events"] = array;
            if (!action.ContainsKey("deadzone")) action["deadzone"] = InputEvents.DefaultDeadzone;
            RqlProject.Set(RqlProject.InputPrefix + name, action);
        }
    }

    /// <summary>An autoload (singleton) of the project.</summary>
    public sealed class RqlAutoload : IRqlViewOf
    {
        public RqlAutoload(string name) { this.name = name; }

        GodotObject IRqlViewOf.Owner => RqlProject.Instance;

        [RqlDescription("autoload name (the global name of the singleton)")]
        public string name { get; }

        [RqlDescription("res:// path of the script or scene it loads")]
        public string path
        {
            get => Raw().TrimStart('*');
            set => RqlProject.Set(RqlProject.AutoloadPrefix + name, RqlProject.AutoloadValue(RqlProject.RequireAutoloadPath(value), singleton));
        }

        [RqlDescription("true when it is a global variable (the \"Global Variable\" checkbox)")]
        public bool singleton
        {
            get => Raw().StartsWith("*", StringComparison.Ordinal);
            set => RqlProject.Set(RqlProject.AutoloadPrefix + name, RqlProject.AutoloadValue(path, value));
        }

        [RqlDescription("load order (lower loads first)")]
        public int order => ProjectSettings.GetOrder(RqlProject.AutoloadPrefix + name);

        [RqlCallable("remove this autoload from the project")]
        public void Remove() => RqlProject.RemoveAutoload(name);

        private string Raw() => ProjectSettings.GetSetting(RqlProject.AutoloadPrefix + RqlProject.RequireAutoload(name)).AsString();
    }

    /// <summary>A node group: the nodes of the edited scene in it, and its global-group entry when it has one.</summary>
    public sealed class RqlGroup
    {
        public RqlGroup(string name, List<Node> nodes) { this.name = name; this.nodes = nodes; }

        [RqlDescription("group name")]
        public string name { get; }

        [RqlDescription("nodes of the edited scene in the group")]
        public List<Node> nodes { get; }

        [RqlDescription("number of nodes of the edited scene in the group")]
        public int count => nodes.Count;

        [RqlDescription("true for a global group (Project Settings → Global Groups)")]
        public bool global => ProjectSettings.HasSetting(RqlProject.GlobalGroupPrefix + name);

        [RqlDescription("description of a global group, or null")]
        public string? description => global ? ProjectSettings.GetSetting(RqlProject.GlobalGroupPrefix + name).AsString() : null;
    }
}
#endif
