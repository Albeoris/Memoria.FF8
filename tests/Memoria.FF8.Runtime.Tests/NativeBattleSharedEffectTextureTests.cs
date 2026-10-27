using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSharedEffectTextureTests
{
    [Fact]
    public void Common_tim_publishes_three_native_pages_with_one_shared_palette_canvas()
    {
        NativeBattleEffectHeapAllocation tim = Tim();
        NativeBattleEffectTextureDrain drain = NativeBattleEffectTextureState.FromColdProcess().EnqueueTim505E30(tim, identity: NativeBattleSharedEffectTexture.File).State.Drain505D20();
        Assert.Equal(2, drain.Uploads.Length);
        NativeBattleTextureSourceRegion[] pages = drain.State.LiveTextureSources!.Value.Where(source => source.Origin.Role == NativeBattleTextureRole.Pixels).ToArray();
        Assert.Equal(3, pages.Length);
        Assert.Equal(3, drain.State.TextureRegistrations!.Value.Length);
        for (int index = 0; index < 3; index++)
        {
            Assert.Equal($"a8def{index}.tim", pages[index].Origin.Owner);
            Assert.Equal(NativeBattleTextureDomain.Battle, pages[index].Origin.Domain);
            Assert.Equal(NativeBattleTextureSourceKind.Image, pages[index].Origin.Kind);
            Assert.Equal((64 + index * 64, 256, 64, 256, 0, 0), (pages[index].X, pages[index].Y, pages[index].WidthWords, pages[index].Height, pages[index].SourceXWords, pages[index].SourceY));
            Assert.Equal(4, pages[index].Origin.BitsPerPixel);
            Assert.Equal(new(0, 0, 16, 2), pages[index].Origin.PaletteRectangle);
        }
        NativeBattleTextureSourceRegion palette = Assert.Single(drain.State.LiveTextureSources.Value.Where(source => source.Origin.Role == NativeBattleTextureRole.Clut));
        Assert.Equal("a8def.tim", palette.Origin.Owner);
        NativeBattleEffectTextureState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(drain.State)))!);
        restored.Validate();
        Assert.Equal(drain.State.LiveTextureSources.Value, restored.LiveTextureSources!.Value);
    }

    [Fact]
    public void Embedded_staging_and_mutable_common_tim_keep_only_their_verified_source_identity()
    {
        NativeBattleEffectHeapAllocation tim = Tim();
        uint address = NativeBattleFileStaging.Address + 0xA00;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, tim.Data.AsSpan());
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueStagingTim505E30(staging, address, NativeBattleSharedEffectTexture.File).State;
        NativeBattleEffectTextureState initialQueued = NativeBattleStorageSnapshots.Capture(queued);
        Assert.Equal(3, queued.Drain505D20(stagingSource: staging).State.LiveTextureSources!.Value.Count(source => source.Origin.Kind == NativeBattleTextureSourceKind.Image));
        NativeBattleFileStaging changed = staging.Write(address + (uint)tim.Data.Length - 2, [1, 2]);
        Assert.Null(NativeBattleStorageSnapshots.Restore(initialQueued).Drain505D20(stagingSource: changed).State.LiveTextureSources);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(512, tim.Data.AsSpan());
        queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaTim505E30(arena, 512, NativeBattleSharedEffectTexture.File).State;
        Assert.Equal(3, queued.Drain505D20(arena).State.LiveTextureSources!.Value.Count(source => source.Origin.Kind == NativeBattleTextureSourceKind.Image));
        Assert.Null(NativeBattleEffectTextureState.FromColdProcess().EnqueueStagingTim505E30(staging, address).State.Drain505D20(stagingSource: staging).State.LiveTextureSources);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Named_raw_page_reuploads_reuse_the_same_virtual_page_identity(int page)
    {
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().EnqueueTim505E30(Tim(), identity: NativeBattleSharedEffectTexture.File).State.Drain505D20().State;
        byte[] raw = new byte[0x8000];
        Array.Fill(raw, (byte)(page + 1));
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(128, raw).RememberTextureFile(128, raw, new($"MA8DEF_P.{page}", NativeBattleTextureDomain.Battle));
        textures = textures.EnqueueArenaRectangle505DF0(new((short)(64 + page * 64), 256, 64, 256), arena, 128).Drain505D20(arena).State;
        NativeBattleTextureSourceRegion[] pixels = textures.LiveTextureSources!.Value.Where(source => source.Origin.Role == NativeBattleTextureRole.Pixels).ToArray();
        Assert.Equal(3, pixels.Length);
        NativeBattleTextureSourceRegion uploaded = Assert.Single(pixels, source => source.X == 64 + page * 64);
        Assert.Equal($"a8def{page}.tim", uploaded.Origin.Owner);
        Assert.Equal(NativeBattleTextureSourceKind.Image, uploaded.Origin.Kind);
        Assert.Equal(NativeBattleTextureDomain.Battle, uploaded.Origin.Domain);
        Assert.Null(uploaded.Origin.PaletteRectangle);
        textures.Validate();
        NativeBattleEffectHeapAllocation heap = new($"ma8def_p.{page}", raw.ToImmutableArray()) { TextureIdentity = new($"ma8def_p.{page}", NativeBattleTextureDomain.Magic) };
        Assert.Null(NativeBattleEffectTextureState.FromColdProcess().EnqueueRectangle505DF0(new(64, 256, 64, 256), heap).Drain505D20().State.LiveTextureSources);
    }

    [Fact]
    public void Unknown_raw_page_words_do_not_claim_a_common_texture_alias()
    {
        NativeBattleEffectHeapAllocation raw = new("ma8def_p.1", new byte[0x8000].ToImmutableArray());
        Assert.Null(NativeBattleEffectTextureState.FromColdProcess().EnqueueRectangle505DF0(new(64, 256, 64, 256), raw).Drain505D20().State.LiveTextureSources);
    }

    [Fact]
    public void Named_battle_raw_file_uses_its_unsuffixed_native_image_identity_and_default_depth()
    {
        byte[] bytes = new byte[64];
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(128, bytes).RememberTextureFile(128, bytes, new("MAG123_B.00", NativeBattleTextureDomain.Battle));
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(64, 256, 4, 4), arena, 128);
        NativeBattleEffectTextureState initialQueued = NativeBattleStorageSnapshots.Capture(queued);
        NativeBattleEffectTextureDrain drain = queued.Drain505D20(arena);
        Assert.Equal("mag123_b.00", drain.Uploads.Single().Origin!.Owner);
        Assert.Equal(NativeBattleTextureSourceKind.Image, drain.Uploads.Single().Origin!.Kind);
        Assert.Equal(8, drain.Uploads.Single().Origin!.BitsPerPixel);
        Assert.Null(NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(64, 256, 4, 4), arena, 130).Drain505D20(arena).Uploads.Single().Origin);
        Assert.Null(NativeBattleStorageSnapshots.Restore(initialQueued).Drain505D20(arena.Write(128, [1, 2])).Uploads.Single().Origin);
        NativeBattleEffectTextureState copied = drain.State.EnqueueCopy505EB0(64, 256, 4, 4, 128, 256).Drain505D20().State;
        NativeBattlePcTextureWindowPixels retained = NativeBattlePcTextureWindowPixels.Capture(50, new(0, 0, 8, 4), copied);
        Assert.Contains(retained.LiveTextureSources!.Value, source => source.X == 128 && source.Origin.Owner == "mag123_b.00");
        copied = copied.Upload45BD30(128, 256, 4, 4, new byte[32]);
        Assert.DoesNotContain(copied.LiveTextureSources!.Value, source => source.X == 128);
        NativeBattlePcTextureWindowPixels restored = JsonSerializer.Deserialize<NativeBattlePcTextureWindowPixels>(JsonSerializer.Serialize(retained))!;
        restored.Validate();
        Assert.Contains(restored.LiveTextureSources!.Value, source => source.X == 128 && source.Origin.Kind == NativeBattleTextureSourceKind.Image);
    }

    [Fact]
    public void Named_archive_delivery_retains_its_metadata_through_transaction_publication_and_rollback()
    {
        byte[] raw = new byte[0x8000];
        using NativeBattleEffectArenaTransaction transaction = new(NativeBattleEffectArena.Clear());
        NativeBattleEffectArena view = transaction.View.Write(128, raw).RememberTextureFile(128, raw, new("ma8def_p.1", NativeBattleTextureDomain.Battle));
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(128, 256, 64, 256), view, 128);
        Assert.Equal("a8def1.tim", queued.Slots[0]!.Origin!.Owner);
        NativeBattleEffectArena published = transaction.Freeze();
        Assert.Single(published.TextureFiles!.Value);
        transaction.BeginCheckpoint();
        view.Write(128, raw).RememberTextureFile(128, raw, new("ma8def_p.2", NativeBattleTextureDomain.Battle));
        Assert.Equal("a8def2.tim", NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(128, 256, 64, 256), view, 128).Slots[0]!.Origin!.Owner);
        transaction.RollbackCheckpoint();
        Assert.Equal("ma8def_p.1", Assert.Single(transaction.Freeze().TextureFiles!.Value).Identity.Name);
        Assert.Equal("a8def1.tim", queued.Drain505D20(published).Uploads.Single().Origin!.Owner);
    }

    private static NativeBattleEffectHeapAllocation Tim()
    {
        int palette = 12 + 16 * 2 * 2;
        int image = 12 + 192 * 256 * 2;
        byte[] bytes = new byte[8 + palette + image];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), palette);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), 2);
        int imageOffset = 8 + palette;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(imageOffset), image);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(imageOffset + 4), 64);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(imageOffset + 6), 256);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(imageOffset + 8), 192);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(imageOffset + 10), 256);
        return new("a8def.tim", bytes.ToImmutableArray());
    }
}
