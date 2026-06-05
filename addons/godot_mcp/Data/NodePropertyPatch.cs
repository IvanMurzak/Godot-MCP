/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.ComponentModel;
using System.Text.Json.Serialization;
using com.IvanMurzak.ReflectorNet.Model;

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// A single path-scoped property patch consumed by <c>node-modify</c> and routed through
    /// ReflectorNet's <c>Reflector.TryModifyAt(ref obj, path, SerializedMember value, ...)</c>. The Godot
    /// analog of Unity-MCP's per-GameObject path-patch entry, narrowed to a plain <c>{path, value}</c>
    /// pair where <see cref="Value"/> is a ReflectorNet <see cref="SerializedMember"/> describing the new
    /// value at <see cref="Path"/>.
    ///
    /// Pure data (no Godot API), so it (de)serializes off the main thread and is unit-testable in the
    /// plain xUnit host.
    /// </summary>
    [System.Serializable]
    [Description("A path-scoped property patch: 'path' locates the member, 'value' carries the new value " +
        "as a ReflectorNet SerializedMember. Path syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'.")]
    public class NodePropertyPatch
    {
        [JsonInclude, JsonPropertyName("path")]
        [Description("Path to the member to modify. Syntax: 'fieldName', 'nested/field', 'arrayField/[i]', 'dictField/[key]'. Leading '#/' is stripped.")]
        public string Path { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("value")]
        [Description("New value for the member, as a ReflectorNet SerializedMember.")]
        public SerializedMember? Value { get; set; } = null;

        public NodePropertyPatch() { }

        /// <summary>
        /// True when <paramref name="value"/> carries nothing the modifier could apply — no
        /// <c>value</c> JSON, no <c>fields</c>, and no <c>props</c> (e.g. a <c>{typeName}</c>-only entry).
        /// Such a patch is a guaranteed no-op: it would silently change nothing. <c>node-modify</c> uses this
        /// to emit a WARNING naming the path rather than letting the no-op pass unremarked. Pure-managed (no
        /// Godot API), so it is unit-testable. A <c>null</c> value also counts as a no-op.
        /// </summary>
        public static bool IsStructuralNoOp(SerializedMember? value)
        {
            if (value == null)
                return true;

            var hasValue = value.valueJsonElement.HasValue;
            var hasFields = value.fields != null && value.fields.Count > 0;
            var hasProps = value.props != null && value.props.Count > 0;

            return !hasValue && !hasFields && !hasProps;
        }
    }
}
