using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGilgameshOpeningFlareResourcesReader
{
    public static NativeBattleGilgameshOpeningFlareResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        byte[] bytes = memory.Read(0xCD03A8, 64);
        ImmutableArray<NativeBattleGilgameshOpeningFlareRow>.Builder rows = ImmutableArray.CreateBuilder<NativeBattleGilgameshOpeningFlareRow>(8);
        for (int index = 0; index < 8; index++)
        {
            int offset = index * 8;
            rows.Add(new(Word(offset), Word(offset + 2), Word(offset + 4), Word(offset + 6)));
        }
        NativeBattleGilgameshOpeningFlareResources resources = new(new(0, 0xCC6B64, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0xCC6B64)), rows.MoveToImmutable());
        resources.Validate();
        return resources;

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGilgameshOpeningFlareResources? nativeGilgameshOpeningFlareResources;

    public NativeBattleGilgameshOpeningFlareResources ReadNativeGilgameshOpeningFlareResources() => nativeGilgameshOpeningFlareResources ??= NativeBattleGilgameshOpeningFlareResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
