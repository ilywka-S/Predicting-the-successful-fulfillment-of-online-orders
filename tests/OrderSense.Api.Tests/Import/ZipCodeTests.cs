using OrderSense.Olist;

namespace OrderSense.Api.Tests.Import;

public sealed class ZipCodeTests
{
    [Theory]
    [InlineData("1151", "01151")]
    [InlineData("01151", "01151")]
    [InlineData(" 1151 ", "01151")]
    [InlineData("20040", "20040")]
    [InlineData("", "00000")]
    [InlineData(null, "00000")]
    public void Normalize_PadsToFiveCharacters(string? input, string expected)
    {
        string result = ZipCode.Normalize(input);

        Assert.Equal(expected, result);
    }
}