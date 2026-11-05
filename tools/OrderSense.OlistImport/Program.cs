using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.OlistImport;
if (args.Length < 1)
{
    Console.Error.WriteLine("Використання: OlistImport <шлях до папки з CSV>");
    return 1;
}
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ordersense");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Не задано змінну середовища ConnectionStrings__ordersense.");
    return 1;
}
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention()
    .Options;

await using var db = new AppDbContext(options);
await db.Database.MigrateAsync();
await new OlistImporter(db, args[0]).RunAsync();
Console.WriteLine("Імпорт завершено.");
return 0;