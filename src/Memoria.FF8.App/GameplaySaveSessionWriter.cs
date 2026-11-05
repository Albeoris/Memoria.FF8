using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Publishes an explicitly selected gameplay document only after normal-load validation succeeds.</summary>
internal static class GameplaySaveSessionWriter
{
    internal static GameState Write(string path, GameplaySaveComponentDocument document, string gameDirectory, GameAssets? sharedAssets = null, ResourceService? resources = null, IModelOverrideSource? modelOverrides = null, Func<GameplayTuningPolicy>? readGameplayPolicy = null, Action<GameState>? validateContinuation = null, DateTime? savedAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);
        document = document with { SavedLocalAt = DateTimeOffset.Now };
        document.Validate();
        string destination = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        bool temporaryCreated = false;
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                temporaryCreated = true;
                GameplaySaveCodec.Write(stream, document);
                stream.Flush(flushToDisk: true);
            }
            GameState restored = GameplaySaveSessionLoader.Load(temporary, gameDirectory, sharedAssets, resources, modelOverrides, readGameplayPolicy);
            validateContinuation?.Invoke(restored);
            if (savedAtUtc is { } timestamp)
            {
                File.SetLastWriteTimeUtc(temporary, timestamp);
            }
            File.Move(temporary, destination, overwrite: true);
            return restored;
        }
        finally
        {
            if (temporaryCreated)
            {
                File.Delete(temporary);
            }
        }
    }
}
