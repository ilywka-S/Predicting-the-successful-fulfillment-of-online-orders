using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace OrderSense.Api.Data.Entities;

[Index(nameof(TokenHash), IsUnique = true)]
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [MaxLength(64)] public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}