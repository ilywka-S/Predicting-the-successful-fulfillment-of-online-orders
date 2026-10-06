using System.ComponentModel.DataAnnotations;

namespace OrderSense.Api.Dtos;

public record CreateUserRequest([Required, EmailAddress] string Email, [Required] string Password, [MaxLength(100)] string? FullName, [Required] string Role);