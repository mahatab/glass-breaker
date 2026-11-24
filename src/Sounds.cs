using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GlassBreaker
{
    /// <summary>
    /// Every sound is synthesized (no audio assets). Each named sound has a few variations,
    /// written once to %TEMP% and played through MCI so that sounds can overlap.
    /// </summary>
    sealed class Sounds : IDisposable
    {
        const int SR = 44100;
        const string Version = "v2";
        static readonly string[] Names = { "smash", "crash", "gunshot", "splat", "plip", "pop", "sizzle", "clang", "crunch", "squish" };
        static readonly int[] Counts = { 5, 2, 3, 3, 3, 3, 2, 3, 3, 2 };

        readonly Dictionary<string, int> count = new Dictionary<string, int>();
        readonly Dictionary<string, int> last = new Dictionary<string, int>();
        readonly Random rnd = new Random();
        public bool Enabled = true;
        public readonly List<string> Log = new List<string>();

        public Sounds()
        {
            string dir = Path.Combine(Path.GetTempPath(), "GlassBreaker", Version);
            Directory.CreateDirectory(dir);
            for (int n = 0; n < Names.Length; n++)
            {
                count[Names[n]] = Counts[n];
                last[Names[n]] = -1;
                for (int i = 0; i < Counts[n]; i++)
                {
                    string path = Path.Combine(dir, Names[n] + i + ".wav");
                    if (!File.Exists(path))
                    {
                        try { File.WriteAllBytes(path, Wav(Make(Names[n], new Random(1000 + n * 131 + i * 77)))); }
                        catch (IOException) { }
                    }
                    int rc = Native.mciSendString("open \"" + path + "\" type waveaudio alias gb_" + Names[n] + i, null, 0, IntPtr.Zero);
                    StringBuilder sb = new StringBuilder(32);
                    Native.mciSendString("status gb_" + Names[n] + i + " length", sb, sb.Capacity, IntPtr.Zero);
                    Log.Add(Names[n] + i + ": open rc=" + rc + " length=" + sb + "ms");
                }
            }
        }

        public void Play(string name)
        {
            if (!Enabled) return;
            int n = count[name], i = rnd.Next(n);
            if (i == last[name]) i = (i + 1) % n;   // don't cut off the one still ringing
            last[name] = i;
            Native.mciSendString("play gb_" + name + i + " from 0", null, 0, IntPtr.Zero);
        }

        public void Dispose()
        {
            foreach (KeyValuePair<string, int> kv in count)
                for (int i = 0; i < kv.Value; i++) Native.mciSendString("close gb_" + kv.Key + i, null, 0, IntPtr.Zero);
        }

        // ------------------------------------------------------------ synthesis

        static double[] Make(string name, Random r)
        {
            switch (name)
            {
                case "smash": return Smash(r, 1.35, 55 + r.Next(30), 0.85, 8, false);
                case "crash": return Smash(r, 2.4, 190, 1.7, 3.5, true);
                case "gunshot": return Gunshot(r);
                case "splat": return Splat(r);
                case "plip": return Plip(r);
                case "pop": return Pop(r);
                case "sizzle": return Sizzle(r);
                case "clang": return Clang(r);
                case "crunch": return Crunch(r);
                default: return Squish(r);
            }
        }

        static double White(Random r) { return r.NextDouble() * 2 - 1; }
        static double Coef(double hz) { return 1 - Math.Exp(-2 * Math.PI * hz / SR); }

        static double[] Smash(Random r, double dur, int tinkles, double spread, double tailDecay, bool second)
        {
            int n = (int)(SR * dur);
            double[] buf = new double[n];
            double thumpHz = 90 + r.NextDouble() * 60;
            for (int i = 0; i < SR * 0.09; i++)
            {
                double t = i / (double)SR;
                buf[i] += Math.Sin(2 * Math.PI * thumpHz * t) * Math.Exp(-t * 42) * 0.7;
            }
            double prev = 0, hp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR;
                double white = White(r);
                hp = 0.82 * (hp + white - prev);
                prev = white;
                double env = Math.Exp(-t * 55) + 0.32 * Math.Exp(-t * tailDecay);
                if (second && t > 0.38) env += 0.8 * Math.Exp(-(t - 0.38) * 30);   // the pane hitting the floor
                buf[i] += hp * env * 0.55;
            }
            for (int k = 0; k < tinkles; k++)
            {
                double start = Math.Pow(r.NextDouble(), 2.2) * spread;
                double f = 1900 + Math.Pow(r.NextDouble(), 1.4) * 9000;
                double decay = 16 + r.NextDouble() * 75;
                double amp = (0.05 + r.NextDouble() * 0.17) * (1 - start / spread * 0.7);
                double f2 = f * (2.3 + r.NextDouble() * 0.9);
                bool two = f2 < 19000;
                int s0 = (int)(start * SR), len = (int)(SR * 6.0 / decay);
                for (int j = 0; j < len && s0 + j < n; j++)
                {
                    double t = j / (double)SR;
                    double v = Math.Sin(2 * Math.PI * f * t);
                    if (two) v += 0.5 * Math.Sin(2 * Math.PI * f2 * t + 1.0);
                    buf[s0 + j] += amp * Math.Exp(-t * decay) * v * Math.Min(1.0, j / 20.0);
                }
            }
            return buf;
        }

        static double[] Gunshot(Random r)
        {
            int n = (int)(SR * 0.75);
            double[] buf = new double[n];
            double lp = 0, ph = 0, pitch = 0.85 + r.NextDouble() * 0.3;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR;
                lp += Coef(700 + 6500 * Math.Exp(-t * 28)) * (White(r) - lp);
                double env = Math.Exp(-t * 26) + 0.22 * Math.Exp(-t * 5.5);
                ph += 2 * Math.PI * (55 + 110 * Math.Exp(-t * 40)) * pitch / SR;
                buf[i] = lp * env * 3.0 + Math.Sin(ph) * Math.Exp(-t * 16) * 0.9;
                if (i < 90) buf[i] += White(r) * 1.5;
            }
            return buf;
        }

        static double[] Splat(Random r)
        {
            int n = (int)(SR * 0.42);
            double[] buf = new double[n];
            double lp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR;
                lp += Coef(300 + 2600 * Math.Exp(-t * 12)) * (White(r) - lp);
                buf[i] = lp * Math.Min(1, t / 0.004) * Math.Exp(-t * 15) * 2.2;
            }
            for (int k = 0; k < 3; k++)
            {
                int s0 = (int)((k * 0.035 + r.NextDouble() * 0.01) * SR);
                double ph = 0, f0 = 100 + r.NextDouble() * 50;
                for (int j = 0; s0 + j < n; j++)
                {
                    double t = j / (double)SR;
                    ph += 2 * Math.PI * (f0 + 260 * Math.Exp(-t * 30)) / SR;
                    buf[s0 + j] += Math.Sin(ph) * Math.Exp(-t * 22) * 0.55;
                }
            }
            return buf;
        }

        static double[] Plip(Random r)
        {
            int n = (int)(SR * 0.4);
            double[] buf = new double[n];
            double[] starts = { 0, 0.085 + r.NextDouble() * 0.04 };
            double[] amps = { 1.0, 0.45 };
            for (int k = 0; k < 2; k++)
            {
                double f0 = (520 + r.NextDouble() * 260) * (k == 0 ? 1 : 1.5), ph = 0;
                int s0 = (int)(starts[k] * SR);
                for (int j = 0; s0 + j < n; j++)
                {
                    double t = j / (double)SR;
                    ph += 2 * Math.PI * f0 * (1 + 1.5 * (1 - Math.Exp(-t * 32))) / SR;
                    buf[s0 + j] += Math.Sin(ph) * Math.Exp(-t * 26) * Math.Min(1, t / 0.003) * amps[k];
                }
            }
            return buf;
        }

        static double[] Pop(Random r)
        {
            int n = (int)(SR * 1.25);
            double[] buf = new double[n];
            double lp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR;
                lp += Coef(400 + 5000 * Math.Exp(-t * 20)) * (White(r) - lp);
                buf[i] = lp * Math.Exp(-t * 24) * 2.2 + Math.Sin(2 * Math.PI * 75 * t) * Math.Exp(-t * 18) * 0.9;
            }
            int clicks = 110 + r.Next(60);
            for (int k = 0; k < clicks; k++)
            {
                double start = 0.07 + Math.Pow(r.NextDouble(), 1.4) * 1.05;
                double amp = (0.15 + r.NextDouble() * 0.5) * (1 - start / 1.2);
                int s0 = (int)(start * SR), len = 40 + r.Next(60);
                double prev = 0;
                for (int j = 0; j < len && s0 + j < n; j++)
                {
                    double w = White(r);
                    buf[s0 + j] += (w - prev) * amp * (1 - j / (double)len);
                    prev = w;
                }
            }
            return buf;
        }

        static double[] Sizzle(Random r)
        {
            int n = (int)(SR * 1.4);
            double[] buf = new double[n];
            double prev = 0, hp = 0, ph = 0, mod = 0, target = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR;
                double w = White(r);
                hp = 0.9 * (hp + w - prev); prev = w;
                if (i % 300 == 0) target = r.NextDouble();
                mod += 0.01 * (target - mod);
                ph += 2 * Math.PI * (300 + 2800 * Math.Exp(-t * 45)) / SR;
                buf[i] = hp * Math.Exp(-t * 3.0) * Math.Min(1, t / 0.01) * (0.35 + 0.65 * mod) * 0.8
                       + Math.Sin(ph) * Math.Exp(-t * 28) * 0.7;
            }
            return buf;
        }

        static double[] Clang(Random r)
        {
            int n = (int)(SR * 1.4);
            double[] buf = new double[n];
            double f0 = 320 + r.NextDouble() * 240;
            double[] ratio = { 1, 1.51, 2.76, 3.3, 5.4, 8.93 }, amp = { 1, 0.6, 0.7, 0.4, 0.35, 0.2 }, dec = { 4.5, 6.5, 8, 10, 13, 19 };
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR, v = 0;
                for (int k = 0; k < ratio.Length; k++) v += amp[k] * Math.Exp(-t * dec[k]) * Math.Sin(2 * Math.PI * f0 * ratio[k] * t + k);
                buf[i] = v * Math.Min(1, t / 0.001);
                if (i < 280) buf[i] += White(r) * 1.4 * (1 - i / 280.0);
            }
            return buf;
        }

        static double[] Crunch(Random r)
        {
            int n = (int)(SR * 0.42);
            double[] buf = new double[n];
            int bursts = 3 + r.Next(3);
            double at = 0;
            for (int k = 0; k < bursts; k++)
            {
                int s0 = (int)(at * SR), len = (int)(SR * 0.035);
                double lp = 0, prev = 0, hp = 0, a = Coef(1800 + r.NextDouble() * 2500), amp = 0.6 + r.NextDouble() * 0.4;
                for (int j = 0; j < len && s0 + j < n; j++)
                {
                    double w = White(r);
                    lp += a * (w - lp);
                    hp = 0.93 * (hp + lp - prev); prev = lp;
                    buf[s0 + j] += hp * amp * Math.Exp(-j / (double)SR * 85) * 3;
                }
                at += 0.05 + r.NextDouble() * 0.035;
            }
            return buf;
        }

        static double[] Squish(Random r)
        {
            int n = (int)(SR * 0.28);
            double[] buf = new double[n];
            double lp = 0, ph = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR;
                lp += Coef(400 + 1800 * Math.Exp(-t * 18)) * (White(r) - lp);
                ph += 2 * Math.PI * (90 + 420 * Math.Exp(-t * 26)) / SR;
                buf[i] = lp * Math.Exp(-t * 20) * 2.4 + Math.Sin(ph) * Math.Exp(-t * 24) * 0.5;
            }
            return buf;
        }

        static byte[] Wav(double[] buf)
        {
            int n = buf.Length;
            double peak = 1e-9;
            for (int i = 0; i < n; i++) peak = Math.Max(peak, Math.Abs(buf[i]));
            MemoryStream ms = new MemoryStream();
            BinaryWriter w = new BinaryWriter(ms);
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(SR); w.Write(SR * 2); w.Write((short)2); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
            for (int i = 0; i < n; i++)
            {
                double v = Math.Tanh(buf[i] / peak * 1.6) * 0.95;
                int tail = n - i;
                if (tail < 2000) v *= tail / 2000.0;
                w.Write((short)(v * 32767));
            }
            w.Flush();
            return ms.ToArray();
        }
    }
}
