using WpfApp = System.Windows.Application;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using GHubWidget.Models;

namespace GHubWidget.Services
{
    public class LgHubService : IDisposable
    {
        private const string WebSocketUri = "ws://127.0.0.1:9010";
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cts;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly Dictionary<string, DeviceInfo> _deviceMap = new();
        private bool _disposed;

        public ObservableCollection<DeviceInfo> Devices { get; } = new();

        public bool IsConnected { get; private set; }
        public string StatusMessage { get; private set; } = "Iniciando conexão...";
        public int RefreshIntervalSeconds { get; set; } = 5;

        public event Action<bool, string>? ConnectionStatusChanged;
        public event Action<DeviceInfo>? LowBatteryAlertTriggered;

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ConnectionWorkerAsync(_cts.Token));
        }

        public async Task RefreshNowAsync()
        {
            if (IsConnected && _webSocket?.State == WebSocketState.Open && _cts != null)
            {
                await SendJsonAsync("{\"path\":\"/devices/list\",\"verb\":\"GET\"}", _cts.Token);
            }
        }

        private async Task ConnectionWorkerAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && !_disposed)
            {
                try
                {
                    UpdateStatus(false, "Conectando ao Logitech G HUB...");
                    _webSocket = new ClientWebSocket();
                    _webSocket.Options.AddSubProtocol("json");
                    _webSocket.Options.SetRequestHeader("Origin", "file://");

                    using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        connectCts.CancelAfter(4000);
                        await _webSocket.ConnectAsync(new Uri(WebSocketUri), connectCts.Token);
                    }

                    if (_webSocket.State == WebSocketState.Open)
                    {
                        UpdateStatus(true, "Conectado ao G HUB");

                        // Initial device list request
                        await SendJsonAsync("{\"path\":\"/devices/list\",\"verb\":\"GET\"}", ct);

                        // Run listen loop & timer concurrently
                        var listenTask = ListenLoopAsync(_webSocket, ct);
                        var refreshTask = RefreshTimerLoopAsync(ct);

                        await Task.WhenAny(listenTask, refreshTask);
                    }
                }
                catch
                {
                    // Connection failed or lost
                }
                finally
                {
                    UpdateStatus(false, "Aguardando Logitech G HUB...");
                    CleanUpSocket();
                }

                if (!ct.IsCancellationRequested)
                {
                    await Task.Delay(3000, ct).ContinueWith(_ => { });
                }
            }
        }

        private async Task RefreshTimerLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && IsConnected && _webSocket?.State == WebSocketState.Open)
            {
                int delayMs = Math.Max(2, RefreshIntervalSeconds) * 1000;
                await Task.Delay(delayMs, ct);

                if (_webSocket.State == WebSocketState.Open)
                {
                    await SendJsonAsync("{\"path\":\"/devices/list\",\"verb\":\"GET\"}", ct);

                    // Also explicitly request battery for all known devices
                    List<string> deviceIds;
                    lock (_deviceMap)
                    {
                        deviceIds = new List<string>(_deviceMap.Keys);
                    }

                    foreach (var id in deviceIds)
                    {
                        var req = $"{{\"path\":\"/battery/{id}/state\",\"verb\":\"GET\"}}";
                        await SendJsonAsync(req, ct);
                    }
                }
            }
        }

        private async Task ListenLoopAsync(ClientWebSocket ws, CancellationToken ct)
        {
            var buffer = new byte[65536];
            using var ms = new MemoryStream();

            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                ms.SetLength(0);

                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(ms.ToArray());
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

                if (!root.TryGetProperty("path", out var pathProp))
                    return;

                var path = pathProp.GetString() ?? string.Empty;

                if (path == "/devices/list")
                {
                    HandleDevicesList(root);
                }
                else if (path.StartsWith("/battery/") && path.EndsWith("/state"))
                {
                    HandleBatteryState(root, path);
                }
            }
            catch { }
        }

        private void HandleDevicesList(JsonElement root)
        {
            if (!root.TryGetProperty("payload", out var payload) ||
                !payload.TryGetProperty("deviceInfos", out var deviceInfos) ||
                deviceInfos.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in deviceInfos.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString() ?? string.Empty;
                var name = item.TryGetProperty("extendedDisplayName", out var nameProp) ? nameProp.GetString() : null;
                if (string.IsNullOrEmpty(name))
                {
                    name = item.TryGetProperty("displayName", out var dProp) ? dProp.GetString() : id;
                }

                var state = item.TryGetProperty("state", out var stateProp) ? stateProp.GetString() : "ACTIVE";
                var connType = item.TryGetProperty("connectionType", out var connProp) ? connProp.GetString() : "WIRELESS";
                var devType = "UNKNOWN";

                if (item.TryGetProperty("activeInterfaces", out var interfaces) && interfaces.ValueKind == JsonValueKind.Array)
                {
                    foreach (var iface in interfaces.EnumerateArray())
                    {
                        if (iface.TryGetProperty("deviceType", out var dt))
                        {
                            devType = dt.GetString() ?? devType;
                            break;
                        }
                    }
                }

                WpfApp.Current?.Dispatcher.Invoke(() =>
                {
                    lock (_deviceMap)
                    {
                        if (!_deviceMap.TryGetValue(id, out var dev))
                        {
                            dev = new DeviceInfo
                            {
                                Id = id,
                                Name = name ?? id,
                                DeviceType = devType,
                                ConnectionType = connType ?? "WIRELESS",
                                IsActive = state == "ACTIVE"
                            };
                            _deviceMap[id] = dev;
                            Devices.Add(dev);
                        }
                        else
                        {
                            dev.Name = name ?? dev.Name;
                            dev.DeviceType = devType != "UNKNOWN" ? devType : dev.DeviceType;
                            dev.ConnectionType = connType ?? dev.ConnectionType;
                            dev.IsActive = (state == "ACTIVE");
                        }
                    }
                });

                // Immediately fetch battery for this device
                _ = SendJsonAsync($"{{\"path\":\"/battery/{id}/state\",\"verb\":\"GET\"}}", CancellationToken.None);
            }
        }

        private void HandleBatteryState(JsonElement root, string path)
        {
            if (!root.TryGetProperty("payload", out var payload))
                return;

            string? deviceId = null;
            if (payload.TryGetProperty("deviceId", out var dIdProp))
            {
                deviceId = dIdProp.GetString();
            }
            else
            {
                // Parse from path: /battery/{id}/state
                var parts = path.Split('/');
                if (parts.Length >= 3)
                {
                    deviceId = parts[2];
                }
            }

            if (string.IsNullOrEmpty(deviceId))
                return;

            int percentage = payload.TryGetProperty("percentage", out var pProp) ? pProp.GetInt32() : 0;
            bool charging = payload.TryGetProperty("charging", out var chProp) && chProp.GetBoolean();
            double mileage = payload.TryGetProperty("mileage", out var mProp) ? mProp.GetDouble() : 0;
            double maxLifespan = payload.TryGetProperty("maxLifeSpan", out var mlProp) ? mlProp.GetDouble() : 0;

            double consumption = 0;
            if (payload.TryGetProperty("consumption", out var cProp) && cProp.TryGetProperty("value", out var vProp))
            {
                consumption = vProp.GetDouble();
            }

            WpfApp.Current?.Dispatcher.Invoke(() =>
            {
                lock (_deviceMap)
                {
                    if (_deviceMap.TryGetValue(deviceId, out var dev))
                    {
                        dev.BatteryPercentage = percentage;
                        dev.IsCharging = charging;
                        dev.Mileage = mileage;
                        dev.MaxLifespan = maxLifespan;
                        dev.ConsumptionMw = consumption;

                        if (percentage <= 20 && !charging)
                        {
                            LowBatteryAlertTriggered?.Invoke(dev);
                        }
                    }
                }
            });
        }

        private async Task SendJsonAsync(string json, CancellationToken ct)
        {
            if (_webSocket?.State != WebSocketState.Open)
                return;

            var bytes = Encoding.UTF8.GetBytes(json);
            await _sendLock.WaitAsync(ct);
            try
            {
                if (_webSocket?.State == WebSocketState.Open)
                {
                    await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
                }
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
            WpfApp.Current?.Dispatcher.Invoke(() =>
            {
                ConnectionStatusChanged?.Invoke(connected, message);
            });
        }

        private void CleanUpSocket()
        {
            try
            {
                if (_webSocket != null)
                {
                    if (_webSocket.State == WebSocketState.Open)
                    {
                        _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).Wait(1000);
                    }
                    _webSocket.Dispose();
                }
            }
            catch { }
            _webSocket = null;
        }

        public void Dispose()
        {
            _disposed = true;
            _cts?.Cancel();
            CleanUpSocket();
            _sendLock.Dispose();
        }
    }
}

