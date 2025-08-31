using Avalonia.Platform.Storage;

namespace ArkPlot.Avalonia.Services;

internal static class GlobalStorageProvider
{
    public static IStorageProvider StorageProvider { get; set; } = null!;
}
