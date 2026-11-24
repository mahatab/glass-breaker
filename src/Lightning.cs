using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GlassBreaker
{
    /// <summary>
    /// A bolt of lightning from the top of the screen down to the click. It flickers twice and
    /// leaves a small scorched crater. The window is a tall strip above the click.
    /// </summary>
    sealed class LightningDecal : Decal
    {
        readonly List<PointF[]> bolts = new List<PointF[]>();
        readonly List<float> widths = new List<float>();
        readonly PointF[] scorch;
        readonly CrackBuilder cracks;

        public LightningDecal(Point p, Random rnd, float scale) : base(rnd, scale)
        {
            int top = Offscreen ? p.Y - (int)(520 * s) : Screen.FromPoint(p).Bounds.Top;
            float height = Math.Max(260 * s, p.Y - top);
            w = (int)(560 * s); h = (int)(height + 90 * s);
            cx = w / 2f; cy = height;
            AnimTime = 0.55;

            List<PointF> main = new List<PointF>();
            main.Add(new PointF(cx + Rand(-150, 150) * s, 0));
            Jag(main, main[0], new PointF(cx, cy), 70 * s);
            bolts.Add(main.ToArray()); widths.Add(2.4f * s);

            int forks = 2 + rnd.Next(3);
            for (int i = 0; i < forks; i++)
            {
                PointF from = main[rnd.Next(1, Math.Max(2, main.Count * 7 / 10))];
                double a = Math.PI / 2 + (rnd.Next(2) == 0 ? -1 : 1) * Rand(0.4f, 0.95f);
                float len = Rand(80, 230) * s;
                List<PointF> fork = new List<PointF>();
                fork.Add(from);
                Jag(fork, from, new PointF(from.X + len * (float)Math.Cos(a), from.Y + len * (float)Math.Sin(a)), 28 * s);
                bolts.Add(fork.ToArray()); widths.Add(1.2f * s);
            }

            scorch = Art.Blob(rnd, cx, cy, Rand(20, 30) * s, 0.15f, 12);
            cracks = new CrackBuilder(rnd, s, 70 * s);
            for (int i = 0; i < 6; i++) cracks.Walk(new PointF(cx, cy), Rand(0, 6.28f), Rand(20, 65) * s, 0, 0.9f, 6 * s, 1);
            Start(p);
        }

        /// <summary>Midpoint displacement: splits a to b in two at a random sideways offset, and repeats on each half.</summary>
        void Jag(List<PointF> pts, PointF a, PointF b, float sway)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 26 * s || sway < 2 * s) { pts.Add(b); return; }
            float off = Rand(-sway, sway);
            PointF mid = new PointF((a.X + b.X) / 2 - dy / len * off, (a.Y + b.Y) / 2 + dx / len * off);
            Jag(pts, a, mid, sway * 0.55f);
            Jag(pts, mid, b, sway * 0.55f);
        }

        protected override void RenderStill(Graphics g, double t)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddClosedCurve(scorch, 0.5f);
                using (PathGradientBrush pg = new PathGradientBrush(path))
                {
                    pg.CenterPoint = new PointF(cx, cy);
                    pg.CenterColor = Color.FromArgb(235, 6, 6, 8);
                    pg.SurroundColors = new Color[] { Color.FromArgb(0, 30, 30, 40) };
                    pg.FocusScales = new PointF(0.3f, 0.3f);
                    g.FillPath(pg, path);
                }
            }
            cracks.Draw(g, 1f);
        }

        protected override void RenderOverlay(Graphics g, double t, float dt)
        {
            // strike, dim, strike again, then die away: the double flash that reads as lightning
            float k = t < 0.08 ? 1f : t < 0.14 ? 0.22f : t < 0.24 ? 0.9f : (float)Math.Max(0, 1 - (t - 0.24) / 0.3) * 0.7f;
            if (k <= 0) return;
            Art.Glow(g, cx, cy, 140 * s, Color.FromArgb((int)(210 * k), 215, 230, 255));
            for (int i = 0; i < bolts.Count; i++)
            {
                float wd = widths[i];
                using (Pen glow = new Pen(Color.FromArgb((int)(55 * k), 150, 190, 255), wd * 7))
                using (Pen mid = new Pen(Color.FromArgb((int)(170 * k), 200, 225, 255), wd * 2.6f))
                using (Pen core = new Pen(Color.FromArgb((int)(255 * k), 255, 255, 255), wd))
                {
                    glow.LineJoin = mid.LineJoin = core.LineJoin = LineJoin.Round;
                    glow.StartCap = glow.EndCap = mid.StartCap = mid.EndCap = LineCap.Round;
                    g.DrawLines(glow, bolts[i]);
                    g.DrawLines(mid, bolts[i]);
                    g.DrawLines(core, bolts[i]);
                }
            }
        }
    }
}
