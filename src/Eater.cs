using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace WindowsTrayToys
{
    /// <summary>
    /// A purple fuzzball that wanders around eating the screen. Each bite leaves a scalloped hole
    /// showing a starfield "behind" the desktop, and the eaten area keeps growing until the screen
    /// is repaired. Click the creature and it bolts off-screen for a while.
    /// </summary>
    sealed class Eater : IAmbient
    {
        const int Roam = 0, Flee = 1, Away = 2;

        readonly float s;
        readonly Random rnd;
        readonly Sounds snd;
        readonly KeyedForm form;
        readonly LayerWin sprite;
        readonly Bitmap backing, starTile, sb;
        readonly TextureBrush stars;
        readonly int size;
        Rectangle vs;
        float x, y, head, target, turnT, biteT = 1, phase, awayT;
        int state = Roam, bites;

        public Eater(float scale, Random rnd, Sounds snd, bool onScreen)
        {
            s = scale; this.rnd = rnd; this.snd = snd;
            size = (int)(76 * s);
            vs = SystemInformation.VirtualScreen;
            x = vs.Left + vs.Width * 0.5f; y = vs.Top + vs.Height * 0.45f;

            // the eaten area lives in a screen-sized bitmap; untouched pixels stay the transparent key colour
            backing = new Bitmap(vs.Width, vs.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(backing)) g.Clear(KeyedForm.Key);

            // one tiling starfield, so every bite reveals the same sky
            starTile = new Bitmap(256, 256, PixelFormat.Format24bppRgb);
            Random sr = new Random(99);
            using (Graphics g = Graphics.FromImage(starTile))
            {
                g.Clear(Color.FromArgb(5, 5, 14));
                for (int i = 0; i < 80; i++)
                {
                    int v = 90 + sr.Next(166), px = sr.Next(254), py = sr.Next(254), big = sr.Next(9) == 0 ? 2 : 1;
                    Color c = sr.Next(6) == 0 ? Color.FromArgb(v, v * 4 / 5, v * 3 / 5) : sr.Next(6) == 0 ? Color.FromArgb(v * 3 / 4, v * 4 / 5, v) : Color.FromArgb(v, v, v);
                    using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, px, py, big, big);
                }
            }
            stars = new TextureBrush(starTile);

            if (!onScreen) return;
            form = new KeyedForm();
            form.Painter = delegate(Graphics g, Rectangle clip)
            {
                if (clip.Width <= 0 || clip.Height <= 0) return;
                g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(backing, clip, clip, GraphicsUnit.Pixel);
            };
            form.Show();
            sb = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            sprite = new LayerWin();
            sprite.Create(0, 0, size, size);
        }

        float Rand(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        int Dir { get { return Math.Cos(head) >= 0 ? 1 : -1; } }

        void Bite()
        {
            float bx = x + Dir * size * 0.28f - vs.X, by = y + size * 0.08f - vs.Y, r = Rand(22, 32) * s;
            using (Graphics g = Graphics.FromImage(backing))
            {
                g.SmoothingMode = SmoothingMode.None;   // hard edges only: soft ones would blend with the key colour
                g.FillEllipse(stars, bx - r, by - r, r * 2, r * 2);
                for (int i = 0; i < 5; i++)             // tooth marks around the rim
                {
                    double a = rnd.NextDouble() * Math.PI * 2;
                    float tr = r * 0.38f, tx = bx + r * 0.85f * (float)Math.Cos(a), ty = by + r * 0.85f * (float)Math.Sin(a);
                    g.FillEllipse(stars, tx - tr, ty - tr, tr * 2, tr * 2);
                }
            }
            if (form != null) form.Invalidate(Rectangle.FromLTRB((int)(bx - r * 1.4f), (int)(by - r * 1.4f), (int)(bx + r * 1.4f) + 1, (int)(by + r * 1.4f) + 1));
            if (++bites % 3 == 0 && snd != null) snd.Play("crunch");
        }

        public void Step(float dt)
        {
            phase += dt;
            if (state == Away)
            {
                awayT -= dt;
                if (awayT > 0) return;
                // wander back in from a random edge
                bool side = rnd.Next(2) == 0;
                x = side ? (rnd.Next(2) == 0 ? vs.Left - size : vs.Right + size) : vs.Left + Rand(0.1f, 0.9f) * vs.Width;
                y = side ? vs.Top + Rand(0.1f, 0.9f) * vs.Height : (rnd.Next(2) == 0 ? vs.Top - size : vs.Bottom + size);
                state = Roam;
            }
            float cxs = vs.Left + vs.Width / 2f, cys = vs.Top + vs.Height / 2f;
            if (state == Flee)
            {
                x += (float)Math.Cos(head) * 750 * s * dt; y += (float)Math.Sin(head) * 750 * s * dt;
                if (x < vs.Left - size || x > vs.Right + size || y < vs.Top - size || y > vs.Bottom + size) { state = Away; awayT = 20; }
                return;
            }
            turnT -= dt;
            if (turnT <= 0) { target = head + Rand(-1, 1); turnT = Rand(0.8f, 2.5f); }
            bool inside = x > vs.Left + 60 * s && x < vs.Right - 60 * s && y > vs.Top + 60 * s && y < vs.Bottom - 60 * s;
            if (!inside) target = (float)Math.Atan2(cys - y, cxs - x);
            head += (float)Math.IEEERemainder(target - head, Math.PI * 2) * Math.Min(1f, dt * 2);
            x += (float)Math.Cos(head) * 60 * s * dt; y += (float)Math.Sin(head) * 60 * s * dt;
            biteT -= dt;
            if (biteT <= 0 && inside) { Bite(); biteT = Rand(0.45f, 0.8f); }
        }

        public void SetMode(string mode) { }

        public void Tick(float dt)
        {
            Step(dt);
            if (state == Away) { sprite.Hide(); return; }
            using (Graphics g = Graphics.FromImage(sb))
            {
                g.Clear(Color.Transparent);
                Art.Setup(g);
                DrawCreature(g, size, Dir, state == Flee ? 1f : 0.5f + 0.5f * (float)Math.Sin(phase * 9));
            }
            sprite.Origin = new Point((int)(x - size / 2f), (int)(y - size / 2f));
            sprite.Push(sb, 255);
        }

        public bool TryClick(Point p)
        {
            if (state != Roam) return false;
            float dx = x - p.X, dy = y - p.Y;
            if (dx * dx + dy * dy > size * size * 0.3f) return false;
            head = (float)Math.Atan2(dy, dx);     // straight away from the click
            if (dx == 0 && dy == 0) head = 0;
            state = Flee;
            if (snd != null) snd.Play("squish");
            return true;
        }

        /// <summary>Repair gives the eaten screen back.</summary>
        public void Repair()
        {
            using (Graphics g = Graphics.FromImage(backing)) g.Clear(KeyedForm.Key);
            if (form != null) form.Invalidate();
        }

        public void Dispose()
        {
            if (form != null) { form.Close(); form.Dispose(); }
            if (sprite != null) sprite.Destroy();
            if (sb != null) sb.Dispose();
            stars.Dispose(); starTile.Dispose(); backing.Dispose();
        }

        /// <summary>Draws the creature in an S x S box. mouth runs 0 (shut) to 1 (wide open).</summary>
        public static void DrawCreature(Graphics g, float S, int dir, float mouth)
        {
            float k = S / 76f;
            GraphicsState st = g.Save();
            g.TranslateTransform(S / 2, S / 2);
            if (dir < 0) g.ScaleTransform(-1, 1);

            using (SolidBrush foot = new SolidBrush(Color.FromArgb(255, 85, 45, 135)))
            {
                g.FillEllipse(foot, -15 * k, 24 * k, 13 * k, 8 * k);
                g.FillEllipse(foot, 3 * k, 24 * k, 13 * k, 8 * k);
            }
            using (SolidBrush horn = new SolidBrush(Color.FromArgb(255, 250, 220, 120)))
            {
                g.FillPolygon(horn, new PointF[] { new PointF(-14 * k, -22 * k), new PointF(-18 * k, -36 * k), new PointF(-6 * k, -27 * k) });
                g.FillPolygon(horn, new PointF[] { new PointF(6 * k, -27 * k), new PointF(16 * k, -37 * k), new PointF(15 * k, -21 * k) });
            }
            PointF[] fur = new PointF[26];
            for (int i = 0; i < fur.Length; i++)
            {
                double a = i * Math.PI * 2 / fur.Length;
                float r = (i % 2 == 0 ? 27 : 32) * k;
                fur[i] = new PointF(r * (float)Math.Cos(a), r * (float)Math.Sin(a));
            }
            using (SolidBrush body = new SolidBrush(Color.FromArgb(255, 150, 85, 210)))
            using (Pen line = new Pen(Color.FromArgb(255, 80, 40, 130), 1.5f * k))
            {
                g.FillPolygon(body, fur);
                g.DrawPolygon(line, fur);
            }

            // the mouth: a wedge opening toward the front, with teeth on both jaws
            float open = 14 + 58 * mouth, mx = 2 * k, my = 5 * k, mr = 26 * k;
            using (SolidBrush maw = new SolidBrush(Color.FromArgb(255, 60, 10, 30)))
                g.FillPie(maw, mx - mr, my - mr, mr * 2, mr * 2, -open / 2, open);
            for (int jaw = -1; jaw <= 1; jaw += 2)
            {
                double a = jaw * open / 2 * Math.PI / 180;
                float ux = (float)Math.Cos(a), uy = (float)Math.Sin(a);     // along the jaw
                float nx = uy * jaw, ny = -ux * jaw;                        // into the mouth
                for (int i = 0; i < 3; i++)
                {
                    float d = (9 + i * 6) * k;
                    g.FillPolygon(Brushes.White, new PointF[] {
                        new PointF(mx + ux * (d - 2.6f * k), my + uy * (d - 2.6f * k)),
                        new PointF(mx + ux * (d + 2.6f * k), my + uy * (d + 2.6f * k)),
                        new PointF(mx + ux * d + nx * 5 * k, my + uy * d + ny * 5 * k) });
                }
            }

            using (Pen line = new Pen(Color.FromArgb(255, 80, 40, 130), 1.2f * k))
            {
                g.FillEllipse(Brushes.White, -4 * k, -22 * k, 15 * k, 15 * k);
                g.DrawEllipse(line, -4 * k, -22 * k, 15 * k, 15 * k);
                g.FillEllipse(Brushes.Black, 3 * k, -17 * k, 6 * k, 6 * k);
            }
            g.Restore(st);
        }

        /// <summary>Self-test: a trail of bites with the creature at the end of it.</summary>
        public static void Sample(Graphics g, float cell)
        {
            Eater e = new Eater(1f, new Random(4), null, false);
            e.x = e.vs.Left + 110; e.y = e.vs.Top + 150; e.head = e.target = 0.35f; e.turnT = 100;
            for (int i = 0; i < 260; i++) e.Step(1 / 60f);
            Rectangle crop = new Rectangle(0, 0, Math.Min((int)cell, e.vs.Width), Math.Min((int)cell, e.vs.Height));
            using (Bitmap part = e.backing.Clone(crop, PixelFormat.Format32bppArgb))
            {
                part.MakeTransparent(KeyedForm.Key);
                g.DrawImage(part, 0, 0, crop.Width, crop.Height);
            }
            g.TranslateTransform(e.x - e.vs.Left - 60, e.y - e.vs.Top - 60);
            DrawCreature(g, 120, 1, 0.8f);
            e.Dispose();
        }
    }
}
