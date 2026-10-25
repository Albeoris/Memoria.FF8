using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Original executable effect descriptors and571DC0 frame tables, outside the module ID directory.</summary>
public static class NativeBattleEffectStaticResourcesReader
{
    public static ImmutableArray<byte> Read(byte[] executable, uint address, int length)
    {
        return Read(new NativeExecutableMemory(executable), address, length);
    }

    public static ImmutableArray<byte> Read(IGameNativeMemorySource memory, uint address, int length)
    {
        return NativeBattleTelemetry.Immutable(1045, memory.Read(address, length));
    }

    public static NativeBattleModuleResource ReadSprite(byte[] executable, uint address)
    {
        return ReadSprite(new NativeExecutableMemory(executable), address);
    }

    public static NativeBattleModuleResource ReadSprite(IGameNativeMemorySource memory, uint address)
    {
        ReadOnlySpan<byte> header = memory.Read(address, 12);
        if (header[3] != 0)
        {
            throw new InvalidDataException("Native effect sprite requires the original kind0 frame layout.");
        }
        ImmutableArray<byte> bytes = ReadSpriteBytes(memory, address);
        uint wrapper = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xC7EFB4, 4));
        uint callback = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xC7EFA0, 4));
        NativeBattleModuleResource resource = new(0, address, wrapper, callback, bytes);
        resource.Validate();
        return resource;
    }

    public static ImmutableArray<byte> ReadProjectedSprite(byte[] executable, uint address)
    {
        return ReadProjectedSprite(new NativeExecutableMemory(executable), address);
    }

    public static ImmutableArray<byte> ReadProjectedSprite(IGameNativeMemorySource memory, uint address)
    {
        return ReadSpriteBytes(memory, address);
    }

    private static ImmutableArray<byte> ReadSpriteBytes(IGameNativeMemorySource memory, uint address)
    {
        ReadOnlySpan<byte> header = memory.Read(address, 12);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(header[8..]);
        int length = checked(10 + 2 * (count + 1));
        ReadOnlySpan<byte> directory = memory.Read(address, length);
        for (int phase = 0; phase < count; phase++)
        {
            ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(directory[(10 + 2 * phase)..]);
            uint quads = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(address + offset), 4)) & 0x7FFFFFFF;
            length = Math.Max(length, checked(offset + 4 + 20 * (int)quads));
        }
        return NativeBattleTelemetry.Immutable(1046, memory.Read(address, length));
    }

    public static NativeBattleStaticMeshResource ReadMesh(byte[] executable, byte id)
    {
        return ReadMesh(new NativeExecutableMemory(executable), id);
    }

    public static NativeBattleStaticMeshResource ReadMesh(IGameNativeMemorySource memory, byte id)
    {
        uint address = U32(0xC81678u + 4u * id);
        return ReadMesh(memory, address, id);

        uint U32(uint location) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(location, 4));
    }

    public static NativeBattleStaticMeshResource ReadMesh(byte[] executable, uint address)
    {
        return ReadMesh(new NativeExecutableMemory(executable), address);
    }

    public static NativeBattleStaticMeshResource ReadMesh(IGameNativeMemorySource memory, uint address)
    {
        return ReadMesh(memory, address, 0);
    }

    private static NativeBattleStaticMeshResource ReadMesh(IGameNativeMemorySource memory, uint address, byte id)
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
        return new(id, address, NativeBattleTelemetry.Immutable(1047, memory.Read(address, cursor)));

        uint U32(uint location) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(location, 4));
    }
}
