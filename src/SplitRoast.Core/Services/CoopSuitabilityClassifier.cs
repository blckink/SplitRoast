using System.Collections.Generic;
using SplitRoast.Core.Models;

namespace SplitRoast.Core.Services;

/// <summary>
/// Turns the set of Steam store category ids for a game into a SplitRoast
/// suitability verdict. Pure and deterministic, so it is unit-testable without
/// touching the network.
/// </summary>
public static class CoopSuitabilityClassifier
{
    // Steam store category ids (stable public values from the storefront API).
    private const int SinglePlayer = 2;
    private const int MultiPlayer = 1;
    private const int CoOp = 9;
    private const int OnlineCoOp = 38;
    private const int LanCoOp = 39;
    private const int SharedSplitScreen = 24;
    private const int SharedSplitScreenCoOp = 48;
    private const int SharedSplitScreenPvp = 37;
    private const int OnlinePvp = 36;
    private const int LanPvp = 47;
    private const int Pvp = 49;

    /// <summary>
    /// Classifies a game from its Steam category ids. An empty/null set yields
    /// <see cref="GameCoopInfo.Unknown"/>.
    /// </summary>
    public static GameCoopInfo Classify(IReadOnlyCollection<int>? categoryIds)
    {
        if (categoryIds is null || categoryIds.Count == 0)
        {
            return GameCoopInfo.Unknown;
        }

        var ids = new HashSet<int>(categoryIds);

        bool nativeSplit = ids.Contains(SharedSplitScreen)
            || ids.Contains(SharedSplitScreenCoOp)
            || ids.Contains(SharedSplitScreenPvp);
        bool onlineCoop = ids.Contains(OnlineCoOp) || ids.Contains(LanCoOp) || ids.Contains(CoOp);
        bool anyPvp = ids.Contains(OnlinePvp) || ids.Contains(LanPvp) || ids.Contains(Pvp);
        bool multiplayer = ids.Contains(MultiPlayer) || onlineCoop || anyPvp;

        // Order matters: a game that already does local split-screen doesn't need us,
        // even if it also has online co-op.
        if (nativeSplit)
        {
            return new GameCoopInfo(
                CoopSuitability.NativeSplitScreen,
                "Native split-screen",
                "Already has local split-screen",
                "Steam lists this game with built-in shared / split-screen, so you can play couch co-op without SplitRoast.");
        }

        if (onlineCoop)
        {
            return new GameCoopInfo(
                CoopSuitability.Recommended,
                "Online co-op",
                "Great fit for SplitRoast",
                "This game has online / LAN co-op, so SplitRoast can run two instances and bring them together on one screen.");
        }

        if (multiplayer)
        {
            return new GameCoopInfo(
                CoopSuitability.Maybe,
                "Multiplayer",
                "Might work",
                "Steam lists this as multiplayer but without a co-op mode. SplitRoast can only help if two instances are able to connect — worth a test.");
        }

        if (ids.Contains(SinglePlayer))
        {
            return new GameCoopInfo(
                CoopSuitability.NotSuitable,
                "Single-player",
                "Not a SplitRoast game",
                "Steam lists this as single-player only. There's no second-instance multiplayer for SplitRoast to connect.");
        }

        return GameCoopInfo.Unknown;
    }
}
