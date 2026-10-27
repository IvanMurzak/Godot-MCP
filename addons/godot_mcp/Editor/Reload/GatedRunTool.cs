/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;

namespace com.IvanMurzak.Godot.MCP.Reload
{
    /// <summary>
    /// A tool whose calls pass the <see cref="AssemblyReloadGate"/>: refused with a retryable
    /// <see cref="ResponseErrorKind.Unavailable"/> error while a reload is pending or running, counted while they run.
    /// Everything else is the wrapped tool's, so the tool list, schemas and enable states are unchanged.
    /// </summary>
    public sealed class GatedRunTool : IRunTool
    {
        readonly IRunTool _inner;
        readonly AssemblyReloadGate _gate;

        public GatedRunTool(IRunTool inner, AssemblyReloadGate gate)
        {
            _inner = inner;
            _gate = gate;
        }

        public IRunTool Inner => _inner;

        /// <summary>Wrap every tool of <paramref name="tools"/> (already wrapped ones are left as they are), keeping their order.</summary>
        public static int WrapAll(IToolManager? tools, AssemblyReloadGate gate)
        {
            if (tools == null)
                return 0;
            var wrapped = 0;
            foreach (var tool in tools.GetAllTools().Where(t => t is not GatedRunTool).ToList())
            {
                // Remove then add: the tool table reuses the freed slot, so the listed order stays the same.
                if (!tools.RemoveTool(tool.Name))
                    continue;
                tools.AddTool(tool.Name, new GatedRunTool(tool, gate));
                wrapped++;
            }
            return wrapped;
        }

        public async Task<ResponseCallTool> Run(string requestId, IReadOnlyDictionary<string, JsonElement>? namedParameters, CancellationToken cancellationToken = default)
        {
            if (!_gate.TryEnter(out var refusal))
                return Refused(refusal, requestId);
            try
            {
                var result = await _inner.Run(requestId, namedParameters, cancellationToken).ConfigureAwait(false);
                // A body the unloading gate refused on the main thread fails the tool with the refusal wrapped in its error
                // ("Tool execution failed ... One or more errors occurred. (<refusal>)"): answer with the refusal itself.
                return result.Status == ResponseStatus.Error && _gate.Refusal is { } late && result.GetMessage()?.Contains(late) == true
                    ? Refused(late, requestId)
                    : result;
            }
            finally
            {
                _gate.Exit();
            }
        }

        static ResponseCallTool Refused(string? refusal, string requestId)
            => ResponseCallTool.Error(refusal, ResponseErrorKind.Unavailable).SetRequestID(requestId);

        public bool Enabled { get => _inner.Enabled; set => _inner.Enabled = value; }
        public string Name => _inner.Name;
        public string? Title => _inner.Title;
        public string? Description => _inner.Description;
        public string? SkillDescription => _inner.SkillDescription;
        public string? SkillBody => _inner.SkillBody;
        public JsonNode? InputSchema => _inner.InputSchema;
        public JsonNode? OutputSchema => _inner.OutputSchema;
        public McpToolType ToolType => _inner.ToolType;
        public bool? ReadOnlyHint => _inner.ReadOnlyHint;
        public bool? DestructiveHint => _inner.DestructiveHint;
        public bool? IdempotentHint => _inner.IdempotentHint;
        public bool? OpenWorldHint => _inner.OpenWorldHint;
        public int TokenCount => _inner.TokenCount;
        public int SkillMetadataTokenCount => _inner.SkillMetadataTokenCount;
    }
}
#endif
