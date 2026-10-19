using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleClash26ResourcesReader
{
    public static NativeBattleClash26Resources Read(byte[] executable)
    {
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0x16104B4, 0x160FA94, 0x160FF3C }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(executable, address)).ToImmutableArray();
        ImmutableArray<NativeBattleModuleResource> sprites = new uint[] { 0x160F7A8, 0x160F6CC }.Select(address => new NativeBattleModuleResource(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(executable, address))).ToImmutableArray();
        ImmutableArray<short> fade = Enumerable.Range(0, 12).Select(index => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x161075C + (uint)(2 * index), 2).AsSpan())).ToImmutableArray();
        ImmutableArray<uint>.Builder script = ImmutableArray.CreateBuilder<uint>();
        for (uint address = 0x1610774; ; address += 4)
        {
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, address, 4).AsSpan());
            script.Add(word);
            if ((word >> 24) == 0xFF)
            {
                break;
            }
        }
        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x160F6C8, 4).AsSpan());
        return new(meshes, sprites, fade, script.ToImmutable(), sound);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleClash26Resources? nativeClash26Resources;
    public NativeBattleClash26Resources ReadNativeClash26Resources() => nativeClash26Resources ??= NativeBattleClash26ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}

