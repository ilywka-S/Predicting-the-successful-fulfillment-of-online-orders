using System.Globalization;

namespace OrderSense.Maui.Controls;

public abstract class FormatConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? "—" : Format(value);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    protected abstract string Format(object value);
}

public class PercentConverter : FormatConverter
{
    protected override string Format(object value) => Formats.Percent(System.Convert.ToDouble(value, CultureInfo.InvariantCulture));
}

public class MoneyConverter : FormatConverter
{
    protected override string Format(object value) => Formats.Money(System.Convert.ToDecimal(value, CultureInfo.InvariantCulture));
}

public class KilometersConverter : FormatConverter
{
    protected override string Format(object value) => Formats.Kilometers(System.Convert.ToDouble(value, CultureInfo.InvariantCulture));
}

public class WeightConverter : FormatConverter
{
    protected override string Format(object value) => Formats.Weight(System.Convert.ToDouble(value, CultureInfo.InvariantCulture));
}

public class ShortIdConverter : FormatConverter
{
    protected override string Format(object value) => Formats.ShortId(value.ToString());
}

public class OrderStatusConverter : FormatConverter
{
    protected override string Format(object value) => Formats.OrderStatus(value.ToString());
}

public class PaymentTypeConverter : FormatConverter
{
    protected override string Format(object value) => Formats.PaymentType(value.ToString());
}

public class CategoryConverter : FormatConverter
{
    protected override string Format(object value) => Formats.Category(value.ToString());
}

public class RoleConverter : FormatConverter
{
    protected override string Format(object value) => Formats.Role(value.ToString());
}
