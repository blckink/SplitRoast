using System;
using System.Globalization;
using System.Windows.Data;

namespace SplitRoast.App.Converters;

/// <summary>
/// Returns true when the bound value equals the converter parameter. Used by the
/// Discover filter chips: each chip's Tag binds the currently selected filter value
/// against the chip's own value (passed as ConverterParameter), so a single
/// selection lights up exactly one chip via the shared Tag-driven button style.
/// </summary>
public sealed class EqualityToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Equals(value, parameter);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
