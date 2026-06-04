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
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using com.IvanMurzak.ReflectorNet.Json;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    /// <summary>
    /// System.Text.Json converter for Godot's <see cref="global::Godot.NodePath"/>. Unlike the
    /// Vector/Color value types, <c>NodePath</c> is an opaque class with no public settable members,
    /// so the reflection serializer cannot round-trip it field-by-field. It is fully described by its
    /// string form (e.g. <c>"Main/Player:position:x"</c>), so it is (de)serialized as a JSON string —
    /// the Godot analog of how Unity-MCP ships dedicated System.Text.Json converters for its structs.
    /// </summary>
    public class GodotNodePathJsonConverter : JsonSchemaConverter<global::Godot.NodePath>
    {
        public override JsonNode GetSchema() => new JsonObject
        {
            [JsonSchema.Type] = JsonSchema.String
        };

        public override JsonNode GetSchemaRef() => new JsonObject
        {
            [JsonSchema.Ref] = JsonSchema.RefValue + Id
        };

        public override global::Godot.NodePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // A JSON null deserializes to the empty NodePath (the canonical "no path" form) rather
            // than a C# null: NodePath is a Godot native type with no meaningful null state in the
            // resolver, so callers test emptiness via NodePath.IsEmpty rather than a null check.
            // This is deliberate — null and "" round-trip to the same empty NodePath.
            if (reader.TokenType == JsonTokenType.Null)
                return new global::Godot.NodePath();

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"Expected a string for NodePath, got {reader.TokenType}.");

            return new global::Godot.NodePath(reader.GetString() ?? string.Empty);
        }

        public override void Write(Utf8JsonWriter writer, global::Godot.NodePath value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value?.ToString() ?? string.Empty);
        }
    }
}
