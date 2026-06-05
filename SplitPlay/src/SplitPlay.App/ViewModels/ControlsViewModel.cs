using System;
using System.Collections.ObjectModel;
using System.Windows;
using SplitPlay.Core.Abstractions;
using SplitPlay.Core.Models;

namespace SplitPlay.App.ViewModels;

/// <summary>
/// Live overview of the controllers connected to the PC. Updates automatically as
/// pads are plugged in or removed. (Future home for per-controller details such
/// as battery, vibration test and re-ordering.)
/// </summary>
public sealed class ControlsViewModel : PageViewModel
{
    private readonly IGamepadService _gamepadService;

    public ControlsViewModel(IGamepadService gamepadService)
    {
        _gamepadService = gamepadService;
        _gamepadService.GamepadsChanged += OnGamepadsChanged;
        Refresh();
    }

    public override string Title => "Controls";

    /// <summary>The currently connected controllers.</summary>
    public ObservableCollection<GamepadInfo> Controllers { get; } = new();

    /// <summary>True when no controllers are connected (shows guidance text).</summary>
    public bool IsEmpty => Controllers.Count == 0;

    private void OnGamepadsChanged(object? sender, EventArgs e) =>
        Application.Current?.Dispatcher.Invoke(Refresh);

    private void Refresh()
    {
        Controllers.Clear();
        foreach (GamepadInfo pad in _gamepadService.GetConnectedGamepads())
        {
            Controllers.Add(pad);
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
}
