using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WindowsTrayToys
{
    /// <summary>A firework burst: coloured sparks with trails that slow, droop and twinkle out.</summary>
    sealed class FireworkDecal : Decal
    {
        class Spark { public float X, Y, VX, VY, Size, Life; public Color C; }
        readonly Spark[] sparks;

        public FireworkDecal(Point p, Random rnd, float scale) : base(rnd, scale)
        {
            float R = 340 * s;
            w = h = (int)(R * 2);
            cx = cy = R;
            HasStill = false;
            AnimTime = 2.0;

            double hue = rnd.NextDouble() * 360, hue2 = hue + 40 + rnd.NextDouble() * 140;
            sparks = new Spark[80 + rnd.Next(40)];
            for (int i = 0; i < sparks.Length; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2;
                float sp = Rand(120, 520) * s * (rnd.NextDouble() < 0.3 ? 0.55f : 1f);
                Spark k = new Spark();
                k.X = cx; k.Y = cy; k.VX = sp * (float)Math.Cos(a); k.VY = sp * (float)Math.Sin(a);
                k.Size = Rand(1.6f, 3f) * s; k.Life = Rand(1.1f, 1.9f);
                k.C = Art.Hsv(rnd.Next(3) == 0 ? hue2 : hue, 0.85, 1, 255);
                sparks[i] = k;
            }
            Start(p);
        }

        protected override void RenderOverlay(Graphics g, double t, float dt)
        {
            if (t < 0.12) Art.Glow(g, cx, cy, 90 * s, Color.FromArgb((int)(220 * (1 - t / 0.12)), 255, 255, 240));
            float drag = (float)Math.Exp(-2.4 * dt);
            foreach (Spark k in sparks)
            {
                k.VX *= drag; k.VY = k.VY * drag + 260 * s * dt;
                k.X += k.VX * dt; k.Y += k.VY * dt;
                float left = (float)(1 - t / k.Life);
                if (left <= 0 || (left < 0.4f && rnd.Next(3) == 0)) continue;   // twinkle near the end
                int a = (int)(255 * Math.Min(1f, left * 1.6f));
                using (Pen pen = new Pen(Color.FromArgb(a, k.C), k.Size))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, k.X, k.Y, k.X - k.VX * 0.06f, k.Y - k.VY * 0.06f);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 255, 255, 255)))
                    g.FillEllipse(b, k.X - k.Size * 0.5f, k.Y - k.Size * 0.5f, k.Size, k.Size);
            }
        }
    }

    /// <summary>A pop of paper confetti that flutters down.</summary>
    sealed class ConfettiDecal : Decal
    {
        class Bit { public float X, Y, VX, VY, Rot, Spin, Flip, FlipSpeed, W, H; public Color C; }
        readonly Bit[] bits;

        public ConfettiDecal(Point p, Random rnd, float scale) : base(rnd, scale)
        {
            w = (int)(660 * s); cx = w / 2f; cy = 300 * s;
            h = (int)(cy + 540 * s);
            HasStill = false;
            AnimTime = 2.6;

            bits = new Bit[70 + rnd.Next(30)];
            for (int i = 0; i < bits.Length; i++)
            {
                double a = -Math.PI / 2 + Rand(-1.3f, 1.3f);
                float sp = Rand(250, 900) * s;
                Bit b = new Bit();
                b.X = cx; b.Y = cy; b.VX = sp * (float)Math.Cos(a); b.VY = sp * (float)Math.Sin(a);
                b.Rot = Rand(0, 360); b.Spin = Rand(-400, 400); b.Flip = Rand(0, 6.28f); b.FlipSpeed = Rand(5, 14);
                b.W = Rand(5, 9) * s; b.H = Rand(8, 14) * s;
                b.C = Art.Hsv(rnd.Next(12) * 30, 0.8, 1, 255);
                bits[i] = b;
            }
            Start(p);
        }

        protected override void RenderOverlay(Graphics g, double t, float dt)
        {
            int alpha = (int)(255 * Math.Min(1.0, (AnimTime - t) / 0.5));
            float drag = (float)Math.Exp(-1.9 * dt);
            foreach (Bit b in bits)
            {
                b.VX *= drag; b.VY = b.VY * drag + 1100 * s * dt;
                b.X += b.VX * dt; b.Y += b.VY * dt; b.Rot += b.Spin * dt; b.Flip += b.FlipSpeed * dt;
                float hh = b.H * (float)Math.Abs(Math.Cos(b.Flip));   // paper turning edge-on
                if (hh < 1) hh = 1;
                g.ResetTransform();
                g.TranslateTransform(b.X, b.Y);
                g.RotateTransform(b.Rot);
                using (SolidBrush br = new SolidBrush(Color.FromArgb(alpha, b.C))) g.FillRectangle(br, -b.W / 2, -hh / 2, b.W, hh);
            }
            g.ResetTransform();
        }
    }
}
