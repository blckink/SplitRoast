using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace SplitRoast.Launch;

/// <summary>
/// Ensures the Steam client is running before launching a game that needs it
/// (a Steam-DRM / SteamStub title). Without a running client such a game's stub
/// relaunches the process through Steam and the copy we started exits, which is
/// what produces a stray uncontrolled fullscreen window instead of the split. By
/// starting Steam first and waiting until it is ready, the game launches cleanly
/// from our mirrored instance every time - no need for the user to start Steam by
/// hand first.
/// </summary>
public static class SteamClientController
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    // Grace period after the client process appears so it can finish connecting
    // (logging in, opening its pipes) before a DRM stub queries it.
    private static readonly TimeSpan ReadyGrace = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Makes sure Steam is running. Returns true if Steam is (now) running. Never
    /// throws; failures are logged and reported as false so the launch can still
    /// proceed (the user may have Steam started another way).
    /// </summary>
    public static bool EnsureRunning(Action<string> log, CancellationToken cancellationToken = default)
    {
        if (IsRunning())
        {
            log("Steam client already running.");
            return true;
        }

        string? steamExe = GetSteamExePath();
        if (steamExe is null || !File.Exists(steamExe))
        {
            log("Steam is not running and steam.exe could not be located; " +
                "please start Steam manually for this game.");
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = steamExe, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            log($"Could not start Steam automatically ({ex.Message}); please start it manually.");
            return false;
        }

        log("Started the Steam client; waiting for it to be ready...");

        var deadline = DateTime.UtcNow + ReadyTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return IsRunning();
            }

            if (IsRunning())
            {
                // Let the client settle before the game's DRM stub talks to it.
                cancellationToken.WaitHandle.WaitOne(ReadyGrace);
                log("Steam client is ready.");
                return true;
            }

            cancellationToken.WaitHandle.WaitOne(PollInterval);
        }

        log("Timed out waiting for the Steam client to start.");
        return IsRunning();
    }

    /// <summary>
    /// True if a Steam client is currently active. Prefers Steam's own
    /// ActiveProcess/pid registry marker (set only while the client is live) and
    /// falls back to scanning for the process by name.
    /// </summary>
    private static bool IsRunning()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Valve\Steam\ActiveProcess");
            if (key?.GetValue("pid") is int pid && pid != 0)
            {
                try
                {
                    using Process _ = Process.GetProcessById(pid);
                    return true;
                }
                catch (ArgumentException)
                {
                    // Stale pid (client exited): fall through to the name scan.
                }
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
        }

        return Process.GetProcessesByName("steam").Length > 0;
    }

    private static string? GetSteamExePath()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");

            if (key?.GetValue("SteamExe") is string exe && !string.IsNullOrWhiteSpace(exe))
            {
                return Path.GetFullPath(exe);
            }

            if (key?.GetValue("SteamPath") is string dir && !string.IsNullOrWhiteSpace(dir))
            {
                return Path.GetFullPath(Path.Combine(dir, "steam.exe"));
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
        }

        return null;
    }
}
