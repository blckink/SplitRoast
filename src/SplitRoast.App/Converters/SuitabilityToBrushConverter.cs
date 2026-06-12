using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SplitRoast.Core.Models;

namespace SplitRoast.App.Converters;

/// <summary>
/// Maps a <see cref="CoopSuitability"/> to its themed accent brush, so the games
/// grid badge and the detail-page banner share one colour language. Falls back to a
/// neutral grey if the resource can't be resolved (e.g. at design time).
/// </summary>
public sealed class SuitabilityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string key = value is CoopSuitability suitability
            ? suitability switch
            {
                CoopSuitability.Recommended => "SuitGoodBrush",
                CoopSuitability.NativeSplitScreen => "SuitNativeBrush",
                CoopSuitability.Maybe => "SuitMaybeBrush",
                CoopSuitability.NotSuitable => "SuitBadBrush",
                _ => "SuitUnknownBrush"
            }
            : "SuitUnknownBrush";

        return Application.Current?.TryFindResource(key) as Brush
               ?? new SolidColorBrush(Color.FromRgb(0x55, 0x60, 0x6C));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
