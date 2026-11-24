using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace GlassBreaker
{
    /// <summary>Ants and flies that wander the screen, each in its own tiny window. Click one to squash it.</summary>
    sealed class Bugs : IAmbient
    {
        class Bug
        {
            public LayerWin Win = new LayerWin();
            public float X, Y, Head, Target, Speed, Phase, Turn, Pause, StateT;
            public int Kind, Seed;      // kind 0 = ant, 1 = fly
            public bool Splat, SplatDrawn;
        }

        readonly float s;
        readonly Random rnd;
        readonly Sounds snd;
        readonly List<Bug> bugs = new List<Bug>();
        readonly Bitmap bmp;
        readonly int size;
        Rectangle vs;

        public Bugs(float scale, Random rnd, Sounds snd)
        {
            s = scale; this.rnd = rnd; this.snd = snd;
            size = (int)(48 * s);
            bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            vs = SystemInformation.VirtualScreen;
            SetMode(null);
        }

        float Rand(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        void Respawn(Bug b)
        {
            b.Kind = rnd.Next(3) == 0 ? 1 : 0;
            b.Speed = (b.Kind == 0 ? Rand(55, 95) : Rand(150, 230)) * s;
            b.Splat = b.SplatDrawn = false; b.StateT = 0; b.Pause = 0; b.Turn = 0; b.Seed = rnd.Next();
            switch (rnd.Next(4))
            {
                case 0: b.X = vs.Left - 20; b.Y = vs.Top + Rand(0, 1) * vs.Height; break;
                case 1: b.X = vs.Right + 20; b.Y = vs.Top + Rand(0, 1) * vs.Height; break;
                case 2: b.Y = vs.Top - 20; b.X = vs.Left + Rand(0, 1) * vs.Width; break;
                default: b.Y = vs.Bottom + 20; b.X = vs.Left + Rand(0, 1) * vs.Width; break;
            }
            b.Head = b.Target = (float)Math.Atan2(vs.Top + vs.Height / 2f - b.Y, vs.Left + vs.Width / 2f - b.X);
        }

        /// <summary>Called whenever settings change: grows or shrinks the swarm to the chosen size.</summary>
        public void SetMode(string mode)
        {
            int want = Math.Max(1, Math.Min(25, Settings.Int("bugcount", 7)));
            while (bugs.Count < want)
            {
                Bug b = new Bug();
                b.Win.Create(0, 0, size, size);
                Respawn(b);
                // start scattered across the screen rather than all at the edges
                b.X = vs.Left + Rand(0.1f, 0.9f) * vs.Width; b.Y = vs.Top + Rand(0.1f, 0.9f) * vs.Height;
                bugs.Add(b);
            }
            while (bugs.Count > want)
            {
                bugs[bugs.Count - 1].Win.Destroy();
                bugs.RemoveAt(bugs.Count - 1);
            }
        }

        public void Tick(float dt)
        {
            foreach (Bug b in bugs)
            {
                if (b.Splat)
                {
                    b.StateT += dt;
                    if (b.StateT > 9) { Respawn(b); continue; }
                    if (b.StateT > 6) { b.Win.Hide(); continue; }
                    if (b.SplatDrawn && b.StateT < 5) continue;
                    using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); Art.Setup(g); DrawSplat(g, size / 2f, s, b.Seed); }
                    b.Win.Push(bmp, (byte)(255 * Art.Clamp01(6 - b.StateT)));
                    b.SplatDrawn = true;
                    continue;
                }

                b.Turn -= dt;
                if (b.Turn <= 0)
                {
                    b.Target = b.Head + Rand(-1.2f, 1.2f);
                    b.Turn = Rand(0.4f, 1.8f);
                    if (rnd.Next(4) == 0) b.Pause = Rand(0.3f, 1.5f) * (b.Kind == 1 ? 1.6f : 1f);
                }
                if (b.X < vs.Left + 30 || b.X > vs.Right - 30 || b.Y < vs.Top + 30 || b.Y > vs.Bottom - 30)
                    b.Target = (float)Math.Atan2(vs.Top + vs.Height / 2f - b.Y, vs.Left + vs.Width / 2f - b.X);
                float diff = (float)Math.IEEERemainder(b.Target - b.Head, Math.PI * 2);
                b.Head += diff * Math.Min(1f, dt * 5);
                if (b.Pause > 0) b.Pause -= dt;
                else
                {
                    b.X += (float)Math.Cos(b.Head) * b.Speed * dt; b.Y += (float)Math.Sin(b.Head) * b.Speed * dt;
                    b.Phase += dt * 22;
                }
                using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); Art.Setup(g); DrawBug(g, size / 2f, s, b.Kind, b.Head, b.Phase); }
                b.Win.Origin = new Point((int)(b.X - size / 2f), (int)(b.Y - size / 2f));
                b.Win.Push(bmp, 255);
            }
        }

        public bool TryClick(Point p)
        {
            foreach (Bug b in bugs)
            {
                if (b.Splat) continue;
                float dx = p.X - b.X, dy = p.Y - b.Y;
                if (dx * dx + dy * dy > 24 * s * 24 * s) continue;
                b.Splat = true; b.StateT = 0;
                snd.Play("squish");
                return true;
            }
            return false;
        }

        public void Repair()
        {
            foreach (Bug b in bugs) if (b.Splat) { b.Win.Hide(); b.StateT = 6.5f; }
        }

        public void Dispose()
        {
            foreach (Bug b in bugs) b.Win.Destroy();
            bmp.Dispose();
        }

        static void Oval(Graphics g, Brush b, float x, float y, float rx, float ry) { g.FillEllipse(b, x - rx, y - ry, rx * 2, ry * 2); }

        /// <summary>Draws a bug centred at (c,c), facing along its heading.</summary>
        public static void DrawBug(Graphics g, float c, float k, int kind, float heading, float phase)
        {
            GraphicsState st = g.Save();
            g.TranslateTransform(c, c);
            g.RotateTransform(heading * 180f / (float)Math.PI);
            using (Pen leg = new Pen(Color.FromArgb(255, 30, 20, 15), 1.2f * k))
            using (SolidBrush halo = new SolidBrush(Color.FromArgb(80, 235, 225, 205)))
            using (SolidBrush body = new SolidBrush(kind == 0 ? Color.FromArgb(255, 40, 25, 18) : Color.FromArgb(255, 42, 46, 56)))
            using (SolidBrush shine = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
            {
                for (int i = -1; i <= 1; i++)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float x0 = i * 3.2f * k, swing = (float)Math.Sin(phase + i * 2.1f + (side > 0 ? 0 : Math.PI)) * 2.5f * k;
                        float reach = kind == 0 ? 1f : 0.7f;
                        g.DrawLines(leg, new PointF[] { new PointF(x0, side * 2 * k), new PointF(x0 + i * 2 * k + swing, side * 8 * k * reach), new PointF(x0 + i * 3 * k + swing, side * 11 * k * reach) });
                    }
                if (kind == 0)
                {
                    g.DrawLine(leg, 8 * k, 1.5f * k, 13 * k, 5 * k);
                    g.DrawLine(leg, 8 * k, -1.5f * k, 13 * k, -5 * k);
                    Oval(g, halo, -8 * k, 0, 7.6f * k, 5.6f * k); Oval(g, halo, 6.5f * k, 0, 4.5f * k, 4.2f * k);
                    Oval(g, body, -8 * k, 0, 6.5f * k, 4.5f * k);
                    Oval(g, body, 0, 0, 4 * k, 3 * k);
                    Oval(g, body, 6.5f * k, 0, 3.5f * k, 3.2f * k);
                    Oval(g, shine, -9 * k, -1.5f * k, 3 * k, 1.5f * k);
                }
                else
                {
                    float flutter = 3.2f * k * (0.75f + 0.25f * (float)Math.Sin(phase * 3));
                    Oval(g, halo, -1 * k, 0, 7.2f * k, 4.8f * k);
                    Oval(g, body, -1 * k, 0, 6 * k, 3.6f * k);
                    Oval(g, body, 6 * k, 0, 3 * k, 3 * k);
                    using (SolidBrush eye = new SolidBrush(Color.FromArgb(255, 160, 35, 30))) { Oval(g, eye, 7.2f * k, 1.8f * k, 1.3f * k, 1.3f * k); Oval(g, eye, 7.2f * k, -1.8f * k, 1.3f * k, 1.3f * k); }
                    using (SolidBrush wing = new SolidBrush(Color.FromArgb(150, 210, 225, 240)))
                    using (Pen edge = new Pen(Color.FromArgb(170, 90, 100, 115), 0.8f * k))
                        for (int side = -1; side <= 1; side += 2)
                        {
                            GraphicsState ws = g.Save();
                            g.TranslateTransform(-1 * k, side * 2 * k);
                            g.RotateTransform(side * 28);
                            g.FillEllipse(wing, -9 * k, side > 0 ? 0 : -flutter * 2, 11 * k, flutter * 2);
                            g.DrawEllipse(edge, -9 * k, side > 0 ? 0 : -flutter * 2, 11 * k, flutter * 2);
                            g.Restore(ws);
                        }
                }
            }
            g.Restore(st);
        }

        public static void DrawSplat(Graphics g, float c, float k, int seed)
        {
            Random r = new Random(seed);
            using (SolidBrush goo = new SolidBrush(Color.FromArgb(225, 172, 190, 60)))
            using (SolidBrush dark = new SolidBrush(Color.FromArgb(235, 40, 28, 20)))
            using (Pen leg = new Pen(Color.FromArgb(235, 30, 20, 15), 1.2f * k))
            {
                g.FillClosedCurve(goo, Art.Blob(r, c, c, 9 * k, 0.35f, 10), FillMode.Winding, 0.5f);
                for (int i = 0; i < 9; i++)
                {
                    double a = r.NextDouble() * Math.PI * 2; float d = (10 + (float)r.NextDouble() * 9) * k, rr = (1 + (float)r.NextDouble() * 2) * k;
                    Oval(g, goo, c + d * (float)Math.Cos(a), c + d * (float)Math.Sin(a), rr, rr);
                }
                for (int i = 0; i < 6; i++)
                {
                    double a = i * Math.PI / 3 + r.NextDouble() * 0.6; float d = (9 + (float)r.NextDouble() * 5) * k;
                    g.DrawLine(leg, c + 3 * k * (float)Math.Cos(a), c + 3 * k * (float)Math.Sin(a), c + d * (float)Math.Cos(a), c + d * (float)Math.Sin(a));
                }
                Oval(g, dark, c - 2 * k, c + k, 4 * k, 3 * k);
                Oval(g, dark, c + 3 * k, c - k, 2.5f * k, 2.2f * k);
            }
        }

        /// <summary>Self-test: an ant, a fly and a splat, enlarged.</summary>
        public static void Sample(Graphics g, float cell)
        {
            float k = 4f, c = 24 * k;
            g.TranslateTransform(20, 60); DrawBug(g, c, k, 0, 0.3f, 1.0f);
            g.TranslateTransform(190, 0); DrawBug(g, c, k, 1, -0.5f, 2.0f);
            g.TranslateTransform(-100, 170); DrawSplat(g, c, k, 12345);
        }
    }
}
