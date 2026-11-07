using System.IO;
using System.Threading.Tasks;

namespace OrderSense.Client.Core.Interfaces;

public interface IFileService
{
    Task<(Stream? Stream, string FileName)> PickCsvFileAsync();
    Task<bool> SaveFileAsync(string fileName, Stream stream);
}