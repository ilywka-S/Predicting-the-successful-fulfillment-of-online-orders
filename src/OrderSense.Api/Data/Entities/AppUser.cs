using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace OrderSense.Api.Data.Entities;

public class AppUser : IdentityUser<Guid>
{
    [MaxLength(100)]
    public string? FullName { get; set; }
}