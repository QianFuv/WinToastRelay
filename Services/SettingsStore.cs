using System.Text.Json;
using Windows.Storage;
using WinToastRelay.Models;

namespace WinToastRelay.Services;

public sealed class SettingsStore
{
    private const string FileName = "relay-settings.json";
    public async Task<RelaySettings> LoadAsync()
    {
        try
        {
            var file = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileName) as StorageFile;
            if (file is null) return new RelaySettings();
            var json = await FileIO.ReadTextAsync(file);
            return JsonSerializer.Deserialize(json, AppJsonContext.Default.RelaySettings) ?? new RelaySettings();
        }
        catch (JsonException)
        {
            return new RelaySettings();
        }
    }

    public async Task SaveAsync(RelaySettings settings)
    {
        var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting);
        await FileIO.WriteTextAsync(file, JsonSerializer.Serialize(settings, AppJsonContext.Default.RelaySettings));
    }
}
