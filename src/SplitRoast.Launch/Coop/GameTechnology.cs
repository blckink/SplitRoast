using System;

namespace SplitRoast.Launch.Coop;

/// <summary>
/// The technologies a game ships, detected from characteristic files in its install
/// folder. This is the local, no-network equivalent of SteamDB's "Detected
/// Technologies" (e.g. Len's Island: Unity Engine, FishNet, Mono, Steamworks) - and
/// more reliable, because we read the actual binaries rather than scrape a page.
///
/// It drives strategy without any per-game handler:
///   - engine    -&gt; window/borderless arguments and input expectations;
///   - scripting -&gt; where a single-instance lock or config tends to live;
///   - net SDK   -&gt; whether two local copies pair over the game's OWN networking
///                  (FishNet/Mirror/Photon/Netcode connect by IP, so localhost
///                  host+join works) or need a platform emulator for identity only;
///   - platform  -&gt; which identity emulator a second instance needs.
/// </summary>
[Flags]
public enum GameTechnology
{
    None = 0,

    // Engines
    UnityEngine = 1 << 0,
    UnrealEngine = 1 << 1,
    GodotEngine = 1 << 2,

    // Unity scripting backends
    Mono = 1 << 3,
    Il2Cpp = 1 << 4,

    // Platform / identity SDKs
    SteamworksSdk = 1 << 5,
    EpicOnlineServices = 1 << 6,
    GogGalaxy = 1 << 7,

    // In-game networking middleware (connects by IP -> localhost co-op works,
    // the platform SDK is then only used for identity/lobby).
    FishNet = 1 << 8,
    Mirror = 1 << 9,
    Photon = 1 << 10,
    NetcodeForGameObjects = 1 << 11,
}
