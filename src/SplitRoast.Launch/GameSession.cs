using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SplitRoast.Launch.InputIsolation;

namespace SplitRoast.Launch;

/// <summary>
/// A running split-screen session: the processes that were launched plus the
/// controller router and any input-isolation state that must be cleaned up. Owns
/// the teardown so the rest of the engine doesn't have to: stopping a session closes
/// the game windows, kills anything that lingers, disposes the router and restores
/// the original game folder (for direct, in-place isolation).
///
/// The session also ends itself when every tracked process has exited on its own,
/// so the UI can return to an idle state without the user pressing Stop.
/// </summary>
public sealed class GameSession : IDisposable
{
    private static readonly TimeSpan GracefulCloseWait = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(4);

    private readonly List<Process> _processes;
    private readonly ControllerRouter? _router;
    private readonly InputIsolationManager _isolation;
    private readonly bool _restoreIsolationOnStop;
    private readonly object _gate = new();
    private bool _ended;

    /// <summary>Raised once when the session ends (stopped, or all games exited).</summary>
    public event EventHandler? Ended;

    public GameSession(
        IEnumerable<Process> processes,
        ControllerRouter? router,
        InputIsolationManager isolation,
        bool restoreIsolationOnStop)
    {
        _processes = processes.Where(p => p is not null).ToList();
        _router = router;
        _isolation = isolation;
        _restoreIsolationOnStop = restoreIsolationOnStop;

        foreach (Process p in _processes)
        {
            try
            {
                p.EnableRaisingEvents = true;
                p.Exited += OnProcessExited;
            }
            catch
            {
                // A process that already exited (or can't raise events) is handled by
                // the all-exited check below.
            }
        }

        // If everything already exited before we wired up, end immediately.
        if (_processes.Count == 0 || _processes.All(SafeHasExited))
        {
            End();
        }
    }

    /// <summary>True while at least one tracked process is still running.</summary>
    public bool IsActive
    {
        get { lock (_gate) { return !_ended; } }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_processes.All(SafeHasExited))
        {
            End();
        }
    }

    /// <summary>
    /// Stops the session: politely asks each window to close, kills whatever is left,
    /// then runs the common teardown (router + isolation restore).
    /// </summary>
    public async Task StopAsync()
    {
        foreach (Process p in _processes)
        {
            TryCloseMainWindow(p);
        }

        await Task.Delay(GracefulCloseWait).ConfigureAwait(false);

        foreach (Process p in _processes)
        {
            if (!SafeHasExited(p))
            {
                TryKill(p);
            }
        }

        foreach (Process p in _processes)
        {
            try { p.WaitForExit((int)ExitWait.TotalMilliseconds); }
            catch { /* ignore */ }
        }

        End();
    }

    private void End()
    {
        lock (_gate)
        {
            if (_ended)
            {
                return;
            }
            _ended = true;
        }

        _router?.Dispose();

        if (_restoreIsolationOnStop)
        {
            // Games are stopped now, so the shadowed DLLs are unlocked and restorable.
            try { _isolation.RestoreAll(); }
            catch { /* restore is retried on next app start if anything is still locked */ }
        }

        foreach (Process p in _processes)
        {
            try { p.Exited -= OnProcessExited; } catch { /* ignore */ }
            try { p.Dispose(); } catch { /* ignore */ }
        }

        Ended?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => End();

    private static bool SafeHasExited(Process p)
    {
        try { return p.HasExited; }
        catch { return true; }
    }

    private static void TryCloseMainWindow(Process p)
    {
        try
        {
            if (!p.HasExited && p.MainWindowHandle != IntPtr.Zero)
            {
                p.CloseMainWindow();
            }
        }
        catch { /* best-effort */ }
    }

    private static void TryKill(Process p)
    {
        try { p.Kill(entireProcessTree: true); }
        catch { /* already gone, or access denied */ }
    }
}
