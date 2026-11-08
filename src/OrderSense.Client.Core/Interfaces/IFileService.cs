using System.IO;
using System.Threading.Tasks;

namespace OrderSense.Client.Core.Interfaces;

public class FileSelectionResult
{
    public string FileName { get; set; } = string.Empty;
    public Func<Task<Stream>> OpenReadAsync { get; set; } = null!;
}

public interface IFileService
{
    Task<bool> SaveFileAsync(string fileName, Stream stream);
    
    Task<FileSelectionResult?> PickCsvAsync(); 
}