using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicSoundStop2CTests
{
    [Fact]
    public void Actual_mask_stops_voice_two_without_changing_global_sound_or_other_frame_owners()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0xE42C);
        NativeGlobalSoundProcess process = NativeGlobalSoundProcess.UnknownBufferless();
        string before = JsonSerializer.Serialize(process);
        List<byte> stopped = [];
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicSoundStop2C.Execute(frame, process, stopped.Add);
        Assert.Equal(new byte[] { 2 }, stopped);
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.Arena, result.Arena);
        Assert.Same(frame.Random, result.Random);
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Equal(before, JsonSerializer.Serialize(process));
    }

    [Fact]
    public void Nonzero_mask_selects_all_requested_voices_in_physical_order_without_identity_reads()
    {
        NativeGlobalSoundProcess process = NativeGlobalSoundProcess.UnknownBufferless();
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, NativeBattleCinematicSoundStop2C.SelectedVoices(process, 15));
        Assert.Equal(new byte[] { 2, 4 }, NativeBattleCinematicSoundStop2C.SelectedVoices(process, 10));
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicSoundStop2C.SelectedVoices(process, 16));
    }

    [Fact]
    public void Zero_mask_stops_first_zero_identity_including_voice_zero_and_preserves_later_unknowns()
    {
        NativeGlobalSoundProcess process = NativeGlobalSoundProcess.UnknownBufferless() with { OriginalEffectIds = ImmutableArray.CreateRange<uint?>(new uint?[32]).SetItem(0, 0) };
        Assert.Equal(new byte[] { 0 }, NativeBattleCinematicSoundStop2C.SelectedVoices(process, 0));
        process = process with { OriginalEffectIds = Enumerable.Repeat<uint?>(99, 32).ToImmutableArray().SetItem(29, 0).SetItem(30, null) };
        List<byte> stopped = [];
        NativeBattleWaterExecutionFrame frame = Fixture(0x2C);
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicSoundStop2C.Execute(restored, process, stopped.Add);
        Assert.Equal(new byte[] { 29 }, stopped);
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(0u, process.OriginalEffectIds[29]);
    }

    [Fact]
    public void Zero_mask_requires_earlier_identities_and_no_match_does_not_stop_or_scan_voice_thirty()
    {
        NativeGlobalSoundProcess process = NativeGlobalSoundProcess.UnknownBufferless() with { OriginalEffectIds = Enumerable.Repeat<uint?>(99, 32).ToImmutableArray().SetItem(30, 0) };
        Assert.Empty(NativeBattleCinematicSoundStop2C.SelectedVoices(process, 0));
        process = process with { OriginalEffectIds = process.OriginalEffectIds.SetItem(3, null).SetItem(4, 0) };
        List<byte> stopped = [];
        NativeBattleWaterExecutionFrame frame = Fixture(0x2C);
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicSoundStop2C.Execute(frame, process, stopped.Add));
        Assert.Empty(stopped);
        Assert.Equal(0x1000u, WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450));
    }

    [Fact]
    public void Wrong_callback_owner_or_instruction_fails_before_sound_and_pc_wraps_after_valid_stop()
    {
        NativeGlobalSoundProcess process = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        NativeBattleWaterExecutionFrame frame = Fixture(0x42C);
        List<byte> stopped = [];
        NativeBattleWaterExecutionFrame wrong = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x104);
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicSoundStop2C.Execute(wrong, process, stopped.Add));
        Assert.Throws<InvalidDataException>(() => NativeBattleCinematicSoundStop2C.Execute(frame.WriteTemporaryWord(0x14A, 0x42D), process, stopped.Add));
        Assert.Empty(stopped);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, uint.MaxValue - 1);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicSoundStop2C.Execute(frame, process, stopped.Add);
        Assert.Equal(new byte[] { 2 }, stopped);
        Assert.Equal(0u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        return WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
    }
}
