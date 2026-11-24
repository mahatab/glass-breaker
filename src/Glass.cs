using System;
using System.Collections.Generic;
using System.Drawing;

namespace WindowsTrayToys
{
    /// <summary>The original effect: a spider-web glass break with flying shards.</summary>
    sealed class GlassDecal : Decal
    {
        struct Facet { public PointF[] P; public float D; public Color C; }
        class Shard { public float X, Y, VX, VY, Rot, Spin, Size; public int Tint; }

        const double Grow = 0.17;
        readonly List<Facet> facets = new List<Facet>();
        readonly List<Shard> shards = new List<Shard>();
        readonly CrackBuilder cb;
        readonly float R;

        public GlassDecal(Point p, Random rnd, float scale) : base(rnd, scale)
        {
            R = (280 + rnd.Next(110)) * s;
            w = h = (int)(R * 2 + 40 * s);
            cx = cy = w / 2f;
            AnimTime = 0.8; StillAt = Grow;
            cb = new CrackBuilder(rnd, s, R);
            Build();
            Start(p);
        }

        void Build()
        {
            PointF center = new PointF(cx, cy);
            float step = 13 * s;
            int n = 11 + rnd.Next(7);
            List<PointF>[] rad = new List<PointF>[n];
            float[] len = new float[n];
            double off = rnd.NextDouble() * Math.PI * 2;

            for (int i = 0; i < n; i++)
            {
                float ang = (float)(off + (i + Rand(-0.35f, 0.35f)) * Math.PI * 2 / n);
                rad[i] = cb.Walk(center, ang, R * Rand(0.42f, 1.0f), 0, 1.9f, step, 0);
                len[i] = (rad[i].Count - 1) * step;
            }

            // spider-web rings joining neighbouring radials, plus tinted facets between them
            float rr = R * Rand(0.06f, 0.09f), prevRR = 0;
            while (rr < R * 0.82f)
            {
                double prob = 0.92 - 0.6 * (rr / R);
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    if (len[i] < rr * 1.05f || len[j] < rr * 1.05f || rnd.NextDouble() > prob) continue;
                    PointF a = CrackBuilder.PointAt(rad[i], step, rr * Rand(0.86f, 1.14f));
                    PointF b = CrackBuilder.PointAt(rad[j], step, rr * Rand(0.86f, 1.14f));
                    float pull = Rand(0.02f, 0.10f);
                    PointF mid = new PointF((a.X + b.X) / 2 + (cx - (a.X + b.X) / 2) * pull + Rand(-2, 2) * s,
                                            (a.Y + b.Y) / 2 + (cy - (a.Y + b.Y) / 2) * pull + Rand(-2, 2) * s);
                    float lw = s * Rand(0.55f, 1.25f);
                    cb.Add(a, mid, rr / R, lw);
                    cb.Add(mid, b, rr / R, lw);
                    if (rnd.NextDouble() < 0.42)
                    {
                        Facet f;
                        f.P = new PointF[] { CrackBuilder.PointAt(rad[i], step, prevRR), a, mid, b, CrackBuilder.PointAt(rad[j], step, prevRR) };
                        f.D = rr / R;
                        f.C = rnd.Next(4) == 0 ? Color.FromArgb(10 + rnd.Next(28), 0, 10, 20)
                                               : Color.FromArgb(8 + rnd.Next(30), 225, 240, 255);
                        facets.Add(f);
                    }
                }
                prevRR = rr;
                rr *= Rand(1.4f, 1.8f);
            }

            // crushed glass at the point of impact
            float crush = R * 0.075f;
            for (int i = 0; i < 46; i++)
            {
                double a1 = rnd.NextDouble() * Math.PI * 2, a2 = a1 + (rnd.NextDouble() - 0.5) * 1.6;
                float r1 = crush * (float)Math.Sqrt(rnd.NextDouble()), r2 = Math.Min(crush * 1.2f, r1 + Rand(3, 12) * s);
                cb.Add(new PointF(cx + r1 * (float)Math.Cos(a1), cy + r1 * (float)Math.Sin(a1)),
                       new PointF(cx + r2 * (float)Math.Cos(a2), cy + r2 * (float)Math.Sin(a2)), 0, s * Rand(0.5f, 1.3f));
            }

            int count = 26 + rnd.Next(16);
            for (int i = 0; i < count; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2;
                float sp = Rand(120, 620) * s;
                Shard sh = new Shard();
                sh.X = cx + Rand(-6, 6) * s; sh.Y = cy + Rand(-6, 6) * s;
                sh.VX = sp * (float)Math.Cos(a); sh.VY = sp * (float)Math.Sin(a) - 160 * s;
                sh.Rot = Rand(0, 6.28f); sh.Spin = Rand(-14, 14); sh.Size = Rand(2.5f, 9f) * s; sh.Tint = rnd.Next(3);
                shards.Add(sh);
            }
        }

        protected override void RenderStill(Graphics g, double t)
        {
            double u = Math.Min(1.0, t / Grow);
            float reveal = (float)(1 - (1 - u) * (1 - u));

            foreach (Facet f in facets)
                if (f.D <= reveal) using (SolidBrush b = new SolidBrush(f.C)) g.FillPolygon(b, f.P);

            // frosted bloom of pulverised glass at the centre
            Art.Glow(g, cx, cy, R * 0.13f * Math.Min(1f, reveal * 5f), Color.FromArgb(150, 240, 248, 255));
            cb.Draw(g, reveal);

            float hr = 3.2f * s;   // the hole itself
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 10, 14, 18))) g.FillEllipse(b, cx - hr, cy - hr, hr * 2, hr * 2);
        }

        protected override void RenderOverlay(Graphics g, double t, float dt)
        {
            float life = (float)(1 - t / AnimTime);
            int alpha = (int)(230 * Math.Min(1f, life * 1.6f));
            if (alpha <= 0) return;
            using (SolidBrush b0 = new SolidBrush(Color.FromArgb(alpha, 240, 248, 255)))
            using (SolidBrush b1 = new SolidBrush(Color.FromArgb(alpha, 170, 205, 230)))
            using (SolidBrush b2 = new SolidBrush(Color.FromArgb(alpha * 2 / 3, 40, 55, 70)))
            {
                PointF[] tri = new PointF[3];
                foreach (Shard sh in shards)
                {
                    sh.VY += 1900 * s * dt;
                    sh.X += sh.VX * dt; sh.Y += sh.VY * dt; sh.Rot += sh.Spin * dt;
                    for (int k = 0; k < 3; k++)
                    {
                        double a = sh.Rot + k * 2.2;
                        float rad = sh.Size * (k == 0 ? 1.3f : 0.8f);
                        tri[k] = new PointF(sh.X + rad * (float)Math.Cos(a), sh.Y + rad * (float)Math.Sin(a));
                    }
                    g.FillPolygon(sh.Tint == 0 ? b0 : sh.Tint == 1 ? b1 : b2, tri);
                }
            }
        }
    }
}
