// Windows Tray Toys - a collection of desktop toys that lives in the system tray.
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

namespace WindowsTrayToys
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
            All.Add(new EffectDef("ripples", "Water ripples", "plip", delegate(Point p, Random r, float s) { return new RippleDecal(p, r, s); }));
            All.Add(new EffectDef("fireworks", "Fireworks", "pop", delegate(Point p, Random r, float s) { return new FireworkDecal(p, r, s); }));
            All.Add(new EffectDef("confetti", "Confetti", "pop", delegate(Point p, Random r, float s) { return new ConfettiDecal(p, r, s); }));
            All.Add(new EffectDef("burn", "Burn marks", "sizzle", delegate(Point p, Random r, float s) { return new BurnDecal(p, r, s); }));
            All.Add(new EffectDef("dent", "Hammer dents", "clang", delegate(Point p, Random r, float s) { return new DentDecal(p, r, s); }));
            All.Add(new EffectDef("lightning", "Lightning strikes", "thunder", delegate(Point p, Random r, float s) { return new LightningDecal(p, r, s); }));
        }

        public static EffectDef Find(string key)
        {
            foreach (EffectDef e in All) if (e.Key == key) return e;
            return null;
        }
    }

    /// <summary>Small key=value settings file in %APPDATA%\WindowsTrayToys.</summary>
    static class Settings
    {
        static readonly Dictionary<string, string> map = new Dictionary<string, string>();
        static string path;

        public static void Load()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsTrayToys");
            path = Path.Combine(dir, "settings.ini");
            try
            {
                Directory.CreateDirectory(dir);
                // the project used to be called Glass Breaker: carry settings over from its folder once
                string old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Glass" + "Breaker", "settings.ini");
                if (!File.Exists(path) && File.Exists(old)) File.Copy(old, path);
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
        public static int Int(string key, int def) { int v; return int.TryParse(Get(key, ""), out v) ? v : def; }
        public static void Set(string key, bool value) { Set(key, value ? "1" : "0"); }

        static int unsaved;

        /// <summary>Adds one to a counter. Written to disk only every so often, and on exit, since it changes on every click.</summary>
        public static void Count(string key)
        {
            map[key] = (Int(key, 0) + 1).ToString();
            if (++unsaved >= 25) Save();
        }

        public static void Set(string key, string value)
        {
            map[key] = value;
            Save();
        }

        public static void Save()
        {
            unsaved = 0;
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
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly ToolStripMenuItem miEnabled;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly List<Decal> decals = new List<Decal>();
        readonly Dictionary<string, IAmbient> live = new Dictionary<string, IAmbient>();
        readonly List<Action> menuSync = new List<Action>();
        SettingsForm settingsForm;
        readonly Control marshal = new Control();
        readonly Random rnd = new Random();
        readonly Sounds sounds = new Sounds(Settings.Int("volume", 100));
        readonly HotkeyWindow hotkey = new HotkeyWindow();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Native.HookProc hookProc;   // kept alive for the lifetime of the hook
        readonly float scale;
        IntPtr hook;
        double lastTick, lastScreenCheck;
        bool blocked;   // a full-screen app is in front

        public TrayApp(float scale, bool quiet)
        {
            this.scale = scale;
            if (Settings.On("startpaused", false)) Settings.Set("enabled", false);
            marshal.CreateControl();
            IntPtr force = marshal.Handle;
            timer.Interval = 15;
            timer.Tick += delegate { Tick(); };

            ToolStripMenuItem miSettings = new ToolStripMenuItem("Settings...");
            miSettings.Font = new Font(miSettings.Font, FontStyle.Bold);   // bold = what double-click does
            miSettings.Click += delegate { ShowSettings(); };
            menu.Items.Add(miSettings);
            menu.Items.Add(new ToolStripSeparator());
            menu.Opening += delegate { foreach (Action sync in menuSync) sync(); };

            miEnabled = Check("Enabled", "enabled", true);
            menu.Items.Add(miEnabled);
            // the hotkey is the panic button: it pauses everything and wipes the screen clean, or re-arms
            hotkey.Pressed += delegate
            {
                if (Enabled) Repair();
                miEnabled.Checked = !miEnabled.Checked;
            };
            if (hotkey.Ok) miEnabled.ShortcutKeyDisplayString = HotkeyWindow.Display;
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Picker("Left click", "left"));
            menu.Items.Add(Picker("Middle click", "middle"));
            menu.Items.Add(Picker("Right click", "right"));
            menu.Items.Add(new ToolStripSeparator());
            foreach (AmbientDef a in Ambients.All)
                menu.Items.Add(a.Keys == null ? Check(a.Title, a.Key, false) : Choice(a.Title, a.Key, a.Off, a.Keys, a.Names));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Check("Sound", "sound", true));
            menu.Items.Add(Check("Keep marks until repaired", "persist", false));
            menu.Items.Add(Check("Chain reaction (6 breaks drop the pane)", "chain", true));
            menu.Items.Add(AutoStartItem());
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem miClear = new ToolStripMenuItem("Repair screen");
            miClear.Click += delegate { Repair(); };
            ToolStripMenuItem miExit = new ToolStripMenuItem("Exit");
            miExit.Click += delegate { Quit(); };
            ToolStripMenuItem miCount = new ToolStripMenuItem();
            miCount.Enabled = false;   // a readout, not a command
            menuSync.Add(delegate { miCount.Text = MarksMade(); });
            menu.Items.Add(miCount);
            menu.Items.Add(miClear);
            menu.Items.Add(miExit);

            tray = new NotifyIcon();
            tray.Icon = MakeIcon();
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowSettings(); };
            Apply();
            if (!quiet)
                tray.ShowBalloonTip(4000, Enabled ? "Windows Tray Toys is armed" : "Windows Tray Toys is paused",
                    (Enabled ? "Click anywhere to smash the screen. " : "") + "Double-click this tray icon for settings, or right-click it for the menu.", ToolTipIcon.Info);

            hookProc = HookCallback;
            hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, hookProc, Native.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero)
                MessageBox.Show("Could not install the mouse hook (error " + Marshal.GetLastWin32Error() + ").", "Windows Tray Toys");
        }

        // ---------------------------------------------------------- menu helpers

        static string DefaultFor(string button) { return button == "left" ? "glass" : "none"; }

        ToolStripMenuItem Check(string title, string key, bool def)
        {
            ToolStripMenuItem mi = new ToolStripMenuItem(title);
            mi.Checked = Settings.On(key, def);
            mi.CheckOnClick = true;
            mi.CheckedChanged += delegate { if (Settings.On(key, def) != mi.Checked) { Settings.Set(key, mi.Checked); Apply(); } };
            // the settings window can change the same setting, so re-read it whenever the menu opens
            menuSync.Add(delegate { mi.Checked = Settings.On(key, def); });
            return mi;
        }

        void ShowSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed)
                settingsForm = new SettingsForm(scale, delegate { Apply(); }, tray.Icon, hotkey.Ok);
            settingsForm.RefreshAll();
            settingsForm.Show();
            settingsForm.Activate();
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

        ToolStripMenuItem AutoStartItem()
        {
            ToolStripMenuItem mi = new ToolStripMenuItem("Start with Windows");
            mi.Checked = AutoStart.Get();
            mi.Click += delegate { AutoStart.Set(!mi.Checked); mi.Checked = AutoStart.Get(); Apply(); };
            menuSync.Add(delegate { mi.Checked = AutoStart.Get(); });
            return mi;
        }

        bool Enabled { get { return Settings.On("enabled", true); } }

        public static string MarksMade()
        {
            int n = Settings.Int("marks", 0);
            return n == 0 ? "No marks made yet" : n == 1 ? "1 mark made so far" : n.ToString("N0") + " marks made so far";
        }

        /// <summary>
        /// True when the window in front exactly covers its monitor: a game, a film, a presentation.
        /// A maximized window does not count, because it overshoots the screen by its hidden borders
        /// and leaves room for the taskbar.
        /// </summary>
        static bool FullScreenAppInFront()
        {
            IntPtr h = Native.GetForegroundWindow();
            if (h == IntPtr.Zero) return false;
            uint pid;
            Native.GetWindowThreadProcessId(h, out pid);
            if (pid == (uint)Process.GetCurrentProcess().Id) return false;
            StringBuilder cls = new StringBuilder(64);
            Native.GetClassName(h, cls, cls.Capacity);
            string c = cls.ToString();
            if (c == "Progman" || c == "WorkerW") return false;   // the desktop itself
            Native.RECT r;
            if (!Native.GetWindowRect(h, out r)) return false;
            Rectangle b = Screen.FromHandle(h).Bounds;
            return r.Left == b.Left && r.Top == b.Top && r.Right == b.Right && r.Bottom == b.Bottom;
        }

        /// <summary>Display scale times the user's chosen mark size.</summary>
        float MarkScale
        {
            get
            {
                string size = Settings.Get("size", "normal");
                return scale * (size == "small" ? 0.65f : size == "large" ? 1.4f : size == "huge" ? 1.9f : 1f);
            }
        }

        /// <summary>Brings everything in line with the current settings.</summary>
        void Apply()
        {
            Decal.Persist = Settings.On("persist", false);
            Decal.HoldSeconds = Math.Max(1, Math.Min(60, Settings.Int("hold", 6)));
            sounds.Enabled = Settings.On("sound", true);
            sounds.SetVolume(Settings.Int("volume", 100));
            string keyHint = hotkey.Ok ? " (" + HotkeyWindow.Display + (Enabled ? " to pause)" : " to arm)") : "";
            tray.Text = (Enabled ? "Windows Tray Toys - armed" : "Windows Tray Toys - paused") + keyHint;
            if (settingsForm != null && !settingsForm.IsDisposed && settingsForm.Visible) settingsForm.RefreshAll();

            // start, stop or re-mode the always-on features
            foreach (AmbientDef a in Ambients.All)
            {
                string mode = Settings.Get(a.Key, a.Off);
                IAmbient inst;
                bool have = live.TryGetValue(a.Key, out inst);
                if (Enabled && !blocked && mode != a.Off)
                {
                    if (!have) { inst = a.Make(scale, rnd, sounds); live[a.Key] = inst; }
                    inst.SetMode(mode);
                }
                else if (have) { inst.Dispose(); live.Remove(a.Key); }
            }
            if (live.Count > 0) timer.Start();
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
            // don't smash our own settings window while the user is working in it
            if (settingsForm != null && !settingsForm.IsDisposed && settingsForm.Visible && settingsForm.Bounds.Contains(p)) return;
            if (Settings.On("fullscreen", true) && FullScreenAppInFront()) return;   // leave games and films alone
            foreach (IAmbient a in live.Values) if (a.TryClick(p)) return;   // squashed a bug, poked a creature...
            string key = Settings.Get(button, DefaultFor(button));
            if (key == "none") return;
            EffectDef e = key == "random" ? Effects.All[rnd.Next(Effects.All.Count)] : Effects.Find(key);
            if (e != null) Spawn(e, p);
        }

        void Spawn(EffectDef e, Point p)
        {
            sounds.Play(e.Sound);
            Settings.Count("marks");
            int most = Math.Max(5, Math.Min(150, Settings.Int("maxmarks", 60)));   // the oldest mark makes room for the newest
            while (decals.Count >= most) { decals[0].Destroy(); decals.RemoveAt(0); }
            decals.Add(e.Make(p, rnd, MarkScale));
            if (e.Key == "glass" && Settings.On("chain", true)) ChainReaction(p);
            timer.Start();
        }

        /// <summary>Enough glass breaks close together and the whole pane falls out.</summary>
        void ChainReaction(Point p)
        {
            const int Needed = 6;
            float reach = 260 * MarkScale;
            List<Decal> near = new List<Decal>();
            long sx = 0, sy = 0;
            foreach (Decal d in decals)
            {
                if (!(d is GlassDecal)) continue;
                Point c = d.Center;
                float dx = c.X - p.X, dy = c.Y - p.Y;
                if (dx * dx + dy * dy > reach * reach) continue;
                near.Add(d); sx += c.X; sy += c.Y;
            }
            if (near.Count < Needed) return;
            foreach (Decal d in near) { d.Destroy(); decals.Remove(d); }
            sounds.Play("crash");
            decals.Add(new PaneFallDecal(new Point((int)(sx / near.Count), (int)(sy / near.Count)), rnd, MarkScale));
        }

        void Tick()
        {
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.05, now - lastTick);
            lastTick = now;
            for (int i = decals.Count - 1; i >= 0; i--)
                if (!decals[i].Update()) { decals[i].Destroy(); decals.RemoveAt(i); }
            foreach (IAmbient a in live.Values) a.Tick(dt);

            // once a second, hide the always-on features while a full-screen app is in front
            if (now - lastScreenCheck > 1)
            {
                lastScreenCheck = now;
                bool block = Settings.On("fullscreen", true) && FullScreenAppInFront();
                if (block != blocked) { blocked = block; Apply(); }
            }
            // while blocked the timer must keep running, or nothing would notice the app leaving
            if (decals.Count == 0 && live.Count == 0 && !blocked) timer.Stop();
        }

        void Repair()
        {
            foreach (Decal d in decals) d.Destroy();
            decals.Clear();
            foreach (IAmbient a in live.Values) a.Repair();
        }

        void Quit()
        {
            if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            timer.Stop();
            Settings.Save();   // the mark counter is only written now and then
            hotkey.Dispose();
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.Dispose();
            Repair();
            foreach (IAmbient a in live.Values) a.Dispose();
            live.Clear();
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
            if (args.Length > 0 && args[0] == "--docs") { DocImages.Run(args.Length > 1 ? args[1] : "docs\\images"); return; }
            if (args.Length > 0 && args[0] == "--icon") { DocImages.Icon(args.Length > 1 ? args[1] : "assets\\app.ico"); return; }

            bool first;
            using (Mutex m = new Mutex(true, "WindowsTrayToys.SingleInstance", out first))
            {
                if (!first)
                {
                    MessageBox.Show("Windows Tray Toys is already running - look for its icon in the system tray.", "Windows Tray Toys");
                    return;
                }
                Settings.Load();
                AutoStart.MigrateOldName();
                Application.Run(new TrayApp(scale, Array.IndexOf(args, "--autostart") >= 0));
            }
        }
    }

    /// <summary>
    /// "WindowsTrayToys.exe --selftest [dir]" renders every effect to PNG contact sheets without
    /// touching the screen, and checks that each sound file opens. Used to verify builds.
    /// </summary>
    static class SelfTest
    {
        public static readonly List<KeyValuePair<string, Action<Graphics, float>>> Samples = new List<KeyValuePair<string, Action<Graphics, float>>>();
        const int Cell = 420;

        /// <summary>Collects one sample drawing per always-on feature, plus the chain reaction.</summary>
        public static void BuildSamples()
        {
            if (Samples.Count > 0) return;
            foreach (AmbientDef a in Ambients.All)
                if (a.Sample != null) Samples.Add(new KeyValuePair<string, Action<Graphics, float>>(a.Title, a.Sample));

            Decal.Offscreen = true;
            Samples.Add(new KeyValuePair<string, Action<Graphics, float>>("Chain reaction (0.45s)", delegate(Graphics g, float cell)
            {
                PaneFallDecal d = new PaneFallDecal(new Point(0, 0), new Random(11), 1f);
                using (Bitmap b = d.Simulate(0.45))
                {
                    float k = Math.Min(cell / b.Width, cell / b.Height);
                    g.DrawImage(b, (cell - b.Width * k) / 2, 0, b.Width * k, b.Height * k);
                }
            }));
        }

        public static void Run(float scale, string outDir)
        {
            StringBuilder log = new StringBuilder();
            try
            {
                using (Sounds snd = new Sounds()) foreach (string line in snd.Log) log.AppendLine(line);

                Decal.Offscreen = true;
                BuildSamples();
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

                // the settings window, one picture per tab, side by side
                using (SettingsForm form = new SettingsForm(scale, null, null, true))
                {
                    List<Bitmap> shots = form.Snapshots();
                    using (Bitmap sheet = new Bitmap(shots[0].Width * shots.Count, shots[0].Height))
                    {
                        using (Graphics g = Graphics.FromImage(sheet))
                            for (int i = 0; i < shots.Count; i++) g.DrawImageUnscaled(shots[i], i * shots[0].Width, 0);
                        sheet.Save(Path.Combine(outDir, "selftest_settings.png"), ImageFormat.Png);
                    }
                    foreach (Bitmap b in shots) b.Dispose();
                    log.AppendLine("settings tabs rendered: " + shots.Count);
                }
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
