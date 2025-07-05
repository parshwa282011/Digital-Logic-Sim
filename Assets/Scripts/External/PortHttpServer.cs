// Simple HTTP server for PortIn/PortOut chips
// This is a minimal implementation for macOS/Unity using System.Net
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DLS.External
{
    public static class PortHttpServer
    {
        private static HttpListener listener;
        private static Thread serverThread;

        private static Dictionary<uint, string> names = new();
        private static Dictionary<uint, uint> portOutValues = new();
        private static Dictionary<uint, uint> portInValues = new();
        private static bool running = false;
        static bool isSyncing = false; // Prevents infinite sync loops

        // --- Sync with PortSocketServer ---
        public static Action<uint, string> OnSetPortName;
        public static Action<uint, uint> OnSetPortOutValue;
        public static Action<uint, uint> OnSetPortInValue;

        static PortHttpServer()
        {
            // Register sync callbacks for WebSocket server
            OnSetPortName += (port, name) => {
                DLS.External.PortSocketServer.SetPortName(port, name, true);
            };
            OnSetPortOutValue += (port, value) => {
                DLS.External.PortSocketServer.SetPortOutValue(port, value,true);
            };
            OnSetPortInValue += (port, value) => {
                DLS.External.PortSocketServer.SyncSetPortInValue(port, value, true);
            };
        }

        public static void Start()
        {
            if (running) return;
            running = true;
            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:9999/");
            serverThread = new Thread(ServerLoop);
            serverThread.Start();
        }

        public static void Stop()
        {
            running = false;
            try
            {
                listener?.Abort(); // Forcefully aborts any blocking GetContext
                listener?.Close(); // Disposes the listener
            }
            catch { }
            listener = null;
            if (serverThread != null && serverThread.IsAlive)
            {
                try { serverThread.Join(500); } catch { }
            }
            serverThread = null;
        }

        public static void StartBoth()
        {
            if (!running) Start();
            DLS.External.PortSocketServer.Start();
        }

        public static void SetPortName(uint port, string name)
        {
            lock (names)
            {
                names[port] = name;
            }
            BroadcastPortStates();
            OnSetPortName?.Invoke(port, name);
        }

        public static void SetPortOutValue(uint port, uint value)
        {
            lock (portOutValues)
            {
                portOutValues[port] = value;
            }
            BroadcastPortStates();
            OnSetPortOutValue?.Invoke(port, value);
        }

        public static void SyncSetPortInValue(uint port, uint value, bool isSync = false)
        {
            if (isSyncing && isSync) return; // Prevent infinite sync only if called as a sync
            if (isSync) isSyncing = true;
            uint safeValue = value;
            lock (portInValues) portInValues[port] = safeValue;
            BroadcastPortStates();
            OnSetPortInValue?.Invoke(port, safeValue);
            if (isSync) isSyncing = false;
        }

        // New: direct input from user or external, not a sync
        public static void SetPortInValue(uint port, uint value)
        {
            SyncSetPortInValue(port, value, false);
        }

        public static uint GetPortInValue(uint port)
        {
            if (portInValues.TryGetValue(port, out var value))
            {
                return value;
            }else
            {
                portInValues[port] = 0;
                return 0;
            }
        }

        private static void ServerLoop()
        {
            try { listener.Start(); } catch { return; }
            while (running)
            {
                try
                {
                    var ctx = listener.GetContext();
                    var req = ctx.Request;
                    var resp = ctx.Response;
                    string path = req.Url.AbsolutePath.ToLower();
                    if (path == "/")
                    {
                        // Show all port outs and ins
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("<h1>Port Chips</h1>");
                        sb.AppendLine("<h2>Port Out</h2><ul>");
                        lock (portOutValues)
                            foreach (var kv in portOutValues)
                                sb.AppendLine($"<li>{kv.Key}: {names[kv.Key]}: {kv.Value}</li>");
                        sb.AppendLine("</ul><h2>PortIn</h2><ul>");
                        lock (portInValues)
                            foreach (var kv in portInValues)
                                sb.AppendLine($"<li>{kv.Key}: {names[kv.Key]}: {kv.Value} <form method='get' action='/setin'><input type='hidden' name='name' value='{kv.Key}'/><input name='value' value='{kv.Value}'/><button type='submit'>Set</button></form></li>");
                        sb.AppendLine("</ul>");
                        WriteString(resp, sb.ToString());
                    }
                    else if (path == "/setin" && req.HttpMethod == "GET")
                    {
                        string name = req.QueryString["name"] ?? "";
                        string valueStr = req.QueryString["value"] ?? "0";
                        Debug.Log($"Setting port in {name} to {valueStr}");
                        uint value = uint.TryParse(valueStr, out var v) ? v : 0;
                        uint id = uint.TryParse(name, out var w) ? w : 0;
                        if (id == 0)
                        {
                            resp.StatusCode = 400;
                            WriteString(resp, "Invalid port ID");
                            return;
                        }
                        if (!portInValues.ContainsKey(id))
                        {
                            resp.StatusCode = 404;
                            WriteString(resp, "Port not found");
                            return;
                        }
                        lock (portInValues) portInValues[id] = value;
                        resp.Redirect("/");
                        resp.Close();
                    }
                    else
                    {
                        resp.StatusCode = 404;
                        WriteString(resp, "Not found");
                    }
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped/aborted, exit thread
                    break;
                }
                catch { }
            }
        }

        private static void WriteString(HttpListenerResponse resp, string s)
        {
            byte[] buf = Encoding.UTF8.GetBytes(s);
            resp.ContentType = "text/html";
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
            resp.Close();
        }

        private static void BroadcastPortStates()
        {
            // This method should broadcast the current state of ports to all connected clients
            // Implementation depends on the specific requirements and is not provided here
        }
    }
}