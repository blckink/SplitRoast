using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SplitRoast.Core.Abstractions;
using SplitRoast.Core.Models;

namespace SplitRoast.Steam;

/// <summary>
/// Default <see cref="ISteamLibraryScanner"/>. Locates Steam, walks every library
/// folder and parses each app manifest. Runs the (IO-bound) work on a background
/// thread so the UI thread is never blocked.
/// </summary>
public sealed class SteamLibraryScanner : ISteamLibraryScanner
{
    public Task<IReadOnlyList<SteamGame>> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    private static IReadOnlyList<SteamGame> Scan(CancellationToken cancellationToken)
    {
        string? steamPath = SteamLocator.FindSteamPath();
        if (steamPath is null)
        {
            return Array.Empty<SteamGame>();
        }

        var games = new List<SteamGame>();

        foreach (string library in LibraryFoldersParser.GetLibraryPaths(steamPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string steamapps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamapps))
            {
                continue;
            }

            foreach (string manifest in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // One bad manifest must never abort the whole scan.
                try
                {
                    SteamGame? game = AppManifestParser.TryParse(manifest, library);
                    if (game is not null && !IsNonGameApp(game))
                    {
                        games.Add(game);
                    }
                }
                catch (IOException)
                {
                    // File locked/unreadable: skip it.
                }
            }
        }

        // De-duplicate (a game can appear in more than one library after moves)
        // and present alphabetically for a tidy grid.
        return games
            .GroupBy(g => g.AppId)
            .Select(g => g.First())
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    // Steam installs non-game "tools" (redistributable bundles, Linux runtimes,
    // Proton) as regular apps under steamapps. They carry no store artwork and can
    // never be launched as a split-screen game, so they only show up as blank,
    // unlaunchable tiles - filter them out by their well-known app ids and names.
    private static readonly HashSet<uint> NonGameAppIds = new()
    {
        228980,   // Steamworks Common Redistributables
        1070560,  // Steam Linux Runtime 1.0 (scout)
        1391110,  // Steam Linux Runtime 2.0 (soldier)
        1628350,  // Steam Linux Runtime 3.0 (sniper)
        1493710,  // Proton Experimental
    };

    private static bool IsNonGameApp(SteamGame game)
    {
        if (NonGameAppIds.Contains(game.AppId))
        {
            return true;
        }

        string name = game.Name;
        return name.StartsWith("Proton", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Steam Linux Runtime", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Steamworks Common Redistributable", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Steamworks SDK Redist", StringComparison.OrdinalIgnoreCase);
    }
}
