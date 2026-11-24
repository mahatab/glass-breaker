// Glass Breaker - a Windows desktop toy.
// Lives in the system tray; every left click anywhere on screen "cracks the monitor"
// at that spot with an animated glass break and a smash sound.
// Clicks are never blocked: the overlay is click-through and the mouse hook only listens.
//
// Build: see build.cmd (uses the C# compiler that ships with Windows, no SDK needed).

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
    static class Native
    {
        public const int WH_MOUSE_LL = 14;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOPMOST = 0x8,
                         WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        public const int SW_SHOWNOACTIVATE = 4;
        public const int ULW_ALPHA = 2;

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; public POINT(int x, int y) { this.x = x; this.y = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; public SIZE(int x, int y) { cx = x; cy = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

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
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] public static extern int mciSendString(string command, StringBuilder ret, int retLen, IntPtr hwnd);
    }

    // ---------------------------------------------------------------- sound

    /// <summary>Synthesizes a handful of glass-smash variations and plays them (overlapping) through MCI.</summary>
    sealed class SmashSound : IDisposable
    {
        const int Variants = 6;
        readonly Random rnd = new Random();
        public readonly int[] OpenResults = new int[Variants];
        int last = -1;

        public SmashSound()
        {
            string dir = Path.Combine(Path.GetTempPath(), "GlassBreaker");
            Directory.CreateDirectory(dir);
            for (int i = 0; i < Variants; i++)
            {
                string path = Path.Combine(dir, "smash" + i + ".wav");
                try { File.WriteAllBytes(path, Synthesize(1000 + i * 77)); } catch (IOException) { /* in use by another instance; reuse */ }
                OpenResults[i] = Native.mciSendString("open \"" + path + "\" type waveaudio alias gb" + i, null, 0, IntPtr.Zero);
            }
        }

        public void Play()
        {
            int i = rnd.Next(Variants);
            if (i == last) i = (i + 1) % Variants;   // avoid cutting off the one still ringing
            last = i;
            Native.mciSendString("play gb" + i + " from 0", null, 0, IntPtr.Zero);
        }

        public string Status(int i)
        {
            StringBuilder sb = new StringBuilder(64);
            int r = Native.mciSendString("status gb" + i + " length", sb, sb.Capacity, IntPtr.Zero);
            return "rc=" + r + " length=" + sb + "ms";
        }

        public void Dispose()
        {
            for (int i = 0; i < Variants; i++) Native.mciSendString("close gb" + i, null, 0, IntPtr.Zero);
        }

        static byte[] Synthesize(int seed)
        {
            const int sr = 44100;
            Random r = new Random(seed);
            int n = (int)(sr * 1.35);
            double[] buf = new double[n];

            // 1. the hit: a short low thump
            double thumpHz = 90 + r.NextDouble() * 60;
            for (int i = 0; i < sr * 0.09; i++)
            {
                double t = i / (double)sr;
                buf[i] += Math.Sin(2 * Math.PI * thumpHz * t) * Math.Exp(-t * 42) * 0.7;
            }

            // 2. the crash: bright (high-passed) noise, sharp attack with a hissy tail
            double prev = 0, hp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)sr;
                double white = r.NextDouble() * 2 - 1;
                hp = 0.82 * (hp + white - prev);
                prev = white;
                double env = 1.0 * Math.Exp(-t * 55) + 0.32 * Math.Exp(-t * 8);
                buf[i] += hp * env * 0.55;
            }

            // 3. the tinkle: lots of tiny ringing shards, dense at first then scattering
            int count = 55 + r.Next(30);
            for (int k = 0; k < count; k++)
            {
                double start = Math.Pow(r.NextDouble(), 2.2) * 0.85;
                double f = 1900 + Math.Pow(r.NextDouble(), 1.4) * 9000;
                double decay = 16 + r.NextDouble() * 75;
                double amp = (0.05 + r.NextDouble() * 0.17) * (1 - start * 0.7);
                double f2 = f * (2.3 + r.NextDouble() * 0.9);
                bool second = f2 < 19000;
                int s0 = (int)(start * sr), len = (int)(sr * 6.0 / decay);
                for (int j = 0; j < len && s0 + j < n; j++)
                {
                    double t = j / (double)sr;
                    double v = Math.Sin(2 * Math.PI * f * t);
                    if (second) v += 0.5 * Math.Sin(2 * Math.PI * f2 * t + 1.0);
                    buf[s0 + j] += amp * Math.Exp(-t * decay) * v * Math.Min(1.0, j / 20.0);
                }
            }

            // normalize with a soft clip, fade the very end
            double peak = 0;
            for (int i = 0; i < n; i++) peak = Math.Max(peak, Math.Abs(buf[i]));
            MemoryStream ms = new MemoryStream();
            BinaryWriter w = new BinaryWriter(ms);
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(sr); w.Write(sr * 2); w.Write((short)2); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
            for (int i = 0; i < n; i++)
            {
                double v = Math.Tanh(buf[i] / peak * 1.6) * 0.95;
                int tail = n - i;
                if (tail < 2000) v *= tail / 2000.0;
                w.Write((short)(v * 32767));
            }
            w.Flush();
            return ms.ToArray();
        }
    }

    // ---------------------------------------------------------------- crack

    /// <summary>One impact: its own small click-through, per-pixel-alpha, always-on-top window.</summary>
    sealed class Crack : NativeWindow
    {
        struct Seg { public PointF A, B; public float D, W; }
        struct Facet { public PointF[] P; public float D; public Color C; }
        class Shard { public float X, Y, VX, VY, Rot, Spin, Size; public int Tint; }

        const double GrowTime = 0.17, ShardTime = 0.8, FadeTime = 0.9;
        public static double HoldSeconds = 6;
        public static bool Persist = false;

        readonly List<Seg> segs = new List<Seg>();
        readonly List<Facet> facets = new List<Facet>();
        readonly List<Shard> shards = new List<Shard>();
        readonly Random rnd;
        readonly float scale, R, c;
        readonly int size;
        readonly Point origin;
        readonly Stopwatch clock = Stopwatch.StartNew();
        Bitmap frame, still;
        bool stillShown;
        double fadeStart = -1, lastT;

        public Crack(Point screenPoint, Random rnd, float scale)
        {
            this.rnd = rnd;
            this.scale = scale;
            R = (280 + rnd.Next(110)) * scale;
            size = (int)(R * 2 + 40 * scale);
            c = size / 2f;
            origin = new Point(screenPoint.X - size / 2, screenPoint.Y - size / 2);
            Build();

            CreateParams cp = new CreateParams();
            cp.Style = Native.WS_POPUP;
            cp.ExStyle = Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOPMOST | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
            cp.X = origin.X; cp.Y = origin.Y; cp.Width = size; cp.Height = size;
            CreateHandle(cp);
            frame = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            Update();
            Native.ShowWindow(Handle, Native.SW_SHOWNOACTIVATE);
        }

        float Rand(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        void AddSeg(PointF a, PointF b, float d, float w)
        {
            Seg s; s.A = a; s.B = b; s.D = d; s.W = w; segs.Add(s);
        }

        // Walks a wobbly crack outward; returns the points so rings can attach to it.
        List<PointF> Walk(PointF start, float angle, float len, float dist0, float wMax, float step, int depth)
        {
            List<PointF> pts = new List<PointF>();
            pts.Add(start);
            PointF p = start;
            float a = angle, dist = 0;
            while (dist < len)
            {
                a += Rand(-0.16f, 0.16f) + (angle - a) * 0.15f;
                PointF np = new PointF(p.X + step * (float)Math.Cos(a), p.Y + step * (float)Math.Sin(a));
                float taper = 1 - dist / len;
                AddSeg(p, np, (dist0 + dist) / R, scale * (0.55f + wMax * taper));
                if (depth < 2 && dist > len * 0.18f && rnd.NextDouble() < (depth == 0 ? 0.10 : 0.06))
                {
                    float side = rnd.Next(2) == 0 ? -1 : 1;
                    Walk(np, a + side * Rand(0.3f, 0.85f), (len - dist) * Rand(0.3f, 0.75f), dist0 + dist, wMax * 0.55f * taper + 0.2f, step, depth + 1);
                }
                p = np; dist += step; pts.Add(p);
            }
            return pts;
        }

        static PointF PointAt(List<PointF> pts, float step, float d)
        {
            float f = d / step; int i = (int)f;
            if (i >= pts.Count - 1) return pts[pts.Count - 1];
            float u = f - i;
            return new PointF(pts[i].X + (pts[i + 1].X - pts[i].X) * u, pts[i].Y + (pts[i + 1].Y - pts[i].Y) * u);
        }

        void Build()
        {
            PointF center = new PointF(c, c);
            float step = 13 * scale;
            int n = 11 + rnd.Next(7);
            List<PointF>[] rad = new List<PointF>[n];
            float[] len = new float[n];
            double off = rnd.NextDouble() * Math.PI * 2;

            // long radial cracks
            for (int i = 0; i < n; i++)
            {
                float ang = (float)(off + (i + Rand(-0.35f, 0.35f)) * Math.PI * 2 / n);
                float l = R * Rand(0.42f, 1.0f);
                rad[i] = Walk(center, ang, l, 0, 1.9f, step, 0);
                len[i] = (rad[i].Count - 1) * step;
            }

            // spider-web rings joining neighbouring radials, plus tinted facets between them
            float rr = R * Rand(0.06f, 0.09f), prevRR = 0;
            while (rr < R * 0.82f)
            {
                double prob = 0.92 - 0.6 * (rr / R);
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    if (len[i] < rr * 1.05f || len[j] < rr * 1.05f || rnd.NextDouble() > prob) continue;
                    PointF a = PointAt(rad[i], step, rr * Rand(0.86f, 1.14f));
                    PointF b = PointAt(rad[j], step, rr * Rand(0.86f, 1.14f));
                    float pull = Rand(0.02f, 0.10f);
                    PointF mid = new PointF((a.X + b.X) / 2 + (c - (a.X + b.X) / 2) * pull + Rand(-2, 2) * scale,
                                            (a.Y + b.Y) / 2 + (c - (a.Y + b.Y) / 2) * pull + Rand(-2, 2) * scale);
                    float w = scale * Rand(0.55f, 1.25f);
                    AddSeg(a, mid, rr / R, w);
                    AddSeg(mid, b, rr / R, w);
                    if (rnd.NextDouble() < 0.42)
                    {
                        Facet f;
                        f.P = new PointF[] { PointAt(rad[i], step, prevRR), a, mid, b, PointAt(rad[j], step, prevRR) };
                        f.D = rr / R;
                        f.C = rnd.Next(4) == 0 ? Color.FromArgb(10 + rnd.Next(28), 0, 10, 20)
                                               : Color.FromArgb(8 + rnd.Next(30), 225, 240, 255);
                        facets.Add(f);
                    }
                }
                prevRR = rr;
                rr *= Rand(1.4f, 1.8f);
            }

            // crushed glass at the point of impact
            float crush = R * 0.075f;
            for (int i = 0; i < 46; i++)
            {
                double a1 = rnd.NextDouble() * Math.PI * 2, a2 = rnd.NextDouble() * Math.PI * 2;
                float r1 = crush * (float)Math.Sqrt(rnd.NextDouble()), r2 = Math.Min(crush * 1.2f, r1 + Rand(3, 12) * scale);
                AddSeg(new PointF(c + r1 * (float)Math.Cos(a1), c + r1 * (float)Math.Sin(a1)),
                       new PointF(c + r2 * (float)Math.Cos(a1 + (a2 - Math.PI) * 0.25), c + r2 * (float)Math.Sin(a1 + (a2 - Math.PI) * 0.25)),
                       0, scale * Rand(0.5f, 1.3f));
            }

            // flying shards
            int count = 26 + rnd.Next(16);
            for (int i = 0; i < count; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2;
                float sp = Rand(120, 620) * scale;
                Shard s = new Shard();
                s.X = c + Rand(-6, 6) * scale; s.Y = c + Rand(-6, 6) * scale;
                s.VX = sp * (float)Math.Cos(a); s.VY = sp * (float)Math.Sin(a) - 160 * scale;
                s.Rot = Rand(0, 6.28f); s.Spin = Rand(-14, 14); s.Size = Rand(2.5f, 9f) * scale; s.Tint = rnd.Next(3);
                shards.Add(s);
            }
        }

        void Render(Graphics g, float reveal)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            foreach (Facet f in facets)
                if (f.D <= reveal) using (SolidBrush b = new SolidBrush(f.C)) g.FillPolygon(b, f.P);

            // frosted bloom of pulverised glass at the centre
            float gr = R * 0.13f * Math.Min(1f, reveal * 5f);
            if (gr > 1)
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(c - gr, c - gr, gr * 2, gr * 2);
                    using (PathGradientBrush pg = new PathGradientBrush(path))
                    {
                        pg.CenterColor = Color.FromArgb(150, 240, 248, 255);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, 240, 248, 255) };
                        g.FillPath(pg, path);
                    }
                }

            float so = 1.3f * scale;
            using (Pen glow = new Pen(Color.FromArgb(26, 220, 235, 255)))
            using (Pen shadow = new Pen(Color.FromArgb(135, 0, 0, 0)))
            using (Pen line = new Pen(Color.FromArgb(240, 238, 246, 255)))
            {
                glow.StartCap = glow.EndCap = shadow.StartCap = shadow.EndCap = line.StartCap = line.EndCap = LineCap.Round;
                foreach (Seg s in segs) { if (s.D > reveal) continue; glow.Width = s.W + 3.2f * scale; g.DrawLine(glow, s.A, s.B); }
                foreach (Seg s in segs) { if (s.D > reveal) continue; shadow.Width = s.W + 0.5f * scale; g.DrawLine(shadow, s.A.X + so, s.A.Y + so, s.B.X + so, s.B.Y + so); }
                foreach (Seg s in segs) { if (s.D > reveal) continue; line.Width = s.W; g.DrawLine(line, s.A, s.B); }
            }

            // the hole itself
            float hr = 3.2f * scale;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 10, 14, 18))) g.FillEllipse(b, c - hr, c - hr, hr * 2, hr * 2);
        }

        void DrawShards(Graphics g, double t)
        {
            float life = (float)(1 - t / ShardTime);
            if (life <= 0) return;
            int alpha = (int)(230 * Math.Min(1f, life * 1.6f));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b0 = new SolidBrush(Color.FromArgb(alpha, 240, 248, 255)))
            using (SolidBrush b1 = new SolidBrush(Color.FromArgb(alpha, 170, 205, 230)))
            using (SolidBrush b2 = new SolidBrush(Color.FromArgb(alpha * 2 / 3, 40, 55, 70)))
            {
                PointF[] tri = new PointF[3];
                foreach (Shard s in shards)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        double a = s.Rot + k * 2.2;
                        float rad = s.Size * (k == 0 ? 1.3f : 0.8f);
                        tri[k] = new PointF(s.X + rad * (float)Math.Cos(a), s.Y + rad * (float)Math.Sin(a));
                    }
                    g.FillPolygon(s.Tint == 0 ? b0 : s.Tint == 1 ? b1 : b2, tri);
                }
            }
        }

        void Push(Bitmap bmp, byte alpha)
        {
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr old = Native.SelectObject(memDc, hBmp);
            Native.SIZE sz = new Native.SIZE(size, size);
            Native.POINT src = new Native.POINT(0, 0), dst = new Native.POINT(origin.X, origin.Y);
            Native.BLENDFUNCTION bf = new Native.BLENDFUNCTION();
            bf.SourceConstantAlpha = alpha; bf.AlphaFormat = 1;
            Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref sz, memDc, ref src, 0, ref bf, Native.ULW_ALPHA);
            Native.SelectObject(memDc, old);
            Native.DeleteObject(hBmp);
            Native.DeleteDC(memDc);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }

        void EnsureStill()
        {
            if (still != null) return;
            still = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(still)) Render(g, 1f);
        }

        /// <summary>Advances the animation. Returns false once the crack has faded away.</summary>
        public bool Update()
        {
            double t = clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.05, t - lastT);
            lastT = t;

            if (t < ShardTime)
            {
                double u = Math.Min(1.0, t / GrowTime);
                float reveal = (float)(1 - (1 - u) * (1 - u));
                if (u >= 1) EnsureStill();
                foreach (Shard s in shards)
                {
                    s.VY += 1900 * scale * dt;
                    s.X += s.VX * dt; s.Y += s.VY * dt; s.Rot += s.Spin * dt;
                }
                using (Graphics g = Graphics.FromImage(frame))
                {
                    g.Clear(Color.Transparent);
                    if (still != null) g.DrawImageUnscaled(still, 0, 0); else Render(g, reveal);
                    DrawShards(g, t);
                }
                Push(frame, 255);
                return true;
            }

            if (!stillShown)
            {
                EnsureStill();
                Push(still, 255);
                stillShown = true;
                frame.Dispose(); frame = null;
            }
            if (fadeStart < 0)
            {
                if (!Persist && t > ShardTime + HoldSeconds) fadeStart = t;
                return true;
            }
            double a = 1 - (t - fadeStart) / FadeTime;
            if (a <= 0) return false;
            Push(still, (byte)(a * 255));
            return true;
        }

        public Bitmap Snapshot() { EnsureStill(); return still; }

        public void Destroy()
        {
            if (Handle != IntPtr.Zero) DestroyHandle();
            if (frame != null) { frame.Dispose(); frame = null; }
            if (still != null) { still.Dispose(); still = null; }
        }
    }

    // ---------------------------------------------------------------- tray app

    sealed class TrayApp : ApplicationContext
    {
        const int MaxCracks = 40;
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;
        readonly ToolStripMenuItem miEnabled, miSound, miPersist;
        readonly System.Windows.Forms.Timer timer;
        readonly List<Crack> cracks = new List<Crack>();
        readonly Control marshal = new Control();
        readonly Random rnd = new Random();
        readonly SmashSound sound = new SmashSound();
        readonly Native.HookProc hookProc;   // kept alive for the lifetime of the hook
        readonly float scale;
        IntPtr hook;

        public TrayApp(float scale)
        {
            this.scale = scale;
            marshal.CreateControl();
            IntPtr force = marshal.Handle;

            menu = new ContextMenuStrip();
            miEnabled = new ToolStripMenuItem("Break glass on click"); miEnabled.Checked = true; miEnabled.CheckOnClick = true;
            miEnabled.CheckedChanged += delegate { UpdateTip(); };
            miSound = new ToolStripMenuItem("Sound"); miSound.Checked = true; miSound.CheckOnClick = true;
            miPersist = new ToolStripMenuItem("Keep cracks until cleared"); miPersist.CheckOnClick = true;
            miPersist.CheckedChanged += delegate { Crack.Persist = miPersist.Checked; };
            ToolStripMenuItem miClear = new ToolStripMenuItem("Repair screen (clear cracks)");
            miClear.Click += delegate { ClearAll(); };
            ToolStripMenuItem miExit = new ToolStripMenuItem("Exit");
            miExit.Click += delegate { Quit(); };
            menu.Items.AddRange(new ToolStripItem[] { miEnabled, miSound, miPersist, new ToolStripSeparator(), miClear, new ToolStripSeparator(), miExit });

            tray = new NotifyIcon();
            tray.Icon = MakeIcon();
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.DoubleClick += delegate { miEnabled.Checked = !miEnabled.Checked; };
            UpdateTip();
            tray.ShowBalloonTip(4000, "Glass Breaker is armed", "Click anywhere to smash the screen. Right-click this tray icon for options or to exit.", ToolTipIcon.Info);

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 15;
            timer.Tick += delegate { Tick(); };

            hookProc = HookCallback;
            hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, hookProc, Native.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero)
                MessageBox.Show("Could not install the mouse hook (error " + Marshal.GetLastWin32Error() + ").", "Glass Breaker");
        }

        void UpdateTip() { tray.Text = miEnabled.Checked ? "Glass Breaker - armed (double-click to pause)" : "Glass Breaker - paused (double-click to arm)"; }

        IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (int)wParam == Native.WM_LBUTTONDOWN && miEnabled.Checked && !menu.Visible)
            {
                Native.MSLLHOOKSTRUCT info = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                Point p = new Point(info.pt.x, info.pt.y);
                // do the work after the hook returns so the real click is never delayed
                marshal.BeginInvoke((MethodInvoker)delegate { Smash(p); });
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        void Smash(Point p)
        {
            if (miSound.Checked) sound.Play();
            while (cracks.Count >= MaxCracks) { cracks[0].Destroy(); cracks.RemoveAt(0); }
            cracks.Add(new Crack(p, rnd, scale));
            timer.Start();
        }

        void Tick()
        {
            for (int i = cracks.Count - 1; i >= 0; i--)
                if (!cracks[i].Update()) { cracks[i].Destroy(); cracks.RemoveAt(i); }
            if (cracks.Count == 0) timer.Stop();
        }

        void ClearAll()
        {
            foreach (Crack k in cracks) k.Destroy();
            cracks.Clear();
        }

        void Quit()
        {
            if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            timer.Stop();
            ClearAll();
            tray.Visible = false;
            tray.Dispose();
            sound.Dispose();
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

    // ---------------------------------------------------------------- entry point

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // physical pixels everywhere, so cracks land exactly under the cursor on scaled displays
            try { if (!Native.SetProcessDpiAwarenessContext(new IntPtr(-4))) Native.SetProcessDPIAware(); }
            catch (EntryPointNotFoundException) { Native.SetProcessDPIAware(); }
            Application.EnableVisualStyles();

            float scale;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = Math.Max(1f, g.DpiX / 96f);

            if (args.Length > 0 && args[0] == "--selftest") { SelfTest(scale, args.Length > 1 ? args[1] : "."); return; }

            bool first;
            using (Mutex m = new Mutex(true, "GlassBreaker.SingleInstance", out first))
            {
                if (!first)
                {
                    MessageBox.Show("Glass Breaker is already running - look for its icon in the system tray.", "Glass Breaker");
                    return;
                }
                Application.Run(new TrayApp(scale));
            }
        }

        // Renders one crack over a private backdrop in the middle of the primary screen,
        // grabs it back off the screen, and checks the sound files open. Used to verify builds.
        static void SelfTest(float scale, string outDir)
        {
            StringBuilder log = new StringBuilder();
            try
            {
                using (SmashSound s = new SmashSound())
                    for (int i = 0; i < s.OpenResults.Length; i++) log.AppendLine("sound " + i + ": open rc=" + s.OpenResults[i] + " " + s.Status(i));

                Rectangle scr = Screen.PrimaryScreen.Bounds;
                int side = (int)(820 * scale);
                Rectangle area = new Rectangle(scr.X + (scr.Width - side) / 2, scr.Y + (scr.Height - side) / 2, side, side);
                Form back = new Form();
                back.FormBorderStyle = FormBorderStyle.None; back.StartPosition = FormStartPosition.Manual;
                back.ShowInTaskbar = false; back.TopMost = true; back.Bounds = area;
                back.Paint += delegate(object o, PaintEventArgs e)
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(back.ClientRectangle, Color.FromArgb(20, 40, 70), Color.FromArgb(245, 245, 245), 0f))
                        e.Graphics.FillRectangle(b, back.ClientRectangle);
                };
                back.Show();
                Application.DoEvents();

                Crack k = new Crack(new Point(area.X + side / 2, area.Y + side / 2), new Random(7), scale);
                Stopwatch sw = Stopwatch.StartNew();
                bool mid = false;
                while (sw.ElapsedMilliseconds < 1300)
                {
                    k.Update();
                    Application.DoEvents();
                    if (!mid && sw.ElapsedMilliseconds > 260) { Grab(area, Path.Combine(outDir, "selftest_mid.png")); mid = true; }
                    Thread.Sleep(15);
                }
                Grab(area, Path.Combine(outDir, "selftest_final.png"));
                using (Bitmap off = new Bitmap(side, side))
                {
                    using (Graphics g = Graphics.FromImage(off))
                    {
                        using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, side, side), Color.FromArgb(20, 40, 70), Color.FromArgb(245, 245, 245), 0f))
                            g.FillRectangle(b, 0, 0, side, side);
                        Bitmap snap = k.Snapshot();
                        g.DrawImageUnscaled(snap, (side - snap.Width) / 2, (side - snap.Height) / 2);
                    }
                    off.Save(Path.Combine(outDir, "selftest_offline.png"), ImageFormat.Png);
                }
                log.AppendLine("interactive=" + SystemInformation.UserInteractive + " session=" + Process.GetCurrentProcess().SessionId + " backVisible=" + back.Visible);
                k.Destroy();
                back.Close();
                log.AppendLine("render ok, scale=" + scale + ", area=" + area);
            }
            catch (Exception ex) { log.AppendLine("FAILED: " + ex); }
            File.WriteAllText(Path.Combine(outDir, "selftest.log"), log.ToString());
        }

        static void Grab(Rectangle area, string path)
        {
            using (Bitmap bmp = new Bitmap(area.Width, area.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    IntPtr screen = Native.GetDC(IntPtr.Zero), dst = g.GetHdc();
                    Native.BitBlt(dst, 0, 0, area.Width, area.Height, screen, area.X, area.Y, 0x40CC0020); // SRCCOPY | CAPTUREBLT
                    g.ReleaseHdc(dst);
                    Native.ReleaseDC(IntPtr.Zero, screen);
                }
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
