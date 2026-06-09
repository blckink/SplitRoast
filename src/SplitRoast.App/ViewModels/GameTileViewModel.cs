using System.Windows.Media;
using SplitRoast.App.Imaging;
using SplitRoast.App.Mvvm;
using SplitRoast.Core.Models;

namespace SplitRoast.App.ViewModels;

/// <summary>
/// View model for a single game tile in the grid. Wraps a <see cref="SteamGame"/>
/// and exposes just what the tile UI needs: a title and a cover image that is
/// filled in lazily once artwork has been resolved.
/// </summary>
public sealed class GameTileViewModel : ObservableObject
{
    // Tiles render around ~200px wide; decode a little larger for crispness on
    // high-DPI displays while keeping memory in check.
    private const int CoverDecodeWidth = 240;

    private ImageSource? _cover;
    private GameCoopInfo _coop = GameCoopInfo.Unknown;

    public GameTileViewModel(SteamGame game)
    {
        Game = game;
    }

    /// <summary>The underlying game this tile represents.</summary>
    public SteamGame Game { get; }

    public string Title => Game.Name;

    /// <summary>Cover image (library capsule). Null until artwork is resolved.</summary>
    public ImageSource? Cover
    {
        get => _cover;
        private set => SetProperty(ref _cover, value);
    }

    /// <summary>True while no cover has loaded yet (used to show a placeholder).</summary>
    public bool HasCover => _cover is not null;

    /// <summary>
    /// Applies resolved artwork to the tile. Safe to call from the UI thread after
    /// the (cheap) artwork resolution completes.
    /// </summary>
    public void ApplyArtwork(GameArtwork artwork)
    {
        Game.Artwork = artwork;
        Cover = ImageLoader.TryLoad(artwork.LibraryCapsulePath, CoverDecodeWidth);
        OnPropertyChanged(nameof(HasCover));
    }

    /// <summary>Co-op suitability for the corner badge.</summary>
    public CoopSuitability Suitability => _coop.Suitability;

    /// <summary>Compact badge label (e.g. "Online co-op"). Empty when unknown.</summary>
    public string BadgeText => _coop.ShortLabel;

    /// <summary>True when there is a meaningful badge to show.</summary>
    public bool HasBadge =>
        _coop.Suitability != CoopSuitability.Unknown && !string.IsNullOrEmpty(_coop.ShortLabel);

    /// <summary>Applies a resolved suitability verdict (call on the UI thread).</summary>
    public void ApplySuitability(GameCoopInfo info)
    {
        _coop = info;
        OnPropertyChanged(nameof(Suitability));
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(HasBadge));
    }
}
