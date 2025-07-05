// WebSocket server for PortIn/PortOut chips, similar to PortHttpServer
// Uses System.Net.WebSockets for WebSocket support
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DLS.External
{
    public static class PortSocketServer
    {
        private static HttpListener httpListener;
        private static List<WebSocket> allSockets = new();
        private static Dictionary<uint, string> names = new();
        private static Dictionary<uint, uint> portOutValues = new();
        private static Dictionary<uint, uint> portInValues = new();
        private static bool running = false;
        private const int Port = 9001;
        private static Thread serverThread;
        private static bool isSyncing = false; // Prevents infinite sync loops

        // --- Sync with PortHttpServer ---
        public static Action<uint, string> OnSetPortName;
        public static Action<uint, uint> OnSetPortOutValue;
        public static Action<uint, uint> OnSetPortInValue;

        static PortSocketServer()
        {
            // Register sync callbacks for HTTP server
            OnSetPortName += (port, name) => {
                if (!isSyncing) DLS.External.PortHttpServer.SetPortName(port, name);
            };
            OnSetPortOutValue += (port, value) => {
                if (!isSyncing) DLS.External.PortHttpServer.SetPortOutValue(port, value);
            };
            OnSetPortInValue += (port, value) => {
                if (!isSyncing) DLS.External.PortHttpServer.SetPortInValue(port, value);
            };
        }

        public static void Start()
        {
            if (running) return;
            running = true;
            httpListener = new HttpListener();
            httpListener.Prefixes.Add($"http://*:{Port}/ws/");
            serverThread = new Thread(ServerLoop) { IsBackground = true };
            serverThread.Start();
        }

        public static void Stop()
        {
            running = false;
            try { httpListener?.Abort(); httpListener?.Close(); } catch { }
            httpListener = null;
            lock (allSockets) foreach (var ws in allSockets) try { ws.Abort(); ws.Dispose(); } catch { }
            allSockets.Clear();
            if (serverThread != null && serverThread.IsAlive) try { serverThread.Join(500); } catch { }
            serverThread = null;
        }

        private static async void ServerLoop()
        {
            try { httpListener.Start(); } catch (Exception e) { Debug.LogError($"WebSocket server failed to start: {e.Message}"); return; }
            while (running)
            {
                try
                {
                    var ctx = await httpListener.GetContextAsync();
                    if (ctx.Request.IsWebSocketRequest)
                    {
                        _ = HandleWebSocket(ctx);
                    }
                    else
                    {
                        ctx.Response.StatusCode = 400;
                        ctx.Response.Close();
                    }
                }
                catch { break; }
            }
        }

        private static async Task HandleWebSocket(HttpListenerContext ctx)
        {
            WebSocket ws = null;
            try
            {
                var wsCtx = await ctx.AcceptWebSocketAsync(null);
                ws = wsCtx.WebSocket;
                lock (allSockets) allSockets.Add(ws);
                await SendAllPortStates(ws);
                var buffer = new ArraySegment<byte>(new byte[4096]);
                while (ws.State == WebSocketState.Open && running)
                {
                    var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    var msg = System.Text.Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                    HandleMessage(ws, msg);
                }
            }
            catch (Exception e) { Debug.LogWarning($"WebSocket error: {e.Message}"); }
            finally
            {
                if (ws != null)
                {
                    lock (allSockets) allSockets.Remove(ws);
                    try { ws.Dispose(); } catch { }
                }
            }
        }

        public static void StartBoth()
        {
            if (!running) Start();
            DLS.External.PortHttpServer.Start();
        }

        public static void SetPortName(uint port, string name, bool isSync = false)
        {
            lock (names) { names[port] = name; }
            BroadcastPortStates();
            if (isSync){return;} // Prevent infinite sync loops
            OnSetPortName?.Invoke(port, name);
        }

        public static void SetPortOutValue(uint port, uint value, bool isSync = false)
        {
            lock (portOutValues) { portOutValues[port] = value; }
             // Prevent infinite sync loops
            BroadcastPortStates();
            if (isSync) return;
            OnSetPortOutValue?.Invoke(port, value);
        }

        public static uint GetPortInValue(uint port)
        {
            lock (portInValues)
            {
                if (portInValues.TryGetValue(port, out var value))
                    return value;
                portInValues[port] = 0;
                return 0;
            }
        }

        // Called by HTTP server to sync PortIn value
        public static void SyncSetPortInValue(uint port, uint value, bool isSync = false)
        {
            uint safeValue = value;
            lock (portInValues) portInValues[port] = safeValue;
            BroadcastPortStates();
            if (isSync) return; // Prevent infinite sync loops
            OnSetPortInValue?.Invoke(port, safeValue);
        }

        // New: direct input from user or external, not a sync
        public static void SetPortInValue(uint port, uint value)
        {
            SyncSetPortInValue(port, value, false);
        }

        private static void HandleMessage(WebSocket ws, string message)
        {
            try
            {
                var msg = JsonUtility.FromJson<PortMsg>(message);
                if (msg.type == "setin")
                {
                    lock (portInValues) portInValues[msg.port] = msg.value;
                    BroadcastPortStates();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"WebSocket message error: {e.Message}");
            }
        }

        private static void BroadcastPortStates()
        {
            var json = GetAllPortStatesJson();
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            lock (allSockets)
            {
                foreach (var ws in allSockets)
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        try { ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None); } catch { }
                    }
                }
            }
        }

        private static async Task SendAllPortStates(WebSocket ws)
        {
            var json = GetAllPortStatesJson();
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            if (ws.State == WebSocketState.Open)
            {
                try { await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None); } catch { }
            }
        }

        private static string GetAllPortStatesJson()
        {
            var portOutList = new List<PortEntry>();
            var portInList = new List<PortEntry>();
            lock (portOutValues)
                foreach (var kv in portOutValues)
                    portOutList.Add(new PortEntry { port = kv.Key, name = names.TryGetValue(kv.Key, out var n) ? n : "", value = kv.Value });
            lock (portInValues)
                foreach (var kv in portInValues)
                    portInList.Add(new PortEntry { port = kv.Key, name = names.TryGetValue(kv.Key, out var n) ? n : "", value = kv.Value });
            var state = new PortStateMsg
            {
                portOut = portOutList.ToArray(),
                portIn = portInList.ToArray()
            };
            return JsonUtility.ToJson(state);
        }

        [Serializable]
        private struct PortMsg { public string type; public uint port; public uint value; }
        [Serializable]
        private struct PortStateMsg { public PortEntry[] portOut; public PortEntry[] portIn; }
        [Serializable]
        private struct PortEntry { public uint port; public string name; public uint value; }
    }
}
