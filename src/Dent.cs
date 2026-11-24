using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WindowsTrayToys
{
    /// <summary>A hammer dent, as if the screen were sheet metal: shaded bowl, bright lower lip, a few sparks.</summary>
    sealed class DentDecal : Decal
    {
        readonly float R;
        readonly float[] scuff;      // angle, start, length triples
        readonly float[] sparkAng, sparkSpeed;

        public DentDecal(Point p, Random rnd, float scale) : base(rnd, scale)
        {
            R = Rand(52, 74) * s;
            w = h = (int)(R * 2 + 60 * s);
            cx = cy = w / 2f;
            AnimTime = 0.3;
            scuff = new float[18];
            for (int i = 0; i < scuff.Length; i += 3) { scuff[i] = Rand(0, 6.28f); scuff[i + 1] = R * Rand(0.05f, 0.3f); scuff[i + 2] = R * Rand(0.12f, 0.3f); }
            sparkAng = new float[12]; sparkSpeed = new float[12];
            for (int i = 0; i < 12; i++) { sparkAng[i] = Rand(0, 6.28f); sparkSpeed[i] = Rand(40, 120) * s; }
            Start(p);
        }

        static void Arc(Graphics g, Color c, float width, float x, float y, float r, float start, float sweep)
        {
            using (Pen p = new Pen(c, width))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                g.DrawArc(p, x - r, y - r, r * 2, r * 2, start, sweep);
            }
        }

        protected override void RenderStill(Graphics g, double t)
        {
            // the bowl: darkest toward the upper left, where the light can't reach
            Art.Glow(g, cx - R * 0.14f, cy - R * 0.14f, R * 1.08f, Color.FromArgb(135, 0, 0, 0));
            for (int i = 0; i < 5; i++)   // layered thin arcs give soft-edged crescents
            {
                float k = i / 4f;
                Arc(g, Color.FromArgb(34, 0, 0, 0), R * (0.20f - 0.035f * i), cx, cy, R * 0.84f, 180 + 14 * k, 110 - 28 * k);
                // the lit lower-right lip
                Arc(g, Color.FromArgb(30, 255, 255, 255), R * (0.18f - 0.032f * i), cx, cy, R * 0.88f, 0 + 14 * k, 110 - 28 * k);
            }
            // the flat spot where the hammer face landed
            float fr = R * 0.3f;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(b, cx - fr, cy - fr, fr * 2, fr * 2);
            Arc(g, Color.FromArgb(130, 255, 255, 255), 1.3f * s, cx, cy, fr, 15, 100);
            Arc(g, Color.FromArgb(150, 0, 0, 0), 1.3f * s, cx, cy, fr, 195, 100);
            using (Pen p = new Pen(Color.FromArgb(95, 235, 240, 245), 1.1f * s))
                for (int i = 0; i < scuff.Length; i += 3)
                {
                    float c = (float)Math.Cos(scuff[i]), sn = (float)Math.Sin(scuff[i]);
                    g.DrawLine(p, cx + c * scuff[i + 1], cy + sn * scuff[i + 1], cx + c * (scuff[i + 1] + scuff[i + 2]), cy + sn * (scuff[i + 1] + scuff[i + 2]));
                }
        }

        protected override void RenderOverlay(Graphics g, double t, float dt)
        {
            float u = (float)(t / AnimTime), e = 1 - (1 - u) * (1 - u);
            using (Pen p = new Pen(Color.FromArgb((int)(255 * (1 - u)), 255, 205, 95), 1.6f * s))
                for (int i = 0; i < sparkAng.Length; i++)
                {
                    float c = (float)Math.Cos(sparkAng[i]), sn = (float)Math.Sin(sparkAng[i]), d = R * 0.3f + sparkSpeed[i] * e;
                    g.DrawLine(p, cx + c * d, cy + sn * d, cx + c * (d - 9 * s), cy + sn * (d - 9 * s));
                }
        }
    }
}
