<div align="center">

# 🎮 SplitRoast

**Couch co-op for Steam games that never asked to be couch co-op.**

Two controllers, one PC, one screen sliced cleanly in half — for online co-op
games that stubbornly refuse to do split-screen themselves.

[![SplitRoast Build](https://github.com/blckink/SplitRoast/actions/workflows/splitroast-build.yml/badge.svg)](https://github.com/blckink/SplitRoast/actions/workflows/splitroast-build.yml)
![Platform: Windows](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue)
![Status: MVP](https://img.shields.io/badge/status-early%20MVP-orange)

</div>

---

## What is this?

SplitRoast launches a game **twice** on a single PC, tells each copy "you only get
*this one* controller, ignore the others," makes both windows borderless, and tiles
them into a tidy split — vertical or horizontal. Plug in two pads, hit **Start**,
and you and a friend get local split-screen on a game that was only ever built for
two separate machines on a network.

It's the [Nucleus Co-op](https://github.com/SplitScreen-Me/splitscreenme-nucleus)
idea, rebuilt from scratch with a modern .NET 8 / WPF stack, a friendlier UI, and
**per-game settings configured in the app** instead of hunting down handler scripts.

> **Reality check.** SplitRoast is an early MVP. It compiles, it runs, the UI is real,
> controller isolation actually works — and so far it's been properly verified on
> exactly **one** game. We're not going to pretend otherwise. The architecture is
> built to grow; the games list is built to embarrass us until it does. 😅

---

## Does it actually work? (an honest table)

| Thing | Status | Notes |
|---|---|---|
| Scan installed Steam games, show cover tiles | ✅ Works | Parses `libraryfolders.vdf` + `appmanifest_*.acf`, art from local cache → Steam CDN. |
| Per-game profile (split, display, controller routing) | ✅ Works | Stored as JSON in `%AppData%/SplitRoast/profiles/{appid}.json`. |
| Controller detection + rumble test | ✅ Works | XInput discovery with live connect/disconnect. |
| Launch game ×2, borderless, tiled into the split | ✅ Works | Real launch engine + Win32 window placement, pixel-accurate, DPI-aware. |
| **Per-window controller isolation** | ✅ Works | Native XInput proxy: one pad → one window, even in the background. KB+M stay free. |
| Single-account co-op via Steam emulator | 🟡 Wired up | Bundles **gbe_fork**; broader game coverage still being proven out. |
| Verified end-to-end on real games | 🟡 **One** | The honest number. More to come — see the roadmap. |
| Keyboard + mouse players | ❌ Not supported | By design, for now. Two controllers only. |
| 3+ players | ❌ Not yet | MVP is exactly 2. |
| Handler scripts / 800-game library | ❌ Not a thing here | That's Nucleus's superpower. We went a different route (see below). |

**Scope of the MVP:** exactly **2 players**, each on an **XInput controller**, split
**vertically (left/right)** or **horizontally (top/bottom)**.

---

## Screenshots

<div align="center">

| Games | Game detail | Controls |
|---|---|---|
| ![Games](docs/mockups/01-games.png) | ![Detail](docs/mockups/02-detail.png) | ![Controls](docs/mockups/03-controls.png) |

</div>

---

## How the magic trick works

No real magic, just a few well-aimed hacks:

- **Controller isolation via a native XInput proxy.** A tiny C++ DLL
  (`native/SplitRoast.XInputProxy`, built with [MinHook](https://github.com/TsudaKageyu/minhook))
  is dropped next to the game (originals backed up, restored crash-safe). Each
  instance launches with a `SPLITROAST_XINPUT_INDEX` env var, so the proxy exposes
  **only that one physical pad** as index 0 and reports the rest as disconnected.
  It works at the API level, so isolation holds even when a window is in the
  background — and it never touches keyboard or mouse.
- **Borderless tiling.** The launch engine finds each game window, strips its
  border, and positions it to fill exactly its half of the monitor. Per-Monitor-v2
  DPI aware, so no rounding gaps and no "why is it 1px off" rage.
- **Second instance from one Steam account.** SplitRoast ships the unmodified
  [gbe_fork](https://github.com/Detanup01/gbe_fork) Steam emulator so a mirrored
  copy of the game can run a second instance for local co-op.

> SplitRoast does **not** add multiplayer to single-player games. The game already
> needs some form of online/LAN co-op — we just trick it into running twice on one
> couch.

---

## Requirements

- **Windows 10 / 11** (this is Win32 + WPF; there is no Linux/Mac build and there
  won't be one for a while — sorry, penguins).
- Two **XInput / Xbox-style controllers**.
- The one-command installer pulls in everything else (.NET 8, the build tools,
  Inno Setup) for you.

---

## Quick start

### Easiest: one file, no fuss

Grab the latest **`SplitRoastSetup.exe`** from
[Actions → SplitRoast Build → Artifacts](https://github.com/blckink/SplitRoast/actions/workflows/splitroast-build.yml)
(or a Releases page once we cut one). It's self-contained: no .NET, no C++ runtime,
no tools on the target PC. Double-click, done.

### Build the installer yourself

```cmd
installer\build-release.cmd
```

Auto-installs every needed tool via `winget` (.NET 8 SDK, Visual C++ Build Tools,
Inno Setup), builds the native proxy, publishes self-contained, and spits out
`installer\output\SplitRoastSetup.exe`. See [installer/README.md](installer/README.md).

### From source (development)

Needs the **.NET 8 SDK** and **Windows**. From the repo root:

```powershell
dotnet build SplitRoast.sln
dotnet run --project src/SplitRoast.App/SplitRoast.App.csproj
```

Or just double-click **`setup.cmd`** — it installs the SDK if missing, then builds
and launches the app.

### Building the native XInput proxy

Controller isolation needs the small native proxy DLL. Requires the
**"Desktop development with C++"** workload (or standalone C++ Build Tools). Build
once (re-run only if `proxy.cpp` changes):

```cmd
native\build-proxy.cmd
```

Produces `native\bin\{x64,x86}\SplitRoast.XInputProxy.dll`; the app build copies them
into its output. **If the proxy is missing, the app still runs** — it just reports
controller isolation as off.

---

## Architecture

The solution is split into focused modules; dependencies only ever point *toward*
`Core`, which is pure and unit-testable.

| Project | Target | Responsibility |
|---|---|---|
| `SplitRoast.Core` | `net8.0` | Domain models, abstractions, pure layout math. No UI/OS deps. |
| `SplitRoast.Steam` | `net8.0-windows` | Locate Steam, parse libraries/manifests, resolve artwork. |
| `SplitRoast.Input` | `net8.0-windows` | XInput discovery + connect/disconnect monitoring. |
| `SplitRoast.Launch` | `net8.0-windows` | Process launch, window placement, isolation, the real launch engine. |
| `SplitRoast.TestTarget` | `net8.0-windows` | Tiny WinForms test window (placeholder + live controller readout). |
| `SplitRoast.App` | `net8.0-windows` | WPF presentation: views, view models, DI composition root. |

```
.
├─ SplitRoast.sln
├─ Directory.Build.props        # shared compiler settings
├─ native/                      # native XInput proxy (C++), built via build-proxy.cmd
├─ installer/                   # one-command release → SplitRoastSetup.exe
├─ redist/                      # fetch-goldberg.ps1 (downloads gbe_fork at build time)
├─ docs/                        # mockups + render script
└─ src/                         # the six projects above
```

- **MVVM, view-first templating.** `App.xaml` maps each page view model to its view
  via `DataTemplate`s; navigation is just swapping the bound view model.
- **Decoupling via interfaces.** View models depend on `Core` abstractions
  (`ISteamLibraryScanner`, `IGamepadService`, `ILaunchEngine`, …); swapping an
  implementation is a one-line change in `AppBootstrapper`.

---

## Roadmap

Roughly in order of "things that stop us being a one-game tech demo":

- **More verified games.** The single most important number on this page.
- Runtime-injection fallback for the rare games that load XInput in a way the
  folder proxy can't shadow (hardcoded System32 path, etc.).
- Instance lifecycle: track launched processes, clean teardown, relaunch.
- Smarter executable / second-instance handling (Steam launch config, launcher →
  game hand-off, single-instance mutex strategies).
- Instance strategies: mirrored copy + emulator and dual real Steam accounts.
- Auto-detection of per-game settings (the goal: retire handler files entirely).
- More than two players; richer controller info (battery / live input); themes.

---

## ⚖️ Licensing & credits

We wanted to keep this tidy and make sure everyone gets their due, so here's how the
pieces fit together:

- **SplitRoast's own code** is written from scratch and licensed under
  **[GPL-3.0](LICENSE)** — happily in the spirit of the co-op-tooling community it
  grew up admiring.
- **The third-party pieces we ship** are listed, with their licenses and source, in
  **[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)**:
  - **gbe_fork** (Steam emulator) — **LGPL-3.0**, used unmodified, license + source
    pointer bundled alongside the binaries.
  - **MinHook** — **BSD-2-Clause**, compiled into the proxy, attribution preserved.
- **Nucleus, ProtoInput and x360ce** are the projects we learned the most from. We
  ended up writing our own implementation rather than reusing their code, but the
  inspiration is entirely theirs — they're credited below with genuine thanks.
- **Play games you actually own.** SplitRoast is just a convenience layer for running
  *your* games in split-screen on *your* PC.

If you're one of these projects, or a rights-holder, and you'd like a credit tweaked
or some wording changed, just open an issue — we'll happily sort it out.

---

## ⚠️ Disclaimer (a.k.a. the part where we duck)

SplitRoast is a free, open-source hobby project, shipped **"as is"**, with
**absolutely no warranty** — express or implied (the [GPL-3.0](LICENSE) spells this
out in sections 15–16, and we mean it).

- **Use it at your own risk.** The maintainers and contributors are **not liable**
  for anything that happens because you ran it — broken installs, corrupted saves,
  misbehaving games, account issues, a PC that decides to take a nap, or your dinner
  getting cold. If it breaks, you get to keep both pieces.
- **You are responsible for how you use it.** Complying with the terms of service,
  EULAs, and licenses of the games, platforms (Steam included), and bundled tools you
  point it at is **on you**, not us. We provide a tool; what you do with it is your
  call and your responsibility.
- **Not affiliated with anyone.** SplitRoast is not affiliated with, endorsed by, or
  connected to Valve, Steam, Microsoft, or any game publisher. All trademarks belong
  to their respective owners.
- **Play games you own.** Same as above, louder: this is for *legitimately owned*
  games on hardware you control. It will never help you pirate anything.

Short version: it's a fun toy for couch co-op, not a contract. Be sensible. 🫡

---

## 🙏 Standing on the shoulders of giants (who did the hard part first)

Honestly, SplitRoast is a tiny hobby tool we built so we could mess around in
split-screen on the couch without the hassle. It only got off the ground because
other people solved the genuinely hard problems first. Big respect and a heartfelt
thank-you to:

- **[Nucleus Co-op / SplitScreen.Me](https://github.com/SplitScreen-Me/splitscreenme-nucleus)**
  — the reason this entire category exists. 800+ games, years of thankless plumbing,
  and the patience of saints. If you want the mature, battle-tested tool *today*, go
  use theirs. We're the cheeky new kid; they're the institution.
- **[ProtoInput](https://github.com/SplitScreen-Me/splitscreenme-protoInput)** by
  Ilyaki — the input-isolation wizardry that made us believe "one pad per window"
  was even possible.
- **[Goldberg Emulator](https://gitlab.com/Mr_Goldberg/goldberg_emulator)** &
  **[gbe_fork](https://github.com/Detanup01/gbe_fork)** — for letting a second
  instance exist without selling a kidney for a second copy of every game.
- **[MinHook](https://github.com/TsudaKageyu/minhook)** by Tsuda Kageyu — 200 lines
  of C that quietly do the scariest part of the job.

Full attribution and licenses: **[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)**.

---

## Contributing

Bug reports, game-verification reports ("it works / it doesn't on *X*"), and PRs are
all welcome. Start with **[CONTRIBUTING.md](CONTRIBUTING.md)** and please be excellent
to each other — see the **[Code of Conduct](CODE_OF_CONDUCT.md)**.

## License

**[GPL-3.0](LICENSE)** © SplitRoast contributors. Third-party components retain their
own licenses — see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
