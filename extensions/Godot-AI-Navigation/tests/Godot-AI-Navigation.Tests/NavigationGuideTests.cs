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

namespace com.IvanMurzak.Godot.MCP.Navigation.Tests
{
    /// <summary>The shared guide checks (extensions/Shared/Tests/GuideExampleTests.cs) for NavigationGuide.</summary>
    public sealed class NavigationGuideTests : GuideExampleTests
    {
        protected override string Examples => NavigationGuide.Examples;
        protected override string Markdown => NavigationGuide.Markdown;
    }
}
