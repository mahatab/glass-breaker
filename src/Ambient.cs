using System;
using System.Collections.Generic;
using System.Drawing;

namespace GlassBreaker
{
    /// <summary>Something that lives on the desktop without needing a click (trails, creatures, weather).</summary>
    interface IAmbient : IDisposable
    {
        void SetMode(string mode);
        void Tick(float dt);
        /// <summary>Returns true if the click hit this feature, in which case no click effect is spawned.</summary>
        bool TryClick(Point p);
        void Repair();
    }

    sealed class AmbientDef
    {
        public readonly string Key, Title;
        public readonly string[] Keys, Names;     // null for a plain on/off feature
        public readonly Func<float, Random, Sounds, IAmbient> Make;
        public readonly Action<Graphics, float> Sample;   // self-test drawing

        public AmbientDef(string key, string title, string[] keys, string[] names, Func<float, Random, Sounds, IAmbient> make, Action<Graphics, float> sample)
        {
            Key = key; Title = title; Keys = keys; Names = names; Make = make; Sample = sample;
        }

        public string Off { get { return Keys == null ? "0" : Keys[0]; } }
    }

    static class Ambients
    {
        public static readonly List<AmbientDef> All = new List<AmbientDef>();

        static Ambients()
        {
            All.Add(new AmbientDef("trail", "Mouse trail", new[] { "off", "sparks", "bubbles", "paws" }, new[] { "Off", "Sparks", "Bubbles", "Footprints" },
                delegate(float s, Random r, Sounds snd) { return new Trail(s, r, true); }, Trail.Sample));
        }
    }
}
