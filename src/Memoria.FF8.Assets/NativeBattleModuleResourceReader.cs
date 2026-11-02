using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleModuleResourceReader
{
    public static ImmutableArray<NativeBattleModuleResource> Read(byte[] executable)
    {
        return Read(new NativeExecutableMemory(executable));
    }

    public static ImmutableArray<NativeBattleModuleResource> Read(IGameNativeMemorySource memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        if (!NativeFieldRuntimeProfile.IsSupportedImage(memory.SourceHash))
        {
            throw new NotSupportedException("Native module resources require an authenticated English runtime image.");
        }
        ImmutableArray<NativeBattleModuleResource>.Builder resources = ImmutableArray.CreateBuilder<NativeBattleModuleResource>(42);
        //C7E554 has42 original mapped resource pointers; the following DWORD is data FF000000, not another pointer.
        for (byte id = 0; id < 42; id++)
        {
            uint address = U32(0xC7E554 + 4u * id);
            byte kind = Bytes(address, 4)[3];
            int length = 4;
            if (kind == 0)
            {
                ushort count = U16(address + 8);
                length = checked(10 + 2 * (count + 1));
                for (int phase = 0; phase < count; phase++)
                {
                    ushort offset = U16(address + 10u + 2u * (uint)phase);
                    uint quads = U32(address + offset) & 0x7FFFFFFF;
                    length = Math.Max(length, checked(offset + 4 + 20 * (int)quads));
                }
            }
            NativeBattleModuleResource resource = new(id, address, U32(0xC7EFB4 + 4u * kind), U32(0xC7EFA0 + 4u * kind), NativeBattleTelemetry.Immutable(1049, NativeBattleTelemetry.Materialize(1048, Bytes(address, length))));
            resource.Validate();
            resources.Add(resource);
        }
        return resources.MoveToImmutable();

        ReadOnlySpan<byte> Bytes(uint address, int count)
        {
            return memory.Read(address, count);
        }

        uint U32(uint address)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(Bytes(address, 4));
        }

        ushort U16(uint address)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(Bytes(address, 2));
        }
    }
}

public sealed partial class GameAssets
{
    private ImmutableArray<NativeBattleModuleResource> nativeBattleModuleResources;

    public ImmutableArray<NativeBattleModuleResource> ReadNativeBattleModuleResources()
    {
        if (nativeBattleModuleResources.IsDefault)
        {
            _ = GameDataResources.RequireNativeRuntimeProfile(resources);
            nativeBattleModuleResources = NativeBattleModuleResourceReader.Read(ReadNativeMemory());
        }
        return nativeBattleModuleResources;
    }
}
