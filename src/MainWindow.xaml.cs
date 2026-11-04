using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Forms;
using Microsoft.Win32;
using GHubWidget.Helpers;
using GHubWidget.Models;
using GHubWidget.Services;
using GHubWidget.Views;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Color = System.Windows.Media.Color;

namespace GHubWidget
{
    public partial class MainWindow : Window
    {
        private const int WM_SETTINGCHANGE = 0x001A;

        private readonly LgHubService _service = new();
        private AppSettings _settings = new();
        private TrayManager? _trayManager;
        private TaskbarFlyoutWindow? _flyoutWindow;
        private TaskbarStripWindow? _stripWindow;
        private bool _isExplicitExit = false;
        private bool _isInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            new WindowInteropHelper(this).EnsureHandle();
            InitializeWidget();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = (HwndSource?)PresentationSource.FromVisual(this);
            source?.AddHook(WndProc);

            if (_settings.DisplayMode != "FloatingWindow")
            {
                EnterTaskbarMode();
            }
        }

        public void EnterTaskbarMode()
        {
            AppLogger.Log("MainWindow EnterTaskbarMode called.");
            ShowInTaskbar = false;
            Visibility = Visibility.Hidden;
            _trayManager?.UpdateTrayIcons();
            if (_stripWindow != null)
            {
                _stripWindow.Visibility = Visibility.Visible;
                _stripWindow.RebuildPills();
                _stripWindow.UpdatePosition();
            }
        }

        public void EnterWindowMode()
        {
            ShowInTaskbar = true;
            Visibility = Visibility.Visible;
            Show();

            var cursorScreen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position) 
                               ?? System.Windows.Forms.Screen.PrimaryScreen 
                               ?? System.Windows.Forms.Screen.AllScreens[0];

            double targetLeft = cursorScreen.WorkingArea.Right - Width - 30;
            double targetTop = cursorScreen.WorkingArea.Top + 60;

            if (!double.IsNaN(_settings.WindowLeft) && !double.IsNaN(_settings.WindowTop))
            {
                bool onValidScreen = false;
                foreach (var s in System.Windows.Forms.Screen.AllScreens)
                {
                    if (s.Bounds.Contains((int)_settings.WindowLeft, (int)_settings.WindowTop))
                    {
                        onValidScreen = true;
                        break;
                    }
                }

                if (onValidScreen)
                {
                    targetLeft = _settings.WindowLeft;
                    targetTop = _settings.WindowTop;
                }
            }

            Left = targetLeft;
            Top = targetTop;

            WindowState = WindowState.Normal;
            Activate();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SETTINGCHANGE)
            {
                if (_settings.ThemeMode == "System")
                {
                    Dispatcher.Invoke(() => ApplyTheme("System"));
                }
            }
            return IntPtr.Zero;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                InitializeWidget();
            }
        }

        private void InitializeWidget()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            _settings = AppSettings.Load();

            // Hook system theme change notification
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            // Apply Theme
            ApplyTheme(_settings.ThemeMode);

            // Display Mode Pills
            if (_settings.DisplayMode == "FloatingWindow")
            {
                RadioModeWindow.IsChecked = true;
            }
            else
            {
                RadioModeTaskbar.IsChecked = true;
            }

            // Taskbar Options
            ChkHoverFlyout.IsChecked = _settings.HoverFlyoutEnabled;
            ChkIndividualIcons.IsChecked = _settings.IndividualDeviceIcons;
            ChkHideInactive.IsChecked = _settings.HideInactiveFromTaskbar;
            ChkShowTrayIcons.IsChecked = _settings.ShowTrayIconsWithTaskbarStrip;

            // Theme Pills
            if (_settings.ThemeMode == "Light")
                RadioThemeLight.IsChecked = true;
            else if (_settings.ThemeMode == "Dark")
                RadioThemeDark.IsChecked = true;
            else
                RadioThemeSystem.IsChecked = true;

            // Interval Pills
            switch (_settings.RefreshIntervalSeconds)
            {
                case 2: RadioInterval2.IsChecked = true; break;
                case 15: RadioInterval15.IsChecked = true; break;
                case 30: RadioInterval30.IsChecked = true; break;
                default: RadioInterval5.IsChecked = true; break;
            }

            // Position & Sizing
            if (!double.IsNaN(_settings.WindowLeft) && !double.IsNaN(_settings.WindowTop))
            {
                Left = Math.Max(10, Math.Min(_settings.WindowLeft, SystemParameters.VirtualScreenWidth - Width - 10));
                Top = Math.Max(10, Math.Min(_settings.WindowTop, SystemParameters.VirtualScreenHeight - Height - 10));
            }
            else
            {
                // Align to top-right corner by default like a true Windows widget
                Left = SystemParameters.WorkArea.Right - Width - 30;
                Top = 60;
            }

            // Apply Settings to UI
            SliderOpacity.Value = _settings.Opacity;
            MainFrame.Opacity = _settings.Opacity;
            TxtOpacityValue.Text = $"{(int)(_settings.Opacity * 100)}%";

            ChkAlwaysOnTop.IsChecked = _settings.AlwaysOnTop;
            ChkPinToDesktop.IsChecked = _settings.PinToDesktop;
            ChkAutoStart.IsChecked = _settings.AutoStart;
            ChkLowBatteryAlert.IsChecked = _settings.LowBatteryAlert;

            ApplyWindowLayering();

            // Setup Flyout and Tray Manager
            SetupTrayManager();

            // Setup Service
            DevicesList.ItemsSource = _service.Devices;
            _service.RefreshIntervalSeconds = _settings.RefreshIntervalSeconds;

            _service.ConnectionStatusChanged += (connected, msg) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (connected)
                    {
                        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(108, 203, 95));
                        StatusText.Text = "Conectado";
                        ConnectingPanel.Visibility = Visibility.Collapsed;
                        DevicesScrollViewer.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(252, 225, 0));
                        StatusText.Text = msg;
                        if (_service.Devices.Count == 0)
                        {
                            ConnectingPanel.Visibility = Visibility.Visible;
                            DevicesScrollViewer.Visibility = Visibility.Collapsed;
                        }
                    }

                    UpdateFooterInfo();
                });
            };

            _service.Devices.CollectionChanged += (s, args) =>
            {
                if (args.NewItems != null)
                {
                    foreach (DeviceInfo dev in args.NewItems)
                    {
                        dev.PropertyChanged += (devSender, propArgs) =>
                        {
                            if (propArgs.PropertyName == nameof(DeviceInfo.IsActive) ||
                                propArgs.PropertyName == nameof(DeviceInfo.BatteryPercentage) ||
                                propArgs.PropertyName == nameof(DeviceInfo.IsCharging))
                            {
                                Dispatcher.Invoke(UpdateFooterInfo);
                            }
                        };
                    }
                }

                Dispatcher.Invoke(UpdateFooterInfo);
            };

            _service.LowBatteryAlertTriggered += (dev) =>
            {
                if (_settings.LowBatteryAlert)
                {
                    Dispatcher.Invoke(() => _trayManager?.ShowLowBatteryNotification(dev));
                }
            };

            _service.Start();
        }

        private void SetupTrayManager()
        {
            _flyoutWindow = new TaskbarFlyoutWindow(_service, OnOpenMainWindowFromTray, OnOpenSettingsFromTray);
            bool isDark = _settings.ThemeMode == "Dark" || (_settings.ThemeMode == "System" && AppSettings.IsSystemDarkTheme());
            _flyoutWindow.ApplyTheme(isDark);

            _trayManager = new TrayManager(
                _service,
                _settings,
                _flyoutWindow,
                OnOpenMainWindowFromTray,
                OnOpenSettingsFromTray,
                OnExplicitExitFromTray);

            _stripWindow = new TaskbarStripWindow(
                _service,
                _settings,
                _flyoutWindow,
                OnOpenMainWindowFromTray,
                OnOpenSettingsFromTray,
                OnExplicitExitFromTray);

            if (_settings.DisplayMode != "FloatingWindow")
            {
                _stripWindow.Show();
                _stripWindow.UpdatePosition();
            }
        }

        private void OnOpenMainWindowFromTray()
        {
            EnterWindowMode();
            if (TabDevices != null) TabDevices.IsChecked = true;
        }

        private void OnOpenSettingsFromTray()
        {
            EnterWindowMode();
            if (TabSettings != null) TabSettings.IsChecked = true;
        }

        private void OnExplicitExitFromTray()
        {
            _isExplicitExit = true;
            Close();
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General || 
                e.Category == UserPreferenceCategory.Color || 
                e.Category == UserPreferenceCategory.VisualStyle)
            {
                Dispatcher.Invoke(() =>
                {
                    if (_settings.ThemeMode == "System")
                    {
                        ApplyTheme("System");
                    }
                });
            }
        }

        public void ApplyTheme(string mode)
        {
            bool isDark = mode switch
            {
                "Dark" => true,
                "Light" => false,
                _ => AppSettings.IsSystemDarkTheme()
            };

            if (isDark)
            {
                Resources["Win11WindowBg"] = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                Resources["Win11TitleBarBg"] = new SolidColorBrush(Color.FromRgb(38, 38, 38));
                Resources["Win11WindowBorder"] = new SolidColorBrush(Color.FromRgb(56, 56, 56));
                Resources["Win11CardBg"] = new SolidColorBrush(Color.FromRgb(43, 43, 43));
                Resources["Win11CardBorder"] = new SolidColorBrush(Color.FromRgb(62, 62, 62));
                Resources["Win11Accent"] = new SolidColorBrush(Color.FromRgb(96, 205, 255));
                Resources["Win11AccentForeground"] = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                Resources["Win11AccentTint"] = new SolidColorBrush(Color.FromArgb(32, 96, 205, 255));
                Resources["Win11TextPrimary"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Resources["Win11TextSecondary"] = new SolidColorBrush(Color.FromRgb(204, 204, 204));
                Resources["Win11TextMuted"] = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                Resources["Win11SegmentedBg"] = new SolidColorBrush(Color.FromRgb(28, 28, 28));
                Resources["Win11SegmentedActive"] = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                Resources["Win11ProgressTrack"] = new SolidColorBrush(Color.FromRgb(26, 26, 26));
                Resources["Win11ButtonBg"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                Resources["Win11ButtonBorder"] = new SolidColorBrush(Color.FromRgb(62, 62, 62));
                Resources["Win11ToggleTrackUnchecked"] = new SolidColorBrush(Color.FromRgb(64, 64, 64));
                Resources["Win11ToggleBorderUnchecked"] = new SolidColorBrush(Color.FromRgb(96, 96, 96));
                Resources["Win11ToggleThumbUnchecked"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Resources["Win11ToggleThumbChecked"] = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            }
            else
            {
                // Windows 11 Light Theme (Fluent Design contrast & crispness)
                Resources["Win11WindowBg"] = new SolidColorBrush(Color.FromRgb(243, 243, 243));
                Resources["Win11TitleBarBg"] = new SolidColorBrush(Color.FromRgb(234, 234, 234));
                Resources["Win11WindowBorder"] = new SolidColorBrush(Color.FromRgb(212, 212, 212));
                Resources["Win11CardBg"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Resources["Win11CardBorder"] = new SolidColorBrush(Color.FromRgb(229, 229, 229));
                Resources["Win11Accent"] = new SolidColorBrush(Color.FromRgb(0, 95, 184));
                Resources["Win11AccentForeground"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Resources["Win11AccentTint"] = new SolidColorBrush(Color.FromArgb(24, 0, 95, 184));
                Resources["Win11TextPrimary"] = new SolidColorBrush(Color.FromRgb(26, 26, 26));
                Resources["Win11TextSecondary"] = new SolidColorBrush(Color.FromRgb(94, 94, 94));
                Resources["Win11TextMuted"] = new SolidColorBrush(Color.FromRgb(120, 120, 120));
                Resources["Win11SegmentedBg"] = new SolidColorBrush(Color.FromRgb(230, 230, 230));
                Resources["Win11SegmentedActive"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Resources["Win11ProgressTrack"] = new SolidColorBrush(Color.FromRgb(235, 235, 235));
                Resources["Win11ButtonBg"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Resources["Win11ButtonBorder"] = new SolidColorBrush(Color.FromRgb(215, 215, 215));
                Resources["Win11ToggleTrackUnchecked"] = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                Resources["Win11ToggleBorderUnchecked"] = new SolidColorBrush(Color.FromRgb(180, 180, 180));
                Resources["Win11ToggleThumbUnchecked"] = new SolidColorBrush(Color.FromRgb(94, 94, 94));
                Resources["Win11ToggleThumbChecked"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            }

            foreach (var dev in _service.Devices)
            {
                dev.SetTheme(isDark);
            }

            _flyoutWindow?.ApplyTheme(isDark);
            _stripWindow?.UpdateTheme();
            _trayManager?.UpdateTrayIcons();
        }

        private void DisplayModeRadio_Changed(object sender, RoutedEventArgs e)
        {
            if (RadioModeTaskbar == null || RadioModeWindow == null) return;

            if (RadioModeTaskbar.IsChecked == true)
            {
                _settings.DisplayMode = "TaskbarWidget";
                EnterTaskbarMode();
            }
            else
            {
                _settings.DisplayMode = "FloatingWindow";
                if (_stripWindow != null) _stripWindow.Visibility = Visibility.Hidden;
                EnterWindowMode();
            }

            _settings.Save();
            _trayManager?.UpdateTrayIcons();
        }

        private void ChkHoverFlyout_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkHoverFlyout == null) return;
            _settings.HoverFlyoutEnabled = ChkHoverFlyout.IsChecked == true;
            _settings.Save();
        }

        private void ChkIndividualIcons_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkIndividualIcons == null) return;
            _settings.IndividualDeviceIcons = ChkIndividualIcons.IsChecked == true;
            _settings.Save();
            _trayManager?.UpdateTrayIcons();
        }

        private void ChkHideInactive_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkHideInactive == null) return;
            _settings.HideInactiveFromTaskbar = ChkHideInactive.IsChecked == true;
            _settings.Save();
            _trayManager?.UpdateTrayIcons();
        }

        private void ChkShowTrayIcons_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkShowTrayIcons == null) return;
            _settings.ShowTrayIconsWithTaskbarStrip = ChkShowTrayIcons.IsChecked == true;
            _settings.Save();
            _trayManager?.UpdateTrayIcons();
        }

        private void ThemeRadio_Changed(object sender, RoutedEventArgs e)
        {
            if (RadioThemeSystem == null || RadioThemeLight == null || RadioThemeDark == null) return;
            string mode = "System";
            if (RadioThemeLight.IsChecked == true) mode = "Light";
            else if (RadioThemeDark.IsChecked == true) mode = "Dark";

            _settings.ThemeMode = mode;
            _settings.Save();
            ApplyTheme(mode);
        }

        private void IntervalRadio_Changed(object sender, RoutedEventArgs e)
        {
            if (RadioInterval2 == null || RadioInterval5 == null || RadioInterval15 == null || RadioInterval30 == null) return;
            int seconds = 5;
            if (RadioInterval2.IsChecked == true) seconds = 2;
            else if (RadioInterval15.IsChecked == true) seconds = 15;
            else if (RadioInterval30.IsChecked == true) seconds = 30;

            _settings.RefreshIntervalSeconds = seconds;
            _service.RefreshIntervalSeconds = seconds;
            _settings.Save();
        }

        private void Tab_Changed(object sender, RoutedEventArgs e)
        {
            if (ViewDevices == null || ViewSettings == null) return;
            if (TabDevices?.IsChecked == true)
            {
                ViewDevices.Visibility = Visibility.Visible;
                ViewSettings.Visibility = Visibility.Collapsed;
            }
            else
            {
                ViewDevices.Visibility = Visibility.Collapsed;
                ViewSettings.Visibility = Visibility.Visible;
            }
        }

        private void ApplyWindowLayering()
        {
            if (_settings.PinToDesktop)
            {
                NativeMethods.SetPinToDesktop(this, true);
                TxtPinIcon.Text = "📌";
                BtnPin.ToolTip = "Modo: Fixado na Área de Trabalho (Clique para Flutuante)";
            }
            else if (_settings.AlwaysOnTop)
            {
                NativeMethods.SetAlwaysOnTop(this, true);
                TxtPinIcon.Text = "🔝";
                BtnPin.ToolTip = "Modo: Sempre no Topo (Clique para Área de Trabalho)";
            }
            else
            {
                NativeMethods.SetAlwaysOnTop(this, false);
                TxtPinIcon.Text = "📌";
                BtnPin.ToolTip = "Modo: Janela Padrão";
            }
        }

        private void UpdateFooterInfo()
        {
            int total = _service.Devices.Count;
            int active = 0;
            foreach (var d in _service.Devices)
            {
                if (d.IsActive) active++;
            }

            if (total == 0)
            {
                TxtFooterInfo.Text = "Nenhum dispositivo detectado";
            }
            else if (active == 0)
            {
                TxtFooterInfo.Text = $"{total} dispositivo(s) cadastrado(s) (todos inativos)";
            }
            else if (active == total)
            {
                TxtFooterInfo.Text = $"{active} dispositivo(s) conectado(s) e ativo(s)";
            }
            else
            {
                TxtFooterInfo.Text = $"{active} de {total} dispositivo(s) ativo(s)";
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                NativeMethods.DragWindow(this);
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
                _settings.Save();
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await _service.RefreshNowAsync();
        }

        private void BtnPin_Click(object sender, RoutedEventArgs e)
        {
            if (_settings.AlwaysOnTop)
            {
                _settings.AlwaysOnTop = false;
                _settings.PinToDesktop = true;
            }
            else
            {
                _settings.AlwaysOnTop = true;
                _settings.PinToDesktop = false;
            }

            ChkAlwaysOnTop.IsChecked = _settings.AlwaysOnTop;
            ChkPinToDesktop.IsChecked = _settings.PinToDesktop;
            _settings.Save();
            ApplyWindowLayering();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            EnterTaskbarMode();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            EnterTaskbarMode();
        }

        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MainFrame != null)
            {
                MainFrame.Opacity = e.NewValue;
                if (TxtOpacityValue != null)
                {
                    TxtOpacityValue.Text = $"{(int)(e.NewValue * 100)}%";
                }
                _settings.Opacity = e.NewValue;
                _settings.Save();
            }
        }

        private void ChkAlwaysOnTop_Changed(object sender, RoutedEventArgs e)
        {
            _settings.AlwaysOnTop = ChkAlwaysOnTop.IsChecked == true;
            if (_settings.AlwaysOnTop && _settings.PinToDesktop)
            {
                _settings.PinToDesktop = false;
                ChkPinToDesktop.IsChecked = false;
            }
            _settings.Save();
            ApplyWindowLayering();
        }

        private void ChkPinToDesktop_Changed(object sender, RoutedEventArgs e)
        {
            _settings.PinToDesktop = ChkPinToDesktop.IsChecked == true;
            if (_settings.PinToDesktop && _settings.AlwaysOnTop)
            {
                _settings.AlwaysOnTop = false;
                ChkAlwaysOnTop.IsChecked = false;
            }
            _settings.Save();
            ApplyWindowLayering();
        }

        private void ChkAutoStart_Changed(object sender, RoutedEventArgs e)
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                _settings.SetAutoStart(ChkAutoStart.IsChecked == true, exePath);
            }
        }

        private void ChkLowBatteryAlert_Changed(object sender, RoutedEventArgs e)
        {
            _settings.LowBatteryAlert = ChkLowBatteryAlert.IsChecked == true;
            _settings.Save();
        }

        private void BtnLaunchGHub_Click(object sender, RoutedEventArgs e)
        {
            LaunchGHub();
        }

        private void LaunchGHub()
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
                    MessageBox.Show("Não foi possível encontrar o executável do Logitech G HUB em C:\\Program Files\\LGHUB\\lghub.exe.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir o G HUB: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExplicitExit)
            {
                e.Cancel = true;
                EnterTaskbarMode();
                return;
            }

            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.Save();

            _stripWindow?.Dispose();
            _trayManager?.Dispose();
            _flyoutWindow?.Close();
            _service.Dispose();

            System.Windows.Application.Current.Shutdown();
        }
    }
}
