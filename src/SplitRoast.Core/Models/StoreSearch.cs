using System.Collections.Generic;

namespace SplitRoast.Core.Models;

/// <summary>Which co-op category to filter the store search by.</summary>
public enum StoreCoopFilter
{
    /// <summary>Online Co-op (Steam category 38) - SplitRoast's sweet spot.</summary>
    OnlineCoop,

    /// <summary>LAN Co-op (Steam category 39).</summary>
    LanCoop,

    /// <summary>Any co-op (Steam category 9), online or local.</summary>
    AnyCoop,

    /// <summary>Shared / split-screen (Steam category 24): native couch co-op.</summary>
    SplitScreen
}

/// <summary>An optional genre filter. <see cref="All"/> applies no genre tag.</summary>
public enum StoreGenre
{
    All,
    Action,
    Adventure,
    Rpg,
    Strategy,
    Simulation,
    Indie,
    Casual,
    Racing,
    Sports,
    MassivelyMultiplayer
}

/// <summary>How to order the store search results.</summary>
public enum StoreSortOrder
{
    /// <summary>Steam's default relevance ordering.</summary>
    Relevance,

    /// <summary>Most positively reviewed first.</summary>
    TopReviews,

    /// <summary>Most recently released first.</summary>
    Newest,

    /// <summary>Alphabetical by name.</summary>
    NameAsc
}

/// <summary>
/// The parameters for one store-search request. Immutable; the view model builds a
/// fresh query whenever the filters or paging change.
/// </summary>
public sealed class StoreSearchQuery
{
    /// <summary>Free-text search term (optional).</summary>
    public string? Term { get; init; }

    public StoreCoopFilter Coop { get; init; } = StoreCoopFilter.OnlineCoop;

    public StoreGenre Genre { get; init; } = StoreGenre.All;

    public StoreSortOrder Sort { get; init; } = StoreSortOrder.TopReviews;

    /// <summary>Zero-based index of the first result to return (for paging).</summary>
    public int Start { get; init; }

    /// <summary>How many results to return in this page.</summary>
    public int Count { get; init; } = 50;
}

/// <summary>One page of store-search results plus the total match count for paging.</summary>
public sealed class StoreSearchResult
{
    public static readonly StoreSearchResult Empty = new()
    {
        Games = new List<StoreGame>(),
        TotalCount = 0
    };

    /// <summary>The games on this page, in result order.</summary>
    public required IReadOnlyList<StoreGame> Games { get; init; }

    /// <summary>Total number of games matching the query across all pages.</summary>
    public required int TotalCount { get; init; }
}
