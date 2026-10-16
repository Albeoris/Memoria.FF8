using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeDrawPointParticleTableReader
{
    public static NativeDrawPointParticleTables Read(byte[] executable)
    {
        string hash = Convert.ToHexString(SHA256.HashData(executable));
        if (hash != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Native draw-point particles require the authenticated English executable.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        byte[] Range(uint address, int length)
        {
            long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + length <= section.VirtualAddress + section.SizeOfRawData);
            return executable.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), length).ToArray();
        }
        return new NativeDrawPointParticleTables(hash, Range(0xB80A18, 256), Range(0xB80D1C, 512), Range(0xB80D9C, 512), Range(0xB809D8, 64));
    }
}
