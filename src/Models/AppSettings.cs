using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace GHubWidget.Models
{
    public class AppSettings
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "LogitechGHubWidget";

        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        public bool AlwaysOnTop { get; set; } = true;
        public bool PinToDesktop { get; set; } = false;
        public double Opacity { get; set; } = 0.98;
        public bool AutoStart { get; set; } = false;
        public bool LowBatteryAlert { get; set; } = true;
        public int LowBatteryThreshold { get; set; } = 20;
        public int RefreshIntervalSeconds { get; set; } = 5;
        public string ThemeMode { get; set; } = "System"; // "System", "Dark", "Light"

        private static string SettingsFilePath
        {
            get
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogitechGHubWidget");
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, "settings.json");
            }
        }

        public static AppSettings Load()
        {
            try
            {
                var file = SettingsFilePath;
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        settings.SyncAutoStartWithRegistry();
                        return settings;
                    }
                }
            }
            catch { }

            var defaultSettings = new AppSettings();
            defaultSettings.SyncAutoStartWithRegistry();
            return defaultSettings;
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
        }

        public void SetAutoStart(bool enable, string executablePath)
        {
            AutoStart = enable;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key != null)
                {
                    if (enable)
                    {
                        key.SetValue(AppRegistryName, $"\"{executablePath}\"");
                    }
                    else
                    {
                        key.DeleteValue(AppRegistryName, false);
                    }
                }
            }
            catch { }
            Save();
        }

        private void SyncAutoStartWithRegistry()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                if (key != null)
                {
                    var val = key.GetValue(AppRegistryName);
                    AutoStart = (val != null);
                }
            }
            catch { }
        }

        public static bool IsSystemDarkTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
                if (key != null)
                {
                    var val = key.GetValue("AppsUseLightTheme");
                    if (val is int intVal)
                    {
                        return intVal == 0;
                    }
                }
            }
            catch { }
            return true; // Default to dark on Windows 11
        }
    }
}
