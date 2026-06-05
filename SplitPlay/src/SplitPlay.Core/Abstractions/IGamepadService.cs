using System;
using System.Collections.Generic;
using SplitPlay.Core.Models;

namespace SplitPlay.Core.Abstractions;

/// <summary>
/// Reports the controllers currently connected to the PC and notifies when that
/// set changes (plugged in / unplugged). The MVP backs this with XInput.
/// </summary>
public interface IGamepadService : IDisposable
{
    /// <summary>Returns a snapshot of the currently connected controllers.</summary>
    IReadOnlyList<GamepadInfo> GetConnectedGamepads();

    /// <summary>
    /// Raised when the set of connected controllers changes. The UI uses this to
    /// keep the controller-assignment view live without polling itself.
    /// </summary>
    event EventHandler? GamepadsChanged;

    /// <summary>Begins monitoring for connect/disconnect events.</summary>
    void StartMonitoring();

    /// <summary>Stops monitoring.</summary>
    void StopMonitoring();
}
