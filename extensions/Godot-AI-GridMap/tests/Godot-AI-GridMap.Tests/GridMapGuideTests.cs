/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.Extensions.Tests;

namespace com.IvanMurzak.Godot.MCP.GridMap.Tests
{
    /// <summary>The shared guide checks (extensions/Shared/Tests/GuideExampleTests.cs) for GridMapGuide.</summary>
    public sealed class GridMapGuideTests : GuideExampleTests
    {
        protected override string Examples => GridMapGuide.Examples;
        protected override string Markdown => GridMapGuide.Markdown;
    }
}
