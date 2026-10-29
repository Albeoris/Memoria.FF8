using System.Formats.Tar;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Memoria.FF8;
using SharpCompress.Compressors.Xz;

namespace Memoria.Launcher;

/// <summary>Installs a pinned prebuilt LGPL FFmpeg package for macOS, keeping its bin/lib layout and license notices together.</summary>
/// <remarks>
/// dfloresc/ffmpeg-macos-builds v9.0.1-1 is built by GitHub Actions from checksum-pinned upstream sources without --enable-gpl or --enable-nonfree;
/// the release carries the corresponding sources and each archive's BUILDINFO.txt records the exact configure line.
/// The tools are linked against shared libraries found through @executable_path/../lib, so bin/ and lib/ are installed side by side.
/// </remarks>
internal static class MacFfmpegPrebuiltInstall
{
    internal const string Version = "9.0.1";
    internal const string ReleaseTag = "v9.0.1-1";
    internal const string ReleasePage = "https://github.com/dfloresc/ffmpeg-macos-builds/releases/tag/" + ReleaseTag;
    // Records which entries Memoria installed, so a later installation never replaces bin/ or lib/ directories it did not create.
    internal const string MarkerName = "memoria-ffmpeg-package.txt";
    internal static readonly string[] InstalledEntries = ["bin", "lib", "LICENSES", "BUILDINFO.txt"];
    private const long MaximumExtractedSize = 256L * 1024 * 1024;

    internal static Uri ReleaseUrl => new((Environment.GetEnvironmentVariable("MEMORIA_FF8_FFMPEG_PREBUILT_URL") ?? "https://github.com/dfloresc/ffmpeg-macos-builds/releases/download/" + ReleaseTag + "/").TrimEnd('/') + "/");

    internal static (string Root, string Sha256) Package(Architecture architecture) => architecture switch
    {
        Architecture.Arm64 => ("ffmpeg-9.0.1-1-lgpl-macos-arm64", "d0ea5ea43a5adfa5d01d4137c9e5a61728b800a812f12917a26fd7af0c14c03a"),
        Architecture.X64 => ("ffmpeg-9.0.1-1-lgpl-macos-x86_64", "d63ad731cbf1eb71c2055130fedfbd7e49edd60a9815ab763e72ba802c1ac666"),
        _ => throw new PlatformNotSupportedException("No prebuilt FFmpeg package is pinned for this macOS architecture.")
    };

    internal static string Prompt(string directory, Architecture architecture) => $"Download and verify a prebuilt LGPL FFmpeg {Version} for macOS?\n\nSource: {new Uri(ReleaseUrl, Package(architecture).Root + ".tar.xz")} (SHA-256 pinned)\nCorresponding sources: {ReleasePage}\nInstall to: {directory}\n\nFFmpeg is a separate third-party LGPL dependency. License notices and build information are kept with the tools. If the download or validation fails, you can build FFmpeg from source instead. Cancel to stay in the launcher.";

    public static async Task InstallAsync(HttpClient client, string directory, Action<MediaToolsProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("The prebuilt FFmpeg package is available only on macOS.");
        }
        if (MediaToolsInstallation.IsUnmanagedPathDirectory(directory))
        {
            throw new InvalidOperationException("FFmpeg cannot be installed into a system PATH directory. Choose the managed or a separate media-tools directory.");
        }
        RequireReplaceable(directory);
        (string root, string sha256) = Package(RuntimeInformation.ProcessArchitecture);
        Directory.CreateDirectory(directory);
        string staging = Path.Combine(directory, ".download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            string archivePath = Path.Combine(staging, root + ".tar.xz");
            Uri source = new(ReleaseUrl, root + ".tar.xz");
            progress?.Invoke(new(MediaToolsStage.Downloading));
            await using (FileStream archive = File.Create(archivePath))
            {
                await MacFfmpegSourceBuild.DownloadAsync(client, source, archive, MediaToolsInstallation.DownloadInactivityNoticeDelay, progress, cancellationToken);
                progress?.Invoke(new(MediaToolsStage.Verifying));
                archive.Position = 0;
                string actual = Convert.ToHexString(await SHA256.HashDataAsync(archive, cancellationToken));
                if (!actual.Equals(sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The prebuilt FFmpeg package does not match its pinned SHA-256.");
                }
            }
            progress?.Invoke(new(MediaToolsStage.Extracting));
            string package = Path.Combine(staging, "package");
            await Task.Run(() =>
            {
                using FileStream file = File.OpenRead(archivePath);
                using XZStream decompressed = new(file);
                ExtractPackageTar(decompressed, package, root, cancellationToken);
            }, cancellationToken);

            progress?.Invoke(new(MediaToolsStage.Validating));
            await File.WriteAllTextAsync(Path.Combine(package, "download-source.txt"), $"Source: {source}\nSHA256: {sha256}\nCorresponding sources: {ReleasePage}\nInstalled UTC: {DateTimeOffset.UtcNow:O}\n", cancellationToken);
            await File.WriteAllLinesAsync(Path.Combine(package, MarkerName), InstalledEntries.Append("download-source.txt"), cancellationToken);
            List<string> diagnostics = [];
            if (!await MediaToolsInstallation.ValidateAsync(package, diagnostics.Add, cancellationToken))
            {
                throw new InvalidDataException("The prebuilt FFmpeg tools failed validation: " + string.Join(" ", diagnostics));
            }
            string ffmpeg = Path.Combine(package, "bin", "ffmpeg");
            await MacFfmpegSourceBuild.RequireComponentsAsync(ffmpeg, "-encoders", MacFfmpegSourceBuild.RequiredEncoders, cancellationToken);
            await MacFfmpegSourceBuild.RequireComponentsAsync(ffmpeg, "-muxers", MacFfmpegSourceBuild.RequiredMuxers, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            Replace(package, directory);
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

    /// <summary>Refuses to replace bin/, lib/ or LICENSES/ unless an earlier Memoria package installation created them.</summary>
    internal static void RequireReplaceable(string directory)
    {
        if (File.Exists(Path.Combine(directory, MarkerName)))
        {
            return;
        }
        string[] foreign = InstalledEntries.Where(entry => Directory.Exists(Path.Combine(directory, entry)) || File.Exists(Path.Combine(directory, entry))).ToArray();
        if (foreign.Length > 0)
        {
            throw new InvalidOperationException($"{directory} already contains {string.Join(", ", foreign)} not installed by Memoria. Choose an empty media-tools directory.");
        }
    }

    /// <summary>Moves a validated package into place; the stale top-level pair would otherwise take precedence over bin/.</summary>
    private static void Replace(string package, string directory)
    {
        RequireReplaceable(directory);
        foreach (string entry in InstalledEntries.Append("download-source.txt").Append(MarkerName).Concat(MediaToolsInstallation.ToolNames))
        {
            string target = Path.Combine(directory, entry);
            if (Directory.Exists(target))
            {
                Directory.Delete(target, true);
            }
            else if (File.Exists(target))
            {
                File.Delete(target);
            }
        }
        foreach (string entry in InstalledEntries.Append("download-source.txt").Append(MarkerName))
        {
            string source = Path.Combine(package, entry);
            string target = Path.Combine(directory, entry);
            if (Directory.Exists(source))
            {
                Directory.Move(source, target);
            }
            else
            {
                File.Move(source, target);
            }
        }
    }

    /// <summary>
    /// Extracts the tools, their libraries, license notices and build information below the expected release root. Headers are skipped.
    /// Library version links are recreated only when they name a sibling library extracted from the same archive.
    /// </summary>
    internal static void ExtractPackageTar(Stream stream, string directory, string root, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        string fullRoot = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        using TarReader archive = new(stream, leaveOpen: true);
        HashSet<string> seen = new(StringComparer.Ordinal);
        Dictionary<string, string> links = new(StringComparer.Ordinal);
        long total = 0;
        int count = 0;
        TarEntry? entry;
        while ((entry = archive.GetNextEntry()) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++count > 5000 || (total += entry.Length) > MaximumExtractedSize)
            {
                throw new InvalidDataException("The prebuilt FFmpeg package exceeds extraction limits.");
            }
            string name = entry.Name.TrimEnd('/');
            string[] parts = name.Split('/');
            if (parts[0] != root || parts.Any(part => part is "" or "." or "..") || name.Contains('\\') || name.Contains(':') || Path.IsPathRooted(name))
            {
                throw new InvalidDataException("Unexpected prebuilt FFmpeg package path: " + entry.Name);
            }
            if (parts.Length == 1 || parts[1] == "include")
            {
                continue;
            }
            string relative = string.Join('/', parts.Skip(1));
            bool regular = entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile;
            bool directoryEntry = entry.EntryType == TarEntryType.Directory;
            bool allowed = parts[1] switch
            {
                "bin" => directoryEntry ? parts.Length == 2 : regular && relative is "bin/ffmpeg" or "bin/ffprobe",
                "lib" => directoryEntry ? parts.Length == 2 : parts.Length == 3 && parts[2].EndsWith(".dylib", StringComparison.Ordinal) && (regular || entry.EntryType == TarEntryType.SymbolicLink),
                "LICENSES" => directoryEntry || regular,
                "BUILDINFO.txt" => regular,
                _ => false
            };
            if (!allowed)
            {
                if (parts[1] == "bin" && regular)
                {
                    continue;
                }
                throw new InvalidDataException("Unexpected prebuilt FFmpeg package entry: " + entry.Name);
            }
            string target = Path.GetFullPath(Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(fullRoot, StringComparison.Ordinal) || !seen.Add(relative))
            {
                throw new InvalidDataException("Duplicate or escaping prebuilt FFmpeg package entry: " + entry.Name);
            }
            if (directoryEntry)
            {
                Directory.CreateDirectory(target);
                continue;
            }
            if (entry.EntryType == TarEntryType.SymbolicLink)
            {
                links.Add(relative, entry.LinkName);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: false);
        }
        foreach ((string relative, string linkTarget) in links)
        {
            if (linkTarget.Contains('/') || linkTarget.Contains('\\') || !linkTarget.EndsWith(".dylib", StringComparison.Ordinal) || links.ContainsKey("lib/" + linkTarget) || !seen.Contains("lib/" + linkTarget))
            {
                throw new InvalidDataException($"The prebuilt FFmpeg library link {relative} does not name a library in the package.");
            }
            File.CreateSymbolicLink(Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar)), linkTarget);
        }
        string[] required = ["bin/ffmpeg", "bin/ffprobe", "BUILDINFO.txt", "LICENSES/ffmpeg/COPYING.LGPLv2.1", "LICENSES/ffmpeg/LICENSE.md"];
        if (!required.All(seen.Contains) || !seen.Any(path => path.StartsWith("lib/", StringComparison.Ordinal) && !links.ContainsKey(path)))
        {
            throw new InvalidDataException("The prebuilt FFmpeg package is missing its tools, libraries, build information or license notices.");
        }
        if (!OperatingSystem.IsWindows())
        {
            foreach (string tool in MediaToolsInstallation.ToolNames)
            {
                File.SetUnixFileMode(Path.Combine(directory, "bin", tool), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }
    }
}
