using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedTextureDrainTests
{
    private static readonly NativeBattleEffectTextureRectangle Rectangle = new(0, 0, 1, 1);

    [Fact]
    public void Global_drain_preserves_readback_and_invalidates_the_cache_against_the_pre_drain_image()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectTextureState textures = memory.Textures!.Upload45BD30(0, 0, 1, 1, [0x11, 0]);
        textures = QueueWord(textures, 0x22).EnqueueArenaReadback505E70(Rectangle, 16);
        textures = QueueWord(textures, 0x33);

        NativeBattleEffectMemory drained = BattleSession.DrainNativeTextureUploads(memory with { Textures = textures }, textures, null);

        Assert.Equal(new byte[] { 0x22, 0 }, drained.Arena!.Read(16, 2));
        Assert.Equal(0, drained.Textures!.Count);
        Assert.Equal(new byte[] { 0x33, 0 }, drained.Textures.ReadVram()[..2]);
        AssertInvalidated(drained.PrimitiveProcess!.TextureWindowCache!, 0x11);
    }

    [Fact]
    public void Actor_uploads_and_copies_retain_separate_images_and_each_cache_keeps_the_first_old_pixels()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectTextureState global = memory.Textures!.Upload45BD30(0, 0, 1, 1, [0x77, 0]);
        NativeBattleEffectTextureState alpha = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [0x11, 0]);
        alpha = QueueWord(QueueWord(alpha, 0x22), 0x33);
        NativeBattleEffectTextureState beta = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 2, 1, [0x44, 0, 0x88, 0]);
        beta = beta.EnqueueCopy505EB0(1, 0, 1, 1, 0, 0);
        global = global.WithPresentationTexture("beta", new(beta, PreparedCache())).WithPresentationTexture("alpha", new(alpha, PreparedCache()));

        NativeBattleEffectMemory drained = BattleSession.DrainNativeTextureUploads(memory with { Textures = global }, global, null);

        Assert.Same(memory.Arena, drained.Arena);
        Assert.Equal(new byte[] { 0x77, 0 }, drained.Textures!.ReadVram()[..2]);
        Assert.True(Assert.Single(drained.PrimitiveProcess!.TextureWindowCache!.Rows[0].Regions).CurrentPixels);
        AssertActor(drained.Textures.RequirePresentationTexture("alpha"), 0x33, 0x11);
        AssertActor(drained.Textures.RequirePresentationTexture("beta"), 0x88, 0x44);
    }

    [Fact]
    public void Actor_readback_is_rejected_before_the_global_upload_mutates_its_owner()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectTextureState global = QueueWord(memory.Textures!, 0x22);
        NativeBattleEffectTextureState actor = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaReadback505E70(Rectangle, 16);
        global = global.WithPresentationTexture("actor", new(actor, PreparedCache()));

        Assert.Throws<NotSupportedException>(() => BattleSession.DrainNativeTextureUploads(memory with { Textures = global }, global, null));

        NativeBattleEffectTextureState retry = global with { PresentationTextures = null };
        NativeBattleEffectMemory drained = BattleSession.DrainNativeTextureUploads(memory with { Textures = retry }, retry, null);
        Assert.Equal(new byte[] { 0x22, 0 }, drained.Textures!.ReadVram()[..2]);
    }

    private static NativeBattleEffectMemory Memory()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0);
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { TextureWindowCache = PreparedCache() };
        return memory with { PrimitiveProcess = process };
    }

    private static NativeBattlePcTextureWindowCache PreparedCache()
    {
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null) with { ScaleU = 1, ScaleV = 1 };
        return cache.Preprocess462F90(0, 0).Prepare464BD0().Cache.Flush465930();
    }

    private static NativeBattleEffectTextureState QueueWord(NativeBattleEffectTextureState owner, byte value)
    {
        NativeBattleEffectHeapAllocation allocation = new("word", ImmutableArray.Create(value, (byte)0));
        return owner.EnqueueRectangle505DF0(Rectangle, allocation);
    }

    private static void AssertActor(NativeBattlePresentationTextureBank actor, byte current, ushort previous)
    {
        Assert.Equal(0, actor.Textures.Count);
        Assert.Equal(new byte[] { current, 0 }, actor.Textures.ReadVram()[..2]);
        AssertInvalidated(actor.Cache, previous);
    }

    private static void AssertInvalidated(NativeBattlePcTextureWindowCache cache, ushort previous)
    {
        NativeBattlePcTextureWindowRow row = cache.Rows[0];
        Assert.Equal(uint.MaxValue, row.DirtyMask);
        NativeBattlePcTextureWindowRegion region = Assert.Single(row.Regions);
        Assert.False(region.CurrentPixels);
        Assert.Equal(previous, region.RetainedPixels!.ReadWord(0, 0));
    }
}
