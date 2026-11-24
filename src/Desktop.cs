using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace GlassBreaker
{
    struct WinInfo { public IntPtr H; public Rectangle R; }

    /// <summary>
    /// The visible top-level windows in front-to-back order, refreshed twice a second, plus one
    /// pseudo-window for the taskbar ("the floor"). Lets snow settle and creatures stand on things.
    /// </summary>
    static class WindowList
    {
        public static readonly IntPtr Floor = new IntPtr(-1);
        public static readonly List<WinInfo> Extra = new List<WinInfo>();   // self-test stand-ins, always "visible"
        static List<WinInfo> list = new List<WinInfo>();
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static long stamp = -100000;
        static readonly uint ownPid = (uint)Process.GetCurrentProcess().Id;

        public static List<WinInfo> Get()
        {
            if (clock.ElapsedMilliseconds - stamp < 500) return list;
            stamp = clock.ElapsedMilliseconds;
            List<WinInfo> found = new List<WinInfo>();
            StringBuilder cls = new StringBuilder(64);
            Native.EnumWindows(delegate(IntPtr h, IntPtr lp)
            {
                if (!Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
                if ((Native.GetWindowLong(h, Native.GWL_EXSTYLE) & Native.WS_EX_TOOLWINDOW) != 0) return true;
                uint pid; Native.GetWindowThreadProcessId(h, out pid);
                if (pid == ownPid) return true;
                int cloaked;
                if (Native.DwmGetWindowAttribute(h, Native.DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return true;
                cls.Length = 0; Native.GetClassName(h, cls, cls.Capacity);
                string c = cls.ToString();
                if (c == "Progman" || c == "WorkerW") return true;
                Native.RECT r;
                if (Native.DwmGetWindowAttribute(h, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out r, 16) != 0 && !Native.GetWindowRect(h, out r)) return true;
                if (r.Right - r.Left < 160 || r.Bottom - r.Top < 80) return true;
                WinInfo w; w.H = h; w.R = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                found.Add(w);
                return true;
            }, IntPtr.Zero);

            Rectangle wa = Screen.PrimaryScreen.WorkingArea, full = Screen.PrimaryScreen.Bounds;
            if (full.Bottom > wa.Bottom)
            {
                WinInfo f; f.H = Floor; f.R = Rectangle.FromLTRB(wa.Left, wa.Bottom, wa.Right, full.Bottom);
                found.Add(f);
            }
            found.AddRange(Extra);
            list = found;
            return list;
        }

        public static bool Find(IntPtr h, out WinInfo info)
        {
            foreach (WinInfo w in Get()) if (w.H == h) { info = w; return true; }
            info = new WinInfo();
            return false;
        }

        /// <summary>Is this window's top edge the thing actually showing at screen x (not covered by another window)?</summary>
        public static bool TopVisibleAt(WinInfo w, int x)
        {
            if (w.H == Floor || Extra.Contains(w)) return true;
            IntPtr hit = Native.WindowFromPoint(new Native.POINT(x, w.R.Top + 2));
            return hit != IntPtr.Zero && Native.GetAncestor(hit, Native.GA_ROOT) == w.H;
        }
    }

    /// <summary>
    /// A click-through window covering every monitor, where one key colour is fully transparent.
    /// Cheaper than per-pixel alpha at full-screen size; used for weather and the eaten-away screen.
    /// Draw without anti-aliasing so nothing blends with the key colour.
    /// </summary>
    sealed class KeyedForm : Form
    {
        public static readonly Color Key = Color.FromArgb(255, 0, 255);
        public Action<Graphics, Rectangle> Painter;

        public KeyedForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Key;
            TransparencyKey = Key;
            Bounds = SystemInformation.VirtualScreen;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TRANSPARENT | Native.WS_EX_TOPMOST | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Painter != null) Painter(e.Graphics, e.ClipRectangle);
            else e.Graphics.Clear(Key);
        }
    }
}
