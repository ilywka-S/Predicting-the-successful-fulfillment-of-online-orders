using Microsoft.Extensions.Configuration;
using OrderSense.Api.Predictions;

namespace OrderSense.Api.Tests.Jobs;

public class HangfireConnectionTests
{
    private static IConfiguration Config(string key, string value)
    {
        Dictionary<string, string?> data = new Dictionary<string, string?> { [key] = value };

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    [Fact]
    public void Resolve_PoolerHost_IsReplacedWithDirectHost()
    {
        IConfiguration config = Config("ConnectionStrings:ordersense", "Host=ep-abc-pooler.eu-central-1.aws.neon.tech;Database=neondb;Username=u;Password=p");

        HangfireConnectionInfo info = HangfireConnection.Resolve(config);

        Assert.True(info.PoolerRemoved);
        Assert.Contains("Host=ep-abc.eu-central-1.aws.neon.tech", info.ConnectionString);
        Assert.DoesNotContain("-pooler", info.ConnectionString);
    }

    [Fact]
    public void Resolve_DirectHost_IsUnchanged()
    {
        string connection = "Host=ep-abc.eu-central-1.aws.neon.tech;Database=neondb;Username=u;Password=p";
        IConfiguration config = Config("ConnectionStrings:ordersense", connection);

        HangfireConnectionInfo info = HangfireConnection.Resolve(config);

        Assert.False(info.PoolerRemoved);
        Assert.Equal(connection, info.ConnectionString);
    }

    [Fact]
    public void Resolve_HangfireSpecificStringWins()
    {
        Dictionary<string, string?> data = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ordersense"] = "Host=main;Database=d",
            ["ConnectionStrings:hangfire"] = "Host=queue;Database=d",
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();

        HangfireConnectionInfo info = HangfireConnection.Resolve(config);

        Assert.Contains("Host=queue", info.ConnectionString);
    }

    [Fact]
    public void Resolve_WithoutConnectionString_Throws()
    {
        IConfiguration config = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => HangfireConnection.Resolve(config));
    }
}