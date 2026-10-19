using System.Globalization;
using System.Security.Cryptography;

namespace Memoria.Launcher;

internal static class UpdatePackageCache
{
    internal const string CacheDirectory = ".memoria-download";

    internal static async Task<string> GetArchiveAsync(string root, ReleaseClient client, UpdateRelease release, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(release);
        if (release.AssetId <= 0 || release.Size <= 0 || release.Sha256.Length != 64 || !release.Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("The update archive metadata is invalid. Check for updates again.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        string cache = Path.Combine(Path.GetFullPath(root), CacheDirectory);
        UpdateInstaller.RejectLinks(cache);
        Directory.CreateDirectory(cache);
        string filename = string.Create(CultureInfo.InvariantCulture, $"package-{release.AssetId}-{release.Size}-{release.Sha256.ToLowerInvariant()}.zip");
        string archive = Path.Combine(cache, filename);
        UpdateInstaller.RejectLinks(archive);
        if (await IsVerifiedAsync(archive, release, cancellationToken).ConfigureAwait(false))
        {
            progress?.Report(1);
            return archive;
        }

        string pending = Path.Combine(cache, "download-" + Guid.NewGuid().ToString("N") + ".tmp");
        UpdateInstaller.RejectLinks(pending);
        try
        {
            await client.DownloadAsync(release, pending, progress, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            UpdateInstaller.RejectLinks(archive);
            File.Move(pending, archive, true);
        }
        finally
        {
            UpdateInstaller.RejectLinks(pending);
            File.Delete(pending);
        }
        foreach (string path in Directory.EnumerateFiles(cache))
        {
            string name = Path.GetFileName(path);
            if (!path.Equals(archive, StringComparison.OrdinalIgnoreCase) && ((name.StartsWith("package-", StringComparison.Ordinal) && name.EndsWith(".zip", StringComparison.Ordinal)) || (name.StartsWith("download-", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.Ordinal))))
            {
                UpdateInstaller.RejectLinks(path);
                File.Delete(path);
            }
        }
        return archive;
    }

    private static async Task<bool> IsVerifiedAsync(string archive, UpdateRelease release, CancellationToken cancellationToken)
    {
        if (!File.Exists(archive))
        {
            return false;
        }
        await using FileStream stream = new(archive, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length != release.Size)
        {
            return false;
        }
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(release.Sha256));
    }
}
