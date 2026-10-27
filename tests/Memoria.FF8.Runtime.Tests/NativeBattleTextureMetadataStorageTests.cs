using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTextureMetadataStorageTests
{
    [Fact]
    public void Detached_capture_and_independent_restores_keep_immutable_provenance_and_copy_history()
    {
        NativeBattleEffectTextureState state = Fixture();
        NativeBattleEffectTextureState captured = NativeBattleStorageSnapshots.Capture(state);
        Assert.Equal(state.LiveTextureSources, captured.LiveTextureSources);
        Assert.Equal(state.TextureRegistrations, captured.TextureRegistrations);
        Assert.Same(state.TextureFileContext, captured.TextureFileContext);
        Assert.Same(state.LiveTextureSources!.Value[0], captured.LiveTextureSources!.Value[0]);
        Assert.Same(state.TextureRegistrations!.Value[0], captured.TextureRegistrations!.Value[0]);
        NativeBattleEffectTextureState first = NativeBattleStorageSnapshots.Restore(captured);
        NativeBattleEffectTextureState second = NativeBattleStorageSnapshots.Restore(captured);
        Assert.Equal(state.LiveTextureSources, first.LiveTextureSources);
        Assert.Equal(state.TextureRegistrations, first.TextureRegistrations);
        Assert.Same(state.TextureFileContext, first.TextureFileContext);
        first = first.Upload45BD30(0, 0, 1, 1, [9, 0]).OpenTextureFile(new("next.tim", NativeBattleTextureDomain.Battle));
        Assert.Equal((ushort)9, first.ReadVramWord(0, 0));
        Assert.Equal((ushort)1, second.ReadVramWord(0, 0));
        Assert.Equal(state.LiveTextureSources, second.LiveTextureSources);
        Assert.Equal(state.TextureRegistrations, second.TextureRegistrations);
        Assert.Equal("source.tim", second.TextureFileContext!.Identity.Name);
        Assert.Equal(state.LiveTextureSources, captured.LiveTextureSources);
        Assert.Throws<InvalidOperationException>(() => captured.Upload45BD30(0, 0, 1, 1, [7, 0]));
    }

    [Fact]
    public void Json_roundtrip_retains_all_metadata_and_legacy_null_fields_stay_optional()
    {
        NativeBattleEffectTextureState captured = NativeBattleStorageSnapshots.Capture(Fixture());
        string json = JsonSerializer.Serialize(captured);
        NativeBattleEffectTextureState decoded = JsonSerializer.Deserialize<NativeBattleEffectTextureState>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(decoded));
        NativeBattleEffectTextureState restored = NativeBattleStorageSnapshots.Restore(decoded);
        Assert.Equal(captured.LiveTextureSources!.Value.ToArray(), restored.LiveTextureSources!.Value.ToArray());
        Assert.Equal(captured.TextureRegistrations!.Value[0].Origin, restored.TextureRegistrations!.Value[0].Origin);
        Assert.Equal(captured.TextureRegistrations.Value[0].CopyFrames.ToArray(), restored.TextureRegistrations.Value[0].CopyFrames.ToArray());
        Assert.Equal(captured.TextureRegistrations.Value[0].CurrentFrame, restored.TextureRegistrations.Value[0].CurrentFrame);
        Assert.Equal(captured.TextureRegistrations.Value[0].CopyHistoryKnown, restored.TextureRegistrations.Value[0].CopyHistoryKnown);
        Assert.Equal(captured.TextureRegistrations.Value[0].OwnedSpans.ToArray(), restored.TextureRegistrations.Value[0].OwnedSpans.ToArray());
        Assert.Equal(captured.TextureRegistrations.Value[0].PaletteOwnedSpans!.Value.ToArray(), restored.TextureRegistrations.Value[0].PaletteOwnedSpans!.Value.ToArray());
        Assert.Equal(captured.TextureFileContext, restored.TextureFileContext);
        Assert.Equal((ushort)1, restored.ReadVramWord(0, 0));
        string legacy = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffectTextureState.FromColdProcess()));
        Assert.DoesNotContain("LiveTextureSources", legacy);
        Assert.DoesNotContain("TextureRegistrations", legacy);
        Assert.DoesNotContain("TextureFileContext", legacy);
        NativeBattleEffectTextureState old = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(legacy)!);
        Assert.Null(old.LiveTextureSources);
        Assert.Null(old.TextureRegistrations);
        Assert.Null(old.TextureFileContext);
    }

    [Fact]
    public void Prepared_presentation_retains_metadata_and_pixel_epoch_after_next_upload()
    {
        NativeBattleEffectTextureState state = Fixture();
        NativeBattlePreparedPresentation prepared = new(1, 2, 3, false, new([], []), state, null, null, null, [], null, null, [], null);
        NativeBattleEffectTextureState captured = NativeBattleStorageSnapshots.Capture(prepared.Textures!);
        string original = JsonSerializer.Serialize(captured);
        NativeBattleEffectTextureState next = state.Upload45BD30(0, 0, 2, 2, [9, 0, 9, 0, 9, 0, 9, 0]).OpenTextureFile(null);
        Assert.Empty(next.LiveTextureSources!.Value);
        NativeBattleTextureRegistration retired = Assert.Single(next.TextureRegistrations!.Value);
        Assert.False(retired.Active);
        Assert.Empty(retired.OwnedSpans);
        Assert.Equal(state.TextureRegistrations!.Value[0].PaletteOwnedSpans, retired.PaletteOwnedSpans);
        Assert.Null(next.TextureFileContext);
        Assert.Same(state, prepared.Textures);
        Assert.Equal((ushort)1, prepared.Textures!.ReadVramWord(0, 0));
        Assert.Equal((ushort)9, next.ReadVramWord(0, 0));
        Assert.Equal(state.LiveTextureSources, prepared.Textures.LiveTextureSources);
        Assert.Equal(state.TextureRegistrations, prepared.Textures.TextureRegistrations);
        Assert.Equal("source.tim", prepared.Textures.TextureFileContext!.Identity.Name);
        Assert.Equal(original, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(prepared.Textures)));
    }

    private static NativeBattleEffectTextureState Fixture()
    {
        NativeBattleTextureOrigin origin = new("source.tim", 2, NativeBattleTextureRole.Pixels, 8, 2, 2)
        {
            Kind = NativeBattleTextureSourceKind.Tim,
            Domain = NativeBattleTextureDomain.Battle,
            SourceOwner = "container.bin",
            SourceOffset = 64,
            PaletteRectangle = new(0, 256, 256, 1)
        };
        return NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 2, 2, [1, 0, 2, 0, 3, 0, 4, 0]) with
        {
            LiveTextureSources = [new(origin, 0, 0, 2, 2)],
            TextureRegistrations = [new(origin, 0, 0, 2, 2, [new(0, 0, 1, 1), new(1, 0, 1, 1)], 1) { CopyHistoryKnown = true, PaletteOwnedSpans = [new(0, 256, 256, 1)] }],
            TextureFileContext = new(new("source.tim", NativeBattleTextureDomain.Battle) { SourceOwner = "container.bin", SourceOffset = 64 }, 3)
        };
    }
}
