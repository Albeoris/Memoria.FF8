using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using SkiaSharp;

namespace Memoria.FF8.GameUi;

/// <summary>A single, cancellable selected-preview worker. All transport, cache and decoding work stays off the owner thread.</summary>
internal sealed class ModCatalogPreviewCache : IDisposable
{
    internal const int MaximumResponseBytes = 8 * 1024 * 1024;
    internal const int MaximumSourceDimension = 2048;
    internal const long MaximumCacheBytes = 16 * 1024 * 1024;
    private const int MaximumDisplayDimension = 512;
    internal sealed record Decoded(string Url, int Width, int Height, byte[] Pixels);
    private readonly string directory;
    private readonly HttpClient client;
    private readonly bool ownsClient;
    private readonly Channel<string> requests = Channel.CreateBounded<string>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task worker;
    private CancellationTokenSource? active;
    private string? selected;
    private Decoded? completed;

    internal ModCatalogPreviewCache(string directory, HttpClient? client = null)
    {
        this.directory = directory;
        ownsClient = client == null;
        this.client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.None }) { Timeout = Timeout.InfiniteTimeSpan };
        worker = Task.Run(RunAsync);
    }

    internal static bool IsAllowedUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo);

    internal void Request(string url)
    {
        if (Interlocked.Exchange(ref selected, url) == url)
        {
            return;
        }
        try
        {
            Volatile.Read(ref active)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The preceding selection just finished its worker scope.
        }
        if (IsAllowedUrl(url))
        {
            requests.Writer.TryWrite(url);
        }
    }

    internal Decoded? TakeCompleted() => Interlocked.Exchange(ref completed, null);

    private async Task RunAsync()
    {
        try
        {
            await foreach (string url in requests.Reader.ReadAllAsync(lifetime.Token))
            {
                if (url != Volatile.Read(ref selected))
                {
                    continue;
                }
                using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                Volatile.Write(ref active, cancellation);
                cancellation.CancelAfter(TimeSpan.FromSeconds(25));
                try
                {
                    Decoded image = await LoadAsync(url, cancellation.Token);
                    if (url == Volatile.Read(ref selected) && !cancellation.IsCancellationRequested)
                    {
                        Interlocked.Exchange(ref completed, image);
                    }
                }
                catch (Exception error) when (error is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException or InvalidDataException or NotSupportedException or ArgumentException)
                {
                    // Previews are optional. Failure leaves the stable catalog placeholder visible.
                }
                finally
                {
                    Interlocked.CompareExchange(ref active, null, cancellation);
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
    }

    private async Task<Decoded> LoadAsync(string url, CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
        string path = Path.Combine(directory, key + ".preview");
        byte[]? bytes = null;
        if (File.Exists(path) && new FileInfo(path).Length <= MaximumResponseBytes)
        {
            bytes = await File.ReadAllBytesAsync(path, token);
            if (File.GetLastWriteTimeUtc(path) > DateTime.UtcNow.AddDays(-1))
            {
                return Decode(url, bytes);
            }
        }
        try
        {
            bytes = await DownloadAsync(new Uri(url), token);
            Decoded decoded = Decode(url, bytes);
            token.ThrowIfCancellationRequested();
            string temporary = path + ".tmp";
            await File.WriteAllBytesAsync(temporary, bytes, token);
            File.Move(temporary, path, true);
            TrimCache(path);
            return decoded;
        }
        catch (HttpRequestException) when (bytes != null)
        {
            return Decode(url, bytes);
        }
    }

    private async Task<byte[]> DownloadAsync(Uri uri, CancellationToken token)
    {
        for (int redirect = 0; redirect < 8; redirect++)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Memoria.FF8/1.0");
            request.Headers.AcceptEncoding.ParseAdd("identity");
            using CancellationTokenSource headerTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            headerTimeout.CancelAfter(TimeSpan.FromSeconds(15));
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headerTimeout.Token);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                if (response.Headers.Location == null)
                {
                    throw new InvalidDataException("Preview redirect has no destination.");
                }
                uri = new Uri(uri, response.Headers.Location);
                if (!IsAllowedUrl(uri.AbsoluteUri))
                {
                    throw new InvalidDataException("Preview redirect is unsafe.");
                }
                continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumResponseBytes || response.Content.Headers.ContentEncoding.Count > 0)
            {
                throw new InvalidDataException("Preview exceeds its transfer policy.");
            }
            using Stream body = await response.Content.ReadAsStreamAsync(token);
            using MemoryStream buffer = new();
            byte[] chunk = new byte[32768];
            int count;
            while ((count = await body.ReadAsync(chunk, token)) > 0)
            {
                if (buffer.Length + count > MaximumResponseBytes)
                {
                    throw new InvalidDataException("Preview exceeds its transfer limit.");
                }
                buffer.Write(chunk, 0, count);
            }
            return buffer.ToArray();
        }
        throw new InvalidDataException("Preview exceeded its redirect limit.");
    }

    internal static Decoded Decode(string url, byte[] bytes)
    {
        if (bytes.Length > MaximumResponseBytes)
        {
            throw new InvalidDataException("Preview exceeds its byte limit.");
        }
        using SKData data = SKData.CreateCopy(bytes);
        using SKCodec codec = SKCodec.Create(data) ?? throw new InvalidDataException("Preview is not a supported image.");
        SKImageInfo source = codec.Info;
        if (source.Width <= 0 || source.Height <= 0 || source.Width > MaximumSourceDimension || source.Height > MaximumSourceDimension)
        {
            throw new InvalidDataException("Preview exceeds its dimension limit.");
        }
        using SKBitmap bitmap = new(new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
        {
            throw new InvalidDataException("Preview image is incomplete.");
        }
        float scale = Math.Min(1, MaximumDisplayDimension / (float)Math.Max(source.Width, source.Height));
        int width = Math.Max(1, (int)(source.Width * scale));
        int height = Math.Max(1, (int)(source.Height * scale));
        using SKBitmap? resized = scale < 1 ? bitmap.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear)) : null;
        SKBitmap output = resized ?? bitmap;
        byte[] pixels = new byte[checked(width * height * 4)];
        for (int row = 0; row < height; row++)
        {
            Marshal.Copy(output.GetPixels() + row * output.RowBytes, pixels, row * width * 4, width * 4);
        }
        return new(url, width, height, pixels);
    }

    private void TrimCache(string current)
    {
        FileInfo[] files = new DirectoryInfo(directory).EnumerateFiles("*.preview").Where(file => file.Name.Length == 72 && file.Name[..64].All(Uri.IsHexDigit)).OrderBy(file => file.LastWriteTimeUtc).ToArray();
        long bytes = files.Sum(file => file.Length);
        foreach (FileInfo file in files)
        {
            if (bytes <= MaximumCacheBytes)
            {
                break;
            }
            if (file.FullName != current)
            {
                file.Delete();
                bytes -= file.Length;
            }
        }
    }

    public void Dispose()
    {
        lifetime.Cancel();
        requests.Writer.TryComplete();
        _ = worker.ContinueWith(task =>
        {
            _ = task.Exception;
            if (ownsClient)
            {
                client.Dispose();
            }
            lifetime.Dispose();
        }, TaskScheduler.Default);
    }
}
