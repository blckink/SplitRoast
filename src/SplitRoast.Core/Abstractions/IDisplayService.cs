using System.Collections.Generic;
using SplitRoast.Core.Models;

namespace SplitRoast.Core.Abstractions;

/// <summary>
/// Enumerates the monitors connected to the PC. Implemented by the platform
/// layer; consumed by the layout calculator and the detail view.
/// </summary>
public interface IDisplayService
{
    /// <summary>Returns all connected displays, primary first.</summary>
    IReadOnlyList<DisplayInfo> GetDisplays();

    /// <summary>Returns the primary display.</summary>
    DisplayInfo GetPrimaryDisplay();
}
