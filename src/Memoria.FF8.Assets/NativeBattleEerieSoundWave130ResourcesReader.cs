using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEerieSoundWave130ResourcesReader
{
    public static NativeBattleEerieSoundWave130Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x12238B0, 11)) != "mag129.tim\0")
        {
            throw new InvalidDataException("Eerie Sound Wave requires its original initializer texture name.");
        }
        NativeBattleEerieSoundWave130Resources resources = new(new(0x1221138, memory.Read(0x1221138, 6000).ToImmutableArray()), new(0x12228A8, memory.Read(0x12228A8, 3940).ToImmutableArray()), new(0x1220990, memory.Read(0x1220990, 1960).ToImmutableArray()), new("FF8_EN:1223810", memory.Read(0x1223810, 160).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x122380C, 4)));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEerieSoundWave130Resources? nativeEerieSoundWave130Resources;
    public NativeBattleEerieSoundWave130Resources ReadNativeEerieSoundWave130Resources() => nativeEerieSoundWave130Resources ??= NativeBattleEerieSoundWave130ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
