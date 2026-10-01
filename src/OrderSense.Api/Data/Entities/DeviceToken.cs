using Microsoft.EntityFrameworkCore;

namespace OrderSense.Api.Data.Entities;

[Index(nameof(Token), IsUnique = true)]
public class DeviceToken
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public DevicePlatform Platform { get; set; }

    public required string Token { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}