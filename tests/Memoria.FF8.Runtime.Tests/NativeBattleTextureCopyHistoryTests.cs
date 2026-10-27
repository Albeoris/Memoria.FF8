using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTextureCopyHistoryTests
{
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

    private static ImmutableArray<NativeBattleTextureRegistration>? Upload(ImmutableArray<NativeBattleTextureRegistration>? owners, string name, int x, int y, int width, int height)
    {
        NativeBattleTextureOrigin origin = new(name, 0, NativeBattleTextureRole.Pixels, 4, width, height) { Kind = NativeBattleTextureSourceKind.Tim };
        return NativeBattleTextureCopyHistory.Upload(owners, x, y, width, height, origin);
    }
}
