namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFireBreath230TextureTests
{
    [Fact]
    public void Shared_texture_and_model_row_bytes_transfer_opaquely_without_resolving_symbolic_pointers()
    {
        const int length = 128 * 256 * 2;
        const int descriptors = 12288;
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)7, length).ToArray());
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(576, 256, 128, 256), source, 0);
        source.WriteResourcePointer(descriptors, new(new(0, 0) { ResourceOnly = true }, null));
        source.WriteResourcePointer(descriptors + 4, new(new(0, 1312) { ResourceOnly = true }, null));
        source.WriteResourcePointer(descriptors + 16, new(new(0, 49844) { ResourceOnly = true }, null));
        source.WriteResourcePointer(descriptors + 20, new(new(0, 50292) { ResourceOnly = true }, null));
        source.Write(descriptors + 32, [11, 22]);
        NativeBattleEffectTextureDrain uploaded = queued.Drain505D20(source);
        Assert.Equal(0, uploaded.State.Count);
        Assert.Equal((ushort)0x0707, uploaded.State.ReadVramWord(576, 256));
        Assert.Equal((ushort)0x160B, uploaded.State.ReadVramWord(592, 304));
        Assert.Throws<NotSupportedException>(() => uploaded.State.ReadVramWord(576, 304));
        Assert.Equal(16, Assert.Single(uploaded.Uploads).Known!.Value.Count(value => !value));
        NativeBattleEffectTextureState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectTextureState>(NativeBattleTestSnapshots.Json(uploaded.State))!;
        Assert.Equal((ushort)0x160B, restored.ReadVramWord(592, 304));
        Assert.Throws<NotSupportedException>(() => restored.ReadVramWord(576, 304));
        NativeBattleEffectTextureDrain copied = restored.EnqueueCopy505EB0(576, 256, 128, 256, 0, 0).Drain505D20();
        Assert.Equal((ushort)0x160B, copied.State.ReadVramWord(16, 48));
        Assert.Throws<NotSupportedException>(() => copied.State.ReadVramWord(0, 48));
        NativeBattleEffectArena destination = NativeBattleEffectArena.Clear();
        NativeBattleEffectTextureDrain readback = copied.State.EnqueueArenaReadback505E70(new(0, 0, 128, 256), 0).Drain505D20(destination);
        Assert.Equal(new byte[] { 11, 22 }, readback.Arena!.Read(descriptors + 32, 2));
        Assert.Equal(16, readback.Arena.ReadSnapshot(0, length).Known.Count(value => !value));
        Assert.Throws<NotSupportedException>(() => readback.Arena.Read(descriptors, 2));
    }
    [Fact]
    public void Overlapping_copy_preserves_native_forward_dword_and_tail_knownness()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, new byte[] { 1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0 }).MarkUnknown(2, 2);
        NativeBattleEffectTextureState uploaded = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(0, 0, 6, 1), source, 0).Drain505D20(source).State;
        NativeBattleEffectTextureState copied = uploaded.EnqueueCopy505EB0(0, 0, 5, 1, 1, 0).Drain505D20().State;
        Assert.Equal((ushort)1, copied.ReadVramWord(1, 0));
        Assert.Throws<NotSupportedException>(() => copied.ReadVramWord(2, 0));
        Assert.Throws<NotSupportedException>(() => copied.ReadVramWord(3, 0));
        Assert.Equal((ushort)4, copied.ReadVramWord(4, 0));
        Assert.Equal((ushort)4, copied.ReadVramWord(5, 0));
    }

}
