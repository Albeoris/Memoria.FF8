using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private TexturePublication PrepareTexturePublication(IReadOnlyDictionary<TextureKey, TextureValue> changed)
    {
        List<TexturePublication> prepared = [];
        var background = world?.PrepareTexturePublication(changed, resourceHost!.Resources.Generation + 1);
        var previous = resourceHost!.Resources.CaptureTextureValues(changed.Keys);
        try
        {
            if (nativeTextFont?.PrepareTexturePublication(changed) is { } font)
            {
                prepared.Add(font);
            }
            foreach (GpuTriangleRenderer gpu in new[] { fieldGpu, battleGpu }.OfType<GpuTriangleRenderer>())
            {
                IEnumerable<PixelImage> used = changed.Where(pair => previous.TryGetValue(pair.Key, out TextureValue? value) && gpu.ContainsTexture(value.Image)).SelectMany(pair => new[] { pair.Value.Image }.Concat(pair.Value.MipLevels ?? []).Concat(pair.Value.Variants?.Values ?? Enumerable.Empty<PixelImage>()));
                if (ReferenceEquals(gpu, fieldGpu) && background is not null)
                {
                    used = used.Concat(background.Images);
                }
                IEnumerable<PixelImage> retired = previous.Values.Select(value => value.Image);
                if (ReferenceEquals(gpu, fieldGpu) && background is not null)
                {
                    retired = retired.Concat(background.RetiredImages);
                }
                prepared.Add(gpu.PrepareTexturePublication(used, retired));
            }
        }
        catch
        {
            foreach (TexturePublication publication in prepared)
            {
                publication.Dispose();
            }
            throw;
        }
        return new TexturePublication(() =>
        {
            foreach (TexturePublication publication in prepared)
            {
                publication.Commit();
            }
            background?.Commit();
        }, () =>
        {
            foreach (TexturePublication publication in prepared)
            {
                publication.Dispose();
            }
        });
    }
}
