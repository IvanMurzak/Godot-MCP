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
using System.Collections.Generic;
using com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters;
using Godot;
using Rql;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>A plain object with the struct kinds an agent computes with, plus a property map.</summary>
    public sealed class RqlArithmeticHolder
    {
        public string name { get; set; } = "";
        public Vector2 Offset { get; set; }
        public Vector3 Scale { get; set; } = Vector3.One;
        public Vector3 Position { get; set; }
        public Vector3I Cell { get; set; }
        public Vector2I Size { get; set; }
        public Vector4 Tint4 { get; set; }
        public Color Tint { get; set; } = new Color(0.5f, 0.25f, 1f, 1f);
        public Quaternion Rotation { get; set; } = Quaternion.Identity;
        public Basis Basis { get; set; } = Basis.Identity;
        public Transform3D Transform { get; set; } = Transform3D.Identity;
        public Dictionary<string, object?> props { get; set; } = new Dictionary<string, object?>();
    }

    /// <summary>
    /// RQL 0.3 semantics the Godot adapters opt in to or inherit: component-wise arithmetic on vectors and colors (never on
    /// quaternions, bases or transforms), struct keys in distinct rounded like their display, absent map keys reading null.
    /// </summary>
    public class RqlVectorArithmeticTests
    {
        private readonly List<RqlArithmeticHolder> _items = new List<RqlArithmeticHolder>
        {
            new RqlArithmeticHolder { name = "a", Scale = new Vector3(1, 2, 3), Position = new Vector3(1, 2, 3), Cell = new Vector3I(1, 2, 3), Size = new Vector2I(4, 6),
                                      Offset = new Vector2(2, 4), Tint4 = new Vector4(1, 2, 3, 4), props = { ["speed"] = 2.0 } },
            new RqlArithmeticHolder { name = "b", Scale = new Vector3(0.99999994f, 1, 1) },
            new RqlArithmeticHolder { name = "c", Scale = new Vector3(1, 1, 1) },
        };

        private RqlEngine Engine()
        {
            var engine = new RqlEngine();
            engine.Mutations.Enabled = true;
            engine.AddSource("items", () => _items);
            foreach (var adapter in new RqlTypeAdapter[]
                     {
                         new Godot_Vector2_Adapter(), new Godot_Vector2I_Adapter(), new Godot_Vector3_Adapter(), new Godot_Vector3I_Adapter(),
                         new Godot_Vector4_Adapter(), new Godot_Vector4I_Adapter(), new Godot_Color_Adapter(), new Godot_Quaternion_Adapter(),
                         new Godot_Basis_Adapter(), new Godot_Transform3D_Adapter(),
                     })
                engine.AddAdapter(adapter);
            return engine;
        }

        private QueryResult Query(string q) => Engine().Execute(q);

        private QueryResult Mutate(string q) => Engine().Execute(q, null, new QueryOptions { AllowMutation = true });

        private static string Text(QueryResult r) => AgentFormat.Render(r, OutputFormat.Table);

        private RqlArithmeticHolder A => _items[0];

        [Fact]
        public void ScaleTimesANumberScalesEveryComponent()
        {
            var r = Mutate("items | where name == \"a\" | set Scale = Scale * 1.5");
            Assert.True(r.Ok, Text(r));
            Assert.Equal(new Vector3(1.5f, 3f, 4.5f), A.Scale);
        }

        [Theory]
        [InlineData("set Position = Position + [0, 1, 0]", 1, 3, 3)]
        [InlineData("set Position = Position - Scale", 0, 0, 0)]
        [InlineData("set Position = -Position", -1, -2, -3)]
        [InlineData("set Position = Position / 2", 0.5f, 1, 1.5f)]
        [InlineData("set Position = Position * Scale", 1, 4, 9)]
        public void Vector3ComputesComponentWise(string set, float x, float y, float z)
        {
            var r = Mutate("items | where name == \"a\" | " + set);
            Assert.True(r.Ok, Text(r));
            Assert.Equal(new Vector3(x, y, z), A.Position);
        }

        [Fact]
        public void Vector2Vector4AndColorComputeComponentWise()
        {
            var r = Mutate("items | where name == \"a\" | set Offset = Offset / 2, Tint4 = Tint4 * 2, Tint = Tint * 0.5");
            Assert.True(r.Ok, Text(r));
            Assert.Equal(new Vector2(1, 2), A.Offset);
            Assert.Equal(new Vector4(2, 4, 6, 8), A.Tint4);
            Assert.Equal(new Color(0.25f, 0.125f, 0.5f, 0.5f), A.Tint);
        }

        [Fact]
        public void IntegerVectorsComputeWhileTheResultIsWhole()
        {
            var r = Mutate("items | where name == \"a\" | set Cell = Cell + [1, 0, 0], Size = Size / 2");
            Assert.True(r.Ok, Text(r));
            Assert.Equal(new Vector3I(2, 2, 3), A.Cell);
            Assert.Equal(new Vector2I(2, 3), A.Size);

            var fraction = Mutate("items | where name == \"a\" | set Cell = Cell * 1.5");
            Assert.False(fraction.Ok);
            Assert.Equal(new Vector3I(2, 2, 3), A.Cell);
        }

        [Theory]
        [InlineData("set Rotation = Rotation * 2")]
        [InlineData("set Basis = Basis * 2")]
        [InlineData("set Transform = Transform * 2")]
        [InlineData("set Position = Position + 1")]
        [InlineData("set Position = Position + Offset")]
        public void QuaternionsBasesTransformsAndMixedOperandsAreRefused(string set)
        {
            var before = (A.Rotation, A.Basis, A.Transform, A.Position);
            var r = Mutate("items | where name == \"a\" | " + set);
            Assert.False(r.Ok);
            Assert.Contains("cannot apply", Text(r));
            Assert.Equal(before, (A.Rotation, A.Basis, A.Transform, A.Position));
        }

        [Fact]
        public void DistinctMergesScalesThatPrintAlike()
        {
            var r = Query("items | where name != \"a\" | select Scale | distinct");
            Assert.True(r.Ok, Text(r));
            Assert.Single(r.Rows!);
        }

        [Fact]
        public void AnAbsentMapKeyReadsNull()
        {
            var r = Query("items | where props[\"speed\"] == null | select name");
            Assert.True(r.Ok, Text(r));
            Assert.Equal(2, r.Rows!.Count);
            Assert.Single(Query("items | where has(props[\"speed\"]) | select name").Rows!);
        }
    }
}
