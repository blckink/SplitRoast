namespace SplitRoast.Core.Models;

/// <summary>
/// How well a game suits SplitRoast's "run two instances side by side" approach,
/// inferred from the co-op / multiplayer categories Steam lists for it. This is a
/// hint to the user, not a guarantee.
/// </summary>
public enum CoopSuitability
{
    /// <summary>No category data available (offline, or Steam listed none).</summary>
    Unknown,

    /// <summary>Online / LAN co-op: SplitRoast's sweet spot.</summary>
    Recommended,

    /// <summary>Multiplayer, but no co-op listed: might work, might not.</summary>
    Maybe,

    /// <summary>Already ships local / shared split-screen: you don't need SplitRoast.</summary>
    NativeSplitScreen,

    /// <summary>Single-player only: nothing for a second instance to connect to.</summary>
    NotSuitable
}

/// <summary>
/// A human-friendly verdict on whether SplitRoast makes sense for a given game,
/// produced by <see cref="Services.CoopSuitabilityClassifier"/> from Steam's store
/// categories. <see cref="ShortLabel"/> is for a compact tile badge; the headline
/// and detail are for the game's detail page.
/// </summary>
public sealed record GameCoopInfo(
    CoopSuitability Suitability,
    string ShortLabel,
    string Headline,
    string Detail)
{
    /// <summary>The neutral "we couldn't determine this" verdict.</summary>
    public static GameCoopInfo Unknown { get; } = new(
        CoopSuitability.Unknown,
        string.Empty,
        "Co-op info unavailable",
        "Couldn't read Steam's co-op categories for this game (offline, or none were listed). You can still give it a try.");
}
