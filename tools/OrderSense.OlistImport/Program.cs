using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
namespace OrderSense.OlistImport;
public static class ImportProgram
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Використання: OlistImport <шлях до папки з CSV>");
            return 1;
        }
        string? connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ordersense");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("Не задано змінну середовища ConnectionStrings__ordersense.");
            return 1;
        }
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using AppDbContext db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        OlistImporter importer = new OlistImporter(db, args[0]);
        await importer.RunAsync();
        Console.WriteLine("Імпорт завершено.");
        return 0;
    }
}