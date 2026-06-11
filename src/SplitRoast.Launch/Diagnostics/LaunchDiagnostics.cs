using System;
using System.IO;
using System.Linq;

namespace SplitRoast.Launch.Diagnostics;

/// <summary>
/// Per-game launch diagnostics. Writes one readable log of every launch step and
/// its outcome to a folder the app manages, and gathers the in-game proxy logs and
/// the game's own Unity log there too. The game detail page can open this folder,
/// so the player never has to dig through %AppData% or the Event Viewer to find out
/// why a game did or did not work.
/// </summary>
public sealed class LaunchDiagnostics
{
    private readonly string _dir;
    private readonly string _logPath;
    private readonly object _gate = new();

    public LaunchDiagnostics(uint appId, string gameName)
    {
        _dir = FolderFor(appId);
        Directory.CreateDirectory(_dir);
        _logPath = Path.Combine(_dir, "launch.log");
        try
        {
            File.WriteAllText(_logPath,
                $"=== SplitRoast launch log — {gameName} (appid {appId}) ==={Environment.NewLine}" +
                $"Started {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The diagnostics folder for a game (the UI opens this).</summary>
    public static string FolderFor(uint appId) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "SplitRoast", "Diagnostics", appId.ToString());

    public string Folder => _dir;

    /// <summary>Appends a timestamped line to the launch log.</summary>
    public void Log(string message)
    {
        lock (_gate)
        {
            try
            {
                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Copies each instance's proxy log next to the launch log.</summary>
    public void CollectProxyLogs(string instancesRoot)
    {
        try
        {
            if (!Directory.Exists(instancesRoot))
            {
                return;
            }

            foreach (string instanceDir in Directory.EnumerateDirectories(instancesRoot))
            {
                // The proxy sits next to the game exe, which can be in a sub-folder.
                foreach (string proxyLog in Directory.EnumerateFiles(
                             instanceDir, "splitroast_proxy.log", SearchOption.AllDirectories))
                {
                    string name = $"proxy_{Path.GetFileName(instanceDir)}.log";
                    File.Copy(proxyLog, Path.Combine(_dir, name), overwrite: true);
                    Log($"Collected proxy log -> {name}");
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Best-effort copy of the game's Unity log from %AppData%\LocalLow.</summary>
    public void CollectGameLog(string gameName)
    {
        try
        {
            string localLow = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow");
            if (!Directory.Exists(localLow))
            {
                return;
            }

            string key = Normalize(gameName);
            foreach (string companyDir in Directory.EnumerateDirectories(localLow))
            {
                foreach (string productDir in Directory.EnumerateDirectories(companyDir))
                {
                    if (key.Length > 3 && !Normalize(Path.GetFileName(productDir)).Contains(key))
                    {
                        continue;
                    }

                    foreach (string logName in new[] { "Player.log", "output_log.txt" })
                    {
                        string src = Path.Combine(productDir, logName);
                        if (File.Exists(src))
                        {
                            string dest = Path.Combine(_dir, $"game_{Path.GetFileName(productDir)}_{logName}");
                            File.Copy(src, dest, overwrite: true);
                            Log($"Collected game log -> {Path.GetFileName(dest)}");
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string Normalize(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
