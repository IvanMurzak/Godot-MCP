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
                (o, key) => ShaderUniforms.Of(((ShaderMaterial)o).Shader).TryGetValue(key, out var u) ? u.Value((ShaderMaterial)o) : null,
                (o, key, value) => ((ShaderMaterial)o).SetShaderParameter(key, GodotVariants.ToVariant(value)),
                (o, key, value) => Validate((ShaderMaterial)o, key, value),
                "the shader's uniforms by name, typed (e.g. params[\"speed\"] = 2, params[\"tint\"] = \"#ff8800\", a texture as res://…); null reverts to the default",
                // Every key reaches validate, which names the shader's uniforms.
                has: (o, key) => true);
            m.Untyped
                .AddMethod("ClearParam", new[] { new CallableParameter("name", typeof(string)) }, typeof(bool),
                    (o, a) => Clear((ShaderMaterial)o, Require((ShaderMaterial)o, (string)a[0]!)),
                    "revert one uniform to the shader's default; returns false when it already had the default")
                .AddMethod("ClearParams", Array.Empty<CallableParameter>(), typeof(int),
                    (o, a) => ShaderUniforms.Of(((ShaderMaterial)o).Shader).Values.Count(u => Clear((ShaderMaterial)o, u)),
                    "revert every uniform to the shader's default; returns how many had another value");
        }

        private static Type Nullable(Type t) => t.IsValueType && System.Nullable.GetUnderlyingType(t) == null ? typeof(Nullable<>).MakeGenericType(t) : t;

        // The uniform a key names, or why there is none.
        private static ShaderUniform? Find(ShaderMaterial mat, string key, out string? reason)
        {
            reason = null;
            if (mat.Shader == null)
            {
                reason = "the material has no shader: set Shader first (e.g. set Shader = \"res://….gdshader\")";
                return null;
            }
            var uniforms = ShaderUniforms.Of(mat.Shader);
            if (uniforms.TryGetValue(key, out var uniform)) return uniform;
            reason = uniforms.Count == 0
                ? $"the shader has no uniforms (no '{key}')"
                : $"the shader has no uniform '{key}' (its uniforms: {string.Join(", ", uniforms.Keys.Take(30))})" + RqlGodot.Similar(uniforms.Keys, key);
            return null;
        }

        private static ShaderUniform Require(ShaderMaterial mat, string key) => Find(mat, key, out var reason) ?? throw new ArgumentException(reason);

        private static string? Validate(ShaderMaterial mat, string key, object? value)
        {
            if (!(Find(mat, key, out var reason) is { } uniform)) return reason;
            if (value is Resource resource && uniform.Class is { Length: > 0 } classes
                && !classes.Split(',').Any(c => ClassDB.ClassExists(c.Trim()) && (resource.GetClass() == c.Trim() || ClassDB.IsParentClass(resource.GetClass(), c.Trim()))))
                return $"uniform '{key}' takes a {classes}, not a {resource.GetClass()}";
            return null;
        }

        private static bool Clear(ShaderMaterial mat, ShaderUniform uniform)
        {
            var overridden = uniform.Overridden(mat);
            mat.SetShaderParameter(uniform.Name, default(Variant));
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

    /// <summary>One uniform of a shader as <see cref="Shader.GetShaderUniformList"/> describes it, with its default.</summary>
    internal sealed class ShaderUniform
    {
        private readonly Shader _shader;
        private readonly Lazy<Dictionary<string, double[]>> _codeDefaults;
        private readonly Lazy<object?> _default;

        public ShaderUniform(Shader shader, Lazy<Dictionary<string, double[]>> codeDefaults, GodotVariants.GodotProperty info, string name, string group)
        {
            _shader = shader; _codeDefaults = codeDefaults; Info = info; Name = name; Group = group;
            _default = new Lazy<object?>(ReadDefault);
        }

        public string Name { get; }
        public GodotVariants.GodotProperty Info { get; }
        public string Group { get; }

        public Type ClrType => Info.ClrType;

        /// <summary>The resource class(es) an object uniform takes ("Texture2D"), or null.</summary>
        public string? Class => Info.Type == Variant.Type.Object && Info.Hint == PropertyHint.ResourceType && Info.HintString.Length > 0 ? Info.HintString : null;

        /// <summary>The shader's default: the rendering server's, else the literal in the code (the headless dummy renderer keeps none).</summary>
        public object? Default => _default.Value;

        private object? ReadDefault()
        {
            var value = RenderingServer.ShaderGetParameterDefault(_shader.GetRid(), Name);
            if (value.VariantType != Variant.Type.Nil) return GodotVariants.ToClr(value);
            return _codeDefaults.Value.TryGetValue(Name, out var parts) ? ShaderUniformDefaults.ToValue(Info.Type, parts) : null;
        }

        /// <summary>The value <paramref name="mat"/> uses: its own, else the default.</summary>
        public object? Value(ShaderMaterial mat)
        {
            var own = mat.GetShaderParameter(Name);
            return own.VariantType != Variant.Type.Nil ? GodotVariants.ToClr(own) : Default;
        }

        /// <summary>Whether the material holds a value of its own that differs from the default.</summary>
        public bool Overridden(ShaderMaterial mat)
        {
            var own = mat.GetShaderParameter(Name);
            if (own.VariantType == Variant.Type.Nil) return false;
            // A shader keeps float uniforms in 32 bits (the rendering server's default of 0.1 is 0.100000001…), the material
            // the double written to it: compare them at the shader's precision.
            if (own.VariantType == Variant.Type.Float && Default is double d) return (float)own.AsDouble() != (float)d;
            return !RqlGodot.Same(own, GodotVariants.ToVariant(Default));
        }
    }

    /// <summary>The uniforms of a shader, by name, in declaration order (cached for the running query).</summary>
    internal static class ShaderUniforms
    {
        private static readonly Dictionary<string, ShaderUniform> None = new Dictionary<string, ShaderUniform>();

        public static Dictionary<string, ShaderUniform> Of(Shader? shader) =>
            shader == null ? None : GodotVariants.Scoped(("uniforms", shader.GetInstanceId()), () => Read(shader));

        private static Dictionary<string, ShaderUniform> Read(Shader shader)
        {
            var uniforms = new Dictionary<string, ShaderUniform>(StringComparer.Ordinal);
            var codeDefaults = new Lazy<Dictionary<string, double[]>>(() => ShaderUniformDefaults.Parse(shader.Code));
            var group = "";
            foreach (var entry in shader.GetShaderUniformList(getGroups: true))
            {
                var info = entry.AsGodotDictionary();
                var name = info["name"].AsString();
                if (GodotVariants.IsHeader((PropertyUsageFlags)info["usage"].AsInt64()))
                {
                    // "group_uniforms a;" opens a group, "group_uniforms a.b;" a subgroup ("a::b"), "group_uniforms;" closes it.
                    group = name;
                    continue;
                }
                if (name.Length == 0 || uniforms.ContainsKey(name)) continue;
                var property = new GodotVariants.GodotProperty((Variant.Type)info["type"].AsInt64(), (PropertyHint)info["hint"].AsInt64(), info["hint_string"].AsString());
                uniforms[name] = new ShaderUniform(shader, codeDefaults, property, name, group);
            }
            return uniforms;
        }
    }

    /// <summary>A uniform row of <c>uniforms</c> (a Shader's, or a ShaderMaterial's with its value).</summary>
    public sealed class RqlShaderUniform
    {
        private readonly ShaderMaterial? _material;
        private readonly ShaderUniform _uniform;

        private RqlShaderUniform(ShaderMaterial? material, ShaderUniform uniform) { _material = material; _uniform = uniform; }

        internal static List<RqlShaderUniform> Of(Shader? shader, ShaderMaterial? material) =>
            ShaderUniforms.Of(shader).Values.Select(u => new RqlShaderUniform(material, u)).ToList();

        [RqlDescription("uniform name (the key of a ShaderMaterial's params)")]
        public string name => _uniform.Name;

        [RqlDescription("Godot value type (\"Float\", \"Color\", \"Vector3\" …), or the resource class of a sampler (\"Texture2D\")")]
        public string type => _uniform.Class ?? _uniform.Info.Type.ToString();

        [RqlDescription("group_uniforms group it belongs to (\"\" when none)")]
        public string group => _uniform.Group;

        [RqlDescription("the shader's default value (null when the shader computes it, or for a sampler)")]
        public object? @default => _uniform.Default;

        [RqlDescription("the value the material uses: its own, else the default (null for a Shader row)")]
        public object? value => _material == null ? null : _uniform.Value(_material);

        [RqlDescription("true when the material holds a value other than the default (false for a Shader row)")]
        public bool overridden => _material != null && _uniform.Overridden(_material);
    }
}
#endif
