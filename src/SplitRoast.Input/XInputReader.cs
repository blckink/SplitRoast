using System;
using SplitRoast.Core.Models;
using SplitRoast.Input.Native;

namespace SplitRoast.Input;

/// <summary>
/// Public, allocation-free helpers over XInput, shared by the gamepad service and
/// the test windows. Keeping the native calls behind this small surface means
/// there is exactly one place that talks to XInput.
/// </summary>
public static class XInputReader
{
    /// <summary>Number of controller slots XInput exposes (0-3).</summary>
    public const int MaxControllers = XInput.MaxControllers;

    /// <summary>Returns true if a controller is connected at the given index.</summary>
    public static bool IsConnected(int userIndex) =>
        XInput.XInputGetState(userIndex, out _) == XInput.ErrorSuccess;

    /// <summary>Reads the full input state for a controller index.</summary>
    public static GamepadState ReadState(int userIndex)
    {
        if (XInput.XInputGetState(userIndex, out XInput.XINPUT_STATE state) != XInput.ErrorSuccess)
        {
            return GamepadState.Disconnected;
        }

        XInput.XINPUT_GAMEPAD pad = state.Gamepad;
        return new GamepadState(
            IsConnected: true,
            Buttons: (GamepadButtons)pad.wButtons,
            LeftTrigger: pad.bLeftTrigger,
            RightTrigger: pad.bRightTrigger,
            LeftStickX: pad.sThumbLX,
            LeftStickY: pad.sThumbLY,
            RightStickX: pad.sThumbRX,
            RightStickY: pad.sThumbRY);
    }

    /// <summary>
    /// Reads device capabilities: a friendly sub-type name, whether the pad is
    /// wireless, and whether it has rumble motors. Falls back to sensible defaults
    /// (a wired gamepad with rumble) if the capability query is unavailable.
    /// </summary>
    public static (string DeviceType, bool IsWireless, bool SupportsRumble) GetCapabilities(int userIndex)
    {
        if (XInput.XInputGetCapabilities(userIndex, 0, out XInput.XINPUT_CAPABILITIES caps) != XInput.ErrorSuccess)
        {
            return ("Gamepad", false, true);
        }

        bool wireless = (caps.Flags & XInput.CapsWireless) != 0;
        bool rumble = caps.Vibration.wLeftMotorSpeed != 0 || caps.Vibration.wRightMotorSpeed != 0;
        return (SubTypeName(caps.SubType), wireless, rumble);
    }

    /// <summary>Reads the controller's battery level (best-effort).</summary>
    public static GamepadBattery GetBattery(int userIndex)
    {
        if (XInput.XInputGetBatteryInformation(
                userIndex, XInput.BatteryDevTypeGamepad, out XInput.XINPUT_BATTERY_INFORMATION info)
            != XInput.ErrorSuccess)
        {
            return GamepadBattery.Unknown;
        }

        // BatteryType: 0 = disconnected, 1 = wired, 2 = alkaline, 3 = NiMH, 0xFF = unknown.
        if (info.BatteryType == 0x00 || info.BatteryType == 0xFF)
        {
            return GamepadBattery.Unknown;
        }

        if (info.BatteryType == 0x01)
        {
            return GamepadBattery.Wired;
        }

        // BatteryLevel: 0 = empty, 1 = low, 2 = medium, 3 = full.
        return info.BatteryLevel switch
        {
            0 => GamepadBattery.Empty,
            1 => GamepadBattery.Low,
            2 => GamepadBattery.Medium,
            3 => GamepadBattery.Full,
            _ => GamepadBattery.Unknown
        };
    }

    /// <summary>Maps an XInput device sub-type byte to a friendly name.</summary>
    private static string SubTypeName(byte subType) => subType switch
    {
        1 => "Gamepad",
        2 => "Racing wheel",
        3 => "Arcade stick",
        4 => "Flight stick",
        5 => "Dance pad",
        6 => "Guitar",
        7 => "Guitar (alternate)",
        8 => "Drum kit",
        11 => "Guitar (bass)",
        19 => "Arcade pad",
        _ => "Gamepad"
    };

    /// <summary>
    /// Sets the rumble motors (0.0-1.0 each). Out-of-range values are clamped.
    /// </summary>
    public static void SetVibration(int userIndex, double leftMotor, double rightMotor)
    {
        var vibration = new XInput.XINPUT_VIBRATION
        {
            wLeftMotorSpeed = ToMotorSpeed(leftMotor),
            wRightMotorSpeed = ToMotorSpeed(rightMotor)
        };

        XInput.XInputSetState(userIndex, ref vibration);
    }

    private static ushort ToMotorSpeed(double value)
    {
        double clamped = Math.Clamp(value, 0.0, 1.0);
        return (ushort)(clamped * ushort.MaxValue);
    }
}
