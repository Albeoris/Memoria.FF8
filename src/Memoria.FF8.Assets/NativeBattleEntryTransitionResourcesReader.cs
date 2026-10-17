using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEntryTransitionResourcesReader
{
    public static NativeBattleEntryTransitionResources Read(byte[] executable)
    {
        if (!Convert.ToHexString(SHA256.HashData(executable)).Equals(NativeFieldRuntimeProfile.FF8EnExecutableHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Battle entry transition requires the authenticated FF8_EN executable.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        ImmutableArray<ushort>.Builder encounters = ImmutableArray.CreateBuilder<ushort>(43);
        for (uint address = 0xB80F9C; address < 0xB80FF2; address += 2)
        {
            encounters.Add(BinaryPrimitives.ReadUInt16LittleEndian(Bytes(address, 2)));
        }
        if (BinaryPrimitives.ReadUInt16LittleEndian(Bytes(0xB80FF2, 2)) != ushort.MaxValue)
        {
            throw new InvalidDataException("Native47CC70 transition selector lost its FFFF terminator.");
        }
        NativeBattleEntryTransitionConstants constants = new(F32(0xB69910), F32(0xB6995C), F32(0xB69960), F32(0xB69964), F64(0xB69968), F64(0xB69970), F32(0xB69978), F64(0xB69980), F64(0xB69988), F32(0xB69990), F32(0xB69994), F64(0xB695C0), F64(0xB695D0), F32(0xC76DAC), F32(0xC76DB0));
        return new(encounters.MoveToImmutable(), constants);

        ReadOnlySpan<byte> Bytes(uint address, int length)
        {
            long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            foreach (SectionHeader section in reader.PEHeaders.SectionHeaders)
            {
                if (rva >= section.VirtualAddress && rva + length <= section.VirtualAddress + section.SizeOfRawData)
                {
                    return executable.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), length);
                }
            }
            throw new InvalidDataException($"Native transition address{address:X8} is outside executable sections.");
        }

        float F32(uint address)
        {
            return BinaryPrimitives.ReadSingleLittleEndian(Bytes(address, 4));
        }

        double F64(uint address)
        {
            return BinaryPrimitives.ReadDoubleLittleEndian(Bytes(address, 8));
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEntryTransitionResources? nativeBattleEntryTransitionResources;

    public NativeBattleEntryTransitionResources ReadNativeBattleEntryTransitionResources()
    {
        return nativeBattleEntryTransitionResources ??= NativeBattleEntryTransitionResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
