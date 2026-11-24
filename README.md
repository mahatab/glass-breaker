# Glass Breaker

A Windows desktop toy. It sits in the system tray, and clicking anywhere on screen leaves a
mark at that spot, with a matching sound. The default is a glass break that makes it look as
if the monitor cracked under the mouse.

![A crack](crack-preview.png)

Clicks are never blocked: everything is drawn on click-through overlays, so whatever you
clicked still receives the click.

## Build

Run `build.cmd`. It uses the C# compiler that ships with Windows (.NET Framework 4.x), so no
SDK is needed. The result is a single `GlassBreaker.exe` with no dependencies.

## Use

Start `GlassBreaker.exe`, then click anywhere. Right-click the tray icon for everything else.

### Click effects

Pick one for each mouse button under **Left click**, **Middle click** and **Right click**:

| Effect | What it does |
|---|---|
| Glass break | Spider-web cracks and flying shards |
| Bullet holes | A small puncture with short cracks |
| Paintball splats | A coloured splat whose drips run down the screen |
| Water ripples | Rings spreading from the click |
| Fireworks | A burst of coloured sparks |
| Confetti | Paper that flutters down |
| Burn marks | A laser hit that glows, smokes and leaves a scorch |
| Hammer dents | A dent, as if the screen were sheet metal |
| Random | A different one each click |

**Chain reaction:** six glass breaks close together and the whole pane falls out.

### Always-on features

| Feature | What it does |
|---|---|
| Mouse trail | Sparks, bubbles or paw prints follow the cursor |
| Crawling bugs | Ants and flies wander the screen; click one to squash it |
| Weather | Snow that settles on window tops and the taskbar, or rain |
| Screen mate | A critter that walks the taskbar, hops onto windows and naps; click it to make it jump |
| Screen eater | A fuzzball that eats holes in the screen until you repair it; click it to chase it off |

### Controls

- **Enabled**, or double-click the tray icon: pause and resume.
- **Ctrl+Alt+G**: panic button. Clears the screen and pauses; press again to resume.
- **Repair screen**: clear every mark and give back anything eaten.
- **Keep marks until repaired**: marks normally fade after about six seconds.
- **Sound**, **Start with Windows**.

Choices are saved in `%APPDATA%\GlassBreaker\settings.ini`.

## How it works

- A low-level mouse hook listens for clicks without intercepting them.
- Each mark is its own small, topmost, click-through layered window with per-pixel alpha
  (`src/Layers.cs`); each effect is one class that draws into it.
- Full-screen features (weather, the eaten screen) use one colour-keyed overlay instead, which
  is much cheaper at that size (`src/Desktop.cs`).
- Every pattern is generated randomly and every sound is synthesized at first run
  (`src/Sounds.cs`), so there are no image or audio assets.
- `GlassBreaker.exe --selftest` renders every effect to PNG contact sheets without touching
  the screen and checks that each sound opens.

Adding a click effect means writing one `Decal` subclass and one line in `Effects` (`src/App.cs`).
Adding an always-on feature means one `IAmbient` class and one line in `Ambients` (`src/Ambient.cs`).
