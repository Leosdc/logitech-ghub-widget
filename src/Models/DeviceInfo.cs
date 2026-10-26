using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace GHubWidget.Models
{
    public class DeviceInfo : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _deviceType = "UNKNOWN";
        private int _batteryPercentage = 0;
        private bool _isCharging = false;
        private double _mileage = 0;
        private double _maxLifespan = 0;
        private double _consumptionMw = 0;
        private bool _isActive = true;
        private string _connectionType = "WIRELESS";
        private bool _isDarkTheme = true;

        public void SetTheme(bool isDark)
        {
            _isDarkTheme = isDark;
            OnPropertyChanged(nameof(BatteryColor));
            OnPropertyChanged(nameof(BatteryGlowColor));
            OnPropertyChanged(nameof(StatusDotColor));
            OnPropertyChanged(nameof(StatusBadgeBg));
            OnPropertyChanged(nameof(StatusBadgeBorder));
            OnPropertyChanged(nameof(StatusBadgeFg));
        }

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string DeviceType
        {
            get => _deviceType;
            set 
            { 
                _deviceType = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeviceCategoryIcon));
                OnPropertyChanged(nameof(DeviceCategoryLabel));
            }
        }

        public int BatteryPercentage
        {
            get => _batteryPercentage;
            set 
            { 
                _batteryPercentage = Math.Clamp(value, 0, 100); 
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormattedPercentage));
                OnPropertyChanged(nameof(BatteryColor));
                OnPropertyChanged(nameof(BatteryGlowColor));
                OnPropertyChanged(nameof(BatteryProgressFraction));
                OnPropertyChanged(nameof(BatteryProgressValue));
                OnPropertyChanged(nameof(FormattedMileage));
            }
        }

        public bool IsCharging
        {
            get => _isCharging;
            set 
            { 
                _isCharging = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusBadgeText));
                OnPropertyChanged(nameof(StatusDotColor));
                OnPropertyChanged(nameof(StatusBadgeBg));
                OnPropertyChanged(nameof(StatusBadgeBorder));
                OnPropertyChanged(nameof(StatusBadgeFg));
                OnPropertyChanged(nameof(FormattedMileage));
                OnPropertyChanged(nameof(BatteryColor));
                OnPropertyChanged(nameof(BatteryGlowColor));
            }
        }

        public double Mileage
        {
            get => _mileage;
            set 
            { 
                _mileage = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormattedMileage));
                OnPropertyChanged(nameof(HasMileage));
            }
        }

        public double MaxLifespan
        {
            get => _maxLifespan;
            set { _maxLifespan = value; OnPropertyChanged(); }
        }

        public double ConsumptionMw
        {
            get => _consumptionMw;
            set 
            { 
                _consumptionMw = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(FormattedConsumption));
                OnPropertyChanged(nameof(HasConsumption));
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set 
            { 
                if (_isActive != value)
                {
                    _isActive = value; 
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusBadgeText));
                    OnPropertyChanged(nameof(StatusDotColor));
                    OnPropertyChanged(nameof(StatusBadgeBg));
                    OnPropertyChanged(nameof(StatusBadgeBorder));
                    OnPropertyChanged(nameof(StatusBadgeFg));
                    OnPropertyChanged(nameof(FormattedPercentage));
                    OnPropertyChanged(nameof(FormattedMileage));
                    OnPropertyChanged(nameof(FormattedConsumption));
                    OnPropertyChanged(nameof(BatteryColor));
                    OnPropertyChanged(nameof(BatteryGlowColor));
                    OnPropertyChanged(nameof(BatteryProgressValue));
                    OnPropertyChanged(nameof(CardOpacity));
                }
            }
        }

        public string ConnectionType
        {
            get => _connectionType;
            set { _connectionType = value; OnPropertyChanged(); }
        }

        public double CardOpacity => _isActive ? 1.0 : 0.60;

        public int BatteryProgressValue
        {
            get => _isActive ? _batteryPercentage : 0;
            set { }
        }

        public string FormattedPercentage => _isActive ? $"{_batteryPercentage}%" : "Inativo";

        public double BatteryProgressFraction => _isActive ? (_batteryPercentage / 100.0) : 0.0;

        public bool HasMileage => _isActive && _mileage > 0;

        public bool HasConsumption => _isActive && _consumptionMw > 0.05;

        public string FormattedMileage
        {
            get
            {
                if (!_isActive)
                {
                    if (_batteryPercentage > 0)
                        return $"Inativo • Última carga: {_batteryPercentage}%";
                    return "Dispositivo inativo ou desligado";
                }

                if (_isCharging) return "Carregando...";
                if (_mileage > 0)
                {
                    int hours = (int)Math.Round(_mileage);
                    return $"~{hours}h de duração estimada";
                }
                return "Duração em cálculo";
            }
        }

        public string FormattedConsumption
        {
            get
            {
                if (!_isActive) return "Desconectado";
                return _consumptionMw > 0 ? $"Consumo: {_consumptionMw:F1} mW" : "Consumo normal";
            }
        }

        public string StatusBadgeText
        {
            get
            {
                if (!_isActive) return "Inativo";
                if (_isCharging) return "Carregando";
                return "Ativo";
            }
        }

        public Brush StatusDotColor
        {
            get
            {
                if (!_isActive)
                    return _isDarkTheme 
                        ? new SolidColorBrush(Color.FromRgb(140, 140, 140)) 
                        : new SolidColorBrush(Color.FromRgb(160, 160, 160));
                if (_isCharging)
                    return _isDarkTheme 
                        ? new SolidColorBrush(Color.FromRgb(96, 205, 255)) 
                        : new SolidColorBrush(Color.FromRgb(0, 95, 184));
                return _isDarkTheme 
                    ? new SolidColorBrush(Color.FromRgb(108, 203, 95)) 
                    : new SolidColorBrush(Color.FromRgb(15, 123, 15));
            }
        }

        public Brush StatusBadgeBg
        {
            get
            {
                if (!_isActive)
                    return _isDarkTheme
                        ? new SolidColorBrush(Color.FromArgb(25, 255, 255, 255))
                        : new SolidColorBrush(Color.FromArgb(15, 0, 0, 0));
                if (_isCharging)
                    return _isDarkTheme
                        ? new SolidColorBrush(Color.FromArgb(35, 96, 205, 255))
                        : new SolidColorBrush(Color.FromArgb(25, 0, 95, 184));
                return _isDarkTheme
                    ? new SolidColorBrush(Color.FromArgb(35, 108, 203, 95))
                    : new SolidColorBrush(Color.FromArgb(25, 15, 123, 15));
            }
        }

        public Brush StatusBadgeBorder
        {
            get
            {
                if (!_isActive)
                    return _isDarkTheme
                        ? new SolidColorBrush(Color.FromArgb(45, 255, 255, 255))
                        : new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
                if (_isCharging)
                    return _isDarkTheme
                        ? new SolidColorBrush(Color.FromArgb(70, 96, 205, 255))
                        : new SolidColorBrush(Color.FromArgb(50, 0, 95, 184));
                return _isDarkTheme
                    ? new SolidColorBrush(Color.FromArgb(70, 108, 203, 95))
                    : new SolidColorBrush(Color.FromArgb(50, 15, 123, 15));
            }
        }

        public Brush StatusBadgeFg
        {
            get
            {
                if (!_isActive)
                    return _isDarkTheme
                        ? new SolidColorBrush(Color.FromRgb(170, 170, 170))
                        : new SolidColorBrush(Color.FromRgb(115, 115, 115));
                if (_isCharging)
                    return _isDarkTheme
                        ? new SolidColorBrush(Color.FromRgb(96, 205, 255))
                        : new SolidColorBrush(Color.FromRgb(0, 95, 184));
                return _isDarkTheme
                    ? new SolidColorBrush(Color.FromRgb(108, 203, 95))
                    : new SolidColorBrush(Color.FromRgb(15, 123, 15));
            }
        }

        public string DeviceCategoryLabel
        {
            get
            {
                var upper = (_deviceType + " " + _name).ToUpperInvariant();
                if (upper.Contains("HEADSET") || upper.Contains("FONE") || upper.Contains("AUDIO")) return "Headset Sem Fio";
                if (upper.Contains("KEYBOARD") || upper.Contains("TECLADO") || upper.Contains("TKL")) return "Teclado Sem Fio";
                if (upper.Contains("MOUSE") || upper.Contains("SUPERLIGHT")) return "Mouse Sem Fio";
                return "Dispositivo Logitech";
            }
        }

        public string DeviceCategoryIcon
        {
            get
            {
                var upper = (_deviceType + " " + _name).ToUpperInvariant();
                if (upper.Contains("HEADSET") || upper.Contains("FONE") || upper.Contains("AUDIO")) return "🎧";
                if (upper.Contains("KEYBOARD") || upper.Contains("TECLADO") || upper.Contains("TKL")) return "⌨️";
                if (upper.Contains("MOUSE") || upper.Contains("SUPERLIGHT")) return "🖱️";
                return "🎮";
            }
        }

        // Dynamic High-Contrast Windows 11 Colors for Dark & Light Mode
        public Brush BatteryColor
        {
            get
            {
                if (!_isActive)
                {
                    return _isDarkTheme
                        ? new SolidColorBrush(Color.FromRgb(140, 140, 140))
                        : new SolidColorBrush(Color.FromRgb(130, 130, 130));
                }

                if (_isDarkTheme)
                {
                    if (_isCharging)
                        return new SolidColorBrush(Color.FromRgb(96, 205, 255)); // Win11 Cyan #60CDFF
                    if (_batteryPercentage > 50)
                        return new SolidColorBrush(Color.FromRgb(108, 203, 95)); // Win11 Green #6CCB5F
                    if (_batteryPercentage > 20)
                        return new SolidColorBrush(Color.FromRgb(252, 225, 0));  // Win11 Amber #FCE100
                    return new SolidColorBrush(Color.FromRgb(255, 153, 164));   // Win11 Red #FF99A4
                }
                else
                {
                    if (_isCharging)
                        return new SolidColorBrush(Color.FromRgb(0, 95, 184));   // High-contrast Blue #005FB8
                    if (_batteryPercentage > 50)
                        return new SolidColorBrush(Color.FromRgb(15, 123, 15));  // High-contrast Green #0F7B0F
                    if (_batteryPercentage > 20)
                        return new SolidColorBrush(Color.FromRgb(158, 98, 0));   // High-contrast Amber #9E6200
                    return new SolidColorBrush(Color.FromRgb(196, 43, 28));    // High-contrast Red #C42B1C
                }
            }
        }

        public Brush BatteryGlowColor
        {
            get
            {
                if (!_isActive)
                    return new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                if (_isDarkTheme)
                {
                    return new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                }
                else
                {
                    return new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
