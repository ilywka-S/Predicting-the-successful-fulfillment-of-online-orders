using System.Globalization;

namespace OrderSense.Maui.Controls;

public static class Formats
{
    private static readonly string Nbsp = ((char)160).ToString();

    private static readonly NumberFormatInfo Numbers = new()
    {
        NumberGroupSeparator = Nbsp,
        NumberDecimalSeparator = ",",
    };

    public static string Percent(double share) =>
        double.IsNaN(share) ? "—" : (share * 100).ToString("0", Numbers) + Nbsp + "%";

    public static string Money(decimal value) => "R$" + Nbsp + value.ToString("#,0.00", Numbers);

    public static string Kilometers(double km) =>
        double.IsNaN(km) ? "—" : km.ToString("#,0", Numbers) + Nbsp + "км";

    public static string Weight(double grams) =>
        double.IsNaN(grams) ? "—"
        : grams < 1000 ? grams.ToString("0", Numbers) + Nbsp + "г"
        : (grams / 1000).ToString("#,0.##", Numbers) + Nbsp + "кг";

    public static string Count(long n, string one, string few, string many)
    {
        var lastTwo = n % 100;
        var last = n % 10;
        var word = lastTwo is >= 11 and <= 14 ? many
            : last == 1 ? one
            : last is >= 2 and <= 4 ? few
            : many;
        return n.ToString("#,0", Numbers) + Nbsp + word;
    }

    public static string ShortId(string? id) =>
        string.IsNullOrEmpty(id) ? "—" : "#" + (id.Length > 8 ? id[..8] : id);

    public static string OrderStatus(string? status) => status switch
    {
        "created" => "Створено",
        "approved" => "Підтверджено",
        "invoiced" => "Виставлено рахунок",
        "processing" => "Обробляється",
        "shipped" => "Відправлено",
        "delivered" => "Доставлено",
        "canceled" => "Скасовано",
        "unavailable" => "Недоступно",
        null or "" => "—",
        _ => status,
    };

    public static string PaymentType(string? type) => type switch
    {
        "credit_card" => "Кредитна картка",
        "debit_card" => "Дебетова картка",
        "boleto" => "Boleto (банківський рахунок)",
        "voucher" => "Ваучер",
        "not_defined" => "Не вказано",
        null or "" => "—",
        _ => type,
    };

    public static string Role(string? role) => role switch
    {
        "manager" => "Менеджер",
        "analyst" => "Аналітик",
        "admin" => "Адміністратор",
        null or "" => "—",
        _ => role,
    };

    public static string Category(string? category)
    {
        if (string.IsNullOrEmpty(category))
        {
            return "Без категорії";
        }

        if (Categories.TryGetValue(category, out var name))
        {
            return name;
        }

        var text = category.Replace('_', ' ');
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static readonly Dictionary<string, string> Categories = new()
    {
        ["bed_bath_table"] = "Текстиль для дому",
        ["health_beauty"] = "Здоров'я і краса",
        ["sports_leisure"] = "Спорт і дозвілля",
        ["furniture_decor"] = "Меблі й декор",
        ["computers_accessories"] = "Комп'ютери й аксесуари",
        ["housewares"] = "Товари для дому",
        ["watches_gifts"] = "Годинники й подарунки",
        ["telephony"] = "Телефонія",
        ["garden_tools"] = "Сад та інструменти",
        ["auto"] = "Авто",
        ["toys"] = "Іграшки",
        ["cool_stuff"] = "Цікаві речі",
        ["perfumery"] = "Парфумерія",
        ["baby"] = "Дитячі товари",
        ["electronics"] = "Електроніка",
        ["stationery"] = "Канцтовари",
        ["fashion_bags_accessories"] = "Сумки й аксесуари",
        ["pet_shop"] = "Зоотовари",
        ["office_furniture"] = "Офісні меблі",
        ["consoles_games"] = "Консолі та ігри",
        ["luggage_accessories"] = "Валізи",
        ["home_appliances"] = "Побутова техніка",
        ["musical_instruments"] = "Музичні інструменти",
        ["books_general_interest"] = "Книги",
        ["food"] = "Продукти",
    };
}
