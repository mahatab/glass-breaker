# Adding Effects

There are two kinds of thing you can add: a **click effect** (a mark left by a click) and an
**always-on feature** (something that lives on the desktop). Each is one class plus one
registration line. Read [Architecture](ARCHITECTURE.md) first if the terms below are new.

## Add a click effect

### 1. Write a `Decal` subclass

Create `src/YourEffect.cs`. The constructor sets the window size and where the click sits
inside it, builds any random geometry, and ends with `Start(p)`.

```csharp
using System;
using System.Drawing;

namespace GlassBreaker
{
    /// <summary>A gold star that pops in and stays.</summary>
    sealed class StarDecal : Decal
    {
        readonly PointF[] star = new PointF[10];

        public StarDecal(Point p, Random rnd, float scale) : base(rnd, scale)
        {
            float R = Rand(40, 60) * s;          // s is the display scale; multiply every size by it
            w = h = (int)(R * 2 + 10 * s);       // window size in pixels
            cx = cy = w / 2f;                    // the click lands at the centre
            AnimTime = 0.25;                     // seconds of animation before the mark goes still

            for (int i = 0; i < 10; i++)
            {
                double a = i * Math.PI / 5 - Math.PI / 2;
                float r = i % 2 == 0 ? R : R * 0.45f;
                star[i] = new PointF(cx + r * (float)Math.Cos(a), cy + r * (float)Math.Sin(a));
            }
            Start(p);
        }

        // What lasts. Drawn once and cached after StillAt (0 by default).
        protected override void RenderStill(Graphics g, double t)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 250, 205, 60))) g.FillPolygon(b, star);
            using (Pen p = new Pen(Color.FromArgb(255, 150, 100, 10), 1.5f * s)) g.DrawPolygon(p, star);
        }

        // What moves. Drawn on top every frame until AnimTime.
        protected override void RenderOverlay(Graphics g, double t, float dt)
        {
            float k = (float)(1 - t / AnimTime);
            Art.Glow(g, cx, cy, w / 2f, Color.FromArgb((int)(200 * k), 255, 255, 220));
        }
    }
}
```

### 2. Register it

Add one line to the `Effects` static constructor in `src/App.cs`:

```csharp
All.Add(new EffectDef("star", "Gold stars", "plip", delegate(Point p, Random r, float s) { return new StarDecal(p, r, s); }));
```

The arguments are the settings key, the menu name, the sound to play, and a factory. That is
all: the effect now appears in all three mouse-button menus, in **Random**, and in the self-test.

### Things to know

| Setting | Default | Meaning |
|---------|---------|---------|
| `AnimTime` | `0.3` | Seconds the overlay layer animates |
| `StillAt` | `0` | Until this time `RenderStill` is called every frame, so the mark can grow in |
| `HasStill` | `true` | Set `false` for effects that leave nothing behind |
| `Hold` | `-1` | Seconds to stay before fading; `-1` uses the standard six |

- **Keep windows small.** Cost grows with window area. Size the window to what you draw.
- **Draw for any background.** Marks land on white documents and dark wallpapers alike. Pair a
  light stroke with a dark one, the way `CrackBuilder.Draw` and the ripples do.
- **Use `rnd`.** Take all randomness from the `rnd` you are given so the self-test is repeatable.
- **Reuse the helpers.** `CrackBuilder` makes branching cracks; `Art.Glow`, `Art.Blob` and
  `Art.Hsv` cover soft glows, irregular shapes and colours.

### Add a sound

Existing sounds are `smash`, `crash`, `gunshot`, `splat`, `plip`, `pop`, `sizzle`, `clang`,
`crunch` and `squish`. To add one, in `src/Sounds.cs`:

1. Add its name to `Names` and how many variations to `Counts`.
2. Add a `case` in `Make` that returns the samples.
3. Write the synth function: fill a `double[]` at 44100 samples per second. Loudness does not
   matter, since `Wav` normalizes it.
4. Bump `Version`, so files cached by older builds are regenerated.

## Add an always-on feature

### 1. Implement `IAmbient`

```csharp
interface IAmbient : IDisposable
{
    void SetMode(string mode);     // called when the menu choice changes
    void Tick(float dt);           // called every 15 ms while enabled
    bool TryClick(Point p);        // return true to claim a click (no effect is spawned)
    void Repair();                 // "Repair screen" was chosen
}
```

Pick the overlay that fits:

- **Small and moving** (a creature, a trail): a `LayerWin`. Draw into a bitmap, set `Origin`,
  call `Push`. See `Mate.cs` or `Bugs.cs`.
- **Screen-sized** (weather, anything that accumulates): a `KeyedForm`. Set its `Painter`, call
  `Invalidate` for the area that changed, and draw with `SmoothingMode.None`. See `Weather.cs`
  or `Eater.cs`.

Use `WindowList.Get()` and `WindowList.TopVisibleAt()` if the feature should react to the
user's windows.

### 2. Register it

Add one line to the `Ambients` static constructor in `src/Ambient.cs`.

An on/off feature passes `null` for the choices:

```csharp
All.Add(new AmbientDef("fish", "Fish tank", null, null,
    delegate(float s, Random r, Sounds snd) { return new Fish(s, r); }, Fish.Sample));
```

A feature with modes lists their settings keys and menu names, with "off" first:

```csharp
All.Add(new AmbientDef("fish", "Fish tank", new[] { "off", "gold", "tropical" }, new[] { "Off", "Goldfish", "Tropical" },
    delegate(float s, Random r, Sounds snd) { return new Fish(s, r); }, Fish.Sample));
```

The tray menu item, saving the choice, starting and stopping with **Enabled**, the timer, click
routing and **Repair screen** are all handled for you.

### 3. Give it a sample

The last argument is a `static void Sample(Graphics g, float cell)` that draws a representative
picture into a `cell` x `cell` square. The self-test and the README images call it. Structure
the class so its drawing can run without creating a window: most features take an `onScreen`
flag in the constructor and keep the drawing in a `static` method. Pass `null` if you really
cannot provide one.

## Check your work

```bat
build.cmd
GlassBreaker.exe --selftest
```

Open `selftest_effects.png` (each click effect at 0.12 s, 0.45 s and 5 s) and
`selftest_ambient.png` (one sample per always-on feature), and check `selftest.log` for
`FAILED`. Then run the app and try it for real: the self-test cannot tell you how something
feels, or how it looks over actual windows.

If the effect belongs in the README, run `GlassBreaker.exe --docs` to regenerate the images.
