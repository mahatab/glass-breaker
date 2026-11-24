using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace WindowsTrayToys
{
    /// <summary>
    /// A small orange critter that walks along the top of the taskbar, hops up onto windows,
    /// sits down for naps, and drops back to the floor if its window moves away. Click it to make it jump.
    /// </summary>
    sealed class Mate : IAmbient
    {
        const int Walk = 0, Sit = 1, Hop = 2, Fall = 3;

        readonly float s;
        readonly Random rnd;
        readonly LayerWin win = new LayerWin();
        readonly Bitmap bmp;
        readonly int size;
        float x, y, vy, stT, stLen, decide = 3, phase, blinkT, checkT;
        float hx0, hy0, hx1, hy1;
        int dir = 1, state = Walk;
        IntPtr perch = WindowList.Floor, hopTarget = WindowList.Floor;

        public Mate(float scale, Random rnd)
        {
            s = scale; this.rnd = rnd;
            size = (int)(64 * s);
            bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            x = wa.Left + wa.Width * 0.3f; y = wa.Bottom;
            win.Create(0, 0, size, size);
        }

        float Rand(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        /// <summary>The ledge the mate is standing on: its x range and height.</summary>
        bool Ledge(IntPtr h, out float left, out float right, out float top)
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            left = wa.Left + 24 * s; right = wa.Right - 24 * s; top = wa.Bottom;
            if (h == WindowList.Floor) return true;
            WinInfo w;
            if (!WindowList.Find(h, out w)) return false;
            left = Math.Max(left, w.R.Left + 14 * s); right = Math.Min(right, w.R.Right - 14 * s); top = w.R.Top;
            return right > left;
        }

        void StartHop(float tx, float ty, IntPtr target)
        {
            hx0 = x; hy0 = y; hx1 = tx; hy1 = ty; hopTarget = target;
            if (Math.Abs(tx - x) > 2) dir = tx > x ? 1 : -1;
            state = Hop; stT = 0;
        }

        void TryHop()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            List<WinInfo> options = new List<WinInfo>();
            foreach (WinInfo w in WindowList.Get())
            {
                if (w.H == perch) continue;
                if (w.H == WindowList.Floor) { options.Add(w); continue; }
                if (w.R.Top > wa.Top + 70 && w.R.Top < wa.Bottom - 60 && w.R.Width > 220 && w.R.Right > wa.Left + 60 && w.R.Left < wa.Right - 60) options.Add(w);
            }
            if (options.Count == 0) return;
            WinInfo pick = options[rnd.Next(options.Count)];
            float l, r, top;
            if (!Ledge(pick.H, out l, out r, out top)) return;
            float tx = pick.H == WindowList.Floor ? Math.Max(l, Math.Min(r, x)) : Rand(l, r);
            if (!WindowList.TopVisibleAt(pick, (int)tx)) return;
            StartHop(tx, top, pick.H);
        }

        public void SetMode(string mode) { }

        public void Tick(float dt)
        {
            phase += dt;
            blinkT -= dt;
            if (blinkT < -0.15f) blinkT = Rand(1.5f, 4f);

            if (state == Walk || state == Sit)
            {
                float l, r, top;
                bool ok = Ledge(perch, out l, out r, out top);
                checkT -= dt;
                if (ok && perch != WindowList.Floor && checkT <= 0)
                {
                    checkT = 0.5f;
                    WinInfo w;
                    ok = WindowList.Find(perch, out w) && WindowList.TopVisibleAt(w, (int)x);
                }
                if (!ok || x < l - 12 * s || x > r + 12 * s) { state = Fall; vy = 0; }
                else
                {
                    y = top;
                    if (state == Walk)
                    {
                        x += dir * 55 * s * dt;
                        if (x > r) { x = r; dir = -1; }
                        if (x < l) { x = l; dir = 1; }
                    }
                    else { stT += dt; if (stT > stLen) state = Walk; }
                    decide -= dt;
                    if (decide <= 0)
                    {
                        decide = Rand(3, 7);
                        double roll = rnd.NextDouble();
                        if (roll < 0.35) { state = Sit; stT = 0; stLen = Rand(2, 5); }
                        else if (roll < 0.55) dir = -dir;
                        else TryHop();
                    }
                }
            }
            else if (state == Hop)
            {
                stT += dt;
                float u = stT / 0.75f;
                if (u >= 1) { x = hx1; y = hy1; perch = hopTarget; state = Walk; }
                else { x = hx0 + (hx1 - hx0) * u; y = hy0 + (hy1 - hy0) * u - (float)Math.Sin(Math.PI * u) * 130 * s; }
            }
            else
            {
                float floor = Screen.PrimaryScreen.WorkingArea.Bottom;
                vy += 2200 * s * dt; y += vy * dt;
                if (y >= floor) { y = floor; perch = WindowList.Floor; state = Walk; }
            }

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                Art.Setup(g);
                Draw(g, size, dir, state, phase, blinkT < 0 || (state == Sit && stT > 1.5f));
            }
            win.Origin = new Point((int)(x - size / 2f), (int)(y - size + 3 * s));
            win.Push(bmp, 255);
        }

        public bool TryClick(Point p)
        {
            if (state != Walk && state != Sit) return false;
            if (p.X < x - size / 2f || p.X > x + size / 2f || p.Y < y - size || p.Y > y) return false;
            StartHop(x, y, perch);
            return true;
        }

        public void Repair() { }

        public void Dispose() { win.Destroy(); bmp.Dispose(); }

        /// <summary>Draws the critter in an S x S box, feet on the bottom edge.</summary>
        public static void Draw(Graphics g, float S, int dir, int state, float phase, bool eyesShut)
        {
            float k = S / 64f;
            bool sit = state == Sit, air = state == Hop || state == Fall;
            GraphicsState st = g.Save();
            g.TranslateTransform(S / 2, S - 3 * k);
            if (dir < 0) g.ScaleTransform(-1, 1);

            Color fur = Color.FromArgb(255, 242, 150, 55), darkFur = Color.FromArgb(255, 150, 80, 20);
            float step = sit || air ? 0 : (float)Math.Sin(phase * 12);
            float by = sit ? -25 * k : -33 * k + step * 1.2f * k, bh = sit ? 25 * k : 28 * k;

            using (Pen tail = new Pen(darkFur, 4.5f * k))
            {
                tail.StartCap = tail.EndCap = LineCap.Round;
                float wag = (float)Math.Sin(phase * 5) * 4 * k;
                g.DrawBezier(tail, -13 * k, by + bh * 0.65f, -24 * k, by + bh * 0.6f, -27 * k + wag, by + 2 * k, -19 * k + wag, by - 5 * k);
            }
            using (SolidBrush foot = new SolidBrush(Color.FromArgb(255, 190, 100, 30)))
                if (!sit)
                {
                    float f = step * 3 * k, lift = air ? 3 * k : 0;
                    g.FillEllipse(foot, -12 * k + f, -6 * k - lift, 11 * k, 6.5f * k);
                    g.FillEllipse(foot, 2 * k - f, -6 * k - lift, 11 * k, 6.5f * k);
                }
            using (SolidBrush body = new SolidBrush(fur))
            using (SolidBrush belly = new SolidBrush(Color.FromArgb(255, 255, 222, 172)))
            using (Pen line = new Pen(darkFur, 1.5f * k))
            {
                g.FillEllipse(body, -17 * k, by, 34 * k, bh); g.DrawEllipse(line, -17 * k, by, 34 * k, bh);
                g.FillEllipse(belly, -6 * k, by + 9 * k, 19 * k, bh - 12 * k);

                float hy = by - 15 * k;
                PointF[] ear1 = { new PointF(-1 * k, hy + 6 * k), new PointF(2 * k, hy - 7 * k), new PointF(9 * k, hy + 2 * k) };
                PointF[] ear2 = { new PointF(12 * k, hy + 2 * k), new PointF(19 * k, hy - 7 * k), new PointF(22 * k, hy + 7 * k) };
                g.FillPolygon(body, ear1); g.DrawPolygon(line, ear1);
                g.FillPolygon(body, ear2); g.DrawPolygon(line, ear2);
                g.FillEllipse(body, -4 * k, hy, 27 * k, 23 * k); g.DrawEllipse(line, -4 * k, hy, 27 * k, 23 * k);

                using (Pen lid = new Pen(Color.FromArgb(255, 40, 25, 15), 1.6f * k))
                {
                    float[] ex = { 8 * k, 17 * k };
                    foreach (float e in ex)
                    {
                        if (eyesShut) { g.DrawLine(lid, e - 2.5f * k, hy + 11 * k, e + 2.5f * k, hy + 11 * k); continue; }
                        g.FillEllipse(Brushes.White, e - 2.8f * k, hy + 7.5f * k, 5.6f * k, 7 * k);
                        g.FillEllipse(Brushes.Black, e - 0.6f * k, hy + 9.5f * k, 3 * k, 3.6f * k);
                    }
                    g.FillEllipse(Brushes.Black, 12 * k, hy + 15 * k, 3 * k, 2.2f * k);
                }
            }
            g.Restore(st);
        }

        /// <summary>Self-test: walking, sitting asleep, and mid-hop, enlarged.</summary>
        public static void Sample(Graphics g, float cell)
        {
            g.TranslateTransform(10, 30); Draw(g, 180, 1, Walk, 0.13f, false);
            g.TranslateTransform(200, 0); Draw(g, 180, -1, Sit, 0.5f, true);
            g.TranslateTransform(-100, 190); Draw(g, 180, 1, Hop, 0.9f, false);
        }
    }
}
