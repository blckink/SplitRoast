using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SplitRoast.Launch.Coop;

/// <summary>
/// Inspects a game's install folder to auto-derive a <see cref="CoopRecipe"/>:
/// the engine, the executable and the Steam API DLLs. This is what lets the tool
/// support many games automatically instead of needing a hand-written handler.
/// </summary>
public static class GameAnalyzer
{
    public static CoopRecipe Analyze(uint appId, string installDir, string exePath)
    {
        EngineType engine = DetectEngine(installDir);
        (string? api64, string? api32) = FindSteamApi(installDir);
        bool hasSteamDrm = SteamStubDetector.IsSteamDrmProtected(exePath);
        OnlineSdk sdks = DetectOnlineSdks(installDir, api64, api32);
        GameTechnology tech = DetectTechnologies(installDir, engine, sdks);

        return new CoopRecipe
        {
            AppId = appId,
            SourceInstallDir = installDir,
            SourceExePath = exePath,
            Engine = engine,
            SteamApi64RelPath = api64,
            SteamApi32RelPath = api32,
            DetectedSdks = sdks,
            DetectedTech = tech,
            HasSteamDrm = hasSteamDrm
        };
    }

    private static EngineType DetectEngine(string installDir)
    {
        // Unity: ships UnityPlayer.dll and/or a "<Game>_Data" folder with the
        // engine's globalgamemanagers asset.
        if (File.Exists(Path.Combine(installDir, "UnityPlayer.dll")))
        {
            return EngineType.Unity;
        }

        try
        {
            foreach (string dir in Directory.EnumerateDirectories(installDir, "*_Data"))
            {
                if (File.Exists(Path.Combine(dir, "globalgamemanagers")) ||
                    File.Exists(Path.Combine(dir, "data.unity3d")))
                {
                    return EngineType.Unity;
                }
            }

            // Unreal: a Binaries/Win64 layout under an Engine or game module.
            if (Directory.EnumerateDirectories(installDir, "Binaries", SearchOption.AllDirectories).Any())
            {
                return EngineType.Unreal;
            }
        }
        catch (IOException)
        {
            // Ignore unreadable folders during detection.
        }

        return EngineType.Unknown;
    }

    private static (string? Api64, string? Api32) FindSteamApi(string installDir)
    {
        string? api64 = FindFirstRelative(installDir, "steam_api64.dll");
        string? api32 = FindFirstRelative(installDir, "steam_api.dll");
        return (api64, api32);
    }

    /// <summary>
    /// Detects which online/multiplayer SDKs the game ships, from the well-known
    /// runtime DLL names each one drops next to the game. This is the signal that
    /// tells the engine which network emulator a second instance needs - no per-game
    /// handler required.
    /// </summary>
    private static OnlineSdk DetectOnlineSdks(string installDir, string? steamApi64, string? steamApi32)
    {
        OnlineSdk sdks = OnlineSdk.None;

        if (steamApi64 is not null || steamApi32 is not null)
        {
            sdks |= OnlineSdk.Steam;
        }

        // Epic Online Services ships EOSSDK-Win64-Shipping.dll / EOSSDK-Win32-Shipping.dll.
        if (ContainsAnyFile(installDir, "EOSSDK-Win64-Shipping.dll", "EOSSDK-Win32-Shipping.dll"))
        {
            sdks |= OnlineSdk.Epic;
        }

        // GOG Galaxy ships Galaxy64.dll / Galaxy.dll (the peer/identity SDK).
        if (ContainsAnyFile(installDir, "Galaxy64.dll", "Galaxy.dll"))
        {
            sdks |= OnlineSdk.Galaxy;
        }

        return sdks;
    }

    private static bool ContainsAnyFile(string root, params string[] fileNames)
    {
        foreach (string name in fileNames)
        {
            if (FindFirstRelative(root, name) is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds the game's "detected technologies" set from one pass over the install
    /// folder - the local, no-network analogue of SteamDB's tech list. Drives launch
    /// strategy (engine quirks, scripting backend, in-game networking vs. emulator)
    /// with no per-game handler. Whatever we cannot positively identify is simply
    /// left unset rather than guessed.
    /// </summary>
    private static GameTechnology DetectTechnologies(string installDir, EngineType engine, OnlineSdk sdks)
    {
        GameTechnology tech = GameTechnology.None;

        // Carry over what the engine/SDK passes already established.
        if (engine == EngineType.Unity) { tech |= GameTechnology.UnityEngine; }
        if (engine == EngineType.Unreal) { tech |= GameTechnology.UnrealEngine; }
        if (sdks.HasFlag(OnlineSdk.Steam)) { tech |= GameTechnology.SteamworksSdk; }
        if (sdks.HasFlag(OnlineSdk.Epic)) { tech |= GameTechnology.EpicOnlineServices; }
        if (sdks.HasFlag(OnlineSdk.Galaxy)) { tech |= GameTechnology.GogGalaxy; }

        // One depth-limited sweep; match by lowercase file name. Managed assemblies
        // live a few levels down (e.g. <Game>_Data/Managed), hence depth 5.
        HashSet<string> names = EnumerateFileNames(installDir, maxDepth: 5);

        bool Has(string name) => names.Contains(name);
        bool HasPrefix(string prefix) => names.Any(n => n.StartsWith(prefix, StringComparison.Ordinal));

        // Scripting backend (Unity).
        if (Has("gameassembly.dll")) { tech |= GameTechnology.Il2Cpp; }
        if (Has("assembly-csharp.dll") || HasPrefix("mono-")) { tech |= GameTechnology.Mono; }

        // Godot ships a .pck pack next to the runner.
        if (HasPrefix("godot") || names.Any(n => n.EndsWith(".pck", StringComparison.Ordinal)))
        {
            tech |= GameTechnology.GodotEngine;
        }

        // Platform SDKs that may only appear as managed wrappers.
        if (Has("steamworks.net.dll") || Has("facepunch.steamworks.dll")) { tech |= GameTechnology.SteamworksSdk; }

        // In-game networking middleware (these connect by IP -> localhost co-op).
        if (Has("fishnet.runtime.dll") || HasPrefix("fishnet")) { tech |= GameTechnology.FishNet; }
        if (Has("mirror.dll") || HasPrefix("mirror.")) { tech |= GameTechnology.Mirror; }
        if (HasPrefix("photon")) { tech |= GameTechnology.Photon; }
        if (HasPrefix("unity.netcode")) { tech |= GameTechnology.NetcodeForGameObjects; }

        return tech;
    }

    /// <summary>Collects lowercase file names under <paramref name="root"/> once.</summary>
    private static HashSet<string> EnumerateFileNames(string root, int maxDepth)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = maxDepth,
            IgnoreInaccessible = true
        };

        try
        {
            foreach (string file in Directory.EnumerateFiles(root, "*", options))
            {
                names.Add(Path.GetFileName(file).ToLowerInvariant());
            }
        }
        catch (IOException)
        {
            // Partial detection is fine; never let a quirky folder abort analysis.
        }

        return names;
    }

    private static string? FindFirstRelative(string root, string fileName)
    {
        try
        {
            string? hit = Directory
                .EnumerateFiles(root, fileName, SearchOption.AllDirectories)
                .FirstOrDefault();
            return hit is null ? null : Path.GetRelativePath(root, hit);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
