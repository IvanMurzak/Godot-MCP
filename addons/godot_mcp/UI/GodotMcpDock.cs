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
using com.IvanMurzak.Godot.MCP.Connection;
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// Root editor-dock <see cref="Control"/> for the Godot-MCP addon — the "AI Game Developer" panel
    /// the user docks in the Godot editor. This FOUNDATION scaffold builds only the header section (title
    /// + addon version) and exposes a <see cref="Body"/> container plus a <see cref="Refresh"/> hook that
    /// later tasks fill in (connection status / mode toggle, features list, footer, cloud auth). It is
    /// registered/unregistered by <see cref="GodotMcpPlugin"/> via
    /// <c>AddControlToDock</c>/<c>RemoveControlFromDocks</c>.
    ///
    /// <para>
    /// Editor-only (<c>#if TOOLS</c>): it constructs live Godot UI <see cref="Node"/>s, so it is verified
    /// via the headless Godot smoke (see <c>test.md</c> Suite 3), not the plain-xUnit host.
    /// </para>
    /// </summary>
    [Tool]
    public partial class GodotMcpDock : VBoxContainer
    {
        /// <summary>
        /// Addon version shown in the dock header. Kept in sync MANUALLY with <c>plugin.cfg</c>'s
        /// <c>version=</c> field (the dock does not parse <c>plugin.cfg</c> at runtime — it is not on the
        /// project's resource path in a published install, and a const avoids the IO). This mirrors the
        /// <c>PluginVersion</c> const on <c>GodotMcpConnection</c>; bump both when the addon version moves.
        /// </summary>
        public const string AddonVersion = "0.1.0";

        /// <summary>The dock's display title (also its tab name in the editor dock).</summary>
        public const string DockTitle = "AI Game Developer";

        /// <summary>
        /// Container into which later tasks insert the connection / features / footer / cloud-auth
        /// sections. Populated by <see cref="BuildUi"/> with the <see cref="ConnectionPanel"/>; later tasks
        /// add their sections here rather than re-parenting the whole dock.
        /// </summary>
        public VBoxContainer? Body { get; private set; }

        readonly GodotMcpConnection? _connection;
        ConnectionPanel? _connectionPanel;
        SupportFooter? _supportFooter;

        /// <summary>
        /// Construct the dock wired to the live <paramref name="connection"/> so its connection panel can
        /// show status and drive Connect/Disconnect/mode/URL. <see cref="GodotMcpPlugin"/> owns the
        /// connection and threads it through here. A null connection (defensive / design-preview) builds the
        /// header-only chrome with no connection panel.
        /// </summary>
        public GodotMcpDock(GodotMcpConnection? connection)
        {
            _connection = connection;
            Name = DockTitle;
            BuildUi();
        }

        /// <summary>
        /// Build the static dock chrome: a header (title + version) and the (currently empty)
        /// <see cref="Body"/> placeholder. Logo is intentionally omitted in this foundation (no committed
        /// logo asset yet) — a later task can add it to the header without changing this layout.
        /// </summary>
        void BuildUi()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;

            // --- Header section ---
            var header = new VBoxContainer { Name = "Header" };
            AddChild(header);

            var title = new Label
            {
                Name = "Title",
                Text = DockTitle
            };
            header.AddChild(title);

            var version = new Label
            {
                Name = "Version",
                Text = $"v{AddonVersion}"
            };
            header.AddChild(version);

            header.AddChild(new HSeparator { Name = "HeaderSeparator" });

            // --- Body ---
            // Hosts the connection section now; later tasks (features list, footer, cloud auth) add their
            // sections as further children of Body.
            Body = new VBoxContainer
            {
                Name = "Body",
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            AddChild(Body);

            // Connection section — only when a live connection was threaded in.
            if (_connection != null)
            {
                _connectionPanel = new ConnectionPanel(_connection);
                Body.AddChild(_connectionPanel);
            }

            // Support/footer section — static links + thanks, appended BELOW the connection panel. It holds
            // no live state / subscriptions, so it builds unconditionally (independent of the connection).
            _supportFooter = new SupportFooter();
            Body.AddChild(_supportFooter);
        }

        /// <summary>
        /// Re-render the dock from current state. No-op in this foundation scaffold — there is no dynamic
        /// state to show yet. The later connection task fills this in (status line, mode indicator) and
        /// the connection layer calls it on the editor main thread (e.g. via the main-thread dispatcher)
        /// when the connection state changes. Safe to call any number of times.
        /// </summary>
        public void Refresh()
        {
            _connectionPanel?.Refresh();
        }
    }
}
#endif
