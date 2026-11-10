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

    public async Task<bool> SaveFileAsync(string fileName, Stream stream)
    {
        var result = await _fileSaver.SaveAsync(fileName, stream, default);
        return result.IsSuccessful;
    }

    public async Task<FileSelectionResult?> PickCsvAsync()
    {
        var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.WinUI, new[] { ".csv" } },
            { DevicePlatform.Android, new[] { "text/comma-separated-values", "text/csv" } }
        });

        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Виберіть CSV файл",
            FileTypes = customFileType
        });

        if (result == null) return null;

        return new FileSelectionResult
        {
            FileName = result.FileName,
            OpenReadAsync = () => result.OpenReadAsync()
        };
    }
}