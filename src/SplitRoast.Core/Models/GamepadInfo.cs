namespace SplitRoast.Core.Models;

/// <summary>Charge level of a controller's battery, as reported by XInput.</summary>
public enum GamepadBattery
{
    /// <summary>Battery state could not be determined.</summary>
    Unknown,

    /// <summary>Wired controller (no battery / mains powered).</summary>
    Wired,

    Empty,
    Low,
    Medium,
    Full
}

/// <summary>
/// A controller currently connected to the PC, as reported by the input module.
/// MVP targets XInput pads (Xbox-style), which expose a fixed user index 0-3.
/// </summary>
public sealed class GamepadInfo
{
    /// <summary>
    /// XInput user index (0-3). This is the value we will route to the correct
    /// game instance so that one pad only ever drives one window.
    /// </summary>
    public required int UserIndex { get; init; }

    /// <summary>Whether the pad is currently connected.</summary>
    public required bool IsConnected { get; init; }

    /// <summary>A friendly, user-facing label, e.g. "Controller 1".</summary>
    public string DisplayName => $"Controller {UserIndex + 1}";

    /// <summary>The XInput slot, e.g. "Slot 1".</summary>
    public string SlotLabel => $"Slot {UserIndex + 1}";

    /// <summary>
    /// Friendly device sub-type (e.g. "Gamepad", "Wheel", "Arcade stick"), or null
    /// if the backend could not determine it.
    /// </summary>
    public string? DeviceType { get; init; }

    /// <summary>True if the controller reports itself as wireless.</summary>
    public bool IsWireless { get; init; }

    /// <summary>True if the controller exposes rumble motors.</summary>
    public bool SupportsRumble { get; init; } = true;

    /// <summary>Battery level (only meaningful for wireless pads).</summary>
    public GamepadBattery Battery { get; init; } = GamepadBattery.Unknown;

    // --- Display helpers (string-formatted so the views need no converters) ---

    /// <summary>Sub-type for display, defaulting to "Gamepad".</summary>
    public string DeviceTypeDisplay => string.IsNullOrWhiteSpace(DeviceType) ? "Gamepad" : DeviceType!;

    /// <summary>"Wireless" or "Wired".</summary>
    public string ConnectionDisplay => IsWireless ? "Wireless" : "Wired";

    /// <summary>"Supported" or "—".</summary>
    public string RumbleDisplay => SupportsRumble ? "Supported" : "—";

    /// <summary>Battery level as a label, e.g. "Full", "Low", "Wired".</summary>
    public string BatteryDisplay => Battery switch
    {
        GamepadBattery.Wired => "Wired",
        GamepadBattery.Empty => "Empty",
        GamepadBattery.Low => "Low",
        GamepadBattery.Medium => "Medium",
        GamepadBattery.Full => "Full",
        _ => "Unknown"
    };
}
