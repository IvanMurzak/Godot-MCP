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
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using Rql;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// RQL tools (rql-query / rql-mutate / rql-schema): query and change the live Godot editor — scene nodes, resources,
    /// files, scenes, editor state — with one language instead of one tool per operation (they replace the node-*,
    /// scene-*, resource-*, filesystem-*, editor-* and, in the editor, console-* / reflection-* tools; see docs/rql.md).
    /// Results are compact text tables (fewer tokens than JSON objects). Every call runs on the editor main thread.
    /// </summary>
    [AiToolType]
    public partial class Tool_Rql
    {
        /// <summary>A query or mutation: executed on the main thread, its (already encoded) result rendered after the hop.</summary>
        private static ResponseCallTool Run(Func<QueryResult> execute) => Run(execute, result =>
        {
            var text = AgentFormat.Render(result, OutputFormat.Table);
            return result.Ok ? ResponseCallTool.Success(text) : ResponseCallTool.Error(text);
        });

        /// <summary>
        /// Run <paramref name="execute"/> on the editor main thread inside one property-table cache scope (a query asks for
        /// the same object's Godot properties many times), then build the response off it; an exception is an
        /// <c>internal-error</c> response.
        /// </summary>
        private static ResponseCallTool Run<T>(Func<T> execute, Func<T, ResponseCallTool> respond)
        {
            T result;
            try
            {
                result = MainThread.Instance.Run(() =>
                {
                    using (GodotVariants.QueryScope()) return execute();
                });
            }
            catch (Exception ex)
            {
                var cause = ex is AggregateException { InnerException: { } inner } ? inner : ex;
                return ResponseCallTool.Error($"error internal-error: {cause.GetType().Name}: {cause.Message}");
            }
            return respond(result);
        }
    }
}
#endif
