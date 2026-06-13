using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SplitRoast.App.Mvvm;
using SplitRoast.Core.Abstractions;
using SplitRoast.Core.Models;

namespace SplitRoast.App.ViewModels;

/// <summary>
/// The Discover page: searches the public Steam store for co-op games beyond the
/// ones the user has installed. Holds the filter state (co-op type, genre, sort,
/// free text), runs the search through <see cref="IStoreDiscoveryProvider"/>, and
/// exposes a responsive tile grid with paging ("load more"). All network work is
/// best-effort; an empty result simply shows the empty state.
/// </summary>
public sealed class DiscoverViewModel : PageViewModel
{
    private const int PageSize = 50;

    private readonly IStoreDiscoveryProvider _provider;

    private CancellationTokenSource? _inflight;

    private string _searchTerm = string.Empty;
    private StoreCoopFilter _coop = StoreCoopFilter.OnlineCoop;
    private StoreGenre _genre = StoreGenre.All;
    private StoreSortOrder _sort = StoreSortOrder.TopReviews;

    private bool _isLoading;
    private bool _hasLoaded;
    private bool _hasError;
    private int _total;

    public DiscoverViewModel(IStoreDiscoveryProvider provider)
    {
        _provider = provider;

        SelectCoopCommand = new RelayCommand<StoreCoopFilter>(v => { if (v is { } coop) SetCoop(coop); });
        SelectGenreCommand = new RelayCommand<StoreGenre>(v => { if (v is { } genre) SetGenre(genre); });
        SelectSortCommand = new RelayCommand<StoreSortOrder>(v => { if (v is { } sort) SetSort(sort); });
        SearchCommand = new AsyncRelayCommand(ReloadAsync);
        LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync, () => CanLoadMore);
        OpenStoreCommand = new RelayCommand<DiscoverTileViewModel>(OpenInStore);
    }

    public override string Title => "Discover";

    /// <summary>The tiles bound to the grid.</summary>
    public ObservableCollection<DiscoverTileViewModel> Results { get; } = new();

    public RelayCommand<StoreCoopFilter> SelectCoopCommand { get; }
    public RelayCommand<StoreGenre> SelectGenreCommand { get; }
    public RelayCommand<StoreSortOrder> SelectSortCommand { get; }
    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand LoadMoreCommand { get; }
    public RelayCommand<DiscoverTileViewModel> OpenStoreCommand { get; }

    /// <summary>Free-text term. Changing it does not search until the user submits.</summary>
    public string SearchTerm
    {
        get => _searchTerm;
        set => SetProperty(ref _searchTerm, value);
    }

    public StoreCoopFilter SelectedCoop
    {
        get => _coop;
        private set
        {
            if (SetProperty(ref _coop, value))
            {
                OnPropertyChanged(nameof(CoopLabel));
            }
        }
    }

    public StoreGenre SelectedGenre
    {
        get => _genre;
        private set => SetProperty(ref _genre, value);
    }

    public StoreSortOrder SelectedSort
    {
        get => _sort;
        private set => SetProperty(ref _sort, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(ShowLoadingOverlay));
                OnPropertyChanged(nameof(CanLoadMore));
                LoadMoreCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (SetProperty(ref _hasError, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    /// <summary>True when a finished search produced no games (and didn't error).</summary>
    public bool ShowEmptyState => _hasLoaded && !IsLoading && !HasError && Results.Count == 0;

    /// <summary>The full-page "searching" overlay, shown only on an initial/refreshed search.</summary>
    public bool ShowLoadingOverlay => IsLoading && Results.Count == 0;

    /// <summary>More results exist than are currently loaded.</summary>
    public bool CanLoadMore => !IsLoading && Results.Count < _total;

    /// <summary>"123 of 4,567 games" summary shown under the filters.</summary>
    public string ResultSummary => _hasLoaded && Results.Count > 0
        ? $"{Results.Count:N0} of {_total:N0} games"
        : string.Empty;

    /// <summary>Badge label applied to every tile for the active co-op filter.</summary>
    public string CoopLabel => _coop switch
    {
        StoreCoopFilter.OnlineCoop => "Online co-op",
        StoreCoopFilter.LanCoop => "LAN co-op",
        StoreCoopFilter.SplitScreen => "Split-screen",
        _ => "Co-op"
    };

    /// <summary>Loads the first page once, the first time the page is shown.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (!_hasLoaded && !IsLoading)
        {
            await ReloadAsync();
        }
    }

    private void SetCoop(StoreCoopFilter value)
    {
        if (value != _coop)
        {
            SelectedCoop = value;
            _ = ReloadAsync();
        }
    }

    private void SetGenre(StoreGenre value)
    {
        if (value != _genre)
        {
            SelectedGenre = value;
            _ = ReloadAsync();
        }
    }

    private void SetSort(StoreSortOrder value)
    {
        if (value != _sort)
        {
            SelectedSort = value;
            _ = ReloadAsync();
        }
    }

    private async Task ReloadAsync()
    {
        CancellationToken token = BeginRequest();
        IsLoading = true;
        HasError = false;
        Results.Clear();
        OnPropertyChanged(nameof(ResultSummary));

        try
        {
            StoreSearchResult result = await _provider.SearchAsync(BuildQuery(0), token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            _total = result.TotalCount;
            AddTiles(result);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer request; ignore.
        }
        catch (Exception ex)
        {
            SplitRoast.App.Diagnostics.CrashReporter.Report(ex, "Discover search");
            HasError = true;
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                _hasLoaded = true;
                IsLoading = false;
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(ResultSummary));
                OnPropertyChanged(nameof(CanLoadMore));
                LoadMoreCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private async Task LoadMoreAsync()
    {
        if (!CanLoadMore)
        {
            return;
        }

        CancellationToken token = BeginRequest();
        IsLoading = true;
        try
        {
            StoreSearchResult result = await _provider.SearchAsync(BuildQuery(Results.Count), token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (result.TotalCount > 0)
            {
                _total = result.TotalCount;
            }

            AddTiles(result);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SplitRoast.App.Diagnostics.CrashReporter.Report(ex, "Discover load more");
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsLoading = false;
                OnPropertyChanged(nameof(ResultSummary));
                OnPropertyChanged(nameof(CanLoadMore));
                LoadMoreCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private StoreSearchQuery BuildQuery(int start) => new()
    {
        Term = _searchTerm,
        Coop = _coop,
        Genre = _genre,
        Sort = _sort,
        Start = start,
        Count = PageSize
    };

    private void AddTiles(StoreSearchResult result)
    {
        CoopSuitability suitability = _coop == StoreCoopFilter.SplitScreen
            ? CoopSuitability.NativeSplitScreen
            : CoopSuitability.Recommended;

        foreach (StoreGame game in result.Games)
        {
            Results.Add(new DiscoverTileViewModel(game, CoopLabel, suitability));
        }
    }

    /// <summary>Cancels any in-flight request and returns a token for the new one.</summary>
    private CancellationToken BeginRequest()
    {
        _inflight?.Cancel();
        _inflight = new CancellationTokenSource();
        return _inflight.Token;
    }

    private void OpenInStore(DiscoverTileViewModel? tile)
    {
        if (tile is null)
        {
            return;
        }

        try
        {
            // Prefer the Steam client's in-app store page; fall back to the browser.
            Process.Start(new ProcessStartInfo(tile.Game.SteamStoreUrl) { UseShellExecute = true });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo(tile.Game.StorePageUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                SplitRoast.App.Diagnostics.CrashReporter.Report(ex, "Open store page");
            }
        }
    }
}
