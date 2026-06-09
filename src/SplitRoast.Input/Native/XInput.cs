using System.Runtime.InteropServices;

namespace SplitRoast.Input.Native;

/// <summary>
/// Thin P/Invoke wrapper around the parts of XInput we use: reading controller
/// state (connection + buttons/sticks/triggers) and setting the rumble motors.
/// </summary>
internal static class XInput
{
    /// <summary>XInput supports a maximum of four controllers (indices 0-3).</summary>
    public const int MaxControllers = 4;

    public const int ErrorSuccess = 0;

    /// <summary>Capability flag: the device is wireless.</summary>
    public const ushort CapsWireless = 0x0002;

    /// <summary>Battery query device type: gamepad.</summary>
    public const byte BatteryDevTypeGamepad = 0x00;

    // xinput1_4.dll ships with Windows 8+. On the supported OS range it is always
    // present, so we bind to it directly.
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    public static extern int XInputGetState(int dwUserIndex, out XINPUT_STATE pState);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    public static extern int XInputSetState(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetCapabilities")]
    public static extern int XInputGetCapabilities(int dwUserIndex, int dwFlags, out XINPUT_CAPABILITIES pCapabilities);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetBatteryInformation")]
    public static extern int XInputGetBatteryInformation(int dwUserIndex, byte devType, out XINPUT_BATTERY_INFORMATION pBatteryInformation);

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_VIBRATION
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_CAPABILITIES
    {
        public byte Type;
        public byte SubType;
        public ushort Flags;
        public XINPUT_GAMEPAD Gamepad;
        public XINPUT_VIBRATION Vibration;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_BATTERY_INFORMATION
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }
}
