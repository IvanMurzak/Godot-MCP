/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using com.IvanMurzak.Godot.MCP.CSG;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.CSG.Tests
{
    /// <summary>
    /// Pure-managed specs for <see cref="CsgOperations"/> — the boolean-operation parsing every CSG tool uses
    /// to map an LLM-supplied string onto the right Godot <c>OperationEnum</c>. No Godot binary required.
    /// </summary>
    public class CsgOperationTests
    {
        [Theory]
        [InlineData("Union", CsgOperation.Union)]
        [InlineData("union", CsgOperation.Union)]
        [InlineData(" add ", CsgOperation.Union)]
        [InlineData("merge", CsgOperation.Union)]
        [InlineData("Intersection", CsgOperation.Intersection)]
        [InlineData("intersect", CsgOperation.Intersection)]
        [InlineData("Subtraction", CsgOperation.Subtraction)]
        [InlineData("subtract", CsgOperation.Subtraction)]
        [InlineData("SUB", CsgOperation.Subtraction)]
        public void TryParse_KnownValues_ParseToExpected(string input, CsgOperation expected)
        {
            Assert.True(CsgOperations.TryParse(input, out var operation));
            Assert.Equal(expected, operation);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("xor")]
        [InlineData("difference")]
        public void TryParse_UnknownOrEmpty_ReturnsFalse(string? input)
        {
            Assert.False(CsgOperations.TryParse(input, out _));
        }

        [Fact]
        public void Parse_Unknown_Throws()
        {
            Assert.Throws<ArgumentException>(() => CsgOperations.Parse("nand"));
        }

        [Fact]
        public void ToLabel_IsStable()
        {
            Assert.Equal("Union", CsgOperation.Union.ToLabel());
            Assert.Equal("Intersection", CsgOperation.Intersection.ToLabel());
            Assert.Equal("Subtraction", CsgOperation.Subtraction.ToLabel());
        }

        [Fact]
        public void EnumValues_MatchGodotOperationEnumOrdering()
        {
            // The editor tools cast CsgOperation <-> CsgShape3D.OperationEnum directly, so the integer
            // values MUST stay pinned to Godot's ordering (Union=0, Intersection=1, Subtraction=2).
            Assert.Equal(0, (int)CsgOperation.Union);
            Assert.Equal(1, (int)CsgOperation.Intersection);
            Assert.Equal(2, (int)CsgOperation.Subtraction);
        }
    }
}
