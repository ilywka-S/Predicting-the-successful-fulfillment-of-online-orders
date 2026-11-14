namespace OrderSense.Client.Core.Interfaces;

public interface IThemeService
{
    string Current { get; }

    void Apply(string theme);
}
