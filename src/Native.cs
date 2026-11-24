using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsTrayToys
{
    static class Native
    {
        public const int WH_MOUSE_LL = 14;
        public const int WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204, WM_MBUTTONDOWN = 0x0207, WM_HOTKEY = 0x0312;
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOPMOST = 0x8,
                         WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        public const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4;
        public const int ULW_ALPHA = 2;
        public const int GWL_EXSTYLE = -20, GA_ROOT = 2;
        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9, DWMWA_CLOAKED = 14;

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; public POINT(int x, int y) { this.x = x; this.y = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; public SIZE(int x, int y) { cx = x; cy = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, int modifiers, int vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, int flags);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] public static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT value, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] public static extern int mciSendString(string command, StringBuilder ret, int retLen, IntPtr hwnd);
    }
}
