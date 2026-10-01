using System.ComponentModel.DataAnnotations;

namespace OrderSense.Api.Dtos;

public record CreateUserRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password,
    [property: MaxLength(100)] string? FullName,
    [property: Required] string Role);