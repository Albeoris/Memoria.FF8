using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGilgameshSwordTrailResourcesReader
{
    public static NativeBattleGilgameshSwordTrailResources Read(byte[] executable)
    {
        byte[] bytes = new NativeExecutableMemory(executable).Read(0xCD0490, 16);
        return new(Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray());
    }
}

public sealed partial class GameAssets
{
    private NativeBattleGilgameshSwordTrailResources? nativeGilgameshSwordTrailResources;

    public NativeBattleGilgameshSwordTrailResources ReadNativeGilgameshSwordTrailResources() => nativeGilgameshSwordTrailResources ??= NativeBattleGilgameshSwordTrailResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
