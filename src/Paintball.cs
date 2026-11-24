using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace GlassBreaker
{
    /// <summary>A random-coloured paint splat whose drips slowly run down the screen.</summary>
    sealed class PaintballDecal : Decal
    {
        static readonly Color[] Palette =
        {
            Color.FromArgb(228, 40, 50), Color.FromArgb(255, 135, 0), Color.FromArgb(248, 208, 20), Color.FromArgb(60, 185, 70),
            Color.FromArgb(30, 135, 240), Color.FromArgb(155, 70, 220), Color.FromArgb(238, 60, 160), Color.FromArgb(20, 195, 195)
        };
        const double DripTime = 4.0;

        struct Ray { public float Ang, Len, Wid; }
        struct Dot { public float X, Y, R; }
        struct Drip { public float X, Y, Wid, Len, Delay; }

        readonly Color main, dark;
        readonly float R;
        readonly PointF[] blob, rim;
        readonly Ray[] rays;
        readonly Dot[] dots;
        readonly Drip[] drips;

        public PaintballDecal(Point p, Random rnd, float scale) : base(rnd, scale)
        {
            R = Rand(34, 50) * s;
            float reach = R * 2.8f;
            w = (int)(reach * 2); cx = reach; cy = reach;
            h = (int)(cy + Math.Max(reach, R + 260 * s));
            AnimTime = StillAt = DripTime;

            main = Palette[rnd.Next(Palette.Length)];
            dark = Color.FromArgb(main.R * 7 / 10, main.G * 7 / 10, main.B * 7 / 10);

            // everything is built around (0,0) and drawn through a transform
            blob = Art.Blob(rnd, 0, 0, R, 0.2f, 16);
            rim = new PointF[blob.Length];
            for (int i = 0; i < blob.Length; i++) rim[i] = new PointF(blob[i].X * 1.05f, blob[i].Y * 1.05f);

            rays = new Ray[8 + rnd.Next(6)];
            for (int i = 0; i < rays.Length; i++) { rays[i].Ang = Rand(0, 6.28f); rays[i].Len = R * Rand(1.3f, 2.3f); rays[i].Wid = R * Rand(0.10f, 0.22f); }

            dots = new Dot[16 + rnd.Next(10)];
            for (int i = 0; i < dots.Length; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2; float d = R * Rand(1.2f, 2.6f);
                dots[i].X = d * (float)Math.Cos(a); dots[i].Y = d * (float)Math.Sin(a); dots[i].R = Rand(1.5f, 6f) * s;
            }

            drips = new Drip[3 + rnd.Next(4)];
            for (int i = 0; i < drips.Length; i++)
            {
                drips[i].X = R * Rand(-0.7f, 0.7f); drips[i].Y = R * 0.5f;
                drips[i].Wid = Rand(5, 10) * s; drips[i].Len = Rand(50, 230) * s; drips[i].Delay = Rand(0, 0.7f);
            }
            Start(p);
        }

        protected override void RenderStill(Graphics g, double t)
        {
            float pop = Art.Clamp01(t / 0.07);
            pop = 1 - (1 - pop) * (1 - pop);
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(0.3f + 0.7f * pop, 0.3f + 0.7f * pop);

            using (SolidBrush paint = new SolidBrush(Color.FromArgb(238, main)))
            using (SolidBrush edge = new SolidBrush(Color.FromArgb(238, dark)))
            using (SolidBrush shine = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
            {
                foreach (Ray r in rays)
                {
                    float c = (float)Math.Cos(r.Ang), sn = (float)Math.Sin(r.Ang), b = R * 0.55f;
                    g.FillPolygon(paint, new PointF[] {
                        new PointF(c * b - sn * r.Wid, sn * b + c * r.Wid), new PointF(c * b + sn * r.Wid, sn * b - c * r.Wid),
                        new PointF(c * r.Len, sn * r.Len) });
                    float d = r.Wid * 0.55f;
                    g.FillEllipse(paint, c * r.Len * 1.04f - d, sn * r.Len * 1.04f - d, d * 2, d * 2);
                }
                foreach (Dot d in dots) g.FillEllipse(paint, d.X - d.R, d.Y - d.R, d.R * 2, d.R * 2);

                foreach (Drip d in drips)
                {
                    float u = Art.Clamp01((t - d.Delay) / (DripTime - 0.8));
                    float len = d.Len * (1 - (1 - u) * (1 - u));
                    if (len < 1) continue;
                    using (Pen pen = new Pen(Color.FromArgb(238, main), d.Wid))
                    {
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, d.X, d.Y, d.X, d.Y + len);
                    }
                    float br = d.Wid * 0.72f;
                    g.FillEllipse(paint, d.X - br, d.Y + len - br * 0.6f, br * 2, br * 2);
                    g.FillEllipse(shine, d.X - br * 0.5f, d.Y + len - br * 0.2f, br * 0.6f, br * 0.6f);
                }

                g.FillClosedCurve(edge, rim, FillMode.Winding, 0.5f);
                g.FillClosedCurve(paint, blob, FillMode.Winding, 0.5f);

                GraphicsState st = g.Save();
                g.TranslateTransform(-R * 0.28f, -R * 0.34f);
                g.RotateTransform(-28);
                g.FillEllipse(shine, -R * 0.3f, -R * 0.14f, R * 0.6f, R * 0.28f);
                g.Restore(st);
            }
            g.ResetTransform();
        }
    }
}
