using System.Collections.Concurrent;
using System.Security.Cryptography;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class ClassicGameDataSource : IGameRuntimeSource
{
    private static readonly ConcurrentDictionary<(string Path, long Length, long ModifiedTicks), string> ExecutableHashes = new();

    public string? NativeExecutableHash
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            FileInfo file = new(Path.Combine(gameDirectory, "FF8_EN.exe"));
            return file.Exists ? ExecutableHashes.GetOrAdd((file.FullName, file.Length, file.LastWriteTimeUtc.Ticks), identity =>
            {
                using FileStream stream = File.OpenRead(identity.Path);
                return Convert.ToHexString(SHA256.HashData(stream));
            }) : null;
        }
    }

    public NativeFieldRuntimeProfile? NativeRuntimeProfile => NativeExecutableHash == NativeFieldRuntimeProfile.FF8EnExecutableHash ? NativeFieldRuntimeProfile.FF8En : null;

    public NativeFieldRuntimeProfile RequireNativeRuntimeProfile()
    {
        return NativeRuntimeProfile ?? throw new NotSupportedException("Native runtime contracts require an authenticated Classic source image.");
    }

    private ResourceContent? FindRuntimeResource(ResourceKey key)
    {
        if (!key.Path.StartsWith("runtime/", StringComparison.Ordinal))
        {
            return null;
        }
        if (key.Path == "runtime/disc-wrapper")
        {
            return new ResourceContent(key, "bin", 1, $"{Descriptor.Id}:{key}", () =>
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return File.OpenRead(Path.Combine(gameDirectory, "AF3DN.P"));
            });
        }
        if (key.Path != "runtime/native-executable")
        {
            throw new NotImplementedException($"Classic runtime resource '{key}' is not implemented.");
        }
        return new ResourceContent(key, "exe", 1, $"{Descriptor.Id}:{key}", () => new MemoryStream(ReadAuthenticatedExecutable(), false));
    }

    private byte[] ReadAuthenticatedExecutable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        byte[] image = File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe"));
        if (Convert.ToHexString(SHA256.HashData(image)) != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Executable resources require an authenticated Classic source image.");
        }
        return image;
    }
}
