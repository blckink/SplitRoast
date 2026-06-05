using System.Runtime.InteropServices;

namespace SplitPlay.Input.Native;

/// <summary>
/// Thin P/Invoke wrapper around the parts of XInput we need. We only query
/// connection state (XInputGetState), which is enough to enumerate pads and
/// detect plug/unplug. Input routing to specific windows is added later.
/// </summary>
internal static class XInput
{
    /// <summary>XInput supports a maximum of four controllers (indices 0-3).</summary>
    public const int MaxControllers = 4;

    private const int ErrorSuccess = 0;

    /// <summary>
    /// Returns true if a controller is connected at <paramref name="userIndex"/>.
    /// </summary>
    public static bool IsConnected(int userIndex) =>
        XInputGetState(userIndex, out _) == ErrorSuccess;

    // xinput1_4.dll ships with Windows 8+. The DLL name is resolved at runtime;
    // on the supported OS range this is always present.
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState(int dwUserIndex, out XINPUT_STATE pState);

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }
}
