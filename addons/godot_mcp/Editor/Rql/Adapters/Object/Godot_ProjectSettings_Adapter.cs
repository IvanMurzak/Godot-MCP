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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Editor.Rql.Settings;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// The ProjectSettings singleton (the <c>project</c> source): <c>settings["section/key"]</c> reads and writes any project
    /// setting typed like the Project Settings dialog does, <c>name</c> / <c>mainScene</c> for the two most used ones, and
    /// calls for the input map, autoloads and global groups. Changes are one undo action and are saved to project.godot.
    /// </summary>
    public sealed class Godot_ProjectSettings_Adapter : RqlTypeAdapter<ProjectSettingsInstance>
    {
        public override string Description =>
            "Project settings (project.godot): settings[\"section/key\"], name, mainScene; calls SetSetting, ClearSetting, " +
            "AddInputAction, RemoveInputAction, AddInputEvent, RemoveInputEvent, AddAutoload, RemoveAutoload, AddGlobalGroup, RemoveGlobalGroup";

        private const string NameSetting = "application/config/name";
        private const string MainSceneSetting = "application/run/main_scene";

        protected override void Members(MemberTable<ProjectSettingsInstance> m)
        {
            m.Add<string>("name", _ => ProjectSettings.GetSetting(NameSetting).AsString(), (_, v) => RqlProject.Set(NameSetting, v),
                    validate: (_, v) => string.IsNullOrWhiteSpace(v) ? "the project name cannot be empty" : null,
                    description: "project name (application/config/name)")
             .Add<string?>("mainScene", _ => ProjectSettings.GetSetting(MainSceneSetting).AsString() is { Length: > 0 } s ? s : null,
                    (_, v) => RqlProject.Set(MainSceneSetting, v ?? ""), validate: (_, v) => ValidateScene(v),
                    description: "res:// path of the scene the game starts with (application/run/main_scene)");
            m.Untyped.AddMixedMap("settings",
                o => GodotVariants.Properties((GodotObject)o).Keys,
                (o, key) => GodotVariants.Properties((GodotObject)o).TryGetValue(key, out var info) ? info.ClrType : typeof(object),
                (o, key) => RqlProject.Get(key),
                (o, key, value) => RqlProject.Set(key, GodotVariants.ToVariant(value)),
                (o, key, value) => GodotVariants.Properties((GodotObject)o).ContainsKey(key)
                    ? null
                    : $"no project setting '{key}'; create a custom one with project | call SetSetting(\"{key}\", value)",
                "every project setting by its path (\"display/window/size/viewport_width\"), typed; list them with the settings source",
                // Every key passes to validate, which names SetSetting for a setting that does not exist yet.
                has: (o, key) => true);
            m.Display("name");

            var u = m.Untyped;
            u.AddMethod("SetSetting", new[] { P("name", typeof(string)), P("value", typeof(object)) }, typeof(object),
                (o, a) => SetSetting((string)a[0]!, a[1]),
                "create a custom setting, or set one to a value of its own type; returns the value");
            u.AddMethod("ClearSetting", new[] { P("name", typeof(string)) }, typeof(void),
                (o, a) => { ClearSetting((string)a[0]!); return null; },
                "remove a custom setting, or return a built-in one to its default");
            u.AddMethod("AddInputAction",
                new[] { P("name", typeof(string)), P("events", typeof(string[]), null), P("deadzone", typeof(double), double.NaN) },
                typeof(RqlInputAction),
                (o, a) => RqlProject.AddInputAction((string)a[0]!, a[1] as string[], a[2] is double d && !double.IsNaN(d) ? d : (double?)null),
                "add an input action with optional events (e.g. [\"Key:Space\", \"JoypadButton:A\"]) and deadzone; returns it");
            u.AddMethod("RemoveInputAction", new[] { P("name", typeof(string)) }, typeof(void),
                (o, a) => { RqlProject.RemoveInputAction((string)a[0]!); return null; }, "remove an input action");
            u.AddMethod("AddInputEvent", new[] { P("action", typeof(string)), P("event", typeof(string)) }, typeof(object),
                (o, a) => new RqlInputAction(RqlProject.RequireInputAction((string)a[0]!)).AddEvent((string)a[1]!),
                "add an input event (\"Key:W\", \"Mouse:Left\", \"JoypadButton:A\", \"JoypadMotion:LeftX-\") to an action; returns its events");
            u.AddMethod("RemoveInputEvent", new[] { P("action", typeof(string)), P("event", typeof(string)) }, typeof(object),
                (o, a) => new RqlInputAction(RqlProject.RequireInputAction((string)a[0]!)).RemoveEvent((string)a[1]!),
                "remove an input event from an action by its text; returns its events");
            u.AddMethod("AddAutoload", new[] { P("name", typeof(string)), P("path", typeof(string)), P("singleton", typeof(bool), true) },
                typeof(RqlAutoload), (o, a) => RqlProject.AddAutoload((string)a[0]!, (string)a[1]!, a[2] is bool b ? b : true),
                "add an autoload of a res:// script or scene (singleton = a global variable of that name); returns it");
            u.AddMethod("RemoveAutoload", new[] { P("name", typeof(string)) }, typeof(void),
                (o, a) => { RqlProject.RemoveAutoload((string)a[0]!); return null; }, "remove an autoload");
            u.AddMethod("AddGlobalGroup", new[] { P("name", typeof(string)), P("description", typeof(string), "") }, typeof(void),
                (o, a) => { RqlProject.AddGlobalGroup((string)a[0]!, a[1] as string); return null; },
                "add a global group (Project Settings → Global Groups)");
            u.AddMethod("RemoveGlobalGroup", new[] { P("name", typeof(string)) }, typeof(void),
                (o, a) => { RqlProject.RemoveGlobalGroup((string)a[0]!); return null; }, "remove a global group");
        }

        private static CallableParameter P(string name, Type type) => new CallableParameter(name, type);

        private static CallableParameter P(string name, Type type, object? defaultValue) =>
            new CallableParameter(name, type, hasDefault: true, defaultValue: defaultValue);

        private static string? ValidateScene(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!path.StartsWith("res://", StringComparison.Ordinal) && !path.StartsWith("uid://", StringComparison.Ordinal))
                return $"'{path}' is not a res:// path to a scene";
            return ResourceLoader.Exists(path) ? null : $"no scene at '{path}'";
        }

        private static object? SetSetting(string name, object? value)
        {
            var variant = GodotVariants.ToVariant(value);
            // RQL numbers are doubles: a whole number creates an int setting (written 3, not 3.0).
            if (variant.VariantType == Variant.Type.Float && !ProjectSettings.HasSetting(name) && variant.AsDouble() % 1 == 0
                && Math.Abs(variant.AsDouble()) < 9e15)
                variant = (long)variant.AsDouble();
            if (ProjectSettings.HasSetting(name))
            {
                var current = ProjectSettings.GetSetting(name);
                if (current.VariantType != Variant.Type.Nil && variant.VariantType != current.VariantType && !Numeric(current, variant))
                    throw new ArgumentException($"'{name}' holds a {current.VariantType}, not a {variant.VariantType}: set it with " +
                                                $"project | set settings[\"{name}\"] = … (the value is converted to its type)");
                if (Numeric(current, variant))
                    variant = current.VariantType == Variant.Type.Int ? (Variant)(long)variant.AsDouble() : variant.AsDouble();
            }
            RqlProject.Set(name, variant);
            return RqlProject.Get(name);
        }

        private static bool Numeric(Variant a, Variant b) =>
            (a.VariantType is Variant.Type.Int or Variant.Type.Float) && (b.VariantType is Variant.Type.Int or Variant.Type.Float)
            && !(a.VariantType == Variant.Type.Int && b.VariantType == Variant.Type.Float && b.AsDouble() % 1 != 0);

        private static void ClearSetting(string name)
        {
            if (!ProjectSettings.HasSetting(name)) throw new ArgumentException($"no project setting '{name}'");
            var ps = RqlProject.Instance;
            // A built-in setting goes back to its default; a custom one is removed.
            RqlProject.Set(name, ps.PropertyCanRevert(name) ? ps.PropertyGetRevert(name) : default);
        }
    }
}
#endif
