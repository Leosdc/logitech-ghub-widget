using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GHubCli.Models;

namespace GHubCli.Services
{
    public class LgHubCliService : IDisposable
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 9010;
        public int RefreshIntervalSeconds { get; set; } = 2;

        public bool IsConnected { get; private set; }
        public string StatusMessage { get; private set; } = "Aguardando conexão...";

        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cts;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly Dictionary<string, CliDeviceInfo> _deviceMap = new();
        private bool _disposed;

        public event Action<IReadOnlyList<CliDeviceInfo>>? DevicesUpdated;
        public event Action<bool, string>? ConnectionStatusChanged;

        public Uri WebSocketUri => new($"ws://{Host}:{Port}");

        public async Task StartAsync(CancellationToken externalToken = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            var ct = _cts.Token;

            while (!ct.IsCancellationRequested && !_disposed)
            {
                try
                {
                    UpdateStatus(false, $"Conectando ao Logitech G HUB em {WebSocketUri}...");
                    _webSocket?.Dispose();
                    _webSocket = new ClientWebSocket();
                    _webSocket.Options.AddSubProtocol("json");
                    _webSocket.Options.SetRequestHeader("Origin", "file://");

                    using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        connectCts.CancelAfter(4000);
                        await _webSocket.ConnectAsync(WebSocketUri, connectCts.Token);
                    }

                    if (_webSocket.State == WebSocketState.Open)
                    {
                        UpdateStatus(true, $"Conectado ao G HUB ({WebSocketUri})");

                        // Subscribe to live events
                        await SendJsonAsync("{\"path\":\"/devices/state/changed\",\"verb\":\"SUBSCRIBE\"}", ct);
                        await SendJsonAsync("{\"path\":\"/battery/state/changed\",\"verb\":\"SUBSCRIBE\"}", ct);

                        // Initial fetch
                        await SendJsonAsync("{\"path\":\"/devices/list\",\"verb\":\"GET\"}", ct);

                        var listenTask = ListenLoopAsync(_webSocket, ct);
                        var refreshTask = RefreshTimerLoopAsync(ct);

                        await Task.WhenAny(listenTask, refreshTask);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    UpdateStatus(false, $"Falha de conexão: {ex.Message}. Tentando novamente...");
                }
                finally
                {
                    UpdateStatus(false, "Desconectado do G HUB");
                    if (_webSocket != null)
                    {
                        try { _webSocket.Abort(); } catch { }
                        _webSocket.Dispose();
                        _webSocket = null;
                    }
                }

                if (!ct.IsCancellationRequested && !_disposed)
                {
                    try { await Task.Delay(3000, ct); } catch { }
                }
            }
        }

        public async Task<IReadOnlyList<CliDeviceInfo>> FetchSnapshotAsync(TimeSpan timeout)
        {
            using var localCts = new CancellationTokenSource(timeout);
            var ct = localCts.Token;

            using var ws = new ClientWebSocket();
            ws.Options.AddSubProtocol("json");
            ws.Options.SetRequestHeader("Origin", "file://");

            await ws.ConnectAsync(WebSocketUri, ct);

            // Fetch device list
            var reqBytes = Encoding.UTF8.GetBytes("{\"path\":\"/devices/list\",\"verb\":\"GET\"}");
            await ws.SendAsync(new ArraySegment<byte>(reqBytes), WebSocketMessageType.Text, true, ct);

            var buffer = new byte[65536];
            using var ms = new MemoryStream();
            var pendingBatteryIds = new HashSet<string>();
            bool devicesListReceived = false;

            try
            {
                var deadline = DateTime.UtcNow + timeout;
                while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    ms.Write(buffer, 0, result.Count);
                    if (result.EndOfMessage)
                    {
                        var json = Encoding.UTF8.GetString(ms.ToArray());
                        ms.SetLength(0);

                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            var path = root.TryGetProperty("path", out var pProp) ? pProp.GetString() : null;

                            if (path == "/devices/list")
                            {
                                HandleDeviceList(root);
                                devicesListReceived = true;
                                lock (_deviceMap)
                                {
                                    pendingBatteryIds = new HashSet<string>(_deviceMap.Keys);
                                }

                                if (pendingBatteryIds.Count == 0)
                                {
                                    break;
                                }

                                foreach (var id in pendingBatteryIds)
                                {
                                    var bReq = Encoding.UTF8.GetBytes($"{{\"path\":\"/battery/{id}/state\",\"verb\":\"GET\"}}");
                                    await ws.SendAsync(new ArraySegment<byte>(bReq), WebSocketMessageType.Text, true, ct);
                                }
                            }
                            else if (path != null && path.StartsWith("/battery/"))
                            {
                                HandleBatteryState(root, path);
                                var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length >= 2 && parts[0] == "battery")
                                {
                                    pendingBatteryIds.Remove(parts[1]);
                                }

                                if (devicesListReceived && pendingBatteryIds.Count == 0)
                                {
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (OperationCanceledException) { }

            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None); } catch { }

            lock (_deviceMap)
            {
                return _deviceMap.Values.OrderBy(d => d.CategoryLabel).ThenBy(d => d.Name).ToList();
            }
        }

        public async Task RefreshNowAsync()
        {
            if (IsConnected && _webSocket?.State == WebSocketState.Open && _cts != null)
            {
                await SendJsonAsync("{\"path\":\"/devices/list\",\"verb\":\"GET\"}", _cts.Token);

                List<string> deviceIds;
                lock (_deviceMap)
                {
                    deviceIds = new List<string>(_deviceMap.Keys);
                }

                foreach (var id in deviceIds)
                {
                    var req = $"{{\"path\":\"/battery/{id}/state\",\"verb\":\"GET\"}}";
                    await SendJsonAsync(req, _cts.Token);
                }
            }
        }

        private async Task RefreshTimerLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                int delaySec = Math.Max(1, RefreshIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
                await RefreshNowAsync();
            }
        }

        private async Task ListenLoopAsync(ClientWebSocket ws, CancellationToken ct)
        {
            var buffer = new byte[65536];
            using var ms = new MemoryStream();

            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                ms.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    var json = Encoding.UTF8.GetString(ms.ToArray());
                    ms.SetLength(0);
                    ProcessMessage(json);
                }
            }
        }

        private void ProcessMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var path = root.TryGetProperty("path", out var pProp) ? pProp.GetString() : null;

                if (string.IsNullOrEmpty(path)) return;

                if (path == "/devices/list")
                {
                    HandleDeviceList(root);
                }
                else if (path.StartsWith("/battery/") && path.EndsWith("/state"))
                {
                    HandleBatteryState(root, path);
                }
                else if (path == "/battery/state/changed")
                {
                    HandleBatteryState(root, path);
                }
                else if (path == "/devices/state/changed")
                {
                    _ = RefreshNowAsync();
                }
            }
            catch { }
        }

        private void HandleDeviceList(JsonElement root)
        {
            if (!root.TryGetProperty("payload", out var payload) ||
                !payload.TryGetProperty("deviceInfos", out var deviceInfos) ||
                deviceInfos.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var currentIds = new HashSet<string>();

            foreach (var item in deviceInfos.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString() ?? string.Empty;
                if (string.IsNullOrEmpty(id)) continue;
                currentIds.Add(id);

                var name = item.TryGetProperty("extendedDisplayName", out var nameProp) ? nameProp.GetString() : null;
                if (string.IsNullOrEmpty(name))
                {
                    name = item.TryGetProperty("displayName", out var dProp) ? dProp.GetString() : null;
                }
                var state = item.TryGetProperty("state", out var sProp) ? sProp.GetString() : "UNKNOWN";
                var connType = item.TryGetProperty("connectionType", out var connProp) ? connProp.GetString() : "WIRELESS";
                var devType = "UNKNOWN";

                int activeInterfaceCount = 0;
                if (item.TryGetProperty("activeInterfaces", out var interfaces) && interfaces.ValueKind == JsonValueKind.Array)
                {
                    activeInterfaceCount = interfaces.GetArrayLength();
                    foreach (var iface in interfaces.EnumerateArray())
                    {
                        if (iface.TryGetProperty("deviceType", out var dt))
                        {
                            devType = dt.GetString() ?? "UNKNOWN";
                            break;
                        }
                    }
                }

                bool isActive = string.Equals(state, "ACTIVE", StringComparison.OrdinalIgnoreCase);
                if (activeInterfaceCount == 0 && string.Equals(connType, "WIRELESS", StringComparison.OrdinalIgnoreCase))
                {
                    isActive = false;
                }

                lock (_deviceMap)
                {
                    if (!_deviceMap.TryGetValue(id, out var dev))
                    {
                        dev = new CliDeviceInfo
                        {
                            Id = id,
                            Name = name ?? id,
                            DeviceType = devType,
                            ConnectionType = connType ?? "WIRELESS",
                            IsActive = isActive
                        };
                        _deviceMap[id] = dev;
                    }
                    else
                    {
                        dev.Name = name ?? dev.Name;
                        dev.DeviceType = devType != "UNKNOWN" ? devType : dev.DeviceType;
                        dev.ConnectionType = connType ?? dev.ConnectionType;
                        dev.IsActive = isActive;
                    }
                }

                if (isActive)
                {
                    _ = SendJsonAsync($"{{\"path\":\"/battery/{id}/state\",\"verb\":\"GET\"}}", CancellationToken.None);
                }
            }

            lock (_deviceMap)
            {
                foreach (var kvp in _deviceMap)
                {
                    if (!currentIds.Contains(kvp.Key))
                    {
                        kvp.Value.IsActive = false;
                    }
                }
            }

            NotifyDevicesUpdated();
        }

        private void HandleBatteryState(JsonElement root, string path)
        {
            string? deviceId = null;
            if (root.TryGetProperty("payload", out var payload) && payload.TryGetProperty("deviceId", out var dIdProp))
            {
                deviceId = dIdProp.GetString();
            }

            if (string.IsNullOrEmpty(deviceId))
            {
                var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[0] == "battery")
                {
                    deviceId = parts[1];
                }
            }

            if (string.IsNullOrEmpty(deviceId)) return;

            bool hasError = false;
            if (root.TryGetProperty("result", out var resProp))
            {
                if (resProp.TryGetProperty("code", out var codeProp))
                {
                    var code = codeProp.GetString();
                    if (!string.IsNullOrEmpty(code) && !string.Equals(code, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        hasError = true;
                    }
                }
            }

            if (hasError || !root.TryGetProperty("payload", out payload))
            {
                lock (_deviceMap)
                {
                    if (_deviceMap.TryGetValue(deviceId, out var dev))
                    {
                        dev.IsActive = false;
                    }
                }
                NotifyDevicesUpdated();
                return;
            }

            int percentage = payload.TryGetProperty("percentage", out var pProp) ? pProp.GetInt32() : 0;
            bool charging = payload.TryGetProperty("charging", out var chProp) && chProp.GetBoolean();
            double mileage = payload.TryGetProperty("mileage", out var mProp) ? mProp.GetDouble() : 0;
            double maxLifespan = payload.TryGetProperty("maxLifeSpan", out var mlProp) ? mlProp.GetDouble() : 0;

            double consumption = 0;
            if (payload.TryGetProperty("consumption", out var consProp))
            {
                if (consProp.TryGetProperty("value", out var vProp))
                    consumption = vProp.GetDouble();
            }

            lock (_deviceMap)
            {
                if (_deviceMap.TryGetValue(deviceId, out var dev))
                {
                    dev.BatteryPercentage = percentage;
                    dev.IsCharging = charging;
                    dev.Mileage = mileage;
                    dev.MaxLifespan = maxLifespan;
                    dev.ConsumptionMw = consumption;
                    dev.IsActive = true;
                    dev.LastUpdated = DateTime.Now;
                }
            }

            NotifyDevicesUpdated();
        }

        private void NotifyDevicesUpdated()
        {
            List<CliDeviceInfo> list;
            lock (_deviceMap)
            {
                list = _deviceMap.Values.OrderBy(d => d.CategoryLabel).ThenBy(d => d.Name).ToList();
            }
            DevicesUpdated?.Invoke(list);
        }

        private async Task SendJsonAsync(string json, CancellationToken ct)
        {
            if (_webSocket?.State != WebSocketState.Open) return;
            await _sendLock.WaitAsync(ct);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
            }
            catch { }
            finally
            {
                _sendLock.Release();
            }
        }

        private void UpdateStatus(bool connected, string message)
        {
            IsConnected = connected;
            StatusMessage = message;
            ConnectionStatusChanged?.Invoke(connected, message);
        }

        public void Dispose()
        {
            _disposed = true;
            _cts?.Cancel();
            _webSocket?.Dispose();
            _sendLock.Dispose();
        }
    }
}
