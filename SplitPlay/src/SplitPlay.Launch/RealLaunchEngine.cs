using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SplitPlay.Core.Abstractions;
using SplitPlay.Core.Models;

namespace SplitPlay.Launch;

/// <summary>
/// The MVP launch engine. It starts the game executable once per player, waits for
/// each instance's window, makes it borderless and places it into the player's
/// split region. If a second instance cannot produce its own window (a common
/// single-instance restriction) or test mode is enabled, the affected slot falls
/// back to a neutral SplitPlay test window so the split layout is always verifiable.
///
/// Not yet handled (next milestone): isolating controller input so one pad drives
/// only one window. For now every window receives input from all pads.
/// </summary>
public sealed class RealLaunchEngine : ILaunchEngine
{
    private static readonly TimeSpan GameWindowTimeout = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan TestWindowTimeout = TimeSpan.FromSeconds(10);

    // Distinct colors so the two test windows are easy to tell apart.
    private static readonly string[] PlayerColors = { "#4FD1A5", "#5AA9E6", "#E0A85A", "#C77DD6" };

    private readonly WindowManager _windowManager = new();
    private readonly GameWindowLocator _locator = new();

    public async Task<LaunchResult> LaunchAsync(
        LaunchRequest request,
        IProgress<LaunchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? validationError = Validate(request);
        if (validationError is not null)
        {
            return LaunchResult.Fail(validationError);
        }

        return await Task.Run(() => RunAsync(request, progress, cancellationToken), cancellationToken);
    }

    private async Task<LaunchResult> RunAsync(
        LaunchRequest request,
        IProgress<LaunchProgress>? progress,
        CancellationToken cancellationToken)
    {
        bool testMode = request.Profile.UseTestWindows;

        progress?.Report(new LaunchProgress(8, "Resolving game executable..."));
        string? exePath = testMode
            ? null
            : ExecutableResolver.Resolve(
                request.Game.InstallDir, request.Game.Name, request.Profile.ExecutableOverride);

        string? testTargetPath = ResolveTestTargetPath();
        var notes = new List<string>();
        var startedWindows = new List<IntPtr>();
        int total = request.Targets.Count;

        for (int i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PlayerLaunchTarget target = request.Targets[i];
            int percent = 10 + (int)((i + 0.5) / total * 80);
            progress?.Report(new LaunchProgress(
                percent, $"Launching instance {i + 1} of {total}..."));

            IntPtr handle = IntPtr.Zero;

            // 1. Try the real game executable (unless we are in test mode).
            if (!testMode && exePath is not null)
            {
                handle = await TryLaunchAndLocateAsync(
                    StartGame(exePath), GameWindowTimeout, cancellationToken);

                if (handle == IntPtr.Zero)
                {
                    notes.Add($"{target.Player.DisplayName}: game window not detected, used a test window.");
                }
            }

            // 2. Fall back to a SplitPlay test window for this slot.
            if (handle == IntPtr.Zero)
            {
                if (testTargetPath is null)
                {
                    return LaunchResult.Fail(
                        "Test window helper (SplitPlay.TestTarget.exe) was not found next to the app.");
                }

                handle = await TryLaunchAndLocateAsync(
                    StartTestWindow(testTargetPath, target, i), TestWindowTimeout, cancellationToken);
            }

            if (handle == IntPtr.Zero)
            {
                return LaunchResult.Fail(
                    $"Could not obtain a window for {target.Player.DisplayName}.");
            }

            _windowManager.PlaceBorderless(handle, target.Region);
            startedWindows.Add(handle);
        }

        // Give both windows focus in order so they are visibly active.
        progress?.Report(new LaunchProgress(95, "Positioning windows..."));
        foreach (IntPtr handle in startedWindows)
        {
            _windowManager.BringToFront(handle);
            await Task.Delay(60, cancellationToken);
        }

        progress?.Report(new LaunchProgress(100, "Ready."));
        return BuildResult(request, testMode, exePath, notes);
    }

    private async Task<IntPtr> TryLaunchAndLocateAsync(
        Process? process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (process is null)
        {
            return IntPtr.Zero;
        }

        return await _locator.WaitForMainWindowAsync(process, timeout, cancellationToken);
    }

    private static Process? StartGame(string exePath)
    {
        try
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty,
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Anti-cheat, permissions, etc. - treat as "couldn't launch", fall back.
            return null;
        }
    }

    private static Process? StartTestWindow(string testTargetPath, PlayerLaunchTarget target, int playerIndex)
    {
        string color = PlayerColors[playerIndex % PlayerColors.Length];
        try
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = testTargetPath,
                UseShellExecute = false,
                ArgumentList =
                {
                    "--player", (playerIndex + 1).ToString(),
                    "--controller", target.ControllerIndex.ToString(),
                    "--color", color
                }
            });
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Finds the bundled test-window helper next to the running app.</summary>
    private static string? ResolveTestTargetPath()
    {
        // Bundled into a "TestTarget" subfolder by the app build; also accept it
        // sitting directly next to the app as a fallback.
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, "TestTarget", "SplitPlay.TestTarget.exe"),
            Path.Combine(AppContext.BaseDirectory, "SplitPlay.TestTarget.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>Returns a validation error message, or null if the request is valid.</summary>
    private static string? Validate(LaunchRequest request)
    {
        if (request.Targets.Count < 2)
        {
            return "A split session needs at least two players.";
        }

        var indices = request.Targets.Select(t => t.ControllerIndex).ToList();
        if (indices.Any(i => i < 0))
        {
            return "Every player must have a controller assigned.";
        }

        if (indices.Distinct().Count() != indices.Count)
        {
            return "Each player must be assigned a different controller.";
        }

        return null;
    }

    private static LaunchResult BuildResult(
        LaunchRequest request, bool testMode, string? exePath, List<string> notes)
    {
        string orientation = request.Profile.Orientation.ToString().ToLowerInvariant();

        if (testMode)
        {
            return LaunchResult.Ok(
                $"Opened {request.Targets.Count} test windows in a {orientation} split.");
        }

        if (exePath is null)
        {
            return LaunchResult.Ok(
                "No game executable could be detected, so test windows were used. " +
                "Set an executable override for this game and try again.");
        }

        string message = $"Launched \"{request.Game.Name}\" as a {orientation} split.";
        if (notes.Count > 0)
        {
            message += " " + string.Join(" ", notes);
        }

        message += " Note: controller input is not yet isolated per window.";
        return LaunchResult.Ok(message);
    }
}
