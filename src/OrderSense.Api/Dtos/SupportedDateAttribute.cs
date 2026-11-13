using System.ComponentModel.DataAnnotations;

namespace OrderSense.Api.Dtos;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SupportedDateAttribute() : ValidationAttribute($"Date must be between {Min:yyyy-MM-dd} and {Max:yyyy-MM-dd}")
{
    public static readonly DateOnly Min = new(2000, 1, 1);
    public static readonly DateOnly Max = new(2099, 12, 31);

    public override bool IsValid(object? value) => value is not DateOnly date || (date >= Min && date <= Max);
}