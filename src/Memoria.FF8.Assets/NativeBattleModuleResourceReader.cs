using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleModuleResourceReader
{
    public static ImmutableArray<NativeBattleModuleResource> Read(byte[] executable)
    {
        if (Convert.ToHexString(SHA256.HashData(executable)) != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Native module resources require the authenticated English executable.");
        }
        using PEReader reader = new(new MemoryStream(executable));
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
            NativeBattleModuleResource resource = new(id, address, U32(0xC7EFB4 + 4u * kind), U32(0xC7EFA0 + 4u * kind), Bytes(address, length).ToArray().ToImmutableArray());
            resource.Validate();
            resources.Add(resource);
        }
        return resources.MoveToImmutable();

        ReadOnlySpan<byte> Bytes(uint address, int count)
        {
            long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + count <= section.VirtualAddress + section.SizeOfRawData);
            return executable.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), count);
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
            nativeBattleModuleResources = NativeBattleModuleResourceReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));
        }
        return nativeBattleModuleResources;
    }
}
