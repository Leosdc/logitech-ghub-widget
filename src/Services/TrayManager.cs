using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using GHubWidget.Helpers;
using GHubWidget.Models;
using GHubWidget.Views;
using Application = System.Windows.Application;

namespace GHubWidget.Services
{
    public class TrayManager : IDisposable
    {
        private readonly LgHubService _service;
        private readonly AppSettings _settings;
        private readonly TaskbarFlyoutWindow _flyoutWindow;
        private readonly Action _onOpenMainWindow;
        private readonly Action _onOpenSettings;
        private readonly Action _onExit;

        private readonly Dictionary<string, NotifyIcon> _deviceIcons = new();
        private NotifyIcon? _fallbackIcon;
        private bool _disposed = false;

        public TrayManager(
            LgHubService service, 
            AppSettings settings, 
            TaskbarFlyoutWindow flyoutWindow,
            Action onOpenMainWindow,
            Action onOpenSettings,
            Action onExit)
        {
            _service = service;
            _settings = settings;
            _flyoutWindow = flyoutWindow;
            _onOpenMainWindow = onOpenMainWindow;
            _onOpenSettings = onOpenSettings;
            _onExit = onExit;

            SetupFallbackIcon();
            HookServiceEvents();
            UpdateTrayIcons();
        }

        private void SetupFallbackIcon()
        {
            try
            {
                int iconSize = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON);
                if (iconSize <= 0) iconSize = 16;
                var icon = DeviceIconGenerator.CreateOfflineIcon(AppSettings.IsTaskbarDarkTheme(), iconSize);
                _fallbackIcon = new NotifyIcon
                {
                    Icon = icon,
                    Visible = true,
                    Text = "Logitech G HUB Widget"
                };

                _fallbackIcon.MouseMove += (s, e) =>
                {
                    if (_settings.HoverFlyoutEnabled)
                    {
                        _flyoutWindow.ShowAt(Cursor.Position);
                    }
                };

                _fallbackIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        if (_flyoutWindow.IsVisible && !_flyoutWindow.IsPinned)
                        {
                            _flyoutWindow.HideFlyout();
                        }
                        else
                        {
                            _flyoutWindow.ShowAt(Cursor.Position);
                        }
                    }
                };

                _fallbackIcon.DoubleClick += (s, e) =>
                {
                    _onOpenMainWindow();
                };

                _fallbackIcon.ContextMenuStrip = CreateContextMenu(null);
            }
            catch { }
        }

        private void HookServiceEvents()
        {
            _service.ConnectionStatusChanged += (connected, msg) =>
            {
                Application.Current?.Dispatcher.Invoke(UpdateTrayIcons);
            };

            _service.Devices.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (DeviceInfo dev in e.NewItems)
                    {
                        dev.PropertyChanged += (devSender, propArgs) =>
                        {
                            if (propArgs.PropertyName == nameof(DeviceInfo.IsActive) ||
                                propArgs.PropertyName == nameof(DeviceInfo.BatteryPercentage) ||
                                propArgs.PropertyName == nameof(DeviceInfo.IsCharging) ||
                                propArgs.PropertyName == nameof(DeviceInfo.Name))
                            {
                                Application.Current?.Dispatcher.Invoke(UpdateTrayIcons);
                            }
                        };
                    }
                }

                Application.Current?.Dispatcher.Invoke(UpdateTrayIcons);
            };

            // Hook devices already present
            foreach (var dev in _service.Devices)
            {
                dev.PropertyChanged += (devSender, propArgs) =>
                {
                    if (propArgs.PropertyName == nameof(DeviceInfo.IsActive) ||
                        propArgs.PropertyName == nameof(DeviceInfo.BatteryPercentage) ||
                        propArgs.PropertyName == nameof(DeviceInfo.IsCharging) ||
                        propArgs.PropertyName == nameof(DeviceInfo.Name))
                    {
                        Application.Current?.Dispatcher.Invoke(UpdateTrayIcons);
                    }
                };
            }
        }

        public void UpdateTrayIcons()
        {
            if (_disposed) return;

            try
            {
                bool isDarkTaskbar = AppSettings.IsTaskbarDarkTheme();
                int iconSize = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON);
                if (iconSize <= 0) iconSize = 16;

                var displayDevices = new List<DeviceInfo>();
                foreach (var d in _service.Devices)
                {
                    if (d.IsActive || !_settings.HideInactiveFromTaskbar)
                    {
                        displayDevices.Add(d);
                    }
                }

                // Keep icons in a consistent, intuitive order: Mouse -> Keyboard -> Headset -> Others
                displayDevices.Sort((a, b) =>
                {
                    static int Order(DeviceInfo d)
                    {
                        var s = ((d.DeviceType ?? "") + " " + (d.Name ?? "")).ToUpperInvariant();
                        if (s.Contains("MOUSE") || s.Contains("SUPERLIGHT") || s.Contains("G305") || s.Contains("G502")) return 1;
                        if (s.Contains("KEYBOARD") || s.Contains("TECLADO") || s.Contains("TKL") || s.Contains("G515") || s.Contains("G915")) return 2;
                        if (s.Contains("HEADSET") || s.Contains("HEADPHONE") || s.Contains("FONE") || s.Contains("AUDIO") || s.Contains("G325") || s.Contains("G733") || s.Contains("G535") || s.Contains("PRO X")) return 3;
                        return 4;
                    }
                    int cmp = Order(a).CompareTo(Order(b));
                    return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });

                // Tray device icons should only be shown if:
                // 1) User explicitly configured DisplayMode == "TaskbarTray", OR
                // 2) User checked "ShowTrayIconsWithTaskbarStrip" in settings.
                // In standard TaskbarWidget mode (strip on taskbar), tray icons are hidden to keep the ^ menu clean!
                bool shouldShowTrayDeviceIcons = (_settings.DisplayMode == "TaskbarTray" || _settings.ShowTrayIconsWithTaskbarStrip) && _settings.IndividualDeviceIcons;

                if (shouldShowTrayDeviceIcons && displayDevices.Count > 0)
                {
                    var displayIds = new HashSet<string>();

                    foreach (var dev in displayDevices)
                    {
                        displayIds.Add(dev.Id);
                        var newIcon = DeviceIconGenerator.CreateDeviceIcon(
                            dev.DeviceType, 
                            dev.Name,
                            dev.BatteryPercentage, 
                            dev.IsCharging, 
                            dev.IsActive,
                            isDarkTaskbar, 
                            iconSize);

                        string tooltip = dev.IsActive
                            ? $"{dev.Name}: {dev.BatteryPercentage}%{(dev.IsCharging ? " ⚡ (Carregando)" : "")}"
                            : $"{dev.Name}: [Inativo / Repouso]";

                        if (_deviceIcons.TryGetValue(dev.Id, out var existingTray))
                        {
                            var oldIcon = existingTray.Icon;
                            existingTray.Icon = newIcon;
                            SafeDisposeIcon(oldIcon);
                            existingTray.Text = TruncateTooltip(tooltip);
                        }
                        else
                        {
                            var tray = new NotifyIcon
                            {
                                Icon = newIcon,
                                Visible = true,
                                Text = TruncateTooltip(tooltip)
                            };

                            var capturedDev = dev;
                            tray.MouseMove += (s, e) =>
                            {
                                if (_settings.HoverFlyoutEnabled)
                                {
                                    _flyoutWindow.ShowAt(Cursor.Position, capturedDev);
                                }
                            };

                            tray.MouseClick += (s, e) =>
                            {
                                if (e.Button == MouseButtons.Left)
                                {
                                    if (_flyoutWindow.IsVisible && !_flyoutWindow.IsPinned)
                                    {
                                        _flyoutWindow.HideFlyout();
                                    }
                                    else
                                    {
                                        _flyoutWindow.ShowAt(Cursor.Position, capturedDev);
                                    }
                                }
                            };

                            tray.DoubleClick += (s, e) =>
                            {
                                _onOpenMainWindow();
                            };

                            tray.ContextMenuStrip = CreateContextMenu(capturedDev);
                            _deviceIcons[dev.Id] = tray;
                        }
                    }

                    // Remove devices that are no longer displayed
                    var toRemove = new List<string>();
                    foreach (var kvp in _deviceIcons)
                    {
                        if (!displayIds.Contains(kvp.Key))
                        {
                            toRemove.Add(kvp.Key);
                        }
                    }

                    foreach (var id in toRemove)
                    {
                        if (_deviceIcons.TryGetValue(id, out var deadTray))
                        {
                            deadTray.Visible = false;
                            SafeDisposeIcon(deadTray.Icon);
                            deadTray.Dispose();
                            _deviceIcons.Remove(id);
                        }
                    }

                    if (_fallbackIcon != null)
                    {
                        _fallbackIcon.Visible = false;
                    }
                }
                else
                {
                    foreach (var kvp in _deviceIcons)
                    {
                        kvp.Value.Visible = false;
                        SafeDisposeIcon(kvp.Value.Icon);
                        kvp.Value.Dispose();
                    }
                    _deviceIcons.Clear();

                    if (_fallbackIcon != null)
                    {
                        // The main Logitech G Hub Widget tray icon is ALWAYS visible in the system tray
                        _fallbackIcon.Visible = true;

                        var activeDevices = new List<DeviceInfo>();
                        foreach (var d in _service.Devices)
                        {
                            if (d.IsActive) activeDevices.Add(d);
                        }

                        if (activeDevices.Count > 0)
                        {
                            int minBattery = 100;
                            bool hasCharging = false;
                            foreach (var d in activeDevices)
                            {
                                if (d.BatteryPercentage < minBattery) minBattery = d.BatteryPercentage;
                                if (d.IsCharging) hasCharging = true;
                            }

                            var oldIcon = _fallbackIcon.Icon;
                            _fallbackIcon.Icon = DeviceIconGenerator.CreateCombinedIcon(activeDevices.Count, minBattery, hasCharging, isDarkTaskbar, iconSize);
                            SafeDisposeIcon(oldIcon);

                            _fallbackIcon.Text = TruncateTooltip($"Logitech G: {activeDevices.Count} ativo(s) • Min: {minBattery}%");
                        }
                        else
                        {
                            var oldIcon = _fallbackIcon.Icon;
                            _fallbackIcon.Icon = DeviceIconGenerator.CreateOfflineIcon(isDarkTaskbar, iconSize);
                            SafeDisposeIcon(oldIcon);

                            _fallbackIcon.Text = _service.IsConnected 
                                ? "Logitech G HUB: Nenhum periférico ativo" 
                                : "Logitech G HUB: Aguardando conexão...";
                        }
                    }
            }
            }
            catch { }
        }

        private ContextMenuStrip CreateContextMenu(DeviceInfo? dev)
        {
            var menu = new ContextMenuStrip();

            var itemPopup = menu.Items.Add("⚡ Pop-up da Bateria", null, (s, e) =>
            {
                _flyoutWindow.ShowAt(Cursor.Position, dev);
            });
            itemPopup.Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold);

            menu.Items.Add("🖥️ Janela Flutuante", null, (s, e) =>
            {
                _onOpenMainWindow();
            });

            menu.Items.Add("🔄 Atualizar Dados", null, async (s, e) =>
            {
                await _service.RefreshNowAsync();
            });

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add("⚙️ Configurações", null, (s, e) =>
            {
                _onOpenSettings();
            });

            menu.Items.Add("🎮 Abrir Logitech G HUB", null, (s, e) =>
            {
                LaunchGHub();
            });

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add("❌ Sair", null, (s, e) =>
            {
                _onExit();
            });

            return menu;
        }

        private static void LaunchGHub()
        {
            try
            {
                var officialPath = @"C:\Program Files\LGHUB\lghub.exe";
                if (File.Exists(officialPath))
                {
                    Process.Start(new ProcessStartInfo(officialPath) { UseShellExecute = true });
                }
                else
                {
                    System.Windows.MessageBox.Show("Logitech G HUB não encontrado em C:\\Program Files\\LGHUB\\lghub.exe.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao abrir o G HUB: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string TruncateTooltip(string text)
        {
            if (string.IsNullOrEmpty(text)) return "Logitech G HUB";
            return text.Length > 63 ? text.Substring(0, 60) + "..." : text;
        }

        public void ShowLowBatteryNotification(DeviceInfo dev)
        {
            NotifyIcon? target = null;
            if (_deviceIcons.TryGetValue(dev.Id, out var icon) && icon.Visible)
            {
                target = icon;
            }
            else if (_fallbackIcon != null && _fallbackIcon.Visible)
            {
                target = _fallbackIcon;
            }

            target?.ShowBalloonTip(
                5000,
                "Bateria Fraca - Logitech G",
                $"{dev.Name} está com {dev.BatteryPercentage}% de bateria. Conecte o carregador!",
                ToolTipIcon.Warning);
        }

        private static void SafeDisposeIcon(Icon? icon)
        {
            if (icon == null) return;
            try
            {
                var handle = icon.Handle;
                icon.Dispose();
                if (handle != IntPtr.Zero)
                {
                    NativeMethods.DestroyIcon(handle);
                }
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var kvp in _deviceIcons)
            {
                kvp.Value.Visible = false;
                SafeDisposeIcon(kvp.Value.Icon);
                kvp.Value.Dispose();
            }
            _deviceIcons.Clear();

            if (_fallbackIcon != null)
            {
                _fallbackIcon.Visible = false;
                SafeDisposeIcon(_fallbackIcon.Icon);
                _fallbackIcon.Dispose();
                _fallbackIcon = null;
            }
        }
    }
}
