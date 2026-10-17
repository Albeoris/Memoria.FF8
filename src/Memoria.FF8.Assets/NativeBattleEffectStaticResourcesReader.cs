using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Original executable effect descriptors and571DC0 frame tables, outside the module ID directory.</summary>
public static class NativeBattleEffectStaticResourcesReader
{
    public static ImmutableArray<byte> Read(byte[] executable, uint address, int length)
    {
        Authenticate(executable);
        using PEReader reader = new(new MemoryStream(executable));
        return Bytes(executable, reader, address, length).ToArray().ToImmutableArray();
    }

    public static NativeBattleModuleResource ReadSprite(byte[] executable, uint address)
    {
        Authenticate(executable);
        using PEReader reader = new(new MemoryStream(executable));
        ReadOnlySpan<byte> header = Bytes(executable, reader, address, 12);
        if (header[3] != 0)
        {
            throw new InvalidDataException("Native effect sprite requires the original kind0 frame layout.");
        }
        int count = BinaryPrimitives.ReadUInt16LittleEndian(header[8..]);
        int length = checked(10 + 2 * (count + 1));
        ReadOnlySpan<byte> directory = Bytes(executable, reader, address, length);
        for (int phase = 0; phase < count; phase++)
        {
            ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(directory[(10 + 2 * phase)..]);
            uint quads = BinaryPrimitives.ReadUInt32LittleEndian(Bytes(executable, reader, checked(address + offset), 4)) & 0x7FFFFFFF;
            length = Math.Max(length, checked(offset + 4 + 20 * (int)quads));
        }
        uint wrapper = BinaryPrimitives.ReadUInt32LittleEndian(Bytes(executable, reader, 0xC7EFB4, 4));
        uint callback = BinaryPrimitives.ReadUInt32LittleEndian(Bytes(executable, reader, 0xC7EFA0, 4));
        NativeBattleModuleResource resource = new(0, address, wrapper, callback, Bytes(executable, reader, address, length).ToArray().ToImmutableArray());
        resource.Validate();
        return resource;
    }

    public static NativeBattleStaticMeshResource ReadMesh(byte[] executable, byte id)
    {
        Authenticate(executable);
        using PEReader reader = new(new MemoryStream(executable));
        uint address = U32(0xC81678u + 4u * id);
        return ReadMesh(executable, reader, address, id);

        uint U32(uint location) => BinaryPrimitives.ReadUInt32LittleEndian(Bytes(executable, reader, location, 4));
    }

    public static NativeBattleStaticMeshResource ReadMesh(byte[] executable, uint address)
    {
        Authenticate(executable);
        using PEReader reader = new(new MemoryStream(executable));
        return ReadMesh(executable, reader, address, 0);
    }

    private static NativeBattleStaticMeshResource ReadMesh(byte[] executable, PEReader reader, uint address, byte id)
    {
        int cursor = checked((int)U32(address));
        int vertices = checked((int)U32(address + 4));
        if (vertices > 4096 || cursor != checked(8 + vertices * 8))
        {
            throw new InvalidDataException("Native571240 mesh has an invalid original vertex directory.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        foreach (int stride in strides)
        {
            uint count = U32(checked(address + (uint)cursor));
            if (count > 4096)
            {
                throw new InvalidDataException("Native571240 primitive count exceeds its bounded original mesh.");
            }
            cursor = checked(cursor + 4 + checked((int)count) * stride);
        }
        return new(id, address, Bytes(executable, reader, address, cursor).ToArray().ToImmutableArray());

        uint U32(uint location) => BinaryPrimitives.ReadUInt32LittleEndian(Bytes(executable, reader, location, 4));
    }

    private static void Authenticate(byte[] executable)
    {
        if (Convert.ToHexString(SHA256.HashData(executable)) != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Native effect static resources require the authenticated English executable.");
        }
    }

    private static ReadOnlySpan<byte> Bytes(byte[] executable, PEReader reader, uint address, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
        SectionHeader? section = reader.PEHeaders.SectionHeaders.Cast<SectionHeader?>().SingleOrDefault(section => section is { } value && rva >= value.VirtualAddress && rva + count <= value.VirtualAddress + value.SizeOfRawData);
        if (section is not { } selected)
        {
            throw new InvalidDataException("Native static effect resource leaves the executable's original raw-backed sections.");
        }
        return executable.AsSpan(checked((int)(rva - selected.VirtualAddress + selected.PointerToRawData)), count);
    }
}
