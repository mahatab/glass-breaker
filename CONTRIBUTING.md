# Contributing to Windows Tray Toys

Thanks for wanting to make screens look more broken. New effects and creatures are especially
welcome, and each one is a single class plus one line: see [Adding Effects](docs/ADDING_EFFECTS.md).

## Development setup

You need Windows 10 or 11 and nothing else. The C# compiler that builds the project ships with
Windows.

```bat
git clone https://github.com/mahatab/windows-tray-toys.git
cd windows-tray-toys
build.cmd
WindowsTrayToys.exe
```

Exit any running copy before building: Windows locks the exe while it runs.

## Testing

There is no unit test project. Instead the app can exercise itself:

```bat
WindowsTrayToys.exe --selftest
```

This writes three files to the current folder without touching the screen:

| File | What to check |
|------|---------------|
| `selftest.log` | No line contains `FAILED`, and every sound shows `open rc=0` |
| `selftest_effects.png` | Every click effect at 0.12 s, 0.45 s and 5 s after the click |
| `selftest_ambient.png` | One sample of each always-on feature |

The self-test proves the drawing code runs and looks right. It cannot prove how something
behaves live, so also run the app and try your change over real windows, on a light and a dark
background, before opening a PR.

## Code style

- **C# 5 only.** The built-in compiler does not support newer syntax: no string interpolation
  (`$"..."`), no `?.`, no expression-bodied members, no `out var`, no `nameof`.
- **No dependencies.** Only `System.Windows.Forms` and `System.Drawing`. No NuGet packages.
- **No assets.** Generate images and sounds in code.
- **Scale everything.** Multiply sizes by the display scale `s` so the toy looks the same on
  high-DPI displays.
- **Dispose GDI objects.** Wrap every `Pen`, `Brush`, `GraphicsPath` and `Bitmap` in `using`.
- **Stay click-through.** Overlays must never take focus or swallow a click.
- **Comments say why.** Explain the reason for anything non-obvious; skip comments that restate
  the code.

## Pull requests

- One feature or fix per PR, on its own branch.
- Say what the change does and how you checked it. A screenshot or the relevant part of a
  self-test image helps a lot for visual changes.
- If you add an effect or feature, update the lists in `README.md`, and run
  `WindowsTrayToys.exe --docs` if the README images should show it.
- Do not commit `WindowsTrayToys.exe` or the `selftest*` files; both are ignored.

## Ideas

Looking for something to build? Open an issue to talk it over first if it is large. Things that
would fit well: more click effects, more creatures, a settings window, per-monitor display
scaling, and volume control.

## License

By contributing you agree that your contributions are licensed under the [MIT License](LICENSE).
