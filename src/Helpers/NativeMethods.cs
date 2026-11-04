using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GHubWidget.Helpers
{
    public static class NativeMethods
    {
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        public const int SW_SHOWNOACTIVATE = 4;
        public const int SW_HIDE = 0;

        public const int SM_CXSMICON = 49;
        public const int SM_CYSMICON = 50;

        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const int GWLP_HWNDPARENT = -8;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const uint WS_CAPTION = 0x00C00000;
        public const uint WS_MAXIMIZE = 0x01000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        public static int GetWindowLong(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return (int)GetWindowLongPtr64(hWnd, nIndex).ToInt64();
            return GetWindowLong32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        public static int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong)
        {
            if (IntPtr.Size == 8)
                return (int)SetWindowLongPtr64(hWnd, nIndex, new IntPtr(dwNewLong)).ToInt64();
            return SetWindowLong32(hWnd, nIndex, dwNewLong);
        }

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint WINEVENT_OUTOFCONTEXT = 0;

        public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", EntryPoint = "FindWindowExW", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CreateIconFromResourceEx(
            byte[] pbIconBits,
            uint cbIconBits,
            bool fIcon,
            uint dwVersion,
            int cxDesired,
            int cyDesired,
            uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public static void DragWindow(Window window)
        {
            var helper = new WindowInteropHelper(window);
            ReleaseCapture();
            SendMessage(helper.Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
        }

        public static void SetPinToDesktop(Window window, bool pin)
        {
            var helper = new WindowInteropHelper(window);
            if (pin)
            {
                window.Topmost = false;
                SetWindowPos(helper.Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            else
            {
                SetWindowPos(helper.Handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }

        public static void SetAlwaysOnTop(Window window, bool alwaysOnTop)
        {
            var helper = new WindowInteropHelper(window);
            if (alwaysOnTop)
            {
                window.Topmost = true;
                SetWindowPos(helper.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
            else
            {
                window.Topmost = false;
                SetWindowPos(helper.Handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
        }

        public static bool IsForegroundWindowFullscreen()
        {
            // Fully disabled: Taskbar-anchored widget should never hide arbitrarily when switching apps or clicking desktop
            return false;
        }
    }
}
