using System.Diagnostics;
using System.Formats.Tar;
using System.Runtime.InteropServices;
using Memoria.FF8.Platform;
using SharpCompress.Compressors.Xz;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Memoria.FF8;

internal sealed record FfmpegDownloadOptions(Uri ReleaseUrl, string ArchiveName, string RuntimeIdentifier = "win-x64")
{
    public static FfmpegDownloadOptions FromEnvironment()
    {
        string rid = OperatingSystem.IsWindows() ? "win-x64" : OperatingSystem.IsLinux() ? "linux-x64" : OperatingSystem.IsMacOS() ? "osx-x64" : throw new PlatformNotSupportedException("FFmpeg setup requires Windows, Linux or macOS x64.");
        string archive = rid == "win-x64" ? "ffmpeg-master-latest-win64-lgpl.zip" : rid == "linux-x64" ? "ffmpeg-master-latest-linux64-lgpl.tar.xz" : "";
        return new(new Uri(Environment.GetEnvironmentVariable("MEMORIA_FF8_FFMPEG_RELEASE_URL") ?? "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/"), Environment.GetEnvironmentVariable("MEMORIA_FF8_FFMPEG_ARCHIVE") ?? archive, rid);
    }

}

internal sealed record MediaToolsDiscoveryOptions(string ManagedDirectory, string? ConfiguredDirectory, string? SearchPath);

internal sealed record MediaToolProbeResult(int ExitCode, string Output, string Error = "");

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
    private static readonly string[] ToolNames = ["ffmpeg", "ffprobe"];
    private static readonly string[] Executables = ["ffmpeg.exe", "ffprobe.exe"];
    private readonly TimeSpan timeout = responseTimeout ?? TimeSpan.FromSeconds(15);
    private readonly Func<string, Task<bool>> validator = validate ?? ValidateAsync;

    public static string PrepareForStartup(string directory, Func<string, bool> consent, Action<string> report, string? installDirectory = null)
    {
        if (ValidateAsync(directory, report).GetAwaiter().GetResult())
        {
            return directory;
        }
        directory = installDirectory ?? directory;
        if (IsUnmanagedPathDirectory(directory))
        {
            report("The selected system FFmpeg pair is unavailable or violates the LGPL policy. Automatic setup will not change a PATH directory. Select another LGPL pair or use the managed media-tools directory.");
            return directory;
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
        return directory;
    }

    private static string ManualInstructions(string directory, string? archiveName = null)
    {
        string tools = OperatingSystem.IsWindows() ? "ffmpeg.exe and ffprobe.exe" : "ffmpeg and ffprobe";
        string source = OperatingSystem.IsMacOS() ? "Obtain trusted Intel x64 FFmpeg tools built for macOS; automatic macOS download is not configured. See https://ffmpeg.org/download.html and retain the matching source/license information." : $"Download {archiveName ?? (OperatingSystem.IsWindows() ? "ffmpeg-master-latest-win64-lgpl.zip" : "ffmpeg-master-latest-linux64-lgpl.tar.xz")} (x64, LGPL, static) from {LatestReleaseUrl}.";
        return $"Continuing without FFmpeg video playback (and replacement audio decoding).\nManual installation: {source}\nPlace {tools} in:\n{directory}\nKeep copyright, license and other notices alongside the tools. Use native x64 builds with a reported build configuration that does not enable GPL or nonfree components. On Unix, grant executable permission to both tools. Movie-dependent scenes may require FFmpeg to continue.";
    }

    public static string ResolveExecutable(string directory, string toolName) => Memoria.FF8.Platform.MediaToolPaths.ResolveExecutable(directory, toolName);

    public static string ResolveDirectory(string? directory = null)
    {
        string? configured = directory ?? Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }
        return Path.Combine(Memoria.FF8.Platform.ApplicationPaths.Current.LegacyState, "media-tools");
    }

    public static Task<string> ResolveDirectoryAsync(string? directory = null, Action<string>? report = null, CancellationToken cancellationToken = default)
    {
        string? configured = directory ?? Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS");
        MediaToolsDiscoveryOptions sources = new(Path.Combine(ApplicationPaths.Current.LegacyState, "media-tools"), configured, Environment.GetEnvironmentVariable("PATH"));
        return ResolveDirectoryAsync(sources, candidate => ToolNames.All(tool => File.Exists(ResolveExecutable(candidate, tool))), (candidate, token) => ValidateAsync(candidate, report, token), cancellationToken);
    }

    internal static Task<string> ResolveDirectoryAsync(MediaToolsDiscoveryOptions sources, Func<string, bool> hasPair, Func<string, CancellationToken, Task<bool>> validate, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(sources.ConfiguredDirectory))
            {
                return Path.GetFullPath(sources.ConfiguredDirectory);
            }
            string managed = Path.GetFullPath(sources.ManagedDirectory);
            if (hasPair(managed))
            {
                if (await validate(managed, cancellationToken).ConfigureAwait(false))
                {
                    return managed;
                }
            }
            HashSet<string> seen = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            seen.Add(managed);
            foreach (string entry in (sources.SearchPath ?? "").Split(Path.PathSeparator))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string candidate = entry.Trim().Trim('"');
                if (!IsLocalSearchDirectory(candidate))
                {
                    continue;
                }
                candidate = Path.GetFullPath(candidate);
                if (seen.Add(candidate) && hasPair(candidate) && await validate(candidate, cancellationToken).ConfigureAwait(false))
                {
                    return candidate;
                }
            }
            return managed;
        }, cancellationToken);
    }

    private static bool IsLocalSearchDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || directory.StartsWith("\\", StringComparison.Ordinal) || directory.StartsWith("//", StringComparison.Ordinal) || !Path.IsPathFullyQualified(directory))
        {
            return false;
        }
        try
        {
            return !OperatingSystem.IsWindows() || new DriveInfo(Path.GetPathRoot(directory)!).DriveType != DriveType.Network;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsUnmanagedPathDirectory(string directory)
    {
        string managed = Path.GetFullPath(Path.Combine(ApplicationPaths.Current.LegacyState, "media-tools"));
        StringComparer comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        string selected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (comparer.Equals(selected, Path.TrimEndingDirectorySeparator(managed)))
        {
            return false;
        }
        foreach (string entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            string candidate = entry.Trim().Trim('"');
            if (Path.IsPathFullyQualified(candidate) && comparer.Equals(selected, Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate))))
            {
                return true;
            }
        }
        return false;
    }

    public async Task<bool> EnsureAsync(string directory, Func<string, bool> consent, Action<string> report, Action<MediaToolsProgress>? progress = null)
    {
        try
        {
            if (await validator(directory))
            {
                return true;
            }
            if (IsUnmanagedPathDirectory(directory))
            {
                report("Automatic FFmpeg setup cannot replace files in a system PATH directory. Select an LGPL pair or choose a separate managed/user media-tools directory.");
                return false;
            }
            Directory.CreateDirectory(directory);
            if (options.RuntimeIdentifier == "osx-x64")
            {
                report(ManualInstructions(directory));
                return false;
            }
            string prompt = $"FFmpeg is required for video playback and replacement audio files. Download the {options.RuntimeIdentifier} LGPL static build ({options.ArchiveName}) directly from BtbN/FFmpeg-Builds?\n\nSource: {options.ReleaseUrl}\nInstall to: {directory}\n\nFFmpeg is a separate third-party dependency licensed under LGPL. Its license notices will be kept with the tools. You can continue without installing it.";
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
        string suffix = options.RuntimeIdentifier switch { "win-x64" => "-win64-lgpl.zip", "linux-x64" => "-linux64-lgpl.tar.xz", _ => throw new PlatformNotSupportedException("Automatic FFmpeg download is unavailable for this platform. Install trusted x64 tools manually.") };
        if (Path.GetFileName(options.ArchiveName) != options.ArchiveName || !options.ArchiveName.EndsWith(suffix, StringComparison.Ordinal))
        {
            throw new ArgumentException("The archive must be an x64 LGPL static filename matching the selected platform.");
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
            await Task.Run(() =>
            {
                if (options.RuntimeIdentifier == "linux-x64")
                {
                    ExtractLinux(archivePath, extracted);
                }
                else
                {
                    Extract(archivePath, extracted);
                }
            });
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
                if (!OperatingSystem.IsWindows() && ToolNames.Contains(Path.GetFileName(target), StringComparer.Ordinal))
                {
                    File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
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

    private static void ExtractLinux(string archivePath, string directory)
    {
        using FileStream file = File.OpenRead(archivePath);
        using XZStream decompressed = new(file);
        ExtractLinuxTar(decompressed, directory);
    }

    internal static void ExtractLinuxTar(Stream stream, string directory)
    {
        Directory.CreateDirectory(directory);
        using TarReader archive = new(stream, leaveOpen: true);
        HashSet<string> seen = new(StringComparer.Ordinal);
        string? root = null;
        long total = 0;
        int count = 0;
        TarEntry? entry;
        while ((entry = archive.GetNextEntry()) is not null)
        {
            if (++count > 10000 || entry.Length > 1024L * 1024 * 1024 || (total += entry.Length) > 2L * 1024 * 1024 * 1024)
            {
                throw new InvalidDataException("FFmpeg archive exceeds extraction limits.");
            }
            string name = entry.Name.TrimEnd('/');
            string[] parts = name.Split('/');
            if (parts.Any(part => part is "" or "." or "..") || name.Contains('\\') || name.Contains(':') || Path.IsPathRooted(name))
            {
                throw new InvalidDataException("Unexpected FFmpeg archive path.");
            }
            root ??= parts[0];
            if (parts[0] != root || entry.EntryType is not (TarEntryType.Directory or TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            {
                throw new InvalidDataException("Unexpected FFmpeg archive entry or link.");
            }
            if (entry.EntryType == TarEntryType.Directory)
            {
                continue;
            }
            string relative = string.Join('/', parts.Skip(1));
            if (relative.StartsWith("bin/", StringComparison.Ordinal))
            {
                if (relative is not ("bin/ffmpeg" or "bin/ffprobe"))
                {
                    continue;
                }
                relative = relative[4..];
            }
            if (relative.Length == 0 || !seen.Add(relative))
            {
                throw new InvalidDataException("Duplicate FFmpeg archive entry.");
            }
            string target = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using FileStream output = File.Create(target);
            (entry.DataStream ?? throw new InvalidDataException("Missing FFmpeg archive data.")).CopyTo(output);
        }
        if (!ToolNames.All(seen.Contains) || !seen.Any(name => Path.GetFileName(name).StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(name).StartsWith("COPYING", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("FFmpeg archive is missing tools or license notices.");
        }
        if (!OperatingSystem.IsWindows())
        {
            foreach (string tool in ToolNames)
            {
                File.SetUnixFileMode(Path.Combine(directory, tool), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }
    }

    internal static bool IsHostExecutable(string path)
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            return false;
        }
        try
        {
            using FileStream stream = File.OpenRead(path);
            NativeImage.RequireX64(stream, HostPlatforms.Current.RuntimeIdentifier());
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return false;
        }
    }

    public static Task<bool> ValidateAsync(string directory) => ValidateAsync(directory, report: null);

    public static Task<bool> ValidateAsync(string directory, Action<string>? report, CancellationToken cancellationToken = default) => Task.Run(() => ValidateToolsAsync(directory, IsHostExecutable, ProbeAsync, report, cancellationToken), cancellationToken);

    internal static async Task<bool> ValidateToolsAsync(string directory, Func<string, bool> isNativeExecutable, Func<string, CancellationToken, Task<MediaToolProbeResult>> probe, Action<string>? report = null, CancellationToken cancellationToken = default)
    {
        foreach (string tool in ToolNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = ResolveExecutable(directory, tool);
            if (!isNativeExecutable(path))
            {
                report?.Invoke($"{tool} is missing or is not a native x64 executable for this platform: {path}. Select a complete matching FFmpeg/FFprobe LGPL pair.");
                return false;
            }
            try
            {
                MediaToolProbeResult result = await probe(path, cancellationToken).ConfigureAwait(false);
                string output = result.Output + "\n" + result.Error;
                string[] lines = output.Split('\n').Select(line => line.Trim()).ToArray();
                if (result.ExitCode != 0 || !lines.Any(line => line.StartsWith(tool + " version ", StringComparison.Ordinal)))
                {
                    report?.Invoke($"{tool} did not report a successful FFmpeg tool version. Install a matching native x64 LGPL pair.");
                    return false;
                }
                string[] configurations = lines.Where(line => line.StartsWith("configuration:", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (configurations.Length == 0)
                {
                    report?.Invoke($"{tool} did not report its build configuration. Its LGPL/nonfree policy cannot be verified; install a build that reports configuration with -version.");
                    return false;
                }
                if (Regex.IsMatch(output, "(?:^|[\\s'\"])--enable-(?:gpl|nonfree)(?:$|[\\s'\"=])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    report?.Invoke($"{tool} enables GPL or nonfree components. Memoria requires an LGPL build without --enable-gpl or --enable-nonfree; select another pair or use the checked managed installer.");
                    return false;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or OperationCanceledException or InvalidOperationException)
            {
                report?.Invoke($"Could not validate {tool}: {exception.Message}. Check executable permissions and install a native x64 LGPL pair.");
                return false;
            }
        }
        return true;
    }

    private static async Task<MediaToolProbeResult> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using Process process = new() { StartInfo = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        process.StartInfo.ArgumentList.Add("-version");
        process.Start();
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            return new MediaToolProbeResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
