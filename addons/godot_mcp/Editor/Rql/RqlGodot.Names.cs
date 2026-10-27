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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters;
using com.IvanMurzak.Godot.MCP.Reflection;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    public static partial class RqlGodot
    {
        private static HashSet<string>? _knownMembers;

        /// <summary>
        /// <c>RqlEngine.IsKnownMember</c>: whether some Godot object type declares a member of this name. Node and resource
        /// streams are polymorphic (<c>nodes</c> is typed Node), so a member of MeshInstance3D or of a C# script class is not
        /// statically reachable; without this a <c>nodes | where … | set Mesh = …</c> that matched no mesh would be reported as
        /// a typo. Known: the C# members of every Godot class (the C# binding of ClassDB's properties: light_energy is
        /// LightEnergy), of the project's Godot object types (C# scripts, extensions) and the adapters' members (props, class,
        /// path …). Godot's snake_case names stay unknown as members: they are props["…"] keys, so <c>set light_energy = 2</c>
        /// is still the typo it is. Built on first use, dropped with the engine.
        /// </summary>
        internal static bool IsKnownMember(RqlEngine engine, string name)
        {
            var known = _knownMembers;
            if (known == null)
            {
                known = RqlHostNames.MemberNames(GodotTypes.GodotObjectTypes());
                // Adapter members (props, params, colors …) come from the engine's type model: only the adapted types are
                // built there (building every Godot class would compile thousands of getters).
                var adapterTypes = GodotAssemblyUtils.AllAssemblies
                    .Where(a => !GodotAssemblyUtils.IsSkippedForDiscovery(a, typeof(RqlGodot).Assembly))
                    .SelectMany(a => TypeModel.LoadableTypes(a));
                foreach (var target in RqlHostNames.AdapterTargets(adapterTypes).Distinct())
                    foreach (var member in engine.Types.Info(target).Members) known.Add(member.Name);
                _knownMembers = known;
            }
            return known.Contains(name);
        }

        /// <summary>
        /// <c>RqlEngine.EnumAliases</c>: the names Godot shows for the entries of a C# class enum — its ClassDB constants
        /// (<c>PRESET_FULL_RECT</c> for <c>Control.LayoutPreset.FullRect</c>, what the docs and GDScript use) and the inspector
        /// labels of the class's properties of that enum (<c>"Per-Pixel"</c> for <c>BaseMaterial3D.ShadingModeEnum.PerPixel</c>).
        /// Global enums (Key, Error …) have no ClassDB entry and keep their C# names.
        /// </summary>
        internal static IReadOnlyDictionary<string, string>? EnumAliases(Type enumType)
        {
            if (enumType.Namespace != "Godot" || enumType.DeclaringType is not { } owner) return null;
            var classes = GodotTypes.GodotClasses();
            var godotClass = classes.FirstOrDefault(c => string.Equals(c, owner.Name, StringComparison.OrdinalIgnoreCase));
            if (godotClass == null) return null;
            // The C# binding appends "Enum" where the enum's name is also a property's (BaseMaterial3D.ShadingModeEnum).
            var enumName = ClassDB.ClassHasEnum(godotClass, enumType.Name, noInheritance: true) ? enumType.Name
                : enumType.Name.EndsWith("Enum", StringComparison.Ordinal) && ClassDB.ClassHasEnum(godotClass, enumType.Name.Substring(0, enumType.Name.Length - 4), noInheritance: true)
                    ? enumType.Name.Substring(0, enumType.Name.Length - 4) : null;
            if (enumName == null) return null;
            var names = new List<(string, long)>();
            foreach (var constant in ClassDB.ClassGetEnumConstants(godotClass, enumName, noInheritance: true))
                names.Add((constant, ClassDB.ClassGetIntegerConstant(godotClass, constant)));
            var qualified = godotClass + "." + enumName;
            foreach (var info in ClassDB.ClassGetPropertyList(godotClass, noInheritance: true))
                if ((PropertyHint)info["hint"].AsInt64() == PropertyHint.Enum && info["class_name"].AsString() == qualified)
                    names.AddRange(GodotEnumNames.ParseHint(info["hint_string"].AsString()));
            return RqlHostNames.EnumAliases(enumType, names);
        }
    }
}
#endif
