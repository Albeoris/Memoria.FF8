using Memoria.FF8.Mods;

namespace Memoria.FF8;

/// <summary>Keeps original and package resources mounted until both players and any pending seek have stopped.</summary>
internal sealed class CatalogMediaComparison(ModPreviewMediaSources sources, CatalogMediaPlayback playback)
{
    private readonly List<Task> retirements = [];

    public CatalogMediaPlayback Playback { get; private set; } = playback;

    public static async Task<CatalogMediaComparison> OpenAsync(ModPreviewDocument document, ModPreviewEntry entry, ModPreviewResourcesProfile profile, string? tools, CancellationToken token, bool audioEnabled = true, PcmAudioOutput? output = null)
    {
        ModPreviewMediaSources sources = await document.OpenMediaAsync(entry, profile, token).ConfigureAwait(false);
        try
        {
            CatalogMediaPlayback player = await CatalogMediaPlayback.OpenAsync(tools, CatalogNativeMedia.WithNativeDecoder(sources.Before), CatalogNativeMedia.WithNativeDecoder(sources.After), sources.Video, token, audioEnabled: audioEnabled, output: output).ConfigureAwait(false);
            player.ListenAfter = !player.Before.HasAudio && player.After.HasAudio;
            return new(sources, player);
        }
        catch
        {
            sources.Dispose();
            throw;
        }
    }

    public void ReplacePlayback(CatalogMediaPlayback replacement)
    {
        replacement.ListenAfter = Playback.ListenAfter;
        Retire(Playback);
        Playback = replacement;
    }

    public async Task ReleaseAsync(Task<CatalogMediaPlayback>? pendingSeek = null)
    {
        Retire(Playback);
        try
        {
            if (pendingSeek is not null)
            {
                try
                {
                    Retire(await pendingSeek.ConfigureAwait(false));
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    // A failed or cancelled seek releases its temporary inputs before completing.
                }
            }
            await Task.WhenAll(retirements).ConfigureAwait(false);
        }
        finally
        {
            sources.Dispose();
        }
    }

    private void Retire(CatalogMediaPlayback previous)
    {
        retirements.RemoveAll(task => task.IsCompletedSuccessfully);
        retirements.Add(previous.DisposeAsync().AsTask());
    }

    public static ModPreviewSide Describe(CatalogMediaSide side, string unavailable)
    {
        if (string.IsNullOrWhiteSpace(side.Name))
        {
            return new(unavailable);
        }
        if (side.Error is { Length: > 0 } error)
        {
            return new($"{side.Name}\n{error}");
        }
        return new(side.HasVideo && !side.HasAudio ? $"{side.Name}\nNo audio track." : side.Name);
    }
}
