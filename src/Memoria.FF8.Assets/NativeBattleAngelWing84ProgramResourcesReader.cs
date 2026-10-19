using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleAngelWing84ProgramResourcesReader
{
    public static NativeBattleAngelWing84ProgramResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleGfAnimationSource> sources = new (uint Address, int Length)[] { (0x171F2B0, 0x12C0), (0x1720570, 0x1238), (0x17217A8, 0x2C50) }.Select(source => new NativeBattleGfAnimationSource(source.Address, NativeBattleEffectStaticResourcesReader.Read(memory, source.Address, source.Length))).ToImmutableArray();
        return new(sources);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleAngelWing84ProgramResources? nativeAngelWing84Programs;
    public NativeBattleAngelWing84ProgramResources ReadNativeAngelWing84ProgramResources() => nativeAngelWing84Programs ??= NativeBattleAngelWing84ProgramResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
