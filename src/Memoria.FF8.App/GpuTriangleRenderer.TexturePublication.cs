using System.Collections.Concurrent;
using Albeoris.Games.FF8.Imaging;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

public sealed partial class GpuTriangleRenderer
{
    private readonly ConcurrentQueue<Texture2D> discardedPublicationTextures = new();

    internal bool ContainsTexture(PixelImage image) => textures.TryGetValue(image, out _);

    /// <summary>Allocate and upload a complete candidate without evicting any usable current texture.</summary>
    internal TexturePublication PrepareTexturePublication(IEnumerable<PixelImage> images, IEnumerable<PixelImage>? retiredImages = null)
    {
        if (active)
        {
            throw new InvalidOperationException("Publish textures between rendered frames.");
        }
        DrainDiscardedPublicationTextures();
        PixelImage[] required = images.Distinct<PixelImage>(ReferenceEqualityComparer.Instance).ToArray();
        PixelImage[] candidates = required.Where(image => !textures.TryGetValue(image, out _)).ToArray();
        HashSet<PixelImage> retained = new(PresentedTextureImages, ReferenceEqualityComparer.Instance);
        retained.ExceptWith(retiredImages ?? []);
        retained.UnionWith(required);
        if (retained.Count > maximumCachedTextures || retained.Sum(image => (long)image.Pixels.Length * 4) > maximumCachedTextureBytes)
        {
            throw new InvalidDataException("The active texture update exceeds the graphics publication budget. Import a smaller coherent selection or reduce image resolution.");
        }
        List<(PixelImage Image, Texture2D Texture)> prepared = [];
        try
        {
            foreach (PixelImage image in candidates)
            {
                Texture2D texture = new(device, image.Width, image.Height, false, SurfaceFormat.Color);
                prepared.Add((image, texture));
                texture.SetData(image.Pixels);
            }
        }
        catch
        {
            foreach (var item in prepared)
            {
                item.Texture.Dispose();
            }
            throw;
        }
        return new TexturePublication(() =>
        {
            foreach ((PixelImage image, Texture2D texture) in prepared)
            {
                long bytes = (long)image.Pixels.Length * 4;
                textures.Add(image, recentTextures.AddLast(new CachedTexture(new(image), texture, bytes)));
                cachedTextureBytes += bytes;
            }
            for (LinkedListNode<CachedTexture>? node = recentTextures.First; node is not null && (recentTextures.Count > maximumCachedTextures || cachedTextureBytes > maximumCachedTextureBytes);)
            {
                LinkedListNode<CachedTexture>? next = node.Next;
                if (!node.Value.Image.TryGetTarget(out PixelImage? image) || !retained.Contains(image))
                {
                    RemoveCachedTexture(node);
                }
                node = next;
            }
        }, () =>
        {
            foreach (var item in prepared)
            {
                discardedPublicationTextures.Enqueue(item.Texture);
            }
        });
    }

    private void DrainDiscardedPublicationTextures()
    {
        while (discardedPublicationTextures.TryDequeue(out Texture2D? texture))
        {
            texture.Dispose();
        }
    }
}

internal sealed class TexturePublication(Action commit, Action discard) : IDisposable
{
    private bool completed;

    public void Commit()
    {
        if (completed)
        {
            throw new InvalidOperationException("Texture publication was already completed.");
        }
        commit();
        completed = true;
    }

    public void Dispose()
    {
        if (!completed)
        {
            completed = true;
            discard();
        }
    }
}
