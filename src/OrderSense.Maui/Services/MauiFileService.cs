using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Maui.Services;

public class MauiFileService : IFileService
{
    private readonly IFileSaver _fileSaver;

    public MauiFileService(IFileSaver fileSaver)
    {
        _fileSaver = fileSaver;
    }

    public async Task<(Stream? Stream, string FileName)> PickCsvFileAsync()
    {
        var customFileType = new FilePickerFileType(
            new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".csv" } },
                { DevicePlatform.Android, new[] { "text/comma-separated-values", "text/csv" } },
            });

        var options = new PickOptions
        {
            PickerTitle = "Оберіть CSV файл із замовленнями",
            FileTypes = customFileType,
        };

        var result = await FilePicker.Default.PickAsync(options);
        
        if (result == null) 
            return (null, string.Empty);
        
        var stream = await result.OpenReadAsync();
        return (stream, result.FileName);
    }

    public async Task<bool> SaveFileAsync(string fileName, Stream stream)
    {
        var result = await _fileSaver.SaveAsync(fileName, stream, default);
        return result.IsSuccessful;
    }
}