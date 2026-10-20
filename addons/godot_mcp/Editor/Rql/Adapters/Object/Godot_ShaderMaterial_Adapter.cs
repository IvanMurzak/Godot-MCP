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
using Godot;
using Rql;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// ShaderMaterial: the shader's uniforms (<c>uniforms</c>: name, type, group, default, value, overridden) and
    /// <c>params["uniform"]</c> — every uniform of the shader (text or VisualShader alike) by name, typed by the uniform,
    /// validated against the shader's uniform list; assigning null reverts one to its default. Writes go through
    /// <see cref="ShaderMaterial.SetShaderParameter"/>, i.e. the stored <c>shader_parameter/…</c> properties, so the
    /// mutation's property snapshot makes them undoable and a material file is saved.
    /// </summary>
    public sealed class Godot_ShaderMaterial_Adapter : RqlTypeAdapter<ShaderMaterial>
    {
        public override string Description =>
            "ShaderMaterial: Shader, uniforms (name type group default value overridden), params[\"uniform\"] typed by the shader's uniforms " +
            "(null reverts one to its default); calls ClearParam(name), ClearParams()";

        protected override void Members(MemberTable<ShaderMaterial> m)
        {
            m.Add<List<RqlShaderUniform>>("uniforms", mat => RqlShaderUniform.Of(mat.Shader, mat),
                description: "the shader's uniforms: name type group default value overridden");
            m.Untyped.AddMixedMap("params",
                o => ShaderUniforms.Of(((ShaderMaterial)o).Shader).Keys,
                // Nullable: assigning null reverts a uniform to its default.
                (o, key) => ShaderUniforms.Of(((ShaderMaterial)o).Shader).TryGetValue(key, out var u) ? Nullable(u.ClrType) : typeof(object),
                (o, key) => Value((ShaderMaterial)o, key),
                (o, key, value) => ((ShaderMaterial)o).SetShaderParameter(key, GodotVariants.ToVariant(value)),
                (o, key, value) => Validate((ShaderMaterial)o, key, value),
                "the shader's uniforms by name, typed (e.g. params[\"speed\"] = 2, params[\"tint\"] = \"#ff8800\", a texture as res://…); null reverts to the default",
                // Every key reaches validate, which names the shader's uniforms.
                has: (o, key) => true);
            m.Untyped
                .AddMethod("ClearParam", new[] { new CallableParameter("name", typeof(string)) }, typeof(bool),
                    (o, a) => ClearParam((ShaderMaterial)o, (string)a[0]!),
                    "revert one uniform to the shader's default; returns false when it already had the default")
                .AddMethod("ClearParams", Array.Empty<CallableParameter>(), typeof(int),
                    (o, a) => ShaderUniforms.Of(((ShaderMaterial)o).Shader).Keys.Count(k => ClearParam((ShaderMaterial)o, k)),
                    "revert every uniform to the shader's default; returns how many had another value");
        }

        private static Type Nullable(Type t) => t.IsValueType && System.Nullable.GetUnderlyingType(t) == null ? typeof(Nullable<>).MakeGenericType(t) : t;

        // The value the material uses: its own, else the shader's default.
        private static object? Value(ShaderMaterial mat, string key)
        {
            if (!ShaderUniforms.Of(mat.Shader).TryGetValue(key, out var uniform)) return null;
            var own = mat.GetShaderParameter(key);
            return own.VariantType != Variant.Type.Nil ? GodotVariants.ToClr(own) : uniform.Default(mat.Shader!);
        }

        private static string? Validate(ShaderMaterial mat, string key, object? value)
        {
            if (mat.Shader == null) return "the material has no shader: set Shader first (e.g. set Shader = \"res://….gdshader\")";
            var uniforms = ShaderUniforms.Of(mat.Shader);
            if (!uniforms.TryGetValue(key, out var uniform))
                return uniforms.Count == 0
                    ? $"the shader has no uniforms (no '{key}')"
                    : $"the shader has no uniform '{key}' (its uniforms: {string.Join(", ", uniforms.Keys.Take(30))})" + RqlGodot.Similar(uniforms.Keys, key);
            if (value is Resource resource && uniform.Class is { Length: > 0 } classes
                && !classes.Split(',').Any(c => ClassDB.ClassExists(c.Trim()) && (resource.GetClass() == c.Trim() || ClassDB.IsParentClass(resource.GetClass(), c.Trim()))))
                return $"uniform '{key}' takes a {classes}, not a {resource.GetClass()}";
            return null;
        }

        private static bool ClearParam(ShaderMaterial mat, string name)
        {
            if (mat.Shader == null) throw new InvalidOperationException("the material has no shader");
            var uniforms = ShaderUniforms.Of(mat.Shader);
            if (!uniforms.TryGetValue(name, out var uniform))
                throw new ArgumentException($"the shader has no uniform '{name}'" + RqlGodot.Similar(uniforms.Keys, name));
            var overridden = uniform.Overridden(mat);
            mat.SetShaderParameter(name, default(Variant));
            return overridden;
        }
    }

    /// <summary>Shader: its uniforms (name type group default), for a text shader and a VisualShader alike.</summary>
    public sealed class Godot_Shader_Adapter : RqlTypeAdapter<Shader>
    {
        public override string Description => "Shader: Code, uniforms (name type group default); a ShaderMaterial sets them in params[\"…\"]";

        protected override void Members(MemberTable<Shader> m) =>
            m.Add<List<RqlShaderUniform>>("uniforms", s => RqlShaderUniform.Of(s, null), description: "the shader's uniforms: name type group default");
    }

    /// <summary>One uniform of a shader as <see cref="Shader.GetShaderUniformList"/> describes it.</summary>
    internal sealed class ShaderUniform
    {
        public ShaderUniform(string name, Variant.Type type, PropertyHint hint, string hintString, string group)
        {
            Name = name; Type = type; Hint = hint; HintString = hintString; Group = group;
        }

        public string Name { get; }
        public Variant.Type Type { get; }
        public PropertyHint Hint { get; }
        public string HintString { get; }
        public string Group { get; }

        public Type ClrType => GodotVariants.ClrType(Type, Hint, HintString);

        /// <summary>The resource class(es) an object uniform takes ("Texture2D"), or null.</summary>
        public string? Class => Type == Variant.Type.Object && Hint == PropertyHint.ResourceType && HintString.Length > 0 ? HintString : null;

        /// <summary>The shader's default: the rendering server's, else the literal in the code (the headless dummy renderer keeps none).</summary>
        public object? Default(Shader shader)
        {
            var value = RenderingServer.ShaderGetParameterDefault(shader.GetRid(), Name);
            if (value.VariantType != Variant.Type.Nil) return GodotVariants.ToClr(value);
            return ShaderUniformDefaults.Parse(shader.Code).TryGetValue(Name, out var parts) ? ShaderUniformDefaults.ToValue(Type, parts) : null;
        }

        /// <summary>Whether the material holds a value of its own that differs from the default.</summary>
        public bool Overridden(ShaderMaterial mat)
        {
            var own = mat.GetShaderParameter(Name);
            return own.VariantType != Variant.Type.Nil && !RqlGodot.Same(own, GodotVariants.ToVariant(Default(mat.Shader!)));
        }
    }

    /// <summary>The uniforms of a shader, by name, in declaration order.</summary>
    internal static class ShaderUniforms
    {
        private const long GroupUsage = (long)(PropertyUsageFlags.Group | PropertyUsageFlags.Subgroup | PropertyUsageFlags.Category);

        public static Dictionary<string, ShaderUniform> Of(Shader? shader)
        {
            var uniforms = new Dictionary<string, ShaderUniform>(StringComparer.Ordinal);
            if (shader == null) return uniforms;
            var group = "";
            foreach (var entry in shader.GetShaderUniformList(getGroups: true))
            {
                var info = entry.AsGodotDictionary();
                var name = info["name"].AsString();
                if ((info["usage"].AsInt64() & GroupUsage) != 0)
                {
                    // "group_uniforms a;" opens a group, "group_uniforms a.b;" a subgroup ("a::b"), "group_uniforms;" closes it.
                    group = name;
                    continue;
                }
                if (name.Length == 0 || uniforms.ContainsKey(name)) continue;
                uniforms[name] = new ShaderUniform(name, (Variant.Type)info["type"].AsInt64(), (PropertyHint)info["hint"].AsInt64(),
                    info["hint_string"].AsString(), group);
            }
            return uniforms;
        }
    }

    /// <summary>A uniform row of <c>uniforms</c> (a Shader's, or a ShaderMaterial's with its value).</summary>
    public sealed class RqlShaderUniform
    {
        private readonly Shader _shader;
        private readonly ShaderMaterial? _material;
        private readonly ShaderUniform _uniform;

        private RqlShaderUniform(Shader shader, ShaderMaterial? material, ShaderUniform uniform)
        {
            _shader = shader; _material = material; _uniform = uniform;
        }

        internal static List<RqlShaderUniform> Of(Shader? shader, ShaderMaterial? material) =>
            shader == null ? new List<RqlShaderUniform>() : ShaderUniforms.Of(shader).Values.Select(u => new RqlShaderUniform(shader, material, u)).ToList();

        [RqlDescription("uniform name (the key of a ShaderMaterial's params)")]
        public string name => _uniform.Name;

        [RqlDescription("Godot value type (\"Float\", \"Color\", \"Vector3\" …), or the resource class of a sampler (\"Texture2D\")")]
        public string type => _uniform.Class ?? _uniform.Type.ToString();

        [RqlDescription("group_uniforms group it belongs to (\"\" when none)")]
        public string group => _uniform.Group;

        [RqlDescription("the shader's default value (null when the shader computes it, or for a sampler)")]
        public object? @default => _uniform.Default(_shader);

        [RqlDescription("the value the material uses: its own, else the default (null for a Shader row)")]
        public object? value
        {
            get
            {
                if (_material == null) return null;
                var own = _material.GetShaderParameter(_uniform.Name);
                return own.VariantType != Variant.Type.Nil ? GodotVariants.ToClr(own) : @default;
            }
        }

        [RqlDescription("true when the material holds a value other than the default (false for a Shader row)")]
        public bool overridden => _material != null && _uniform.Overridden(_material);
    }
}
#endif
