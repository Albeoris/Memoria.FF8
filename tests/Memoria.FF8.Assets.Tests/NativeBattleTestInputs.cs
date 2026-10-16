using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

internal static class NativeBattleTestInputs
{
    private static readonly Lazy<(ImmutableArray<byte> Sequences, NativeBattleRetainedMemory ColdMemory)> Inputs = new(() =>
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        return (assets.ReadNativeBattleCommandSequences(), assets.ReadColdProcessNativeBattleMemory());
    });

    public static ImmutableArray<byte> CommandSequences => Inputs.Value.Sequences;
    public static NativeBattleRetainedMemory ColdMemory => Inputs.Value.ColdMemory;
}
