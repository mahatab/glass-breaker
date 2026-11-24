using System;
using System.Drawing;

namespace WindowsTrayToys
{
    /// <summary>Rings spreading from the click, as if the screen were a pond. Leaves no mark.</summary>
    sealed class RippleDecal : Decal
    {
        readonly float R;

        public RippleDecal(Point p, Random rnd, float scale) : base(rnd, scale)
        {
            R = Rand(140, 190) * s;
            w = h = (int)(R * 2 + 14 * s);
            cx = cy = w / 2f;
            HasStill = false;
            AnimTime = 1.5;
            Start(p);
        }

        protected override void RenderOverlay(Graphics g, double t, float dt)
        {
            for (int k = 0; k < 4; k++)
            {
                float u = (float)((t - k * 0.2) / 0.9);
                if (u < 0 || u > 1) continue;
                float r = R * (1 - (1 - u) * (1 - u)), a = (float)Math.Pow(1 - u, 1.5), wd = s * (1 - u * 0.5f);
                // a dark ring just outside a light one, so it shows on any background
                using (Pen dark = new Pen(Color.FromArgb((int)(80 * a), 20, 50, 90), 3.5f * wd))
                    g.DrawEllipse(dark, cx - r - 1.5f * s, cy - r - 1.5f * s, (r + 1.5f * s) * 2, (r + 1.5f * s) * 2);
                using (Pen light = new Pen(Color.FromArgb((int)(215 * a), 225, 240, 255), 2.4f * wd))
                    g.DrawEllipse(light, cx - r, cy - r, r * 2, r * 2);
            }
            if (t < 0.18)
            {
                float k = (float)(1 - t / 0.18), r = 6 * s * k;
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(230 * k), 240, 248, 255))) g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2);
            }
        }
    }
}
