using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GHubWidget.Helpers;
using GHubWidget.Models;
using GHubWidget.Services;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Drawing.Point;

namespace GHubWidget.Views
{
    public partial class TaskbarFlyoutWindow : Window
    {
        private readonly DispatcherTimer _mouseCheckTimer;
        private Point _lastTrayPoint;
        private int _outsideTicks = 0;
        private bool _isPinned = false;
        private readonly LgHubService _service;
        private readonly Action _onOpenMainWindow;
        private readonly Action _onOpenSettings;

        public bool IsPinned => _isPinned;

        public TaskbarFlyoutWindow(LgHubService service, Action onOpenMainWindow, Action onOpenSettings)
        {
            InitializeComponent();
            _service = service;
            _onOpenMainWindow = onOpenMainWindow;
            _onOpenSettings = onOpenSettings;

            DevicesItemsControl.ItemsSource = _service.Devices;

            _mouseCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _mouseCheckTimer.Tick += MouseCheckTimer_Tick;

            UpdateConnectionStatus(_service.IsConnected, _service.StatusMessage);

            _service.ConnectionStatusChanged += (connected, msg) =>
            {
                Dispatcher.Invoke(() => UpdateConnectionStatus(connected, msg));
            };
        }

        public void UpdateConnectionStatus(bool connected, string msg)
        {
            if (connected)
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(108, 203, 95));
                StatusText.Text = "Conectado";
                BannerConnecting.Visibility = Visibility.Collapsed;
            }
            else
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(252, 225, 0));
                StatusText.Text = "Aguardando";
                TxtConnectingMsg.Text = msg;
                if (_service.Devices.Count == 0)
                {
                    BannerConnecting.Visibility = Visibility.Visible;
                }
            }
        }

        public void ApplyTheme(bool isDark)
        {
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
                Resources["Win11ProgressTrack"] = new SolidColorBrush(Color.FromRgb(26, 26, 26));
                Resources["Win11ButtonBg"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                Resources["Win11ButtonBorder"] = new SolidColorBrush(Color.FromRgb(62, 62, 62));
            }
            else
            {
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
                Resources["Win11ProgressTrack"] = new SolidColorBrush(Color.FromRgb(235, 235, 235));
                Resources["Win11ButtonBg"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Resources["Win11ButtonBorder"] = new SolidColorBrush(Color.FromRgb(215, 215, 215));
            }
        }

        public void ShowAt(Point cursorPoint, DeviceInfo? triggerDevice = null)
        {
            _lastTrayPoint = cursorPoint;
            _outsideTicks = 0;

            // Make sure dimensions are calculated
            UpdateLayout();
            Measure(new System.Windows.Size(Width, double.PositiveInfinity));

            double targetWidth = Width;
            double targetHeight = DesiredSize.Height > 0 ? DesiredSize.Height : 280;

            // Determine monitor working area from cursor position
            var screen = Screen.FromPoint(cursorPoint);
            var workArea = screen.WorkingArea;

            // Calculate taskbar alignment
            double left = cursorPoint.X - targetWidth / 2.0;
            // Clamp horizontal position
            left = Math.Max(workArea.Left + 12, Math.Min(left, workArea.Right - targetWidth - 12));

            double top;
            // Check if taskbar is at bottom, top, left, or right
            if (screen.Bounds.Bottom > workArea.Bottom)
            {
                // Standard Taskbar at bottom
                top = workArea.Bottom - targetHeight - 8;
            }
            else if (screen.Bounds.Top < workArea.Top)
            {
                // Taskbar at top
                top = workArea.Top + 8;
            }
            else if (screen.Bounds.Right > workArea.Right)
            {
                // Taskbar at right
                top = Math.Max(workArea.Top + 12, Math.Min(cursorPoint.Y - targetHeight / 2.0, workArea.Bottom - targetHeight - 12));
                left = workArea.Right - targetWidth - 8;
            }
            else
            {
                // Taskbar at left
                top = Math.Max(workArea.Top + 12, Math.Min(cursorPoint.Y - targetHeight / 2.0, workArea.Bottom - targetHeight - 12));
                left = workArea.Left + 8;
            }

            Left = left;
            Top = top;

            if (!IsVisible)
            {
                Opacity = 0;
                Show();
                var anim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(130));
                BeginAnimation(OpacityProperty, anim);
            }
            else
            {
                Opacity = 1.0;
            }

            if (!_mouseCheckTimer.IsEnabled)
            {
                _mouseCheckTimer.Start();
            }
        }

        public void HideFlyout()
        {
            if (_isPinned) return;
            _mouseCheckTimer.Stop();
            var anim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(100));
            anim.Completed += (s, e) =>
            {
                if (!_mouseCheckTimer.IsEnabled)
                {
                    Hide();
                }
            };
            BeginAnimation(OpacityProperty, anim);
        }

        private void MouseCheckTimer_Tick(object? sender, EventArgs e)
        {
            if (_isPinned) return;

            if (!NativeMethods.GetCursorPos(out var cursorPos))
            {
                return;
            }

            // Window boundary with generous 12px margin
            bool isOverWindow = cursorPos.X >= (Left - 12) &&
                               cursorPos.X <= (Left + ActualWidth + 12) &&
                               cursorPos.Y >= (Top - 12) &&
                               cursorPos.Y <= (Top + ActualHeight + 12);

            // Tray icon area (45px radius around the tray position)
            double distToTray = Math.Sqrt(Math.Pow(cursorPos.X - _lastTrayPoint.X, 2) + Math.Pow(cursorPos.Y - _lastTrayPoint.Y, 2));
            bool isNearTray = distToTray <= 50;

            if (isOverWindow || isNearTray)
            {
                _outsideTicks = 0;
            }
            else
            {
                _outsideTicks++;
                // Debounce ~360ms (3 ticks of 120ms) before fading out
                if (_outsideTicks >= 3)
                {
                    HideFlyout();
                }
            }
        }

        private void BtnPin_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = !_isPinned;
            if (_isPinned)
            {
                TxtPinIcon.Text = "📍";
                BtnPin.ToolTip = "Pop-up fixado (clique para desafixar)";
                BtnPin.Background = (Brush)Resources["Win11AccentTint"];
            }
            else
            {
                TxtPinIcon.Text = "📌";
                BtnPin.ToolTip = "Fixar pop-up aberto";
                BtnPin.Background = Brushes.Transparent;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = false;
            TxtPinIcon.Text = "📌";
            BtnPin.Background = Brushes.Transparent;
            HideFlyout();
        }

        private void BtnLaunchGHub_Click(object sender, RoutedEventArgs e)
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

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await _service.RefreshNowAsync();
        }

        private void BtnOpenMainWindow_Click(object sender, RoutedEventArgs e)
        {
            _onOpenMainWindow?.Invoke();
            HideFlyout();
        }

        private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
        {
            _onOpenSettings?.Invoke();
            HideFlyout();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (!_isPinned)
            {
                HideFlyout();
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _isPinned = false;
                HideFlyout();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            _mouseCheckTimer.Stop();
            Hide();
        }
    }
}
