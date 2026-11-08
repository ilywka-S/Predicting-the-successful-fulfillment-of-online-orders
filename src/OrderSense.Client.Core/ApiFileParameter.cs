namespace OrderSense.Client.Core;

public partial class FileParameter
{
    public FileParameter(Stream data)
        : this(data, null, null)
    {
    }

    public FileParameter(Stream data, string? fileName)
        : this(data, fileName, null)
    {
    }

    public FileParameter(Stream data, string? fileName, string? contentType)
    {
        Data = data;
        FileName = fileName;
        ContentType = contentType;
    }

    public Stream Data { get; }

    public string? FileName { get; }

    public string? ContentType { get; }
}
