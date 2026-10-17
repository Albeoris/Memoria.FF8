using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCommonTextureResourcesTests
{
    [Fact]
    public void Original_A3_is_read_into_mutable_arena_then_queued_and_drained_by_current_TIM_contract()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleEffectInitializerAsset source = game.ReadNativeCommonEffectTexture();
        Assert.Equal("a8def.tim", source.Name);
        Assert.NotEmpty(source.Data);
        NativeBattleEffectMemory memory = game.ReadColdProcessNativeBattleMemory().EffectMemory!.BeginLoad(115);
        memory = memory with { Arena = memory.Arena!.Write(0x2E50, source.Data.AsSpan()) };
        (NativeBattleEffectTextureState queue, int next) = memory.Textures!.EnqueueArenaTim505E30(memory.Arena!, 0x2E50);
        Assert.InRange(next, 0x2E50 + 32, 0x2E50 + source.Data.Length);
        Assert.Equal(0x2E50, queue.Slots[0]!.ArenaOffset);
        Assert.Null(queue.Slots[0]!.Source);
        NativeBattleEffectTextureDrain drained = queue.Drain505D20(memory.Arena);
        Assert.Equal(2, drained.Uploads.Length);
        Assert.Equal(0, drained.State.Count);
        Assert.All(drained.Uploads, upload => Assert.Equal(2 * upload.Width * upload.Height, upload.Bytes.Length));
    }
}
