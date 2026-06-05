using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms; // Screen enumeration (WindowsForms is available to WPF on net8.0-windows)
using SplitPlay.Core.Abstractions;
using SplitPlay.Core.Models;

namespace SplitPlay.App.Services;

/// <summary>
/// <see cref="IDisplayService"/> implemented with the Win32 screen enumeration
/// exposed through System.Windows.Forms.Screen. It reports device-pixel bounds,
/// which is exactly what the layout calculator and window manager need.
/// </summary>
public sealed class DisplayService : IDisplayService
{
    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        // Primary first, then the rest, so index 0 is always the primary monitor.
        return Screen.AllScreens
            .OrderByDescending(s => s.Primary)
            .Select(ToDisplayInfo)
            .ToList();
    }

    public DisplayInfo GetPrimaryDisplay() =>
        ToDisplayInfo(Screen.PrimaryScreen ?? Screen.AllScreens[0]);

    private static DisplayInfo ToDisplayInfo(Screen screen) => new()
    {
        DeviceName = screen.DeviceName,
        IsPrimary = screen.Primary,
        Bounds = new ScreenRegion(
            screen.Bounds.X, screen.Bounds.Y,
            screen.Bounds.Width, screen.Bounds.Height),
        WorkingArea = new ScreenRegion(
            screen.WorkingArea.X, screen.WorkingArea.Y,
            screen.WorkingArea.Width, screen.WorkingArea.Height)
    };
}
