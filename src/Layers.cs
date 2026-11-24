using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace WindowsTrayToys
{
    /// <summary>A small click-through, always-on-top window with per-pixel alpha.</summary>
    class LayerWin : NativeWindow
    {
        public Point Origin;
        bool visible;

        public void Create(int x, int y, int w, int h)
        {
            CreateParams cp = new CreateParams();
            cp.Style = Native.WS_POPUP;
            cp.ExStyle = Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOPMOST | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
            cp.X = x; cp.Y = y; cp.Width = w; cp.Height = h;
            CreateHandle(cp);
            Origin = new Point(x, y);
        }

        /// <summary>Shows the bitmap at Origin (moving the window there if needed).</summary>
        public void Push(Bitmap bmp, byte alpha)
        {
            if (Handle == IntPtr.Zero) return;
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr old = Native.SelectObject(memDc, hBmp);
            Native.SIZE sz = new Native.SIZE(bmp.Width, bmp.Height);
            Native.POINT src = new Native.POINT(0, 0), dst = new Native.POINT(Origin.X, Origin.Y);
            Native.BLENDFUNCTION bf = new Native.BLENDFUNCTION();
            bf.SourceConstantAlpha = alpha; bf.AlphaFormat = 1;
            Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref sz, memDc, ref src, 0, ref bf, Native.ULW_ALPHA);
            Native.SelectObject(memDc, old);
            Native.DeleteObject(hBmp);
            Native.DeleteDC(memDc);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
            if (!visible) { Native.ShowWindow(Handle, Native.SW_SHOWNOACTIVATE); visible = true; }
        }

        public void Hide()
        {
            if (visible && Handle != IntPtr.Zero) { Native.ShowWindow(Handle, Native.SW_HIDE); visible = false; }
        }

        public virtual void Destroy()
        {
            if (Handle != IntPtr.Zero) DestroyHandle();
            visible = false;
        }
    }

    /// <summary>
    /// One mark left by a click. It animates for AnimTime seconds (redrawn every tick), then, if it
    /// has a lasting "still" image, holds that and finally fades away.
    /// </summary>
    abstract class Decal : LayerWin
    {
        public static bool Offscreen;            // self-test: render without creating windows
        public static bool Persist;
        public static double HoldSeconds = 6;
        const double FadeTime = 0.9;

        protected readonly Random rnd;
        protected readonly float s;              // display scale (1 = 96 dpi)
        protected int w, h;
        protected float cx, cy;                  // where the click sits inside the window
        protected double AnimTime = 0.3, StillAt = 0, Hold = -1;
        protected bool HasStill = true;
        public Point Center;
        public PointF Anchor { get { return new PointF(cx, cy); } }

        readonly Stopwatch clock = new Stopwatch();
        Bitmap frame, still;
        bool stillShown;
        double fadeStart = -1, lastT;

        protected Decal(Random rnd, float scale) { this.rnd = rnd; s = scale; }

        protected float Rand(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        protected void Start(Point p)
        {
            Center = p;
            frame = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            if (Offscreen) return;
            Create(p.X - (int)cx, p.Y - (int)cy, w, h);
            clock.Start();
            Update();
        }

        /// <summary>The lasting part. Called every frame until StillAt, then once more and cached.</summary>
        protected virtual void RenderStill(Graphics g, double t) { }
        /// <summary>The moving part, drawn on top until AnimTime.</summary>
        protected virtual void RenderOverlay(Graphics g, double t, float dt) { }

        void EnsureStill()
        {
            if (still != null) return;
            still = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(still)) { Art.Setup(g); RenderStill(g, 1e9); }
        }

        Bitmap Compose(double t, float dt)
        {
            if (t < AnimTime)
            {
                using (Graphics g = Graphics.FromImage(frame))
                {
                    g.Clear(Color.Transparent);
                    Art.Setup(g);
                    if (HasStill)
                    {
                        if (t >= StillAt) { EnsureStill(); g.DrawImageUnscaled(still, 0, 0); }
                        else RenderStill(g, t);
                    }
                    RenderOverlay(g, t, dt);
                    g.ResetTransform();
                }
                return frame;
            }
            if (!HasStill) return null;
            EnsureStill();
            return still;
        }

        /// <summary>Advances the animation. Returns false once the decal is gone.</summary>
        public bool Update()
        {
            double t = clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.05, t - lastT);
            lastT = t;
            if (t < AnimTime) { Push(Compose(t, dt), 255); return true; }
            if (!HasStill) return false;
            if (!stillShown)
            {
                Push(Compose(t, dt), 255);
                stillShown = true;
                frame.Dispose(); frame = null;
            }
            if (fadeStart < 0)
            {
                if (!Persist && t > AnimTime + (Hold > 0 ? Hold : HoldSeconds)) fadeStart = t;
                return true;
            }
            double a = 1 - (t - fadeStart) / FadeTime;
            if (a <= 0) return false;
            Push(still, (byte)(a * 255));
            return true;
        }

        /// <summary>Self-test: what the decal looks like t seconds after the click.</summary>
        public Bitmap Simulate(double t)
        {
            const float step = 1 / 60f;
            for (double tt = 0; tt < t; tt += step) Compose(tt, step);
            Bitmap b = Compose(t, step);
            return b == null ? new Bitmap(w, h, PixelFormat.Format32bppArgb) : new Bitmap(b);
        }

        public override void Destroy()
        {
            base.Destroy();
            if (frame != null) { frame.Dispose(); frame = null; }
            if (still != null) { still.Dispose(); still = null; }
        }
    }

    struct Seg { public PointF A, B; public float D, W; }

    /// <summary>Generates and draws wobbly, branching crack lines.</summary>
    sealed class CrackBuilder
    {
        public readonly List<Seg> Segs = new List<Seg>();
        readonly Random rnd;
        readonly float s, R;

        public CrackBuilder(Random rnd, float scale, float radius) { this.rnd = rnd; s = scale; R = radius; }

        float Rand(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        public void Add(PointF a, PointF b, float d, float w)
        {
            Seg g; g.A = a; g.B = b; g.D = d; g.W = w; Segs.Add(g);
        }

        /// <summary>Walks a crack outward; returns its points so other cracks can attach to it.</summary>
        public List<PointF> Walk(PointF start, float angle, float len, float dist0, float wMax, float step, int depth)
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
                Add(p, np, (dist0 + dist) / R, s * (0.55f + wMax * taper));
                if (depth < 2 && dist > len * 0.18f && rnd.NextDouble() < (depth == 0 ? 0.10 : 0.06))
                {
                    float side = rnd.Next(2) == 0 ? -1 : 1;
                    Walk(np, a + side * Rand(0.3f, 0.85f), (len - dist) * Rand(0.3f, 0.75f), dist0 + dist, wMax * 0.55f * taper + 0.2f, step, depth + 1);
                }
                p = np; dist += step; pts.Add(p);
            }
            return pts;
        }

        public static PointF PointAt(List<PointF> pts, float step, float d)
        {
            float f = d / step; int i = (int)f;
            if (i >= pts.Count - 1) return pts[pts.Count - 1];
            float u = f - i;
            return new PointF(pts[i].X + (pts[i + 1].X - pts[i].X) * u, pts[i].Y + (pts[i + 1].Y - pts[i].Y) * u);
        }

        /// <summary>Soft glow, then a dark offset shadow, then the bright line, so cracks read on any background.</summary>
        public void Draw(Graphics g, float reveal)
        {
            float so = 1.3f * s;
            using (Pen glow = new Pen(Color.FromArgb(26, 220, 235, 255)))
            using (Pen shadow = new Pen(Color.FromArgb(135, 0, 0, 0)))
            using (Pen line = new Pen(Color.FromArgb(240, 238, 246, 255)))
            {
                glow.StartCap = glow.EndCap = shadow.StartCap = shadow.EndCap = line.StartCap = line.EndCap = LineCap.Round;
                foreach (Seg e in Segs) { if (e.D > reveal) continue; glow.Width = e.W + 3.2f * s; g.DrawLine(glow, e.A, e.B); }
                foreach (Seg e in Segs) { if (e.D > reveal) continue; shadow.Width = e.W + 0.5f * s; g.DrawLine(shadow, e.A.X + so, e.A.Y + so, e.B.X + so, e.B.Y + so); }
                foreach (Seg e in Segs) { if (e.D > reveal) continue; line.Width = e.W; g.DrawLine(line, e.A, e.B); }
            }
        }
    }

    static class Art
    {
        public static void Setup(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static float Clamp01(double v) { return v < 0 ? 0 : v > 1 ? 1 : (float)v; }

        public static Color A(double alpha, Color c)
        {
            int a = (int)alpha; if (a < 0) a = 0; if (a > 255) a = 255;
            return Color.FromArgb(a, c.R, c.G, c.B);
        }

        /// <summary>A radial glow that fades to nothing at radius r.</summary>
        public static void Glow(Graphics g, float x, float y, float r, Color center)
        {
            if (r < 0.5f || center.A == 0) return;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(x - r, y - r, r * 2, r * 2);
                using (PathGradientBrush pg = new PathGradientBrush(path))
                {
                    pg.CenterColor = center;
                    pg.SurroundColors = new Color[] { Color.FromArgb(0, center.R, center.G, center.B) };
                    g.FillPath(pg, path);
                }
            }
        }

        /// <summary>An irregular ring of n points around a centre.</summary>
        public static PointF[] Blob(Random r, float x, float y, float rad, float jitter, int n)
        {
            PointF[] p = new PointF[n];
            double off = r.NextDouble() * Math.PI * 2;
            for (int i = 0; i < n; i++)
            {
                double a = off + i * Math.PI * 2 / n;
                float rr = rad * (1 + ((float)r.NextDouble() * 2 - 1) * jitter);
                p[i] = new PointF(x + rr * (float)Math.Cos(a), y + rr * (float)Math.Sin(a));
            }
            return p;
        }

        public static Color Hsv(double h, double sat, double v, int alpha)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * sat, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c, r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; } else if (h < 120) { r = x; g = c; } else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; } else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
            return Color.FromArgb(alpha, (int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
        }
    }
}
