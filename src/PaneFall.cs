using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WindowsTrayToys
{
    /// <summary>
    /// The chain reaction: after enough glass breaks in one area the whole pane gives way.
    /// Big shards drop out of a jagged hole, leaving a dark void with cracked edges.
    /// </summary>
    sealed class PaneFallDecal : Decal
    {
        class Shard { public PointF[] P; public PointF C; public float Delay, VX, Spin; }

        readonly PointF[] hole, inner;
        readonly Shard[] shards;
        readonly CrackBuilder cb;

        public PaneFallDecal(Point p, Random rnd, float scale) : base(rnd, scale)
        {
            float Rh = Rand(190, 240) * s, reach = Rh * 1.2f + 170 * s;
            w = (int)(reach * 2); cx = cy = reach;
            h = (int)(cy + reach + 420 * s);     // room below for the shards to fall
            AnimTime = 1.6; Hold = 14;

            int n = 12 + rnd.Next(4);
            hole = new PointF[n]; inner = new PointF[n];
            float[] ang = new float[n];
            double off = rnd.NextDouble() * Math.PI * 2;
            for (int i = 0; i < n; i++)
            {
                ang[i] = (float)(off + (i + Rand(-0.3f, 0.3f)) * Math.PI * 2 / n);
                float r = Rh * Rand(0.7f, 1.15f);
                hole[i] = new PointF(cx + r * (float)Math.Cos(ang[i]), cy + r * (float)Math.Sin(ang[i]));
                inner[i] = new PointF(cx + r * 0.965f * (float)Math.Cos(ang[i]), cy + r * 0.965f * (float)Math.Sin(ang[i]));
            }

            cb = new CrackBuilder(rnd, s, reach);
            for (int i = 0; i < n; i++) cb.Walk(hole[i], ang[i] + Rand(-0.3f, 0.3f), Rand(40, 170) * s, 0, 1.6f, 12 * s, 0);

            PointF mid = new PointF(cx + Rand(-20, 20) * s, cy + Rand(-20, 20) * s);
            shards = new Shard[n];
            for (int i = 0; i < n; i++)
            {
                PointF a = hole[i], b = hole[(i + 1) % n];
                Shard sh = new Shard();
                sh.C = new PointF((a.X + b.X + mid.X) / 3, (a.Y + b.Y + mid.Y) / 3);
                sh.P = new PointF[] { new PointF(mid.X - sh.C.X, mid.Y - sh.C.Y), new PointF(a.X - sh.C.X, a.Y - sh.C.Y), new PointF(b.X - sh.C.X, b.Y - sh.C.Y) };
                sh.Delay = Rand(0, 0.3f); sh.VX = Rand(-70, 70) * s; sh.Spin = Rand(-150, 150);
                shards[i] = sh;
            }
            Start(p);
        }

        protected override void RenderStill(Graphics g, double t)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddPolygon(hole);
                using (PathGradientBrush pg = new PathGradientBrush(path))
                {
                    pg.CenterColor = Color.FromArgb(250, 3, 5, 9);
                    pg.SurroundColors = new Color[] { Color.FromArgb(240, 16, 20, 26) };
                    g.FillPath(pg, path);
                }
            }
            cb.Draw(g, 1f);
            // the broken edge: a bright outer lip and a dimmer inner one hint at the glass thickness
            using (Pen lip = new Pen(Color.FromArgb(230, 235, 245, 255), 2 * s)) g.DrawPolygon(lip, hole);
            using (Pen depth = new Pen(Color.FromArgb(95, 150, 185, 215), 1.2f * s)) g.DrawPolygon(depth, inner);
        }

        protected override void RenderOverlay(Graphics g, double t, float dt)
        {
            foreach (Shard sh in shards)
            {
                float tt = (float)t - sh.Delay, fade = 1, dx = 0, dy = 0, rot = 0;
                if (tt > 0)
                {
                    fade = 1 - tt / 1.2f;
                    if (fade <= 0) continue;
                    dx = sh.VX * tt; dy = 0.5f * 2600 * s * tt * tt; rot = sh.Spin * tt;
                }
                g.ResetTransform();
                g.TranslateTransform(sh.C.X + dx, sh.C.Y + dy);
                g.RotateTransform(rot);
                using (SolidBrush glass = new SolidBrush(Color.FromArgb((int)(85 * fade), 200, 225, 245))) g.FillPolygon(glass, sh.P);
                using (Pen edge = new Pen(Color.FromArgb((int)(220 * fade), 240, 248, 255), 1.4f * s)) g.DrawPolygon(edge, sh.P);
            }
            g.ResetTransform();
        }
    }
}
