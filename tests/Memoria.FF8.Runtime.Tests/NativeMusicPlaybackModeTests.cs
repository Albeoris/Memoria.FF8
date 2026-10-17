using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeMusicPlaybackModeTests
{
    [Theory]
    [InlineData(3, "003-combat.wav")]
    [InlineData(6, "006-funsui.wav")]
    [InlineData(10, "010-ante.wav")]
    [InlineData(11, "011-wind.wav")]
    [InlineData(30, "030-flangchorus.wav")]
    [InlineData(31, "031-dubchorus.wav")]
    [InlineData(32, "032-solochorus.wav")]
    [InlineData(33, "033-femalechorus.wav")]
    [InlineData(34, "034-chorus.wav")]
    [InlineData(37, "037-reet.wav")]
    [InlineData(38, "038-soyo.wav")]
    [InlineData(39, "039-rouka.wav")]
    [InlineData(40, "040-night.wav")]
    [InlineData(44, "044-sea.wav")]
    [InlineData(74, "074-train.wav")]
    [InlineData(87, "087-mdmotor.wav")]
    [InlineData(94, "094-laswhite.wav")]
    [InlineData(95, "095-lasbl.wav")]
    public void Original_stream_switch_selects_Wave_before_attempting_its_single_shared_device(int id, string name)
    {
        Assert.Equal(name, NativeMusicRouting.GetStreamFileName(id));
        Assert.Equal(NativeMusicPlaybackMode.Wave, NativeMusicRouting.Mode46B530(id));
        NativeMusicPlaybackState next = NativeMusicPlaybackState.FromColdProcess().PlayEncoded46B530(1, checked((byte)(id + 1)), 300);
        Assert.Equal(NativeMusicPlaybackMode.Wave, next.ReadMode(1));
        Assert.Equal(NativeMusicPlaybackMode.Stopped, next.ReadMode(0));
        Assert.Equal(id, next.CurrentMusicId1);
        Assert.Equal(300, next.Gain1);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(43)]
    [InlineData(116)]
    [InlineData(117)]
    [InlineData(254)]
    public void Non_stream_ID_selects_Midi_even_when_unsigned_filename_lookup_will_reject_it(int id)
    {
        Assert.Null(NativeMusicRouting.GetStreamFileName(id));
        Assert.Equal(NativeMusicPlaybackMode.Midi, NativeMusicRouting.Mode46B530(id));
        NativeMusicPlaybackState initial = new(12, null, 1000, null);
        NativeMusicPlaybackState next = initial.PlayEncoded46B530(0, checked((byte)(id + 1)), -123, () => 339);
        Assert.Equal(id, next.CurrentMusicId0);
        Assert.Equal(-123, next.Gain0);
        Assert.Equal(NativeMusicPlaybackMode.Midi, next.Mode0);
        Assert.Null(next.Mode1);
    }

    [Fact]
    public void Same_ID_preserves_unknown_or_stopped_mode_without_inventing_a_device_attempt()
    {
        NativeMusicPlaybackState unknownMode = new(127, 0, 3, 0);
        Assert.Same(unknownMode, unknownMode.PlayEncoded46B530(0, 4, 300));
        Assert.Null(unknownMode.Mode0);
        Assert.Throws<NotSupportedException>(() => unknownMode.ReadMode(0));
        NativeMusicPlaybackState cold = NativeMusicPlaybackState.FromColdProcess();
        Assert.Same(cold, cold.PlayEncoded46B530(0, 1, 127));
        Assert.Equal(NativeMusicPlaybackMode.Stopped, cold.Mode0);
        NativeMusicPlaybackState songZero = cold.PlayEncoded46B530(0, 2, 127).PlayEncoded46B530(0, 1, 127);
        Assert.Equal(0, songZero.CurrentMusicId0);
        Assert.Equal(NativeMusicPlaybackMode.Midi, songZero.Mode0);
    }

    [Fact]
    public void Stop_and_concert_write_modes_only_on_the_original_reached_branches()
    {
        NativeMusicPlaybackState state = new(300, -1, 43, 6) { Mode0 = NativeMusicPlaybackMode.Midi, Mode1 = NativeMusicPlaybackMode.Wave };
        NativeMusicPlaybackState stopped = state.Stop46C060(1);
        Assert.Equal(new NativeMusicPlaybackState(300, -1, 43, 0) { Mode0 = NativeMusicPlaybackMode.Midi, Mode1 = NativeMusicPlaybackMode.Stopped }, stopped);
        Assert.Same(state, state.Concert46B990(4095, () => 0));
        NativeMusicPlaybackState concert = NativeMusicPlaybackState.Unknown().Concert46B990(4095, () => 634);
        Assert.Equal(NativeMusicPlaybackMode.Midi, concert.Mode0);
        Assert.Null(concert.Mode1);
        Assert.Null(concert.Gain0);
        Assert.Equal(43, concert.CurrentMusicId0);
        Assert.Equal(NativeMusicPlaybackMode.Stopped, state.Cross46B850("AKAO\u0001"u8, 127).Mode0);
    }

    [Fact]
    public void Legacy_JSON_retains_missing_modes_and_known_modes_roundtrip_without_cold_inference()
    {
        NativeMusicPlaybackState cold = NativeMusicPlaybackState.FromColdProcess();
        JsonObject old = JsonSerializer.SerializeToNode(cold)!.AsObject();
        Assert.True(old.Remove("Mode0"));
        Assert.True(old.Remove("Mode1"));
        NativeMusicPlaybackState restored = JsonSerializer.Deserialize<NativeMusicPlaybackState>(old.ToJsonString())!;
        restored.Validate();
        Assert.False(restored.IsColdProcess);
        Assert.Null(restored.Mode0);
        Assert.Null(restored.Mode1);
        Assert.DoesNotContain("Mode0", JsonSerializer.Serialize(restored));
        Assert.Throws<NotSupportedException>(() => restored.ReadMode(1));
        Assert.Equal(cold, JsonSerializer.Deserialize<NativeMusicPlaybackState>(JsonSerializer.Serialize(cold)));
        NativeMusicPlaybackState partial = restored.Stop46C060(1);
        Assert.Equal(partial, JsonSerializer.Deserialize<NativeMusicPlaybackState>(JsonSerializer.Serialize(partial)));
        Assert.Null(partial.Mode0);
        Assert.Equal(NativeMusicPlaybackMode.Stopped, partial.Mode1);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Invalid_known_modes_fail_the_existing_global_owner_validation(int value)
    {
        NativeMusicPlaybackState invalid = NativeMusicPlaybackState.Unknown() with { Mode0 = (NativeMusicPlaybackMode)value };
        Assert.Throws<InvalidDataException>(invalid.Validate);
        Assert.Throws<InvalidDataException>(() => (NativeGlobalSoundProcess.UnknownBufferless() with { MusicPlayback = invalid }).Validate());
    }
}
