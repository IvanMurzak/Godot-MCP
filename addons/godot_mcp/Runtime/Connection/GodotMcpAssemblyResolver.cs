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
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Resolves the addon's transitive NuGet dependency assemblies (ReflectorNet, McpPlugin,
    /// McpPlugin.Common, and their own transitive deps such as R3 and the SignalR client) at
    /// <b>Godot editor runtime</b>.
    ///
    /// <para>
    /// <b>Why this exists.</b> Godot loads the project's compiled assembly into the default
    /// <see cref="AssemblyLoadContext"/>, but does NOT teach that context how to find the project's
    /// NuGet dependency graph. The CLR's default resolution probes only the host (Godot's own folder)
    /// and the shared framework — it does NOT read the project's <c>*.deps.json</c>, and (unlike a
    /// normal <c>dotnet</c> app launch) Godot does not emit a <c>*.runtimeconfig.dev.json</c> carrying
    /// the NuGet global-packages probing paths next to the built assembly. So the moment editor-plugin
    /// code touches a type from a dependency that was not copied beside the project assembly, the JIT
    /// throws <c>System.IO.FileNotFoundException: Could not load file or assembly 'ReflectorNet, ...'</c>.
    /// This is a long-standing Godot C# gotcha for addons with external dependencies
    /// (see godotengine/godot-proposals#9074, godotengine/godot#112701).
    /// </para>
    ///
    /// <para>
    /// <b>How it fixes it.</b> We hook <see cref="AssemblyLoadContext.Resolving"/> on the default
    /// context and answer the misses ourselves. Per missed assembly name, in order:
    /// <list type="number">
    ///   <item><b>Same-directory probe</b> — <c>&lt;name&gt;.dll</c> next to the project assembly.
    ///   Covers consumers who copy deps into the output dir
    ///   (<c>&lt;CopyLocalLockFileAssemblies&gt;true&lt;/CopyLocalLockFileAssemblies&gt;</c>).</item>
    ///   <item><b><c>*.deps.json</c> + NuGet global-packages probe</b> — parse the project's
    ///   <c>*.deps.json</c> to learn each library's <c>{package}/{version}</c> and the runtime asset's
    ///   relative path (<c>lib/net8.0/ReflectorNet.dll</c>), then locate the file under the NuGet
    ///   global packages folder (<c>NUGET_PACKAGES</c> env or <c>~/.nuget/packages</c>). This is the
    ///   decisive path in the Godot editor, where no dev.json probing config exists. It is fully
    ///   consumer-portable: any project that <c>PackageReference</c>s these libraries gets a
    ///   <c>*.deps.json</c> and the DLLs in its own NuGet cache.</item>
    ///   <item><b><see cref="AssemblyDependencyResolver"/></b> — used as a best-effort first try when a
    ///   sibling dev.json/runtimeconfig IS present (e.g. a plain <c>dotnet</c> host), since it also
    ///   handles runtime-specific (RID) assets. It returns empty in the Godot editor (no dev.json), so
    ///   strategy 2 is what actually carries there.</item>
    /// </list>
    /// All hits load via <see cref="AssemblyLoadContext.LoadFromAssemblyPath"/> into the default
    /// context so the loaded assembly satisfies the original reference.
    /// </para>
    ///
    /// <para>
    /// The hook references ONLY BCL types, so installing it does not itself trigger a load of the very
    /// assemblies it resolves. It is installed by a <see cref="ModuleInitializerAttribute"/> — the only
    /// hook that runs before Godot instantiates the <c>EditorPlugin</c> type (whose field/using
    /// references would otherwise fault during type load, before any plugin method body runs).
    /// </para>
    ///
    /// <para>
    /// <b>Known limitation (not implemented).</b> Strategy 2 probes only the NuGet <i>global</i> packages
    /// folder (<c>NUGET_PACKAGES</c> env, else <c>~/.nuget/packages</c>). It does NOT consult NuGet
    /// <i>fallback</i> package folders — <c>DOTNET_NUGET_FALLBACK_PACKAGES</c> / the SDK's
    /// <c>NuGetFallbackFolder</c> / the <c>additionalProbingPaths</c> a <c>*.deps.json</c> may declare —
    /// which some enterprise/CI caches rely on. If a dependency lives only in a fallback folder and not
    /// the global cache, strategy 2 misses it and the resolve falls through to strategy 3. This is a
    /// follow-up; for the supported consumer story (a normal <c>dotnet restore</c> into the global cache)
    /// it does not arise.
    /// </para>
    /// </summary>
    public static class GodotMcpAssemblyResolver
    {
        static readonly object _gate = new object();
        static bool _installed;

        // Load-bearing concurrency invariant for the three cached fields below: they are seeded under
        // _gate inside Install() BEFORE the Resolving hook is subscribed, and Install() runs its seeding
        // body exactly once (the _installed latch makes every later call a no-op — it never re-seeds).
        // Because of that one-time, publish-before-subscribe ordering, the Resolving hook
        // (OnResolving → ResolvePath) reads these WITHOUT taking _gate and is still safe: by the time any
        // resolve can fire, the fields are fully written and never mutated again. A future edit that
        // re-seeds these post-install (or seeds them after subscribing) would break this and introduce a
        // data race — take _gate there if you ever do.
        static AssemblyDependencyResolver? _depsResolver;
        static string? _probeDirectory;
        static Dictionary<string, string>? _depsJsonAssemblyPaths;

        /// <summary>
        /// Installs the resolver as early as physically possible. A module initializer runs once when
        /// the CLR loads THIS assembly's module — BEFORE any type in the assembly is constructed or any
        /// method JIT-compiled. That matters: Godot instantiates the <c>EditorPlugin</c> type (whose
        /// fields/usings reference the NuGet-dependency types) before the plugin's <c>_EnterTree</c>
        /// body would run, so installing from inside <c>_EnterTree</c> is too late — type load already
        /// triggers the failing resolution. The module initializer wins that race and references only
        /// BCL types, so it cannot itself fault on a missing dependency.
        /// </summary>
        [ModuleInitializer]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries",
            Justification = "This assembly is host-loaded application code (a Godot editor-plugin " +
                "assembly), not a reusable library. The module initializer is the only hook that runs " +
                "before Godot instantiates the EditorPlugin type, which is required to install the " +
                "dependency resolver before the failing assembly load occurs.")]
        internal static void ModuleInit()
        {
            try
            {
                Install(ResolveAnchorAssemblyPath());
            }
            catch
            {
                // Never let a module initializer throw — that would fail the whole assembly load.
            }

            try
            {
                // Subscribe to the addon ALC's Unloading event from the same earliest-possible hook. On a
                // Godot "Build Project" hot-reload the EditorPlugin's _ExitTree is NOT called before the ALC
                // unload, so this event is the only place the live connection threads / GC handles that pin
                // the collectible context can be released. No-op in the non-collectible CI/xUnit host.
                InstallUnloadingHook();
            }
            catch
            {
                // Never let a module initializer throw.
            }
        }

        /// <summary>
        /// Optional sink for diagnostic lines (e.g. <c>GD.Print</c>). The resolver stays free of Godot
        /// API references so it remains pure-BCL and unit-testable; the editor plugin passes a
        /// Godot-aware logger in. Null is fine (silent). Note: the module initializer installs the hook
        /// before any plugin code sets this, so installation itself is silent; resolutions that happen
        /// after the plugin sets <see cref="Log"/> are surfaced.
        /// </summary>
        public static Action<string>? Log { get; set; }

        /// <summary>
        /// Reload-safe teardown invoked when the addon's (collectible) <see cref="AssemblyLoadContext"/>
        /// begins unloading — the Godot "Build Project" hot-reload path. This is the hook the plugin's
        /// <c>_ExitTree</c> MISSES: a C# rebuild raises an ALC unload WITHOUT first calling the
        /// <c>EditorPlugin</c>'s <c>_ExitTree</c>, so all teardown wired only into <c>_ExitTree</c> is dead
        /// code on a reload. The live SignalR connection (HubConnection + HttpClient + the auto-reconnect
        /// loop + R3 subscriptions) and the dev-control listener thread keep running threads / strong GC
        /// handles that PIN this collectible context open, so the CLR reports
        /// <c>gd_mono.cpp:791 Failed to unload assemblies</c> and floods
        /// <c>delegate_handle.value == nullptr</c> (the downstream symptom of the failed unload).
        ///
        /// <para>
        /// The editor plugin assigns this in <c>_EnterTree</c>/<c>BootMcp</c> to a SYNCHRONOUS, idempotent
        /// teardown (stop dev-control → <c>DisconnectImmediate()</c> → dispose subscriptions/plugin → free
        /// dock/dispatcher on the main thread) and clears it when teardown runs. Null means "no live plugin
        /// to tear down" (safe no-op). In the CI/xUnit host the addon assembly is loaded into a
        /// NON-collectible context, so <see cref="AssemblyLoadContext.Unloading"/> never fires there and
        /// this stays null — keeping the unit-test host unaffected.
        /// </para>
        /// </summary>
        public static Action? ReloadTeardown { get; set; }

        static bool _unloadingHooked;

        /// <summary>
        /// Subscribe <see cref="OnAssemblyUnloading"/> to the Unloading event of the load context that owns
        /// THIS assembly. Installed once from <see cref="ModuleInit"/> (same earliest-possible hook the
        /// resolver itself uses). When this assembly is in the DEFAULT (non-collectible) context — the
        /// xUnit/CI host — <c>GetLoadContext(...)</c> returns the default context whose Unloading never
        /// fires, so the subscription is a harmless no-op. When Godot loads the addon into a COLLECTIBLE
        /// context for editor hot-reload, the event fires on the editor main thread at the start of the
        /// unload, giving the plugin its only chance to release the threads/handles pinning the context.
        /// Idempotent and fully wrapped — a failure to hook must never fail the module load.
        /// </summary>
        static void InstallUnloadingHook()
        {
            lock (_gate)
            {
                if (_unloadingHooked)
                    return;

                try
                {
                    var context = AssemblyLoadContext.GetLoadContext(typeof(GodotMcpAssemblyResolver).Assembly);
                    if (context != null)
                    {
                        context.Unloading += OnAssemblyUnloading;
                        _unloadingHooked = true;
                    }
                }
                catch (Exception ex)
                {
                    Log?.Invoke($"[Godot-MCP] could not install ALC unloading hook: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Fires when the addon's load context starts unloading (Godot hot-reload). Runs the plugin's
        /// registered <see cref="ReloadTeardown"/>, if any. NEVER throws — an exception escaping an
        /// Unloading handler would abort the unload it is trying to enable. The event is raised on the
        /// editor main thread (where the rebuild is driven), so the teardown's main-thread-only steps
        /// (Node.Free) are legal here; the teardown itself still guards them defensively.
        /// </summary>
        static void OnAssemblyUnloading(AssemblyLoadContext context)
        {
            try
            {
                Log?.Invoke("[Godot-MCP] assembly unloading — running reload-safe teardown.");
                ReloadTeardown?.Invoke();
            }
            catch (Exception ex)
            {
                // Last-ditch: never let the Unloading handler throw.
                try { Log?.Invoke($"[Godot-MCP] reload teardown error (ignored): {ex.Message}"); }
                catch { /* logging itself must not throw here */ }
            }

            // Remove the strong ref that the NON-collectible Default ALC holds into THIS (collectible)
            // addon assembly: the Resolving handler installed by our ModuleInitializer is a delegate over
            // OnResolving, a static method in this assembly, so Default.Resolving keeps the collectible ALC
            // pinned and contributes to "Failed to unload assemblies". On unload the addon is going away, so
            // dropping the old resolver is correct — the reloaded assembly re-installs its own resolver via
            // its own ModuleInitializer. Wrapped so it can never throw out of the Unloading handler.
            try
            {
                AssemblyLoadContext.Default.Resolving -= OnResolving;
            }
            catch (Exception ex)
            {
                try { Log?.Invoke($"[Godot-MCP] could not detach Default.Resolving on unload (ignored): {ex.Message}"); }
                catch { /* logging itself must not throw here */ }
            }
        }

        /// <summary>
        /// Determine the on-disk path of the project/addon assembly whose sibling <c>*.deps.json</c>
        /// describes the dependency graph. In the Godot editor the assembly's
        /// <see cref="Assembly.Location"/> is EMPTY (Godot loads the project assembly from a byte
        /// stream), so we synthesize the path from <see cref="AppContext.BaseDirectory"/> (which Godot
        /// points at the build output dir, e.g. <c>.godot/mono/temp/bin/&lt;cfg&gt;/</c>) plus the
        /// assembly's simple name.
        /// </summary>
        static string ResolveAnchorAssemblyPath()
        {
            var asm = typeof(GodotMcpAssemblyResolver).Assembly;

            var location = asm.Location;
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
                return location;

            var name = asm.GetName().Name;
            if (!string.IsNullOrEmpty(name))
            {
                var candidate = Path.Combine(AppContext.BaseDirectory, name + ".dll");
                if (File.Exists(candidate))
                    return candidate;
            }

            // Last resort: any *.deps.json in the base dir anchors the resolver; pair it with its dll.
            try
            {
                foreach (var deps in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.deps.json"))
                {
                    var dll = deps.Substring(0, deps.Length - ".deps.json".Length) + ".dll";
                    if (File.Exists(dll))
                        return dll;
                }
            }
            catch
            {
                // ignore — fall through to empty
            }

            return string.Empty;
        }

        /// <summary>
        /// Install the default-context resolving hook. Idempotent — safe to call more than once (plugin
        /// toggled off/on, domain reload). Seeds all three resolution strategies from
        /// <paramref name="anchorAssemblyPath"/> (the project assembly whose sibling <c>*.deps.json</c>
        /// describes the graph).
        /// </summary>
        public static void Install()
        {
            Install(ResolveAnchorAssemblyPath());
        }

        /// <summary>
        /// Test/seam-friendly overload: install seeded from an explicit anchor assembly path (the path
        /// whose sibling <c>*.deps.json</c> describes the dependency graph).
        /// </summary>
        /// <param name="anchorAssemblyPath">
        /// Absolute path to an assembly that has a sibling <c>*.deps.json</c>. May be empty; in that
        /// case only the directory-probe fallback is available, seeded from
        /// <see cref="AppContext.BaseDirectory"/>.
        /// </param>
        public static void Install(string anchorAssemblyPath)
        {
            lock (_gate)
            {
                if (_installed)
                    return;

                if (!string.IsNullOrEmpty(anchorAssemblyPath) && File.Exists(anchorAssemblyPath))
                {
                    _probeDirectory = Path.GetDirectoryName(anchorAssemblyPath);

                    try
                    {
                        _depsResolver = new AssemblyDependencyResolver(anchorAssemblyPath);
                    }
                    catch (Exception ex)
                    {
                        Log?.Invoke($"[Godot-MCP] AssemblyDependencyResolver unavailable: {ex.Message}");
                        _depsResolver = null;
                    }

                    var depsJsonPath = Path.ChangeExtension(anchorAssemblyPath, ".deps.json");
                    _depsJsonAssemblyPaths = BuildDepsJsonIndex(depsJsonPath);
                }

                if (string.IsNullOrEmpty(_probeDirectory))
                    _probeDirectory = AppContext.BaseDirectory;

                AssemblyLoadContext.Default.Resolving += OnResolving;
                _installed = true;

                Log?.Invoke($"[Godot-MCP] assembly resolver installed (probe dir: {_probeDirectory}).");
            }
        }

        /// <summary>
        /// Resolve a single assembly name to an existing file path. Exposed for unit testing; returns
        /// null when no strategy finds a file (the CLR then continues its own resolution / throws as it
        /// normally would).
        /// </summary>
        public static string? ResolvePath(AssemblyName assemblyName)
        {
            var simpleName = assemblyName.Name;
            if (string.IsNullOrEmpty(simpleName))
                return null;

            // 1) Same-directory probe (copied-beside-the-assembly deployments). This trusts the
            //    filename: it returns <probeDir>/<simpleName>.dll if it exists, WITHOUT checking the
            //    file's assembly version against assemblyName.Version. That is intentional — for
            //    copy-local deploys the build already vetted which version landed beside the assembly,
            //    so the on-disk DLL is authoritative. (Version skew there would be a build problem, not
            //    something this runtime probe should second-guess.)
            var dir = _probeDirectory;
            if (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, simpleName + ".dll");
                if (File.Exists(candidate))
                    return candidate;
            }

            // 2) deps.json + NuGet global-packages probe (the path that works in the Godot editor).
            var index = _depsJsonAssemblyPaths;
            if (index != null && index.TryGetValue(simpleName, out var fromDeps) && File.Exists(fromDeps))
                return fromDeps;

            // 3) AssemblyDependencyResolver (best-effort; carries RID-specific assets when a dev.json
            //    probing config is present — e.g. a plain dotnet host).
            var depsResolver = _depsResolver;
            if (depsResolver != null)
            {
                try
                {
                    var resolved = depsResolver.ResolveAssemblyToPath(assemblyName);
                    if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
                        return resolved;
                }
                catch
                {
                    // ignore — nothing more to try
                }
            }

            return null;
        }

        /// <summary>
        /// Parse a <c>*.deps.json</c> into a map of <c>assembly-simple-name → absolute file path</c>,
        /// resolving each runtime asset against the NuGet global packages folder. Returns null when the
        /// file is absent/unreadable (callers then rely on the other strategies). Public for unit tests.
        /// </summary>
        public static Dictionary<string, string>? BuildDepsJsonIndex(string depsJsonPath)
        {
            if (string.IsNullOrEmpty(depsJsonPath) || !File.Exists(depsJsonPath))
                return null;

            var packageRoots = GetNuGetPackageRoots();
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(depsJsonPath));
                if (!doc.RootElement.TryGetProperty("targets", out var targets))
                    return map;

                foreach (var target in targets.EnumerateObject())
                {
                    foreach (var library in target.Value.EnumerateObject())
                    {
                        // library.Name is "Package/Version" (e.g. "com.IvanMurzak.ReflectorNet/5.3.1").
                        var slash = library.Name.IndexOf('/');
                        if (slash <= 0)
                            continue;

                        var packageId = library.Name.Substring(0, slash);
                        var version = library.Name.Substring(slash + 1);

                        if (!library.Value.TryGetProperty("runtime", out var runtime))
                            continue;

                        foreach (var asset in runtime.EnumerateObject())
                        {
                            // asset.Name is the relative runtime path, e.g. "lib/net8.0/ReflectorNet.dll".
                            var relative = asset.Name;
                            if (!relative.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                                continue;

                            var assemblySimpleName = Path.GetFileNameWithoutExtension(relative);
                            if (string.IsNullOrEmpty(assemblySimpleName) || map.ContainsKey(assemblySimpleName))
                                continue;

                            var absolute = LocateInPackageRoots(packageRoots, packageId, version, relative);
                            if (!string.IsNullOrEmpty(absolute))
                                map[assemblySimpleName] = absolute!;
                        }
                    }
                }
            }
            catch
            {
                return map.Count > 0 ? map : null;
            }

            return map;
        }

        static string? LocateInPackageRoots(IReadOnlyList<string> packageRoots, string packageId, string version, string relativeRuntimePath)
        {
            // NuGet lays packages out lower-cased: <root>/<id.lower>/<version.lower>/<relative>.
            var idLower = packageId.ToLowerInvariant();
            var versionLower = version.ToLowerInvariant();
            var relativeNative = relativeRuntimePath.Replace('/', Path.DirectorySeparatorChar);

            foreach (var root in packageRoots)
            {
                if (string.IsNullOrEmpty(root))
                    continue;

                var candidate = Path.Combine(root, idLower, versionLower, relativeNative);
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        static IReadOnlyList<string> GetNuGetPackageRoots()
        {
            var roots = new List<string>();

            var env = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (!string.IsNullOrEmpty(env))
                roots.Add(env);

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
                roots.Add(Path.Combine(userProfile, ".nuget", "packages"));

            return roots;
        }

        static Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            var path = ResolvePath(assemblyName);
            if (string.IsNullOrEmpty(path))
            {
                // Fail-soft: every strategy missed. Returning null lets the CLR continue its own
                // resolution (and ultimately throw FileNotFoundException as it normally would), but a
                // bare FileNotFoundException gives the consumer no clue WHY. The most common cause is
                // version/cache skew — the *.deps.json lists a {package}/{version} whose folder is not
                // in the NuGet cache (e.g. restore never ran, or a different pin), so strategy 2 finds
                // nothing. Surface that here so it is diagnosable from the editor log.
                Log?.Invoke(
                    $"[Godot-MCP] could not resolve '{assemblyName.Name}' via same-dir probe / " +
                    $"deps.json+NuGet-cache / AssemblyDependencyResolver — the CLR will now throw if " +
                    $"this assembly is required. Check that 'dotnet restore' populated the NuGet cache " +
                    $"for the version pinned in *.deps.json (probe dir: {_probeDirectory}).");
                return null;
            }

            try
            {
                var loaded = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                Log?.Invoke($"[Godot-MCP] resolved '{assemblyName.Name}' -> {path}");
                return loaded;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[Godot-MCP] failed to load '{assemblyName.Name}' from {path}: {ex.Message}");
                return null;
            }
        }
    }
}
