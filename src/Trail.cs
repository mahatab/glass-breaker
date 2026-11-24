using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace WindowsTrayToys
{
    /// <summary>Sparks, bubbles or paw prints that follow the cursor, drawn in one window that travels with it.</summary>
    sealed class Trail : IAmbient
    {
        class P { public float X, Y, VX, VY, Age, Life, Size, Rot, Ph; }

        readonly float s;
        readonly Random rnd;
        readonly List<P> ps = new List<P>();
        readonly LayerWin win;
        readonly Bitmap bmp;
        readonly int size;
        string mode = "off";
        Point last;
        bool have, leftFoot;
        float acc;

        public Trail(float scale, Random rnd, bool onScreen)
        {
            s = scale; this.rnd = rnd;
            size = (int)(680 * s);
            if (!onScreen) return;
            bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            win = new LayerWin();
            win.Create(0, 0, size, size);
        }

        float Rand(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        public void SetMode(string m) { if (m != mode) { mode = m; ps.Clear(); } }
        public bool TryClick(Point p) { return false; }
        public void Repair() { ps.Clear(); }

        void Emit(float x, float y, float ang)
        {
            P p = new P();
            p.X = x; p.Y = y;
            if (mode == "sparks")
            {
                p.VX = Rand(-70, 70) * s; p.VY = Rand(-110, 30) * s; p.Life = Rand(0.3f, 0.6f); p.Size = Rand(1.2f, 2.4f) * s;
            }
            else if (mode == "bubbles")
            {
                p.VX = Rand(-12, 12) * s; p.VY = -Rand(25, 60) * s; p.Life = Rand(1.0f, 1.6f); p.Size = Rand(4, 11) * s; p.Ph = Rand(0, 6.28f);
            }
            else
            {
                leftFoot = !leftFoot;
                float side = leftFoot ? 8 * s : -8 * s;
                p.X += -(float)Math.Sin(ang) * side; p.Y += (float)Math.Cos(ang) * side;
                p.Rot = ang; p.Life = 2.8f; p.Size = 6.5f * s;
            }
            ps.Add(p);
        }

        /// <summary>Feeds a new cursor position and advances the particles.</summary>
        public void Feed(Point cp, float dt)
        {
            if (have)
            {
                float dx = cp.X - last.X, dy = cp.Y - last.Y, d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d > 0.5f && d < 800 * s)
                {
                    float step = mode == "sparks" ? 7 * s : mode == "bubbles" ? 20 * s : 36 * s, ang = (float)Math.Atan2(dy, dx);
                    acc += d;
                    while (acc >= step)
                    {
                        acc -= step;
                        float f = Art.Clamp01(1 - acc / d);
                        Emit(last.X + dx * f, last.Y + dy * f, ang);
                    }
                }
            }
            last = cp; have = true;

            for (int i = ps.Count - 1; i >= 0; i--)
            {
                P p = ps[i];
                p.Age += dt;
                if (p.Age >= p.Life) { ps.RemoveAt(i); continue; }
                if (mode == "sparks") p.VY += 520 * s * dt;
                else if (mode == "bubbles") p.X += (float)Math.Sin(p.Age * 5 + p.Ph) * 10 * s * dt;
                p.X += p.VX * dt; p.Y += p.VY * dt;
            }
        }

        public void Render(Graphics g, float ox, float oy)
        {
            foreach (P p in ps)
            {
                float k = 1 - p.Age / p.Life, x = p.X - ox, y = p.Y - oy;
                if (mode == "sparks")
                {
                    using (Pen pen = new Pen(Color.FromArgb((int)(255 * k), 255, (int)(150 + 105 * k), (int)(40 + 180 * k * k)), p.Size))
                    {
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, x, y, x - p.VX * 0.04f, y - p.VY * 0.04f);
                    }
                }
                else if (mode == "bubbles")
                {
                    float r = p.Size * (0.7f + 0.5f * p.Age / p.Life), a = 220 * Math.Min(1f, k * 2);
                    using (SolidBrush fill = new SolidBrush(Color.FromArgb((int)(a * 0.16f), 180, 220, 255))) g.FillEllipse(fill, x - r, y - r, r * 2, r * 2);
                    using (Pen dark = new Pen(Color.FromArgb((int)(a * 0.35f), 30, 70, 120), s)) g.DrawEllipse(dark, x - r - s, y - r - s, (r + s) * 2, (r + s) * 2);
                    using (Pen light = new Pen(Color.FromArgb((int)a, 205, 232, 255), 1.3f * s)) g.DrawEllipse(light, x - r, y - r, r * 2, r * 2);
                    using (SolidBrush shine = new SolidBrush(Color.FromArgb((int)a, 255, 255, 255))) g.FillEllipse(shine, x - r * 0.55f, y - r * 0.6f, r * 0.38f, r * 0.3f);
                }
                else
                {
                    float a = Math.Min(1f, k * 2.5f), S = p.Size;
                    GraphicsState st = g.Save();
                    g.TranslateTransform(x, y);
                    g.RotateTransform(p.Rot * 180f / (float)Math.PI);
                    for (int pass = 0; pass < 2; pass++)   // pale halo first so prints show on dark backgrounds
                    {
                        float grow = pass == 0 ? 1.5f * s : 0;
                        Color c = pass == 0 ? Color.FromArgb((int)(95 * a), 255, 255, 255) : Color.FromArgb((int)(190 * a), 45, 38, 32);
                        using (SolidBrush b = new SolidBrush(c))
                        {
                            g.FillEllipse(b, -S * 0.9f - grow, -S * 0.75f - grow, S * 1.8f + grow * 2, S * 1.5f + grow * 2);
                            float[] ty = { -0.95f, -0.33f, 0.33f, 0.95f }, tx = { 0.95f, 1.3f, 1.3f, 0.95f };
                            for (int i = 0; i < 4; i++)
                            {
                                float tr = S * 0.33f + grow;
                                g.FillEllipse(b, S * tx[i] - tr, S * ty[i] - tr, tr * 2, tr * 2);
                            }
                        }
                    }
                    g.Restore(st);
                }
            }
        }

        public void Tick(float dt)
        {
            Native.POINT cp;
            if (!Native.GetCursorPos(out cp)) return;
            bool had = ps.Count > 0;
            Feed(new Point(cp.x, cp.y), dt);
            if (ps.Count == 0) { if (had) win.Hide(); return; }
            int ox = cp.x - size / 2, oy = cp.y - size / 2;
            win.Origin = new Point(ox, oy);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                Art.Setup(g);
                Render(g, ox, oy);
            }
            win.Push(bmp, 255);
        }

        public void Dispose()
        {
            if (win != null) win.Destroy();
            if (bmp != null) bmp.Dispose();
        }

        /// <summary>Self-test: the three trail styles along a wavy path.</summary>
        public static void Sample(Graphics g, float cell)
        {
            string[] modes = { "sparks", "bubbles", "paws" };
            for (int m = 0; m < 3; m++)
            {
                Trail t = new Trail(1f, new Random(3 + m), false);
                t.SetMode(modes[m]);
                for (int i = 0; i <= 60; i++)
                    t.Feed(new Point((int)(30 + i * (cell - 60) / 60f), (int)(cell * (0.25f + 0.25f * m) + Math.Sin(i * 0.2) * 30)), m == 2 ? 0.01f : 0.012f);
                t.Render(g, 0, 0);
            }
        }
    }
}
