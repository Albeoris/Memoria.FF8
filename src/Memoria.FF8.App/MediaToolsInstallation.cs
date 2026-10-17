using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF8;

internal sealed record FfmpegDownloadOptions(Uri ReleaseUrl, string ArchiveName)
{
    public static FfmpegDownloadOptions FromEnvironment() => new(new Uri(Environment.GetEnvironmentVariable("MEMORIA_FF8_FFMPEG_RELEASE_URL") ?? "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/"), Environment.GetEnvironmentVariable("MEMORIA_FF8_FFMPEG_ARCHIVE") ?? "ffmpeg-master-latest-win64-lgpl.zip");
}

internal enum MediaToolsStage
{
    FetchingChecksums,
    Downloading,
    Verifying,
    Extracting,
    Validating,
    Complete
}

internal sealed record MediaToolsProgress(MediaToolsStage Stage, long BytesReceived = 0, long? TotalBytes = null);

/// <summary>Optional startup installation; playback continues to use independent executables.</summary>
internal sealed class MediaToolsInstallation(HttpClient client, FfmpegDownloadOptions options, TimeSpan? responseTimeout = null, Func<string, Task<bool>>? validate = null)
{
    public const string LatestReleaseUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/latest";
    private static readonly string[] Executables = ["ffmpeg.exe", "ffprobe.exe"];
    private readonly TimeSpan timeout = responseTimeout ?? TimeSpan.FromSeconds(15);
    private readonly Func<string, Task<bool>> validator = validate ?? ValidateAsync;

    public static void PrepareForStartup(string directory, Func<string, bool> consent, Action<string> report)
    {
        if (ValidateAsync(directory).GetAwaiter().GetResult())
        {
            return;
        }
        try
        {
            Directory.CreateDirectory(directory);
            using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            var installation = new MediaToolsInstallation(http, FfmpegDownloadOptions.FromEnvironment());
            installation.EnsureAsync(directory, consent, report).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or IOException or UnauthorizedAccessException)
        {
            report($"FFmpeg setup failed: {exception.Message}");
            report(ManualInstructions(directory));
        }
    }

    private static string ManualInstructions(string directory, string archiveName = "ffmpeg-master-latest-win64-lgpl.zip") => $"Continuing without FFmpeg video playback (and replacement audio decoding).\nManual installation: {LatestReleaseUrl}\nDownload {archiveName} (Windows x64, LGPL, static). Extract bin/ffmpeg.exe and bin/ffprobe.exe into:\n{directory}\nKeep the archive's copyright, license and other notices alongside them. No ffplay.exe or FFmpeg DLLs are required. Movie-dependent scenes may require FFmpeg to continue.";

    public static string ResolveDirectory(string? directory = null)
    {
        string? configured = directory ?? Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }
        return Path.Combine(AppContext.BaseDirectory, "media-tools");
    }

    public async Task<bool> EnsureAsync(string directory, Func<string, bool> consent, Action<string> report, Action<MediaToolsProgress>? progress = null)
    {
        try
        {
            if (await validator(directory))
            {
                return true;
            }
            Directory.CreateDirectory(directory);
            string prompt = $"FFmpeg is required for video playback and replacement audio files. Download the Windows x64 LGPL static build ({options.ArchiveName}) directly from BtbN/FFmpeg-Builds?\n\nSource: {options.ReleaseUrl}\nInstall to: {directory}\n\nFFmpeg is a separate third-party dependency licensed under LGPL. Its license notices will be kept with the tools. You can continue without installing it.";
            if (consent(prompt))
            {
                report("Downloading and verifying FFmpeg. The download may take several minutes.");
                await InstallAsync(directory, progress);
                return true;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            report($"FFmpeg installation failed: {exception.Message}");
        }
        report(ManualInstructions(directory, options.ArchiveName));
        return false;
    }

    public async Task InstallAsync(string directory, Action<MediaToolsProgress>? progress = null)
    {
        if (Path.GetFileName(options.ArchiveName) != options.ArchiveName || !options.ArchiveName.EndsWith("-win64-lgpl.zip", StringComparison.Ordinal))
        {
            throw new ArgumentException("The archive must be a Windows x64 LGPL static ZIP filename.");
        }
        Uri release = new(options.ReleaseUrl.AbsoluteUri.TrimEnd('/') + "/");
        if (release.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("FFmpeg downloads require HTTPS.");
        }
        Directory.CreateDirectory(directory);
        string staging = Path.Combine(directory, ".install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            progress?.Invoke(new(MediaToolsStage.FetchingChecksums));
            using var checksums = new MemoryStream();
            await DownloadAsync(new Uri(release, "checksums.sha256"), checksums, 1024 * 1024);
            string checksum = ReadChecksum(Encoding.UTF8.GetString(checksums.ToArray()), options.ArchiveName);
            string archivePath = Path.Combine(staging, "download.zip");
            await using (FileStream archive = File.Create(archivePath))
            {
                progress?.Invoke(new(MediaToolsStage.Downloading));
                await DownloadAsync(new Uri(release, options.ArchiveName), archive, 1024L * 1024 * 1024, progress);
                progress?.Invoke(new(MediaToolsStage.Verifying));
                archive.Position = 0;
                string actual = Convert.ToHexString(await SHA256.HashDataAsync(archive));
                if (!actual.Equals(checksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("FFmpeg archive SHA-256 does not match the release checksums.");
                }
            }
            string extracted = Path.Combine(staging, "files");
            progress?.Invoke(new(MediaToolsStage.Extracting));
            await Task.Run(() => Extract(archivePath, extracted));
            progress?.Invoke(new(MediaToolsStage.Validating));
            if (!await validator(extracted))
            {
                throw new InvalidDataException("Downloaded FFmpeg tools failed validation.");
            }
            await File.WriteAllTextAsync(Path.Combine(extracted, "download-source.txt"), $"Archive: {new Uri(release, options.ArchiveName)}\nSHA256: {checksum}\nChecksums: {new Uri(release, "checksums.sha256")}\nRetrieved UTC: {DateTimeOffset.UtcNow:O}\n");
            foreach (string file in Directory.EnumerateFiles(extracted, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(directory, Path.GetRelativePath(extracted, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(file, target, true);
            }
            if (!await validator(directory))
            {
                throw new InvalidDataException("Installed FFmpeg tools failed validation.");
            }
        }
        finally
        {
            Directory.Delete(staging, true);
        }
        progress?.Invoke(new(MediaToolsStage.Complete));
    }

    private async Task DownloadAsync(Uri uri, Stream destination, long maximumBytes, Action<MediaToolsProgress>? progress = null)
    {
        using var responseDeadline = new CancellationTokenSource(timeout);
        using HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, responseDeadline.Token);
        response.EnsureSuccessStatusCode();
        long? contentLength = response.Content.Headers.ContentLength;
        progress?.Invoke(new(MediaToolsStage.Downloading, 0, contentLength));
        await using Stream source = await response.Content.ReadAsStreamAsync(responseDeadline.Token);
        byte[] buffer = new byte[81920];
        long total = 0;
        Stopwatch updates = Stopwatch.StartNew();
        while (true)
        {
            // Each network read gets its own deadline; the entire download has no time limit.
            using var readDeadline = new CancellationTokenSource(timeout);
            int read = await source.ReadAsync(buffer, readDeadline.Token);
            if (read == 0)
            {
                break;
            }
            total += read;
            if (total > maximumBytes)
            {
                throw new InvalidDataException("FFmpeg download exceeds the expected size limit.");
            }
            await destination.WriteAsync(buffer.AsMemory(0, read));
            if (updates.ElapsedMilliseconds >= 100)
            {
                progress?.Invoke(new(MediaToolsStage.Downloading, total, contentLength));
                updates.Restart();
            }
        }
        progress?.Invoke(new(MediaToolsStage.Downloading, total, contentLength));
    }

    private static string ReadChecksum(string text, string archiveName)
    {
        string[] hashes = text.Split('\n').Select(line => line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Where(parts => parts.Length == 2 && parts[1].TrimStart('*') == archiveName).Select(parts => parts[0]).ToArray();
        if (hashes.Length != 1 || hashes[0].Length != 64 || !hashes[0].All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("Release checksums must contain exactly one SHA-256 for the selected FFmpeg archive.");
        }
        return hashes[0];
    }

    private static void Extract(string archivePath, string directory)
    {
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        foreach (string executable in Executables)
        {
            ZipArchiveEntry[] matches = archive.Entries.Where(entry => entry.FullName.EndsWith("/bin/" + executable, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidDataException($"FFmpeg archive must contain exactly one bin/{executable}.");
            }
            Directory.CreateDirectory(directory);
            matches[0].ExtractToFile(Path.Combine(directory, executable));
        }
        string root = archive.Entries.Single(entry => entry.FullName.EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase)).FullName[..^"bin/ffmpeg.exe".Length];
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith(root, StringComparison.Ordinal) || entry.FullName.EndsWith('/') || entry.FullName.StartsWith(root + "bin/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            string relative = entry.FullName[root.Length..];
            if (relative.Split('/', '\\').Any(part => part is ".." or "." or "") || relative.Contains(':') || Path.IsPathRooted(relative) || relative.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || relative.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Unexpected FFmpeg archive entry.");
            }
            string target = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target);
        }
        if (!Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any(file => Path.GetFileName(file).StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(file).StartsWith("COPYING", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("FFmpeg archive is missing license notices.");
        }
    }

    public static async Task<bool> ValidateAsync(string directory)
    {
        foreach (string executable in Executables)
        {
            string path = Path.Combine(directory, executable);
            if (!File.Exists(path))
            {
                return false;
            }
            try
            {
                using var process = new Process { StartInfo = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
                process.StartInfo.ArgumentList.Add("-version");
                process.Start();
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    Task<string> output = process.StandardOutput.ReadToEndAsync(deadline.Token);
                    Task<string> errors = process.StandardError.ReadToEndAsync(deadline.Token);
                    await process.WaitForExitAsync(deadline.Token);
                    string version = await output;
                    await errors;
                    if (process.ExitCode != 0 || !version.StartsWith(Path.GetFileNameWithoutExtension(executable) + " version ", StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
                finally
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                        await process.WaitForExitAsync();
                    }
                }
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or OperationCanceledException or InvalidOperationException)
            {
                return false;
            }
        }
        return true;
    }
}
