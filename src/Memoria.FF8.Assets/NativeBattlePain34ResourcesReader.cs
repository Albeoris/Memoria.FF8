using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattlePain34ResourcesReader
{
    public static NativeBattlePain34Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGfAnimationSource glow = new(0x15E7040, Read(0x15E7040, 0x32A8));
        NativeBattleGfAnimationSource model = new(0x15EA2E8, Read(0x15EA2E8, 0xFAC));
        _ = new NativeBattleGfAnimationWorkspace(new byte[2056].ToImmutableArray()).Initialize7016B0(glow, 2056);
        _ = new NativeBattleGfAnimationWorkspace(new byte[640].ToImmutableArray()).Initialize7016B0(model, 640);
        return new(glow, model, new("FF8_EN:15EB294", Read(0x15EB294, 0xAC8)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x15E703C, 4)));

        ImmutableArray<byte> Read(uint address, int length) => NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
    }

    public static NativeBattlePain34State ReadColdProcess(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        byte[] bytes = memory.ReadLoader(NativeBattlePain34State.Address, NativeBattlePain34State.Length);
        return new(NativeBattleEffectArena.Clear().Write(0, bytes));
    }
}

public sealed partial class GameAssets
{
    private NativeBattlePain34Resources? nativePain34Resources;
    public NativeBattlePain34Resources ReadNativePain34Resources() => nativePain34Resources ??= NativeBattlePain34ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}