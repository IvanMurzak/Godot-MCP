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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters;
using Godot;
using Rql;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>A plain object holding Godot structs (all pure C# in GodotSharp, so they run in this host).</summary>
    public sealed class RqlStructHolder
    {
        public string name { get; set; } = "";
        public Vector2 Offset { get; set; }
        public Vector3 Position { get; set; }
        public Vector3I Cell { get; set; }
        public Vector4 Tint4 { get; set; }
        public Color Tint { get; set; }
        public Quaternion Rotation { get; set; } = Quaternion.Identity;
        public Basis Basis { get; set; } = Basis.Identity;
        public Transform3D Transform { get; set; } = Transform3D.Identity;
        public Transform2D Transform2 { get; set; } = Transform2D.Identity;
        public Rect2 Rect { get; set; }
        public Rect2I RectI { get; set; }
        public Aabb Bounds { get; set; }
        public Plane Floor { get; set; }
        public Projection Lens { get; set; } = Projection.Identity;
        public Rid Handle { get; set; }
    }

    /// <summary>
    /// The numeric-struct adapters (Godot_*_Adapter): compact "(…)" output, text / list / object-literal input, whole-number
    /// checks for the integer vectors, "#rrggbb" colors, approximate equality in where.
    /// </summary>
    public class RqlStructAdapterTests
    {
        private readonly List<RqlStructHolder> _items = new List<RqlStructHolder>
        {
            new RqlStructHolder { name = "a", Position = new Vector3(1, 2, 3), Cell = new Vector3I(1, 2, 3), Tint = new Color(1, 0, 0, 1) },
            new RqlStructHolder { name = "b", Position = new Vector3(0, 0.5f, 0) },
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
                         new Godot_Basis_Adapter(), new Godot_Transform2D_Adapter(), new Godot_Transform3D_Adapter(),
                         new Godot_Rect2_Adapter(), new Godot_Rect2I_Adapter(), new Godot_Aabb_Adapter(), new Godot_Plane_Adapter(),
                         new Godot_Projection_Adapter(), new Godot_Rid_Adapter(),
                     })
                engine.AddAdapter(adapter);
            return engine;
        }

        private QueryResult Query(string q) => Engine().Execute(q);

        private QueryResult Mutate(string q) => Engine().Execute(q, null, new QueryOptions { AllowMutation = true });

        private static string Text(QueryResult r) => AgentFormat.Render(r, OutputFormat.Table);

        [Fact]
        public void StructsPrintAsCompactTuples()
        {
            var r = Query("items | where name == \"a\" | select Position Cell Tint Rotation");
            Assert.True(r.Ok, Text(r));
            var text = Text(r);
            Assert.Contains("(1, 2, 3)", text);
            Assert.Contains("(1, 0, 0, 1)", text);
            Assert.Contains("(0, 0, 0, 1)", text);
        }

        [Theory]
        [InlineData("set Position = \"(4, 5, 6)\"")]
        [InlineData("set Position = [4, 5, 6]")]
        [InlineData("set Position = {x: 4, y: 5, z: 6}")]
        public void Vector3AcceptsTextListAndObjectLiterals(string set)
        {
            var r = Mutate("items | where name == \"a\" | " + set);
            Assert.True(r.Ok, Text(r));
            Assert.Equal(new Vector3(4, 5, 6), _items[0].Position);
        }

        [Fact]
        public void APathThroughAStructWritesBack()
        {
            var r = Mutate("items | where name == \"b\" | set Position.Y = Position.Y + 1");
            Assert.True(r.Ok, Text(r));
            Assert.Equal(1.5f, _items[1].Position.Y, 3);
        }

        [Fact]
        public void IntegerVectorsRejectFractions()
        {
            var r = Mutate("items | where name == \"a\" | set Cell = \"(1.5, 2, 3)\"");
            Assert.False(r.Ok);
            Assert.Equal(new Vector3I(1, 2, 3), _items[0].Cell);
            Assert.True(Mutate("items | where name == \"a\" | set Cell = [7, 8, 9]").Ok);
            Assert.Equal(new Vector3I(7, 8, 9), _items[0].Cell);
        }

        [Fact]
        public void ColorsAcceptHexAndTuples()
        {
            Assert.True(Mutate("items | where name == \"b\" | set Tint = \"#00ff00\"").Ok);
            Assert.Equal(new Color(0, 1, 0, 1), _items[1].Tint);
            Assert.True(Mutate("items | where name == \"b\" | set Tint = \"(0, 0, 1)\"").Ok);
            Assert.Equal(new Color(0, 0, 1, 1), _items[1].Tint);
        }

        [Fact]
        public void WhereComparesApproximatelyAgainstText()
        {
            var r = Query("items | where Position == \"(0, 0.5, 0)\" | select name");
            Assert.True(r.Ok, Text(r));
            Assert.Contains("b", Text(r));
            Assert.DoesNotContain("a\n", Text(r) + "\n");
        }

        [Theory]
        [InlineData("Position == {x: 0, y: 0.5, z: 0}", "b")]
        [InlineData("Position == {y: 0.5}", "b")]
        [InlineData("Position != {x: 1, y: 2, z: 3}", "b")]
        [InlineData("Cell == {x: 1, y: 2, z: 3}", "a")]
        [InlineData("Tint == {r: 1, g: 0, b: 0, a: 1}", "a")]
        public void WhereComparesAgainstObjectLiterals(string predicate, string expected)
        {
            var r = Query($"items | where {predicate} | select name");
            Assert.True(r.Ok, Text(r));
            Assert.Equal(new[] { expected }, Rows(r));
        }

        [Fact]
        public void AnObjectLiteralMatchesNothingElse()
        {
            var r = Query("items | where Position == {x: 9, y: 9, z: 9} | count");
            Assert.True(r.Ok, Text(r));
            Assert.Equal("0", Text(r).Trim());
        }

        [Fact]
        public void FloatNoiseIsTidiedForReading()
        {
            // A 90° rotation about Y, scaled by 2: cos(90°) is -4.371139E-08 in float, and 136/255 is 0.533333361.
            _items[0].Basis = new Basis(Vector3.Up, Mathf.Pi / 2).Scaled(new Vector3(2, 2, 2));
            _items[0].Tint = Color.FromHtml("#ff8800");
            _items[0].Position = new Vector3(0.1f, 1e-7f, -1234567.8f);
            var text = Text(Query("items | where name == \"a\" | select Basis Tint Position"));
            // RQL's tuple display (0.2+): 6 significant digits, noise below 1e-6 as 0, -0 as 0.
            Assert.DoesNotContain("E-", text);
            Assert.Contains("(0, 0, -2, 0, 2, 0, 2, 0, 0)", text);
            Assert.Contains("(1, 0.533333, 0, 1)", text);
            Assert.Contains("(0.1, 0, -1.23457E+06)", text);
            // The exact value still compares and stays stored.
            Assert.Equal(new[] { "a" }, Rows(Query("items | where Tint == \"#ff8800\" | select name")));
            Assert.NotEqual(0f, _items[0].Basis.X.X);
        }

        private static string[] Rows(QueryResult r) =>
            Text(r).Split('\n').Skip(2).Where(l => l.Length > 0).ToArray();

        [Fact]
        public void TransformsAndBoundsRoundTrip()
        {
            Assert.True(Mutate("items | where name == \"a\" | set Transform = \"(1, 0, 0, 0, 1, 0, 0, 0, 1, 5, 6, 7)\"").Ok);
            Assert.Equal(new Vector3(5, 6, 7), _items[0].Transform.Origin);
            Assert.True(Mutate("items | where name == \"a\" | set Basis = \"(2, 0, 0, 0, 2, 0, 0, 0, 2)\"").Ok);
            Assert.Equal(new Vector3(2, 0, 0), _items[0].Basis.X);
            Assert.True(Mutate("items | where name == \"a\" | set Transform2 = \"(1, 0, 0, 1, 3, 4)\"").Ok);
            Assert.Equal(new Vector2(3, 4), _items[0].Transform2.Origin);
            Assert.True(Mutate("items | where name == \"a\" | set Rect = \"(1, 2, 3, 4)\", RectI = [1, 2, 3, 4], Bounds = \"(0, 0, 0, 1, 2, 3)\"").Ok);
            Assert.Equal(new Rect2(1, 2, 3, 4), _items[0].Rect);
            Assert.Equal(new Rect2I(1, 2, 3, 4), _items[0].RectI);
            Assert.Equal(new Vector3(1, 2, 3), _items[0].Bounds.Size);
            var r = Query("items | where name == \"a\" | select Transform Bounds");
            Assert.Contains("(1, 0, 0, 0, 1, 0, 0, 0, 1, 5, 6, 7)", Text(r));
            Assert.Contains("(0, 0, 0, 1, 2, 3)", Text(r));
        }

        [Fact]
        public void PlanesRoundTripAsNormalAndDistance()
        {
            Assert.True(Mutate("items | where name == \"a\" | set Floor = \"(0, 1, 0, 2)\"").Ok);
            Assert.Equal(new Plane(0, 1, 0, 2), _items[0].Floor);
            Assert.True(Mutate("items | where name == \"a\" | set Floor = [1, 0, 0, -3]").Ok);
            Assert.Equal(new Vector3(1, 0, 0), _items[0].Floor.Normal);
            Assert.Equal(-3f, _items[0].Floor.D);
            Assert.Contains("(1, 0, 0, -3)", Text(Query("items | where name == \"a\" | select Floor")));
            var r = Mutate("items | where name == \"a\" | set Floor = \"(0, 1, 0)\"");
            Assert.False(r.Ok);
            Assert.Contains("(a, b, c, d)", Text(r));
        }

        [Fact]
        public void ProjectionsRoundTripColumnByColumn()
        {
            Assert.Contains("(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)", Text(Query("items | where name == \"a\" | select Lens")));
            Assert.True(Mutate("items | where name == \"a\" | set Lens = \"(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16)\"").Ok);
            Assert.Equal(new Vector4(1, 2, 3, 4), _items[0].Lens.X);
            Assert.Equal(new Vector4(13, 14, 15, 16), _items[0].Lens.W);
            Assert.Contains("(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16)", Text(Query("items | where name == \"a\" | select Lens")));
            Assert.True(Mutate("items | where name == \"a\" | set Lens = [2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1]").Ok);
            Assert.Equal(2f, _items[0].Lens.Y.Y);
            Assert.True(Query("items | where Lens == \"(2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1)\" | count").Ok);
            var r = Mutate("items | where name == \"a\" | set Lens = \"(1, 0, 0, 1)\"");
            Assert.False(r.Ok);
            Assert.Contains("16 numbers", Text(r));
            Assert.Equal(2f, _items[0].Lens.X.X);
        }

        private static Rid MakeRid(ulong id) =>
            (Rid)typeof(Rid).GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { typeof(ulong) }, null)!
                .Invoke(new object[] { id });

        [Fact]
        public void RidsAreShownAndComparedButNeverWritten()
        {
            _items[0].Handle = MakeRid(1234);
            var text = Text(Query("items | where name == \"a\" | select Handle"));
            Assert.Contains("RID(1234)", text);
            Assert.Contains("RID(0)", Text(Query("items | where name == \"b\" | select Handle")));
            Assert.Contains("a", Text(Query("items | where Handle == \"RID(1234)\" | select name")));
            Assert.Contains("true", Text(Query("items | where name == \"a\" | select v=(Handle.IsValid)")));
            var r = Mutate("items | where name == \"a\" | set Handle = \"RID(5)\"");
            Assert.False(r.Ok);
            Assert.Contains("cannot be written", Text(r));
            Assert.Equal(1234UL, _items[0].Handle.Id);
        }

        [Fact]
        public void NegativeZeroPrintsAsZero()
        {
            _items[1].Position = new Vector3(-0f, 0f, -0f);
            _items[1].Rotation = new Quaternion(-0f, 0f, 0f, 1f);
            var text = Text(Query("items | where name == \"b\" | select Position Rotation"));
            Assert.Contains("(0, 0, 0)", text);
            Assert.Contains("(0, 0, 0, 1)", text);
            Assert.DoesNotContain("-0", text);
        }

        [Fact]
        public void ABadTupleIsACoercionFailureThatChangesNothing()
        {
            var before = _items[0].Position;
            var r = Mutate("items | where name == \"a\" | set Position = \"(1, 2)\"");
            Assert.False(r.Ok);
            Assert.Contains("(x, y, z)", Text(r));
            Assert.Equal(before, _items[0].Position);
        }
    }
}
