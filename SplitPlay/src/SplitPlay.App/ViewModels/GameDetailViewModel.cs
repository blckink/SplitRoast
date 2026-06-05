using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using SplitPlay.App.Imaging;
using SplitPlay.App.Mvvm;
using SplitPlay.App.Services;
using SplitPlay.Core.Abstractions;
using SplitPlay.Core.Models;

namespace SplitPlay.App.ViewModels;

/// <summary>
/// Detail + configuration page for a single game. Lets the user choose the split
/// orientation, target display and per-player controller, then start the session.
/// All choices are persisted to the game's <see cref="GameProfile"/> as they
/// change, so the next visit restores the same setup.
/// </summary>
public sealed class GameDetailViewModel : PageViewModel
{
    // Exactly two players for the MVP.
    private const int PlayerCount = 2;

    private readonly IGameProfileStore _profileStore;
    private readonly IDisplayService _displayService;
    private readonly IGamepadService _gamepadService;
    private readonly ISplitLayoutCalculator _layoutCalculator;
    private readonly ILaunchEngine _launchEngine;
    private readonly IShellNavigator _navigator;

    private SteamGame _game = null!;
    private GameProfile _profile = null!;
    private bool _initializing;

    private ImageSource? _hero;
    private SplitOrientation _orientation = SplitOrientation.Vertical;
    private DisplayInfo? _selectedDisplay;
    private string _statusText = string.Empty;
    private int _progress;
    private bool _isLaunching;

    public GameDetailViewModel(
        IGameProfileStore profileStore,
        IDisplayService displayService,
        IGamepadService gamepadService,
        ISplitLayoutCalculator layoutCalculator,
        ILaunchEngine launchEngine,
        IShellNavigator navigator)
    {
        _profileStore = profileStore;
        _displayService = displayService;
        _gamepadService = gamepadService;
        _layoutCalculator = layoutCalculator;
        _launchEngine = launchEngine;
        _navigator = navigator;

        for (int i = 0; i < PlayerCount; i++)
        {
            var slot = new PlayerSlotViewModel(i);
            slot.SelectionChanged += OnPlayerSelectionChanged;
            Players.Add(slot);
        }

        BackCommand = new RelayCommand(() => _navigator.NavigateToGames());
        SetVerticalCommand = new RelayCommand(() => Orientation = SplitOrientation.Vertical);
        SetHorizontalCommand = new RelayCommand(() => Orientation = SplitOrientation.Horizontal);
        StartCommand = new AsyncRelayCommand(StartAsync, CanStart);
    }

    public override string Title => _game?.Name ?? "Game";

    public ObservableCollection<PlayerSlotViewModel> Players { get; } = new();

    public ObservableCollection<DisplayInfo> Displays { get; } = new();

    public RelayCommand BackCommand { get; }

    public RelayCommand SetVerticalCommand { get; }

    public RelayCommand SetHorizontalCommand { get; }

    public AsyncRelayCommand StartCommand { get; }

    /// <summary>Background hero image for the detail header.</summary>
    public ImageSource? Hero
    {
        get => _hero;
        private set => SetProperty(ref _hero, value);
    }

    public SplitOrientation Orientation
    {
        get => _orientation;
        set
        {
            if (SetProperty(ref _orientation, value))
            {
                OnPropertyChanged(nameof(IsVertical));
                OnPropertyChanged(nameof(IsHorizontal));
                PersistProfile();
            }
        }
    }

    public bool IsVertical => Orientation == SplitOrientation.Vertical;

    public bool IsHorizontal => Orientation == SplitOrientation.Horizontal;

    public DisplayInfo? SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            if (SetProperty(ref _selectedDisplay, value))
            {
                PersistProfile();
            }
        }
    }

    /// <summary>Latest status / result message shown beneath the Start button.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>Launch progress 0-100 (drives the progress bar).</summary>
    public int Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public bool IsLaunching
    {
        get => _isLaunching;
        private set => SetProperty(ref _isLaunching, value);
    }

    /// <summary>
    /// Loads the game's profile, populates displays and controllers, and restores
    /// the saved configuration. Call once after the view model is resolved.
    /// </summary>
    public async Task InitializeAsync(SteamGame game)
    {
        _initializing = true;
        try
        {
            _game = game;
            OnPropertyChanged(nameof(Title));

            Hero = ImageLoader.TryLoad(game.Artwork.HeroPath ?? game.Artwork.HeaderPath, 960);

            _profile = await _profileStore.LoadAsync(game.AppId);

            LoadDisplays();
            RebuildControllerChoices();

            // Restore saved settings.
            Orientation = _profile.Orientation;
            RestoreControllerSelections();

            // The service is monitoring app-wide; we just listen for changes.
            _gamepadService.GamepadsChanged += OnGamepadsChanged;
        }
        finally
        {
            _initializing = false;
            StartCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Unsubscribes from controller updates when leaving the page.</summary>
    public void Cleanup()
    {
        _gamepadService.GamepadsChanged -= OnGamepadsChanged;
    }

    private void LoadDisplays()
    {
        Displays.Clear();
        foreach (DisplayInfo display in _displayService.GetDisplays())
        {
            Displays.Add(display);
        }

        int index = _profile.TargetDisplayIndex ?? 0;
        SelectedDisplay = index >= 0 && index < Displays.Count
            ? Displays[index]
            : Displays.FirstOrDefault();
    }

    /// <summary>
    /// Rebuilds each player's controller drop-down from the currently connected
    /// pads, preserving existing selections where still valid.
    /// </summary>
    private void RebuildControllerChoices()
    {
        IReadOnlyList<GamepadInfo> pads = _gamepadService.GetConnectedGamepads();

        var choices = new List<ControllerChoice> { ControllerChoice.Unassigned };
        choices.AddRange(pads.Select(p => new ControllerChoice
        {
            Index = p.UserIndex,
            Label = p.DisplayName
        }));

        foreach (PlayerSlotViewModel slot in Players)
        {
            int? previous = slot.Selected.Index;

            slot.Choices.Clear();
            foreach (ControllerChoice choice in choices)
            {
                slot.Choices.Add(choice);
            }

            // Re-select the previous pad if it is still connected; otherwise reset.
            slot.Selected = slot.Choices.FirstOrDefault(c => c.Index == previous)
                            ?? ControllerChoice.Unassigned;
        }
    }

    private void RestoreControllerSelections()
    {
        for (int i = 0; i < Players.Count; i++)
        {
            int? saved = i < _profile.ControllerAssignments.Length
                ? _profile.ControllerAssignments[i]
                : null;

            Players[i].Selected =
                Players[i].Choices.FirstOrDefault(c => c.Index == saved)
                ?? ControllerChoice.Unassigned;
        }
    }

    private void OnGamepadsChanged(object? sender, EventArgs e)
    {
        // The event arrives on a timer thread; marshal to the UI thread.
        Application.Current?.Dispatcher.Invoke(() =>
        {
            RebuildControllerChoices();
            StartCommand.RaiseCanExecuteChanged();
        });
    }

    private void OnPlayerSelectionChanged(object? sender, EventArgs e)
    {
        PersistProfile();
        StartCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Writes the current UI state back into the profile and saves it.</summary>
    private void PersistProfile()
    {
        if (_initializing || _profile is null)
        {
            return;
        }

        _profile.Orientation = Orientation;
        _profile.TargetDisplayIndex =
            SelectedDisplay is null ? null : Displays.IndexOf(SelectedDisplay);

        for (int i = 0; i < Players.Count; i++)
        {
            _profile.ControllerAssignments[i] = Players[i].Selected.Index;
        }

        // Fire and forget; saving a tiny JSON file is fast and failures here must
        // not interrupt the user's configuration flow.
        _ = _profileStore.SaveAsync(_profile);
    }

    /// <summary>Start is allowed only with two distinct controllers assigned.</summary>
    private bool CanStart()
    {
        if (IsLaunching || SelectedDisplay is null)
        {
            return false;
        }

        var indices = Players.Select(p => p.Selected.Index).ToList();
        return indices.All(i => i.HasValue) &&
               indices.Distinct().Count() == indices.Count;
    }

    private async Task StartAsync()
    {
        IsLaunching = true;
        Progress = 0;
        StatusText = "Starting...";
        try
        {
            DisplayInfo display = SelectedDisplay!;
            IReadOnlyList<ScreenRegion> regions =
                _layoutCalculator.Calculate(display, Orientation, PlayerCount);

            var targets = new List<PlayerLaunchTarget>(PlayerCount);
            for (int i = 0; i < PlayerCount; i++)
            {
                var slot = new PlayerSlot
                {
                    Index = i,
                    AssignedControllerIndex = Players[i].Selected.Index
                };
                targets.Add(new PlayerLaunchTarget(
                    slot, regions[i], Players[i].Selected.Index!.Value));
            }

            var request = new LaunchRequest
            {
                Game = _game,
                Profile = _profile,
                Targets = targets
            };

            var progress = new Progress<LaunchProgress>(p =>
            {
                Progress = p.Percent;
                StatusText = p.Status;
            });

            LaunchResult result = await _launchEngine.LaunchAsync(request, progress);
            StatusText = result.Message;
        }
        catch (Exception ex)
        {
            StatusText = $"Launch failed: {ex.Message}";
        }
        finally
        {
            IsLaunching = false;
            StartCommand.RaiseCanExecuteChanged();
        }
    }
}
