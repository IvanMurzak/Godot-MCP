/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.YOUR_FEATURE
{
    /// <summary>
    /// The <c>YOUR_TOOL_PREFIX</c> source: a view object whose public members RQL reads and whose <c>[RqlCallable]</c>
    /// methods <c>rql-mutate</c> calls. Every RQL call runs on the editor main thread, so the Godot editor API is safe here.
    /// </summary>
    public sealed class YOUR_FEATUREApi
    {
        public static readonly YOUR_FEATUREApi Instance = new YOUR_FEATUREApi();

        [RqlDescription("the running Godot version, e.g. \"4.5.1-stable (official)\"")]
        public string godotVersion => (string)Engine.GetVersionInfo()["string"];

        [RqlDescription("the editor UI scale")]
        public float editorScale => EditorInterface.Singleton.GetEditorScale();

        [RqlCallable("return the message, or \"YOUR_TOOL_PREFIX-ready\" when it is empty")]
        public string Echo(string? message = null) => EchoText(message);

        /// <summary>Pure-managed logic of <see cref="Echo"/> (unit-tested).</summary>
        public static string EchoText(string? message) => string.IsNullOrEmpty(message) ? "YOUR_TOOL_PREFIX-ready" : message!;
    }
}
#endif
