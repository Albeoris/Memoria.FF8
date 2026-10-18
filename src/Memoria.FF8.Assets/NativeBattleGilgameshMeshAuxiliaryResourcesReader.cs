using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGilgameshMeshAuxiliaryResourcesReader
{
    public static NativeBattleGilgameshMeshAuxiliaryResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        byte[] bytes = memory.Read(0xCD0468, 24);
        ImmutableArray<NativeBattleGilgameshMeshAuxiliaryRow>.Builder rows = ImmutableArray.CreateBuilder<NativeBattleGilgameshMeshAuxiliaryRow>();
        for (int index = 0; index < 3; index++)
        {
            int offset = index * 8;
            rows.Add(new(Word(offset), Word(offset + 2), Word(offset + 4), Word(offset + 6)));
        }
        NativeBattleGilgameshMeshAuxiliaryResources resources = new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCC78ECu), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xCC7A94u), rows.ToImmutable());
        resources.Validate();
        return resources;

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGilgameshMeshAuxiliaryResources? nativeGilgameshMeshAuxiliaryResources;

    public NativeBattleGilgameshMeshAuxiliaryResources ReadNativeGilgameshMeshAuxiliaryResources() => nativeGilgameshMeshAuxiliaryResources ??= NativeBattleGilgameshMeshAuxiliaryResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
