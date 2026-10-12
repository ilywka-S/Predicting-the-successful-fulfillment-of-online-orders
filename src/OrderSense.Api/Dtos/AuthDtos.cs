using System.ComponentModel.DataAnnotations;

namespace OrderSense.Api.Dtos;

public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public record RefreshRequest([Required] string RefreshToken);

public record UserDto(Guid Id, string Email, string? Name, string Role);

public record AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, UserDto User);