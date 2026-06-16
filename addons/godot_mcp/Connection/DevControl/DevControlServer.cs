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
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.MainThreadDispatch;
using com.IvanMurzak.Godot.MCP.UI;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Connection.DevControl
{
    /// <summary>
    /// DEV-ONLY inject/control HTTP bridge for the "AI Game Developer" editor dock. A
    /// <see cref="HttpListener"/> bound to <c>http://127.0.0.1:&lt;port&gt;/</c> (loopback ONLY) whose accept
    /// loop runs on a background thread; it parses JSON in/out (<see cref="System.Text.Json"/>), routes via the
    /// pure-managed <see cref="DevControlRouter"/>, and hops every dock-touching handler onto the editor main
    /// thread (a <see cref="TaskCompletionSource{TResult}"/> + <see cref="MainThreadDispatcher.Enqueue"/>),
    /// because ALL Godot Control access must happen on the main thread.
    ///
    /// <para>
    /// SECURITY: the security boundary IS this server. It binds 127.0.0.1 only (never a routable interface)
    /// and is started ONLY when <c>GODOT_MCP_DEV_CONTROL=1</c> (see <c>GodotMcpPlugin.BootMcp</c>), so a
    /// shipped addon never listens. Editor-only (<c>#if TOOLS</c>); disposed on plugin <c>_ExitTree</c>.
    /// </para>
    /// </summary>
    public sealed class DevControlServer : IDisposable
    {
        /// <summary>Default port when <c>GODOT_MCP_DEV_CONTROL_PORT</c> is unset.</summary>
        public const int DefaultPort = 9920;

        /// <summary>Loopback-only bind host — never a routable interface (the security boundary).</summary>
        const string BindHost = "127.0.0.1";

        /// <summary>How long a main-thread hop may take before the handler gives up and 500s (dispatcher stalled).</summary>
        static readonly TimeSpan MainThreadTimeout = TimeSpan.FromSeconds(10);

        readonly GodotMcpDock _dock;
        readonly int _port;
        readonly HttpListener _listener = new HttpListener();
        readonly CancellationTokenSource _cts = new CancellationTokenSource();
        Thread? _acceptThread;
        bool _disposed;

        /// <summary>The base URL this server listens on (after <see cref="Start"/>).</summary>
        public string BaseUrl => $"http://{BindHost}:{_port}/";

        /// <summary>
        /// Construct the bridge bound to <paramref name="dock"/>, listening on <paramref name="port"/>
        /// (loopback only). Call <see cref="Start"/> to begin accepting; the ctor does not open the socket.
        /// </summary>
        public DevControlServer(GodotMcpDock dock, int port)
        {
            _dock = dock ?? throw new ArgumentNullException(nameof(dock));
            _port = port;
        }

        /// <summary>
        /// Open the loopback listener and start the background accept loop. Idempotent-safe to call once.
        /// Logs <c>[dev-control] Listening on http://127.0.0.1:&lt;port&gt;</c> on success; a bind failure
        /// (port in use) is logged as an error and leaves the server inert rather than throwing into plugin boot.
        /// </summary>
        public void Start()
        {
            try
            {
                _listener.Prefixes.Add(BaseUrl);
                _listener.Start();
            }
            catch (Exception ex)
            {
                GD.PushError($"[dev-control] failed to bind {BaseUrl}: {ex.Message}");
                return;
            }

            _acceptThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "godot-mcp-dev-control",
            };
            _acceptThread.Start();

            GD.Print($"[dev-control] Listening on http://{BindHost}:{_port}");
        }

        void AcceptLoop()
        {
            while (!_cts.IsCancellationRequested && _listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext(); // blocks until a request or the listener is stopped
                }
                catch (Exception)
                {
                    // Listener stopped/disposed (teardown) or a transient accept error — exit the loop on
                    // cancellation, otherwise keep accepting. Never let one bad accept kill the server.
                    if (_cts.IsCancellationRequested || !_listener.IsListening)
                        break;
                    continue;
                }

                try
                {
                    HandleRequest(context);
                }
                catch (Exception ex)
                {
                    // A handler-level failure must never kill the accept loop: answer 500 and keep serving.
                    TryWrite(context, 500, $"{{\"ok\":false,\"error\":{JsonString(ex.Message)}}}");
                }
            }
        }

        void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var method = request.HttpMethod ?? string.Empty;
            // Strip query string + a trailing slash so the router matches the canonical path.
            var path = (request.Url?.AbsolutePath ?? string.Empty).TrimEnd('/');
            if (path.Length == 0)
                path = "/";

            var command = DevControlRouter.Route(method, path);
            if (command == DevControlRouter.Command.Unknown)
            {
                TryWrite(context, 404, $"{{\"ok\":false,\"error\":\"no route for {method} {JsonInner(path)}\"}}");
                return;
            }

            // Health is the only route that does not touch a Control off the bat (it reports dock presence).
            if (command == DevControlRouter.Command.Health)
            {
                var status = RunOnMainThread(() => _dock.DevStateJson());
                TryWrite(context, 200, $"{{\"ok\":true,\"dockPresent\":true,\"state\":{status}}}");
                return;
            }

            if (command == DevControlRouter.Command.State)
            {
                var state = RunOnMainThread(() => _dock.DevStateJson());
                TryWrite(context, 200, $"{{\"ok\":true,\"dock\":{state}}}");
                return;
            }

            // The remaining commands read a JSON body.
            var body = ReadBody(request);
            using var doc = ParseJson(body);
            var root = doc?.RootElement;

            switch (command)
            {
                case DevControlRouter.Command.InjectConnectionStatus:
                {
                    var value = GetString(root, "status");
                    if (!DevControlRouter.TryParseConnectionStatus(value, out _))
                    {
                        TryWrite(context, 400, $"{{\"ok\":false,\"error\":\"invalid connection-status {JsonInner(value)}\"}}");
                        break;
                    }
                    RunOnMainThread(() => { _dock.DevInjectConnectionStatus(value!); return true; });
                    TryWrite(context, 200, "{\"ok\":true}");
                    break;
                }
                case DevControlRouter.Command.InjectServerStatus:
                {
                    var value = GetString(root, "status");
                    if (!DevControlRouter.TryParseServerStatus(value, out _))
                    {
                        TryWrite(context, 400, $"{{\"ok\":false,\"error\":\"invalid server-status {JsonInner(value)}\"}}");
                        break;
                    }
                    RunOnMainThread(() => { _dock.DevInjectServerStatus(value!); return true; });
                    TryWrite(context, 200, "{\"ok\":true}");
                    break;
                }
                case DevControlRouter.Command.InjectAgents:
                {
                    // Body: {"count": N}. N>0 paints N fake connected AI agents onto the dock; N<=0 clears the override.
                    var count = 0;
                    if (root.HasValue && root.Value.TryGetProperty("count", out var countEl) &&
                        countEl.ValueKind == JsonValueKind.Number && countEl.TryGetInt32(out var parsedCount))
                        count = parsedCount;
                    RunOnMainThread(() => { _dock.DevInjectAgents(count); return true; });
                    TryWrite(context, 200, "{\"ok\":true}");
                    break;
                }
                case DevControlRouter.Command.ControlServerUrl:
                {
                    var url = GetString(root, "url");
                    RunOnMainThread(() => { _dock.DevSetServerUrl(url ?? string.Empty); return true; });
                    TryWrite(context, 200, "{\"ok\":true}");
                    break;
                }
                case DevControlRouter.Command.ControlSelectAgent:
                {
                    // Accept either {agent} or {agentId} (the spec uses both across endpoint docs).
                    var agent = GetString(root, "agent") ?? GetString(root, "agentId");
                    var ok = RunOnMainThread(() => _dock.DevSelectAgent(agent ?? string.Empty));
                    TryWrite(context, ok ? 200 : 404,
                        ok ? "{\"ok\":true}" : $"{{\"ok\":false,\"error\":\"unknown agent {JsonInner(agent)}\"}}");
                    break;
                }
                case DevControlRouter.Command.ControlClick:
                {
                    var target = GetString(root, "target");
                    // Invalid vocabulary → 400 (bad request); valid target whose button isn't present in the
                    // current mode/state → 409 (the dock returns false). This keeps "bad input" distinct from
                    // "button absent" and never surfaces a 500 for a client typo.
                    if (!DevControlRouter.TryNormalizeClickTarget(target, out _))
                    {
                        TryWrite(context, 400, $"{{\"ok\":false,\"error\":\"invalid target {JsonInner(target)}\"}}");
                        break;
                    }
                    var ok = RunOnMainThread(() => _dock.DevClick(target!));
                    TryWrite(context, ok ? 200 : 409,
                        ok ? "{\"ok\":true}" : $"{{\"ok\":false,\"error\":\"target not clickable {JsonInner(target)}\"}}");
                    break;
                }
                case DevControlRouter.Command.ControlSetSegment:
                {
                    var control = GetString(root, "control");
                    var option = GetString(root, "option");
                    if (!DevControlRouter.TryNormalizeSegment(control, option, out _, out _))
                    {
                        TryWrite(context, 400, $"{{\"ok\":false,\"error\":\"invalid segment {JsonInner(control)}/{JsonInner(option)}\"}}");
                        break;
                    }
                    var ok = RunOnMainThread(() => _dock.DevSetSegment(control!, option!));
                    TryWrite(context, ok ? 200 : 409,
                        ok ? "{\"ok\":true}" : $"{{\"ok\":false,\"error\":\"segment not present {JsonInner(control)}/{JsonInner(option)}\"}}");
                    break;
                }
                case DevControlRouter.Command.ControlCloudAuthorize:
                {
                    // DEV-ONLY: simulate a successful Cloud authorization (persist token + reconnect) so the
                    // auth UI + reconnect path are testable without a live OAuth. The token is freeform.
                    var token = GetString(root, "token");
                    if (string.IsNullOrEmpty(token))
                    {
                        TryWrite(context, 400, "{\"ok\":false,\"error\":\"missing token\"}");
                        break;
                    }
                    var ok = RunOnMainThread(() => _dock.DevCloudAuthorize(token!));
                    TryWrite(context, ok ? 200 : 409,
                        ok ? "{\"ok\":true}" : "{\"ok\":false,\"error\":\"connection panel not present\"}");
                    break;
                }
                default:
                    TryWrite(context, 404, "{\"ok\":false,\"error\":\"unhandled command\"}");
                    break;
            }
        }

        /// <summary>
        /// Run <paramref name="work"/> on the editor main thread and return its result, marshalling any
        /// exception back to this background thread. Hops via <see cref="MainThreadDispatcher.Enqueue"/> + a
        /// <see cref="TaskCompletionSource{TResult}"/>; when already on the main thread (or no dispatcher is in
        /// the tree) it runs inline. Throws on a <see cref="MainThreadTimeout"/> stall so the handler 500s
        /// rather than hanging the request forever.
        /// </summary>
        static T RunOnMainThread<T>(Func<T> work)
        {
            if (MainThreadDispatcher.Instance == null || MainThreadDispatcher.IsMainThread)
                return work();

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            MainThreadDispatcher.Enqueue(() =>
            {
                try { tcs.TrySetResult(work()); }
                catch (Exception ex) { tcs.TrySetException(ex); }
            });

            if (!tcs.Task.Wait(MainThreadTimeout))
                throw new TimeoutException("editor main-thread hop timed out (dispatcher stalled).");

            return tcs.Task.GetAwaiter().GetResult();
        }

        static string ReadBody(HttpListenerRequest request)
        {
            if (!request.HasEntityBody)
                return string.Empty;
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
            return reader.ReadToEnd();
        }

        static JsonDocument? ParseJson(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;
            try { return JsonDocument.Parse(body); }
            catch { return null; }
        }

        static string? GetString(JsonElement? root, string property)
        {
            if (root is not { ValueKind: JsonValueKind.Object } obj)
                return null;
            if (!obj.TryGetProperty(property, out var el))
                return null;
            return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
        }

        static void TryWrite(HttpListenerContext context, int statusCode, string json)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.OutputStream.Close();
            }
            catch
            {
                // Client hung up mid-write — nothing actionable; the accept loop keeps serving.
            }
        }

        /// <summary>JSON-encode a string AS a complete JSON string literal (with surrounding quotes).</summary>
        static string JsonString(string? value) => JsonSerializer.Serialize(value ?? string.Empty);

        /// <summary>JSON-escape a string WITHOUT surrounding quotes (for embedding inside a larger literal).</summary>
        static string JsonInner(string? value)
        {
            var s = JsonString(value);
            return s.Length >= 2 ? s.Substring(1, s.Length - 2) : s;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            try { _cts.Cancel(); } catch { /* already disposed */ }

            // Stop unblocks the GetContext() call in the accept loop.
            try { if (_listener.IsListening) _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }

            try { _acceptThread?.Join(TimeSpan.FromSeconds(2)); } catch { }

            _cts.Dispose();
        }
    }
}
#endif
