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
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.Godot.MCP.UI;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// A uniform view over one of the reused McpPlugin feature managers (<see cref="IToolManager"/> /
    /// <see cref="IPromptManager"/> / <see cref="IResourceManager"/>). The three managers expose the same
    /// shape (enumerate / count / is-enabled / set-enabled) under different method names; this collapses them
    /// to one surface so <see cref="GodotMcpConnection"/> and the dock's features UI can address any kind
    /// generically via <see cref="GodotMcpFeatureKind"/>. Editor-only (<c>#if TOOLS</c>): it depends on the
    /// reused client types, which are only present once the connection assembly is loaded. The map merge/
    /// capture decisions stay pure-managed in <see cref="GodotMcpFeatureStateMerge"/> and the filter/row
    /// view-model shaping in <see cref="FeatureFilter"/> / <see cref="FeatureRowItem"/>; this adapter is the
    /// thin live-manager binding that maps each live descriptor into a pure-managed <see cref="FeatureRowItem"/>
    /// (verified via the headless Godot smoke, not the plain-xUnit host).
    /// </summary>
    public interface IFeatureManagerAdapter
    {
        /// <summary>Live item names of this kind.</summary>
        IEnumerable<string> GetNames();

        /// <summary>
        /// Live items as pure-managed <see cref="FeatureRowItem"/> view-models — the common
        /// name/title/description/enabled plus the kind-specific metadata (tools: token count + input args;
        /// prompts: role + arguments; resources: uri + mimetype).
        /// </summary>
        IEnumerable<FeatureRowItem> GetItems();

        /// <summary>(enabled, total, enabledTokenCount) — token count is non-zero only for tools.</summary>
        (int Enabled, int Total, int EnabledTokenCount) GetCounts();

        /// <summary>Set one item's enabled-state on the live manager.</summary>
        void SetEnabled(string name, bool enabled);
    }

    /// <summary>Adapter over <see cref="IToolManager"/> (the only kind with a token count + input-schema args).</summary>
    public sealed class ToolManagerAdapter : IFeatureManagerAdapter
    {
        readonly IToolManager _manager;
        public ToolManagerAdapter(IToolManager manager) => _manager = manager;

        public IEnumerable<string> GetNames() =>
            _manager.GetAllTools().Where(t => t != null).Select(t => t.Name);

        public IEnumerable<FeatureRowItem> GetItems() =>
            _manager.GetAllTools().Where(t => t != null)
                .Select(t => new FeatureRowItem
                {
                    Name = t.Name,
                    Title = t.Title,
                    Description = t.Description,
                    Enabled = _manager.IsToolEnabled(t.Name),
                    TokenCount = t.TokenCount,
                    Inputs = FeatureFilter.ParseSchemaArguments(t.InputSchema)
                });

        public (int Enabled, int Total, int EnabledTokenCount) GetCounts() =>
            (_manager.EnabledToolsCount, _manager.TotalToolsCount, _manager.EnabledToolsTokenCount);

        public void SetEnabled(string name, bool enabled) => _manager.SetToolEnabled(name, enabled);
    }

    /// <summary>Adapter over <see cref="IPromptManager"/> (role + prompt arguments; no token count → reports 0).</summary>
    public sealed class PromptManagerAdapter : IFeatureManagerAdapter
    {
        readonly IPromptManager _manager;
        public PromptManagerAdapter(IPromptManager manager) => _manager = manager;

        public IEnumerable<string> GetNames() =>
            _manager.GetAllPrompts().Where(p => p != null).Select(p => p.Name);

        public IEnumerable<FeatureRowItem> GetItems() =>
            _manager.GetAllPrompts().Where(p => p != null)
                .Select(p => new FeatureRowItem
                {
                    Name = p.Name,
                    Title = p.Title,
                    Description = p.Description,
                    Enabled = _manager.IsPromptEnabled(p.Name),
                    Role = p.Role.ToString(),
                    Arguments = FeatureFilter.ParseSchemaArguments(p.InputSchema)
                });

        public (int Enabled, int Total, int EnabledTokenCount) GetCounts() =>
            (_manager.EnabledPromptsCount, _manager.TotalPromptsCount, 0);

        public void SetEnabled(string name, bool enabled) => _manager.SetPromptEnabled(name, enabled);
    }

    /// <summary>Adapter over <see cref="IResourceManager"/> (uri + mimetype; no token count → reports 0).</summary>
    public sealed class ResourceManagerAdapter : IFeatureManagerAdapter
    {
        readonly IResourceManager _manager;
        public ResourceManagerAdapter(IResourceManager manager) => _manager = manager;

        public IEnumerable<string> GetNames() =>
            _manager.GetAllResources().Where(r => r != null).Select(r => r.Name);

        public IEnumerable<FeatureRowItem> GetItems() =>
            _manager.GetAllResources().Where(r => r != null)
                .Select(r => new FeatureRowItem
                {
                    Name = r.Name,
                    // IRunResource carries no Title; fall back to the name for the row heading.
                    Title = null,
                    Description = r.Description,
                    Enabled = _manager.IsResourceEnabled(r.Name),
                    Uri = r.Route,
                    MimeType = r.MimeType
                });

        public (int Enabled, int Total, int EnabledTokenCount) GetCounts() =>
            (_manager.EnabledResourcesCount, _manager.TotalResourcesCount, 0);

        public void SetEnabled(string name, bool enabled) => _manager.SetResourceEnabled(name, enabled);
    }
}
#endif
