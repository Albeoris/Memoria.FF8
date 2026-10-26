using System.Security.Cryptography;

namespace Memoria.FF8.Resources;

/// <summary>Owns immutable disk snapshots copied and hashed without retaining encoded resource bytes in memory.</summary>
public sealed class EncodedResourceStore : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8.Resources", Guid.NewGuid().ToString("N"));
    private readonly object gate = new();
    private readonly Dictionary<string, string> snapshots = new(StringComparer.Ordinal);
    private bool disposed;

    /// <summary>Copies one selected resource to an owned snapshot. The copy callback writes sequentially and must leave its destination open.</summary>
    public EncodedResource Store(ResourceKey key, string format, long revision, string provenance, Action<Stream> copy, ResourceKey? exclusiveScope = null)
    {
        ArgumentNullException.ThrowIfNull(copy);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".bin");
            string temporary = path + ".tmp";
            try
            {
                string fingerprint;
                using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (SHA256 hash = SHA256.Create())
                {
                    using (CryptoStream hashing = new(output, hash, CryptoStreamMode.Write, leaveOpen: true))
                    {
                        copy(hashing);
                        hashing.FlushFinalBlock();
                    }
                    fingerprint = Convert.ToHexString(hash.Hash!);
                }
                if (snapshots.TryGetValue(fingerprint, out string? existing))
                {
                    File.Delete(temporary);
                }
                else
                {
                    File.Move(temporary, path);
                    existing = path;
                    snapshots.Add(fingerprint, path);
                }
                string snapshot = existing;
                return new EncodedResource(new ResourceContent(key, format, revision, provenance, () => Open(snapshot)), fingerprint, exclusiveScope);
            }
            catch
            {
                File.Delete(temporary);
                File.Delete(path);
                throw;
            }
        }
    }

    private Stream Open(string path)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
