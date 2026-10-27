using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedLightingTests
{
    private const uint Light = 0x27977F4;
    private const uint Parent = 0x2797A08;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Packed_rotation_copy_reads_signed_words_and_ignores_only_the_final_padding_word(bool knownPadding)
    {
        short[] expected = [short.MinValue, short.MaxValue, -4096, 4096, -1, 0, 1, 0x1234, -12345];
        byte[] bytes = new byte[20];
        for (int index = 0; index < expected.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), expected[index]);
        }
        bytes[18] = 0xAD;
        bytes[19] = 0xDE;
        NativeBattleScriptedLightingMatrix matrix = new(bytes.ToImmutableArray(), Enumerable.Repeat(true, 20).ToImmutableArray().SetItem(18, knownPadding).SetItem(19, knownPadding));
        Span<short> actual = stackalloc short[11];
        actual.Fill(123);
        matrix.CopyRotation(actual);
        Assert.Equal(expected, actual[..9].ToArray());
        Assert.Equal(new short[] { 123, 123 }, actual[9..].ToArray());
        Assert.Equal(expected, matrix.Rotation);
        Assert.Equal(expected, matrix.ToActionMatrix().Rotation);
        Assert.Equal(bytes, matrix.Bytes.ToArray());
        Assert.Throws<ArgumentException>(() => matrix.CopyRotation(new short[8]));
        for (int index = 0; index < 18; index++)
        {
            NativeBattleScriptedLightingMatrix unknown = matrix with { Known = matrix.Known.SetItem(index, false) };
            short[] destination = Enumerable.Repeat((short)123, 9).ToArray();
            Assert.Throws<NotSupportedException>(() => unknown.CopyRotation(destination));
            Assert.All(destination, value => Assert.Equal((short)123, value));
            Assert.Throws<NotSupportedException>(() => unknown.Rotation);
        }
        NativeBattleScriptedLightingMatrix malformed = matrix with { Bytes = matrix.Bytes.RemoveAt(19) };
        Assert.Throws<InvalidDataException>(() => malformed.CopyRotation(new short[9]));
        Assert.Throws<InvalidDataException>(() => malformed.Rotation);
    }

    [Fact]
    public void Zero_selector_preserves_retained_registers_and_never_reads_unconsumed_fields()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0);
        frame = frame with { Core = NativeBattleScriptedEffectMemory.Unknown().Write(0x27973E8, BitConverter.GetBytes(NativeBattleEffectNodeMemory.ArenaAddress + 0x2000)), Arena = frame.Arena.MarkUnknown(0x20CC, 4).MarkUnknown(0x209C, 2) };
        NativeBattleScriptedLightingMemory retained = new(new(new byte[20].ToImmutableArray(), Enumerable.Repeat(true, 20).ToImmutableArray()), new(new byte[20].ToImmutableArray(), Enumerable.Repeat(true, 20).ToImmutableArray()), new(-1, 2, -3));
        NativeBattleScriptedLightingStep result = NativeBattleScriptedLighting.Prepare(frame, retained, (_, _) => throw new InvalidOperationException());
        Assert.Same(frame, result.Frame);
        Assert.Same(retained, result.Lighting);
        Assert.Null(NativeBattleScriptedLightingMemory.Unknown().LightMatrix);
        Assert.Null(NativeBattleScriptedLightingMemory.Unknown().ColorMatrix);
        Assert.Null(NativeBattleScriptedLightingMemory.Unknown().BackColor);
    }

    [Theory]
    [InlineData(0x01)]
    [InlineData(0x71)]
    public void Unparented_light_masks_low_nibble_copies_exact_twenty_bytes_then_scales_columns(byte selector)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(selector);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x209C, 2) };
        NativeBattleScriptedLightingStep result = NativeBattleScriptedLighting.Prepare(frame, NativeBattleScriptedLightingMemory.Unknown(), (_, _) => throw new InvalidOperationException());
        Assert.Equal(new short[] { 4096, 512, 0, 0, 2048, 128, 0, 0, 1024 }, result.Lighting.LightMatrix!.Rotation);
        Assert.Equal(new short[] { -4096, 100, 200, 300, -2048, 500, 600, 700, 1024 }, result.Lighting.ColorMatrix!.Rotation);
        Assert.Equal(new NativeBattleScriptedBackColor(-16, 0, -16), result.Lighting.BackColor);
        Assert.Equal(new byte[] { 0x34, 0x12 }, result.Lighting.ColorMatrix.Bytes.AsSpan().Slice(18).ToArray());
        Assert.All(result.Lighting.ColorMatrix.Known, known => Assert.True(known));
        Assert.Equal((ushort)0xBEEF, result.Frame.ReadTemporaryWord(0x32));
        Assert.Equal(new byte[] { 0xEF, 0xBE }, result.Lighting.LightMatrix.Bytes.AsSpan().Slice(18).ToArray());
        Assert.All(result.Lighting.LightMatrix.Known, known => Assert.True(known));
        Assert.Equal(4096u, result.Frame.ReadTemporaryDword(0x80));
        Assert.Equal(2048u, result.Frame.ReadTemporaryDword(0x84));
        Assert.Equal(1024u, result.Frame.ReadTemporaryDword(0x88));
        Assert.Throws<NotSupportedException>(() => result.Frame.ReadTemporaryDword(0xFC));
        Assert.Throws<NotSupportedException>(() => result.Frame.ReadTemporaryDword(0x34));
    }

    [Theory]
    [InlineData(0x81)]
    [InlineData(0xF1)]
    public void Signed_high_bit_multiplies_light_by_actual_parent_before_column_scale_without_reading_padding(byte selector)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(selector);
        frame = frame with { Core = frame.Core.MarkUnknown(Light + 18, 2).MarkUnknown(Parent + 18, 14) };
        int resolves = 0;
        NativeBattleScriptedLightingStep result = NativeBattleScriptedLighting.Prepare(frame, NativeBattleScriptedLightingMemory.Unknown(), (current, requested) =>
        {
            resolves++;
            Assert.Equal((ushort)0xFFFE, requested);
            Assert.Equal(4096u, current.ReadTemporaryDword(0x80));
            return Parent;
        });
        Assert.Equal(1, resolves);
        Assert.Equal(new short[] { 1024, -2048, 0, 4096, 0, 128, 0, 0, 1024 }, result.Lighting.LightMatrix!.Rotation);
        Assert.Equal(Parent, result.Frame.ReadTemporaryDword(0xFC));
        Assert.Throws<NotSupportedException>(() => result.Frame.ReadTemporaryWord(0x32));
        Assert.All(result.Lighting.LightMatrix.Known.Skip(18), known => Assert.False(known));
    }

    [Fact]
    public void Nonzero_signed_selector_with_low_nibble_zero_prepares_record_zero()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x80);
        foreach ((uint offset, int length) in new (uint, int)[] { (0, 20), (0x20, 20), (0x40, 12) })
        {
            frame = frame with { Core = frame.Core.Write(0x27977A4 + offset, frame.Core.Read(Light + offset, length)) };
        }
        NativeBattleScriptedLightingStep result = NativeBattleScriptedLighting.Prepare(frame, NativeBattleScriptedLightingMemory.Unknown(), (_, _) => Parent);
        Assert.Equal(new short[] { 1024, -2048, 0, 4096, 0, 128, 0, 0, 1024 }, result.Lighting.LightMatrix!.Rotation);
    }

    [Fact]
    public void Parented_publication_preserves_actual_padding_from_an_earlier_unparented_slot()
    {
        NativeBattleScriptedLightingStep first = NativeBattleScriptedLighting.Prepare(Fixture(1), NativeBattleScriptedLightingMemory.Unknown(), (_, _) => Parent);
        NativeBattleWaterExecutionFrame parented = first.Frame with { Arena = first.Frame.Arena.Write(0x20E1, [0x81]) };
        NativeBattleScriptedLightingStep result = NativeBattleScriptedLighting.Prepare(parented, first.Lighting, (_, _) => Parent);
        Assert.Equal((ushort)0xBEEF, result.Frame.ReadTemporaryWord(0x32));
        Assert.Equal(new byte[] { 0xEF, 0xBE }, result.Lighting.LightMatrix!.Bytes.AsSpan().Slice(18).ToArray());
        Assert.All(result.Lighting.LightMatrix.Known, known => Assert.True(known));
    }

    [Fact]
    public void Missing_actual_light_and_parent_rotation_fail_without_publishing_unlit_registers()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(1);
        frame = frame with { Core = frame.Core.MarkUnknown(Light, 20) };
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedLighting.Prepare(frame, NativeBattleScriptedLightingMemory.Unknown(), (_, _) => Parent));
        frame = Fixture(0x81);
        frame = frame with { Core = frame.Core.MarkUnknown(Parent, 18) };
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedLighting.Prepare(frame, NativeBattleScriptedLightingMemory.Unknown(), (_, _) => Parent));
    }

    [Fact]
    public void Independent_JSON_retains_all_registers_and_repeats_next_preparation()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x81);
        NativeBattleScriptedLightingStep first = NativeBattleScriptedLighting.Prepare(frame, NativeBattleScriptedLightingMemory.Unknown(), (_, _) => Parent);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { ScriptedLighting = first.Lighting };
        NativeBattleEffectMemory restoredMemory = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        NativeBattleWaterExecutionFrame restoredFrame = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleScriptedLightingStep restored = NativeBattleScriptedLighting.Prepare(restoredFrame, restoredMemory.ScriptedLighting!, (_, _) => Parent);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(restored));
        Assert.Equal(JsonSerializer.Serialize(first.Lighting), JsonSerializer.Serialize(restoredMemory.ScriptedLighting));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleScriptedLightingMemory>("{}"));
    }

    private static NativeBattleWaterExecutionFrame Fixture(byte selector)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = frame with { TemporaryFrame = NativeBattleWater222Controller.CreateTemporaryFrame(0x1D9A000), Core = NativeBattleScriptedEffectMemory.Unknown().Write(0x27973E8, BitConverter.GetBytes(NativeBattleEffectNodeMemory.ArenaAddress + 0x2000)), Arena = frame.Arena.Write(0x20E1, [selector]).Write(0x20CC, BitConverter.GetBytes(0xAA204080u)).Write(0x209C, [0xFE, 0xFF]) };
        frame = WriteRotation(frame, Light, [4096, 1024, 0, 0, 4096, 512, 0, 0, 4096]);
        frame = frame with { Core = frame.Core.Write(Light + 18, [0xEF, 0xBE]) };
        frame = WriteRotation(frame, Light + 0x20, [-4096, 100, 200, 300, -2048, 500, 600, 700, 1024]);
        frame = frame with { Core = frame.Core.Write(Light + 0x32, [0x34, 0x12]) };
        frame = frame with { Core = frame.Core.Write(Light + 0x40, BitConverter.GetBytes(-1)).Write(Light + 0x44, BitConverter.GetBytes(int.MinValue)).Write(Light + 0x48, BitConverter.GetBytes(int.MaxValue)) };
        return WriteRotation(frame, Parent, [0, -4096, 0, 4096, 0, 0, 0, 0, 4096]);
    }

    private static NativeBattleWaterExecutionFrame WriteRotation(NativeBattleWaterExecutionFrame frame, uint address, short[] values)
    {
        byte[] bytes = new byte[18];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), values[index]);
        }
        return frame with { Core = frame.Core.Write(address, bytes) };
    }
}
