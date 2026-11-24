using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace WindowsTrayToys
{
    /// <summary>
    /// "WindowsTrayToys.exe --docs [dir]" regenerates the README images from the real drawing code,
    /// so the documentation always shows what the app actually draws.
    /// </summary>
    static class DocImages
    {
        public static void Run(string dir)
        {
            Directory.CreateDirectory(dir);
            Decal.Offscreen = true;
            Logo(Path.Combine(dir, "logo.png"));
            Hero(Path.Combine(dir, "hero.png"));
            EffectsGrid(Path.Combine(dir, "effects.png"));
            AlwaysOn(Path.Combine(dir, "always-on.png"));
        }

        static Graphics Canvas(Bitmap b)
        {
            Graphics g = Graphics.FromImage(b);
            Art.Setup(g);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            return g;
        }

        static GraphicsPath Rounded(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>Draws a decal as it looks t seconds after a click at (x, y), optionally enlarged.</summary>
        static void Stamp(Graphics g, Decal d, double t, float x, float y, float zoom)
        {
            using (Bitmap b = d.Simulate(t))
                g.DrawImage(b, x - d.Anchor.X * zoom, y - d.Anchor.Y * zoom, b.Width * zoom, b.Height * zoom);
            d.Destroy();
        }

        static void Label(Graphics g, string text, float x, float y, float size)
        {
            using (Font f = new Font("Segoe UI Semibold", size, GraphicsUnit.Pixel))
            {
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(170, 0, 0, 0))) g.DrawString(text, f, shadow, x + 1.5f, y + 1.5f);
                g.DrawString(text, f, Brushes.White, x, y);
            }
        }

        /// <summary>
        /// Writes the application icon: the logo at seven sizes in one .ico file. The 256-pixel
        /// image is stored as PNG and the smaller ones as plain 32-bit bitmaps, which is the mix
        /// Windows expects. The build embeds the file in the exe.
        /// </summary>
        public static void Icon(string path)
        {
            Decal.Offscreen = true;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
            List<byte[]> images = new List<byte[]>();
            foreach (int size in sizes)
                using (Bitmap b = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Canvas(b))
                    {
                        // the stand takes room the small sizes cannot spare, so they zoom in on the screen
                        if (size < 48) { g.ScaleTransform(size / 320f, size / 320f); g.TranslateTransform(-20, -32); }
                        else g.ScaleTransform(size / 360f, size / 360f);
                        DrawLogo(g);
                    }
                    using (MemoryStream ms = new MemoryStream())
                    {
                        if (size == 256) b.Save(ms, ImageFormat.Png);
                        else
                        {
                            BinaryWriter w = new BinaryWriter(ms);
                            int maskRow = (size + 31) / 32 * 4;
                            w.Write(40); w.Write(size); w.Write(size * 2); w.Write((short)1); w.Write((short)32);
                            w.Write(0); w.Write(size * size * 4 + maskRow * size); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
                            for (int y = size - 1; y >= 0; y--)
                                for (int x = 0; x < size; x++) w.Write(b.GetPixel(x, y).ToArgb());
                            w.Write(new byte[maskRow * size]);   // transparency comes from the alpha channel
                            w.Flush();
                        }
                        images.Add(ms.ToArray());
                    }
                }
            using (BinaryWriter w = new BinaryWriter(File.Create(path)))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                    w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                    w.Write(images[i].Length); w.Write(offset);
                    offset += images[i].Length;
                }
                foreach (byte[] img in images) w.Write(img);
            }
        }

        static void Logo(string path)
        {
            using (Bitmap b = new Bitmap(360, 360, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Canvas(b)) DrawLogo(g);
                b.Save(path, ImageFormat.Png);
            }
        }

        /// <summary>The logo, drawn in a 360 x 360 space: a monitor with a cracked screen.</summary>
        static void DrawLogo(Graphics g)
        {
            {
                {
                    RectangleF bezel = new RectangleF(30, 50, 300, 210), screen = new RectangleF(44, 64, 272, 182);
                    using (SolidBrush stand = new SolidBrush(Color.FromArgb(255, 52, 58, 72)))
                    {
                        g.FillRectangle(stand, 160, 258, 40, 34);
                        using (GraphicsPath foot = Rounded(new RectangleF(110, 288, 140, 20), 9)) g.FillPath(stand, foot);
                    }
                    using (GraphicsPath p = Rounded(bezel, 18))
                    using (SolidBrush dark = new SolidBrush(Color.FromArgb(255, 28, 32, 42))) g.FillPath(dark, p);
                    using (GraphicsPath p = Rounded(screen, 8))
                    {
                        using (LinearGradientBrush sky = new LinearGradientBrush(screen, Color.FromArgb(255, 70, 165, 235), Color.FromArgb(255, 30, 60, 140), 55f)) g.FillPath(sky, p);
                        g.SetClip(p);
                        Stamp(g, new GlassDecal(new Point(0, 0), new Random(21), 1f), 5, 200, 140, 0.42f);
                        g.ResetClip();
                    }
                }
            }
        }

        static void Window(Graphics g, RectangleF r, bool dark, Random rnd)
        {
            Color body = dark ? Color.FromArgb(255, 32, 34, 40) : Color.FromArgb(255, 245, 246, 248);
            Color bar = dark ? Color.FromArgb(255, 46, 49, 58) : Color.FromArgb(255, 224, 228, 234);
            using (GraphicsPath shadow = Rounded(new RectangleF(r.X - 6, r.Y - 2, r.Width + 12, r.Height + 14), 14))
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillPath(sb, shadow);
            using (GraphicsPath p = Rounded(r, 9))
            {
                using (SolidBrush b = new SolidBrush(body)) g.FillPath(b, p);
                g.SetClip(p);
                using (SolidBrush b = new SolidBrush(bar)) g.FillRectangle(b, r.X, r.Y, r.Width, 34);
                Color[] dots = { Color.FromArgb(255, 235, 95, 85), Color.FromArgb(255, 245, 190, 60), Color.FromArgb(255, 95, 200, 90) };
                for (int i = 0; i < 3; i++) using (SolidBrush b = new SolidBrush(dots[i])) g.FillEllipse(b, r.Right - 30 - i * 22, r.Y + 11, 12, 12);
                // stand-in content: lines of "text"
                Color[] ink = dark
                    ? new[] { Color.FromArgb(255, 110, 170, 250), Color.FromArgb(255, 220, 150, 90), Color.FromArgb(255, 130, 200, 130), Color.FromArgb(255, 150, 155, 170) }
                    : new[] { Color.FromArgb(255, 196, 200, 208), Color.FromArgb(255, 210, 214, 222) };
                for (float y = r.Y + 56; y < r.Bottom - 20; y += 22)
                {
                    float x = r.X + 24 + (dark ? rnd.Next(3) * 22 : 0);
                    int words = 2 + rnd.Next(4);
                    for (int i = 0; i < words && x < r.Right - 60; i++)
                    {
                        float wd = 30 + rnd.Next(90);
                        using (SolidBrush b = new SolidBrush(ink[rnd.Next(ink.Length)])) g.FillRectangle(b, x, y, Math.Min(wd, r.Right - 30 - x), 9);
                        x += wd + 12;
                    }
                }
                g.ResetClip();
            }
        }

        /// <summary>A mock desktop with a bit of everything going on.</summary>
        static void Hero(string path)
        {
            const int W = 1280, H = 720;
            Random rnd = new Random(8);
            using (Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Canvas(b))
                {
                    using (LinearGradientBrush wall = new LinearGradientBrush(new Rectangle(0, 0, W, H), Color.FromArgb(255, 16, 34, 70), Color.FromArgb(255, 78, 44, 116), 35f))
                        g.FillRectangle(wall, 0, 0, W, H);
                    Art.Glow(g, 980, 120, 420, Color.FromArgb(70, 255, 150, 120));
                    Art.Glow(g, 200, 640, 380, Color.FromArgb(60, 60, 200, 230));

                    RectangleF winA = new RectangleF(90, 96, 540, 350), winB = new RectangleF(730, 262, 450, 330);
                    Window(g, winA, false, rnd);
                    Window(g, winB, true, rnd);

                    using (SolidBrush bar = new SolidBrush(Color.FromArgb(235, 18, 20, 28))) g.FillRectangle(bar, 0, H - 46, W, 46);
                    for (int i = 0; i < 8; i++)
                        using (GraphicsPath p = Rounded(new RectangleF(W / 2 - 152 + i * 38, H - 36, 26, 26), 6))
                        using (SolidBrush ic = new SolidBrush(Art.Hsv(i * 47 + 200, 0.55, 0.95, 255))) g.FillPath(ic, p);

                    // the screen eater has been at the top right corner
                    GraphicsState st = g.Save();
                    g.TranslateTransform(800, -70);
                    Eater.Sample(g, 420);
                    g.Restore(st);

                    Stamp(g, new GlassDecal(new Point(0, 0), new Random(5), 1f), 5, 372, 268, 1f);
                    Stamp(g, new PaintballDecal(new Point(0, 0), new Random(3), 1f), 5, 930, 400, 1f);
                    Stamp(g, new PaintballDecal(new Point(0, 0), new Random(15), 1f), 5, 1120, 470, 0.8f);
                    float[] holes = { 650, 520, 700, 560, 640, 600, 742, 610, 690, 640 };
                    for (int i = 0; i < holes.Length; i += 2) Stamp(g, new BulletDecal(new Point(0, 0), new Random(30 + i), 1f), 5, holes[i], holes[i + 1], 1f);
                    Stamp(g, new BurnDecal(new Point(0, 0), new Random(2), 1f), 0.5, 190, 560, 1f);
                    Stamp(g, new DentDecal(new Point(0, 0), new Random(6), 1f), 5, 440, 566, 1f);
                    Stamp(g, new FireworkDecal(new Point(0, 0), new Random(9), 1f), 0.28, 690, 150, 0.9f);

                    // the screen mate, standing on the dark window
                    st = g.Save();
                    g.TranslateTransform(800 - 32, winB.Y - 61);
                    Mate.Draw(g, 64, 1, 0, 0.13f, false);
                    g.Restore(st);

                    float[] ants = { 250, 480, 0.4f, 320, 520, 2.8f, 560, 470, -0.9f, 1080, 640, 3.4f };
                    for (int i = 0; i < ants.Length; i += 3)
                    {
                        st = g.Save();
                        g.TranslateTransform(ants[i] - 24, ants[i + 1] - 24);
                        Bugs.DrawBug(g, 24, 1.15f, i == 6 ? 1 : 0, ants[i + 2], i);
                        g.Restore(st);
                    }
                }
                b.Save(path, ImageFormat.Png);
            }
        }

        static void Cell(Graphics g, Rectangle cell)
        {
            using (LinearGradientBrush bg = new LinearGradientBrush(cell, Color.FromArgb(255, 26, 44, 78), Color.FromArgb(255, 176, 186, 204), 0f))
                g.FillRectangle(bg, cell);
            using (Pen line = new Pen(Color.FromArgb(255, 14, 18, 28), 2)) g.DrawRectangle(line, cell);
        }

        /// <summary>One cell per click effect, each at its most recognisable moment.</summary>
        static void EffectsGrid(string path)
        {
            const int C = 400, Cols = 4;
            Dictionary<string, double> when = new Dictionary<string, double>();
            when["ripples"] = 0.45; when["fireworks"] = 0.3; when["confetti"] = 0.5; when["burn"] = 0.5;
            int rows = (Effects.All.Count + Cols - 1) / Cols;
            using (Bitmap b = new Bitmap(C * Cols, C * rows, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Canvas(b))
                    for (int i = 0; i < Effects.All.Count; i++)
                    {
                        EffectDef e = Effects.All[i];
                        Rectangle cell = new Rectangle(i % Cols * C, i / Cols * C, C, C);
                        Cell(g, cell);
                        g.SetClip(cell);
                        double t; if (!when.TryGetValue(e.Key, out t)) t = 5;
                        Decal d = e.Make(new Point(0, 0), new Random(7 + i), 1f);
                        using (Bitmap img = d.Simulate(t))
                        {
                            float k = Math.Min(1.9f, Math.Min(C / (float)img.Width, C / (float)img.Height)) * 0.94f;
                            g.DrawImage(img, cell.X + (C - img.Width * k) / 2, cell.Y + (C - img.Height * k) / 2, img.Width * k, img.Height * k);
                        }
                        d.Destroy();
                        g.ResetClip();
                        Label(g, e.Name, cell.X + 14, cell.Bottom - 38, 20);
                    }
                b.Save(path, ImageFormat.Png);
            }
        }

        /// <summary>The always-on features and the chain reaction, three to a row.</summary>
        static void AlwaysOn(string path)
        {
            const int C = 420, Cols = 3;
            SelfTest.BuildSamples();
            int n = SelfTest.Samples.Count, rows = (n + Cols - 1) / Cols;
            using (Bitmap b = new Bitmap(C * Cols, C * rows, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Canvas(b))
                    for (int i = 0; i < n; i++)
                    {
                        Rectangle cell = new Rectangle(i % Cols * C, i / Cols * C, C, C);
                        Cell(g, cell);
                        g.SetClip(cell);
                        g.TranslateTransform(cell.X, cell.Y);
                        Art.Setup(g);
                        SelfTest.Samples[i].Value(g, C);
                        g.ResetTransform();
                        g.ResetClip();
                        string name = SelfTest.Samples[i].Key;
                        int cut = name.IndexOf(" (");
                        Label(g, cut > 0 ? name.Substring(0, cut) : name, cell.X + 14, cell.Bottom - 38, 20);
                    }
                b.Save(path, ImageFormat.Png);
            }
        }
    }
}
