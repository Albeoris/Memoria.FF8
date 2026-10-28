using System.Net;
using Memoria.FF8.GameUi;
using SkiaSharp;

namespace Memoria.FF8.App.Tests;

public sealed class ModCatalogPreviewCacheTests
{
    [Theory]
    [InlineData("https://example.invalid/preview.png", true)]
    [InlineData("http://example.invalid/preview.png", false)]
    [InlineData("https://user:password@example.invalid/image", false)]
    [InlineData("file:///image.png", false)]
    [InlineData("javascript:alert(1)", false)]
    public void PreviewPolicyAcceptsOnlyCredentialFreeHttps(string url, bool expected)
    {
        Assert.Equal(expected, ModCatalogPreviewCache.IsAllowedUrl(url));
    }

    [Fact]
    public void DecoderCapsUploadAndRejectsOversizedSourcesAndHtml()
    {
        ModCatalogPreviewCache.Decoded decoded = ModCatalogPreviewCache.Decode("fixture", Png(1024, 512));
        Assert.Equal(512, decoded.Width);
        Assert.Equal(256, decoded.Height);
        Assert.Equal(512 * 256 * 4, decoded.Pixels.Length);
        Assert.Throws<InvalidDataException>(() => ModCatalogPreviewCache.Decode("fixture", Png(2049, 1)));
        Assert.Throws<InvalidDataException>(() => ModCatalogPreviewCache.Decode("fixture", "<html>quota</html>"u8.ToArray()));
        Assert.Throws<InvalidDataException>(() => ModCatalogPreviewCache.Decode("fixture", new byte[ModCatalogPreviewCache.MaximumResponseBytes + 1]));
    }

    [Fact]
    public async Task ChangingSelectionCancelsOldRequestAndOnlyPublishesNewSelection()
    {
        string root = TemporaryRoot();
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using HttpClient client = new(new Handler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/first")
            {
                firstStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Png(12, 8)) };
        }));
        using (ModCatalogPreviewCache cache = new(root, client))
        {
            cache.Request("https://example.invalid/first");
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            cache.Request("https://example.invalid/second");
            ModCatalogPreviewCache.Decoded ready = await Wait(cache);
            Assert.Equal("https://example.invalid/second", ready.Url);
        }
        await Cleanup(root);
    }

    [Fact]
    public async Task FreshDiskPreviewIsReusedWithoutNetwork()
    {
        string root = TemporaryRoot();
        int requests = 0;
        using HttpClient client = new(new Handler((_, _) =>
        {
            Interlocked.Increment(ref requests);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Png(12, 8)) });
        }));
        using (ModCatalogPreviewCache first = new(root, client))
        {
            first.Request("https://example.invalid/cached");
            _ = await Wait(first);
        }
        using (ModCatalogPreviewCache second = new(root, client))
        {
            second.Request("https://example.invalid/cached");
            _ = await Wait(second);
        }
        Assert.Equal(1, requests);
        await Cleanup(root);
    }

    private static async Task<ModCatalogPreviewCache.Decoded> Wait(ModCatalogPreviewCache cache)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            ModCatalogPreviewCache.Decoded? ready = cache.TakeCompleted();
            if (ready != null)
            {
                return ready;
            }
            await Task.Delay(10, deadline.Token);
        }
    }

    private static byte[] Png(int width, int height)
    {
        using SKBitmap bitmap = new(width, height);
        bitmap.Erase(SKColors.Red);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string TemporaryRoot() => Path.Combine(Path.GetTempPath(), "mod-preview-test-" + Guid.NewGuid().ToString("N"));
    private static async Task Cleanup(string root)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
                return;
            }
            catch (IOException)
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
    }
}
