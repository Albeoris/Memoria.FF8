using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleTornado146ResourcesReader
{
    public static NativeBattleTornado146Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ImmutableArray<int> Table(uint address) => Enumerable.Range(0, 15).Select(index => unchecked((int)Dword(address + (uint)index * 4))).ToImmutableArray();
        return new(new("FF8_EN:DD05F8", memory.Read(0xDD05F8, 0x388).ToImmutableArray()), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDD0980), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDD0B2C), memory.Read(0xDD181C, 960).ToImmutableArray(), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDD5564), Enumerable.Range(0, 15).Select(index => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, Dword(0xDD6644u + (uint)index * 4))).ToImmutableArray(), Table(0xDD6680), Table(0xDD66BC), Table(0xDD66F8), memory.Read(0xDD6780, 24).ToImmutableArray(), Dword(0xDD6638), Dword(0xDD663C));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleTornado146Resources? nativeTornado146Resources;
    public NativeBattleTornado146Resources ReadNativeTornado146Resources() => nativeTornado146Resources ??= NativeBattleTornado146ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
