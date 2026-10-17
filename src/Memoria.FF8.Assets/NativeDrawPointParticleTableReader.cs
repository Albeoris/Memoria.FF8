using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeDrawPointParticleTableReader
{
    public static NativeDrawPointParticleTables Read(byte[] executable)
    {
        return Read(new NativeExecutableMemory(executable));
    }

    public static NativeDrawPointParticleTables Read(IGameNativeMemorySource memory)
    {
        string hash = memory.SourceHash;
        byte[] Range(uint address, int length) => memory.Read(address, length);
        return new NativeDrawPointParticleTables(hash, Range(0xB80A18, 256), Range(0xB80D1C, 512), Range(0xB80D9C, 512), Range(0xB809D8, 64));
    }
}

public sealed partial class GameAssets
{
    public NativeDrawPointParticleTables ReadNativeDrawPointParticleTables() => NativeDrawPointParticleTableReader.Read(ReadNativeMemory());
}
