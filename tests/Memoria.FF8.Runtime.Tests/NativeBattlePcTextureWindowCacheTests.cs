using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePcTextureWindowCacheTests
{
    [Fact]
    public void Native_reset_clears96rows_but_preserves_global_scales_and_does_not_guess_unknown_scales()
    {
        NativeBattlePcTextureWindowCache unknown = NativeBattlePcTextureWindowCache.Reset4647A0(null);
        Assert.Equal(96, unknown.Rows.Length);
        Assert.Null(unknown.ScaleU);
        Assert.Null(unknown.ScaleV);
        NativeBattlePcTextureWindowCache populated = Prepared().Resolve465CE0(0, 0, Window(8, 0, 64, 32)).Cache;
        NativeBattlePcTextureWindowCache reset = NativeBattlePcTextureWindowCache.Reset4647A0(populated);
        Assert.Equal(1f / 64, reset.ScaleU);
        Assert.Equal(1f / 32, reset.ScaleV);
        Assert.All(reset.Rows, row =>
        {
            Assert.Equal(0, row.Count);
            Assert.Equal(0u, row.UsedMask | row.AlternateMask | row.DirtyMask);
            Assert.Equal((ushort)0, row.AlternateClut);
        });
    }

    [Theory]
    [InlineData(0x0000, 0)]
    [InlineData(0x001F, 31)]
    [InlineData(0x0080, 32)]
    [InlineData(0x011F, 95)]
    public void Preprocessing_marks_the_actual_page_depth_row_and_prepare_initializes_only_its_full_page(ushort page, int index)
    {
        NativeBattlePcTextureWindowCache state = NativeBattlePcTextureWindowCache.Reset4647A0(null).Preprocess462F90(page, 0xFFFF);
        Assert.Equal(1u, state.Rows[index].UsedMask);
        Assert.Equal(0, state.Rows[index].Count);
        NativeBattlePcTextureWindowPreparation prepared = state.Prepare464BD0();
        NativeBattlePcTextureWindowUpload upload = Assert.Single(prepared.Uploads);
        Assert.Equal(index, upload.Row);
        Assert.Equal(0, upload.Slot);
        Assert.Equal(new NativeBattlePrimitiveTextureWindow(0, 0, 256, 256), upload.Rectangle);
        Assert.Equal(1, prepared.Cache.Rows[index].Count);
        Assert.True(prepared.Cache.Rows[index].Regions[0].CurrentPixels);
        Assert.Null(prepared.Cache.ScaleU);
        Assert.Single(prepared.Cache.Rows, row => row.Count != 0);
    }

    [Fact]
    public void Native_ABR2_flags_and_clut_are_separate_from_window_identity_and_flush_retains_the_last_clut()
    {
        NativeBattlePcTextureWindowCache cache = Prepared(0x40).Resolve465CE0(0x40, 0xFFFF, Window(8, 0, 64, 32)).Cache;
        Assert.Equal(3u, cache.Rows[0].UsedMask);
        Assert.Equal(3u, cache.Rows[0].AlternateMask);
        Assert.Equal((ushort)0x7FFF, cache.Rows[0].AlternateClut);
        NativeBattlePcTextureWindowCache flushed = cache.Flush465930();
        Assert.Equal(0u, flushed.Rows[0].UsedMask | flushed.Rows[0].AlternateMask);
        Assert.Equal((ushort)0x7FFF, flushed.Rows[0].AlternateClut);
        Assert.Equal(cache.Rows[0].Regions, flushed.Rows[0].Regions);
        Assert.Equal(cache.ScaleU, flushed.ScaleU);
    }

    [Fact]
    public void Seven_nonzero_windows_fill_rows1through7_and_eighth_miss_uses_full_page_with_stale_global_scales()
    {
        NativeBattlePcTextureWindowCache cache = Full().Flush465930();
        NativeBattlePcTextureWindowResolve miss = cache.Resolve465CE0(0, 0, Window(64, 0, 128, 64));
        Assert.True(miss.Selection.Fallback);
        Assert.True(miss.Selection.Windowed);
        Assert.Equal(0, miss.Selection.Slot);
        Assert.Equal(0, miss.Selection.X);
        Assert.Equal(256, miss.Selection.Width);
        Assert.Equal(256, miss.Selection.Height);
        Assert.Equal(1f / 64, miss.Selection.ScaleU);
        Assert.Equal(1f / 32, miss.Selection.ScaleV);
        Assert.Equal(8, miss.Cache.Rows[0].Count);
        Assert.Equal(0u, miss.Cache.Rows[0].UsedMask);
        Assert.Equal(32f, 8 * miss.Selection.ScaleU * miss.Selection.Width);
        NativeBattlePcTextureWindowResolve hit = miss.Cache.Resolve465CE0(0, 0, Window(8, 0, 64, 32));
        Assert.False(hit.Selection.Fallback);
        Assert.Equal(1, hit.Selection.Slot);
        Assert.Equal(8, hit.Cache.Rows[0].Count);
    }

    [Fact]
    public void Full_cache_fallback_uses_global_scale_from_another_page_and_zero_E2_does_not_reset_it()
    {
        NativeBattlePcTextureWindowCache cache = Full().Preprocess462F90(1, 0).Prepare464BD0().Cache;
        cache = cache.Resolve465CE0(1, 0, Window(0, 8, 32, 16)).Cache;
        NativeBattlePcTextureWindowResolve miss = cache.Resolve465CE0(0, 0, Window(64, 0, 128, 64));
        Assert.True(miss.Selection.Fallback);
        Assert.Equal(1f / 32, miss.Selection.ScaleU);
        Assert.Equal(1f / 16, miss.Selection.ScaleV);
        NativeBattlePcTextureWindowResolve disabled = miss.Cache.Resolve465CE0(0, 0, 0);
        Assert.False(disabled.Selection.Windowed);
        Assert.Equal(1f / 256, disabled.Selection.ScaleU);
        Assert.Equal(1f / 32, disabled.Cache.ScaleU);
        Assert.Equal(1f / 16, disabled.Cache.ScaleV);
    }

    [Fact]
    public void E2_raw24bit_identity_retains_reserved_bits_even_when_the_decoded_rectangle_matches()
    {
        uint first = Window(8, 0, 64, 32);
        NativeBattlePcTextureWindowCache cache = Prepared().Resolve465CE0(0, 0, first).Cache;
        cache = cache.Resolve465CE0(0, 0, first | 0x100000).Cache;
        Assert.Equal(3, cache.Rows[0].Count);
        Assert.NotEqual(cache.Rows[0].Regions[1].WindowBits, cache.Rows[0].Regions[2].WindowBits);
        Assert.Equal(cache.Rows[0].Regions[1].Rectangle, cache.Rows[0].Regions[2].Rectangle);
    }

    [Fact]
    public void Region_and_scale_use_the_native_clamped_non_power_of_two_dimensions()
    {
        NativeBattlePcTextureWindowSelection selection = Prepared().Resolve465CE0(0, 0, Window(8, 0, 256, 256)).Selection;
        selection.Validate();
        Assert.Equal(8, selection.X);
        Assert.Equal(248, selection.Width);
        Assert.Equal(1f / 248, selection.ScaleU);
        Assert.Throws<InvalidDataException>(() => (selection with { ScaleU = float.NaN }).Validate());
        Assert.Throws<InvalidDataException>(() => (selection with { ScaleV = 0 }).Validate());
    }

    [Fact]
    public void Inclusive_odd_column_upload_invalidates_all_three_depths_and_the_preceding_even_depth1_page_only()
    {
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null).Invalidate464850(64, 0, 127, 255);
        Assert.Equal(new[] { 1, 32, 33, 65 }, cache.Rows.Select((row, index) => (row, index)).Where(pair => pair.row.DirtyMask != 0).Select(pair => pair.index));
        Assert.All(cache.Rows.Where(row => row.DirtyMask != 0), row => Assert.Equal(uint.MaxValue, row.DirtyMask));
        cache = cache.Invalidate464850(128, 256, 128, 256);
        Assert.Equal(uint.MaxValue, cache.Rows[18].DirtyMask);
        Assert.Equal(uint.MaxValue, cache.Rows[50].DirtyMask);
        Assert.Equal(uint.MaxValue, cache.Rows[82].DirtyMask);
        Assert.Equal(0u, cache.Rows[49].DirtyMask);
    }

    [Theory]
    [InlineData(1, 1, 0, 0, true)]
    [InlineData(0, 0, -1, -1, true)]
    [InlineData(64, 1, 63, 0, false)]
    [InlineData(1, 256, 0, 255, false)]
    public void Empty_upload_endpoints_follow_native_signed_division_bucket_loops(int left, int top, int right, int bottom, bool touchesBase)
    {
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null).Invalidate464850(left, top, right, bottom);
        int[] touched = cache.Rows.Select((row, index) => (row, index)).Where(pair => pair.row.DirtyMask != 0).Select(pair => pair.index).ToArray();
        Assert.Equal(touchesBase ? new[] { 0, 32, 64 } : [], touched);
    }

    [Fact]
    public void A_new_region_copies_current_pixels_even_when_its_native_dirty_bit_was_already_set()
    {
        NativeBattlePcTextureWindowCache cache = Prepared().Invalidate464850(0, 0, 63, 255).Preprocess462F90(0, 0).Prepare464BD0().Cache;
        NativeBattlePcTextureWindowResolve created = cache.Resolve465CE0(0, 0, Window(8, 0, 64, 32));
        Assert.True(created.Cache.Rows[0].Regions[1].CurrentPixels);
        Assert.NotEqual(0u, created.Cache.Rows[0].DirtyMask & 2);
        Assert.Equal(created.Selection, created.Cache.Resolve465CE0(0, 0, Window(8, 0, 64, 32)).Selection);
    }

    [Fact]
    public void Dirty_opaque_only_cached_crop_requires_prior_pixels_because_prepare_saw_only_the_base_bit()
    {
        uint bits = Window(8, 0, 64, 32);
        NativeBattlePcTextureWindowCache cache = Prepared().Resolve465CE0(0, 0, bits).Cache.Flush465930();
        cache = cache.Invalidate464850(0, 0, 63, 255).Preprocess462F90(0, 0).Prepare464BD0().Cache;
        Assert.True(cache.Rows[0].Regions[0].CurrentPixels);
        Assert.False(cache.Rows[0].Regions[1].CurrentPixels);
        Assert.Equal(0u, cache.Rows[0].DirtyMask & 1);
        Assert.NotEqual(0u, cache.Rows[0].DirtyMask & 2);
        Assert.Throws<NotSupportedException>(() => cache.Resolve465CE0(0, 0, bits));
    }

    [Fact]
    public void Semi_pass_window_usage_survives_flush_and_refreshes_that_cached_crop_before_the_next_draw()
    {
        uint bits = Window(8, 0, 64, 32);
        NativeBattlePcTextureWindowCache cache = Prepared().Resolve465CE0(0, 0, bits).Cache.Flush465930();
        cache = cache.Resolve465CE0(0, 0, bits).Cache;
        Assert.Equal(2u, cache.Rows[0].UsedMask);
        cache = cache.Invalidate464850(0, 0, 63, 255).Preprocess462F90(0, 0);
        NativeBattlePcTextureWindowPreparation prepared = cache.Prepare464BD0();
        Assert.Equal(new[] { 0, 1 }, prepared.Uploads.Select(upload => upload.Slot));
        Assert.True(prepared.Cache.Rows[0].Regions[1].CurrentPixels);
        Assert.Equal(0u, prepared.Cache.Rows[0].DirtyMask & 3);
        Assert.False(prepared.Cache.Resolve465CE0(0, 0, bits).Selection.Fallback);
    }

    [Fact]
    public void JSON_preserves_global_scale_history_row_keys_and_pending_dirty_preparation()
    {
        NativeBattlePcTextureWindowCache cache = Full().Invalidate464850(64, 0, 64, 0).Preprocess462F90(0, 0);
        string json = JsonSerializer.Serialize(cache);
        NativeBattlePcTextureWindowCache clone = JsonSerializer.Deserialize<NativeBattlePcTextureWindowCache>(json)!;
        clone.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(clone));
        NativeBattlePcTextureWindowResolve next = cache.Prepare464BD0().Cache.Resolve465CE0(0, 0, Window(64, 0, 128, 64));
        NativeBattlePcTextureWindowResolve restored = clone.Prepare464BD0().Cache.Resolve465CE0(0, 0, Window(64, 0, 128, 64));
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Unprepared_resources_unsupported_depth_and_missing_fallback_scales_fail_at_their_required_owner()
    {
        NativeBattlePcTextureWindowCache empty = NativeBattlePcTextureWindowCache.Reset4647A0(null);
        Assert.Throws<NotSupportedException>(() => empty.Resolve465CE0(0, 0, 0));
        Assert.Throws<NotSupportedException>(() => empty.Preprocess462F90(0x180, 0));
        NativeBattlePcTextureWindowCache unknownScale = Full() with { ScaleU = null, ScaleV = null };
        Assert.Throws<NotSupportedException>(() => unknownScale.Resolve465CE0(0, 0, Window(64, 0, 128, 64)));
        Assert.Throws<InvalidDataException>(() => (empty with { ScaleU = 1f / 64 }).Validate());
    }

    private static NativeBattlePcTextureWindowCache Prepared(ushort texturePage = 0)
    {
        return NativeBattlePcTextureWindowCache.Reset4647A0(null).Preprocess462F90(texturePage, 0).Prepare464BD0().Cache;
    }

    private static NativeBattlePcTextureWindowCache Full()
    {
        NativeBattlePcTextureWindowCache cache = Prepared();
        for (int index = 1; index <= 7; index++)
        {
            cache = cache.Resolve465CE0(0, 0, Window(index * 8, 0, 64, 32)).Cache;
        }
        return cache;
    }

    private static uint Window(int x, int y, int width, int height)
    {
        return NativeBattleQuezacotlGeometryPackets.TextureWindowCommand(new((short)x, (short)y, (short)width, (short)height)) & 0xFFFFFF;
    }
}
