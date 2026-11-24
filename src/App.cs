// Glass Breaker - a Windows desktop toy that lives in the system tray.
// Clicks anywhere on screen leave marks (glass breaks and friends) with matching sounds.
// Clicks are never blocked: every overlay is click-through and the mouse hook only listens.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace GlassBreaker
{
    /// <summary>A click effect the user can assign to a mouse button.</summary>
    sealed class EffectDef
    {
        public readonly string Key, Name, Sound;
        public readonly Func<Point, Random, float, Decal> Make;
        public EffectDef(string key, string name, string sound, Func<Point, Random, float, Decal> make)
        {
            Key = key; Name = name; Sound = sound; Make = make;
        }
    }

    static class Effects
    {
        public static readonly List<EffectDef> All = new List<EffectDef>();

        static Effects()
        {
            All.Add(new EffectDef("glass", "Glass break", "smash", delegate(Point p, Random r, float s) { return new GlassDecal(p, r, s); }));
            All.Add(new EffectDef("bullet", "Bullet holes", "gunshot", delegate(Point p, Random r, float s) { return new BulletDecal(p, r, s); }));
            All.Add(new EffectDef("paintball", "Paintball splats", "splat", delegate(Point p, Random r, float s) { return new PaintballDecal(p, r, s); }));
        }

        public static EffectDef Find(string key)
        {
            foreach (EffectDef e in All) if (e.Key == key) return e;
            return null;
        }
    }

    /// <summary>Small key=value settings file in %APPDATA%\GlassBreaker.</summary>
    static class Settings
    {
        static readonly Dictionary<string, string> map = new Dictionary<string, string>();
        static string path;

        public static void Load()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlassBreaker");
            path = Path.Combine(dir, "settings.ini");
            try
            {
                Directory.CreateDirectory(dir);
                if (!File.Exists(path)) return;
                foreach (string line in File.ReadAllLines(path))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) map[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
            }
            catch (Exception) { }
        }

        public static string Get(string key, string def) { string v; return map.TryGetValue(key, out v) ? v : def; }
        public static bool On(string key, bool def) { return Get(key, def ? "1" : "0") == "1"; }
        public static void Set(string key, bool value) { Set(key, value ? "1" : "0"); }

        public static void Set(string key, string value)
        {
            map[key] = value;
            if (path == null) return;
            try
            {
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, string> kv in map) lines.Add(kv.Key + "=" + kv.Value);
                File.WriteAllLines(path, lines.ToArray());
            }
            catch (Exception) { }
        }
    }

    sealed class TrayApp : ApplicationContext
    {
        const int MaxDecals = 60;
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly ToolStripMenuItem miEnabled;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly List<Decal> decals = new List<Decal>();
        readonly Control marshal = new Control();
        readonly Random rnd = new Random();
        readonly Sounds sounds = new Sounds();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Native.HookProc hookProc;   // kept alive for the lifetime of the hook
        readonly float scale;
        IntPtr hook;
        double lastTick;

        public TrayApp(float scale)
        {
            this.scale = scale;
            marshal.CreateControl();
            IntPtr force = marshal.Handle;

            miEnabled = Check("Enabled", "enabled", true);
            menu.Items.Add(miEnabled);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Picker("Left click", "left"));
            menu.Items.Add(Picker("Middle click", "middle"));
            menu.Items.Add(Picker("Right click", "right"));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Check("Sound", "sound", true));
            menu.Items.Add(Check("Keep marks until repaired", "persist", false));
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem miClear = new ToolStripMenuItem("Repair screen");
            miClear.Click += delegate { Repair(); };
            ToolStripMenuItem miExit = new ToolStripMenuItem("Exit");
            miExit.Click += delegate { Quit(); };
            menu.Items.Add(miClear);
            menu.Items.Add(miExit);

            tray = new NotifyIcon();
            tray.Icon = MakeIcon();
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.DoubleClick += delegate { miEnabled.Checked = !miEnabled.Checked; };
            Apply();
            tray.ShowBalloonTip(4000, "Glass Breaker is armed", "Click anywhere to smash the screen. Right-click this tray icon to pick effects or to exit.", ToolTipIcon.Info);

            timer.Interval = 15;
            timer.Tick += delegate { Tick(); };

            hookProc = HookCallback;
            hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, hookProc, Native.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero)
                MessageBox.Show("Could not install the mouse hook (error " + Marshal.GetLastWin32Error() + ").", "Glass Breaker");
        }

        // ---------------------------------------------------------- menu helpers

        static string DefaultFor(string button) { return button == "left" ? "glass" : "none"; }

        ToolStripMenuItem Check(string title, string key, bool def)
        {
            ToolStripMenuItem mi = new ToolStripMenuItem(title);
            mi.Checked = Settings.On(key, def);
            mi.CheckOnClick = true;
            mi.CheckedChanged += delegate { Settings.Set(key, mi.Checked); Apply(); };
            return mi;
        }

        /// <summary>A submenu of mutually exclusive choices stored under one settings key.</summary>
        ToolStripMenuItem Choice(string title, string key, string def, string[] keys, string[] names)
        {
            ToolStripMenuItem parent = new ToolStripMenuItem(title);
            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[i];
                ToolStripMenuItem mi = new ToolStripMenuItem(names[i]);
                mi.Tag = k;
                mi.Click += delegate { Settings.Set(key, k); Apply(); };
                parent.DropDownItems.Add(mi);
            }
            parent.DropDownOpening += delegate
            {
                string cur = Settings.Get(key, def);
                foreach (ToolStripMenuItem mi in parent.DropDownItems) mi.Checked = (string)mi.Tag == cur;
            };
            return parent;
        }

        ToolStripMenuItem Picker(string title, string button)
        {
            List<string> keys = new List<string>(), names = new List<string>();
            keys.Add("none"); names.Add("Nothing");
            foreach (EffectDef e in Effects.All) { keys.Add(e.Key); names.Add(e.Name); }
            if (Effects.All.Count > 1) { keys.Add("random"); names.Add("Random"); }
            return Choice(title, button, DefaultFor(button), keys.ToArray(), names.ToArray());
        }

        bool Enabled { get { return Settings.On("enabled", true); } }

        /// <summary>Brings everything in line with the current settings.</summary>
        void Apply()
        {
            Decal.Persist = Settings.On("persist", false);
            sounds.Enabled = Settings.On("sound", true);
            tray.Text = Enabled ? "Glass Breaker - armed (double-click to pause)" : "Glass Breaker - paused (double-click to arm)";
        }

        // ---------------------------------------------------------- clicks

        IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && Enabled && !menu.Visible)
            {
                int msg = (int)wParam;
                string button = msg == Native.WM_LBUTTONDOWN ? "left" : msg == Native.WM_MBUTTONDOWN ? "middle" : msg == Native.WM_RBUTTONDOWN ? "right" : null;
                if (button != null)
                {
                    Native.MSLLHOOKSTRUCT info = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                    Point p = new Point(info.pt.x, info.pt.y);
                    // do the work after the hook returns so the real click is never delayed
                    marshal.BeginInvoke((MethodInvoker)delegate { OnClick(p, button); });
                }
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        void OnClick(Point p, string button)
        {
            string key = Settings.Get(button, DefaultFor(button));
            if (key == "none") return;
            EffectDef e = key == "random" ? Effects.All[rnd.Next(Effects.All.Count)] : Effects.Find(key);
            if (e != null) Spawn(e, p);
        }

        void Spawn(EffectDef e, Point p)
        {
            sounds.Play(e.Sound);
            while (decals.Count >= MaxDecals) { decals[0].Destroy(); decals.RemoveAt(0); }
            decals.Add(e.Make(p, rnd, scale));
            timer.Start();
        }

        void Tick()
        {
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.05, now - lastTick);
            lastTick = now;
            for (int i = decals.Count - 1; i >= 0; i--)
                if (!decals[i].Update()) { decals[i].Destroy(); decals.RemoveAt(i); }
            if (decals.Count == 0 && !AmbientActive()) timer.Stop();
        }

        bool AmbientActive() { return false; }

        void Repair()
        {
            foreach (Decal d in decals) d.Destroy();
            decals.Clear();
        }

        void Quit()
        {
            if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            timer.Stop();
            Repair();
            tray.Visible = false;
            tray.Dispose();
            sounds.Dispose();
            ExitThread();
        }

        static Icon MakeIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(2, 3, 28, 26), Color.FromArgb(255, 120, 190, 235), Color.FromArgb(255, 30, 80, 140), 60f))
                        g.FillRectangle(b, 2, 3, 28, 26);
                    using (Pen frame = new Pen(Color.FromArgb(255, 20, 30, 45), 2f)) g.DrawRectangle(frame, 2, 3, 28, 26);
                    using (Pen p = new Pen(Color.White, 1.6f))
                    {
                        Point c = new Point(18, 14);
                        Point[] ends = { new Point(3, 5), new Point(12, 28), new Point(29, 25), new Point(29, 8), new Point(20, 4), new Point(4, 20), new Point(24, 28) };
                        foreach (Point e in ends) g.DrawLine(p, c, e);
                        g.DrawLines(p, new Point[] { new Point(11, 10), new Point(13, 20), new Point(22, 21), new Point(25, 12), new Point(19, 8), new Point(11, 10) });
                    }
                }
                IntPtr h = bmp.GetHicon();
                Icon icon = (Icon)Icon.FromHandle(h).Clone();
                Native.DestroyIcon(h);
                return icon;
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // physical pixels everywhere, so marks land exactly under the cursor on scaled displays
            try { if (!Native.SetProcessDpiAwarenessContext(new IntPtr(-4))) Native.SetProcessDPIAware(); }
            catch (EntryPointNotFoundException) { Native.SetProcessDPIAware(); }
            Application.EnableVisualStyles();

            float scale;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = Math.Max(1f, g.DpiX / 96f);

            if (args.Length > 0 && args[0] == "--selftest") { SelfTest.Run(scale, args.Length > 1 ? args[1] : "."); return; }

            bool first;
            using (Mutex m = new Mutex(true, "GlassBreaker.SingleInstance", out first))
            {
                if (!first)
                {
                    MessageBox.Show("Glass Breaker is already running - look for its icon in the system tray.", "Glass Breaker");
                    return;
                }
                Settings.Load();
                Application.Run(new TrayApp(scale));
            }
        }
    }

    /// <summary>
    /// "GlassBreaker.exe --selftest [dir]" renders every effect to PNG contact sheets without
    /// touching the screen, and checks that each sound file opens. Used to verify builds.
    /// </summary>
    static class SelfTest
    {
        public static readonly List<KeyValuePair<string, Action<Graphics, float>>> Samples = new List<KeyValuePair<string, Action<Graphics, float>>>();
        const int Cell = 420;

        public static void Run(float scale, string outDir)
        {
            StringBuilder log = new StringBuilder();
            try
            {
                using (Sounds snd = new Sounds()) foreach (string line in snd.Log) log.AppendLine(line);

                Decal.Offscreen = true;
                double[] times = { 0.12, 0.45, 5.0 };
                using (Bitmap sheet = new Bitmap(Cell * times.Length, Cell * Effects.All.Count))
                {
                    using (Graphics g = Graphics.FromImage(sheet))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        for (int row = 0; row < Effects.All.Count; row++)
                            for (int col = 0; col < times.Length; col++)
                            {
                                Rectangle cell = new Rectangle(col * Cell, row * Cell, Cell, Cell);
                                Backdrop(g, cell);
                                Decal d = Effects.All[row].Make(new Point(0, 0), new Random(7 + row), 1f);
                                using (Bitmap b = d.Simulate(times[col]))
                                {
                                    float k = Math.Min(1f, Math.Min(Cell / (float)b.Width, Cell / (float)b.Height));
                                    g.DrawImage(b, cell.X + (Cell - b.Width * k) / 2, cell.Y + (Cell - b.Height * k) / 2, b.Width * k, b.Height * k);
                                }
                                d.Destroy();
                                g.DrawString(Effects.All[row].Name + "  t=" + times[col] + "s", SystemFonts.DefaultFont, Brushes.Yellow, cell.X + 6, cell.Y + 6);
                            }
                    }
                    sheet.Save(Path.Combine(outDir, "selftest_effects.png"), ImageFormat.Png);
                }
                log.AppendLine("effects rendered: " + Effects.All.Count);

                if (Samples.Count > 0)
                    using (Bitmap sheet = new Bitmap(Cell * Samples.Count, Cell))
                    {
                        using (Graphics g = Graphics.FromImage(sheet))
                            for (int i = 0; i < Samples.Count; i++)
                            {
                                Rectangle cell = new Rectangle(i * Cell, 0, Cell, Cell);
                                Backdrop(g, cell);
                                g.SetClip(cell);
                                g.TranslateTransform(cell.X, cell.Y);
                                Art.Setup(g);
                                Samples[i].Value(g, Cell);
                                g.ResetTransform();
                                g.ResetClip();
                                g.DrawString(Samples[i].Key, SystemFonts.DefaultFont, Brushes.Yellow, cell.X + 6, cell.Y + 6);
                            }
                        sheet.Save(Path.Combine(outDir, "selftest_ambient.png"), ImageFormat.Png);
                    }
                log.AppendLine("ambient samples rendered: " + Samples.Count);
            }
            catch (Exception ex) { log.AppendLine("FAILED: " + ex); }
            File.WriteAllText(Path.Combine(outDir, "selftest.log"), log.ToString());
        }

        static void Backdrop(Graphics g, Rectangle cell)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(cell, Color.FromArgb(20, 40, 70), Color.FromArgb(240, 240, 240), 0f))
                g.FillRectangle(b, cell);
        }
    }
}
