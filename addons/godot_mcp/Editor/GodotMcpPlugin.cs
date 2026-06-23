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
using System.Runtime.CompilerServices;
using Godot;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.Connection.DevControl;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.MainThreadDispatch;
using com.IvanMurzak.Godot.MCP.Tools;
using com.IvanMurzak.Godot.MCP.UI;

namespace com.IvanMurzak.Godot.MCP
{
    /// <summary>
    /// Editor entry point for the Godot-MCP addon. Referenced by
    /// <c>addons/godot_mcp/plugin.cfg</c> (the <c>script</c> field) and loaded by the
    /// Godot Editor when the plugin is enabled.
    ///
    /// On load it installs the editor main-thread dispatcher (the Godot analog of Unity's
    /// <c>MainThread.Instance.Run</c>) so downstream tool handlers can marshal Godot API calls
    /// onto the main thread, then boots the MCP connection (SignalR client via
    /// com.IvanMurzak.McpPlugin) to the configured server (cloud ai-game.dev by default, or a
    /// custom override) and registers the addon's MCP tools.
    /// </summary>
    [Tool]
    public partial class GodotMcpPlugin : EditorPlugin
    {
        const string DispatcherNodeName = "GodotMcpMainThreadDispatcher";

        MainThreadDispatcher? _dispatcher;
        GodotMcpConnection? _connection;
        GodotMcpDock? _dock;

        // DEV-ONLY inject/control HTTP bridge (Connection/DevControl/). Started ONLY when
        // GODOT_MCP_DEV_CONTROL=1 (127.0.0.1, off by default → a shipped addon never listens); disposed in
        // _ExitTree. Lets a terminal / AI agent inject fake states + simulate user actions on the live dock.
        DevControlServer? _devControl;

        // Guards Teardown() so the body runs at most once per plugin instance — it is reachable from BOTH
        // _ExitTree (the normal plugin-disable path) AND the GodotMcpAssemblyResolver.ReloadTeardown hook
        // (the Godot "Build Project" hot-reload path, which never calls _ExitTree). Whichever fires first
        // wins; the second is a no-op. Not volatile/locked: both entry points run on the editor main thread
        // (Godot raises the ALC Unloading event on the main thread, and _ExitTree is a main-thread callback),
        // so there is no cross-thread race to protect against.
        bool _torndown;

        // The live plugin instance the static ReloadTeardown closure reaches. Set in _EnterTree, cleared in
        // Teardown. The ALC-unloading hook (a static event handler in GodotMcpAssemblyResolver) has no
        // instance to call into otherwise — this is the Godot analog of Unity's static
        // UnityMcpPluginRuntime.Instance. Only the most-recent _EnterTree owns it; a stale instance never
        // overwrites a newer one because _EnterTree always sets Current = this last.
        static GodotMcpPlugin? Current;

        /// <summary>
        /// Constructor — the load-bearing seam for surviving a Godot "Build Project" hot-reload
        /// (godotengine/godot#51626). A C# rebuild re-instantiates this <see cref="EditorPlugin"/> script
        /// IN PLACE: the native plugin node never leaves the editor tree, so <see cref="_EnterTree"/> is NOT
        /// called again — yet <see cref="Teardown"/> (run from the ALC-unloading hook) already freed the dock.
        /// The result, with no compensating action, is the "AI Game Developer" tab silently disappearing until
        /// the addon is disabled/re-enabled (the user-reported bug).
        ///
        /// <para>
        /// We distinguish the two paths by <see cref="Godot.Node.IsInsideTree"/> at ctor time:
        /// <list type="bullet">
        ///   <item>NORMAL first boot — Godot constructs the plugin BEFORE adding it to the editor tree, so
        ///   <c>IsInsideTree()</c> is <c>false</c>; <see cref="_EnterTree"/> performs the boot. The ctor does
        ///   nothing.</item>
        ///   <item>HOT-RELOAD re-instantiation — the node is ALREADY in the tree, so <c>IsInsideTree()</c> is
        ///   <c>true</c>; <see cref="_EnterTree"/> will not fire. We re-run the boot (which re-adds the dock +
        ///   dev-control bridge) deferred to the next idle frame, because <see cref="EditorPlugin.AddControlToDock"/>
        ///   requires the plugin to be live in the editor tree.</item>
        /// </list>
        /// Verified empirically on a headless 4.5.1 editor: on reload the ctor runs with
        /// <c>IsInsideTree()==true</c> while <c>_EnterTree</c> does not re-fire; deferring the boot re-registers
        /// the dock and the "AI Game Developer" tab returns.
        /// </para>
        /// </summary>
        public GodotMcpPlugin()
        {
            if (IsInsideTree())
            {
                Log("[Godot-MCP] reload re-instantiation detected — re-registering dock on next idle frame.");
                // Defer to the next idle frame: the plugin is in-tree but mid-reload; AddControlToDock and the
                // dock subtree build are safest once the reload settles. Callable.From → CallDeferred avoids a
                // captured-lambda delegate connection (consistent with the dock's object+method Callable style).
                Callable.From(BootAfterReload).CallDeferred();
            }
        }

        /// <summary>
        /// Re-run the full editor boot after a hot-reload re-instantiation (see the constructor). Bails if the
        /// plugin was removed from the tree before the deferred call landed. Routes through the SAME
        /// <see cref="EnterTreeBoot"/> the normal <see cref="_EnterTree"/> path uses, so a reloaded plugin is
        /// indistinguishable from a freshly-booted one (resolver + dispatcher + connection + dock + bridge).
        /// </summary>
        void BootAfterReload()
        {
            if (!IsInsideTree())
                return; // removed before the deferred frame — nothing to boot
            EnterTreeBoot();
        }

        public override void _EnterTree()
        {
            EnterTreeBoot();
        }

        /// <summary>
        /// The shared boot body, reused by the normal <see cref="_EnterTree"/> path AND the hot-reload
        /// re-entry path (<see cref="BootAfterReload"/>). Installs the log collector + assembly resolver, arms
        /// the reload-safe teardown, builds the dispatcher, then <see cref="BootMcp"/> (connection + dock +
        /// dev-control bridge).
        /// </summary>
        void EnterTreeBoot()
        {
            // Install the process-wide log collector first so every lifecycle line below (resolver
            // probes, plugin-loaded, connection state) is captured for the 'console-get-logs' tool.
            // Godot's C# API exposes no global managed log hook, so the collector is fed explicitly by
            // the plugin's own logging path (Log/LogWarning/LogError helpers) — see GodotLogCollector.
            GodotLogCollector.Current = new GodotLogCollector();

            // Tap the engine's global error stream (Godot 4.5+ OS.AddLogger) so engine-wide GDScript parse
            // errors land in 'console-get-logs' passively AND can be harvested on demand by 'script-validate'.
            // A no-op on Godot < 4.5 (the bridge's #else stub returns null) — script-validate then falls back
            // to a per-file Reload() error-code probe. Wrapped defensively: a logger-registration failure must
            // not take down plugin load.
            try
            {
                GodotScriptErrorLoggerBridge.TryInstall(GodotLogCollector.Current);
            }
            catch (System.Exception ex)
            {
                LogWarning($"[Godot-MCP] engine error-logger not installed: {ex.Message}");
            }

            // FIRST: teach the editor's default AssemblyLoadContext how to find the addon's transitive
            // NuGet dependency assemblies (ReflectorNet / McpPlugin / ...). Godot does not probe the
            // project's *.deps.json, so without this hook the first touch of a NuGet-dependency type
            // throws FileNotFoundException at runtime. The resolver references only BCL types, so
            // installing it here does NOT prematurely load the very assemblies it resolves — it must
            // run before any code path (BootMcp below) reaches a NuGet-dependency type. See
            // GodotMcpAssemblyResolver for the full rationale.
            GodotMcpAssemblyResolver.Log = msg => Log(msg);
            GodotMcpAssemblyResolver.Install();

            // Arm the reload-safe teardown for the Godot "Build Project" hot-reload path. A C# rebuild
            // raises an AssemblyLoadContext unload WITHOUT first calling this EditorPlugin's _ExitTree, so
            // the connection threads + GC handles that pin the (collectible) addon ALC open would otherwise
            // never be released — producing "Failed to unload assemblies" and a flood of
            // "delegate_handle.value == nullptr". GodotMcpAssemblyResolver subscribes the ALC's Unloading
            // event (from its ModuleInitializer) and invokes ReloadTeardown when it fires. We point it at
            // THIS instance's Teardown via Current; the same Teardown also runs from _ExitTree, guarded so it
            // executes at most once. Set Current LAST so this instance owns the static hook.
            Current = this;
            _torndown = false;
            GodotMcpAssemblyResolver.ReloadTeardown = static () => Current?.Teardown(fromReload: true);

            // Pump for off-thread → main-thread work. Added as a child of this EditorPlugin Node so it
            // lives in the editor SceneTree and gets _Process ticks for the lifetime of the plugin.
            // FreeStaleNode first so the hot-reload re-entry (BootAfterReload, deferred to a later idle
            // frame) is idempotent even if the ALC-unload Teardown has not yet freed the prior instance's
            // dispatcher/dock on this matrix version: re-creating without freeing would orphan the old
            // dispatcher child + add a duplicate. (The normal first boot has nothing to free.)
            FreeStaleNode(ref _dispatcher);
            _dispatcher = new MainThreadDispatcher { Name = DispatcherNodeName };
            AddChild(_dispatcher);

            // Route ReflectorNet's MainThread.Instance through the dispatcher.
            GodotMainThread.Install();

            Log("[Godot-MCP] plugin loaded");

            // Build the connection, register the dock (wired to it), and boot the MCP connection. All three
            // touch NuGet-dependency types (McpPlugin / ReflectorNet), so they live in a single non-inlined
            // method invoked AFTER the assembly resolver is installed above — see BootMcp's remarks.
            BootMcp();
        }

        /// <summary>
        /// Instantiate the <see cref="GodotMcpDock"/> (wired to <paramref name="connection"/>) and add it to
        /// an editor dock slot. Isolated and defensively wrapped so a UI failure cannot take down plugin
        /// load or the connection boot — the dock is additive scaffolding, not load-bearing for the MCP path.
        /// </summary>
        void RegisterDock(GodotMcpConnection? connection)
        {
            try
            {
                // Idempotent across the hot-reload re-entry path (BootAfterReload): if a prior dock is
                // still live (Teardown not yet run on this matrix version when the deferred boot lands),
                // remove + free it before re-creating so we never add a SECOND "AI Game Developer" control.
                if (_dock != null)
                {
                    if (GodotObject.IsInstanceValid(_dock))
                        RemoveControlFromDocks(_dock);
                    FreeStaleNode(ref _dock);
                }
                _dock = new GodotMcpDock(connection);
                AddControlToDock(DockSlot.RightUl, _dock);
            }
            catch (System.Exception ex)
            {
                LogError($"[Godot-MCP] failed to register editor dock: {ex.Message}");
                _dock = null;
            }
        }

        /// <summary>
        /// Print an informational lifecycle line to the Godot output AND capture it into the
        /// <see cref="GodotLogCollector"/> so it is retrievable via the <c>console-get-logs</c> tool.
        /// </summary>
        static void Log(string message)
        {
            GD.Print(message);
            GodotLogCollector.Current?.Append(GodotLogType.Log, message);
        }

        /// <summary>Warning-level analog of <see cref="Log"/>.</summary>
        static void LogWarning(string message)
        {
            GD.PushWarning(message);
            GodotLogCollector.Current?.Append(GodotLogType.Warning, message);
        }

        /// <summary>Error-level analog of <see cref="Log"/>.</summary>
        static void LogError(string message)
        {
            GD.PushError(message);
            GodotLogCollector.Current?.Append(GodotLogType.Error, message);
        }

        /// <summary>
        /// Build the connection, register the dock wired to it, and boot the MCP connection. Isolated in
        /// its own non-inlined method so the JIT does not resolve the NuGet-dependency types it (and the
        /// dock's <c>ConnectionPanel</c>) reference until AFTER <see cref="GodotMcpAssemblyResolver"/> has
        /// been installed by <see cref="_EnterTree"/>. (Type references are resolved when a method is
        /// JIT-compiled; keeping this out of <c>_EnterTree</c> guarantees the resolver wins the race.)
        /// Dock registration runs even if the connection construction throws, so a UI failure and a
        /// connection failure are independent — each is caught and logged, keeping the editor usable.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        void BootMcp()
        {
            try
            {
                _connection = new GodotMcpConnection();

                // Resolve the persisted + .env config layers BEFORE building the dock UI. The connection /
                // AI-agent panels read the config at construction time, so the saved connection mode, cloud/
                // custom token, auth option, and selected agent must already be loaded — otherwise the dock
                // paints from built-in defaults (Cloud with an EMPTY cloud token → spurious "Authorize" prompt;
                // the default agent instead of the persisted one) until a later mode-switch heals it. See
                // GodotMcpConnection.ResolveConfig. Start() re-calls it idempotently (guarded), so this is the
                // single source of the first-load config; a resolve failure must not block dock/boot.
                _connection.ResolveConfig();
            }
            catch (System.Exception ex)
            {
                LogError($"[Godot-MCP] failed to create MCP connection: {ex.Message}");
                _connection = null;
            }

            // Register the dock (wired to the connection if one was built; header-only otherwise).
            RegisterDock(_connection);

            // DEV-ONLY: start the inject/control bridge AFTER the dock exists, gated on GODOT_MCP_DEV_CONTROL=1
            // (off by default → a shipped addon never listens). Binds 127.0.0.1 only. A failure here must not
            // take down the connection boot — the bridge is dev scaffolding.
            StartDevControlIfEnabled();

            if (_connection == null)
                return;

            try
            {
                _connection.Start();
            }
            catch (System.Exception ex)
            {
                LogError($"[Godot-MCP] failed to start MCP connection: {ex.Message}");
            }
        }

        /// <summary>
        /// Start the DEV-ONLY inject/control bridge when <c>GODOT_MCP_DEV_CONTROL=1</c>. The port comes from
        /// <c>GODOT_MCP_DEV_CONTROL_PORT</c> (default <see cref="DevControlServer.DefaultPort"/>) — an unset or
        /// unparseable value falls back to the default. No-op (and never listens) when the flag is not exactly
        /// <c>"1"</c>, OR when the dock failed to register (there is nothing to drive). Defensively wrapped so a
        /// bridge failure cannot take down plugin boot.
        /// </summary>
        void StartDevControlIfEnabled()
        {
            // Resolve with precedence process env > project-root .env > default — so a developer can enable the
            // bridge by dropping the flag into the project's `.env` (Godot is launched from the GUI with no shell
            // exports) WITHOUT exporting a process env var. Mirrors the connection config's env-file layer.
            var envPath = ProjectSettings.GlobalizePath("res://.env");
            string? ResolveDevVar(string key)
            {
                var fromProcess = OS.GetEnvironment(key);
                return !string.IsNullOrEmpty(fromProcess) ? fromProcess : GodotMcpEnvFile.LookupRaw(envPath, key);
            }

            // The dev-control bridge is UNAUTHENTICATED; this env gate (plus the 127.0.0.1 bind and #if TOOLS)
            // is its only security boundary. Resolve once and gate through the pure, unit-pinned predicate so the
            // "exactly 1 enables it" contract lives in one CI-testable place (DevControlGate).
            var devControlValue = ResolveDevVar(GodotMcpEnv.DevControl);
            if (!DevControlGate.IsEnabled(devControlValue))
                return;

            if (_dock == null)
            {
                LogWarning("[dev-control] GODOT_MCP_DEV_CONTROL=1 but the dock failed to register; not starting.");
                return;
            }

            var port = DevControlServer.DefaultPort;
            var portEnv = ResolveDevVar(GodotMcpEnv.DevControlPort);
            if (!string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out var parsed) && parsed > 0 && parsed <= 65535)
                port = parsed;

            try
            {
                // Load-bearing assertion: the env gate MUST hold at the construction site. The early-return
                // above already guarantees it, so this never throws in normal operation — its purpose is to make
                // the gate provably load-bearing, so any future refactor that reaches here with the gate off
                // fails fast instead of opening an unauthenticated loopback control surface. (DevControlGateTests
                // pins this.)
                DevControlGate.AssertEnabledOrThrow(devControlValue);
                _devControl = new DevControlServer(_dock, port);
                _devControl.Start();
            }
            catch (System.Exception ex)
            {
                LogError($"[dev-control] failed to start: {ex.Message}");
                _devControl = null;
            }
        }

        public override void _ExitTree()
        {
            // Route through the single idempotent teardown. On a normal plugin-disable this is the only
            // teardown trigger; on a "Build Project" hot-reload the ALC-unloading hook usually fires FIRST
            // (and _ExitTree may not fire at all), so this call is then a no-op via the _torndown guard.
            Teardown(fromReload: false);
        }

        /// <summary>
        /// Single, idempotent teardown reachable from BOTH <see cref="_ExitTree"/> (normal plugin disable)
        /// and the <see cref="GodotMcpAssemblyResolver.ReloadTeardown"/> hook (the Godot "Build Project"
        /// hot-reload, which raises an <see cref="System.Runtime.Loader.AssemblyLoadContext"/> unload WITHOUT
        /// calling <see cref="_ExitTree"/> — the path the old <c>_ExitTree</c>-only teardown missed entirely).
        ///
        /// <para>
        /// Order matters and each step is independently wrapped so one failure cannot abort the rest (and,
        /// critically, cannot escape the ALC-unloading handler, where a throw would abort the unload):
        /// <list type="number">
        ///   <item><b>Dev-control listener FIRST</b> — its <c>Dispose</c> stops the <c>HttpListener</c> and
        ///   joins the accept thread, releasing a running thread that pins the collectible context.</item>
        ///   <item><b><c>DisconnectAndDrain(2s)</c></b> — the LOAD-BEARING step: cancels the reconnect intent
        ///   then runs the reused client's GRACEFUL <c>Disconnect</c> (StopAsync + DisposeAsync) on a
        ///   background thread and bounded-waits for it on the main thread, so the SignalR receive loop + any
        ///   in-flight HTTP reconnect are actually JOINED — not just fire-and-forgotten. A bare
        ///   <c>DisconnectImmediate()</c> left those threads running inside the collectible ALC, so Godot's
        ///   unload (which does not abort threads) failed; draining them first is what lets the unload
        ///   succeed and makes both "Failed to unload assemblies" and the <c>delegate_handle null</c> flood
        ///   vanish.</item>
        ///   <item><b>Dispose the connection</b> — releases the R3 subscriptions + the plugin instance.</item>
        ///   <item><b>Free the dock + dispatcher (MAIN-THREAD ONLY)</b> — severs the native signal Callables by
        ///   tearing down the dock subtree. (All dock Godot-signal connections are now OBJECT+METHOD Callables,
        ///   which can no longer emit the <c>delegate_handle.value == nullptr</c> symptom regardless; freeing the
        ///   subtree still severs them deterministically.)
        ///   These call <c>Node.Free()</c>, which is only legal on the editor main thread; the ALC Unloading
        ///   event is raised on the main thread so that holds here, but the block is still guarded so that
        ///   if it ever runs off-main-thread the (pure-managed) steps 1–3 still run and the Node frees are
        ///   skipped rather than crashing. We do NOT marshal through <see cref="MainThreadDispatcher"/> — it
        ///   is being torn down and its <c>Enqueue</c> throws once its instance is gone.</item>
        ///   <item><b>Null the process-wide statics</b> — error-capture router and the
        ///   <see cref="Current"/>/<see cref="GodotMcpAssemblyResolver.ReloadTeardown"/> hook. The log
        ///   collector (<see cref="GodotLogCollector.Current"/>) is the one exception: it is deliberately
        ///   NOT nulled. Nulling it here would wipe exactly the teardown / reload-window diagnostics an
        ///   operator most wants — the background framework log-routing path reads <c>Current?.Append</c>
        ///   off arbitrary threads, so a null would silently drop those lines (issue #173). Leaving it is
        ///   harmless: it is a bounded ring of plain managed <c>LogEntry</c> rows, it pins no Godot native
        ///   object and no collectible-ALC type, and the next <c>_EnterTree</c> replaces it
        ///   (last-writer-wins) so it cannot accumulate across reloads. The swap is published via Volatile
        ///   (<see cref="GodotLogCollector.Current"/>) so the background reader observes the install without
        ///   a torn read.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="fromReload">
        /// <c>true</c> when invoked from the ALC-unloading hook (diagnostic only — the teardown body is the
        /// same on both paths).
        /// </param>
        void Teardown(bool fromReload)
        {
            if (_torndown)
                return;
            _torndown = true;

            // 1) Stop the dev-control listener FIRST — releases its bound socket + joins the accept thread.
            try
            {
                if (_devControl != null)
                {
                    _devControl.Dispose();
                    _devControl = null;
                }
            }
            catch (System.Exception ex) { TryLogTeardownError("dev-control dispose", ex); }

            // 2) Reload-safe disconnect that DRAINS (joins) the connection's background threads — THE key fix.
            //    A bare DisconnectImmediate() only cancels + fire-and-forgets HubConnection.DisposeAsync(), so
            //    the SignalR receive loop + an in-flight HTTP reconnect kept running inside the collectible ALC
            //    and made Godot's unload fail (it does not abort threads). DisconnectAndDrain runs the graceful
            //    StopAsync+DisposeAsync on a background thread and bounded-waits (2s) on the main thread, so
            //    those threads are joined before the unload proceeds. 2s keeps the whole hook bounded (~3s):
            //    a refused-localhost reconnect aborts fast (RST); a black-holed host could exceed the timeout,
            //    in which case the bounded wait returns and we proceed anyway. Must precede the connection
            //    Dispose (Dispose also disconnects, but doing the drain explicitly first joins the threads
            //    while the plugin is still live and keeps the ordering obvious).
            try
            {
                _connection?.DisconnectAndDrain(System.TimeSpan.FromSeconds(2));
            }
            catch (System.Exception ex) { TryLogTeardownError("disconnect-and-drain", ex); }

            // 3) Dispose the connection — releases the R3 subscriptions and the plugin instance.
            try
            {
                if (_connection != null)
                {
                    _connection.Dispose();
                    _connection = null;
                }
            }
            catch (System.Exception ex) { TryLogTeardownError("connection dispose", ex); }

            // 4) MAIN-THREAD ONLY: free the dock + dispatcher. These touch Godot Nodes (Node.Free), which is
            //    only legal on the editor main thread. The ALC Unloading event and _ExitTree both run on the
            //    main thread, so this normally executes; the guard is a safety net so an unexpected off-thread
            //    caller skips the Node frees instead of crashing the host. Steps 1–3 above are pure-managed and
            //    always run.
            if (MainThreadDispatcher.IsMainThread)
            {
                // Remove the engine error Logger (4.5+) registered in _EnterTree via OS.AddLogger. THIS is the
                // godotengine/godot#78513 fix: the engine holds a strong handle to a GodotObject defined in the
                // COLLECTIBLE addon assembly, which roots the hot-reload ALC and makes its unload fail with the
                // ".NET: Failed to unload assemblies" flood. OS.RemoveLogger is an engine call and MUST run on
                // the main thread (mirroring OS.AddLogger), so it lives inside this main-thread guard. Uninstall
                // is idempotent + defensive (swallows removal failure) and also clears ScriptErrorCapture.Current.
                try { GodotScriptErrorLoggerBridge.Uninstall(); }
                catch (System.Exception ex) { TryLogTeardownError("engine-logger uninstall", ex); }

                FreeNodes();
            }
            else
            {
                // Off-thread teardown skips OS.RemoveLogger (an engine call). Acceptable: the #78513 ALC-unload
                // path is always main-thread, so the logger is always removed before an unload that matters.
                TryLog("[Godot-MCP] teardown ran off the main thread; skipping Node.Free + engine-logger remove (connection torn down).");
            }

            TryLog("[Godot-MCP] plugin unloaded");

            // 5) Null the process-wide statics so a stale router/hook does not outlive this instance.
            //    NOTE: GodotLogCollector.Current is DELIBERATELY left in place (NOT nulled) — see the
            //    "Null the process-wide statics" item in this method's XML doc for the full rationale (#173).

            // Drop the engine error-capture router unconditionally. On 4.5+ the main-thread guard above already
            // called GodotScriptErrorLoggerBridge.Uninstall() (which removes the engine Logger via
            // OS.RemoveLogger AND nulls Current); this pure-managed re-clear is a belt-and-suspenders that also
            // covers the off-thread teardown path (where RemoveLogger is skipped) so a stale validation session
            // never leaks across a reload regardless of which thread tore the plugin down.
            try { ScriptErrorCapture.Current = null; } catch { /* swallow during unload */ }

            // Drop ReflectorNet's static MainThread.Instance when it points at OUR GodotMainThread — one of the
            // two godot#78513 ALC pins (the other is the connection transport, bounded-joined in
            // GodotMcpConnection.DisposePlugin). GodotMcpAssemblyResolver loads ReflectorNet into the
            // NON-collectible Default ALC, so a static field there holding a GodotMainThread (defined in THIS
            // collectible addon ALC) roots the whole context and blocks the "Build Project" hot-reload unload —
            // the same cross-ALC pin class the resolver already fixes for its own Default.Resolving hook.
            // Clearing it releases the root so the ALC can unload. Pure-managed static assignment → safe on any
            // thread, so it lives in this always-run section; the reloaded assembly re-installs its own
            // GodotMainThread via _EnterTree. Guarded by `is GodotMainThread` so a newer instance's install is
            // not clobbered, and try/catch so it can never throw out of the unload.
            try
            {
                if (com.IvanMurzak.ReflectorNet.Utils.MainThread.Instance is GodotMainThread)
                    com.IvanMurzak.ReflectorNet.Utils.MainThread.Instance = null!;
            }
            catch { /* swallow during unload */ }

            // Drop System.Text.Json's process-wide reflection-emit member-accessor cache, which holds compiled
            // accessor delegates over THIS collectible assembly's types (config / device-auth DTOs / tool
            // models routed through ReflectorNet, …) and is a systematic root of godot#78513. One clear
            // releases every STJ-cached addon type at once — including the reflection-based ReflectorNet path
            // that cannot be source-generated. Best-effort + fully defensive (see GodotMcpStjReflectionCache).
            try { GodotMcpStjReflectionCache.Clear(); } catch { /* swallow during unload */ }

            // Drop ReflectorNet's process-wide static reflection caches. They live in the NON-collectible Default
            // ALC (the resolver loads ReflectorNet there) but, during tool registration / parameter-schema build /
            // serialization, get populated with entries that reference THIS collectible addon assembly's types:
            //   • TypeUtils type-name caches hold resolved addon `Type` objects (a Type roots its assembly's ALC);
            //   • TypeMemberUtils field/property caches hold addon `FieldInfo`/`PropertyInfo` (a member roots its
            //     DeclaringType's ALC).
            // Either is a non-collectible→collectible strong static reference = a godot#78513 root (the "Build/
            // tool-registration FAILS while CreateDefaultReflector alone PASSES" bisection signature). ReflectorNet
            // already exposes the clears; the addon just has to call them on teardown (mirrors the MainThread.Instance
            // and STJ clears above). Pure-managed statics → safe on any thread; the reloaded assembly repopulates
            // lazily on next use. No ReflectorNet/McpPlugin change or pin bump required.
            try { com.IvanMurzak.ReflectorNet.Utils.TypeMemberUtils.ClearAllCaches(); } catch { /* swallow during unload */ }
            try { com.IvanMurzak.ReflectorNet.Utils.TypeUtils.ClearTypeCache(); } catch { /* swallow during unload */ }
            try { com.IvanMurzak.ReflectorNet.Utils.TypeUtils.ClearAssemblyTypeCache(); } catch { /* swallow during unload */ }
            try { com.IvanMurzak.ReflectorNet.Utils.TypeUtils.ClearExactAssemblyTypeCache(); } catch { /* swallow during unload */ }
            try { com.IvanMurzak.ReflectorNet.Utils.TypeUtils.ClearEnumerableItemTypeCache(); } catch { /* swallow during unload */ }

            // Release the static reload hook so a torn-down instance is not reachable from the resolver and
            // can be collected. Only clear if WE are the current owner (a newer _EnterTree may already have
            // claimed it).
            try
            {
                if (ReferenceEquals(Current, this))
                {
                    Current = null;
                    GodotMcpAssemblyResolver.ReloadTeardown = null;
                }
            }
            catch { /* swallow during unload */ }
        }

        /// <summary>
        /// The main-thread-only portion of <see cref="Teardown"/>: synchronously free the dock subtree and the
        /// dispatcher. Split out so the main-thread guard in <see cref="Teardown"/> reads cleanly. No managed
        /// signal-delegate sweep is needed — all dock Godot-signal connections are object+method Callables that
        /// are severed when the subtree is freed.
        /// </summary>
        void FreeNodes()
        {
            // On a FAILED hot-reload (godotengine/godot#78513 — the collectible ALC could not unload), Godot may
            // have ALREADY disposed the managed wrappers (this EditorPlugin, the dock, the dispatcher) BEFORE this
            // ALC-unloading teardown runs. Touching a disposed GodotObject throws ObjectDisposedException — which is
            // benign here (Godot already freed it), so guard every native access with IsInstanceValid and treat an
            // ObjectDisposedException as "already gone" rather than logging a spurious teardown ERROR. (This became
            // observable once the connection actually connects on boot — see GodotMcpConnection.ResolveConfig — so
            // the unload races a live connection's threads.)
            try
            {
                if (_dock != null)
                {
                    // Skip when Godot has already disposed this plugin or the dock — nothing left for us to free.
                    if (!GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(_dock))
                    {
                        _dock = null;
                    }
                    else
                    {
                    // Remove from the dock slot before freeing so Godot does not hold a dangling control ref.
                    RemoveControlFromDocks(_dock);

                    // SYNCHRONOUS free (Free, not QueueFree) on the unload path. A DEFERRED QueueFree would not
                    // run the dock subtree's own _ExitTree disconnect logic until the next idle frame — AFTER the
                    // ALC unload has already been attempted — leaving the panels' pure-managed C# EVENT
                    // subscriptions (the connection/server-manager events the panels +=/-= in _EnterTree/_ExitTree)
                    // still rooted into the unloading context. Free() tears the whole subtree down NOW, running
                    // every child's _ExitTree (panels unsubscribe their connection events) and severing every
                    // native signal connection deterministically before the context unloads. (The dock's
                    // Godot-signal connections are object+method Callables, which never enter the ManagedCallable
                    // registry and so can no longer emit the historical "delegate_handle.value == nullptr" flood;
                    // see godotengine/godot#78513.) Transient child windows (FeatureListWindow /
                    // SerializationCheckWindow) are parented under the dock, so this frees them too.
                    _dock.Free();
                    _dock = null;
                    }
                }
            }
            catch (System.ObjectDisposedException) { _dock = null; /* Godot already disposed the plugin/dock during the failed unload — benign. */ }
            catch (System.Exception ex) { TryLogTeardownError("dock free", ex); }

            try
            {
                if (_dispatcher != null)
                {
                    // Skip when Godot has already disposed the dispatcher — nothing left for us to free.
                    if (GodotObject.IsInstanceValid(_dispatcher))
                    {
                        // Synchronous free for the same reason as the dock: the dispatcher pumps main-thread work
                        // and holds registered per-tick delegates; freeing it deterministically here ensures no
                        // dispatcher-rooted managed state survives into the unload.
                        _dispatcher.Free();
                    }
                    _dispatcher = null;
                }
            }
            catch (System.ObjectDisposedException) { _dispatcher = null; /* already disposed by Godot's unload — benign. */ }
            catch (System.Exception ex) { TryLogTeardownError("dispatcher free", ex); }

            // No KeepAlive sweep is needed anymore: every dock signal connection is an OBJECT+METHOD Callable
            // (not a C# delegate/lambda), so none of them is a ManagedCallable and none can survive into the ALC
            // unload as a leftover managed connection. The dock Free() above severs every native connection
            // deterministically as it tears the subtree down.
        }

        /// <summary>
        /// Free a still-live Node referenced by <paramref name="node"/> and null the reference, so the
        /// hot-reload re-boot (<see cref="BootAfterReload"/>) can re-create dock/dispatcher idempotently
        /// even if <see cref="Teardown"/> has not yet freed the prior instance's nodes on this Godot
        /// version. A null or already-disposed node just clears the reference (nothing to free). Defensive
        /// throughout — re-boot runs deferred, possibly racing a partial teardown, and must not fault.
        /// </summary>
        static void FreeStaleNode<T>(ref T? node) where T : global::Godot.Node
        {
            try
            {
                if (node != null && GodotObject.IsInstanceValid(node))
                    node.Free();
            }
            catch (System.ObjectDisposedException) { /* already gone — benign */ }
            catch (System.Exception ex) { TryLogTeardownError("free stale node", ex); }
            node = null;
        }

        /// <summary>
        /// Best-effort log helper for the teardown path. Wrapped because mid-reload the Godot logging API
        /// (<c>GD.Print</c>) or the log collector may already be unavailable; a teardown diagnostic must
        /// never throw on the ALC-unloading path.
        /// </summary>
        static void TryLog(string message)
        {
            try { Log(message); }
            catch { /* logging itself must not break teardown */ }
        }

        /// <summary>Best-effort error-log helper for a named teardown step (never throws).</summary>
        static void TryLogTeardownError(string step, System.Exception ex)
        {
            try { LogError($"[Godot-MCP] teardown step '{step}' failed: {ex.Message}"); }
            catch { /* swallow — teardown must not throw on the unload path */ }
        }
    }
}
#endif
