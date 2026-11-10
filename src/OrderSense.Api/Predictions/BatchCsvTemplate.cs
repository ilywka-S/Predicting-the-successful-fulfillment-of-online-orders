using System.Text;
namespace OrderSense.Api.Predictions;
public static class BatchCsvTemplate
{
    public static readonly string[] Columns =
    [
        "itemsCount", "totalPrice", "freightShare", "totalWeightG", "promisedDays", "distanceKm",
        "customerState", "sellerState", "mainCategory", "paymentType", "installments", "purchaseDayOfWeek",
    ];
    public static byte[] Bytes()
    {
        string text = string.Join(",", Columns) + "\r\n"
            + "1,120.50,0.15,800,20,450,SP,RJ,cama_mesa_banho,credit_card,1,2\r\n"
            + "2,310.00,0.22,,12,1800,AM,SP,informatica_acessorios,boleto,1,4\r\n";
        return [.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes(text)];
    }
}
