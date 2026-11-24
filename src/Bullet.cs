using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace GlassBreaker
{
    /// <summary>A small bullet hole: dark puncture, frosted rim, a few short cracks.</summary>
    sealed class BulletDecal : Decal
    {
        readonly CrackBuilder cb;
        readonly PointF[] hole;
        readonly float[] dustAng, dustSpeed, dustSize;

        public BulletDecal(Point p, Random rnd, float scale) : base(rnd, scale)
        {
            float R = Rand(44, 62) * s;
            w = h = (int)(R * 2 + 16 * s);
            cx = cy = w / 2f;
            AnimTime = 0.35;

            cb = new CrackBuilder(rnd, s, R);
            int n = 6 + rnd.Next(5);
            double off = rnd.NextDouble() * Math.PI * 2;
            for (int i = 0; i < n; i++)
            {
                float ang = (float)(off + (i + Rand(-0.4f, 0.4f)) * Math.PI * 2 / n);
                cb.Walk(new PointF(cx, cy), ang, R * Rand(0.3f, 1f), 0, 1.0f, 6 * s, 1);
            }
            hole = Art.Blob(rnd, cx, cy, Rand(6, 9) * s, 0.3f, 11);

            dustAng = new float[12]; dustSpeed = new float[12]; dustSize = new float[12];
            for (int i = 0; i < 12; i++) { dustAng[i] = Rand(0, 6.28f); dustSpeed[i] = Rand(14, 40) * s; dustSize[i] = Rand(1, 2.6f) * s; }
            Start(p);
        }

        protected override void RenderStill(Graphics g, double t)
        {
            Art.Glow(g, cx, cy, 22 * s, Color.FromArgb(150, 235, 245, 255));
            cb.Draw(g, 1f);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddPolygon(hole);
                using (PathGradientBrush pg = new PathGradientBrush(path))
                {
                    pg.CenterColor = Color.FromArgb(255, 0, 0, 0);
                    pg.SurroundColors = new Color[] { Color.FromArgb(240, 30, 34, 40) };
                    g.FillPath(pg, path);
                }
            }
            using (Pen rim = new Pen(Color.FromArgb(170, 235, 245, 255), 1.1f * s)) g.DrawPolygon(rim, hole);
        }

        protected override void RenderOverlay(Graphics g, double t, float dt)
        {
            float u = (float)(t / AnimTime), e = 1 - (1 - u) * (1 - u);
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(200 * (1 - u)), 230, 235, 240)))
                for (int i = 0; i < dustAng.Length; i++)
                {
                    float d = 8 * s + dustSpeed[i] * e, r = dustSize[i];
                    g.FillEllipse(b, cx + d * (float)Math.Cos(dustAng[i]) - r, cy + d * (float)Math.Sin(dustAng[i]) - r, r * 2, r * 2);
                }
        }
    }
}
