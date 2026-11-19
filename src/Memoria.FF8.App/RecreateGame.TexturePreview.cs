using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private string? texturePreviewIdentity;
    private bool texturePreviewPending;
    private Task? texturePreviewWork;
    private CancellationTokenSource? texturePreviewCancellation;

    private void UpdateTexturePreview()
    {
        if (textureBrowser is not { Busy: false, Selected: { } selected } browser)
        {
            return;
        }
        if (selected.GroupNode)
        {
            browser.PreviewAvailable = false;
            return;
        }
        string identity = selected.Id + "\0" + browser.Resolved + "\0" + browser.Matching;
        if (texturePreviewIdentity == identity || texturePreviewPending)
        {
            return;
        }
        texturePreviewIdentity = identity;
        browser.PreviewAvailable = false;
        texturePreviewCancellation?.Dispose();
        texturePreviewCancellation = new();
        CancellationToken token = texturePreviewCancellation.Token;
        bool resolved = browser.Resolved;
        TextureImportReview? review = browser.Matching ? null : textureReview;
        TextureImportRow? import = review?.Rows.FirstOrDefault(row => row.RelativePath == selected.Id);
        TextureSourceSnapshot? scene = browser.Scope == 0 && review is null ? textureScene.FirstOrDefault(snapshot => TextureId(snapshot.Target) == selected.Id) : null;
        TextureCatalogEntry? entry = textureCatalog.FirstOrDefault(item => TextureId(item.Target) == selected.Id);
        var createSession = scene is not null ? (resolved ? textureSceneResolvedFactory : textureSceneOriginalFactory) ?? resourceHost!.CaptureTextureAuthoringFactory(resolved) : resourceHost!.CaptureTextureAuthoringFactory(resolved);
        texturePreviewPending = true;
        texturePreviewWork = Task.Run(() =>
        {
            try
            {
                PixelImage image;
                if (import is { Workspace: { } workspace })
                {
                    using var authoring = createSession();
                    image = TexturePixelExchange.ToEditable(authoring.Assets.ImportFieldTextureWorkspace(Path.Combine(review!.Source, workspace.Replace('/', Path.DirectorySeparatorChar)), token)[import.WorkspaceTile].Image);
                }
                else if (import is not null)
                {
                    image = TextureImageDecoder.Decode(ReadStableTextureBytes(Path.Combine(review!.Source, import.RelativePath.Replace('/', Path.DirectorySeparatorChar)), token), import.RelativePath).Image;
                }
                else if (scene is not null)
                {
                    TextureBuildContext context = scene.Context;
                    TextureValue? installedOriginal = scene.InstalledOriginal;
                    if (scene.Context.Layout == "native-effect-texture")
                    {
                        using var authoring = createSession();
                        IReadOnlyList<TextureBuildContext> sources = authoring.Assets.LoadAuthoringTextures(scene);
                        context = sources.FirstOrDefault(candidate => TextureTarget.FromContext(candidate) == scene.Target) ?? sources.First();
                        installedOriginal = authoring.Assets.Resources.ResolveOriginalForExport(context);
                        image = resolved ? authoring.Assets.Resources.ResolveForExport(context).Image : installedOriginal.Image;
                    }
                    else
                    {
                        image = resolved ? scene.Resolved.Image : scene.Original;
                    }
                    if (installedOriginal is null ? TexturePixelExchange.UsesNativeFlags(context) : TexturePixelExchange.UsesNativeFlags(context, installedOriginal))
                    {
                        image = TexturePixelExchange.ToEditable(image);
                    }
                }
                else if (entry is not null)
                {
                    using var authoring = createSession();
                    TextureBuildContext context = authoring.Assets.LoadCatalogTexture(entry);
                    TextureValue installedOriginal = authoring.Assets.Resources.ResolveOriginalForExport(context);
                    image = resolved ? authoring.Assets.Resources.ResolveForExport(context).Image : installedOriginal.Image;
                    if (TexturePixelExchange.UsesNativeFlags(context, installedOriginal))
                    {
                        image = TexturePixelExchange.ToEditable(image);
                    }
                }
                else
                {
                    throw new InvalidDataException("No preview source is available.");
                }
                token.ThrowIfCancellationRequested();
                PixelImage thumbnail = CreateTextureThumbnail(image);
                textureCompletions.Enqueue(() =>
                {
                    texturePreviewPending = false;
                    if (!ReferenceEquals(textureBrowser, browser) || token.IsCancellationRequested || texturePreviewIdentity != identity || uiCanvas is null)
                    {
                        return;
                    }
                    Texture2D texture = new(GraphicsDevice, thumbnail.Width, thumbnail.Height);
                    try
                    {
                        texture.SetData(thumbnail.Pixels.Select(pixel => Color.FromNonPremultiplied(pixel.R, pixel.G, pixel.B, pixel.A)).ToArray());
                        uiCanvas.RegisterHostImage(TextureBrowserController.PreviewImageId, texture);
                        browser.PreviewAvailable = true;
                    }
                    catch
                    {
                        texture.Dispose();
                        throw;
                    }
                });
            }
            catch (Exception exception) when (IsModelBrowserError(exception) || exception is OperationCanceledException)
            {
                textureCompletions.Enqueue(() => texturePreviewPending = false);
            }
        });
    }

    private void LoadImportedTexturePreview(TextureImportReview review, TextureImportRow row)
    {
        TextureBrowserController browser = textureBrowser!;
        var createSession = resourceHost!.CaptureTextureAuthoringFactory(false);
        string path = Path.Combine(review.Source, (row.Workspace ?? row.RelativePath).Replace('/', Path.DirectorySeparatorChar));
        StartTextureJob(token =>
        {
            PixelImage image;
            if (row.Workspace is not null)
            {
                using var authoring = createSession();
                image = TexturePixelExchange.ToEditable(authoring.Assets.ImportFieldTextureWorkspace(path, token)[row.WorkspaceTile].Image);
            }
            else if (Path.GetExtension(path).Equals(".ora", StringComparison.OrdinalIgnoreCase))
            {
                RejectTextureReparsePath(path);
                using System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(path);
                var entries = archive.Entries.Where(entry => entry.FullName == "mergedimage.png").ToArray();
                if (entries.Length != 1 || entries[0].Length > 128L * 1024 * 1024)
                {
                    throw new InvalidDataException("The ORA document needs one bounded mergedimage.png for visual matching. Restore its editable layers and source companion before importing.");
                }
                using Stream input = entries[0].Open();
                using MemoryStream bytes = new();
                byte[] buffer = new byte[65536];
                int read;
                while ((read = input.Read(buffer)) != 0)
                {
                    token.ThrowIfCancellationRequested();
                    if (bytes.Length + read > 128L * 1024 * 1024)
                    {
                        throw new InvalidDataException("The imported ORA preview exceeds its decoded input budget.");
                    }
                    bytes.Write(buffer.AsSpan(0, read));
                }
                token.ThrowIfCancellationRequested();
                image = TextureImageDecoder.Decode(bytes.ToArray(), "mergedimage.png").Image;
            }
            else
            {
                image = TextureImageDecoder.Decode(ReadStableTextureBytes(path, token), path).Image;
            }
            return CreateTextureThumbnail(image);
        }, thumbnail =>
        {
            if (!ReferenceEquals(textureBrowser, browser) || !browser.Matching || uiCanvas is null)
            {
                return;
            }
            Texture2D texture = new(GraphicsDevice, thumbnail.Width, thumbnail.Height);
            try
            {
                texture.SetData(thumbnail.Pixels.Select(pixel => Color.FromNonPremultiplied(pixel.R, pixel.G, pixel.B, pixel.A)).ToArray());
                uiCanvas.RegisterHostImage(TextureBrowserController.ImportedPreviewImageId, texture);
                browser.ImportedPreviewAvailable = true;
                browser.Message = LocalizeAuthored("textures.match.compare", "Compare the imported image with the candidate Original / Resolved source. Confirm an exact texture leaf to remember its assignment.");
            }
            catch
            {
                texture.Dispose();
                throw;
            }
        }, LocalizeAuthored("textures.match.preview", "Reading imported image for comparison..."));
    }

    private static PixelImage CreateTextureThumbnail(PixelImage source)
    {
        float scale = Math.Min(1, Math.Min(192f / source.Width, 192f / source.Height));
        int width = Math.Max(1, (int)(source.Width * scale));
        int height = Math.Max(1, (int)(source.Height * scale));
        Rgba32[] pixels = new Rgba32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = source.Pixels[(int)((long)y * source.Height / height) * source.Width + (int)((long)x * source.Width / width)];
            }
        }
        return new(width, height, pixels);
    }
}
