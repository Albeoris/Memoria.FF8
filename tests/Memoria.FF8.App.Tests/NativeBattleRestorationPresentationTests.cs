using System.Text.Json;
using Memoria.FF8.Runtime;
using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleRestorationPresentationTests
{
    private static void PublishJson(string? directory, string name, object value)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        Directory.CreateDirectory(directory);
        using FileStream stream = new(Path.Combine(directory, name + ".json"), FileMode.CreateNew);
        object captured = NativeBattleStorageSnapshots.Capture(value);
        JsonSerializer.Serialize(stream, captured, captured.GetType());
    }

    internal static void Publish(string? directory, string name, BattleSnapshot snapshot, byte[]? pixels)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        PublishJson(directory, name + ".battle", snapshot);
        if (pixels is null)
        {
            return;
        }
        using Bitmap bitmap = new(320, 224);
        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                int offset = (y * 320 + x) * 4;
                bitmap.SetPixel(x, y, Color.FromArgb(pixels[offset + 3], pixels[offset], pixels[offset + 1], pixels[offset + 2]));
            }
        }
        using FileStream stream = new(Path.Combine(directory, name + ".png"), FileMode.CreateNew);
        bitmap.SavePng(stream);
    }
}
