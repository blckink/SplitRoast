# Third-party notices

SplitRoast stands on the shoulders of some genuinely excellent open-source work.
This file lists every third-party component we **ship** or **build against**, what
it is, and the license it comes under. Nothing here is ours; all credit (and all
copyright) stays with the original authors.

If you redistribute SplitRoast, keep this file with it — that's literally the deal
these licenses ask for, and it's a small price for not having to write a Steam
emulator yourself.

---

## Components we redistribute (shipped in the installer / portable build)

### gbe_fork (Goldberg Emulator fork)
- **What:** A maintained fork of the Goldberg Steam Emulator. SplitRoast uses it,
  **unmodified**, as drop-in `steam_api(64).dll` files next to a mirrored copy of
  a game so a second instance can run from a single Steam account for local co-op.
- **Upstream:** https://github.com/Detanup01/gbe_fork
- **License:** **LGPL-3.0**
- **How we comply:** We ship it as separate, unmodified DLLs (dynamic use, no
  static linking, no patches). The emulator's own `LICENSE` is bundled alongside
  the binaries in the release, and the source is available upstream at the link
  above. The DLLs are **not** committed to this repository; they are downloaded at
  build time by [`redist/fetch-goldberg.ps1`](redist/fetch-goldberg.ps1).

### MinHook
- **What:** A minimalistic x86/x64 API-hooking library. Compiled into SplitRoast's
  native XInput proxy DLL so the proxy can intercept XInput calls cleanly.
- **Upstream:** https://github.com/TsudaKageyu/minhook
- **Vendored at:** [`native/SplitRoast.XInputProxy/minhook/`](native/SplitRoast.XInputProxy/minhook/)
- **License:** **BSD-2-Clause** (includes Hacker Disassembler Engine, also BSD-2-Clause).
  Full text: [`native/SplitRoast.XInputProxy/minhook/LICENSE.txt`](native/SplitRoast.XInputProxy/minhook/LICENSE.txt)
- **How we comply:** We keep the original copyright notice and license text intact
  and reproduce the attribution here for the binary distribution.

---

## Prior art & inspiration (referenced with thanks)

These are the projects SplitRoast learned the most from. We wrote our own
implementation rather than reuse their code, but the ideas and the inspiration are
theirs — listed here with full credit and genuine gratitude.

### Nucleus Co-op / SplitScreen.Me
- **What:** The OG of bring-your-own-split-screen on PC. The whole category exists
  because these folks did the hard, thankless plumbing first.
- **Upstream:** https://github.com/SplitScreen-Me/splitscreenme-nucleus
- **License:** GPL-3.0
- **Relationship to SplitRoast:** Conceptual inspiration. A couple of comments in our
  code point back to Nucleus to explain a design choice. If you want the mature,
  800-games-strong tool, theirs is fantastic — go grab it.

### ProtoInput
- **What:** Input isolation / multi-keyboard-and-mouse hooking, by Ilyaki.
- **Upstream:** https://github.com/SplitScreen-Me/splitscreenme-protoInput
- **License:** GPL-3.0
- **Relationship to SplitRoast:** Reference for the "make each instance only see one
  controller" problem. Our XInput proxy is an independent, much narrower take on it.

### x360ce
- **What:** XInput wrapper / controller emulator.
- **Upstream:** https://github.com/x360ce/x360ce
- **License:** See upstream repository.
- **Relationship to SplitRoast:** Reference only; not used or shipped.

---

## SplitRoast itself

SplitRoast's own source code is licensed under **GPL-3.0** — see [`LICENSE`](LICENSE).
