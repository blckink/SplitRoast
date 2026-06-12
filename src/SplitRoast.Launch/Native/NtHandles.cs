using System;
using System.Runtime.InteropServices;

namespace SplitRoast.Launch.Native;

/// <summary>
/// Minimal NT/Win32 interop for enumerating and closing kernel object handles held
/// by another process. Used only by <see cref="Coop.MutexKiller"/> to release a
/// game's single-instance mutex so a second copy can start.
///
/// Safety note: callers must filter handles by object <em>type</em> (cheap, never
/// hangs) before ever querying an object's <em>name</em>. Querying the name of a
/// synchronous named pipe can block indefinitely; restricting name queries to
/// Mutant handles avoids that failure mode entirely.
/// </summary>
internal static class NtHandles
{
    public const int SystemExtendedHandleInformation = 0x40;
    public const int ObjectNameInformation = 1;
    public const int ObjectTypeInformation = 2;

    public const uint StatusInfoLengthMismatch = 0xC0000004;
    public const uint StatusSuccess = 0x00000000;

    public const uint ProcessDupHandle = 0x0040;
    public const uint DuplicateCloseSource = 0x00000001;
    public const uint DuplicateSameAccess = 0x00000002;

    // GrantedAccess value notorious for hanging ObjectNameInformation (sync pipes).
    public const uint DangerousGrantedAccess = 0x0012019F;

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX
    {
        public IntPtr Object;
        public IntPtr UniqueProcessId;
        public IntPtr HandleValue;
        public uint GrantedAccess;
        public ushort CreatorBackTraceIndex;
        public ushort ObjectTypeIndex;
        public uint HandleAttributes;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [DllImport("ntdll.dll")]
    public static extern uint NtQuerySystemInformation(
        int systemInformationClass, IntPtr systemInformation,
        int systemInformationLength, out int returnLength);

    [DllImport("ntdll.dll")]
    public static extern uint NtQueryObject(
        IntPtr handle, int objectInformationClass, IntPtr objectInformation,
        int objectInformationLength, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DuplicateHandle(
        IntPtr sourceProcessHandle, IntPtr sourceHandle, IntPtr targetProcessHandle,
        out IntPtr targetHandle, uint desiredAccess, bool inheritHandle, uint options);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentProcess();
}
