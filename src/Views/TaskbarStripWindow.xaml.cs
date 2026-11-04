using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GHubWidget.Helpers;
using GHubWidget.Models;
using GHubWidget.Services;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Orientation = System.Windows.Controls.Orientation;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Point = System.Windows.Point;

namespace GHubWidget.Views
{
    public partial class TaskbarStripWindow : Window
    {
        private readonly LgHubService _service;
        private readonly AppSettings _settings;
        private readonly TaskbarFlyoutWindow _flyoutWindow;
        private readonly Action _onOpenMainWindow;
        private readonly Action _onOpenSettings;
        private readonly Action _onExit;

        private readonly DispatcherTimer _positionTimer;
        private NativeMethods.WinEventDelegate? _winEventDelegate;
        private IntPtr _winEventHook = IntPtr.Zero;
        private bool _isDisposed = false;
        private bool _isDarkTheme = true;

        public TaskbarStripWindow(
            LgHubService service,
            AppSettings settings,
            TaskbarFlyoutWindow flyoutWindow,
            Action onOpenMainWindow,
            Action onOpenSettings,
            Action onExit)
        {
            InitializeComponent();

            _service = service;
            _settings = settings;
            _flyoutWindow = flyoutWindow;
            _onOpenMainWindow = onOpenMainWindow;
            _onOpenSettings = onOpenSettings;
            _onExit = onExit;

            _positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _positionTimer.Tick += (s, e) => CheckPositionAndState();

            HookEvents();
            AppLogger.Log("TaskbarStripWindow initialized.");
            Loaded += (s, e) =>
            {
                AppLogger.Log("TaskbarStripWindow Loaded event firing.");
                ApplyWindowStyles();
                UpdateTheme();
                RebuildPills();
                UpdatePosition(true);
                BringToTop();
                _positionTimer.Start();
                AppLogger.Log($"TaskbarStripWindow Loaded complete: Visibility={Visibility}, Left={Left}, Top={Top}, ActualWidth={ActualWidth}");
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyWindowStyles();
        }

        private void ApplyWindowStyles()
        {
            var helper = new WindowInteropHelper(this);
            var hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;

            // Apply WS_EX_TOOLWINDOW (no Alt+Tab) | WS_EX_NOACTIVATE (no stealing focus)
            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            exStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
        }

        private void HookEvents()
        {
            _service.Devices.CollectionChanged += OnDevicesCollectionChanged;
            foreach (var dev in _service.Devices)
            {
                dev.PropertyChanged += OnDevicePropertyChanged;
            }

            _service.ConnectionStatusChanged += (connected, msg) =>
            {
                Dispatcher.Invoke(RebuildPills);
            };

            // Whenever this window loses focus or the user clicks the taskbar, re-assert topmost Z-order
            Deactivated += (s, e) =>
            {
                Dispatcher.BeginInvoke(new Action(BringToTop));
            };

            // Win32 hook: whenever ANY foreground window changes (e.g. user clicks the taskbar), keep this strip on top
            try
            {
                _winEventDelegate = (hHook, eventType, hwnd, idObject, idChild, dwEventThread, dwmsEventTime) =>
                {
                    Dispatcher.BeginInvoke(new Action(BringToTop));
                };
                _winEventHook = NativeMethods.SetWinEventHook(
                    NativeMethods.EVENT_SYSTEM_FOREGROUND,
                    NativeMethods.EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero,
                    _winEventDelegate,
                    0,
                    0,
                    NativeMethods.WINEVENT_OUTOFCONTEXT);
            }
            catch { }
        }

        private void OnDevicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (DeviceInfo dev in e.NewItems)
                {
                    dev.PropertyChanged += OnDevicePropertyChanged;
                }
            }

            Dispatcher.Invoke(() =>
            {
                RebuildPills();
                UpdatePosition();
            });
        }

        private void OnDevicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DeviceInfo.IsActive) ||
                e.PropertyName == nameof(DeviceInfo.BatteryPercentage) ||
                e.PropertyName == nameof(DeviceInfo.IsCharging) ||
                e.PropertyName == nameof(DeviceInfo.Name))
            {
                Dispatcher.Invoke(() =>
                {
                    RebuildPills();
                    UpdatePosition();
                });
            }
        }

        private readonly Dictionary<string, (Border Pill, TextBlock Icon, TextBlock Battery)> _pillMap = new();

        public void UpdateTheme()
        {
            _isDarkTheme = _settings.ThemeMode switch
            {
                "Dark" => true,
                "Light" => false,
                _ => AppSettings.IsSystemDarkTheme()
            };
            RebuildPills();
        }

        public void RebuildPills()
        {
            if (_isDisposed) return;

            var displayDevices = new List<DeviceInfo>();
            foreach (var d in _service.Devices)
            {
                if (d.IsActive || !_settings.HideInactiveFromTaskbar)
                {
                    displayDevices.Add(d);
                }
            }

            // Sort: Mouse -> Keyboard -> Headset -> Others
            displayDevices.Sort((a, b) =>
            {
                static int Order(DeviceInfo dev)
                {
                    var s = ((dev.DeviceType ?? "") + " " + (dev.Name ?? "")).ToUpperInvariant();
                    if (s.Contains("MOUSE") || s.Contains("SUPERLIGHT") || s.Contains("G305") || s.Contains("G502")) return 1;
                    if (s.Contains("KEYBOARD") || s.Contains("TECLADO") || s.Contains("TKL") || s.Contains("G515") || s.Contains("G915")) return 2;
                    if (s.Contains("HEADSET") || s.Contains("HEADPHONE") || s.Contains("FONE") || s.Contains("AUDIO") || s.Contains("G325") || s.Contains("G733") || s.Contains("G535") || s.Contains("PRO X")) return 3;
                    return 4;
                }
                int cmp = Order(a).CompareTo(Order(b));
                return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            // Fast path: In-place update if device list is unchanged (prevents visual tree rebuild and flicker)
            bool listMatches = _pillMap.Count == displayDevices.Count;
            if (listMatches)
            {
                int idx = 0;
                foreach (var kvp in _pillMap)
                {
                    if (idx >= displayDevices.Count || kvp.Key != displayDevices[idx].Id)
                    {
                        listMatches = false;
                        break;
                    }
                    idx++;
                }
            }

            if (listMatches && displayDevices.Count > 0)
            {
                foreach (var dev in displayDevices)
                {
                    if (_pillMap.TryGetValue(dev.Id, out var tuple))
                    {
                        UpdatePillContent(tuple.Pill, tuple.Icon, tuple.Battery, dev);
                    }
                }
                return;
            }

            // Full rebuild only when devices are added or removed
            StripContainer.Children.Clear();
            _pillMap.Clear();

            if (displayDevices.Count == 0)
            {
                var fallbackPill = CreateFallbackPill();
                StripContainer.Children.Add(fallbackPill);
                return;
            }

            foreach (var dev in displayDevices)
            {
                var (pill, txtIcon, txtBattery) = CreateDevicePill(dev);
                _pillMap[dev.Id] = (pill, txtIcon, txtBattery);
                StripContainer.Children.Add(pill);
            }

            UpdatePosition(true);
        }

        private (Border Pill, TextBlock Icon, TextBlock Battery) CreateDevicePill(DeviceInfo dev)
        {
            var pill = new Border
            {
                Style = (Style)Resources["TaskbarPill"],
                Tag = dev
            };

            if (!_isDarkTheme)
            {
                pill.MouseEnter += (s, e) =>
                {
                    pill.Background = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0));
                    pill.BorderBrush = new SolidColorBrush(Color.FromArgb(35, 0, 0, 0));
                };
                pill.MouseLeave += (s, e) =>
                {
                    pill.Background = Brushes.Transparent;
                    pill.BorderBrush = Brushes.Transparent;
                };
            }

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var txtIcon = new TextBlock
            {
                Text = GetDeviceGlyph(dev),
                FontSize = 13,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var txtBattery = new TextBlock
            {
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            UpdatePillContent(pill, txtIcon, txtBattery, dev);

            stack.Children.Add(txtIcon);
            stack.Children.Add(txtBattery);
            pill.Child = stack;

            var capturedDev = dev;
            pill.MouseEnter += (s, e) =>
            {
                if (_settings.HoverFlyoutEnabled)
                {
                    var pt = pill.PointToScreen(new Point(pill.ActualWidth / 2, 0));
                    _flyoutWindow.ShowAt(new System.Drawing.Point((int)pt.X, (int)pt.Y), capturedDev);
                }
            };

            pill.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                AppLogger.Log($"Pill LeftClick: Device={capturedDev?.Name}, FlyoutVisible={_flyoutWindow.IsVisible}");
                if (_flyoutWindow.IsVisible && !_flyoutWindow.IsPinned)
                {
                    _flyoutWindow.HideFlyout();
                }
                else
                {
                    var pt = pill.PointToScreen(new Point(pill.ActualWidth / 2, 0));
                    _flyoutWindow.ShowAt(new System.Drawing.Point((int)pt.X, (int)pt.Y), capturedDev);
                }
            };

            pill.MouseDown += (s, e) =>
            {
                if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
                {
                    e.Handled = true;
                    _onOpenMainWindow();
                }
            };

            pill.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                ShowContextMenu(capturedDev);
            };

            return (pill, txtIcon, txtBattery);
        }

        private void UpdatePillContent(Border pill, TextBlock txtIcon, TextBlock txtBattery, DeviceInfo dev)
        {
            txtIcon.Text = GetDeviceGlyph(dev);

            string batteryText;
            Brush textBrush;

            if (!dev.IsActive)
            {
                batteryText = "OFF";
                textBrush = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(140, 140, 140) : Color.FromRgb(120, 120, 120));
            }
            else if (dev.IsCharging)
            {
                batteryText = $"⚡ {dev.BatteryPercentage}%";
                textBrush = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(0, 215, 255) : Color.FromRgb(0, 120, 215));
            }
            else
            {
                batteryText = $"{dev.BatteryPercentage}%";
                if (dev.BatteryPercentage > 50)
                {
                    textBrush = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(108, 203, 95) : Color.FromRgb(16, 140, 16));
                }
                else if (dev.BatteryPercentage > 20)
                {
                    textBrush = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(252, 225, 0) : Color.FromRgb(200, 120, 0));
                }
                else
                {
                    textBrush = new SolidColorBrush(_isDarkTheme ? Color.FromRgb(255, 75, 75) : Color.FromRgb(220, 40, 0));
                }
            }

            txtBattery.Text = batteryText;
            txtBattery.Foreground = textBrush;

            string tooltipText = dev.IsActive
                ? $"{dev.Name}: {dev.BatteryPercentage}%{(dev.IsCharging ? " ⚡ (Carregando)" : "")}\nClique para abrir detalhes"
                : $"{dev.Name}: [Inativo / Repouso]\nClique para abrir detalhes";
            pill.ToolTip = tooltipText;
        }

        private Border CreateFallbackPill()
        {
            var pill = new Border
            {
                Style = (Style)Resources["TaskbarPill"]
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var txt = new TextBlock
            {
                Text = "⚡ Logitech G",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(_isDarkTheme ? System.Windows.Media.Color.FromRgb(160, 160, 160) : System.Windows.Media.Color.FromRgb(100, 100, 100)),
                VerticalAlignment = VerticalAlignment.Center
            };

            stack.Children.Add(txt);
            pill.Child = stack;
            pill.ToolTip = "Logitech G HUB Widget: Aguardando dispositivos...";

            pill.MouseEnter += (s, e) =>
            {
                if (_settings.HoverFlyoutEnabled)
                {
                    var pt = pill.PointToScreen(new Point(pill.ActualWidth / 2, 0));
                    _flyoutWindow.ShowAt(new System.Drawing.Point((int)pt.X, (int)pt.Y));
                }
            };

            pill.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (_flyoutWindow.IsVisible && !_flyoutWindow.IsPinned)
                {
                    _flyoutWindow.HideFlyout();
                }
                else
                {
                    var pt = pill.PointToScreen(new Point(pill.ActualWidth / 2, 0));
                    _flyoutWindow.ShowAt(new System.Drawing.Point((int)pt.X, (int)pt.Y));
                }
            };

            pill.MouseDown += (s, e) =>
            {
                if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
                {
                    e.Handled = true;
                    _onOpenMainWindow();
                }
            };

            pill.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                ShowContextMenu(null);
            };

            return pill;
        }

        private string GetDeviceGlyph(DeviceInfo dev)
        {
            var s = ((dev.DeviceType ?? "") + " " + (dev.Name ?? "")).ToUpperInvariant();
            if (s.Contains("MOUSE") || s.Contains("SUPERLIGHT")) return "🖱️";
            if (s.Contains("KEYBOARD") || s.Contains("TECLADO") || s.Contains("TKL")) return "⌨️";
            if (s.Contains("HEADSET") || s.Contains("HEADPHONE") || s.Contains("FONE") || s.Contains("AUDIO")) return "🎧";
            return "🔋";
        }

        private void ShowContextMenu(DeviceInfo? dev)
        {
            var menu = new ContextMenu();

            if (dev != null)
            {
                var headerItem = new MenuItem
                {
                    Header = $"{dev.Name} - {dev.BatteryPercentage}%",
                    IsEnabled = false,
                    FontWeight = FontWeights.Bold
                };
                menu.Items.Add(headerItem);
                menu.Items.Add(new Separator());
            }

            var itemDashboard = new MenuItem { Header = "📊 Abrir Widget Principal" };
            itemDashboard.Click += (s, e) => _onOpenMainWindow();
            menu.Items.Add(itemDashboard);

            var itemSettings = new MenuItem { Header = "⚙️ Configurações" };
            itemSettings.Click += (s, e) => _onOpenSettings();
            menu.Items.Add(itemSettings);

            var itemGHub = new MenuItem { Header = "🚀 Abrir Logitech G HUB" };
            itemGHub.Click += (s, e) =>
            {
                try
                {
                    var path = @"C:\Program Files\LGHUB\lghub.exe";
                    if (System.IO.File.Exists(path))
                    {
                        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    }
                }
                catch { }
            };
            menu.Items.Add(itemGHub);

            menu.Items.Add(new Separator());

            var itemRefresh = new MenuItem { Header = "🔄 Atualizar Agora" };
            itemRefresh.Click += async (s, e) => await _service.RefreshNowAsync();
            menu.Items.Add(itemRefresh);

            menu.Items.Add(new Separator());

            var itemExit = new MenuItem { Header = "❌ Fechar Widget" };
            itemExit.Click += (s, e) => _onExit();
            menu.Items.Add(itemExit);

            menu.IsOpen = true;
        }

        private double _lastLeft = double.NaN;
        private double _lastTop = double.NaN;
        private double _lastWidth = double.NaN;

        public void BringToTop()
        {
            if (_isDisposed) return;
            try
            {
                var helper = new WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowPos(helper.Handle, (IntPtr)NativeMethods.HWND_TOPMOST,
                        0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                }
            }
            catch { }
        }

        private void CheckPositionAndState()
        {
            if (_isDisposed) return;

            bool shouldBeVisible = (_settings.DisplayMode != "FloatingWindow");

            if (shouldBeVisible)
            {
                if (Visibility != Visibility.Visible)
                {
                    Visibility = Visibility.Visible;
                }
                UpdatePosition(false);
                BringToTop();
            }
            else
            {
                if (Visibility != Visibility.Hidden)
                {
                    Visibility = Visibility.Hidden;
                }
            }
        }

        public void UpdatePosition(bool force = false)
        {
            if (_isDisposed) return;

            try
            {
                var screen = System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0];
                var bounds = screen.Bounds;
                var workArea = screen.WorkingArea;

                // Taskbar height on standard Windows bottom layout
                double taskbarTop = workArea.Bottom;
                double taskbarHeight = bounds.Bottom - workArea.Bottom;
                if (taskbarHeight < 30) taskbarHeight = 48;

                // Strip height is 34px, center vertically within taskbar
                double stripHeight = 34;
                double targetTop = taskbarTop + (taskbarHeight - stripHeight) / 2.0;

                // Determine notification area start
                IntPtr taskbarHwnd = NativeMethods.FindWindow("Shell_TrayWnd", null);
                double targetLeft;

                if (taskbarHwnd != IntPtr.Zero)
                {
                    IntPtr trayHwnd = NativeMethods.FindWindowEx(taskbarHwnd, IntPtr.Zero, "TrayNotifyWnd", null);
                    if (trayHwnd != IntPtr.Zero && NativeMethods.GetWindowRect(trayHwnd, out var trayRect))
                    {
                        // Position strip immediately to the left of TrayNotifyWnd with 8px margin
                        targetLeft = trayRect.Left - ActualWidth - 8;
                    }
                    else
                    {
                        // Fallback: estimate notification area width ~260px
                        targetLeft = bounds.Right - 260 - ActualWidth - 8;
                    }
                }
                else
                {
                    targetLeft = bounds.Right - 260 - ActualWidth - 8;
                }

                if (targetLeft < 0) targetLeft = 0;

                // Anti-flicker: Only update window coordinates if position or width actually changed!
                if (!force &&
                    !double.IsNaN(_lastLeft) &&
                    Math.Abs(targetLeft - _lastLeft) < 1.0 &&
                    Math.Abs(targetTop - _lastTop) < 1.0 &&
                    Math.Abs(ActualWidth - _lastWidth) < 1.0)
                {
                    BringToTop();
                    return;
                }

                _lastLeft = targetLeft;
                _lastTop = targetTop;
                _lastWidth = ActualWidth;

                Left = targetLeft;
                Top = targetTop;
                AppLogger.Log($"TaskbarStripWindow UpdatePosition applied: Left={targetLeft}, Top={targetTop}, ActualWidth={ActualWidth}");

                BringToTop();
            }
            catch { }
        }

        public void Dispose()
        {
            _isDisposed = true;
            _positionTimer.Stop();

            if (_winEventHook != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.UnhookWinEvent(_winEventHook);
                }
                catch { }
                _winEventHook = IntPtr.Zero;
            }

            Close();
        }
    }
}
