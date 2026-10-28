using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTextureCopyHistoryTests
{
    [Fact]
    public void Reobserving_current_frame_reuses_both_owners_and_collection_without_allocating()
    {
        var owners = Upload(null, "source.tim", 64, 256, 32, 16);
        owners = Upload(owners, "target.tim", 128, 256, 32, 16);
        NativeBattleEffectVramCopy copy = new(72, 256, 8, 4, 128, 256);
        owners = NativeBattleTextureCopyHistory.Copy(owners, copy);
        ImmutableArray<NativeBattleTextureRegistration> retained = owners!.Value;
        long allocated = 0;
        ImmutableArray<NativeBattleTextureRegistration>? measuredOwners = null;
        ExceptionDispatchInfo? failure = null;
        Thread measurement = new(() =>
        {
            try
            {
                (allocated, measuredOwners) = MeasureRepeatedCopies(owners, copy);
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });
        using (ExecutionContext.SuppressFlow())
        {
            measurement.Start();
        }
        measurement.Join();
        failure?.Throw();
        owners = measuredOwners;
        Assert.Equal(0, allocated);
        Assert.True(retained.Equals(owners!.Value));
        Assert.Same(retained[0], owners.Value[0]);
        Assert.Same(retained[1], owners.Value[1]);
        var changed = NativeBattleTextureCopyHistory.Copy(owners, new(80, 256, 8, 4, 128, 256));
        Assert.False(retained.Equals(changed!.Value));
        Assert.All(changed.Value, owner => Assert.Equal(1, owner.CurrentFrame));
        Assert.All(changed.Value, owner => Assert.Equal(2, owner.CopyFrames.Length));
        Assert.All(retained, owner => Assert.Equal(0, owner.CurrentFrame));
    }

    [Fact]
    public void Repeated_palette_frame_keeps_identity_but_a_distinct_palette_frame_updates_both_owners()
    {
        NativeBattleTextureOrigin source = new("mag012.tim", 0, NativeBattleTextureRole.Pixels, 4, 2, 1) { Kind = NativeBattleTextureSourceKind.Tim, PaletteRectangle = new(0, 32, 16, 2) };
        NativeBattleTextureOrigin target = new("mag013.tim", 0, NativeBattleTextureRole.Pixels, 4, 2, 1) { Kind = NativeBattleTextureSourceKind.Tim, PaletteRectangle = new(16, 32, 16, 2) };
        var owners = NativeBattleTextureCopyHistory.Upload(null, 64, 256, 2, 1, source);
        owners = NativeBattleTextureCopyHistory.Upload(owners, 128, 256, 2, 1, target);
        NativeBattleEffectVramCopy copy = new(0, 32, 16, 1, 16, 32);
        owners = NativeBattleTextureCopyHistory.Copy(owners, copy);
        ImmutableArray<NativeBattleTextureRegistration> retained = owners!.Value;
        owners = NativeBattleTextureCopyHistory.Copy(owners, copy);
        Assert.True(retained.Equals(owners!.Value));
        var changed = NativeBattleTextureCopyHistory.Copy(owners, new(0, 33, 16, 1, 16, 32));
        Assert.False(retained.Equals(changed!.Value));
        Assert.All(changed.Value, owner => Assert.Equal(1, owner.CurrentFrame));
        Assert.All(changed.Value, owner => Assert.Equal(2, owner.CopyFrames.Length));
    }

    [Fact]
    public void Same_owner_observes_distinct_source_rectangles_in_execution_order_and_reuses_indices()
    {
        var owners = Upload(null, "first.tim", 64, 256, 32, 16);
        Assert.Equal(-1, Assert.Single(owners!.Value).CurrentFrame);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(72, 256, 8, 4, 64, 256));
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(64, 256, 8, 4, 80, 260));
        NativeBattleTextureRegistration owner = Assert.Single(owners!.Value);
        Assert.Equal(1, owner.CurrentFrame);
        Assert.Equal(new NativeBattleEffectTextureRectangle[] { new(72, 256, 8, 4), new(64, 256, 8, 4) }, owner.CopyFrames);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(72, 256, 8, 4, 88, 264));
        owner = Assert.Single(owners!.Value);
        Assert.Equal(0, owner.CurrentFrame);
        Assert.Equal(2, owner.CopyFrames.Length);
        Assert.Equal(64 + 256 * 1024, owner.OwnerId);
    }

    [Fact]
    public void Distinct_source_and_target_keep_independent_first_seen_orders()
    {
        var owners = Upload(null, "source.tim", 64, 256, 32, 16);
        owners = Upload(owners, "target.tim", 128, 256, 32, 16);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(128, 256, 8, 4, 144, 256));
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(72, 256, 8, 4, 128, 256));
        NativeBattleTextureRegistration source = NativeBattleTextureCopyHistory.Find(owners, 64, 256)!;
        NativeBattleTextureRegistration target = NativeBattleTextureCopyHistory.Find(owners, 128, 256)!;
        Assert.Equal(0, source.CurrentFrame);
        Assert.Equal(1, target.CurrentFrame);
        Assert.Equal(source.CopyFrames[0], target.CopyFrames[1]);
        Assert.Equal(new(128, 256, 8, 4), target.CopyFrames[0]);
    }

    [Fact]
    public void Missing_source_does_not_animate_target_and_missing_target_still_animates_source()
    {
        var owners = Upload(null, "known.tim", 64, 256, 32, 16);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(0, 0, 8, 4, 64, 256));
        Assert.Equal(-1, Assert.Single(owners!.Value).CurrentFrame);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(64, 256, 8, 4, 0, 0));
        Assert.Equal(0, Assert.Single(owners!.Value).CurrentFrame);
        Assert.Null(NativeBattleTextureCopyHistory.Find(owners, 0, 0));
    }

    [Fact]
    public void Source_width_and_height_belong_to_frame_identity_but_destination_does_not()
    {
        var owners = Upload(null, "known.tim", 64, 256, 32, 16);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(64, 256, 4, 2, 80, 256));
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(64, 256, 8, 2, 80, 256));
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(64, 256, 8, 4, 80, 256));
        Assert.Equal(2, Assert.Single(owners!.Value).CurrentFrame);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(64, 256, 4, 2, 88, 260));
        Assert.Equal(0, Assert.Single(owners!.Value).CurrentFrame);
        Assert.Equal(3, owners.Value[0].CopyFrames.Length);
    }

    [Fact]
    public void Pixel_reupload_resets_identical_owner_and_overlap_invalidates_entire_old_registration()
    {
        var owners = Upload(null, "first.tim", 64, 256, 32, 16);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(64, 256, 8, 4, 72, 256));
        owners = Upload(owners, "first.tim", 64, 256, 32, 16);
        NativeBattleTextureRegistration reuploaded = Assert.Single(owners!.Value);
        Assert.Equal(-1, reuploaded.CurrentFrame);
        Assert.Empty(reuploaded.CopyFrames);
        owners = Upload(owners, "second.tim", 72, 260, 8, 4);
        Assert.Equal("second.tim", Assert.Single(owners!.Value).Origin.Owner);
        Assert.Null(NativeBattleTextureCopyHistory.Find(owners, 64, 256));
    }

    [Fact]
    public void Unknown_history_stays_unknown_until_a_known_upload_and_unknown_overwrite_removes_owner()
    {
        Assert.Null(NativeBattleTextureCopyHistory.Copy(null, new(64, 256, 8, 4, 72, 256)));
        Assert.Null(NativeBattleTextureCopyHistory.Upload(null, 64, 256, 32, 16));
        var owners = Upload(null, "known.tim", 64, 256, 32, 16);
        owners = NativeBattleTextureCopyHistory.Upload(owners, 72, 260, 1, 1);
        Assert.Empty(owners!.Value);
        Assert.Null(NativeBattleTextureCopyHistory.Find(owners, 64, 256));
    }

    [Theory]
    [InlineData(95, 260, 4, 4)]
    [InlineData(72, 271, 4, 4)]
    public void Final_word_column_or_row_retains_old_history_but_assigns_overlap_to_new_owner(int x, int y, int width, int height)
    {
        var owners = Upload(null, "first.tim", 64, 256, 32, 16);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(64, 256, 8, 4, 72, 256));
        owners = Upload(owners, "edge.tim", x, y, width, height);
        Assert.Equal(2, owners!.Value.Length);
        NativeBattleTextureRegistration old = NativeBattleTextureCopyHistory.Find(owners, 64, 256)!;
        Assert.Equal("first.tim", old.Origin.Owner);
        Assert.Equal(0, old.CurrentFrame);
        Assert.Equal("edge.tim", NativeBattleTextureCopyHistory.Find(owners, x, y)!.Origin.Owner);
        Assert.DoesNotContain(old.OwnedSpans, span => x >= span.X && x < span.X + span.WidthWords && y >= span.Y && y < span.Y + span.Height);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new((short)x, (short)y, 1, 1, 64, 256));
        Assert.Equal(0, NativeBattleTextureCopyHistory.Find(owners, x, y)!.CurrentFrame);
        Assert.Equal(1, NativeBattleTextureCopyHistory.Find(owners, 64, 256)!.CurrentFrame);
    }

    [Fact]
    public void Later_cleanup_of_retained_owner_clears_its_original_rectangle_from_other_owners()
    {
        var owners = Upload(null, "first.tim", 64, 256, 32, 16);
        owners = Upload(owners, "edge.tim", 95, 260, 4, 4);
        owners = Upload(owners, "third.tim", 64, 256, 1, 1);
        Assert.Null(NativeBattleTextureCopyHistory.Find(owners, 95, 260));
        Assert.Equal("edge.tim", NativeBattleTextureCopyHistory.Find(owners, 96, 260)!.Origin.Owner);
        Assert.Equal("third.tim", NativeBattleTextureCopyHistory.Find(owners, 64, 256)!.Origin.Owner);
        Assert.DoesNotContain(owners!.Value, owner => owner.Origin.Owner == "first.tim");
    }

    [Fact]
    public void Small_rectangles_do_not_receive_the_edge_retention_exception()
    {
        var owners = Upload(null, "small.tim", 64, 256, 8, 8);
        owners = Upload(owners, "edge.tim", 71, 260, 4, 4);
        Assert.Equal("edge.tim", Assert.Single(owners!.Value).Origin.Owner);
    }

    [Fact]
    public void Common_image_registers_three_independent_virtual_owners_and_raw_page_reupload_resets_one()
    {
        NativeBattleTextureOrigin common = new("a8def.tim", 0, NativeBattleTextureRole.Pixels, 4, 192, 256) { Domain = NativeBattleTextureDomain.Battle, Kind = NativeBattleTextureSourceKind.Tim, PaletteRectangle = new(0, 224, 16, 16) };
        var owners = NativeBattleTextureCopyHistory.Upload(null, 64, 256, 192, 256, common);
        Assert.Equal(new[] { "a8def0.tim", "a8def1.tim", "a8def2.tim" }, owners!.Value.Select(owner => owner.Origin.Owner));
        Assert.Equal(new[] { 64, 128, 192 }, owners.Value.Select(owner => owner.X));
        Assert.All(owners.Value, owner => Assert.Equal(common.PaletteRectangle, owner.Origin.PaletteRectangle));
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(64, 256, 8, 4, 128, 256));
        NativeBattleTextureRegistration first = owners!.Value[0];
        NativeBattleTextureRegistration second = owners.Value[1];
        owners = NativeBattleTextureCopyHistory.Upload(owners, second.X, second.Y, second.WidthWords, second.Height, second.Origin);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(NativeBattleTextureCopyHistory.Find(owners, 64, 256)));
        Assert.Equal(-1, NativeBattleTextureCopyHistory.Find(owners, 128, 256)!.CurrentFrame);
    }

    [Fact]
    public void Snapshot_round_trip_preserves_observed_order_for_the_next_copy()
    {
        var owners = Upload(null, "source.tim", 64, 256, 32, 16);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(72, 256, 8, 4, 64, 256));
        var restored = JsonSerializer.Deserialize<ImmutableArray<NativeBattleTextureRegistration>>(JsonSerializer.Serialize(owners!.Value));
        var next = NativeBattleTextureCopyHistory.Copy(restored, new(64, 256, 8, 4, 72, 256));
        Assert.Equal(1, Assert.Single(next!.Value).CurrentFrame);
        Assert.Equal(owners.Value[0].CopyFrames[0], next.Value[0].CopyFrames[0]);
    }

    [Fact]
    public void Invalid_metadata_fails_before_unknown_shortcuts()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleTextureCopyHistory.Copy(null, new(-1, 0, 1, 1, 0, 0)));
        Assert.Throws<InvalidDataException>(() => NativeBattleTextureCopyHistory.Upload(null, 1024, 0, 1, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleTextureCopyHistory.Upload(default(ImmutableArray<NativeBattleTextureRegistration>), 0, 0, 0, 0));
        var owners = Upload(null, "known.tim", 64, 256, 32, 16)!.Value;
        Assert.Throws<InvalidDataException>(() => NativeBattleTextureCopyHistory.Validate([owners[0] with { CurrentFrame = 0 }]));
        Assert.Throws<InvalidDataException>(() => NativeBattleTextureCopyHistory.Validate([owners[0], owners[0]]));
        Assert.Throws<InvalidDataException>(() => NativeBattleTextureCopyHistory.Validate([owners[0], owners[0] with { X = 65, OwnedSpans = [new(65, 256, 31, 16)] }]));
    }

    [Fact]
    public void Palette_copies_and_pixel_copies_share_one_first_seen_frame_sequence()
    {
        NativeBattleTextureOrigin origin = new("mag012.tim", 0, NativeBattleTextureRole.Pixels, 4, 8, 8) { Kind = NativeBattleTextureSourceKind.Tim, PaletteRectangle = new(0, 32, 16, 2) };
        var owners = NativeBattleTextureCopyHistory.Upload(null, 64, 256, 8, 8, origin);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(0, 32, 16, 1, 0, 33));
        Assert.Equal(0, Assert.Single(owners!.Value).ExactFrame);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(64, 256, 1, 1, 65, 256));
        NativeBattleTextureRegistration owner = Assert.Single(owners!.Value);
        Assert.Equal(1, owner.ExactFrame);
        Assert.Equal(new NativeBattleEffectTextureRectangle[] { new(0, 32, 16, 1), new(64, 256, 1, 1) }, owner.CopyFrames);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(0, 32, 16, 1, 0, 33));
        Assert.Equal(0, Assert.Single(owners!.Value).ExactFrame);
    }

    [Theory]
    [InlineData(0, 32, 128, 256, -1, -1)]
    [InlineData(64, 256, 16, 32, 0, -1)]
    [InlineData(0, 32, 16, 32, 0, 0)]
    [InlineData(0, 32, 32, 100, -1, -1)]
    [InlineData(64, 256, 32, 100, 0, -1)]
    public void Mixed_role_copies_follow_the_flagged_native_owner_lookup(short sourceX, short sourceY, short targetX, short targetY, int sourceFrame, int targetFrame)
    {
        NativeBattleTextureOrigin source = new("mag012.tim", 0, NativeBattleTextureRole.Pixels, 4, 2, 1) { Kind = NativeBattleTextureSourceKind.Tim, PaletteRectangle = new(0, 32, 16, 1) };
        NativeBattleTextureOrigin target = new("mag013.tim", 0, NativeBattleTextureRole.Pixels, 4, 2, 1) { Kind = NativeBattleTextureSourceKind.Tim, PaletteRectangle = new(16, 32, 16, 1) };
        var owners = NativeBattleTextureCopyHistory.Upload(null, 64, 256, 2, 1, source);
        owners = NativeBattleTextureCopyHistory.Upload(owners, 128, 256, 2, 1, target);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(sourceX, sourceY, 1, 1, targetX, targetY));
        Assert.Equal(sourceFrame, NativeBattleTextureCopyHistory.Find(owners, 64, 256)!.ExactFrame);
        Assert.Equal(targetFrame, NativeBattleTextureCopyHistory.Find(owners, 128, 256)!.ExactFrame);
    }

    [Fact]
    public void Shared_palette_words_belong_only_to_the_last_registered_image_owner()
    {
        NativeBattleTextureOrigin common = new("a8def.tim", 0, NativeBattleTextureRole.Pixels, 4, 192, 256) { Domain = NativeBattleTextureDomain.Battle, Kind = NativeBattleTextureSourceKind.Tim, PaletteRectangle = new(0, 224, 16, 16) };
        var owners = NativeBattleTextureCopyHistory.Upload(null, 64, 256, 192, 256, common);
        Assert.Empty(owners!.Value[0].PaletteOwnedSpans!.Value);
        Assert.Empty(owners.Value[1].PaletteOwnedSpans!.Value);
        Assert.Equal(new NativeBattleTextureOwnedSpan[] { new(0, 224, 16, 16) }, owners.Value[2].PaletteOwnedSpans!.Value);
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(0, 225, 16, 1, 0, 224));
        Assert.Equal(new[] { -1, -1, 0 }, owners!.Value.Select(owner => owner.ExactFrame));
    }

    [Fact]
    public void Palette_registration_overwrites_word_roles_in_the_same_map_as_image_ownership()
    {
        NativeBattleTextureOrigin first = new("mag012.tim", 0, NativeBattleTextureRole.Pixels, 4, 8, 8) { Kind = NativeBattleTextureSourceKind.Tim };
        NativeBattleTextureOrigin second = new("mag013.tim", 0, NativeBattleTextureRole.Pixels, 4, 8, 8) { Kind = NativeBattleTextureSourceKind.Tim, PaletteRectangle = new(64, 256, 16, 1) };
        var owners = NativeBattleTextureCopyHistory.Upload(null, 64, 256, 8, 8, first);
        owners = NativeBattleTextureCopyHistory.Upload(owners, 128, 256, 8, 8, second);
        Assert.Null(NativeBattleTextureCopyHistory.Find(owners, 64, 256));
        Assert.Null(NativeBattleTextureCopyHistory.Find(owners, 64, 257));
        Assert.Equal("mag013.tim", Assert.Single(owners!.Value).Origin.Owner);
    }

    [Fact]
    public void Retired_image_palette_tags_still_allow_a_known_palette_source_to_advance()
    {
        NativeBattleTextureOrigin source = new("mag012.tim", 0, NativeBattleTextureRole.Pixels, 4, 2, 1) { Kind = NativeBattleTextureSourceKind.Tim, PaletteRectangle = new(0, 32, 16, 1) };
        NativeBattleTextureOrigin target = new("mag013.tim", 0, NativeBattleTextureRole.Pixels, 4, 2, 1) { Kind = NativeBattleTextureSourceKind.Tim, PaletteRectangle = new(16, 32, 16, 1) };
        var owners = NativeBattleTextureCopyHistory.Upload(null, 64, 256, 2, 1, source);
        owners = NativeBattleTextureCopyHistory.Upload(owners, 128, 256, 2, 1, target);
        owners = NativeBattleTextureCopyHistory.Upload(owners, 128, 256, 1, 1);
        Assert.Null(NativeBattleTextureCopyHistory.Find(owners, 128, 256));
        owners = NativeBattleTextureCopyHistory.Copy(owners, new(0, 32, 1, 1, 16, 32));
        Assert.Equal(0, NativeBattleTextureCopyHistory.Find(owners, 64, 256)!.ExactFrame);
        Assert.Equal(-1, Assert.Single(owners!.Value.Where(owner => !owner.Active)).CurrentFrame);
    }

    [Fact]
    public void Old_missing_palette_metadata_does_not_invent_an_exact_frame_history()
    {
        NativeBattleTextureOrigin origin = new("mag012.tim", 0, NativeBattleTextureRole.Pixels, 4, 2, 1);
        NativeBattleTextureRegistration old = new(origin, 64, 256, 2, 1, [new(64, 256, 1, 1)], 0);
        Assert.Null(old.PaletteOwnedSpans);
        Assert.Equal(-1, old.ExactFrame);
        var copied = NativeBattleTextureCopyHistory.Copy([old], new(65, 256, 1, 1, 64, 256));
        Assert.Equal(-1, Assert.Single(copied!.Value).ExactFrame);
        var reloaded = NativeBattleTextureCopyHistory.Upload(copied, 64, 256, 2, 1, origin);
        Assert.NotNull(Assert.Single(reloaded!.Value).PaletteOwnedSpans);
        reloaded = NativeBattleTextureCopyHistory.Copy(reloaded, new(65, 256, 1, 1, 64, 256));
        Assert.Equal(0, Assert.Single(reloaded!.Value).ExactFrame);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long Allocated, ImmutableArray<NativeBattleTextureRegistration>? Owners) MeasureRepeatedCopies(ImmutableArray<NativeBattleTextureRegistration>? owners, NativeBattleEffectVramCopy copy)
    {
        _ = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            owners = NativeBattleTextureCopyHistory.Copy(owners, copy);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            owners = NativeBattleTextureCopyHistory.Copy(owners, copy);
        }
        return (GC.GetAllocatedBytesForCurrentThread() - before, owners);
    }

    private static ImmutableArray<NativeBattleTextureRegistration>? Upload(ImmutableArray<NativeBattleTextureRegistration>? owners, string name, int x, int y, int width, int height)
    {
        NativeBattleTextureOrigin origin = new(name, 0, NativeBattleTextureRole.Pixels, 4, width, height) { Kind = NativeBattleTextureSourceKind.Tim };
        return NativeBattleTextureCopyHistory.Upload(owners, x, y, width, height, origin);
    }
}
