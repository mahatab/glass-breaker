using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GlassBreaker
{
    /// <summary>
    /// Snow that settles on the top edges of windows and the taskbar, or rain that streaks past
    /// and beads down the glass. Drawn on one colour-keyed full-screen window at about 30 fps.
    /// </summary>
    sealed class Weather : IAmbient
    {
        class Fl { public float X, Y, VX, VY, Size, Ph, Speed; }
        class Pile { public Rectangle R; public float[] H; public bool[] Vis; public float VisT; }

        readonly float s, colW;
        readonly Random rnd;
        readonly KeyedForm form;
        readonly List<Fl> fl = new List<Fl>(), beads = new List<Fl>();
        readonly Dictionary<IntPtr, Pile> piles = new Dictionary<IntPtr, Pile>();
        readonly List<float[]> splashes = new List<float[]>();   // x, y, time left
        Rectangle vs;
        string mode = "off";
        float time, acc;
        bool skip;

        public Weather(float scale, Random rnd, bool onScreen)
        {
            s = scale; this.rnd = rnd; colW = 6 * s;
            vs = SystemInformation.VirtualScreen;
            if (!onScreen) return;
            form = new KeyedForm();
            form.Painter = delegate(Graphics g, Rectangle clip) { g.Clear(KeyedForm.Key); Paint(g, vs.X, vs.Y); };
            form.Show();
        }

        float Rand(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        public void SetMode(string m)
        {
            if (m == mode) return;
            mode = m;
            fl.Clear(); beads.Clear(); piles.Clear(); splashes.Clear();
            int n = mode == "snow" ? Math.Min(400, vs.Width / 6) : Math.Min(450, vs.Width / 5);
            for (int i = 0; i < n; i++) { Fl f = new Fl(); Spawn(f, false); fl.Add(f); }
            if (mode == "rain")
                for (int i = 0; i < 28; i++)
                {
                    Fl b = new Fl();
                    b.X = vs.Left + Rand(0, 1) * vs.Width; b.Y = vs.Top + Rand(0, 1) * vs.Height; b.Speed = Rand(8, 60) * s; b.Size = Rand(3, 5) * s;
                    beads.Add(b);
                }
        }

        void Spawn(Fl f, bool atTop)
        {
            f.X = vs.Left + Rand(0, 1) * vs.Width;
            f.Y = atTop ? vs.Top - Rand(5, 40) : vs.Top + Rand(0, 1) * vs.Height;
            f.Ph = Rand(0, 6.28f);
            if (mode == "snow") { f.VX = 0; f.VY = Rand(40, 110) * s; f.Size = Rand(2, 5.5f) * s; }
            else { f.VX = -140 * s; f.VY = Rand(900, 1300) * s; f.Size = Rand(14, 24) * s; }
        }

        void Deposit(WinInfo w, float x)
        {
            Pile p;
            if (!piles.TryGetValue(w.H, out p))
            {
                p = new Pile();
                p.R = w.R;
                int cols = (int)Math.Ceiling(w.R.Width / colW);
                p.H = new float[cols]; p.Vis = new bool[cols];
                for (int i = 0; i < cols; i++) p.Vis[i] = true;
                piles[w.H] = p;
            }
            int c = (int)((x - p.R.Left) / colW);
            float cap = 11 * s;
            for (int d = -1; d <= 1; d++)
            {
                int i = c + d;
                if (i >= 0 && i < p.H.Length) p.H[i] = Math.Min(cap, p.H[i] + (d == 0 ? 1.5f : 0.7f) * s);
            }
        }

        public void Step(float dt)
        {
            time += dt;
            List<WinInfo> wins = WindowList.Get();
            bool snow = mode == "snow";

            foreach (Fl f in fl)
            {
                float y0 = f.Y;
                f.Y += f.VY * dt;
                f.X += snow ? ((float)Math.Sin(time * 1.3f + f.Ph) * 22 * s + 8 * s) * dt : f.VX * dt;
                bool landed = false;
                foreach (WinInfo w in wins)
                {
                    if (f.X <= w.R.Left + 6 || f.X >= w.R.Right - 6 || y0 >= w.R.Top || f.Y < w.R.Top || w.R.Top <= vs.Top + 6) continue;
                    if (!WindowList.TopVisibleAt(w, (int)f.X)) continue;
                    if (snow) Deposit(w, f.X); else splashes.Add(new float[] { f.X, w.R.Top, 0.14f });
                    landed = true;
                    break;
                }
                if (landed || f.Y > vs.Bottom + 10 || f.X < vs.Left - 30 || f.X > vs.Right + 30) Spawn(f, true);
            }

            foreach (Fl b in beads)   // drops clinging to the glass: stick, then suddenly run
            {
                if (rnd.NextDouble() < dt * 1.5) b.Speed = rnd.Next(4) == 0 ? Rand(250, 420) * s : Rand(8, 60) * s;
                b.VY += (b.Speed - b.VY) * Math.Min(1f, dt * 6);
                b.Y += b.VY * dt;
                b.X += Rand(-1, 1) * b.VY * 0.04f * dt;
                if (b.Y > vs.Bottom + 10) { b.Y = vs.Top - 10; b.X = vs.Left + Rand(0, 1) * vs.Width; }
            }

            for (int i = splashes.Count - 1; i >= 0; i--) { splashes[i][2] -= dt; if (splashes[i][2] <= 0) splashes.RemoveAt(i); }

            // piles follow their window, melt slowly, and hide where another window covers the edge
            List<IntPtr> gone = null;
            foreach (KeyValuePair<IntPtr, Pile> kv in piles)
            {
                WinInfo w; Pile p = kv.Value;
                if (!WindowList.Find(kv.Key, out w) || w.R.Width != p.R.Width)
                {
                    if (gone == null) gone = new List<IntPtr>();
                    gone.Add(kv.Key);
                    continue;
                }
                p.R = w.R;
                for (int i = 0; i < p.H.Length; i++) p.H[i] = Math.Max(0, p.H[i] - 0.12f * s * dt);
                p.VisT -= dt;
                if (p.VisT > 0) continue;
                p.VisT = 0.7f;
                for (int i = 0; i < p.H.Length; i++)
                    if (p.H[i] > 0.3f) p.Vis[i] = WindowList.TopVisibleAt(w, (int)(p.R.Left + (i + 0.5f) * colW));
            }
            if (gone != null) foreach (IntPtr h in gone) piles.Remove(h);
        }

        /// <summary>Draws at (screen position - offset). No anti-aliasing: the window is colour-keyed.</summary>
        public void Paint(Graphics g, float ox, float oy)
        {
            g.SmoothingMode = SmoothingMode.None;
            using (Pen edge = new Pen(Color.FromArgb(165, 190, 215), 1))
            {
                if (mode == "snow")
                {
                    foreach (Fl f in fl)
                    {
                        g.FillEllipse(Brushes.White, f.X - ox - f.Size / 2, f.Y - oy - f.Size / 2, f.Size, f.Size);
                        if (f.Size > 3.2f * s) g.DrawEllipse(edge, f.X - ox - f.Size / 2, f.Y - oy - f.Size / 2, f.Size, f.Size);
                    }
                    foreach (Pile p in piles.Values)
                        for (int i = 0; i < p.H.Length; i++)
                        {
                            if (p.H[i] < 0.5f || !p.Vis[i]) continue;
                            float x = p.R.Left + i * colW - ox, y = p.R.Top - p.H[i] - oy;
                            g.FillRectangle(Brushes.White, x, y, colW + 1, p.H[i]);
                            g.DrawLine(edge, x, y, x + colW, y);
                        }
                }
                else
                {
                    using (Pen rain = new Pen(Color.FromArgb(150, 185, 228), Math.Max(1f, s)))
                    using (Pen trail = new Pen(Color.FromArgb(120, 160, 205), Math.Max(1f, s)))
                    using (SolidBrush bead = new SolidBrush(Color.FromArgb(200, 222, 245)))
                    {
                        foreach (Fl f in fl) g.DrawLine(rain, f.X - ox, f.Y - oy, f.X - ox - f.VX * 0.018f, f.Y - oy - f.Size);
                        foreach (Fl b in beads)
                        {
                            float x = b.X - ox, y = b.Y - oy;
                            g.DrawLine(trail, x, y, x, y - 4 * s - b.VY * 0.06f);
                            g.FillEllipse(bead, x - b.Size / 2, y - b.Size * 0.7f, b.Size, b.Size * 1.4f);
                            g.DrawEllipse(trail, x - b.Size / 2, y - b.Size * 0.7f, b.Size, b.Size * 1.4f);
                        }
                        foreach (float[] sp in splashes)
                        {
                            float x = sp[0] - ox, y = sp[1] - oy, k = (0.14f - sp[2]) / 0.14f * 6 * s + 2 * s;
                            g.DrawLine(rain, x, y, x - k, y - k);
                            g.DrawLine(rain, x, y, x + k, y - k);
                        }
                    }
                }
            }
        }

        public void Tick(float dt)
        {
            acc += dt;
            skip = !skip;
            if (skip) return;
            Step(acc);
            acc = 0;
            form.Invalidate();
        }

        public bool TryClick(Point p) { return false; }
        public void Repair() { piles.Clear(); }
        public void Dispose() { if (form != null) { form.Close(); form.Dispose(); } }

        /// <summary>Self-test: snow on the left settling on a stand-in window, rain on the right.</summary>
        public static void Sample(Graphics g, float cell)
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            string[] modes = { "snow", "rain" };
            for (int m = 0; m < 2; m++)
            {
                WinInfo fake; fake.H = new IntPtr(-2 - m); fake.R = new Rectangle(vs.Left + 30, vs.Top + 250, 150, 90);
                WindowList.Extra.Clear();
                WindowList.Extra.Add(fake);
                Weather w = new Weather(1f, new Random(5 + m), false);
                w.SetMode(modes[m]);
                List<WinInfo> wins = WindowList.Get();
                if (!wins.Contains(fake)) wins.Add(fake);
                for (int i = 0; i < 900; i++) w.Step(1 / 30f);
                float half = cell / 2;
                GraphicsState st = g.Save();
                g.SetClip(new RectangleF(m * half, 0, half, cell), CombineMode.Intersect);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 80, 95))) g.FillRectangle(b, m * half + 30, 250, 150, 90);
                w.Paint(g, vs.Left - m * half, vs.Top);
                g.Restore(st);
            }
            WindowList.Extra.Clear();
        }
    }
}
