using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGilgameshSwordParticleResourcesReader
{
    public static NativeBattleGilgameshSwordParticleResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        byte[] order = memory.Read(0xCD0480, 16);
        NativeBattleGilgameshSwordParticleResources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCC8E64u), new(0, 0xCC6C34, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0xCC6C34)), Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt32LittleEndian(order.AsSpan(index * 4))).ToImmutableArray());
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGilgameshSwordParticleResources? nativeGilgameshSwordParticleResources;

    public NativeBattleGilgameshSwordParticleResources ReadNativeGilgameshSwordParticleResources() => nativeGilgameshSwordParticleResources ??= NativeBattleGilgameshSwordParticleResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
