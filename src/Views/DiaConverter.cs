using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ProgramacioDocent.Views;

// Converteix el número de dia (1..5) al seu nom en català per a la UI.
public class DiaConverter : IValueConverter
{
    private static readonly string[] Dies =
        { "", "Dilluns", "Dimarts", "Dimecres", "Dijous", "Divendres" };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int d && d >= 1 && d <= 5)
            return Dies[d];
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
