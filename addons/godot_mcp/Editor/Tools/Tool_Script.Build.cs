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
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Reload;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Script
    {
        public const string ScriptBuildToolId = "script-build";

        [AiTool
        (
            ScriptBuildToolId,
            Title = "Script / Build"
        )]
        [Description("Build the project's C# (dotnet build, Debug configuration like the editor's builds) so C# scripts " +
            "written with '" + ScriptCreateToolId + "' / '" + ScriptUpdateToolId + "' load: new [GlobalClass] types, changed " +
            "node scripts. The build runs in the background; when it succeeds Godot reloads the C# assembly and the plugin " +
            "reconnects, and the result (the compiler warnings, or the compiler errors) is delivered after that reload through " +
            "the request's requestId. Tool calls made while the reload is pending are refused with a retryable error: " +
            "retry them a few seconds later.")]
        public ResponseCallTool Build
        (
            [RequestID]
            string? requestId = null
        )
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return ResponseCallTool.Error("The original request with a valid requestId must be provided.");
            var id = requestId!;
            return MainThread.Instance.Run(() =>
            {
                var tracker = ProjectBuildTracker.Current
                    ?? throw new InvalidOperationException("The Godot-MCP editor plugin is not booted; enable it and retry.");
                return ResponseCallTool.Processing(tracker.Start(id)).SetRequestID(id);
            });
        }
    }
}
#endif
