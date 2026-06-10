using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using SplitRoast.Launch.Native;

namespace SplitRoast.Launch.Coop;

/// <summary>
/// Releases single-instance locks (named mutexes) held by a running game process so
/// another copy is allowed to start. This is the generic version of what Nucleus
/// Co-op handlers do with a per-game "KillMutex" name; SplitRoast can target named
/// mutexes exactly (safest) or, with no names, close any named mutex the process
/// holds.
///
/// It is strictly best-effort and fully guarded: any failure returns 0 and never
/// throws. It only ever touches the process id it is given (a copy SplitRoast just
/// launched). To stay safe it filters handles by object <em>type</em> first and only
/// queries the <em>name</em> of Mutant handles, side-stepping the well-known
/// NtQueryObject hang on synchronous named pipes.
/// </summary>
public static class MutexKiller
{
    private const string MutantTypeName = "Mutant";

    /// <summary>
    /// Closes single-instance mutexes held by <paramref name="processId"/>.
    /// </summary>
    /// <param name="names">
    /// Optional exact mutex names to target (matched on the final name segment,
    /// case-insensitive). When null/empty, every named mutex held by the process is
    /// closed.
    /// </param>
    /// <returns>The number of mutex handles closed.</returns>
    public static int CloseSingleInstanceLocks(int processId, IReadOnlyCollection<string>? names = null)
    {
        try
        {
            return CloseCore(processId, NormaliseNames(names));
        }
        catch
        {
            // Best-effort only: never let lock-busting destabilise a launch.
            return 0;
        }
    }

    private static IReadOnlyList<string> NormaliseNames(IReadOnlyCollection<string>? names) =>
        names is null
            ? Array.Empty<string>()
            : names.Select(n => n?.Trim() ?? string.Empty)
                   .Where(n => n.Length > 0)
                   .ToList();

    private static int CloseCore(int processId, IReadOnlyList<string> names)
    {
        IntPtr target = NtHandles.OpenProcess(NtHandles.ProcessDupHandle, false, processId);
        if (target == IntPtr.Zero)
        {
            return 0;
        }

        IntPtr buffer = IntPtr.Zero;
        int closed = 0;
        try
        {
            buffer = QueryAllHandles(out int handleCount);
            if (buffer == IntPtr.Zero)
            {
                return 0;
            }

            int entrySize = Marshal.SizeOf<NtHandles.SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX>();
            IntPtr entryBase = buffer + (IntPtr.Size * 2);
            IntPtr current = NtHandles.GetCurrentProcess();

            for (int i = 0; i < handleCount; i++)
            {
                var entry = Marshal.PtrToStructure<NtHandles.SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX>(
                    entryBase + (i * entrySize));

                if ((int)entry.UniqueProcessId != processId)
                {
                    continue;
                }

                if (entry.GrantedAccess == NtHandles.DangerousGrantedAccess)
                {
                    continue; // Skip handles that could hang a name query.
                }

                if (TryCloseIfTargetMutex(target, current, entry.HandleValue, names))
                {
                    closed++;
                }
            }
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
            NtHandles.CloseHandle(target);
        }

        return closed;
    }

    private static bool TryCloseIfTargetMutex(
        IntPtr target, IntPtr current, IntPtr handleValue, IReadOnlyList<string> names)
    {
        if (!NtHandles.DuplicateHandle(
                target, handleValue, current, out IntPtr dup, 0, false, NtHandles.DuplicateSameAccess))
        {
            return false;
        }

        try
        {
            // Type first — this query never hangs.
            if (!string.Equals(QueryObjectString(dup, NtHandles.ObjectTypeInformation), MutantTypeName, StringComparison.Ordinal))
            {
                return false;
            }

            string name = QueryObjectString(dup, NtHandles.ObjectNameInformation);
            if (!ShouldClose(name, names))
            {
                return false;
            }

            // Close the handle inside the source process so the name is freed.
            if (NtHandles.DuplicateHandle(
                    target, handleValue, current, out IntPtr tmp, 0, false, NtHandles.DuplicateCloseSource))
            {
                NtHandles.CloseHandle(tmp);
                return true;
            }

            return false;
        }
        finally
        {
            NtHandles.CloseHandle(dup);
        }
    }

    private static bool ShouldClose(string name, IReadOnlyList<string> names)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false; // Only ever close *named* mutexes.
        }

        if (names.Count == 0)
        {
            return true; // Auto mode: any named mutex held by the launched copy.
        }

        string lastSegment = name[(name.LastIndexOf('\\') + 1)..];
        return names.Any(n =>
            string.Equals(lastSegment, n, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, n, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Reads the UNICODE_STRING returned by an NtQueryObject info class.</summary>
    private static string QueryObjectString(IntPtr handle, int infoClass)
    {
        int size = 0x800;
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            uint status = NtHandles.NtQueryObject(handle, infoClass, buffer, size, out int needed);
            if (status == NtHandles.StatusInfoLengthMismatch && needed > size)
            {
                Marshal.FreeHGlobal(buffer);
                size = needed;
                buffer = Marshal.AllocHGlobal(size);
                status = NtHandles.NtQueryObject(handle, infoClass, buffer, size, out _);
            }

            if (status != NtHandles.StatusSuccess)
            {
                return string.Empty;
            }

            var us = Marshal.PtrToStructure<NtHandles.UNICODE_STRING>(buffer);
            if (us.Length == 0 || us.Buffer == IntPtr.Zero)
            {
                return string.Empty;
            }

            return Marshal.PtrToStringUni(us.Buffer, us.Length / 2) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Queries the system handle table, growing the buffer until it fits. Returns an
    /// unmanaged buffer (caller frees) plus the handle count, or zero on failure.
    /// </summary>
    private static IntPtr QueryAllHandles(out int handleCount)
    {
        handleCount = 0;
        int size = 0x100000; // 1 MB to start; system handle tables are large.
        const int cap = 256 * 1024 * 1024;
        IntPtr buffer = Marshal.AllocHGlobal(size);

        try
        {
            uint status;
            while ((status = NtHandles.NtQuerySystemInformation(
                       NtHandles.SystemExtendedHandleInformation, buffer, size, out int needed))
                   == NtHandles.StatusInfoLengthMismatch)
            {
                Marshal.FreeHGlobal(buffer);
                size = needed > size ? needed : size * 2;
                if (size > cap)
                {
                    return IntPtr.Zero;
                }
                buffer = Marshal.AllocHGlobal(size);
            }

            if (status != NtHandles.StatusSuccess)
            {
                Marshal.FreeHGlobal(buffer);
                return IntPtr.Zero;
            }

            handleCount = (int)Marshal.ReadIntPtr(buffer);
            return buffer;
        }
        catch
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
            handleCount = 0;
            return IntPtr.Zero;
        }
    }
}
