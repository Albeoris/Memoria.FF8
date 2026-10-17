using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleSharedEffectRandomResourcesTests
{
    [Fact]
    public void Authenticated_loader_publishes_actual_BSS_DWORD2557098_without_aliasing_private_GF_seed()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleRetainedMemory cold = game.ReadColdProcessNativeBattleMemory();
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, cold.ColdExecutableSha256);
        NativeBattleEffectMemory memory = cold.EffectMemory!;
        Assert.Equal(0u, memory.SharedEffectRandom!.State);
        NativeBattleEffectMemory drawn = memory.DrawSharedEffectRandom7059E0(out ushort result);
        Assert.Equal((ushort)14, result);
        Assert.Equal(0u, memory.SharedEffectRandom.State);
        Assert.Equal(14u, drawn.SharedEffectRandom!.State);
        Assert.Equal(memory.QuezacotlPause, drawn.QuezacotlPause);
    }
}
