using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace GHubWidget.Helpers
{
    public static class DeviceIconGenerator
    {
        public static Icon CreateDeviceIcon(string deviceType, string deviceName, int batteryPct, bool isCharging, bool isActive, bool isDarkTaskbar, int size = 16)
        {
            if (size <= 0) size = 16;
            using var bmp = GenerateDeviceIconBitmap(deviceType, deviceName, batteryPct, isCharging, isActive, isDarkTaskbar, size);
            return BitmapToIcon(bmp, size);
        }

        public static Icon CreateDeviceIcon(string deviceType, int batteryPct, bool isCharging, bool isActive, bool isDarkTaskbar, int size = 16)
        {
            return CreateDeviceIcon(deviceType, string.Empty, batteryPct, isCharging, isActive, isDarkTaskbar, size);
        }

        public static Icon CreateCombinedIcon(int activeCount, int lowestBatteryPct, bool hasCharging, bool isDarkTaskbar, int size = 16)
        {
            if (size <= 0) size = 16;
            using var bmp = GenerateCombinedIconBitmap(activeCount, lowestBatteryPct, hasCharging, isDarkTaskbar, size);
            return BitmapToIcon(bmp, size);
        }

        public static Icon CreateOfflineIcon(bool isDarkTaskbar, int size = 16)
        {
            if (size <= 0) size = 16;
            using var bmp = GenerateOfflineIconBitmap(isDarkTaskbar, size);
            return BitmapToIcon(bmp, size);
        }

        public static Icon BitmapToIcon(Bitmap bmp, int size)
        {
            IntPtr hIcon = bmp.GetHicon();
            if (hIcon != IntPtr.Zero)
            {
                return Icon.FromHandle(hIcon);
            }

            return SystemIcons.Application;
        }

        private static Bitmap GenerateDeviceIconBitmap(string deviceType, string deviceName, int batteryPct, bool isCharging, bool isActive, bool isDarkTaskbar, int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.Clear(Color.Transparent);

            float scale = size / 16.0f;

            Color glyphColor;
            Color textColor;

            if (!isActive)
            {
                glyphColor = isDarkTaskbar ? Color.FromArgb(130, 130, 130) : Color.FromArgb(140, 140, 140);
                textColor = glyphColor;
            }
            else if (isCharging)
            {
                glyphColor = isDarkTaskbar ? Color.FromArgb(245, 245, 245) : Color.FromArgb(30, 30, 30);
                textColor = isDarkTaskbar ? Color.FromArgb(0, 215, 255) : Color.FromArgb(0, 120, 215);
            }
            else if (batteryPct > 50)
            {
                glyphColor = isDarkTaskbar ? Color.FromArgb(245, 245, 245) : Color.FromArgb(30, 30, 30);
                textColor = isDarkTaskbar ? Color.FromArgb(60, 230, 95) : Color.FromArgb(16, 140, 16);
            }
            else if (batteryPct > 20)
            {
                glyphColor = isDarkTaskbar ? Color.FromArgb(245, 245, 245) : Color.FromArgb(30, 30, 30);
                textColor = isDarkTaskbar ? Color.FromArgb(255, 205, 0) : Color.FromArgb(200, 115, 0);
            }
            else
            {
                glyphColor = isDarkTaskbar ? Color.FromArgb(245, 245, 245) : Color.FromArgb(30, 30, 30);
                textColor = isDarkTaskbar ? Color.FromArgb(255, 65, 65) : Color.FromArgb(220, 45, 0);
            }

            using var glyphPen = new Pen(glyphColor, Math.Max(1.0f, 1.2f * scale));
            using var glyphBrush = new SolidBrush(glyphColor);
            using var textBrush = new SolidBrush(textColor);

            string upper = ((deviceType ?? string.Empty) + " " + (deviceName ?? string.Empty)).ToUpperInvariant();

            // 1. Draw Top Device Identifier Accent (Compact Rows 0 to 2.5 on 16x16)
            if (upper.Contains("MOUSE") || upper.Contains("SUPERLIGHT") || upper.Contains("G305") || upper.Contains("G502") || upper.Contains("G703") || upper.Contains("G903"))
            {
                // Mouse top curve with center button split line
                float w = 10.0f * scale;
                float x = (size - w) / 2.0f;
                g.DrawArc(glyphPen, x, 0.5f * scale, w, 4.0f * scale, 180, 180);
                g.DrawLine(glyphPen, size / 2.0f, 0.5f * scale, size / 2.0f, 2.2f * scale);
            }
            else if (upper.Contains("KEYBOARD") || upper.Contains("TECLADO") || upper.Contains("TKL") || upper.Contains("G515") || upper.Contains("G915") || upper.Contains("G815"))
            {
                // Keyboard top bar with key notches
                float w = 12.0f * scale;
                float x = (size - w) / 2.0f;
                g.DrawLine(glyphPen, x, 1.5f * scale, x + w, 1.5f * scale);
                // Key pips
                g.FillRectangle(glyphBrush, x + 1.0f * scale, 0.5f * scale, 1.5f * scale, 1.5f * scale);
                g.FillRectangle(glyphBrush, x + 4.0f * scale, 0.5f * scale, 1.5f * scale, 1.5f * scale);
                g.FillRectangle(glyphBrush, x + 7.0f * scale, 0.5f * scale, 1.5f * scale, 1.5f * scale);
                g.FillRectangle(glyphBrush, x + 10.0f * scale, 0.5f * scale, 1.5f * scale, 1.5f * scale);
            }
            else if (upper.Contains("HEADSET") || upper.Contains("HEADPHONE") || upper.Contains("FONE") || upper.Contains("AUDIO") || upper.Contains("G325") || upper.Contains("G733") || upper.Contains("G535") || upper.Contains("G435") || upper.Contains("PRO X"))
            {
                // Headset headband arch with earpads
                float w = 12.0f * scale;
                float x = (size - w) / 2.0f;
                g.DrawArc(glyphPen, x, 0.5f * scale, w, 4.0f * scale, 180, 180);
                g.FillRectangle(glyphBrush, x, 1.5f * scale, 1.5f * scale, 2.0f * scale);
                g.FillRectangle(glyphBrush, x + w - 1.5f * scale, 1.5f * scale, 1.5f * scale, 2.0f * scale);
            }
            else
            {
                // Universal battery top bar
                float w = 12.0f * scale;
                float x = (size - w) / 2.0f;
                g.DrawRectangle(glyphPen, x, 0.5f * scale, w - 2.0f * scale, 2.5f * scale);
                g.FillRectangle(glyphBrush, x + w - 2.0f * scale, 1.0f * scale, 1.5f * scale, 1.5f * scale);
            }

            // 2. Draw Battery Percentage / Status (High Visibility, Maximum Scale)
            string text = !isActive ? "OFF" : (isCharging ? "⚡" : batteryPct.ToString());
            
            // Large font: Arial Bold 12px for 2 digits (e.g. 60, 96), 10.5px for 3 digits (100)
            float fontSize;
            if (size <= 16)
            {
                if (text == "⚡") fontSize = 13.0f;
                else if (text.Length >= 3) fontSize = 10.0f;
                else fontSize = 12.0f;
            }
            else
            {
                if (text == "⚡") fontSize = 24.0f;
                else if (text.Length >= 3) fontSize = 19.0f;
                else fontSize = 23.0f;
            }

            using var font = new Font("Arial", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            // Text occupies the rest of the icon with full width and height
            var textRect = new RectangleF(-1.0f * scale, 2.2f * scale, size + 2.0f * scale, (size - 2.0f) * scale);
            g.DrawString(text, font, textBrush, textRect, sf);

            return bmp;
        }

        private static Bitmap GenerateCombinedIconBitmap(int activeCount, int lowestBatteryPct, bool hasCharging, bool isDarkTaskbar, int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            g.Clear(Color.Transparent);

            float scale = size / 16.0f;

            Color accentColor = hasCharging
                ? (isDarkTaskbar ? Color.FromArgb(0, 215, 255) : Color.FromArgb(0, 120, 215))
                : (lowestBatteryPct > 50
                    ? (isDarkTaskbar ? Color.FromArgb(60, 230, 95) : Color.FromArgb(16, 140, 16))
                    : (lowestBatteryPct > 20
                        ? (isDarkTaskbar ? Color.FromArgb(255, 205, 0) : Color.FromArgb(200, 115, 0))
                        : (isDarkTaskbar ? Color.FromArgb(255, 65, 65) : Color.FromArgb(220, 45, 0))));

            using var pen = new Pen(accentColor, Math.Max(1.2f, 1.4f * scale));
            using var brush = new SolidBrush(accentColor);

            // Battery icon on top (W=13, H=6 on 16x16)
            float w = 13.0f * scale;
            float h = 5.5f * scale;
            float x = (size - w) / 2.0f;
            float y = 0.5f * scale;
            g.DrawRectangle(pen, x, y, w - 2.0f * scale, h);
            g.FillRectangle(brush, x + w - 2.0f * scale, y + 1.5f * scale, 1.5f * scale, h - 3.0f * scale);

            // Fill battery meter
            float fillPct = Math.Max(0.1f, Math.Min(1.0f, lowestBatteryPct / 100.0f));
            g.FillRectangle(brush, x + 1.5f * scale, y + 1.5f * scale, (w - 5.0f * scale) * fillPct, h - 3.0f * scale);

            // Bottom number
            string text = hasCharging ? "⚡" : lowestBatteryPct.ToString();
            float fontSize = size <= 16 ? (text.Length >= 3 ? 7.5f : 8.5f) : (text.Length >= 3 ? 14.0f : 16.5f);
            using var font = new Font("Arial", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            var textRect = new RectangleF(-0.5f, 6.0f * scale, size + 1.0f, 10.0f * scale);
            g.DrawString(text, font, brush, textRect, sf);

            return bmp;
        }

        private static Bitmap GenerateOfflineIconBitmap(bool isDarkTaskbar, int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            g.Clear(Color.Transparent);

            float scale = size / 16.0f;
            Color mutedColor = isDarkTaskbar ? Color.FromArgb(140, 140, 140) : Color.FromArgb(110, 110, 110);
            using var pen = new Pen(mutedColor, Math.Max(1.2f, 1.4f * scale));
            using var brush = new SolidBrush(mutedColor);

            // Empty battery icon outline
            float w = 13.0f * scale;
            float h = 5.5f * scale;
            float x = (size - w) / 2.0f;
            float y = 0.5f * scale;
            g.DrawRectangle(pen, x, y, w - 2.0f * scale, h);
            g.FillRectangle(brush, x + w - 2.0f * scale, y + 1.5f * scale, 1.5f * scale, h - 3.0f * scale);

            // "OFF" text at bottom
            using var font = new Font("Arial", size <= 16 ? 7.0f : 13.0f, FontStyle.Bold, GraphicsUnit.Pixel);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            var textRect = new RectangleF(-0.5f, 6.0f * scale, size + 1.0f, 10.0f * scale);
            g.DrawString("OFF", font, brush, textRect, sf);

            return bmp;
        }
    }
}
