using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace GlassBreaker
{
    /// <summary>A laser hit: a flash, a glowing ring that cools, rising smoke, and a scorch mark left behind.</summary>
    sealed class BurnDecal : Decal
    {
        struct Puff { public float Off, Rise, Size, Delay, Phase; }
        struct Ember { public float Ang, Speed, Life; }

        readonly PointF[] scorch;
        readonly PointF[] specks;
        readonly Puff[] puffs;
        readonly Ember[] embers;

        public BurnDecal(Point p, Random rnd, float scale) : base(rnd, scale)
        {
            w = (int)(240 * s); h = (int)(340 * s);
            cx = w / 2f; cy = h - 120 * s;
            AnimTime = 2.8;

            scorch = Art.Blob(rnd, cx, cy, Rand(50, 64) * s, 0.1f, 18);
            specks = new PointF[16];
            for (int i = 0; i < specks.Length; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2; float d = Rand(16, 60) * s;
                specks[i] = new PointF(cx + d * (float)Math.Cos(a), cy + d * (float)Math.Sin(a));
            }
            puffs = new Puff[7];
            for (int i = 0; i < puffs.Length; i++)
            {
                puffs[i].Off = Rand(-12, 12) * s; puffs[i].Rise = Rand(110, 180) * s; puffs[i].Size = Rand(16, 30) * s;
                puffs[i].Delay = Rand(0.05f, 1.0f); puffs[i].Phase = Rand(0, 6.28f);
            }
            embers = new Ember[10];
            for (int i = 0; i < embers.Length; i++) { embers[i].Ang = Rand(0, 6.28f); embers[i].Speed = Rand(30, 90) * s; embers[i].Life = Rand(0.4f, 1.1f); }
            Start(p);
        }

        protected override void RenderStill(Graphics g, double t)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddClosedCurve(scorch, 0.5f);
                using (PathGradientBrush pg = new PathGradientBrush(path))
                {
                    pg.CenterPoint = new PointF(cx, cy);
                    pg.CenterColor = Color.FromArgb(240, 8, 6, 5);
                    pg.SurroundColors = new Color[] { Color.FromArgb(0, 45, 28, 15) };
                    pg.FocusScales = new PointF(0.3f, 0.3f);
                    g.FillPath(pg, path);
                }
            }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 20, 14, 10)))
                foreach (PointF p in specks) g.FillEllipse(b, p.X - 1.4f * s, p.Y - 1.4f * s, 2.8f * s, 2.8f * s);
            using (Pen ring = new Pen(Color.FromArgb(130, 100, 65, 40), 2 * s)) g.DrawEllipse(ring, cx - 11 * s, cy - 11 * s, 22 * s, 22 * s);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 0, 0, 0))) g.FillEllipse(b, cx - 7 * s, cy - 7 * s, 14 * s, 14 * s);
        }

        protected override void RenderOverlay(Graphics g, double t, float dt)
        {
            float k = (float)(1 - t / AnimTime);
            foreach (Puff p in puffs)
            {
                float u = (float)((t - p.Delay) / 1.8);
                if (u <= 0 || u >= 1) continue;
                float a = 60 * (1 - u) * Math.Min(1f, u * 6);
                Art.Glow(g, cx + p.Off + (float)Math.Sin(u * 4 + p.Phase) * 8 * s, cy - 18 * s - u * p.Rise, p.Size * (0.5f + u), Color.FromArgb((int)a, 125, 125, 125));
            }
            float flick = 0.8f + 0.2f * (float)Math.Sin(t * 40);
            Art.Glow(g, cx, cy, 32 * s, Color.FromArgb((int)(210 * Math.Pow(k, 1.3) * flick), 255, 140, 30));
            Art.Glow(g, cx, cy, 14 * s, Color.FromArgb((int)(245 * k * k), 255, 240, 150));
            if (t < 0.14) Art.Glow(g, cx, cy, 75 * s, Color.FromArgb((int)(235 * (1 - t / 0.14)), 255, 250, 200));
            foreach (Ember e in embers)
            {
                float u = (float)(t / e.Life);
                if (u >= 1) continue;
                float d = 8 * s + e.Speed * u, r = 1.6f * s;
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * (1 - u)), 255, 170, 40)))
                    g.FillEllipse(b, cx + d * (float)Math.Cos(e.Ang) - r, cy + d * (float)Math.Sin(e.Ang) - 20 * s * u - r, r * 2, r * 2);
            }
        }
    }
}
