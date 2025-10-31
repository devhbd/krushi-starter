using System.Globalization;

namespace MotorStarter.Converters;

public class MotorStatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string status)
        {
            return Colors.Gray;
        }

        return status switch
        {
            "Motor Started" => Color.FromArgb("#2ecc71"),
            "Motor Stopped" => Color.FromArgb("#e74c3c"),
            "Error" => Color.FromArgb("#f39c12"),
            _ => Colors.SlateGray
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
