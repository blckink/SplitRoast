# SplitPlay

A clean, modern Windows app for playing online co-op Steam games in **vertical or
horizontal split-screen on a single PC with two controllers**.

SplitPlay is a from-scratch reimagining of the Nucleus Co-op idea (the original
fork lives under `../Master` as reference). The goals are a friendlier UI, a
modular and individually-updatable codebase, and **per-game configuration through
the interface** instead of downloading and managing handler script files.

> Status: **MVP foundation.** The full UI, Steam detection, controller detection
> and per-game profiles are in place. The launch engine is a clearly-defined
> interface with a preview stub (`StubLaunchEngine`); the real, OS-level launch
> pipeline drops in behind the same interface next.

## Scope (MVP)

- Exactly **2 players**, each using a **controller** (XInput / Xbox-style).
- Split the screen **vertically (left/right)** or **horizontally (top/bottom)**.
- Keyboard + mouse players are intentionally **not** supported.

## Tech stack

- **.NET 8** / **WPF** (MVVM), C# with nullable reference types enabled.
- `Microsoft.Extensions.DependencyInjection` for composition.
- No external UI frameworks; the dark theme is a single, retunable resource file.

## Solution layout

The solution is split into focused modules so each concern can evolve and be
updated on its own. Dependencies only ever point *toward* `Core`.

| Project | Target | Responsibility |
|---|---|---|
| `SplitPlay.Core` | `net8.0` | Domain models, abstractions (interfaces), pure layout math. No UI/OS deps — unit-testable. |
| `SplitPlay.Steam` | `net8.0-windows` | Locate Steam, parse libraries (`libraryfolders.vdf`) and manifests (`appmanifest_*.acf`), resolve artwork (local cache → Steam CDN fallback). |
| `SplitPlay.Input` | `net8.0-windows` | XInput controller discovery + connect/disconnect monitoring. |
| `SplitPlay.Launch` | `net8.0-windows` | Win32 borderless window placement + the launch engine (MVP stub). |
| `SplitPlay.App` | `net8.0-windows` | WPF presentation: views, view models, DI composition root. |

```
SplitPlay/
├─ SplitPlay.sln
├─ Directory.Build.props        # shared compiler settings
└─ src/
   ├─ SplitPlay.Core/           # Models/, Abstractions/, Services/
   ├─ SplitPlay.Steam/          # Vdf/, scanner, artwork
   ├─ SplitPlay.Input/          # Native/XInput, gamepad service
   ├─ SplitPlay.Launch/         # Native/User32, WindowManager, StubLaunchEngine
   └─ SplitPlay.App/            # Mvvm/, Services/, ViewModels/, Views/, Themes/
```

## Architecture notes

- **MVVM, view-first templating.** `App.xaml` maps each page view model to its
  view via `DataTemplate`s, so navigation is just swapping the bound view model.
- **Decoupling via interfaces.** View models depend on `Core` abstractions
  (`ISteamLibraryScanner`, `IGamepadService`, `ILaunchEngine`, …). Swapping an
  implementation is a one-line change in `AppBootstrapper`.
- **Per-game profiles** are stored as small JSON files in
  `%AppData%/SplitPlay/profiles/{appid}.json`, written atomically.
- **Responsive grid.** The games grid uses a `WrapPanel` with horizontal
  scrolling disabled, so tiles reflow by window width with uniform spacing and
  never produce a horizontal scrollbar.
- **Pixel-accurate layout.** The app is Per-Monitor-v2 DPI aware; the pure
  `SplitLayoutCalculator` tiles a monitor's bounds exactly (no rounding gaps).

## How a session is configured

1. **Games** page scans Steam and shows installed games as cover tiles.
2. Click a game → detail page: choose **split orientation**, **display**, and a
   **controller per player** (validated to be distinct), with a live preview.
3. **Start** builds a `LaunchRequest` (regions + controller routing) and hands it
   to the `ILaunchEngine`.

## Roadmap (next)

- Real launch engine behind `ILaunchEngine`:
  - executable resolution + second-instance preparation,
  - per-instance XInput routing (one pad → one window),
  - borderless placement via `WindowManager`, lifecycle management.
- Instance strategies (`InstanceStrategy`): mirrored copy + emulator
  (Goldberg/Nemirtingas) and dual real Steam accounts.
- Auto-detection of per-game settings (replacing handler files entirely).
- More than two players, richer controller info, themes.

## Building

Requires the **.NET 8 SDK** and **Windows** (WPF + Win32). From the `SplitPlay`
folder:

```powershell
dotnet build SplitPlay.sln
dotnet run --project src/SplitPlay.App/SplitPlay.App.csproj
```
