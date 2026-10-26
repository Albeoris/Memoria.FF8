using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Memoria.FF8.Platform;

public sealed record NativeDependency(string Name, string RuntimeIdentifier, string RelativePath, string? Sha256 = null)
{
    public string Resolve(string packageDirectory)
    {
        if (Path.IsPathRooted(RelativePath) || RelativePath.Split('/', '\\').Any(part => part == ".."))
        {
            throw new InvalidDataException("A native dependency must be contained in its package.");
        }
        string root = Path.GetFullPath(packageDirectory);
        string path = Path.GetFullPath(Path.Combine(root, RelativePath));
        string relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException("A native dependency escaped its package.");
        }
        using FileStream stream = File.OpenRead(path);
        NativeImage.RequireX64(stream, RuntimeIdentifier);
        if (Sha256 is not null)
        {
            stream.Position = 0;
            if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"The native dependency {Name} does not match its descriptor.");
            }
        }
        return path;
    }
}

/// <summary>Checks native executable format and ABI without loading or executing the image.</summary>
public static class NativeImage
{
    public static void RequireX64(Stream stream, string runtimeIdentifier)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Native image validation requires a seekable stream.", nameof(stream));
        }
        stream.Position = 0;
        Span<byte> header = stackalloc byte[32];
        stream.ReadExactly(header);
        bool valid = runtimeIdentifier switch
        {
            "win-x64" => IsWindowsX64(stream, header),
            "linux-x64" => header[..4].SequenceEqual("\u007fELF"u8) && header[4] == 2 && header[5] == 1 && BinaryPrimitives.ReadUInt16LittleEndian(header[18..]) == 62,
            "osx-x64" => IsMacX64(stream, header),
            _ => throw new ArgumentOutOfRangeException(nameof(runtimeIdentifier), "Unsupported desktop runtime identifier.")
        };
        if (!valid)
        {
            throw new BadImageFormatException($"The native image does not provide the required {runtimeIdentifier} ABI.");
        }
    }

    private static bool IsWindowsX64(Stream stream, ReadOnlySpan<byte> header)
    {
        if (header[0] != 'M' || header[1] != 'Z')
        {
            return false;
        }
        stream.Position = 0;
        using PEReader image = new(stream, PEStreamOptions.LeaveOpen);
        // Portable IL assemblies are not native dependencies and must not be classified as x86 libraries.
        return image.PEHeaders.CorHeader is null && image.PEHeaders.CoffHeader.Machine == Machine.Amd64 && image.PEHeaders.PEHeader?.Magic == PEMagic.PE32Plus;
    }

    private static bool IsMacX64(Stream stream, ReadOnlySpan<byte> header)
    {
        const uint X64 = 0x01000007;
        uint magic = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (magic == 0xcffaedfe)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(header[4..]) == X64;
        }
        if (magic is not (0xcafebabe or 0xcafebabf))
        {
            return false;
        }
        uint count = BinaryPrimitives.ReadUInt32BigEndian(header[4..]);
        int entrySize = magic == 0xcafebabf ? 32 : 20;
        if (count > 64 || stream.Length < 8 + (long)count * entrySize)
        {
            return false;
        }
        byte[] entry = new byte[entrySize];
        for (int index = 0; index < count; index++)
        {
            stream.Position = 8 + (long)index * entrySize;
            stream.ReadExactly(entry);
            if (BinaryPrimitives.ReadUInt32BigEndian(entry) != X64)
            {
                continue;
            }
            ulong offset = entrySize == 32 ? BinaryPrimitives.ReadUInt64BigEndian(entry.AsSpan(8)) : BinaryPrimitives.ReadUInt32BigEndian(entry.AsSpan(8));
            ulong size = entrySize == 32 ? BinaryPrimitives.ReadUInt64BigEndian(entry.AsSpan(16)) : BinaryPrimitives.ReadUInt32BigEndian(entry.AsSpan(12));
            if (offset < (ulong)(8 + count * entrySize) || size < 32 || offset > (ulong)stream.Length || size > (ulong)stream.Length - offset)
            {
                return false;
            }
            stream.Position = (long)offset;
            Span<byte> slice = stackalloc byte[8];
            stream.ReadExactly(slice);
            return BinaryPrimitives.ReadUInt32BigEndian(slice) == 0xcffaedfe && BinaryPrimitives.ReadUInt32LittleEndian(slice[4..]) == X64;
        }
        return false;
    }
}
