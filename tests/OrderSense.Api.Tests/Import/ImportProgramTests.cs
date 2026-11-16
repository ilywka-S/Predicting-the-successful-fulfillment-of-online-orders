using OrderSense.OlistImport;

namespace OrderSense.Api.Tests.Import;

public class ImportProgramTests
{
    [Fact]
    public async Task Main_WithoutArguments_ReturnsErrorCode()
    {
        int code = await ImportProgram.Main([]);

        Assert.Equal(1, code);
    }
}
