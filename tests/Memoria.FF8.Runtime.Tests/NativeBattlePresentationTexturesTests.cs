using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePresentationTexturesTests
{
    [Fact]
    public void An_actor_can_branch_from_the_initial_global_vram_publication()
    {
        NativeBattleEffectTextureState global = Pixels(1);
        NativeBattleEffectTextureState state = global.WithPresentationTexture("a", Bank(global));
        NativeBattlePresentationTextureBank child = state.RequirePresentationTexture("a");

        Assert.NotSame(state.VramCaptureIdentity, child.Textures.VramCaptureIdentity);
        NativeBattleEffectTextureState updated = state.Upload45BD30(64, 256, 1, 1, [2, 0]);
        child = updated.RequirePresentationTexture("a");
        updated = updated.WithPresentationTexture("a", child with { Textures = child.Textures.Upload45BD30(64, 256, 1, 1, [3, 0]) });

        Assert.Equal((ushort)1, state.ReadVramWord(64, 256));
        Assert.Equal((ushort)1, state.RequirePresentationTexture("a").Textures.ReadVramWord(64, 256));
        Assert.Equal((ushort)2, updated.ReadVramWord(64, 256));
        Assert.Equal((ushort)3, updated.RequirePresentationTexture("a").Textures.ReadVramWord(64, 256));
    }

    [Fact]
    public void Actor_banks_hold_independent_pixels_and_cache_state_at_the_same_coordinates()
    {
        NativeBattleEffectTextureState global = Pixels(1);
        NativeBattlePresentationTextureBank first = Bank(Pixels(2));
        NativeBattlePresentationTextureBank second = Bank(Pixels(3));
        NativeBattleEffectTextureState state = global.WithPresentationTexture("actor", first).WithPresentationTexture("Actor", second);
        NativeBattlePresentationTextureBank replacement = new(first.Textures.Upload45BD30(64, 256, 1, 1, [4, 0]), first.Cache.Preprocess462F90(0, 0));
        NativeBattleEffectTextureState updated = state.WithPresentationTexture("actor", replacement);

        Assert.Same(global.VramPublicationIdentity, updated.VramPublicationIdentity);
        Assert.Equal(global.Slots, updated.Slots);
        Assert.Equal(global.LiveTextureSources, updated.LiveTextureSources);
        Assert.Equal((ushort)1, updated.ReadVramWord(64, 256));
        Assert.Equal((ushort)2, state.RequirePresentationTexture("actor").Textures.ReadVramWord(64, 256));
        Assert.Equal((ushort)3, updated.RequirePresentationTexture("Actor").Textures.ReadVramWord(64, 256));
        Assert.Equal((ushort)4, updated.RequirePresentationTexture("actor").Textures.ReadVramWord(64, 256));
        Assert.Same(second, updated.RequirePresentationTexture("Actor"));
        Assert.Equal(1u, updated.RequirePresentationTexture("actor").Cache.Rows[0].UsedMask);
        Assert.Equal(0u, first.Cache.Rows[0].UsedMask);
        Assert.Equal(0u, second.Cache.Rows[0].UsedMask);
        Assert.Throws<InvalidDataException>(() => updated.RequirePresentationTexture("ACTOR"));
    }

    [Fact]
    public void Two_actor_banks_can_branch_from_one_texture_publication()
    {
        NativeBattlePresentationTextureBank shared = Bank(Pixels(2));
        NativeBattleEffectTextureState initial = Pixels(1).WithPresentationTexture("a", shared).WithPresentationTexture("b", shared);
        NativeBattlePresentationTextureBank a = initial.RequirePresentationTexture("a");
        NativeBattlePresentationTextureBank b = initial.RequirePresentationTexture("b");
        Assert.NotSame(a.Textures.VramCaptureIdentity, b.Textures.VramCaptureIdentity);
        Assert.Same(a.Cache, b.Cache);
        NativeBattleEffectTextureState first = initial.WithPresentationTexture("a", a with { Textures = a.Textures.Upload45BD30(64, 256, 1, 1, [3, 0]) });
        b = first.RequirePresentationTexture("b");
        NativeBattleEffectTextureState second = first.WithPresentationTexture("b", b with { Textures = b.Textures.Upload45BD30(64, 256, 1, 1, [4, 0]) });

        Assert.Same(a.Textures.VramCaptureIdentity, second.RequirePresentationTexture("a").Textures.VramCaptureIdentity);
        Assert.Same(b.Textures.VramCaptureIdentity, second.RequirePresentationTexture("b").Textures.VramCaptureIdentity);
        Assert.Equal((ushort)2, initial.RequirePresentationTexture("a").Textures.ReadVramWord(64, 256));
        Assert.Equal((ushort)2, first.RequirePresentationTexture("b").Textures.ReadVramWord(64, 256));
        Assert.Equal((ushort)3, second.RequirePresentationTexture("a").Textures.ReadVramWord(64, 256));
        Assert.Equal((ushort)4, second.RequirePresentationTexture("b").Textures.ReadVramWord(64, 256));
    }

    [Fact]
    public void Capture_and_independent_json_restores_keep_child_queue_provenance_and_cache()
    {
        NativeBattleTextureOrigin source = new("body.tim", 0, NativeBattleTextureRole.Pixels, 4, 2, 1);
        NativeBattleEffectTextureState child = NativeBattleEffectTextureState.FromColdProcess()
            .EnqueueRectangle505DF0(new(64, 256, 2, 1), new("body", [2, 0, 3, 0]), origin: source).Drain505D20().State
            .EnqueueRectangle505DF0(new(66, 256, 2, 1), new("queued", [5, 0, 6, 0]), origin: source with { Member = 1 });
        NativeBattlePresentationTextureBank bank = new(child, NativeBattlePcTextureWindowCache.Reset4647A0(null).Preprocess462F90(0, 0));
        NativeBattleEffectTextureState state = Pixels(1).WithPresentationTexture("a", bank);
        NativeBattleEffectTextureState captured = NativeBattleStorageSnapshots.Capture(state);
        string json = JsonSerializer.Serialize(captured);
        NativeBattleEffectTextureState first = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectTextureState>(json);
        NativeBattleEffectTextureState second = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectTextureState>(json);
        NativeBattlePresentationTextureBank restored = second.RequirePresentationTexture("a");

        Assert.True(((INativeBattleStorageNode)captured.RequirePresentationTexture("a").Textures).IsSnapshot);
        Assert.False(((INativeBattleStorageNode)restored.Textures).IsSnapshot);
        Assert.Equal(1, restored.Textures.Count);
        Assert.Equal(child.Slots[0]!.Origin, restored.Textures.Slots[0]!.Origin);
        Assert.Equal(child.Slots[0]!.OriginSourceSha256, restored.Textures.Slots[0]!.OriginSourceSha256);
        Assert.Equal(Assert.Single(child.LiveTextureSources!.Value), Assert.Single(restored.Textures.LiveTextureSources!.Value));
        Assert.Equal(1u, restored.Cache.Rows[0].UsedMask);
        Assert.Null(restored.Cache.SpriteTileCacheControl1CCFD90);

        NativeBattlePresentationTextureBank firstBank = first.RequirePresentationTexture("a");
        NativeBattleEffectTextureState drained = firstBank.Textures.Drain505D20().State;
        first = first.WithPresentationTexture("a", firstBank with { Textures = drained.Upload45BD30(64, 256, 1, 1, [9, 0]) });
        Assert.Equal((ushort)9, first.RequirePresentationTexture("a").Textures.ReadVramWord(64, 256));
        Assert.Equal((ushort)5, first.RequirePresentationTexture("a").Textures.ReadVramWord(66, 256));
        Assert.Equal((ushort)2, restored.Textures.ReadVramWord(64, 256));
        Assert.Equal(1, restored.Textures.Count);
        Assert.Equal(json, JsonSerializer.Serialize(captured));
        Assert.Equal(json, NativeBattleTestSnapshots.Json(second));
    }

    [Fact]
    public void Native_texture_json_omits_hosted_ownership_and_roundtrips()
    {
        NativeBattleEffectTextureState state = Pixels(1);
        string json = NativeBattleTestSnapshots.Json(state);
        NativeBattleEffectTextureState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectTextureState>(json);

        Assert.DoesNotContain("PresentationTextures", json);
        Assert.Null(restored.PresentationTextures);
        Assert.Equal(json, NativeBattleTestSnapshots.Json(restored));
        Assert.Throws<InvalidDataException>(() => restored.RequirePresentationTexture("a"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData(null)]
    public void Nonblank_actor_id_is_required(string? actorId)
    {
        NativeBattleEffectTextureState state = Pixels(1);
        Assert.Throws<InvalidDataException>(() => state.WithPresentationTexture(actorId!, Bank(Pixels(2))));
        Assert.Throws<InvalidDataException>(() => state.RequirePresentationTexture(actorId!));
    }

    [Fact]
    public void Malformed_banks_are_rejected_before_publication_or_capture()
    {
        NativeBattleEffectTextureState state = Pixels(1);
        NativeBattlePresentationTextureBank bank = Bank(Pixels(2));
        NativeBattleEffectTextureState nested = Pixels(3).WithPresentationTexture("nested", bank);
        NativeBattleEffectTextureState invalidParent = state with { Count = 1 };

        Assert.Throws<InvalidDataException>(() => invalidParent.WithPresentationTexture("a", bank));
        Assert.Throws<InvalidDataException>(() => state.WithPresentationTexture("a", null!));
        Assert.Throws<InvalidDataException>(() => state.WithPresentationTexture("a", bank with { Textures = null! }));
        Assert.Throws<InvalidDataException>(() => state.WithPresentationTexture("a", bank with { Cache = null! }));
        Assert.Throws<InvalidDataException>(() => state.WithPresentationTexture("a", bank with { Cache = bank.Cache with { Rows = [] } }));
        Assert.Throws<InvalidDataException>(() => state.WithPresentationTexture("a", bank with { Textures = nested }));
        Assert.Throws<InvalidDataException>(() => state.WithPresentationTexture("a", bank with { Textures = NativeBattleStorageSnapshots.Capture(bank.Textures) }));

        NativeBattleEffectTextureState wrongComparer = state with { PresentationTextures = ImmutableDictionary.Create<string, NativeBattlePresentationTextureBank>(StringComparer.OrdinalIgnoreCase).Add("a", bank) };
        Assert.Throws<InvalidDataException>(wrongComparer.Validate);
        NativeBattleEffectTextureState blankKey = state with { PresentationTextures = ImmutableDictionary<string, NativeBattlePresentationTextureBank>.Empty.Add("", bank) };
        Assert.Throws<InvalidDataException>(() => NativeBattleStorageSnapshots.Capture(blankKey));

        NativeBattleEffectTextureState detached = NativeBattleStorageSnapshots.Capture(state);
        Assert.Throws<InvalidOperationException>(() => detached.WithPresentationTexture("a", bank));
        NativeBattleEffectTextureState mixed = detached with { PresentationTextures = ImmutableDictionary<string, NativeBattlePresentationTextureBank>.Empty.Add("a", bank) };
        Assert.Throws<InvalidDataException>(() => JsonSerializer.Serialize(mixed));
        Assert.Throws<InvalidDataException>(() => NativeBattleStorageSnapshots.Restore(mixed));
    }

    [Fact]
    public void Direct_maps_cannot_share_actor_vram_ownership_in_working_or_detached_storage()
    {
        NativeBattleEffectTextureState state = Pixels(1);
        NativeBattlePresentationTextureBank bank = Bank(Pixels(2));
        NativeBattleEffectTextureState updated = bank.Textures.Upload45BD30(64, 256, 1, 1, [3, 0]);
        NativeBattleEffectTextureState shared = state with { PresentationTextures = ImmutableDictionary<string, NativeBattlePresentationTextureBank>.Empty.Add("a", bank).Add("b", bank with { Textures = updated }) };
        Assert.Throws<InvalidDataException>(shared.Validate);
        Assert.Throws<InvalidDataException>(() => NativeBattleStorageSnapshots.Capture(shared));
        NativeBattleEffectTextureState sharedWithGlobal = state with { PresentationTextures = ImmutableDictionary<string, NativeBattlePresentationTextureBank>.Empty.Add("a", Bank(state)) };
        Assert.Throws<InvalidDataException>(sharedWithGlobal.Validate);
        Assert.Throws<InvalidDataException>(() => NativeBattleStorageSnapshots.Capture(sharedWithGlobal));

        NativeBattlePresentationTextureBank detachedBank = bank with { Textures = NativeBattleStorageSnapshots.Capture(updated) };
        NativeBattleEffectTextureState detached = NativeBattleStorageSnapshots.Capture(state) with { PresentationTextures = ImmutableDictionary<string, NativeBattlePresentationTextureBank>.Empty.Add("a", detachedBank).Add("b", detachedBank with { }) };
        Assert.Throws<InvalidDataException>(detached.Validate);
        Assert.Throws<InvalidDataException>(() => NativeBattleStorageSnapshots.Restore(detached));
        Assert.Throws<InvalidDataException>(() => JsonSerializer.Serialize(detached));
        NativeBattleEffectTextureState detachedGlobal = NativeBattleStorageSnapshots.Capture(state);
        NativeBattleEffectTextureState detachedSharedWithGlobal = detachedGlobal with { PresentationTextures = ImmutableDictionary<string, NativeBattlePresentationTextureBank>.Empty.Add("a", Bank(detachedGlobal)) };
        Assert.Throws<InvalidDataException>(detachedSharedWithGlobal.Validate);
        Assert.Throws<InvalidDataException>(() => NativeBattleStorageSnapshots.Restore(detachedSharedWithGlobal));
        Assert.Throws<InvalidDataException>(() => JsonSerializer.Serialize(detachedSharedWithGlobal));
    }

    private static NativeBattleEffectTextureState Pixels(byte value) => NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(64, 256, 1, 1, [value, 0]);

    private static NativeBattlePresentationTextureBank Bank(NativeBattleEffectTextureState textures) => new(textures, NativeBattlePcTextureWindowCache.Reset4647A0(null));
}
