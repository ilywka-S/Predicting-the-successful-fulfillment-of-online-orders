using System.ComponentModel.DataAnnotations;

namespace OrderSense.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; set; } = "";

    [Required] public string Audience { get; set; } = "";
    
    [Required] [MinLength(32)] public string Key { get; set; } = "";

    [Range(1, 1440)] public int AccessTokenMinutes { get; set; } = 30;

    [Range(1, 90)] public int RefreshTokenDays { get; set; } = 14;
}