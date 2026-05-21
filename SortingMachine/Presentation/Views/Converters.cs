// =========================================================
// File: Presentation/Views/Converters.cs
// Project: SortingMachine
// Sprint: S2 | Agent: Gemini CLI
// =========================================================
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SortingMachine.Presentation.Views;

public class BoolToColorConverter : IValueConverter
{
    public Brush TrueColor { get; set; } = Brushes.Green;
    public Brush FalseColor { get; set; } = Brushes.Gray;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return TrueColor;
        return FalseColor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToAlarmColorConverter : IValueConverter
{
    public Brush TrueColor { get; set; } = Brushes.Red;
    public Brush FalseColor { get; set; } = Brushes.Gray;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return TrueColor;
        return FalseColor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
