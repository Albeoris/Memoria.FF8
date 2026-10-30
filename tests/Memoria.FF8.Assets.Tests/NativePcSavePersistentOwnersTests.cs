using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed partial class NativePcSaveConverterTests
{
    [Fact]
    public void Preview_update_preserves_reserved_bytes_after_the_disc_byte()
    {
        GameState state = Defaults();
        NativePcSaveData image = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(state));
        image.Block[457] = 0xA1;
        image.Block[458] = 0xB2;
        image.Block[459] = 0xC3;
        NativePcSaveData exported = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(state with { OriginalSave = image }));
        Assert.Equal(new byte[] { 0xA1, 0xB2, 0xC3 }, exported.Block.AsSpan(457, 3).ToArray());
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(19, 100)]
    [InlineData(20, 20)]
    [InlineData(255, 255)]
    public void Import_applies_native_sound_volume_load_normalization(byte saved, byte expected)
    {
        GameState defaults = Defaults() with { InputConfiguration = new(NativeInputConfiguration.OriginalInitHash, 0x00020001, System.Collections.Immutable.ImmutableArray.CreateRange(Enumerable.Range(1, 12).Select(value => (byte)value))) };
        NativePcSaveData image = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(defaults));
        image.Block[NativePcSaveData.MainOffset + 2707] = saved;
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(image), defaults, _ => "fixture", "en");
        Assert.Equal(expected, imported.InputConfiguration!.SoundVolume);
        Assert.Equal(saved, imported.OriginalSave!.Block[NativePcSaveData.MainOffset + 2707]);
    }

    [Theory]
    [InlineData(1279, 2)]
    [InlineData(1280, 1)]
    [InlineData(1407, 4)]
    public void Export_refuses_generic_cells_overlapping_native_world_storage(int address, int width)
    {
        GameState state = Defaults();
        state = state with { EventMemory = state.EventMemory.Write(address, width, NativeScalarValue.Exact(1)) };
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(state));
    }

    [Fact]
    public void Export_transfers_native_totals_test_level_sound_and_field_encounter_rng()
    {
        GameState state = Defaults();
        state = state with
        {
            BattleStatistics = new(0xDEADBEEF, 0x1234, 0x5678),
            SeeD = NativeSeeDState.CreateNewGame() with { TestLevel = 27 },
            InputConfiguration = new(NativeInputConfiguration.OriginalInitHash, 0x00020001, System.Collections.Immutable.ImmutableArray.CreateRange(Enumerable.Range(1, 12).Select(value => (byte)value))) { SoundVolume = 73 },
            RandomEncounters = new(false, NativeRandomEncounterState.FromColdProcess() with { FieldIndex = 0xDA, FieldOffset = 0xB4 }, null)
        };
        GameState imported = NativePcSaveConverter.Import(NativePcSaveConverter.Export(state), Defaults(), _ => "fixture", "en");
        Assert.Equal(state.BattleStatistics, imported.BattleStatistics);
        Assert.Equal((byte)27, imported.SeeD!.TestLevel);
        NativePcSaveData image = imported.OriginalSave!;
        Assert.Equal((byte)73, image.Block[NativePcSaveData.MainOffset + 2707]);
        Assert.Equal((byte)0xDA, imported.RandomEncounters!.Native.FieldIndex);
        Assert.Equal((byte)0xB4, imported.RandomEncounters.Native.FieldOffset);
    }

    [Fact]
    public void Export_transfers_the_full_persistent_battle_mode_before_timer_bit_overlays()
    {
        GameState state = Defaults() with
        {
            EventState = JsonSerializer.SerializeToElement(new { Scene = new { NativeBattleFlagWrites = new[] { new { PersistentFlags = (ushort)0xA35F } } } }),
            ExamTimer = NativeExamTimerState.FromColdProcess() with { Event182Mask4 = false, Event183Mask1 = true }
        };
        NativePcSaveData image = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(state));
        Assert.Equal((ushort)0xA35B, BinaryPrimitives.ReadUInt16LittleEndian(image.Block.AsSpan(NativePcSaveData.EventBankOffset + 182)));
    }
    [Fact]
    public void Export_refuses_unproved_HP_or_countdown_instead_of_choosing_a_representative()
    {
        GameState source = Defaults();
        NativeCharacterVitals vitals = source.CharacterVitals!.DeepCopy();
        vitals.Records[0] = vitals.Records[0] with { HitPoints = new NativeHpWord(1, 2) };
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(source with { CharacterVitals = vitals }));
        NativeExamTimerState timer = NativeExamTimerState.FromReachedSet(2) with { Alternatives = [new NativeTimerAlternative(0, 0, 1), new NativeTimerAlternative(1, 1, 2)] };
        Assert.Throws<NotSupportedException>(() => NativePcSaveConverter.Export(source with { ExamTimer = timer }));
    }

}
