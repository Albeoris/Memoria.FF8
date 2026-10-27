using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeteor223ReadbackTests
{
    [Fact]
    public void Pending_readback_restores_and_observes_queue_order_and_latest_destination()
    {
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x100, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        NativeBattleEffectTextureRectangle first = new(2, 3, 2, 2);
        textures = textures.EnqueueArenaRectangle505DF0(first, arena, 0x100);
        textures = textures.EnqueueArenaReadback505E70(first, 0x200);
        textures = textures.EnqueueArenaRectangle505DF0(new(7, 9, 2, 2), arena, 0x200);
        textures = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectTextureState>(NativeBattleTestSnapshots.Json(textures))!;
        arena = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(arena.Write(0x210, new byte[] { 9 })))!;
        NativeBattleEffectTextureDrain drain = textures.Drain505D20(arena);
        Assert.Equal(0, drain.State.Count);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, drain.Arena!.Read(0x200, 8));
        Assert.Equal(new byte[] { 9 }, drain.Arena.Read(0x210, 1));
        Assert.Equal(2, drain.Uploads.Length);
        Assert.Equal(drain.Uploads[0].Bytes, drain.Uploads[1].Bytes);
        Assert.Equal((byte)2, drain.State.Slots[1]!.Kind);
        Assert.Empty(drain.State.Drain505D20(drain.Arena).Uploads);
    }

    [Fact]
    public void Readback_keeps_rectangle_value_and_requires_actual_destination_at_drain()
    {
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(4, 5, 1, 1, new byte[] { 10, 11 });
        textures = textures.EnqueueArenaReadback505E70(new(4, 5, 1, 1), 0x300);
        Assert.Throws<NotSupportedException>(() => textures.Drain505D20());
        NativeBattleEffectTextureDrain drain = textures.Drain505D20(NativeBattleEffectArena.Clear());
        Assert.Equal(new byte[] { 10, 11 }, drain.Arena!.Read(0x300, 2));
        Assert.Empty(drain.Uploads);
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleStorageSnapshots.Capture(textures).VramRegions), NativeBattleTestSnapshots.Json(NativeBattleStorageSnapshots.Capture(drain.State).VramRegions));
    }
}
