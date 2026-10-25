using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Forms;
using GHubWidget.Helpers;
using GHubWidget.Models;
using GHubWidget.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Color = System.Windows.Media.Color;

namespace GHubWidget
{
    public partial class MainWindow : Window
    {
        private readonly LgHubService _service = new();
        private AppSettings _settings = new();
        private NotifyIcon? _trayIcon;
        private bool _isExplicitExit = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _settings = AppSettings.Load();

            // Apply Theme
            ApplyTheme(_settings.ThemeMode);

            if (_settings.ThemeMode == "Light")
                RadioThemeLight.IsChecked = true;
            else if (_settings.ThemeMode == "Dark")
                RadioThemeDark.IsChecked = true;
            else
                RadioThemeSystem.IsChecked = true;

            // Apply Interval Pills
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

            // Setup Tray Icon
            SetupTrayIcon();

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
                    UpdateTrayToolTip();
                });
            };

            _service.Devices.CollectionChanged += (s, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateFooterInfo();
                    UpdateTrayToolTip();
                });
            };

            _service.LowBatteryAlertTriggered += (dev) =>
            {
                if (_settings.LowBatteryAlert && _trayIcon != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        _trayIcon.ShowBalloonTip(
                            5000,
                            "Bateria Fraca - Logitech G",
                            $"{dev.Name} está com {dev.BatteryPercentage}% de bateria. Conecte o carregador!",
                            ToolTipIcon.Warning);
                    });
                }
            };

            _service.Start();
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
                Resources["Win11TextPrimary"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Resources["Win11TextSecondary"] = new SolidColorBrush(Color.FromRgb(204, 204, 204));
                Resources["Win11TextMuted"] = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                Resources["Win11SegmentedBg"] = new SolidColorBrush(Color.FromRgb(28, 28, 28));
                Resources["Win11SegmentedActive"] = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                Resources["Win11ProgressTrack"] = new SolidColorBrush(Color.FromRgb(26, 26, 26));
                Resources["Win11ButtonBg"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                Resources["Win11ButtonBorder"] = new SolidColorBrush(Color.FromRgb(62, 62, 62));
            }
            else
            {
                // Windows 11 Light Theme (Maximum contrast & crispness)
                Resources["Win11WindowBg"] = new SolidColorBrush(Color.FromRgb(243, 243, 243));
                Resources["Win11TitleBarBg"] = new SolidColorBrush(Color.FromRgb(234, 234, 234));
                Resources["Win11WindowBorder"] = new SolidColorBrush(Color.FromRgb(220, 220, 220));
                Resources["Win11CardBg"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Resources["Win11CardBorder"] = new SolidColorBrush(Color.FromRgb(229, 229, 229));
                Resources["Win11Accent"] = new SolidColorBrush(Color.FromRgb(0, 95, 184));
                Resources["Win11TextPrimary"] = new SolidColorBrush(Color.FromRgb(26, 26, 26));
                Resources["Win11TextSecondary"] = new SolidColorBrush(Color.FromRgb(94, 94, 94));
                Resources["Win11TextMuted"] = new SolidColorBrush(Color.FromRgb(118, 118, 118));
                Resources["Win11SegmentedBg"] = new SolidColorBrush(Color.FromRgb(230, 230, 230));
                Resources["Win11SegmentedActive"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Resources["Win11ProgressTrack"] = new SolidColorBrush(Color.FromRgb(235, 235, 235));
                Resources["Win11ButtonBg"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Resources["Win11ButtonBorder"] = new SolidColorBrush(Color.FromRgb(215, 215, 215));
            }

            foreach (var dev in _service.Devices)
            {
                dev.SetTheme(isDark);
            }
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

        private void SetupTrayIcon()
        {
            _trayIcon = new NotifyIcon();

            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                {
                    _trayIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    _trayIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                _trayIcon.Icon = SystemIcons.Application;
            }

            _trayIcon.Text = "Logitech G HUB Widget";
            _trayIcon.Visible = true;
            _trayIcon.DoubleClick += (s, e) => ShowAndRestore();

            var menu = new ContextMenuStrip();
            menu.Items.Add("Mostrar Widget", null, (s, e) => ShowAndRestore());
            menu.Items.Add("Abrir Logitech G HUB", null, (s, e) => LaunchGHub());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Sair do Widget", null, (s, e) =>
            {
                _isExplicitExit = true;
                Close();
            });

            _trayIcon.ContextMenuStrip = menu;
        }

        private void ShowAndRestore()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            ApplyWindowLayering();
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
            int count = _service.Devices.Count;
            if (count == 0)
            {
                TxtFooterInfo.Text = "Nenhum dispositivo detectado";
            }
            else
            {
                TxtFooterInfo.Text = $"{count} dispositivo(s) conectado(s)";
            }
        }

        private void UpdateTrayToolTip()
        {
            if (_trayIcon == null) return;
            if (_service.Devices.Count == 0)
            {
                _trayIcon.Text = "Logitech G HUB: Conectando...";
                return;
            }

            var parts = new System.Collections.Generic.List<string>();
            foreach (var d in _service.Devices)
            {
                parts.Add($"{d.DeviceCategoryLabel}: {d.BatteryPercentage}%");
            }
            var text = string.Join(" | ", parts);
            if (text.Length > 63) text = text.Substring(0, 60) + "...";
            _trayIcon.Text = text;
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
            Hide();
            _trayIcon?.ShowBalloonTip(2000, "Logitech G HUB Widget", "O widget continua ativo na bandeja do sistema.", ToolTipIcon.Info);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            _trayIcon?.ShowBalloonTip(2000, "Logitech G HUB Widget", "O widget foi minimizado para a bandeja.", ToolTipIcon.Info);
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
                Hide();
                _trayIcon?.ShowBalloonTip(2000, "Logitech G HUB Widget", "O widget continua rodando na bandeja.", ToolTipIcon.Info);
                return;
            }

            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            _settings.Save();

            _trayIcon?.Dispose();
            _service.Dispose();
        }
    }
}
