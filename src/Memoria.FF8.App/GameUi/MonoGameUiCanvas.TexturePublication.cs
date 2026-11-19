using System.Collections.Concurrent;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.GameUi;

internal sealed partial class MonoGameUiCanvas
{
    private readonly ConcurrentQueue<Texture2D> retiredUiPublicationTextures = new();
    private readonly Dictionary<UiImageSource, (string Package, string Path, long Revision)> itemImageVersions = [];
    internal long ImageUploadCount { get; private set; }

    internal TexturePublication? PrepareTexturePublication(ResourceService.PreparedResources prepared, IReadOnlyDictionary<TextureKey, TextureValue> changed, long nextGeneration, long maximumBytes = 256L * 1024 * 1024)
    {
        DrainUiPublicationTextures();
        if (resources is null)
        {
            return null;
        }
        if (changed.Count == 0)
        {
            return new TexturePublication(() => artworkGeneration = nextGeneration, () => { });
        }
        bool Affected(PixelImage image) => resources.CaptureTextureSourcesForImages([image]).Any(snapshot => changed.ContainsKey(snapshot.Context.Key));
        var affected = imageSources.Where(pair => images.ContainsKey(pair.Key) && Affected(pair.Value)).ToArray();
        bool replacePaper = paper is not null && paperSourceHandle is not null && changed.ContainsKey(PaperSourceKey);
        bool replacePointer = menuPointer is not null && pointerSource is not null && Affected(pointerSource);
        if (affected.Length == 0 && !replacePointer && !replacePaper)
        {
            return new TexturePublication(() => artworkGeneration = nextGeneration, () => { });
        }
        ResourceService inspection = prepared.CreateInspectionService();
        MenuArtworkProvider provider = new(gameDirectory, inspection, findItemUseImage);
        List<(UiImageSource Source, PixelImage Image, Texture2D Previous, Texture2D Texture)> candidates = [];
        Texture2D? stagedPointer = null;
        PixelImage? stagedPointerImage = null;
        Texture2D? previousPointer = menuPointer;
        Texture2D? previousPaper = paper;
        Texture2D? stagedPaper = null;
        TextureValue? stagedPaperValue = null;
        UiColor stagedPaperColor = default;
        HashSet<UiImageSource> replaced = affected.Select(pair => pair.Key).ToHashSet();
        long prospectiveBytes = images.Where(pair => !replaced.Contains(pair.Key)).Sum(pair => (long)pair.Value.Width * pair.Value.Height * 4) + (!replacePointer && menuPointer is not null ? (long)menuPointer.Width * menuPointer.Height * 4 : 0) + (!replacePaper && paper is not null ? (long)paper.Width * paper.Height * 4 : 0);

        void BindProvenance(PixelImage image)
        {
            TextureKey[] keys = inspection.CaptureTextureSourcesForImages([image]).Select(snapshot => snapshot.Context.Key).Distinct().ToArray();
            var unchanged = resources.CaptureTextureValues(keys.Where(key => !changed.ContainsKey(key)));
            PixelImage[] parents = keys.Select(key => changed.GetValueOrDefault(key) ?? unchanged.GetValueOrDefault(key)).OfType<TextureValue>().Select(value => value.Image).Distinct<PixelImage>(ReferenceEqualityComparer.Instance).ToArray();
            if (parents.Length == 0)
            {
                throw new InvalidDataException("The prepared UI image lost its source texture dependencies. Retain the current UI generation and refresh the texture mod.");
            }
            resources.RegisterTextureImageDependencies(image, parents);
        }

        Texture2D Upload(PixelImage image)
        {
            prospectiveBytes = checked(prospectiveBytes + (long)image.Width * image.Height * 4);
            if (prospectiveBytes > maximumBytes)
            {
                throw new InvalidDataException("The prepared menu textures exceed the 256 MiB UI publication budget. Reduce replacement resolution or import a smaller coherent selection.");
            }
            Texture2D texture = new(device, image.Width, image.Height);
            try
            {
                texture.SetData(image.Pixels.Select(color => Color.FromNonPremultiplied(color.R, color.G, color.B, color.A)).ToArray());
                ImageUploadCount++;
                return texture;
            }
            catch
            {
                texture.Dispose();
                throw;
            }
        }

        try
        {
            foreach (var affectedImage in affected)
            {
                PixelImage image = provider.Read(affectedImage.Key, text.FontFamily);
                BindProvenance(image);
                Texture2D uploaded = Upload(image);
                candidates.Add((affectedImage.Key, image, images[affectedImage.Key], uploaded));
            }
            if (replacePointer)
            {
                stagedPointerImage = NativeMenuPointer.Read(gameDirectory, inspection).Image;
                BindProvenance(stagedPointerImage);
                stagedPointer = Upload(stagedPointerImage);
            }
            if (replacePaper)
            {
                stagedPaperValue = changed[PaperSourceKey];
                var mean = NativeMagazineArtwork.GetPaperColor(stagedPaperValue.Image);
                stagedPaperColor = new(mean.R, mean.G, mean.B);
                PixelImage image = new(previousPaper!.Width, previousPaper.Height, UiPaper.CreatePixels(previousPaper.Width, previousPaper.Height, stagedPaperColor).Select(color => new Rgba32(color.R, color.G, color.B, color.A)).ToArray());
                resources.RegisterTextureImageDependencies(image, [stagedPaperValue.Image]);
                stagedPaper = Upload(image);
            }
        }
        catch
        {
            foreach (var candidate in candidates)
            {
                candidate.Texture.Dispose();
            }
            stagedPointer?.Dispose();
            stagedPaper?.Dispose();
            throw;
        }
        return new TexturePublication(() =>
        {
            foreach (var candidate in candidates)
            {
                if (!images.TryGetValue(candidate.Source, out Texture2D? current) || !ReferenceEquals(current, candidate.Previous))
                {
                    throw new InvalidOperationException("The UI texture cache changed during preparation; prepare the current generation again.");
                }
            }
            if (replacePointer && !ReferenceEquals(menuPointer, previousPointer))
            {
                throw new InvalidOperationException("The menu pointer changed during preparation; prepare the current generation again.");
            }
            if (replacePaper && !ReferenceEquals(paper, previousPaper))
            {
                throw new InvalidOperationException("The magazine paper changed during preparation; prepare the current generation again.");
            }
            foreach (var candidate in candidates)
            {
                images[candidate.Source] = candidate.Texture;
                imageSources[candidate.Source] = candidate.Image;
                retiredUiPublicationTextures.Enqueue(candidate.Previous);
            }
            if (replacePointer)
            {
                menuPointer = stagedPointer;
                pointerSource = stagedPointerImage;
                retiredUiPublicationTextures.Enqueue(previousPointer!);
            }
            if (replacePaper)
            {
                paper = stagedPaper;
                paperColor = stagedPaperColor;
                boundPaperColor = stagedPaperColor;
                boundPaperValue = stagedPaperValue;
                retiredUiPublicationTextures.Enqueue(previousPaper!);
            }
            artworkGeneration = nextGeneration;
        }, () =>
        {
            foreach (var candidate in candidates)
            {
                retiredUiPublicationTextures.Enqueue(candidate.Texture);
            }
            if (stagedPointer is not null)
            {
                retiredUiPublicationTextures.Enqueue(stagedPointer);
            }
            if (stagedPaper is not null)
            {
                retiredUiPublicationTextures.Enqueue(stagedPaper);
            }
        });
    }

    private void DrainUiPublicationTextures()
    {
        while (retiredUiPublicationTextures.TryDequeue(out Texture2D? texture))
        {
            if (ReferenceEquals(device.Textures[0], texture))
            {
                device.Textures[0] = null;
            }
            texture.Dispose();
        }
    }

    private void RefreshItemImageVersion(UiImageSource source)
    {
        if (source.Kind != UiImageKind.ItemUseImage)
        {
            return;
        }
        var file = findItemUseImage?.Invoke(source.Id);
        var current = (file?.PackageId ?? "", file?.RelativePath ?? "", file?.Revision ?? -1);
        if (itemImageVersions.TryGetValue(source, out var previous) && previous != current && images.Remove(source, out Texture2D? texture))
        {
            texture.Dispose();
            imageSources.Remove(source);
        }
        itemImageVersions[source] = current;
    }
}
