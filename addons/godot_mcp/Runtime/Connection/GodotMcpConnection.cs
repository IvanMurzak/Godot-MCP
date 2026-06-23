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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.MainThreadDispatch;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.Godot.MCP.Tools;
using com.IvanMurzak.Godot.MCP.UI;
using com.IvanMurzak.Godot.MCP.UI.Agents;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet;
using Godot;
using Microsoft.AspNetCore.SignalR.Client;
using R3;
using McpVersion = com.IvanMurzak.McpPlugin.Common.Version;
using McpClientData = com.IvanMurzak.McpPlugin.Common.Model.McpClientData;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Owns the lifecycle of the reused <c>com.IvanMurzak.McpPlugin</c> SignalR client for the
    /// Godot editor plugin. The Godot analog of Unity-MCP's <c>UnityMcpPlugin.BuildMcpPlugin</c> +
    /// <c>Connect</c> path, condensed to the single-instance editor case.
    ///
    /// <para>
    /// Responsibilities:
    /// <list type="bullet">
    ///   <item>Build the <see cref="Reflector"/> with the Godot type converters
    ///   (<see cref="GodotReflectorFactory"/>).</item>
    ///   <item>Build an <see cref="IMcpPlugin"/> via <see cref="McpPluginBuilder"/>, scanning the
    ///   addon assembly for <c>[AiToolType]</c>/<c>[AiTool]</c> methods (the <c>ping</c> tool today).</item>
    ///   <item>Apply the resolved <see cref="GodotMcpConfig"/> (Cloud/Custom host + bearer token).</item>
    ///   <item>Connect. Auto-reconnect/backoff is handled inside the McpPlugin client when
    ///   <see cref="ConnectionConfig.KeepConnected"/> is true — NOT reimplemented here.</item>
    /// </list>
    /// </para>
    ///
    /// This type is runtime-agnostic (it lives OUTSIDE <c>#if TOOLS</c> so it also compiles into an
    /// exported, non-editor game build): it instantiates the reused SignalR client and owns the
    /// connection lifecycle, depending only on pure-managed config / view-model / reflector helpers and
    /// the Godot <i>runtime</i> API (<see cref="Godot.Node"/>, <see cref="GD"/>, <c>ProjectSettings</c>),
    /// never on a Godot editor API. In the editor it is driven by <see cref="GodotMcpPlugin"/>'s tree
    /// lifecycle; the sole editor-only coupling — installing the editor <c>ResourceLoader</c> resolver
    /// into the (pure-managed) Resource reflection converter — is guarded by <c>#if TOOLS</c> at its one
    /// call site in <see cref="Start"/>, so the exported build wires up the reflector without it.
    /// </summary>
    public sealed class GodotMcpConnection : IDisposable
    {
        /// <summary>
        /// Fallback plugin version, used ONLY when <c>addons/godot_mcp/plugin.cfg</c> cannot be read at
        /// runtime (it always can in a real install). Manually maintained — bump it alongside
        /// plugin.cfg's <c>version=</c> (no workflow or doc rewrites this literal, so it can silently
        /// drift if you forget). The live, parsed value in <see cref="PluginVersion"/> is the source of
        /// truth; <see cref="ResolvePluginVersion"/> emits a warning whenever it has to fall back here.
        /// </summary>
        const string FallbackPluginVersion = "0.13.2";

        /// <summary>
        /// Plugin version reported to the server in the MCP handshake. Resolved ONCE from
        /// <c>addons/godot_mcp/plugin.cfg</c> — the single source of truth the release workflow itself reads
        /// (<c>release.yml</c> greps the same <c>version=</c> line and tags <c>v&lt;version&gt;</c>) — so it
        /// can never drift from the released version the way the old hard-coded <c>"0.1.0"</c> literal did
        /// (issue #94). Falls back to <see cref="FallbackPluginVersion"/> only if the file is unreadable.
        /// NOTE: the LOCAL server binary is NOT pinned by this version anymore — the shared
        /// GameDev-MCP-Server download is pinned by <see cref="GodotMcpServerView.ServerVersion"/>.
        /// </summary>
        public static readonly string PluginVersion = ResolvePluginVersion();

        /// <summary>
        /// Resolve the addon version from <c>res://addons/godot_mcp/plugin.cfg</c> via the pure-managed
        /// <see cref="GodotMcpServerView.ParsePluginVersion"/> parser, with a safe fallback. The only Godot
        /// dependency is the <c>res://</c> path resolution; the parse is unit-tested in the plain-xUnit host.
        /// Never throws — a read/parse failure degrades to <see cref="FallbackPluginVersion"/> so a config
        /// problem can never break the connection boot or the type initializer.
        /// </summary>
        static string ResolvePluginVersion()
        {
            try
            {
                var path = ProjectSettings.GlobalizePath("res://addons/godot_mcp/plugin.cfg");
                if (System.IO.File.Exists(path))
                {
                    var parsed = GodotMcpServerView.ParsePluginVersion(System.IO.File.ReadAllText(path));
                    if (!string.IsNullOrEmpty(parsed))
                        return parsed!;
                }
            }
            catch
            {
                // Fall through to the literal fallback — never let a config read break the boot path.
            }

            // Reached only when plugin.cfg was missing/unreadable/blank. Surface it: the fallback pins
            // the handshake version to this literal (the local-server download is pinned independently by
            // GodotMcpServerView.ServerVersion), so a silent drift here misreports the plugin version.
            // Guard the warning itself so the never-throws contract holds even if GD is unavailable mid
            // editor-reload.
            try
            {
                GD.PushWarning(
                    $"[Godot-MCP] plugin.cfg version unreadable; pinning to fallback v{FallbackPluginVersion}. " +
                    "The MCP handshake uses this version — bump FallbackPluginVersion alongside plugin.cfg if it drifts.");
            }
            catch
            {
                // Diagnostics must never break the boot/type-initializer path.
            }

            return FallbackPluginVersion;
        }

        /// <summary>
        /// <c>user://</c> path of the persisted config file (the serialized-config precedence layer).
        /// Resolved to an absolute path lazily in <see cref="ConfigFilePath"/> via
        /// <see cref="ProjectSettings.GlobalizePath(string)"/>.
        /// </summary>
        const string ConfigUserPath = "user://godot-mcp-config.json";

        readonly GodotMcpConfig _config;
        IMcpPlugin? _plugin;
        Reflector? _publishedReflector;

        /// <summary>
        /// Guards <see cref="ResolveConfig"/> so the persisted + <c>.env</c> layers are applied to
        /// <see cref="_config"/> exactly once. Set the first time config is resolved (by <see cref="ResolveConfig"/>,
        /// called from the plugin boot BEFORE the dock UI is built, or lazily from <see cref="Start"/>), so a later
        /// <see cref="Start"/> / <see cref="Reconnect"/> re-entry does not re-read the files (and would never clobber
        /// a live UI edit with the on-disk value mid-session).
        /// </summary>
        bool _configResolved;

        /// <summary>
        /// Live subscription to the reused client's <see cref="IConnection.ConnectionState"/> +
        /// <see cref="IConnection.KeepConnected"/> reactive properties. Re-created on every
        /// <see cref="Start"/> (a new plugin instance exposes new properties) and disposed on
        /// <see cref="Dispose"/>. <see cref="SerialDisposable"/> auto-disposes the prior subscription when
        /// reassigned, so a reconnect never leaks a stale subscription.
        /// </summary>
        readonly SerialDisposable _stateSubscription = new();

        /// <summary>
        /// Live subscription to the reused client's <see cref="IConnection.OnAuthorizationRejected"/> stream.
        /// Re-created on every <see cref="Start"/> and disposed on <see cref="Dispose"/> (same lifecycle as
        /// <see cref="_stateSubscription"/>). Marshals the rejection onto the editor main thread before
        /// raising <see cref="AuthorizationRejected"/>.
        /// </summary>
        readonly SerialDisposable _authRejectedSubscription = new();

        /// <summary>
        /// Live subscription to the three feature managers' <c>On*Updated</c> streams (tools / prompts /
        /// resources), merged into a single editor-main-thread <see cref="FeaturesUpdated"/> event the dock's
        /// features panel refreshes from. Re-created on every <see cref="Start"/> (a new plugin exposes new
        /// managers/streams) and released on plugin teardown — same <see cref="SerialDisposable"/> discipline as
        /// <see cref="_stateSubscription"/>, so a reconnect never leaks a stale subscription.
        /// </summary>
        readonly SerialDisposable _featuresSubscription = new();

        /// <summary>
        /// Live subscription to the reused client's <see cref="IMcpManager.OnClientsChanged"/> stream — fired on the
        /// editor main thread whenever an MCP client (AI agent: Copilot / Claude / …) connects OR disconnects,
        /// carrying the full active-client list. Re-created on every <see cref="Start"/> and released on plugin
        /// teardown (same <see cref="SerialDisposable"/> discipline as <see cref="_featuresSubscription"/>), so a
        /// reconnect never leaks a stale subscription.
        /// </summary>
        readonly SerialDisposable _agentsSubscription = new();

        /// <summary>
        /// The last-seen snapshot of connected MCP clients (AI agents). Replaced atomically on the editor main
        /// thread by <see cref="UpdateActiveAgents"/> right before <see cref="AgentsUpdated"/> fires, so a UI handler
        /// reading <see cref="ActiveAgents"/> always sees the value that triggered the event. Volatile so the
        /// reference write is visible across threads (it is seeded off the SignalR thread's snapshot in
        /// <see cref="SubscribeToAgentUpdates"/>).
        /// </summary>
        volatile IReadOnlyList<McpClientData> _activeAgents = Array.Empty<McpClientData>();

        /// <summary>
        /// Holds the last reduced status (so <see cref="ConnectionStatus"/> is readable without a live
        /// plugin) and owns the de-dup rule. Pure-managed (<see cref="ConnectionStatusTracker"/>) so the
        /// status path — de-dup, late-subscriber convergence, reconnect reset — is unit-tested in the
        /// plain-xUnit host. The dock's periodic re-sync reads <see cref="ConnectionStatus"/> (i.e.
        /// <c>_statusTracker.Current</c>) DIRECTLY, bypassing the event, so a render can never be
        /// permanently lost to the de-dup (the root cause of issue #42).
        /// </summary>
        readonly ConnectionStatusTracker _statusTracker = new();

        /// <summary>The active config (resolved Host/Token/mode are read live off this).</summary>
        public GodotMcpConfig Config => _config;

        /// <summary>The built plugin instance, or null before <see cref="Start"/> / after <see cref="Dispose"/>.</summary>
        public IMcpPlugin? Plugin => _plugin;

        /// <summary>
        /// The plugin's <see cref="IMcpManager"/> (owns the tool/prompt/resource managers), or null before
        /// <see cref="Start"/> / after <see cref="Dispose"/>. The dock's features section reaches the
        /// per-kind managers (<see cref="IMcpManager.ToolManager"/> etc.) through this rather than poking the
        /// plugin internals, so the editor UI has a single, null-safe accessor for the feature stats.
        /// </summary>
        public IMcpManager? McpManager => _plugin?.McpManager;

        /// <summary>
        /// The resolved cloud BASE url (no <c>/mcp</c> hub suffix) — the host the device-auth endpoints
        /// (<c>/api/auth/device/*</c>) live on. The dock's Cloud-auth section passes this to
        /// <see cref="GodotDeviceAuthFlow.StartAsync"/>. Read live off the config so an env override applies.
        /// </summary>
        public string CloudBaseUrl => GodotMcpConfig.ResolveCloudBaseUrl();

        /// <summary>
        /// Raised when the server rejects the connection's authorization token (the reused client's
        /// <see cref="IConnection.OnAuthorizationRejected"/> fired). ALWAYS marshalled onto the Godot editor
        /// main thread, so a UI handler may clear the stored token and touch <see cref="Godot.Control"/>s
        /// directly. The Cloud-auth section subscribes to drop the rejected <c>CloudToken</c> and revert to
        /// the Authorize state. The token itself is never passed through this event (it carries no payload).
        /// </summary>
        public event Action? AuthorizationRejected;

        /// <summary>
        /// The current simplified connection status (Disconnected / Connecting / Connected), reduced from
        /// the reused client's <see cref="HubConnectionState"/> + <c>KeepConnected</c> via
        /// <see cref="ConnectionPanelView.Reduce"/>. The dock reads this for its initial render and is
        /// pushed subsequent changes via <see cref="ConnectionStatusChanged"/>.
        /// </summary>
        public ConnectionStatus ConnectionStatus => _statusTracker.Current;

        /// <summary>
        /// Raised whenever <see cref="ConnectionStatus"/> changes. ALWAYS marshalled onto the Godot editor
        /// main thread (the McpPlugin client fires its R3 properties from background SignalR threads), so a
        /// UI handler can touch <see cref="Godot.Control"/>s directly without re-dispatching. Only fired on
        /// an actual change (de-duplicated) to avoid redundant UI churn.
        /// </summary>
        public event Action<ConnectionStatus>? ConnectionStatusChanged;

        /// <summary>
        /// Raised when any feature manager's registry changes (a tool/prompt/resource was enabled, disabled,
        /// added, or removed — the reused client's <c>On*Updated</c> fired). ALWAYS marshalled onto the Godot
        /// editor main thread, so the dock's features panel may refresh its count labels by touching
        /// <see cref="Godot.Control"/>s directly. Carries no payload — the panel re-reads counts from the
        /// managers. Re-wired to the new managers on every <see cref="Start"/>/<see cref="Reconnect"/>.
        /// </summary>
        public event Action? FeaturesUpdated;

        /// <summary>
        /// The current set of connected MCP clients (AI agents) — Copilot / Claude / Cursor / … — as reported by the
        /// reused client's <see cref="IMcpManager.ActiveClients"/>. Empty before <see cref="Start"/> / after
        /// teardown, and (in Cloud mode) auth-scoped to THIS plugin's own agents by the server's
        /// <c>ShouldNotifySession</c> strategy. The dock's AI-agent timeline point renders this list and turns its
        /// dot green when it is non-empty. Read on the editor main thread (the value is refreshed there before
        /// <see cref="AgentsUpdated"/> fires).
        /// </summary>
        public IReadOnlyList<McpClientData> ActiveAgents => _activeAgents;

        /// <summary>
        /// Raised whenever the set of connected AI agents changes (a client connected or disconnected — the reused
        /// client's <see cref="IMcpManager.OnClientsChanged"/> fired). ALWAYS marshalled onto the Godot editor main
        /// thread, so the dock's AI-agent section may refresh by touching <see cref="Godot.Control"/>s directly.
        /// Carries no payload — the handler re-reads <see cref="ActiveAgents"/>. Re-wired to the new manager on every
        /// <see cref="Start"/>/<see cref="Reconnect"/>.
        /// </summary>
        public event Action? AgentsUpdated;

        /// <summary>
        /// Wire the pure-managed <see cref="GodotMcpDrainDiagnostics"/> seam's warning sink to a defensively
        /// wrapped <see cref="GD.PushWarning(string)"/> exactly once, when this type is first touched (which is
        /// guaranteed before any <see cref="DisconnectAndDrain"/> call). The seam stays Godot-free + unit-testable;
        /// the actual editor/runtime warning is emitted through this swallow-on-fault wrapper so a timeout report
        /// never throws into the ALC-unloading teardown path even if GD is unavailable mid editor-reload.
        /// </summary>
        static GodotMcpConnection()
        {
            GodotMcpDrainDiagnostics.Warn = PushWarningSafe;
        }

        /// <summary>
        /// Emit a warning via <see cref="GD.PushWarning(string)"/>, swallowing any failure (GD may be unavailable
        /// mid editor-reload). Same defensive discipline as the inline <c>try { GD.Push* } catch { }</c> call sites
        /// in <see cref="DisconnectAndDrain"/> — diagnostics must never break the boot/unload path.
        /// </summary>
        static void PushWarningSafe(string message)
        {
            try { GD.PushWarning(message); }
            catch { /* GD may be unavailable mid-reload; swallow */ }
        }

        public GodotMcpConnection(GodotMcpConfig? config = null)
        {
            _config = config ?? new GodotMcpConfig();
        }

        /// <summary>
        /// Build the plugin (reflector + tool scan + config) and initiate the connection. Idempotent:
        /// a second call while a plugin already exists is a no-op. The connect itself is fire-and-forget
        /// from the editor's perspective — the McpPlugin client manages (re)connection in the background.
        /// </summary>
        public void Start()
        {
            if (_plugin != null)
            {
                GD.Print("[Godot-MCP] connection already started; ignoring duplicate Start().");
                return;
            }

            // Resolve the persisted + .env config layers (idempotent). The plugin boot calls ResolveConfig()
            // BEFORE building the dock UI so the panels render from the SAVED mode/token/agent rather than the
            // built-in defaults (the first-load "Cloud token empty / Authorize prompted / wrong agent restored"
            // bug); this lazy call covers a Start() reached without that pre-step. A no-op once already resolved.
            ResolveConfig();

            Reflector reflector = GodotReflectorFactory.CreateDefaultReflector();

            // Wire the editor-side ResourceLoader.Load resolution into the (pure-managed) Resource
            // reflection converter so node-modify can assign a Resource-typed property by ref (res:// path /
            // instance id). The converter lives outside #if TOOLS; this installs the native resolver.
            //
            // EDITOR-ONLY COUPLING (the single one in the connect path): Tool_Resource — and the editor
            // ResourceLoader/EditorInterface wiring it depends on — is itself gated by #if TOOLS, so it does
            // NOT compile into an exported game build. Guard the call so the runtime-agnostic connection
            // compiles with TOOLS undefined; in a non-editor build the Resource converter simply has no
            // native resolver installed here (a runtime entry point can inject its own resolver later — T2).
#if TOOLS
            Tools.Tool_Resource.InstallReflectionResolver();
#endif

            // Publish the connection's reflector as the ambient one so tool handlers (e.g. node-modify)
            // share the exact converter set registered here instead of building their own.
            GodotMcpReflector.Current = reflector;
            _publishedReflector = reflector;

            var version = new McpVersion
            {
                Api = com.IvanMurzak.McpPlugin.Common.Consts.ApiVersion,
                Plugin = PluginVersion,
                Environment = $"Godot {Engine.GetVersionInfo()["string"]}"
            };

            // The scan set for tool/prompt/resource registration AND IReflectorModule discovery (issue
            // #86). Mirrors Unity-MCP's BuildMcpPlugin (which scans AssemblyUtils.AllAssemblies and prunes
            // the heavy ones at the builder via .IgnoreAssemblies): enumerate the default load context
            // broadly so a Godot-MCP extension's assembly — unknown ahead of time — is reachable, then let
            // the .IgnoreAssemblies(...) prune below keep the heavy assemblies from ever being
            // type-enumerated. AllAssemblies includes THIS addon assembly (the ping tool and future tool
            // families live here).
            Assembly[] assemblies = GodotAssemblyUtils.AllAssemblies;

            // Route the reused framework's Microsoft.Extensions.Logging output (ConnectionManager /
            // hub-connector connect, hub-state, version handshake, errors) to the Godot Output, gated by the
            // LIVE configured Log Level (read off _config each call so the dock dropdown applies without a
            // rebuild). Without a provider these framework logs are invisible — they are the diagnostic for
            // the "Connecting…" hang. The provider/logger are pure-managed; the only Godot dependency is the
            // injected GD.* + log-collector sink below.
            var loggerProvider = new GodotMcpLoggerProvider(() => _config.ActiveLogLevel, RouteFrameworkLog);

            // OPT IN to McpPlugin's bounded reconnect + fast connect timeout. The Godot editor addon lives in a
            // COLLECTIBLE AssemblyLoadContext, so an UNREACHABLE server retried forever would keep a negotiate
            // in-flight and pin the ALC on a C# hot-reload (godotengine/godot#78513). Giving up after a few
            // failures (and failing the connect fast) settles the connection into idle-Disconnected so reloads are
            // clean; the dock can reconnect once the server is up. These are McpPlugin defaults of 0 (= unlimited,
            // the historical behaviour Unity/Unreal keep) — we set them here so the opt-in is addon-local.
            _config.MaxConsecutiveConnectionFailures = 4;
            _config.ConnectTimeoutSeconds = 5;

            // Prime the config for McpPlugin 6.10.0's CTOR-time skill generation BEFORE Build() runs. The ctor
            // auto-calls GenerateSkillFilesIfNeeded() at construction; 6.10.0 removed the silent CWD fallback
            // (upstream #107), so with the default RELATIVE SkillsPath ("SKILLS") and an unset ProjectRootPath it
            // throws InvalidOperationException (caught+logged by the ctor, but spamming an ERROR + stack on every
            // boot and skipping the ctor's own generation). Point it at the SAME <project>/.claude/skills
            // destination the post-build MaybeAutoGenerateSkills uses (an absolute SkillsPath removes the throw via
            // ResolveSkillsPath's rooted branch), or disable ctor-time generation when the agent has no skills dir —
            // so no stray SKILLS/ directory is ever written. Pure-managed decision (SkillsBootstrap); the res://
            // resolution is the only Godot dependency.
            SkillsBootstrap.PrimeForCtorSkillGeneration(
                _config,
                GodotAgentConfigurators.GetByAgentId(_config.SelectedAgentId),
                ProjectSettings.GlobalizePath("res://").TrimEnd('/'));

            var builder = new McpPluginBuilder(version, loggerProvider)
                .SetConfig(_config)
                // Prune the heavy assemblies so neither the tool/prompt/resource scan NOR the
                // IReflectorModule discovery below ever type-enumerates them. Mirrors the Unity reference's
                // .IgnoreAssemblies(...) prefix list (BCL, the reused McpPlugin/ReflectorNet/R3/SignalR
                // stack, the Godot engine assemblies, and this repo's own test assembly). This MUST come
                // before .WithReflectorModulesFromAssembly(...) — discovery honors the prune, so an ignored
                // hosting assembly is never walked for modules.
                .IgnoreAssemblies(
                    "mscorlib",
                    "netstandard",
                    "System",
                    "Microsoft",
                    "GodotSharp",
                    "GodotSharpEditor",
                    "Godot.SourceGenerators",
                    "R3",
                    "ObservableCollections",
                    "McpPlugin",
                    "ReflectorNet",
                    "Godot-MCP.Tests")
                .WithToolsFromAssembly(assemblies)
                .WithPromptsFromAssembly(assemblies)
                .WithResourcesFromAssembly(assemblies)
                // Auto-discover IReflectorModule implementors across all loaded assemblies so any
                // assembly (including Godot-MCP extensions added later, unknown ahead of time) can
                // contribute ReflectorNet JSON/reflection converters, serialization-blacklist entries, and
                // scan-ignore rules without a hardcoded extension list — the Godot equivalent of the M8
                // "extension-contributed converters" mechanism. Discovery honors the .IgnoreAssemblies(...)
                // prune above (heavy assemblies are never type-enumerated) and runs strictly before the
                // heavy attribute scan inside Build(). The Godot core converters registered into the
                // reflector by GodotReflectorFactory remain the Order=0 baseline; module contributions
                // layer on top. Mirrors Unity-MCP's UnityMcpPlugin.Build.cs ordering exactly.
                .WithReflectorModulesFromAssembly(assemblies);

            _plugin = builder.Build(reflector);

            // Reapply the persisted per-feature enable-map now that the managers exist (the tool/prompt/
            // resource registries are populated during Build via the assembly scan). A saved disable for a
            // still-registered item is restored; stale entries (renamed/removed) are pruned; items with no
            // saved entry stay at the manager default (enabled). See ReapplyFeatureStates.
            ReapplyFeatureStates(_plugin);

            SubscribeToConnectionState(_plugin);
            SubscribeToFeatureUpdates(_plugin);
            SubscribeToAgentUpdates(_plugin);

            // Auto-generate the selected agent's skills (SKILL.md-per-tool) when the toggle is ON. Runs AFTER the
            // plugin Build so the tool registry the engine reads from is populated. GenerateSkillFilesIfNeeded only
            // regenerates when the on-disk skills are stale/missing, so this is cheap on a warm boot. Gated by the
            // GenerateSkillFiles toggle (ON by default) and a no-op when the selected agent does not support skills.
            MaybeAutoGenerateSkills(_plugin);

            var mode = _config.ActiveMode;
            var host = _config.Host;
            GD.Print($"[Godot-MCP] connecting (mode={mode}, host={host}) ...");

            // Fire-and-forget connect; KeepConnected drives reconnection in the client.
            _ = ConnectAsync();
        }

        /// <summary>
        /// Subscribe to the reused client's <see cref="IConnection.ConnectionState"/> +
        /// <see cref="IConnection.KeepConnected"/> reactive properties and push the reduced
        /// <see cref="ConnectionStatus"/> to <see cref="ConnectionStatusChanged"/> on the editor main
        /// thread. Mirrors the Unity reference's <c>SubscribeToConnectionState</c> (CombineLatest of the
        /// two properties), but marshals via the Godot main-thread dispatcher instead of a Unity
        /// synchronization context. The subscription is stored in a <see cref="SerialDisposable"/> so a
        /// later <see cref="Start"/> (new plugin) replaces it cleanly.
        /// </summary>
        void SubscribeToConnectionState(IMcpPlugin plugin)
        {
            // Seed from current values so the dock's first render is correct even before any change fires.
            // Runs synchronously on the caller (Start, which is on the editor main thread), so it is "inline".
            PublishStatus(ConnectionPanelView.Reduce(plugin.ConnectionState.CurrentValue, plugin.KeepConnected.CurrentValue), marshalled: false);

            _stateSubscription.Disposable = Observable
                .CombineLatest(
                    plugin.ConnectionState,
                    plugin.KeepConnected,
                    (state, keepConnected) => ConnectionPanelView.Reduce(state, keepConnected))
                .Subscribe(status =>
                {
                    // R3 fires off the SignalR thread; hop to the editor main thread before raising the
                    // event so subscribers (the dock) can touch Control nodes directly. A missing
                    // dispatcher (between editor reloads) degrades to a direct call rather than throwing.
                    // Same stale-plugin guard as the auth-rejected subscription below: a status emitted by a
                    // plugin a Reconnect() has already superseded must not overwrite the new plugin's status
                    // (it would briefly flip the label before the re-sync corrects it). Re-checked on the main
                    // thread too, for a callback enqueued just before the swap.
                    if (!ReferenceEquals(_plugin, plugin))
                        return;

                    if (MainThreadDispatcher.Instance != null && !MainThreadDispatcher.IsMainThread)
                        MainThreadDispatcher.Enqueue(() =>
                        {
                            if (ReferenceEquals(_plugin, plugin))
                                PublishStatus(status, marshalled: true);
                        });
                    else if (ReferenceEquals(_plugin, plugin))
                        PublishStatus(status, marshalled: false);
                });

            // Surface the reused client's authorization-rejected stream as a main-thread event the dock can
            // act on (clear the rejected token, revert to Authorize). R3 fires off the SignalR thread, so we
            // hop to the editor main thread before raising, mirroring the status subscription above.
            //
            // STALE-REJECTION GUARD (the "authorized but stays red" bug): in Cloud mode WITHOUT a token the
            // connection sits in a reject→retry loop, so OnAuthorizationRejected fires continuously. When the
            // user authorizes, PersistAuthorizedToken sets CloudToken and calls Reconnect(), which disposes
            // THIS plugin and builds a fresh one with the token. A rejection emitted by this (now-superseded)
            // plugin must NOT reach the dock afterwards — the dock's handler nulls CloudToken, which would wipe
            // the freshly-authorized token and flip the new connection back to "Authorization Required" (red).
            // Guard at BOTH the emit boundary and again on the main thread (a callback enqueued just before the
            // swap must re-check): only raise while this subscription's plugin is still the active _plugin.
            _authRejectedSubscription.Disposable = plugin.OnAuthorizationRejected
                .Subscribe(_ =>
                {
                    if (!ReferenceEquals(_plugin, plugin))
                    {
                        // Superseded by a Reconnect — drop the stale rejection. Traced so a Cloud-auth
                        // re-test can SEE the race being suppressed; if the dock instead logs "server
                        // rejected the authorization token; cleared" AFTER authorizing, that is a GENUINE
                        // rejection from the live connection (token/header/expiry), not this race.
                        LogTrace("[Godot-MCP] dropped a stale authorization-rejection from a superseded connection (post-Reconnect).");
                        return;
                    }

                    if (MainThreadDispatcher.Instance != null && !MainThreadDispatcher.IsMainThread)
                        MainThreadDispatcher.Enqueue(() =>
                        {
                            if (ReferenceEquals(_plugin, plugin))
                                AuthorizationRejected?.Invoke();
                        });
                    else if (ReferenceEquals(_plugin, plugin))
                        AuthorizationRejected?.Invoke();
                });
        }

        /// <summary>
        /// Subscribe to the freshly-built plugin's three feature-manager <c>On*Updated</c> streams (tools /
        /// prompts / resources) and raise the merged <see cref="FeaturesUpdated"/> event on the editor main
        /// thread. The streams fire off the SignalR thread, so each is hopped to the main thread before
        /// raising — mirroring <see cref="SubscribeToConnectionState"/>. Stored in a
        /// <see cref="SerialDisposable"/> so a later <see cref="Start"/> (new managers) replaces it cleanly.
        /// Any kind whose manager is null is simply skipped.
        /// </summary>
        void SubscribeToFeatureUpdates(IMcpPlugin plugin)
        {
            var mgr = plugin.McpManager;
            if (mgr == null)
            {
                _featuresSubscription.Disposable = null;
                return;
            }

            var streams = new List<Observable<Unit>>();
            if (mgr.ToolManager?.OnToolsUpdated is { } toolsUpdated)
                streams.Add(toolsUpdated);
            if (mgr.PromptManager?.OnPromptsUpdated is { } promptsUpdated)
                streams.Add(promptsUpdated);
            if (mgr.ResourceManager?.OnResourcesUpdated is { } resourcesUpdated)
                streams.Add(resourcesUpdated);

            if (streams.Count == 0)
            {
                _featuresSubscription.Disposable = null;
                return;
            }

            _featuresSubscription.Disposable = Observable.Merge(streams)
                .Subscribe(_ => RaiseFeaturesUpdated());
        }

        /// <summary>
        /// Subscribe to the freshly-built plugin's <see cref="IMcpManager.OnClientsChanged"/> stream (fires on an AI
        /// agent connect OR disconnect with the full active-client list) and surface it as the editor-main-thread
        /// <see cref="AgentsUpdated"/> event the dock's AI-agent point refreshes from. Seeds <see cref="_activeAgents"/>
        /// from the manager's current <see cref="IMcpManager.ActiveClients"/> snapshot so a late subscriber (or a
        /// reconnect into an already-populated server) shows agents that connected before this subscription. Stored in
        /// a <see cref="SerialDisposable"/> so a later <see cref="Start"/> replaces it cleanly; a null manager clears
        /// the list. Mirrors <see cref="SubscribeToFeatureUpdates"/>.
        /// </summary>
        void SubscribeToAgentUpdates(IMcpPlugin plugin)
        {
            var mgr = plugin.McpManager;
            if (mgr == null)
            {
                _agentsSubscription.Disposable = null;
                _activeAgents = Array.Empty<McpClientData>();
                return;
            }

            // Seed from the current snapshot (runs inline on Start → editor main thread).
            _activeAgents = mgr.ActiveClients ?? Array.Empty<McpClientData>();

            _agentsSubscription.Disposable = mgr.OnClientsChanged
                .Subscribe(clients =>
                {
                    // Drop emissions from a plugin a Reconnect() has already superseded (mirrors the status/auth
                    // subscriptions): a stale agent-list must not overwrite the new plugin's state.
                    if (!ReferenceEquals(_plugin, plugin))
                        return;
                    UpdateActiveAgents(plugin, clients);
                });
        }

        /// <summary>
        /// Cache the new connected-agent snapshot and raise <see cref="AgentsUpdated"/> — BOTH on the editor main
        /// thread (the R3 stream fires off the SignalR thread), so the panel reads a consistent <see cref="ActiveAgents"/>
        /// when the event fires. Re-checks the stale-plugin guard on the main thread for an emission enqueued just
        /// before a <see cref="Reconnect"/> swap. Mirrors the marshalling discipline of <see cref="PublishStatus"/>.
        /// </summary>
        void UpdateActiveAgents(IMcpPlugin plugin, IReadOnlyList<McpClientData> clients)
        {
            var snapshot = clients ?? Array.Empty<McpClientData>();

            void Apply()
            {
                if (!ReferenceEquals(_plugin, plugin))
                    return;
                _activeAgents = snapshot;
                AgentsUpdated?.Invoke();
            }

            if (MainThreadDispatcher.Instance != null && !MainThreadDispatcher.IsMainThread)
                MainThreadDispatcher.Enqueue(Apply);
            else
                Apply();
        }

        /// <summary>
        /// Update the cached <see cref="ConnectionStatus"/> and raise <see cref="ConnectionStatusChanged"/>
        /// when it actually changed. De-duplicated (via <see cref="_statusTracker"/>) so identical
        /// consecutive states (e.g. the seed plus an immediate first push) do not double-fire the UI. MUST
        /// be called on the editor main thread. <paramref name="marshalled"/> records whether this push was
        /// hopped from the SignalR thread onto the main thread (<c>true</c>) or ran inline on the caller
        /// (<c>false</c>) — surfaced at Trace so the full status path is visible in the Output when
        /// diagnosing a stuck label (issue #42). The de-dup never permanently hides a render: the dock's
        /// periodic re-sync reads <see cref="ConnectionStatus"/> directly.
        /// </summary>
        void PublishStatus(ConnectionStatus status, bool marshalled)
        {
            var previous = _statusTracker.Current;
            if (!_statusTracker.TryAdvance(status))
            {
                LogTrace($"[Godot-MCP] status push (de-duped, no change): {status} ({(marshalled ? "marshalled" : "inline")})");
                return;
            }

            LogTrace($"[Godot-MCP] status: {previous} -> {status} ({(marshalled ? "marshalled to main thread" : "inline")})");
            ConnectionStatusChanged?.Invoke(status);
        }

        /// <summary>
        /// Emit a Trace-level diagnostic line through the same Godot Output + log-collector sink the
        /// framework logs use, gated by the LIVE configured <see cref="GodotMcpConfig.ActiveLogLevel"/> (read
        /// each call so the dock's Log Level dropdown applies without a rebuild). Used for the connection
        /// status path (<see cref="PublishStatus"/>) so a smoke run at Trace shows whether/when
        /// <see cref="ConnectionStatus.Connected"/> is reached and pushed. Never logs secrets — the status
        /// enum carries none.
        /// </summary>
        void LogTrace(string message)
        {
            if (!GodotMcpLogGate.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Trace, _config.ActiveLogLevel))
                return;

            RouteFrameworkLog(GodotLogType.Log, message);
        }

        /// <summary>
        /// Public Trace hook for the dock's <see cref="UI.ConnectionPanel"/> so it can record what status it
        /// actually RENDERED (<c>ApplyStatus rendered status: …</c>) through the same Output + log-collector
        /// sink and the same live <see cref="GodotMcpConfig.ActiveLogLevel"/> gate as the connection's own
        /// status-push traces. Pairing the connection-side <c>status: X -> Y</c> line with the panel-side
        /// <c>rendered status: Y</c> line is what lets a Trace smoke run SEE the terminal Connected render
        /// arrive at the label (the diagnostic for issue #42). Never logs secrets.
        /// </summary>
        public void LogStatusTrace(string message) => LogTrace(message);

        /// <summary>
        /// The Godot sink the <see cref="GodotMcpLoggerProvider"/> routes the reused framework's log lines
        /// to: write to the Godot Output by severity (info/debug/trace → <see cref="GD.Print"/>, warning →
        /// <see cref="GD.PushWarning"/>, error/critical → <see cref="GD.PushError"/>) AND append to
        /// <see cref="GodotLogCollector.Current"/> so <c>console-get-logs</c> captures the framework lines
        /// too. The level decision already happened in the logger (this only runs for enabled lines). The
        /// framework never logs secrets, and this pass-through adds none.
        /// </summary>
        static void RouteFrameworkLog(GodotLogType logType, string message)
        {
            switch (logType)
            {
                case GodotLogType.Error:
                    GD.PushError(message);
                    break;
                case GodotLogType.Warning:
                    GD.PushWarning(message);
                    break;
                default:
                    GD.Print(message);
                    break;
            }

            GodotLogCollector.Current?.Append(logType, message);
        }

        // --- MCP feature enable-map (tools / prompts / resources) -----------------------------------------

        /// <summary>
        /// The per-kind feature manager off the freshly-built plugin, or null when that kind's manager is not
        /// available. The three managers share the same shape (enumerate / count / is-enabled / set-enabled),
        /// so the dock addresses them generically through <see cref="GodotMcpFeatureKind"/>.
        /// </summary>
        IFeatureManagerAdapter? FeatureManager(GodotMcpFeatureKind kind, IMcpPlugin? plugin)
        {
            var mgr = plugin?.McpManager;
            if (mgr == null)
                return null;

            return kind switch
            {
                GodotMcpFeatureKind.Tools => mgr.ToolManager is { } t ? new ToolManagerAdapter(t) : null,
                GodotMcpFeatureKind.Prompts => mgr.PromptManager is { } p ? new PromptManagerAdapter(p) : null,
                GodotMcpFeatureKind.Resources => mgr.ResourceManager is { } r ? new ResourceManagerAdapter(r) : null,
                _ => null
            };
        }

        /// <summary>
        /// Reapply the persisted enable-map to a freshly-built plugin's managers. For each kind, the pure
        /// <see cref="GodotMcpFeatureStateMerge.ComputeReapply"/> decides which live items get an explicit
        /// <c>SetEnabled</c> (saved entries matching a live item) — stale saved names are pruned and items with
        /// no saved entry stay at the manager default (enabled). A null/empty map disables nothing. Runs once
        /// per <see cref="Start"/>, right after the plugin is built.
        /// </summary>
        void ReapplyFeatureStates(IMcpPlugin plugin)
        {
            foreach (GodotMcpFeatureKind kind in Enum.GetValues(typeof(GodotMcpFeatureKind)))
            {
                var manager = FeatureManager(kind, plugin);
                if (manager == null)
                    continue;

                var liveNames = manager.GetNames().ToList();
                var saved = _config.Features.For(kind);
                var toApply = GodotMcpFeatureStateMerge.ComputeReapply(liveNames, saved);

                foreach (var pair in toApply)
                    manager.SetEnabled(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// Auto-generate the selected AI agent's skills (a <c>SKILL.md</c>-per-tool directory) on addon load, gated by
        /// the <see cref="GodotMcpConfig.GenerateSkillFiles"/> toggle. The Godot analog of Unity-MCP's boot-time
        /// auto-generate (<c>MainWindowEditor.AiAgents</c>'s <c>GenerateSkillFiles</c> on configured skills). No-op
        /// when the toggle is OFF or the selected agent does not support skills. Uses the engine's
        /// <see cref="IMcpPlugin.GenerateSkillFilesIfNeeded"/> (which only writes when the on-disk skills are
        /// stale/missing) via the same swap-and-restore pattern the dock's on-demand Generate uses: point the live
        /// config's <c>SkillsPath</c> + <c>ProjectRootPath</c> at the resolved destination, generate, restore. The
        /// only Godot dependencies are the path resolution (<c>OS</c>/<c>ProjectSettings</c>) and the destination
        /// mkdir; the supported/path decision is the pure-managed <see cref="SkillsPlan"/>. Failures are caught and
        /// logged so a generation error never blocks the connection boot.
        /// </summary>
        void MaybeAutoGenerateSkills(IMcpPlugin plugin)
        {
            if (!_config.GenerateSkillFiles)
                return;

            var agent = GodotAgentConfigurators.GetByAgentId(_config.SelectedAgentId);
            var projectRoot = ProjectSettings.GlobalizePath("res://").TrimEnd('/');

            var plan = SkillsPlan.Resolve(agent, projectRoot);
            if (!plan.Supported || string.IsNullOrEmpty(plan.SkillsDir))
                return;

            var skillsDir = plan.SkillsDir!;

            try
            {
                if (!DirAccess.DirExistsAbsolute(skillsDir))
                {
                    var mkdir = DirAccess.MakeDirRecursiveAbsolute(skillsDir);
                    if (mkdir != Error.Ok)
                    {
                        GD.PushWarning($"[Godot-MCP] auto-generate skills: could not create folder {skillsDir} ({mkdir}).");
                        return;
                    }
                }

                var originalSkillsPath = _config.SkillsPath;
                var originalProjectRoot = _config.ProjectRootPath;
                try
                {
                    _config.SkillsPath = skillsDir;
                    _config.ProjectRootPath = projectRoot;
                    plugin.GenerateSkillFilesIfNeeded(skillsDir);
                }
                finally
                {
                    _config.SkillsPath = originalSkillsPath;
                    _config.ProjectRootPath = originalProjectRoot;
                }

                GD.Print($"[Godot-MCP] auto-generate skills: ensured up-to-date skills in {skillsDir}.");
            }
            catch (Exception ex)
            {
                GD.PushError($"[Godot-MCP] auto-generate skills failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Enumerate the live items of a feature kind as pure-managed <see cref="FeatureRowItem"/> view-models
        /// for the list window — the common name/title/description/enabled plus the kind-specific metadata
        /// (tools: token count + input args; prompts: role + arguments; resources: uri + mimetype). Empty when
        /// the plugin/managers are not yet available. Reads the LIVE enabled-state off the manager (which
        /// reflects any reapplied persisted disable). The window filters this list via <see cref="FeatureFilter"/>.
        /// </summary>
        public IReadOnlyList<FeatureRowItem> GetFeatureItems(GodotMcpFeatureKind kind)
        {
            var manager = FeatureManager(kind, _plugin);
            return manager == null
                ? Array.Empty<FeatureRowItem>()
                : manager.GetItems().ToList();
        }

        /// <summary>
        /// Count summary (enabled, total, enabledTokenCount) for a feature kind, or null when the
        /// plugin/managers are not yet available (the panel shows the "—" placeholder then). Only the
        /// <see cref="GodotMcpFeatureKind.Tools"/> kind reports a non-zero token count
        /// (<c>EnabledToolsTokenCount</c>); prompts/resources have no token analog.
        /// </summary>
        public (int Enabled, int Total, int EnabledTokenCount)? GetFeatureCounts(GodotMcpFeatureKind kind)
        {
            var manager = FeatureManager(kind, _plugin);
            return manager?.GetCounts();
        }

        /// <summary>
        /// Toggle one item's enabled-state: push it to the live manager AND patch + persist the enable-map so
        /// the choice survives a restart. No-op (returns false) when the manager is unavailable. The map is
        /// patched via the pure <see cref="GodotMcpFeatureStateMerge.Upsert"/> then <see cref="Save"/>d.
        /// </summary>
        public bool SetFeatureEnabled(GodotMcpFeatureKind kind, string name, bool enabled)
        {
            var manager = FeatureManager(kind, _plugin);
            if (manager == null)
                return false;

            manager.SetEnabled(name, enabled);
            GodotMcpFeatureStateMerge.Upsert(_config.Features.For(kind), name, enabled);
            Save();

            // Guarantee the dock's features panel recomputes after a per-row toggle (issue #54). Directly
            // owning the refresh here makes the dock-count update a property of the toggle path itself rather
            // than an incidental side effect of how the reused client's Set*Enabled happens to notify: with the
            // pinned McpPlugin 6.7.0 the manager's Set*Enabled DOES fire its On*Updated stream (so the
            // SubscribeToFeatureUpdates path also raises FeaturesUpdated), but that is an upstream
            // implementation detail we must not depend on for a first-party UI invariant — older/newer client
            // versions may treat an enable/disable as not-a-registry-change and stay silent. The raise is
            // kind-generic: one call covers tools/prompts/resources because the panel re-reads every kind's
            // counts. A duplicate refresh (when On*Updated also fires) is harmless — RefreshAll only re-reads
            // counts and re-sets label text (idempotent), and the list window does not subscribe to
            // FeaturesUpdated (it re-filters locally), so there is no feedback loop.
            RaiseFeaturesUpdated();
            return true;
        }

        /// <summary>
        /// Raise <see cref="FeaturesUpdated"/> on the editor main thread, mirroring the same dispatcher-guard
        /// the managers' <c>On*Updated</c> subscription uses (see <see cref="SubscribeToFeatureUpdates"/>): hop
        /// to the main thread when off it (and a dispatcher is in the tree), else invoke inline. The toggle
        /// path (<see cref="SetFeatureEnabled"/>) already runs on the editor main thread, so it takes the inline
        /// branch — but routing through the same guard keeps the event's "always marshalled onto the main
        /// thread" contract true regardless of which thread a future caller raises from.
        /// </summary>
        void RaiseFeaturesUpdated()
        {
            if (MainThreadDispatcher.Instance != null && !MainThreadDispatcher.IsMainThread)
                MainThreadDispatcher.Enqueue(() => FeaturesUpdated?.Invoke());
            else
                FeaturesUpdated?.Invoke();
        }

        /// <summary>
        /// (Re)connect with the CURRENT <see cref="GodotMcpConfig"/> (mode/host/token). If a plugin
        /// already exists it is reused — the reused client re-arms its own <c>KeepConnected</c> intent
        /// inside <see cref="IConnection.Connect"/>; otherwise this builds a fresh plugin via
        /// <see cref="Start"/>. The boot path calls <see cref="Start"/> directly; the dock's Connect
        /// button calls this. Idempotent and safe to call repeatedly.
        ///
        /// <para>
        /// Auto-reconnect intent lives in the reused client's <see cref="IConnection.KeepConnected"/>
        /// reactive property, NOT in <see cref="GodotMcpConfig.KeepConnected"/>: the McpPlugin
        /// <c>ConnectionManager</c> sets its internal reconnect flag to <c>true</c> inside
        /// <see cref="IConnection.Connect"/> and to <c>false</c> inside <see cref="IConnection.Disconnect"/>;
        /// it never reads the <see cref="ConnectionConfig.KeepConnected"/> field at runtime. Calling
        /// <c>plugin.Connect()</c> is therefore what re-arms reconnection after a manual Disconnect — this
        /// is the exact mechanism the Unity reference uses (its <c>UnityMcpPluginEditor.KeepConnected</c>
        /// gates only the BOOT path's <c>ConnectIfNeeded</c>, while the live client is driven by
        /// <c>mcpPlugin.Connect()</c> / <c>mcpPlugin.Disconnect()</c>).
        /// </para>
        /// </summary>
        public void Connect()
        {
            // Keep the persisted/boot intent aligned with the user's explicit Connect (boot honours this
            // via Start()'s ConnectIfNeeded analogue); the LIVE reconnect re-arm happens inside
            // plugin.Connect() below, which flips the client's own KeepConnected to true.
            _config.KeepConnected = true;

            if (_plugin == null)
            {
                Start();
                return;
            }

            _ = ConnectAsync();
        }

        /// <summary>
        /// Disconnect from the MCP server and stop auto-reconnect. The reconnect-stop is driven by
        /// <see cref="IConnection.Disconnect"/> on the reused client — it cancels the in-flight connect
        /// loop's token, flips the client's own <see cref="IConnection.KeepConnected"/> reactive property
        /// to <c>false</c>, and stops + disposes the live <c>HubConnection</c> — so the client does NOT
        /// auto-reconnect and the status subscription (which reduces over <c>plugin.KeepConnected</c>)
        /// settles on <see cref="UI.ConnectionStatus.Disconnected"/> (gray). Setting
        /// <see cref="GodotMcpConfig.KeepConnected"/> here is intent bookkeeping only (the client never
        /// reads that field at runtime) and keeps the boot/persisted intent consistent. The plugin
        /// instance is kept (not disposed) so a subsequent <see cref="Connect"/> can reuse it; full
        /// teardown happens in <see cref="Dispose"/> at plugin unload.
        /// </summary>
        public void Disconnect()
        {
            // Bookkeeping only — the client's live reconnect intent is cleared by plugin.Disconnect()
            // below (ConnectionManager sets its internal KeepConnected=false there), not by this field.
            _config.KeepConnected = false;

            var plugin = _plugin;
            if (plugin == null)
                return;

            _ = DisconnectAsync(plugin);
        }

        /// <summary>
        /// Apply the current config (mode / host / token) by rebuilding the connection from scratch:
        /// dispose the existing plugin and <see cref="Start"/> a fresh one. Used by the dock when the user
        /// changes connection mode or the server URL — those alter the resolved <see cref="GodotMcpConfig.Host"/>,
        /// and the cleanest way to re-point the SignalR client at a new host is a fresh build (mirrors the
        /// Unity reference's dispose-then-rebuild on host/mode change). Re-arms <c>KeepConnected</c>.
        /// </summary>
        public void Reconnect()
        {
            _config.KeepConnected = true;
            DisposePlugin();

            // Reset the status baseline so the NEW plugin's seed (pushed from Start → SubscribeToConnectionState)
            // is free to advance from Disconnected rather than being de-duped against a stale value carried over
            // from the disposed plugin. Without this, a Reconnect that lands back on the same reduced status as
            // before would not re-fire ConnectionStatusChanged — the periodic re-sync still converges the label,
            // but resetting keeps the event path correct too. Done on the main thread (callers are UI handlers).
            if (_statusTracker.Reset())
                ConnectionStatusChanged?.Invoke(_statusTracker.Current);

            Start();
        }

        async Task DisconnectAsync(IMcpPlugin plugin)
        {
            try
            {
                await plugin.Disconnect();
                GD.Print("[Godot-MCP] disconnected.");
            }
            catch (Exception ex)
            {
                GD.PushError($"[Godot-MCP] disconnect failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply the config precedence layers — persisted serialized config, then the project-root <c>.env</c> —
        /// onto <see cref="_config"/>, beneath the live process-env overrides the config getters apply. Idempotent
        /// and guarded by <see cref="_configResolved"/>: safe to call repeatedly, runs the file reads only once.
        ///
        /// <para>
        /// CRITICAL ORDERING: the plugin boot (<c>GodotMcpPlugin.BootMcp</c>) calls this BEFORE constructing the
        /// dock so the connection / AI-agent panels read the SAVED connection mode, cloud/custom token, auth
        /// option, and selected agent at build time — not the built-in defaults. Building the dock first (the old
        /// order) painted the UI from defaults (Cloud with an EMPTY cloud token → "Authorize" prompt; the
        /// default <c>claude-code</c> agent instead of the persisted one), and only a later mode-switch — which
        /// re-reads the by-then-loaded config — "healed" the display. Resolving here first removes that whole
        /// class of first-load staleness.
        /// </para>
        /// </summary>
        public void ResolveConfig()
        {
            if (_configResolved)
                return;
            _configResolved = true;

            // PRECEDENCE: process env > .env file > persisted config > built-in default.
            // 1) Seed the SERIALIZED-config layer first (lowest layer above the built-in defaults), so the
            //    .env and live process-env layers below override it — never the other way around.
            ApplyPersistedConfig();

            // 2) Layer the project-root `.env` BENEATH the live process-env overrides the config already
            //    applies in its getters. Godot is launched from the GUI (no inherited shell exports), so a
            //    committed `res://.env` is how a project self-configures its MCP host/token. Process env
            //    still wins because GodotMcpConfig reads it live on every Host/Token/ActiveMode access —
            //    see GodotMcpEnvFile's precedence note.
            ApplyProjectEnvFile();
        }

        /// <summary>
        /// Resolve the project-root <c>.env</c> (<c>res://.env</c> → absolute via
        /// <see cref="ProjectSettings.GlobalizePath(string)"/>) and apply its recognized
        /// <c>GODOT_MCP_*</c> values to <see cref="_config"/> beneath the process-env layer. The native
        /// <c>ProjectSettings</c> call is the only Godot dependency; the parse/apply core is pure-managed
        /// (<see cref="GodotMcpEnvFile"/>). A missing file is a silent no-op.
        /// </summary>
        void ApplyProjectEnvFile()
        {
            string envPath;
            try
            {
                envPath = ProjectSettings.GlobalizePath("res://.env");
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[Godot-MCP] could not resolve res://.env path: {ex.Message}");
                return;
            }

            var values = GodotMcpEnvFile.LoadFile(envPath);
            if (values.Count == 0)
                return;

            GodotMcpEnvFile.Apply(_config, values);
            GD.Print($"[Godot-MCP] applied {values.Count} setting(s) from project .env ({envPath}).");
        }

        /// <summary>
        /// Absolute on-disk path of the persisted config file, resolved from <see cref="ConfigUserPath"/>.
        /// The only Godot dependency of the persistence path; the load/save core
        /// (<see cref="GodotMcpConfigStore"/>) is pure-managed.
        /// </summary>
        public string ConfigFilePath => ProjectSettings.GlobalizePath(ConfigUserPath);

        /// <summary>
        /// Load the persisted config (serialized layer) from <see cref="ConfigFilePath"/> and seed the
        /// active <see cref="_config"/>'s serialized backing fields from it — BENEATH the <c>.env</c> and
        /// process-env layers applied afterwards. A missing/corrupt file is a silent no-op (Load returns
        /// null). This is the persisted half of the precedence chain documented on
        /// <see cref="GodotMcpConfigStore"/>.
        /// </summary>
        void ApplyPersistedConfig()
        {
            string path;
            try
            {
                path = ConfigFilePath;
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[Godot-MCP] could not resolve persisted config path: {ex.Message}");
                return;
            }

            var persisted = GodotMcpConfigStore.Load(path);
            if (persisted == null)
                return;

            GodotMcpConfigStore.ApplyPersisted(_config, persisted);
            GD.Print($"[Godot-MCP] loaded persisted config ({path}).");
        }

        /// <summary>
        /// Persist the active config's serialized layer to <see cref="ConfigFilePath"/>. Invoked by the
        /// dock / connection when the user edits a setting (later UI tasks wire the edit callbacks). IO
        /// failures are caught and logged so a transient save error does not break the editor session.
        /// </summary>
        public void Save()
        {
            try
            {
                GodotMcpConfigStore.Save(ConfigFilePath, _config);
                GD.Print($"[Godot-MCP] saved config ({ConfigFilePath}).");
            }
            catch (Exception ex)
            {
                GD.PushError($"[Godot-MCP] failed to save config: {ex.Message}");
            }
        }

        async Task ConnectAsync()
        {
            var plugin = _plugin;
            if (plugin == null)
                return;

            try
            {
                var ok = await plugin.Connect();

                // A Reconnect()/Dispose() may have superseded THIS plugin while its connect was in flight (e.g. a
                // rapid Custom<->Cloud mode switch). The new plugin drives its own connect — don't report on a
                // plugin we no longer own.
                if (!ReferenceEquals(_plugin, plugin))
                    return;

                if (ok)
                    GD.Print("[Godot-MCP] connected.");
                else
                    GD.PushWarning("[Godot-MCP] initial connect returned false; client will keep retrying if KeepConnected.");
            }
            catch (ObjectDisposedException)
            {
                // Expected teardown race, NOT a failure: a concurrent Reconnect()/Dispose() disposed this plugin
                // (and its internal SemaphoreSlim gate) while the connect above was awaiting. The replacement
                // plugin, if any, runs its own ConnectAsync. Swallow quietly so a fast mode switch does not log a
                // spurious "connect failed: Cannot access a disposed object." error.
            }
            catch (Exception ex)
            {
                // If this plugin was superseded mid-connect, any resulting exception is part of that teardown
                // race — ignore it; only a failure on the CURRENT plugin is a real connect error.
                if (!ReferenceEquals(_plugin, plugin))
                    return;

                GD.PushError($"[Godot-MCP] connect failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Reload-safe, fully-SYNCHRONOUS disconnect for the editor hot-reload / assembly-unload path. Calls
        /// the reused client's <see cref="IConnection.DisconnectImmediate"/> (exposed on
        /// <see cref="IMcpPlugin"/> via the McpPlugin package): it cancels the internal CTS, flips the
        /// client's own <c>KeepConnected</c>/reconnect intent to <c>false</c> (killing the auto-reconnect
        /// loop), and fire-and-forgets <c>HubConnection.DisposeAsync()</c> — spawning NO awaited Task. This
        /// is the LOAD-BEARING step that lets the addon's collectible <see cref="System.Runtime.Loader.AssemblyLoadContext"/>
        /// actually unload on a Godot "Build Project": the live SignalR connection (HubConnection +
        /// HttpClient + reconnect loop + R3 subscriptions) otherwise keeps running threads / strong GC
        /// handles that pin the context open, producing <c>Failed to unload assemblies</c> +
        /// <c>delegate_handle.value == nullptr</c>. Deliberately does NOT call the graceful async
        /// <see cref="Disconnect"/> path — awaiting it can deadlock the unload thread. Idempotent and
        /// defensively wrapped: safe to call when no plugin exists or when already torn down. Mirrors the
        /// Unity reference's <c>Startup.TryDisconnectAndCleanup</c> → <c>plugin.DisconnectImmediate()</c>.
        /// </summary>
        public void DisconnectImmediate()
        {
            var plugin = _plugin;
            if (plugin == null)
                return;

            try
            {
                plugin.DisconnectImmediate();
            }
            catch (Exception ex)
            {
                // Emergency-shutdown path: never let a disconnect failure escape into the ALC-unloading
                // handler (an exception there would abort the very unload we are trying to enable).
                try { GD.PushError($"[Godot-MCP] immediate disconnect failed: {ex.Message}"); }
                catch { /* GD may be unavailable mid-reload; swallow */ }
            }
        }

        /// <summary>
        /// Reload-safe, best-effort teardown for the Godot "Build Project" hot-reload / assembly-unload path,
        /// built ONLY on the McpPlugin 6.7.0 public API. Cancels the live connection via the reused client's
        /// synchronous <see cref="IConnection.DisconnectImmediate"/> (kills the auto-reconnect loop + fire-and-
        /// forgets the HubConnection teardown) and then disposes the plugin (releases R3 subscriptions + the
        /// manager). Both steps are independently try/catch-wrapped and never throw — safe to call from the
        /// ALC-unloading handler, where a throw would abort the very unload it is trying to enable.
        ///
        /// <para>
        /// godot#78513 is MULTI-ROOT. One root is the connection transport: <see cref="IConnection.DisconnectImmediate"/>
        /// only DISPATCHES the HubConnection teardown (it does not wait), so the SignalR HttpClient /
        /// SocketsHttpHandler connection-pool timer + WebSocket receive loop are still running when the
        /// collectible <see cref="System.Runtime.Loader.AssemblyLoadContext"/> unloads — pinning it. The other
        /// root (ReflectorNet's static MainThread.Instance) is cleared in <c>GodotMcpPlugin.Teardown</c>.
        /// This method bounded-JOINS the dispatched teardown via <see cref="IConnection.WaitForImmediateTeardown"/>
        /// (McpPlugin 6.8.x), so the transport's threads/handles are released BEFORE the unload. The disposal
        /// runs on the thread pool, so the bounded main-thread wait cannot deadlock the editor main = unload
        /// thread. <paramref name="timeout"/> bounds the wait (a reachable/refused endpoint clears in a few ms;
        /// a black-holed host is capped at the timeout).
        /// </para>
        ///
        /// Idempotent and defensively wrapped: safe to call with no plugin (no-op) and never throws into the
        /// ALC-unloading handler.
        /// </summary>
        /// <param name="timeout">Upper bound on the wait for the dispatched connection teardown to complete.</param>
        public void DisconnectAndDrain(TimeSpan timeout)
        {
            var plugin = _plugin;
            if (plugin == null)
                return;

            // 1) Synchronous immediate disconnect: cancels the CTS, flips KeepConnected to false (killing the
            //    auto-reconnect loop), and DISPATCHES the HubConnection teardown to a background thread.
            try { plugin.DisconnectImmediate(); }
            catch (Exception ex)
            {
                try { GD.PushError($"[Godot-MCP] immediate disconnect (drain) failed: {ex.Message}"); }
                catch { /* GD may be unavailable mid-reload; swallow */ }
            }

            // 1.5) Bounded-JOIN that dispatched teardown BEFORE Dispose (which would tear down the manager and
            //      abandon the task) and before the ALC unload — so the SignalR transport's running threads /
            //      GC handles are actually released, not just signalled. Non-blocking-on-async (thread-pool
            //      task), so no deadlock on the editor main = unload thread. THE connection root of godot#78513.
            //
            //      WaitForImmediateTeardown returns a bool: true = drained within the bound (or nothing pending /
            //      already disposed); false = the bounded wait TIMED OUT (a black-holed host that never finished
            //      the dispatched teardown) or faulted. On a timeout we still proceed with Dispose + the ALC
            //      unload (the correct bounded behaviour — the timeout value + proceed-anyway semantics are
            //      UNCHANGED), but proceeding silently re-introduces the godot#78513 symptom (live transport
            //      threads/handles may briefly outlive the reload). Surface that as exactly ONE diagnostic
            //      WARNING (not an error — proceeding is still correct) via the pure-managed, unit-testable
            //      GodotMcpDrainDiagnostics seam; the completion path emits nothing. The warning itself is
            //      defensively wrapped (the seam swallows a faulting sink) so it never throws into the unload.
            bool drained = true;
            try { drained = plugin.WaitForImmediateTeardown(timeout); }
            catch (Exception ex)
            {
                drained = false; // treat a fault like a timeout — the teardown did not provably drain.
                try { GD.PushError($"[Godot-MCP] wait-for-immediate-teardown (drain) failed: {ex.Message}"); }
                catch { /* GD may be unavailable mid-reload; swallow */ }
            }
            GodotMcpDrainDiagnostics.ReportDrainResult(drained, timeout);

            // 2) Final plugin Dispose (idempotent): releases the R3 subscriptions + the manager.
            try { plugin.Dispose(); }
            catch (Exception ex)
            {
                try { GD.PushError($"[Godot-MCP] plugin dispose (drain) failed: {ex.Message}"); }
                catch { /* GD may be unavailable mid-reload; swallow */ }
            }
        }

        public void Dispose()
        {
            _stateSubscription.Dispose();
            _authRejectedSubscription.Dispose();
            _featuresSubscription.Dispose();
            _agentsSubscription.Dispose();
            DisposePlugin();
        }

        /// <summary>
        /// Tear down the current plugin instance (and the ambient reflector it published) without
        /// disposing the long-lived <see cref="_stateSubscription"/> holder — so <see cref="Reconnect"/>
        /// can rebuild a fresh plugin. The <see cref="SerialDisposable"/>'s CURRENT inner subscription is
        /// released here (the next <see cref="Start"/> reassigns it); the holder itself is only disposed in
        /// <see cref="Dispose"/>.
        /// </summary>
        void DisposePlugin()
        {
            // Release the live state + auth-rejected + features subscriptions' inner disposables (keeps the
            // SerialDisposables reusable for the next Start()).
            _stateSubscription.Disposable = null;
            _authRejectedSubscription.Disposable = null;
            _featuresSubscription.Disposable = null;
            _agentsSubscription.Disposable = null;
            _activeAgents = Array.Empty<McpClientData>();

            // Clear the ambient reflector if it is the one we published, so a stale instance does not
            // outlive the connection that owned it.
            if (ReferenceEquals(GodotMcpReflector.Current, _publishedReflector))
                GodotMcpReflector.Current = null;
            _publishedReflector = null;

            if (_plugin != null)
            {
                try
                {
                    // Reload-safe disconnect BEFORE Dispose: cancel the CTS, kill the reconnect loop, and
                    // tear down the HubConnection synchronously so no running thread / GC handle survives to
                    // pin a collectible ALC open. Cheap and idempotent when already disconnected. Doing it
                    // here (not only in the unload handler) means EVERY teardown path — _ExitTree, Reconnect,
                    // ALC-unloading — gets the immediate disconnect, so the symptom can never depend on which
                    // path tore the plugin down.
                    _plugin.DisconnectImmediate();
                }
                catch (Exception ex)
                {
                    try { GD.PushError($"[Godot-MCP] immediate disconnect (pre-dispose) failed: {ex.Message}"); }
                    catch { /* GD may be unavailable mid-reload */ }
                }

                try
                {
                    _plugin.Dispose();
                }
                catch (Exception ex)
                {
                    GD.PushError($"[Godot-MCP] error disposing connection: {ex.Message}");
                }
                _plugin = null;
            }
        }
    }
}
