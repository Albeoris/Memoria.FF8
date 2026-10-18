using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco97ProgramResourcesReader
{
    public static NativeBattleChoco97ProgramResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(new(0x168F8BC, memory.Read(0x168F8BC, 0x2884).ToImmutableArray()));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco97ProgramResources? nativeChoco97Program;

    public NativeBattleChoco97ProgramResources ReadNativeChoco97ProgramResources() => nativeChoco97Program ??= NativeBattleChoco97ProgramResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
