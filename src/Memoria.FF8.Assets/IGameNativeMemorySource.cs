using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Authenticated initial native data at guest addresses, independent of its edition's container.</summary>
public interface IGameNativeMemorySource
{
    string SourceHash { get; }
    byte[] Read(uint address, int count);
    byte[] ReadLoader(uint address, int count);
}

public sealed class NativeExecutableMemory : IGameNativeMemorySource
{
    private readonly byte[] executable;
    private readonly PEHeaders headers;

    public NativeExecutableMemory(byte[] executable)
    {
        this.executable = executable.ToArray();
        SourceHash = Convert.ToHexString(SHA256.HashData(this.executable));
        if (SourceHash != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Native memory requires an authenticated Classic image.");
        }
        using PEReader reader = new(new MemoryStream(this.executable, false));
        headers = reader.PEHeaders;
    }

    public string SourceHash { get; }
    public byte[] Read(uint address, int count) => ReadCore(address, count, false);
    public byte[] ReadLoader(uint address, int count) => ReadCore(address, count, true);

    private byte[] ReadCore(uint address, int count, bool loader)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        long rva = address - (long)headers.PEHeader!.ImageBase;
        SectionHeader section = headers.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + count <= (long)section.VirtualAddress + (loader ? section.VirtualSize : section.SizeOfRawData));
        long offset = rva - section.VirtualAddress;
        byte[] bytes = new byte[count];
        int mapped = checked((int)Math.Clamp(section.SizeOfRawData - offset, 0, count));
        if (mapped != 0)
        {
            executable.AsSpan(checked((int)(section.PointerToRawData + offset)), mapped).CopyTo(bytes);
        }
        return bytes;
    }
}

public sealed partial class ClassicGameDataSource : IGameNativeMemorySource
{
    private NativeExecutableMemory? nativeMemory;
    public string SourceHash => Memory.SourceHash;
    public byte[] Read(uint address, int count) => Memory.Read(address, count);
    public byte[] ReadLoader(uint address, int count) => Memory.ReadLoader(address, count);
    private NativeExecutableMemory Memory
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return nativeMemory ??= new NativeExecutableMemory(ReadAuthenticatedExecutable());
        }
    }
}

public sealed partial class GameAssets
{
    internal IGameNativeMemorySource ReadNativeMemory() => GameDataResources.ReadNativeMemory(resources);
}
