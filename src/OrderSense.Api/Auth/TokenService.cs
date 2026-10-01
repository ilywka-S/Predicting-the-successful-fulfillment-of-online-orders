using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;

namespace OrderSense.Api.Auth;

public class TokenService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider,
    ILogger<TokenService> logger)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthResponse> IssueAsync(AppUser user, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? "";
        var userDto = ToUserDto(user, role);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, userDto.Email),
            new("name", userDto.Name ?? userDto.Email),
        };
        
        if (role != "")
        {
            claims.Add(new Claim("role", role));
        }

        var expiresAt = now.AddMinutes(_jwt.AccessTokenMinutes);
        var accessToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)), SecurityAlgorithms.HmacSha256),
        });

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(refreshToken),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwt.RefreshTokenDays),
        });
        
        await db.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, refreshToken, expiresAt, userDto);
    }

    public async Task<AppUser?> RedeemAsync(string refreshToken, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var hash = Hash(refreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        
        if (token is null || token.ExpiresAt <= now)
        {
            return null;
        }

        if (token.RevokedAt is not null)
        {
            logger.LogWarning("Reuse of revoked refresh token for user {UserId}; revoking all sessions", token.UserId);
            
            await db.RefreshTokens
                .Where(t => t.UserId == token.UserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
            
            return null;
        }

        token.RevokedAt = now;
        await db.SaveChangesAsync(ct);
        return await userManager.FindByIdAsync(token.UserId.ToString());
    }
    
    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null, ct);
        
        if (token is not null)
        {
            token.RevokedAt = timeProvider.GetUtcNow();
            
            await db.SaveChangesAsync(ct);
        }
    }

    public static UserDto ToUserDto(AppUser user, string role) => new(user.Id, user.Email ?? "", user.FullName, role);

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}