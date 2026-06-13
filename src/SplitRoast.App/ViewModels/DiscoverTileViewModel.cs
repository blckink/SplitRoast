using System.Windows.Media;
using SplitRoast.App.Imaging;
using SplitRoast.Core.Models;

namespace SplitRoast.App.ViewModels;

/// <summary>
/// View model for a single tile on the Discover grid. Wraps a <see cref="StoreGame"/>
/// (a store-search result, not an installed game) and exposes what the tile needs:
/// a cover, a co-op badge, and review / price metadata. The cover is the Steam CDN
/// capsule, loaded asynchronously by the image pipeline so the grid never blocks.
/// </summary>
public sealed class DiscoverTileViewModel
{
    // Tiles render around ~190px wide; decode a little larger for high-DPI crispness.
    private const int CoverDecodeWidth = 240;

    public DiscoverTileViewModel(StoreGame game, string coopLabel, CoopSuitability suitability)
    {
        Game = game;
        CoopLabel = coopLabel;
        Suitability = suitability;
        Cover = ImageLoader.TryLoad(game.CoverUrl, CoverDecodeWidth);
    }

    /// <summary>The underlying store result this tile represents.</summary>
    public StoreGame Game { get; }

    public string Title => Game.Name;

    /// <summary>Cover capsule. Null if it could not be loaded (placeholder shown).</summary>
    public ImageSource? Cover { get; }

    public bool HasCover => Cover is not null;

    /// <summary>Compact co-op label for the corner badge (e.g. "Online co-op").</summary>
    public string CoopLabel { get; }

    /// <summary>Drives the badge colour via the shared suitability brush converter.</summary>
    public CoopSuitability Suitability { get; }

    public string? PriceText => Game.PriceText;

    public bool HasPrice => !string.IsNullOrEmpty(Game.PriceText);

    /// <summary>Review summary and release date, joined for the caption second line.</summary>
    public string Meta
    {
        get
        {
            string review = Game.ReviewSummary ?? string.Empty;
            string date = Game.ReleaseDate ?? string.Empty;
            if (review.Length > 0 && date.Length > 0)
            {
                return $"{review}  ·  {date}";
            }

            return review.Length > 0 ? review : date;
        }
    }

    public bool HasMeta => Meta.Length > 0;
}
