<p align="center">
  <img src="docs/images/logo.png" alt="Glass Breaker" width="180" />
</p>

<h1 align="center">Glass Breaker</h1>

<p align="center">
  <strong>Click anywhere. Break your screen. Nothing is actually harmed.</strong>
</p>

<p align="center">
  <a href="https://github.com/mahatab/glass-breaker/releases/latest"><img src="https://img.shields.io/github/v/release/mahatab/glass-breaker?color=2ea44f" alt="Latest release" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License" /></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4" alt="Windows 10 | 11" />
  <img src="https://img.shields.io/badge/C%23-.NET%20Framework%204.x-512BD4" alt="C# on .NET Framework 4.x" />
  <img src="https://img.shields.io/badge/size-~100%20KB-orange" alt="About 100 KB" />
  <img src="https://img.shields.io/badge/dependencies-none-brightgreen" alt="No dependencies" />
</p>

---

Glass Breaker is a **Windows desktop toy** that lives in the system tray. Click anywhere on screen and it looks as if the monitor cracked under your mouse, with a smash to match. Pick other effects per mouse button, and switch on creatures and weather that share the desktop with you. Every overlay is **click-through**, so whatever you clicked still receives the click.

<p align="center">
  <img src="docs/images/hero.png" alt="A mock desktop showing a glass break across one window, paint splats dripping down another, bullet holes, a burn mark, a dent, a firework, ants, an orange critter standing on a window, and a purple creature eating a starry hole in the corner of the screen" width="820" />
</p>
<p align="center"><em>A bit of everything at once — every mark above is drawn by the app's own code</em></p>

## Download

Get **`GlassBreaker.exe`** from the [latest release](https://github.com/mahatab/glass-breaker/releases/latest), then double-click it.

- **One file, about 100 KB** — no installer, nothing else to install on Windows 10 or 11
- **Code-signed and timestamped** — each release also lists its SHA-256 checksum
- **Easy to remove** — exit from the tray menu and delete the file

## How It Works

```mermaid
flowchart LR
    A([You click anywhere]) --> B[Mouse hook<br/>listens, never blocks]
    B --> C{Hit a bug or<br/>a creature?}
    C -- yes --> D[It reacts:<br/>squash, jump, flee]
    C -- no --> E[Effect chosen for<br/>that mouse button]
    E --> F[Click-through overlay<br/>draws the mark]
    E --> G[Synthesized sound<br/>plays]
    A --> H([Your click still reaches<br/>the app underneath])
```

A low-level mouse hook hears every click but never intercepts it. Each click spawns a small, always-on-top, click-through window at that spot and draws a randomly generated mark into it, while a matching sound plays. Marks fade after a few seconds unless you ask to keep them. Creatures and weather run on their own overlays and get first refusal on each click.

## Features

### Click Effects
Pick a different effect for the **left**, **middle** and **right** mouse buttons.

- **Glass break** — spider-web cracks spread from the click and shards fly
- **Bullet holes** — a small puncture with short cracks; pepper the screen with them
- **Paintball splats** — a random colour, with drips that run down the screen
- **Water ripples** — rings spread out and fade, leaving no mark
- **Fireworks** — a burst of coloured sparks that droop and twinkle out
- **Confetti** — paper that flutters down
- **Burn marks** — a laser hit that glows, smokes and leaves a scorch
- **Hammer dents** — as if the screen were sheet metal
- **Random** — a different one every click

### Chain Reaction ![Glass break](https://img.shields.io/badge/Glass_break-orange)
- Land **six glass breaks close together** and the whole pane gives way
- Big shards drop out of a jagged hole and fall down the screen, with a longer crash
- Can be turned off from the tray menu

### Mouse Trails
- **Sparks**, **bubbles** or **paw prints** follow the cursor
- Drawn in one small window that travels with the pointer, so it stays cheap

### Crawling Bugs
- **Ants and flies** wander the screen, each in its own tiny window
- **Click one to squash it** — the splat fades and a new bug walks in from the edge

### Weather
- **Snow** drifts down and **settles on the tops of your windows** and the taskbar
- Piles follow their window when it moves, and slowly melt
- **Rain** streaks past, splashes on window tops, and beads cling to the glass then run

### Screen Mate
- A small orange critter **walks along the taskbar** and **hops up onto windows**
- It sits down for naps, and drops to the floor if its window moves away
- **Click it** to make it jump

### Screen Eater
- A purple fuzzball wanders around **eating holes in your screen**
- Each bite reveals a starfield "behind" the desktop, and the eaten area keeps growing
- **Click it** and it bolts off-screen for a while; **Repair screen** gives everything back

### Controls
- **Ctrl+Alt+G panic button** — clears the screen and pauses everything; press again to resume
- **Double-click the tray icon** to pause and resume
- **Repair screen** — clear every mark at once
- **Keep marks until repaired** — marks normally fade after about six seconds
- **Sound** on or off, and **Start with Windows**
- Choices are remembered between runs

### Small and Self-Contained
- **No assets** — every pattern is generated at random, so no two marks are alike
- **No audio files** — every sound is synthesized the first time the app runs
- **No runtime to install** — it uses the .NET Framework that ships with Windows
- **No network access** — nothing is downloaded or sent anywhere

## Screenshots

<p align="center">
  <img src="docs/images/effects.png" alt="A grid of the eight click effects: glass break, bullet holes, paintball splats, water ripples, fireworks, confetti, burn marks and hammer dents" width="820" />
</p>
<p align="center"><em>The eight click effects — assign any of them to any mouse button</em></p>

<p align="center">
  <img src="docs/images/always-on.png" alt="A grid showing mouse trails, crawling bugs, weather, the screen mate, the screen eater and the chain reaction" width="820" />
</p>
<p align="center"><em>Trails, bugs, weather, the screen mate, the screen eater, and the pane falling out</em></p>

## Tray Menu

| Item | What it does |
|------|--------------|
| **Enabled** | Pause or resume everything (also Ctrl+Alt+G, or double-click the icon) |
| **Left / Middle / Right click** | Choose the effect for each mouse button |
| **Mouse trail** | Off, Sparks, Bubbles or Footprints |
| **Crawling bugs** | Ants and flies on or off |
| **Weather** | Off, Snow or Rain |
| **Screen mate** | The critter on or off |
| **Screen eater** | The fuzzball on or off |
| **Sound** | All sounds on or off |
| **Keep marks until repaired** | Stop marks from fading |
| **Chain reaction** | Whether six glass breaks drop the pane |
| **Start with Windows** | Launch at sign-in |
| **Repair screen** | Clear every mark and give back anything eaten |
| **Exit** | Quit |

> Settings are stored in `%APPDATA%\GlassBreaker\settings.ini` and generated sounds in `%TEMP%\GlassBreaker`. Nothing else is written, apart from one registry entry if you tick **Start with Windows**.

## Quick Start

### Prerequisites

- **Windows** 10 or 11
- Nothing else &mdash; the C# compiler that builds it ships with Windows

### Run

```bat
git clone https://github.com/mahatab/glass-breaker.git
cd glass-breaker
build.cmd
GlassBreaker.exe
```

A tray icon appears. Click anywhere to break something; right-click the icon for options.

## Building

```bat
build.cmd
```

Produces `GlassBreaker.exe` next to the script. Exit any running copy first, because Windows locks the exe while it runs.

```bat
GlassBreaker.exe --selftest
GlassBreaker.exe --docs
```

**`--selftest`** renders every effect to PNG contact sheets without touching the screen and checks that each sound opens. **`--docs`** regenerates the images in `docs/images` from the real drawing code.

## Architecture

```
build.cmd                 One-line build with the compiler that ships with Windows
└── src/
    ├── App.cs            Tray icon and menu, mouse hook, effect registry, settings, self-test
    ├── Native.cs         Win32 declarations
    ├── Layers.cs         LayerWin (per-pixel-alpha window), Decal (one mark), crack generator
    ├── Desktop.cs        List of visible windows, colour-keyed full-screen overlay
    ├── Sounds.cs         Synthesizes and plays every sound
    ├── Hotkey.cs         Ctrl+Alt+G
    │
    ├── Glass.cs          Click effects: one Decal subclass each
    ├── Bullet.cs
    ├── Paintball.cs
    ├── Ripples.cs
    ├── Fireworks.cs      Fireworks and confetti
    ├── Burn.cs
    ├── Dent.cs
    ├── PaneFall.cs       The chain reaction
    │
    ├── Ambient.cs        IAmbient interface and registry for always-on features
    ├── Trail.cs
    ├── Bugs.cs
    ├── Weather.cs
    ├── Mate.cs
    ├── Eater.cs
    │
    └── DocImages.cs      Generates the README images
```

### Tech Stack

| Layer | Technology |
|-------|-----------|
| Language | C# 5 (the compiler built into Windows) |
| Runtime | .NET Framework 4.x, preinstalled on Windows 10 and 11 |
| UI | Windows Forms tray icon and menu |
| Drawing | GDI+ |
| Marks and creatures | Layered windows with per-pixel alpha (`UpdateLayeredWindow`) |
| Weather and eaten screen | One colour-keyed full-screen window |
| Click detection | Low-level mouse hook (`WH_MOUSE_LL`), listen-only |
| Sound | Synthesized PCM written as WAV, played through MCI so sounds overlap |
| Signing | Azure Artifact Signing, timestamped |

## Documentation

Detailed docs live in the [`docs/`](docs/) directory:

- [Architecture](docs/ARCHITECTURE.md) — windows, the timer loop, how clicks are routed, sound synthesis
- [Adding Effects](docs/ADDING_EFFECTS.md) — step-by-step guide to add a click effect or an always-on feature

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for building, testing, code style, and PR guidelines.

## License

MIT &mdash; see [LICENSE](LICENSE) for details.
