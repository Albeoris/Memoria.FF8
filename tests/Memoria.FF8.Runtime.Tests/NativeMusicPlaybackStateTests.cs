using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeMusicPlaybackStateTests
{
    [Theory]
    [InlineData(0u, 300, null)]
    [InlineData(1u, null, 300)]
    [InlineData(2u, 300, 300)]
    [InlineData(uint.MaxValue, 300, 300)]
    public void Gain_routing_uses_unsigned_channel_and_full_signed_DWORDs(uint channel, int? gain0, int? gain1)
    {
        NativeMusicPlaybackState state = NativeMusicPlaybackState.Unknown().SetGain46BB40(channel, 300);
        Assert.Equal(gain0, state.Gain0);
        Assert.Equal(gain1, state.Gain1);
        Assert.Null(state.CurrentMusicId0);
        Assert.Null(state.CurrentMusicId1);
        NativeMusicPlaybackState negative = state.SetGain46BB40(channel, -1);
        Assert.Equal(gain0 is null ? (int?)null : -1, negative.Gain0);
        Assert.Equal(gain1 is null ? (int?)null : -1, negative.Gain1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(49)]
    [InlineData(50)]
    [InlineData(255)]
    public void Device_fade_duration_and_initial_gain_do_not_interpolate_the_target_latch(byte duration)
    {
        NativeMusicPlaybackState initial = new(-20, 99, 3, 4);
        Assert.Equal(new NativeMusicPlaybackState(-20, 300, 3, 4), initial.FadeGain46BBC0(1, duration, 300));
        Assert.Equal(new NativeMusicPlaybackState(-7, -7, 3, 4), initial.TransitionGain46BC50(uint.MaxValue, duration, 123456, -7));
    }

    [Theory]
    [InlineData(0u, 0, null)]
    [InlineData(1u, null, 0)]
    [InlineData(uint.MaxValue, 0, 0)]
    public void Stop_establishes_only_selected_IDs_and_preserves_unknown_gains(uint channel, int? id0, int? id1)
    {
        Assert.Equal(new NativeMusicPlaybackState(null, null, id0, id1), NativeMusicPlaybackState.Unknown().Stop46B800(channel));
        Assert.Equal(new NativeMusicPlaybackState(300, -1, id0, id1), new NativeMusicPlaybackState(300, -1, null, null).Stop46B800(channel));
    }

    [Fact]
    public void Same_song_preserves_gain_and_different_song_writes_supplied_gain_before_device_outcome()
    {
        NativeMusicPlaybackState initial = new(null, -40, 3, 4);
        Assert.Same(initial, initial.Play46B530(0, Akao(4), 127));
        Assert.Equal(new NativeMusicPlaybackState(300, -40, 5, 4), initial.Play46B530(0, Akao(6), 300));
        Assert.Equal(new NativeMusicPlaybackState(null, -7, 3, 6), initial.Dual46B870(Akao(7), -7));
        Assert.Throws<NotSupportedException>(() => NativeMusicPlaybackState.Unknown().Play46B530(0, Akao(4), 127));
    }

    [Theory]
    [InlineData(339, 17)]
    [InlineData(0, 99)]
    [InlineData(338, 99)]
    public void Landing_remap_uses_actual_field_only_for_the_selected_branch(ushort field, int id)
    {
        int reads = 0;
        NativeMusicPlaybackState state = NativeMusicPlaybackState.FromColdProcess().Play46B530(0, Akao(18), 127, () =>
        {
            reads++;
            return field;
        });
        Assert.Equal(id, state.CurrentMusicId0);
        Assert.Equal(1, reads);
        Assert.Throws<NotSupportedException>(() => NativeMusicPlaybackState.FromColdProcess().Play46B530(0, Akao(18), 127));
        Assert.Equal(2, NativeMusicPlaybackState.FromColdProcess().Play46B530(0, Akao(3), 127, () => throw new InvalidOperationException()).CurrentMusicId0);
    }

    [Fact]
    public void AKAO_zero_decodes_minus_one_and_cross_stops_before_the_same_ID_comparison()
    {
        NativeMusicPlaybackState state = new(77, -12, 8, 9);
        Assert.Equal(new NativeMusicPlaybackState(127, -12, -1, 9), state.Play46B530(0, Akao(0), 127));
        Assert.Equal(new NativeMusicPlaybackState(77, -12, 0, 9), state.Cross46B850(Akao(1), 127));
        Assert.Equal(new NativeMusicPlaybackState(127, -12, 8, 9), state.Cross46B850(Akao(9), 127));
        Assert.Equal(new NativeMusicPlaybackState(77, -12, 0, 9), state.Cross46B850("12345678"u8, 127));
        Assert.Equal(new NativeMusicPlaybackState(127, null, 3, null), NativeMusicPlaybackState.Unknown().Cross46B850(Akao(4), 127));
    }

    [Fact]
    public void Resource_scan_checks_only_first_A_within_eight_bytes_and_no_unreached_ID_or_gain()
    {
        NativeMusicPlaybackState unknown = NativeMusicPlaybackState.Unknown();
        Assert.Same(unknown, unknown.Play46B530(0, "12345678"u8, 127));
        Assert.Same(unknown, unknown.Play46B530(0, "AXXXAKAO\u0004"u8, 127));
        Assert.Throws<InvalidDataException>(() => unknown.Play46B530(0, "123"u8, 127));
        Assert.Throws<InvalidDataException>(() => unknown.Play46B530(0, "AKA"u8, 127));
        Assert.Throws<NotSupportedException>(() => unknown.Play46B510("12345678"u8));
        byte[] offset7 = [1, 2, 3, 4, 5, 6, 7, (byte)'A', (byte)'K', (byte)'A', (byte)'O', 4];
        Assert.Equal(3, NativeMusicPlaybackState.FromColdProcess().Play46B530(0, offset7, 127).CurrentMusicId0);
        Assert.Equal(0, NativeMusicPlaybackState.FromColdProcess().Play46B530(0, [1, 2, 3, 4, 5, 6, 7, 8, (byte)'A', (byte)'K', (byte)'A', (byte)'O', 4], 127).CurrentMusicId0);
    }

    [Fact]
    public void Plain_play_reads_retained_gain_and_unknown_partial_state_cannot_infer_it_from_IDs()
    {
        NativeMusicPlaybackState state = new(300, null, 0, null);
        Assert.Equal(new NativeMusicPlaybackState(300, null, 3, null), state.Play46B510(Akao(4)));
        Assert.Throws<NotSupportedException>(() => NativeMusicPlaybackState.Unknown().Stop46C060(0).Play46B510(Akao(4)));
        Assert.Throws<NotSupportedException>(() => state.ReadGain(-1));
        Assert.Throws<NotSupportedException>(() => state.Play46B530(2, Akao(4), 127));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    [InlineData(4096u)]
    [InlineData(0x400000u)]
    public void Concert_non_sentinel_masks_write_ID43_without_field_or_gain_reads(uint mask)
    {
        Assert.Equal(new NativeMusicPlaybackState(null, -1, 43, null), new NativeMusicPlaybackState(null, -1, null, null).Concert46B990(mask, () => throw new InvalidOperationException()));
    }

    [Fact]
    public void Concert_4095_early_return_preserves_ID_and_field634_establishes_only_ID43()
    {
        NativeMusicPlaybackState unknown = NativeMusicPlaybackState.Unknown();
        Assert.Same(unknown, unknown.Concert46B990(4095, () => 633));
        Assert.Equal(new NativeMusicPlaybackState(null, null, 43, null), unknown.Concert46B990(4095, () => 634));
        Assert.Throws<NotSupportedException>(() => unknown.Concert46B990(4095));
    }

    [Fact]
    public void Cold_factory_and_legacy_JSON_never_infer_music_from_SFX_zeroes()
    {
        NativeGlobalSoundProcess cold = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        Assert.True(cold.IsColdProcess);
        Assert.Equal(NativeMusicPlaybackState.FromColdProcess(), cold.MusicPlayback);
        Assert.Null(NativeGlobalSoundProcess.UnknownBufferless().MusicPlayback);
        Assert.DoesNotContain("MusicPlayback", JsonSerializer.Serialize(NativeGlobalSoundProcess.UnknownBufferless()));
        JsonObject legacy = JsonNode.Parse(JsonSerializer.Serialize(cold))!.AsObject();
        Assert.True(legacy.Remove("MusicPlayback"));
        NativeGlobalSoundProcess restoredLegacy = JsonSerializer.Deserialize<NativeGlobalSoundProcess>(legacy.ToJsonString())!;
        restoredLegacy.Validate();
        Assert.Null(restoredLegacy.MusicPlayback);
        Assert.False(restoredLegacy.IsColdProcess);
        Assert.DoesNotContain("MusicPlayback", JsonSerializer.Serialize(restoredLegacy));
        NativeGlobalSoundProcess partial = cold with { MusicPlayback = NativeMusicPlaybackState.Unknown().Stop46C060(1).SetGain46BB40(0, 300) };
        NativeGlobalSoundProcess restored = JsonSerializer.Deserialize<NativeGlobalSoundProcess>(JsonSerializer.Serialize(partial))!;
        Assert.Equal(new NativeMusicPlaybackState(300, null, null, 0), restored.MusicPlayback);
        Assert.False(restored.IsColdProcess);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeMusicPlaybackState>("{}"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(18)]
    [InlineData(255)]
    public void Retained_AKAO_identity_uses_the_same_comparison_core_as_the_original_resource_scan(byte encoded)
    {
        NativeMusicPlaybackState initial = new(300, -2, 3, 4);
        Assert.Equal(initial.Play46B530(0, Akao(encoded), -123, () => 74), initial.PlayEncoded46B530(0, encoded, -123, () => 74));
    }

    private static byte[] Akao(byte encoded)
    {
        return [(byte)'A', (byte)'K', (byte)'A', (byte)'O', encoded];
    }
}
