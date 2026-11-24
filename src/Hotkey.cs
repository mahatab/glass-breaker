using System;
using System.Windows.Forms;

namespace WindowsTrayToys
{
    /// <summary>Registers Ctrl+Alt+G system-wide and raises Pressed when it is hit.</summary>
    sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        public const string Display = "Ctrl+Alt+G";
        const int MOD_ALT = 1, MOD_CONTROL = 2, MOD_NOREPEAT = 0x4000;
        public readonly bool Ok;
        public event EventHandler Pressed;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams());
            Ok = Native.RegisterHotKey(Handle, 1, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (int)Keys.G);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && Pressed != null) Pressed(this, EventArgs.Empty);
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle == IntPtr.Zero) return;
            if (Ok) Native.UnregisterHotKey(Handle, 1);
            DestroyHandle();
        }
    }
}
