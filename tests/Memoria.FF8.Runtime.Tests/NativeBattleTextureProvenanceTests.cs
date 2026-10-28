using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTextureProvenanceTests
{
    [Fact]
    public void Named_complete_tim_publishes_separate_pixel_and_clut_origins_and_restores_them()
    {
        NativeBattleEffectHeapAllocation tim = Tim("MAG001.TIM");
        NativeBattleEffectTextureDrain drain = NativeBattleEffectTextureState.FromColdProcess().EnqueueTim505E30(tim, identity: new(tim.Identity, NativeBattleTextureDomain.Magic)).State.Drain505D20();
        Assert.Equal(new("mag001.tim", 0, NativeBattleTextureRole.Clut, 4, 16, 1) { Kind = NativeBattleTextureSourceKind.Tim, PaletteRectangle = new(0, 0, 16, 1), SourceOwner = "mag001.tim", SourceOffset = 0 }, drain.Uploads[0].Origin);
        Assert.Equal(new("mag001.tim", 0, NativeBattleTextureRole.Pixels, 4, 2, 2) { Kind = NativeBattleTextureSourceKind.Tim, PaletteRectangle = new(0, 0, 16, 1), SourceOwner = "mag001.tim", SourceOffset = 0 }, drain.Uploads[1].Origin);
        NativeBattleEffectTextureState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectTextureState>(NativeBattleTestSnapshots.Json(drain.State))!;
        restored.Validate();
        Assert.Equal(drain.State.LiveTextureSources!.Value, restored.LiveTextureSources!.Value);
        Assert.Equal(restored.LiveTextureSources, restored.Initialize500400().LiveTextureSources);
        Assert.Null(NativeBattleEffectTextureState.FromColdProcess().LiveTextureSources);
    }

    [Fact]
    public void Named_arena_and_heap_loaders_preserve_their_namespace_without_guessing_from_names()
    {
        NativeBattleEffectHeapAllocation tim = Tim("mag001.tim");
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0).LoadIntoArena(tim.Data.AsSpan(), new(tim.Identity, NativeBattleTextureDomain.Magic));
        NativeBattleEffectArenaWriter writer = new(memory.Arena!);
        writer.Write(512, [1]);
        NativeBattleEffectArena arena = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(writer.Freeze()))!;
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaTim505E30(arena, 0).State;
        NativeBattleEffectTextureDrain drained = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(queued)).Drain505D20(arena);
        Assert.Equal("mag001.tim", drained.Uploads[1].Origin!.Owner);
        Assert.Equal(NativeBattleTextureDomain.Magic, drained.Uploads[1].Origin!.Domain);
        Assert.Null(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(queued)).Drain505D20(arena.Write(64, [3, 4])).Uploads[1].Origin);
        NativeBattleEffectHeapLoad heap = memory.LoadHeap5718E0(tim);
        Assert.Equal(NativeBattleTextureDomain.Magic, heap.Memory.Allocations[0]!.TextureIdentity!.Domain);
        Assert.NotNull(NativeBattleEffectTextureState.FromColdProcess().EnqueueTim505E30(heap.Memory.Allocations[0]!).State.Drain505D20().Uploads[1].Origin);
        Assert.Null(memory.BeginLoad(1).Arena!.TextureFiles);
        Assert.Null(NativeBattleEffectTextureState.FromColdProcess().EnqueueTim505E30(tim).State.Drain505D20().Uploads[1].Origin);
    }

    [Fact]
    public void Consecutive_palette_uploads_anchor_the_pixel_source_to_their_combined_rows()
    {
        NativeBattleTextureOrigin image = new("mag222_b.00", 3, NativeBattleTextureRole.Pixels, 4, 2, 1);
        NativeBattleTextureOrigin palette = new("mag222_b.01", 3, NativeBattleTextureRole.Clut, 4, 16, 1);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess()
            .EnqueueRectangle505DF0(new(64, 256, 2, 1), new("image", [1, 2, 3, 4]), origin: image)
            .EnqueueRectangle505DF0(new(0, 32, 16, 1), new("palette0", new byte[32].ToImmutableArray()), origin: palette)
            .EnqueueRectangle505DF0(new(0, 33, 16, 1), new("palette1", new byte[32].ToImmutableArray()), origin: palette with { Member = 4 });
        NativeBattleEffectTextureDrain drained = textures.Drain505D20();
        Assert.Equal(new(0, 32, 16, 2), drained.Uploads[0].Origin!.PaletteRectangle);
        Assert.Equal(new[] { NativeBattleTextureRole.Pixels, NativeBattleTextureRole.Clut, NativeBattleTextureRole.Clut }, drained.State.LiveTextureSources!.Value.Select(region => region.Origin.Role));
        NativeBattleEffectTextureState copied = drained.State.EnqueueCopy505EB0(0, 33, 16, 1, 0, 100).Drain505D20().State;
        NativeBattleTextureSourceRegion selected = Assert.Single(copied.LiveTextureSources!.Value.Where(region => Contains(region, 0, 100)));
        Assert.Equal(new(0, 33, 16, 1), selected.Origin.PaletteRectangle);
        int originalY = selected.Origin.PaletteRectangle!.Y + selected.SourceY + 100 - selected.Y;
        Assert.Equal(1, originalY - drained.Uploads[0].Origin!.PaletteRectangle!.Y);
    }

    [Fact]
    public void Raw_selectors_keep_file_number_member_depth_and_palette_owner_independent()
    {
        byte[] tables = new byte[0x70];
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(2 * 8), 64);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(2 * 8 + 4), 1);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(2 * 8 + 6), 1);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(40 + 2 * 8 + 2), 32);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(40 + 2 * 8 + 4), 256);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(40 + 2 * 8 + 6), 1);
        tables[80 + 2] = 0x81;
        tables[88 + 2] = 0;
        NativeBattleWater222Profile profile = new(new("tables", tables.ToImmutableArray()), new("initialization", new byte[0x1C].ToImmutableArray()), new("owner", new byte[0x14].ToImmutableArray()));
        byte[] file = new byte[1024];
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(8), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(20), 48);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(32 + 2 * 4), 96);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(48 + 2 * 4), 0xAB000050);
        ImmutableArray<NativeBattleEffectHeapAllocation> files = [new("first", file.ToImmutableArray()), new("second", file.ToImmutableArray())];
        NativeBattleWater222UploadPlan pixels = NativeBattleWater222TexturePair.Resolve(profile, files, 2, false);
        NativeBattleWater222UploadPlan clut = NativeBattleWater222TexturePair.Resolve(profile, files, 2, true);
        Assert.Equal("mag221_b.01", pixels.Origin!.Owner);
        Assert.Equal("mag221_b.00", clut.Origin!.Owner);
        Assert.Equal(2, pixels.Origin.Member);
        Assert.Equal(8, pixels.Origin.BitsPerPixel);
        Assert.Equal(NativeBattleTextureRole.Clut, clut.Origin.Role);
        Assert.Equal(128, pixels.Offset);
        Assert.Equal(128, clut.Offset);
    }

    [Fact]
    public void Unknown_tim_identity_and_nonzero_member_offset_do_not_guess_a_filename_ordinal()
    {
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess();
        Assert.Null(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(textures)).EnqueueTim505E30(Tim("20DFAB8+80")).State.Drain505D20().State.LiveTextureSources);
        NativeBattleEffectHeapAllocation embedded = new("mag001.tim", new byte[8].Concat(Tim("ignored").Data).ToImmutableArray());
        Assert.Null(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(textures)).EnqueueTim505E30(embedded, 8).State.Drain505D20().State.LiveTextureSources);
    }

    [Fact]
    public void Mutable_raw_upload_uses_current_words_and_drops_origin_when_the_source_changes()
    {
        NativeBattleTextureOrigin origin = new("mag222_b.01", 2, NativeBattleTextureRole.Pixels, 8, 2, 1);
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(128, [1, 2, 3, 4]);
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(64, 256, 2, 1), arena, 128, origin);
        queued = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectTextureState>(NativeBattleTestSnapshots.Json(queued))!;
        NativeBattleEffectTextureDrain valid = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(queued)).Drain505D20(arena);
        Assert.Equal(origin, valid.Uploads.Single().Origin);
        NativeBattleEffectTextureDrain changed = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(queued)).Drain505D20(arena.Write(128, [5, 6, 7, 8]));
        Assert.Null(changed.Uploads.Single().Origin);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, changed.Uploads.Single().Bytes);
    }

    [Fact]
    public void Unknown_mutable_payload_stays_deferred_and_does_not_gain_an_unproved_origin()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(128, 4);
        NativeBattleTextureOrigin origin = new("mag222_b.01", 2, NativeBattleTextureRole.Pixels, 8, 2, 1);
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueArenaRectangle505DF0(new(64, 256, 2, 1), arena, 128, origin);
        Assert.Null(queued.Slots[0]!.Origin);
        NativeBattleEffectTextureDrain drain = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(queued)).Drain505D20(arena.Write(128, [1, 2, 3, 4]));
        Assert.Null(drain.Uploads.Single().Origin);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, drain.Uploads.Single().Bytes);
    }

    [Fact]
    public void Unknown_overwrites_clip_the_surviving_canvas_and_copies_preserve_source_coordinates()
    {
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().EnqueueTim505E30(Tim("mag001.tim"), identity: new("mag001.tim", NativeBattleTextureDomain.Magic)).State.Drain505D20().State;
        textures = textures.Upload45BD30(64, 256, 1, 1, [0, 0]);
        NativeBattleTextureSourceRegion[] pixels = textures.LiveTextureSources!.Value.Where(region => region.Origin.Role == NativeBattleTextureRole.Pixels).ToArray();
        Assert.Equal(2, pixels.Length);
        Assert.DoesNotContain(pixels, region => Contains(region, 64, 256));
        textures = textures.EnqueueCopy505EB0(64, 256, 2, 2, 128, 128).Drain505D20().State;
        NativeBattleTextureSourceRegion corner = Assert.Single(textures.LiveTextureSources!.Value.Where(region => Contains(region, 129, 128)));
        Assert.Equal((1, 0), (corner.SourceXWords, corner.SourceY));
        Assert.DoesNotContain(textures.LiveTextureSources.Value, region => Contains(region, 128, 128));
        textures.Validate();
    }

    [Fact]
    public void Overlapping_copy_follows_forward_dword_groups_and_vertical_propagation()
    {
        NativeBattleTextureOrigin origin = new("mag001_b.00", 0, NativeBattleTextureRole.Pixels, 4, 8, 1);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().EnqueueRectangle505DF0(new(64, 256, 8, 1), new("payload", Enumerable.Range(0, 16).Select(value => (byte)value).ToImmutableArray()), origin: origin).Drain505D20().State;
        textures = textures.EnqueueCopy505EB0(64, 256, 6, 1, 65, 256).Drain505D20().State;
        int[] expected = [0, 0, 1, 1, 3, 3, 5, 7];
        for (int x = 0; x < expected.Length; x++)
        {
            NativeBattleTextureSourceRegion source = Assert.Single(textures.LiveTextureSources!.Value.Where(region => Contains(region, 64 + x, 256)));
            Assert.Equal(expected[x], source.SourceXWords + 64 + x - source.X);
        }
        textures = textures.EnqueueCopy505EB0(64, 256, 8, 3, 64, 257).Drain505D20().State;
        Assert.All(Enumerable.Range(257, 3), y => Assert.Contains(textures.LiveTextureSources!.Value, region => Contains(region, 64, y)));
        textures.Validate();
    }

    [Fact]
    public void Cached_window_preserves_pixel_origins_after_live_vram_overwrite()
    {
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().EnqueueTim505E30(Tim("mag001.tim"), identity: new("mag001.tim", NativeBattleTextureDomain.Magic)).State.Drain505D20().State;
        NativeBattlePcTextureWindowPixels retained = NativeBattlePcTextureWindowPixels.Capture(17, new(0, 0, 8, 2), textures);
        NativeBattleEffectTextureState overwritten = textures.Upload45BD30(64, 256, 2, 2, new byte[8]);
        Assert.Contains(retained.LiveTextureSources!.Value, region => region.Origin.Role == NativeBattleTextureRole.Pixels);
        Assert.DoesNotContain(overwritten.LiveTextureSources!.Value, region => region.Origin.Role == NativeBattleTextureRole.Pixels);
        Assert.Single(retained.TextureRegistrations!.Value);
        Assert.DoesNotContain(overwritten.TextureRegistrations!.Value, registration => !registration.OwnedSpans.IsEmpty);
        NativeBattlePcTextureWindowPixels restored = NativeBattleTestSnapshots.RestoreJson<NativeBattlePcTextureWindowPixels>(NativeBattleTestSnapshots.Json(retained))!;
        restored.Validate();
        Assert.Equal(retained.LiveTextureSources.Value, restored.LiveTextureSources!.Value);
        Assert.Equal(NativeBattleTestSnapshots.Json(retained.TextureRegistrations.Value), NativeBattleTestSnapshots.Json(restored.TextureRegistrations!.Value));
    }

    [Fact]
    public void Self_copy_preserves_source_coordinates_without_per_word_metadata_allocation()
    {
        NativeBattleTextureOrigin origin = new("effect.tim", 0, NativeBattleTextureRole.Pixels, 8, 256, 128);
        ImmutableArray<NativeBattleTextureSourceRegion> regions = [new(origin, 64, 32, 256, 128)];
        NativeBattleEffectVramCopy copy = new(64, 32, 256, 128, 64, 32);
        NativeBattleTextureProvenance.Copy(regions, copy);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 100; index++)
        {
            NativeBattleTextureProvenance.Copy(regions, copy);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(regions, NativeBattleTextureProvenance.Copy(regions, copy));
    }

    [Fact]
    public void Self_copy_preserves_physical_publication_and_still_records_its_observed_frame()
    {
        NativeBattleTextureOrigin origin = new("effect.tim", 0, NativeBattleTextureRole.Pixels, 8, 2, 1);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(64, 32, 2, 1, [1, 2, 3, 4]) with
        {
            LiveTextureSources = [new(origin, 64, 32, 2, 1)],
            TextureRegistrations = [new(origin, 64, 32, 2, 1, []) { PaletteOwnedSpans = [], CopyHistoryKnown = true }]
        };
        NativeBattleEffectTextureDrain copied = state.EnqueueCopy505EB0(64, 32, 2, 1, 64, 32).Drain505D20();
        Assert.Same(state.VramPublicationIdentity, copied.State.VramPublicationIdentity);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, Assert.Single(copied.Uploads).Bytes);
        Assert.Equal((ushort)0x0201, copied.State.ReadVramWord(64, 32));
        Assert.Equal((ushort)0x0403, copied.State.ReadVramWord(65, 32));
        Assert.Equal(state.LiveTextureSources, copied.State.LiveTextureSources);
        Assert.True(Assert.Single(copied.Uploads).Copied);
        NativeBattleTextureRegistration frame = Assert.Single(copied.State.TextureRegistrations!.Value);
        Assert.Equal(0, frame.CurrentFrame);
        Assert.Equal(new(64, 32, 2, 1), Assert.Single(frame.CopyFrames));
    }

    [Theory]
    [InlineData(1, 0, 12)]
    [InlineData(-1, 0, 12)]
    [InlineData(0, 1, 12)]
    [InlineData(0, -1, 12)]
    [InlineData(1, 1, 11)]
    [InlineData(-1, -1, 11)]
    [InlineData(3, 0, 11)]
    public void Overlapping_provenance_matches_physical_forward_word_transfers_including_unknown_holes(int dx, int dy, short width)
    {
        NativeBattleTextureOrigin first = new("first.tim", 0, NativeBattleTextureRole.Pixels, 8, 8, 10);
        NativeBattleTextureOrigin second = new("second.tim", 0, NativeBattleTextureRole.Pixels, 8, 10, 10);
        ImmutableArray<NativeBattleTextureSourceRegion> sources = [new(first, 64, 31, 8, 10), new(second, 74, 31, 10, 10)];
        NativeBattleEffectVramCopy copy = new(66, 33, width, 5, checked((short)(66 + dx)), checked((short)(33 + dy)));
        (NativeBattleTextureOrigin Origin, int X, int Y)?[,] physical = new (NativeBattleTextureOrigin, int, int)?[32, 16];
        foreach (NativeBattleTextureSourceRegion source in sources)
        {
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.WidthWords; x++)
                {
                    physical[source.X + x - 60, source.Y + y - 30] = (source.Origin, source.SourceXWords + x, source.SourceY + y);
                }
            }
        }
        for (int row = 0; row < copy.Height; row++)
        {
            for (int column = 0; column < copy.Width; column += 2)
            {
                var a = physical[copy.SourceX + column - 60, copy.SourceY + row - 30];
                var b = column + 1 < copy.Width ? physical[copy.SourceX + column + 1 - 60, copy.SourceY + row - 30] : null;
                physical[copy.DestinationX + column - 60, copy.DestinationY + row - 30] = a;
                if (column + 1 < copy.Width)
                {
                    physical[copy.DestinationX + column + 1 - 60, copy.DestinationY + row - 30] = b;
                }
            }
        }
        ImmutableArray<NativeBattleTextureSourceRegion> copied = NativeBattleTextureProvenance.Copy(sources, copy)!.Value;
        foreach (NativeBattleTextureSourceRegion source in copied)
        {
            source.Validate();
        }
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                NativeBattleTextureSourceRegion? source = copied.FirstOrDefault(region => Contains(region, x + 60, y + 30));
                (NativeBattleTextureOrigin Origin, int X, int Y)? actual = source is null ? null : (source.Origin, source.SourceXWords + x + 60 - source.X, source.SourceY + y + 30 - source.Y);
                Assert.Equal(physical[x, y], actual);
            }
        }
    }

    [Fact]
    public void Horizontal_overlap_coalesces_equal_row_mappings_without_fragmenting_every_source_word()
    {
        NativeBattleTextureOrigin origin = new("effect.tim", 0, NativeBattleTextureRole.Pixels, 8, 300, 128);
        ImmutableArray<NativeBattleTextureSourceRegion> sources = [new(origin, 64, 32, 300, 128)];
        NativeBattleEffectVramCopy copy = new(64, 32, 256, 128, 66, 32);
        ImmutableArray<NativeBattleTextureSourceRegion> copied = NativeBattleTextureProvenance.Copy(sources, copy)!.Value;
        NativeBattleTextureSourceRegion[] transferred = copied.Where(region => region.X >= 66 && region.X < 322).ToArray();
        Assert.Equal(128, transferred.Length);
        Assert.All(transferred, region =>
        {
            Assert.Equal(2, region.WidthWords);
            Assert.Equal(128, region.Height);
            Assert.Equal(0, region.SourceXWords);
            Assert.Equal(0, region.SourceY);
            region.Validate();
        });
        Assert.Equal(130, copied.Length);
    }

    private static bool Contains(NativeBattleTextureSourceRegion region, int x, int y) => x >= region.X && y >= region.Y && x < region.X + region.WidthWords && y < region.Y + region.Height;

    private static NativeBattleEffectHeapAllocation Tim(string name)
    {
        byte[] bytes = new byte[72];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 44);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(52), 20);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(56), 64);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(58), 256);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(60), 2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(62), 2);
        return new(name, bytes.ToImmutableArray());
    }
}
