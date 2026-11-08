namespace OrderSense.Client.Core.ViewModels;

public sealed record FilterOption(string Label, string? Value)
{
    public override string ToString() => Label;
}
