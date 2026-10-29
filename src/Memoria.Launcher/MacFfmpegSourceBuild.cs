using System.Diagnostics;
using System.Formats.Tar;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Memoria.FF8;
using SharpCompress.Compressors.Xz;

namespace Memoria.Launcher;

/// <summary>Builds an LGPL FFmpeg pair from the upstream source release.</summary>
/// <remarks>Fallback for when the pinned prebuilt package (<see cref="MacFfmpegPrebuiltInstall"/>) cannot be downloaded or validated.</remarks>
internal static class MacFfmpegSourceBuild
{
    // Pinned after verifying ffmpeg-9.0.2.tar.xz.asc against the FFmpeg release signing key FCF986EA15E6E293A5644F10B4322F04D67658D8.
    internal const string Version = "9.0.2";
    internal const string ArchiveName = "ffmpeg-" + Version + ".tar.xz";
    internal const string ArchiveSha256 = "8c3850283eb25fa026482078a04051e0be17347b09ef81a0849bec15a96e002e";
    internal const string LogName = "ffmpeg-build.log";
    private const string SourceRoot = "ffmpeg-" + Version;
    internal const long MaximumArchiveSize = 64L * 1024 * 1024;
    private const long MaximumSourceSize = 512L * 1024 * 1024;
    // A server or proxy that stops sending headers or data fails the download instead of stalling setup indefinitely.
    internal static readonly TimeSpan DownloadInactivityTimeout = TimeSpan.FromSeconds(60);
    // Playback only writes raw video and PCM to pipes (FfmpegMovieDecoder, PcmAudioDecoder) and copies video packets to IVF to count frames.
    internal static readonly string[] RequiredEncoders = ["rawvideo", "pcm_s16le"];
    internal static readonly string[] RequiredMuxers = ["rawvideo", "s16le", "ivf"];
    private static readonly string[] RequiredNotices = ["LICENSE.md", "COPYING.LGPLv2.1"];
    private static readonly string[] OptionalNotices = ["COPYING.LGPLv3", "CREDITS"];
    // Isolate the build from Homebrew/MacPorts tools and libraries so the result links only system frameworks.
    private const string BuildPath = "/usr/bin:/bin:/usr/sbin:/sbin";
    private static readonly string[] ClearedVariables = ["PKG_CONFIG_PATH", "PKG_CONFIG_LIBDIR", "CPATH", "C_INCLUDE_PATH", "LIBRARY_PATH", "CFLAGS", "CPPFLAGS", "LDFLAGS", "DYLD_LIBRARY_PATH", "DYLD_FALLBACK_LIBRARY_PATH"];

    internal static Uri ReleaseUrl => new((Environment.GetEnvironmentVariable("MEMORIA_FF8_FFMPEG_SOURCE_URL") ?? "https://ffmpeg.org/releases/").TrimEnd('/') + "/");

    internal static string Prompt(string directory, string? reason = null) => $"{(reason is null ? "" : "The prebuilt FFmpeg package could not be installed: " + reason + "\n\n")}Build FFmpeg {Version} from the official source release {(reason is null ? "now" : "instead")}?\n\nSource: {new Uri(ReleaseUrl, ArchiveName)} (SHA-256 verified)\nInstall to: {directory}\n\nThe build needs the Xcode Command Line Tools (macOS offers to install them when missing) and takes several minutes. It enables only LGPL components, without --enable-gpl or --enable-nonfree. License notices are kept with the tools. Cancel to stay in the launcher.";

    /// <summary>Configuration uses only system libraries; FFmpeg's default license is LGPL, and GPL/nonfree parts are never enabled.</summary>
    /// <remarks>
    /// iconv stays disabled: macOS ships it in a separate libiconv that FFmpeg's program link omits, and playback needs no subtitle charset conversion.
    /// Decoders, demuxers, parsers and filters keep FFmpeg's defaults so original and replacement media remain readable. Output-only encoders and muxers
    /// are limited to what playback writes, and capture devices and network protocols are left out because only local files are read.
    /// </remarks>
    internal static string[] ConfigureArguments(string prefix, Architecture architecture)
    {
        List<string> arguments = ["--prefix=" + prefix, "--disable-autodetect", "--enable-videotoolbox", "--enable-audiotoolbox", "--enable-zlib", "--enable-bzlib", "--disable-doc", "--disable-debug", "--disable-ffplay", "--disable-encoders", "--enable-encoder=" + string.Join(',', RequiredEncoders), "--disable-muxers", "--enable-muxer=" + string.Join(',', RequiredMuxers), "--disable-devices", "--disable-network"];
        if (architecture == Architecture.X64)
        {
            // Apple's toolchain has no NASM; the x64 build stays usable through the C implementations.
            arguments.Add("--disable-x86asm");
        }
        return arguments.ToArray();
    }

    public static async Task InstallAsync(HttpClient client, string directory, Action<MediaToolsProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("Building FFmpeg from source is available only on macOS.");
        }
        if (MediaToolsInstallation.IsUnmanagedPathDirectory(directory))
        {
            throw new InvalidOperationException("FFmpeg cannot be built into a system PATH directory. Choose the managed or a separate media-tools directory.");
        }
        await RequireToolchainAsync(cancellationToken);
        Directory.CreateDirectory(directory);
        string staging = Path.Combine(directory, ".build-" + Guid.NewGuid().ToString("N"));
        string logPath = Path.Combine(directory, LogName);
        Directory.CreateDirectory(staging);
        try
        {
            await using StreamWriter log = new(logPath, append: false) { AutoFlush = true };
            string archivePath = Path.Combine(staging, ArchiveName);
            Uri source = new(ReleaseUrl, ArchiveName);
            progress?.Invoke(new(MediaToolsStage.Downloading));
            await using (FileStream archive = File.Create(archivePath))
            {
                await DownloadAsync(client, source, archive, DownloadInactivityTimeout, progress, cancellationToken);
                progress?.Invoke(new(MediaToolsStage.Verifying));
                archive.Position = 0;
                string actual = Convert.ToHexString(await SHA256.HashDataAsync(archive, cancellationToken));
                if (!actual.Equals(ArchiveSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The FFmpeg source archive does not match its pinned SHA-256.");
                }
            }
            progress?.Invoke(new(MediaToolsStage.Extracting));
            string sources = Path.Combine(staging, "src");
            await Task.Run(() =>
            {
                using FileStream file = File.OpenRead(archivePath);
                using XZStream decompressed = new(file);
                ExtractSourceTar(decompressed, sources);
            }, cancellationToken);
            string tree = Path.Combine(sources, SourceRoot);
            string prefix = Path.Combine(staging, "install");
            progress?.Invoke(new(MediaToolsStage.Configuring));
            await RunAsync("/bin/sh", ["./configure", .. ConfigureArguments(prefix, RuntimeInformation.ProcessArchitecture)], tree, log, "FFmpeg configure", logPath, cancellationToken);
            progress?.Invoke(new(MediaToolsStage.Compiling));
            await RunAsync("/usr/bin/make", ["-j" + Math.Max(1, Environment.ProcessorCount)], tree, log, "FFmpeg build", logPath, cancellationToken);
            await RunAsync("/usr/bin/make", ["install"], tree, log, "FFmpeg install", logPath, cancellationToken);

            progress?.Invoke(new(MediaToolsStage.Validating));
            string built = Path.Combine(prefix, "bin", "ffmpeg");
            await RequireComponentsAsync(built, "-encoders", RequiredEncoders, cancellationToken);
            await RequireComponentsAsync(built, "-muxers", RequiredMuxers, cancellationToken);
            string result = Path.Combine(staging, "result");
            Directory.CreateDirectory(result);
            foreach (string tool in MediaToolsInstallation.ToolNames)
            {
                File.Copy(Path.Combine(prefix, "bin", tool), Path.Combine(result, tool));
            }
            foreach (string notice in RequiredNotices.Concat(OptionalNotices.Where(name => File.Exists(Path.Combine(tree, name)))))
            {
                File.Copy(Path.Combine(tree, notice), Path.Combine(result, notice));
            }
            await File.WriteAllTextAsync(Path.Combine(result, "download-source.txt"), $"Source: {source}\nSHA256: {ArchiveSha256}\nBuilt locally with: ./configure {string.Join(' ', ConfigureArguments("<prefix>", RuntimeInformation.ProcessArchitecture))}\nBuilt UTC: {DateTimeOffset.UtcNow:O}\n", cancellationToken);
            List<string> diagnostics = [];
            if (!await MediaToolsInstallation.ValidateAsync(result, diagnostics.Add, cancellationToken))
            {
                throw new InvalidDataException("The built FFmpeg tools failed validation: " + string.Join(" ", diagnostics));
            }
            foreach (string file in Directory.EnumerateFiles(result))
            {
                File.Move(file, Path.Combine(directory, Path.GetFileName(file)), true);
            }
            if (!await MediaToolsInstallation.ValidateAsync(directory, diagnostics.Add, cancellationToken))
            {
                throw new InvalidDataException("The installed FFmpeg tools failed validation: " + string.Join(" ", diagnostics));
            }
        }
        finally
        {
            Directory.Delete(staging, true);
        }
        progress?.Invoke(new(MediaToolsStage.Complete));
    }

    private static async Task RequireToolchainAsync(CancellationToken cancellationToken)
    {
        if (await ExitCodeAsync("/usr/bin/xcode-select", ["-p"], cancellationToken) == 0 && await ExitCodeAsync("/usr/bin/xcrun", ["--find", "clang"], cancellationToken) == 0)
        {
            return;
        }
        try
        {
            // Opens Apple's installer dialog; the user confirms it there.
            using Process? installer = Process.Start(new ProcessStartInfo("/usr/bin/xcode-select", "--install") { UseShellExecute = false });
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        throw new InvalidOperationException("Building FFmpeg requires the Xcode Command Line Tools. Finish the macOS installer (or run xcode-select --install), then start FFmpeg setup again.");
    }

    private static async Task<int> ExitCodeAsync(string file, string[] arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(file) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        try
        {
            using Process process = Process.Start(start)!;
            Task drain = Task.WhenAll(process.StandardOutput.ReadToEndAsync(cancellationToken), process.StandardError.ReadToEndAsync(cancellationToken));
            await process.WaitForExitAsync(cancellationToken);
            await drain;
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return -1;
        }
    }

    /// <summary>configure only warns about unmatched component names, so the built tool must list every component playback writes with.</summary>
    internal static async Task RequireComponentsAsync(string ffmpeg, string listOption, string[] required, CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-hide_banner");
        start.ArgumentList.Add(listOption);
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await error;
        string[] missing = required.Except(ListedComponents(await output)).ToArray();
        if (process.ExitCode != 0 || missing.Length > 0)
        {
            throw new InvalidDataException($"The built FFmpeg lacks required {listOption.TrimStart('-')}: {string.Join(", ", missing)}.");
        }
    }

    /// <summary>Reads component names from ffmpeg -encoders/-muxers rows, which start with a flag column followed by the name (muxers may list several, comma-separated).</summary>
    internal static HashSet<string> ListedComponents(string output)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (string line in output.Split('\n'))
        {
            string[] columns = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length >= 2)
            {
                foreach (string name in columns[1].Split(','))
                {
                    names.Add(name);
                }
            }
        }
        return names;
    }

    private static async Task RunAsync(string file, string[] arguments, string workingDirectory, StreamWriter log, string step, string logPath, CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(file) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment["PATH"] = BuildPath;
        foreach (string variable in ClearedVariables)
        {
            start.Environment.Remove(variable);
        }
        lock (log)
        {
            log.WriteLine($"$ {file} {string.Join(' ', arguments)}");
        }
        using Process process = new() { StartInfo = start };
        DataReceivedEventHandler write = (_, line) =>
        {
            if (line.Data is not null)
            {
                lock (log)
                {
                    log.WriteLine(line.Data);
                }
            }
        };
        process.OutputDataReceived += write;
        process.ErrorDataReceived += write;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{step} failed with exit code {process.ExitCode}. See {logPath}.");
        }
    }

    internal static async Task DownloadAsync(HttpClient client, Uri uri, Stream destination, TimeSpan inactivityTimeout, Action<MediaToolsProgress>? progress, CancellationToken cancellationToken)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("FFmpeg downloads require HTTPS.");
        }
        // Every wait on the server restarts the inactivity deadline; only the caller's token means cancellation.
        using CancellationTokenSource inactivity = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            inactivity.CancelAfter(inactivityTimeout);
            using HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, inactivity.Token);
            response.EnsureSuccessStatusCode();
            long? length = response.Content.Headers.ContentLength;
            await using Stream source = await response.Content.ReadAsStreamAsync(inactivity.Token);
            byte[] buffer = new byte[81920];
            long total = 0;
            Stopwatch updates = Stopwatch.StartNew();
            int read;
            inactivity.CancelAfter(inactivityTimeout);
            while ((read = await source.ReadAsync(buffer, inactivity.Token)) > 0)
            {
                total += read;
                if (total > MaximumArchiveSize)
                {
                    throw new InvalidDataException("The FFmpeg download exceeds the expected size limit.");
                }
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                if (updates.ElapsedMilliseconds >= 100)
                {
                    progress?.Invoke(new(MediaToolsStage.Downloading, total, length));
                    updates.Restart();
                }
                inactivity.CancelAfter(inactivityTimeout);
            }
            progress?.Invoke(new(MediaToolsStage.Downloading, total, length));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The FFmpeg download received no data for {inactivityTimeout.TotalSeconds:F0} seconds. Check the connection and start FFmpeg setup again.");
        }
    }

    /// <summary>Extracts only regular files and directories below the single release root, keeping their Unix modes.</summary>
    internal static void ExtractSourceTar(Stream stream, string directory)
    {
        Directory.CreateDirectory(directory);
        string root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        using TarReader archive = new(stream, leaveOpen: true);
        HashSet<string> seen = new(StringComparer.Ordinal);
        long total = 0;
        int count = 0;
        TarEntry? entry;
        while ((entry = archive.GetNextEntry()) is not null)
        {
            if (++count > 20000 || (total += entry.Length) > MaximumSourceSize)
            {
                throw new InvalidDataException("The FFmpeg source archive exceeds extraction limits.");
            }
            string name = entry.Name.TrimEnd('/');
            string[] parts = name.Split('/');
            if (parts[0] != SourceRoot || parts.Any(part => part is "" or "." or "..") || name.Contains('\\') || name.Contains(':') || Path.IsPathRooted(name))
            {
                throw new InvalidDataException("Unexpected FFmpeg source archive path: " + entry.Name);
            }
            if (entry.EntryType is not (TarEntryType.Directory or TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            {
                throw new InvalidDataException("The FFmpeg source archive contains a link or special entry: " + entry.Name);
            }
            string target = Path.GetFullPath(Path.Combine(directory, name.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, StringComparison.Ordinal) || !seen.Add(name))
            {
                throw new InvalidDataException("Duplicate or escaping FFmpeg source archive entry: " + entry.Name);
            }
            if (entry.EntryType == TarEntryType.Directory)
            {
                Directory.CreateDirectory(target);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: false);
        }
        if (!RequiredNotices.Append("configure").All(file => seen.Contains(SourceRoot + "/" + file)))
        {
            throw new InvalidDataException("The FFmpeg source archive is missing its configure script or license notices.");
        }
    }
}
