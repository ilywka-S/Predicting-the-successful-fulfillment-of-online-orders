using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using OrderSense.ML.Training;

var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture);

List<T> ReadCsv<T>(string fileName)
{
    using var reader = new StreamReader($"../../data/olist/{fileName}");
    using var csv = new CsvReader(reader, csvConfig);
    return csv.GetRecords<T>().ToList();
}

Console.WriteLine("Читаю файли...");

var orders = ReadCsv<OrderRow>("olist_orders_dataset.csv");
var items = ReadCsv<OrderItemRow>("olist_order_items_dataset.csv");
var products = ReadCsv<ProductRow>("olist_products_dataset.csv");
var sellers = ReadCsv<SellerRow>("olist_sellers_dataset.csv");
var customers = ReadCsv<CustomerRow>("olist_customers_dataset.csv");
var payments = ReadCsv<PaymentRow>("olist_order_payments_dataset.csv");
var geolocation = ReadCsv<GeolocationRow>("olist_geolocation_dataset.csv");

Console.WriteLine($"orders: {orders.Count}, items: {items.Count}, products: {products.Count}, " +
    $"sellers: {sellers.Count}, customers: {customers.Count}, payments: {payments.Count}, geo: {geolocation.Count}");

//середнє по координатинатах по кожному zip-префіксу
var zipToCoords = geolocation
    .GroupBy(g => g.ZipCodePrefix)
    .ToDictionary(
        g => g.Key,
        g => (Lat: g.Average(x => x.Lat), Lng: g.Average(x => x.Lng)));

Console.WriteLine($"Унікальних zip-префіксів: {zipToCoords.Count}");

//пошук за ключем
var productsById = products.ToDictionary(p => p.ProductId);
var sellersById = sellers.ToDictionary(s => s.SellerId);
var customersById = customers.ToDictionary(c => c.CustomerId);

//кілька рядків на одне замовлення
var itemsByOrder = items.ToLookup(i => i.OrderId);
var paymentsByOrder = payments.ToLookup(p => p.OrderId);

Console.WriteLine($"Товарів у словнику: {productsById.Count}, продавців: {sellersById.Count}, клієнтів: {customersById.Count}");

double HaversineKm(double lat1, double lng1, double lat2, double lng2)
{
    const double earthRadiusKm = 6371.0;
    double dLat = (lat2 - lat1) * Math.PI / 180.0;
    double dLng = (lng2 - lng1) * Math.PI / 180.0;

    double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
               Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
               Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

    double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    return earthRadiusKm * c;
}

OrderCard? BuildCard(OrderRow order)
{
    var orderItems = itemsByOrder[order.OrderId].ToList();
    if (orderItems.Count == 0) return null; //замовлення без позицій пропускаємо

    var card = new OrderCard { OrderId = order.OrderId };

    //товари
    card.ItemsCount = orderItems.Count;
    card.TotalPrice = orderItems.Sum(i => i.Price);
    card.TotalFreight = orderItems.Sum(i => i.FreightValue);

    var totalPriceAndFreight = card.TotalPrice + card.TotalFreight;
    card.FreightShare = totalPriceAndFreight > 0
        ? (double)(card.TotalFreight / totalPriceAndFreight)
        : 0;

    card.SellersCount = orderItems.Select(i => i.SellerId).Distinct().Count();

    //головний продавець = найбільша сума по товарах
    var mainSellerId = orderItems
        .GroupBy(i => i.SellerId)
        .OrderByDescending(g => g.Sum(i => i.Price))
        .First().Key;
    if (sellersById.TryGetValue(mainSellerId, out var mainSeller))
    { 
        card.SellerState = mainSeller.SellerState;
    }

    //головна категорія - товар з найбільшою ціною
    var mainItem = orderItems.OrderByDescending(i => i.Price).First();
    if (productsById.TryGetValue(mainItem.ProductId, out var mainProduct))
    {
        card.MainCategory = mainProduct.ProductCategoryName;
    }

    // вага/об'єм — сума по товарах, де відомі габарити; якщо невідомо у всіх — null, не 0
    float totalWeight = 0;
    float totalVolume = 0;
    bool anyWeight = false;
    bool anyVolume = false;
    foreach (var item in orderItems)
    {
        if (!productsById.TryGetValue(item.ProductId, out var p)) 
        {
            continue;
        }
        if (p.ProductWeightG.HasValue) 
        { 
            totalWeight += p.ProductWeightG.Value; 
            anyWeight = true; 
        }
        if (p.ProductLengthCm.HasValue && p.ProductHeightCm.HasValue && p.ProductWidthCm.HasValue)
        {
            totalVolume += p.ProductLengthCm.Value * p.ProductHeightCm.Value * p.ProductWidthCm.Value;
            anyVolume = true;
        }
    }
    card.TotalWeightG = anyWeight ? totalWeight : null;
    card.TotalVolumeCm3 = anyVolume ? totalVolume : null;

    //оплати
    var orderPayments = paymentsByOrder[order.OrderId].ToList();
    card.PaymentsCount = orderPayments.Count;
    if (orderPayments.Count > 0)
    {
        var mainPayment = orderPayments.OrderByDescending(p => p.PaymentValue).First();
        card.MainPaymentType = mainPayment.PaymentType;
        card.PaymentInstallments = mainPayment.PaymentInstallments; // кількість платежів основної оплати, не сума по всіх
    }

    //клієнт
    if (customersById.TryGetValue(order.CustomerId, out var customer))
    {
        card.CustomerState = customer.CustomerState;
    }

    //дати
    if (order.OrderPurchaseTimestamp.HasValue)
    {
        card.PurchaseDayOfWeek = (int)order.OrderPurchaseTimestamp.Value.DayOfWeek;
        card.PurchaseHour = order.OrderPurchaseTimestamp.Value.Hour;
    }
    if (order.OrderPurchaseTimestamp.HasValue && order.OrderEstimatedDeliveryDate.HasValue)
    {
        card.PromisedDays = (order.OrderEstimatedDeliveryDate.Value.Date -
                              order.OrderPurchaseTimestamp.Value.Date).TotalDays;
    }

    //відстань продавець - клієнт
    if (customer != null && sellersById.TryGetValue(mainSellerId, out var sellerForDistance))
    {
        if (zipToCoords.TryGetValue(customer.CustomerZipCodePrefix, out var custCoords) &&
            zipToCoords.TryGetValue(sellerForDistance.SellerZipCodePrefix, out var sellCoords))
        {
            card.DistanceKm = HaversineKm(custCoords.Lat, custCoords.Lng, sellCoords.Lat, sellCoords.Lng);
        }
    }

    bool isLate = order.OrderDeliveredCustomerDate.HasValue &&
                  order.OrderEstimatedDeliveryDate.HasValue &&
                  order.OrderDeliveredCustomerDate.Value.Date > order.OrderEstimatedDeliveryDate.Value.Date;

    if (order.OrderStatus is "canceled" or "unavailable")
    {
        card.Label = "problem";
    }
    else if (order.OrderStatus == "delivered" && order.OrderDeliveredCustomerDate.HasValue)
    {
        card.Label = isLate ? "problem" : "success";
    }
    else
    {
        card.Label = null; // не враховуємо
    }

    return card;
}

Console.WriteLine("\nБудую картки...");

var cards = new List<OrderCard>();
foreach (var order in orders)
{
    var card = BuildCard(order);
    if (card != null) 
    {
        cards.Add(card);
    }
}

Console.WriteLine($"Побудовано карток: {cards.Count} (пропущено без позицій: {orders.Count - cards.Count})");

Directory.CreateDirectory("../../data/processed");
using (var writer = new StreamWriter("../../data/processed/order_cards.csv"))
using (var csvWriter = new CsvWriter(writer, csvConfig))
{
    csvWriter.WriteRecords(cards);
}

Console.WriteLine("Записано: data/processed/order_cards.csv");

//підрахунок пропусків по кожному полю
Console.WriteLine("\nПропуски по полях:");
Console.WriteLine($"  MainCategory: {cards.Count(c => c.MainCategory == null)}");
Console.WriteLine($"  TotalWeightG: {cards.Count(c => c.TotalWeightG == null)}");
Console.WriteLine($"  TotalVolumeCm3: {cards.Count(c => c.TotalVolumeCm3 == null)}");
Console.WriteLine($"  CustomerState: {cards.Count(c => c.CustomerState == null)}");
Console.WriteLine($"  SellerState: {cards.Count(c => c.SellerState == null)}");
Console.WriteLine($"  MainPaymentType: {cards.Count(c => c.MainPaymentType == null)}");
Console.WriteLine($"  PaymentInstallments: {cards.Count(c => c.PaymentInstallments == null)}");
Console.WriteLine($"  PromisedDays: {cards.Count(c => c.PromisedDays == null)}");
Console.WriteLine($"  DistanceKm: {cards.Count(c => c.DistanceKm == null)}");
Console.WriteLine($"  Label (не враховано): {cards.Count(c => c.Label == null)}");

var ordersById = orders.ToDictionary(o => o.OrderId);
Console.WriteLine("\nПо місяцях (рік-місяць, кількість, % проблемних серед врахованих):");

var byMonth = cards
    .Select(c => (Card: c, Order: ordersById[c.OrderId]))
    .Where(x => x.Order.OrderPurchaseTimestamp.HasValue)
    .GroupBy(x => new DateTime(x.Order.OrderPurchaseTimestamp!.Value.Year, x.Order.OrderPurchaseTimestamp.Value.Month, 1))
    .OrderBy(g => g.Key);

foreach (var g in byMonth)
{
    var considered = g.Count(x => x.Card.Label != null);
    var problem = g.Count(x => x.Card.Label == "problem");
    var pct = considered > 0 ? 100.0 * problem / considered : 0;

    Console.WriteLine($"  {g.Key:yyyy-MM}: замовлень={g.Count()}, враховано={considered}, проблемних={pct:F1}%");
}