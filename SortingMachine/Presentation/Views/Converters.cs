// =========================================================
// File: Presentation/Views/Converters.cs
// Project: SortingMachine
// Sprint: S2 | Agent: Gemini CLI
// =========================================================
using System.Globalization;
using System.Windows;
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

public class StringToBrushConverter : IValueConverter
{
    private static readonly BrushConverter Converter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string colorStr)
        {
            try
            {
                return (Brush)Converter.ConvertFromString(colorStr)!;
            }
            catch
            {
                return Brushes.Gray;
            }
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusToColorConverter : IValueConverter
{
    private static readonly string[] RedKeywords =
        ["报警", "失败", "异常", "错误", "限位", "急停", "Fault"];
    private static readonly string[] GreenKeywords =
        ["运行中", "运行", "启动", "成功", "完成", "就绪", "已清除", "已全部清除", "全部清除", "清除", "已回零", "全绿", "Running", "Online"];
    private static readonly string[] YellowKeywords =
        ["暂停", "警告", "等待", "Warning", "Paused"];

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string ?? string.Empty;

        // Try to retrieve brushes from App resources, fallback to raw colors if not found
        // ✅ 先判绿（已处理的状态优先级高于关键词匹配，如"报警已全部清除"应为绿）
        if (GreenKeywords.Any(k => text.Contains(k)))
        {
            return System.Windows.Application.Current?.TryFindResource("SuccessBrush") as Brush 
                ?? new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47));
        }

        if (RedKeywords.Any(k => text.Contains(k)))
        {
            return System.Windows.Application.Current?.TryFindResource("DangerBrush") as Brush 
                ?? new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));
        }

        if (YellowKeywords.Any(k => text.Contains(k)))
        {
            return System.Windows.Application.Current?.TryFindResource("WarningBrush") as Brush 
                ?? new SolidColorBrush(Color.FromRgb(0xF9, 0xA8, 0x25));
        }

        return System.Windows.Application.Current?.TryFindResource("PrimaryBrush") as Brush 
            ?? new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

