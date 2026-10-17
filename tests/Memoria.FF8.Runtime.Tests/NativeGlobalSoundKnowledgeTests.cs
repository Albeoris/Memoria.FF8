using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeGlobalSoundKnowledgeTests
{
    [Theory]
    [InlineData(1u, 1)]
    [InlineData(0x100u, 9)]
    [InlineData(0xFF800008u, 4)]
    [InlineData(0x800000u, 24)]
    public void Explicit_masks_read_no_unknown_rotation_or_other_effect_ID(uint mask, byte voice)
    {
        NativeGlobalSoundProcess unknown = NativeGlobalSoundProcess.UnknownBufferless();
        unknown.Validate();
        NativeGlobalSoundProcess written = unknown.RecordEffect(300000, mask, out NativeGlobalSoundAllocation allocation);
        Assert.Null(written.Rotation);
        Assert.Null(allocation.BeforeRotation);
        Assert.Null(allocation.AfterRotation);
        Assert.Equal(voice, allocation.Voice);
        NativeGlobalSoundProcess.ValidateAllocation(mask, allocation);
        for (int index = 0; index < 32; index++)
        {
            Assert.Equal(index == voice ? 300000u : (uint?)null, written.OriginalEffectIds[index]);
        }
        Assert.All(unknown.OriginalEffectIds, value => Assert.Null(value));
        Assert.False(unknown.IsColdProcess);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0xFF000000u)]
    public void Automatic_selection_requires_known_rotation_and_cannot_infer_it_from_explicit_writes(uint mask)
    {
        NativeGlobalSoundProcess partial = NativeGlobalSoundProcess.UnknownBufferless().RecordEffect(123, 1, out _);
        string before = JsonSerializer.Serialize(partial);
        Assert.Throws<NotSupportedException>(() => partial.SelectVoice(mask, out _));
        Assert.Throws<NotSupportedException>(() => partial.RecordEffect(456, mask, out _));
        Assert.Equal(before, JsonSerializer.Serialize(partial));
        Assert.Throws<InvalidDataException>(() => NativeGlobalSoundProcess.ValidateAllocation(mask, new(null, null, 26)));
    }

    [Fact]
    public void Known_rotation_with_unknown_IDs_can_advance_and_overwrite_only_the_actual_automatic_slot()
    {
        NativeGlobalSoundProcess partial = NativeGlobalSoundProcess.UnknownBufferless() with { Rotation = 5 };
        NativeGlobalSoundProcess next = partial.RecordEffect(0, 0, out NativeGlobalSoundAllocation allocation);
        Assert.Equal(new NativeGlobalSoundAllocation(5, 0, 24), allocation);
        Assert.Equal((byte)0, next.Rotation);
        Assert.Equal(0u, next.OriginalEffectIds[24]);
        Assert.Equal(31, next.OriginalEffectIds.Count(value => value is null));
        Assert.Throws<InvalidDataException>(() => NativeGlobalSoundProcess.ValidateAllocation(1, new(null, 0, 1)));
    }

    [Fact]
    public void Explicit_receipts_and_partial_processes_roundtrip_with_the_same_current_JSON_shape()
    {
        var first = new NativeBattleModuleSound(123, 0x100, null).Commit46B2A0(NativeGlobalSoundProcess.UnknownBufferless());
        NativeGlobalSoundProcess restored = JsonSerializer.Deserialize<NativeGlobalSoundProcess>(JsonSerializer.Serialize(first.Process))!;
        NativeBattleModuleSound receipt = JsonSerializer.Deserialize<NativeBattleModuleSound>(JsonSerializer.Serialize(first.Sound))!;
        Assert.Equal((byte)9, receipt.ReadCommittedVoice());
        Assert.Null(receipt.Allocation!.BeforeRotation);
        var next = new NativeBattleModuleSound(456, 16, null).Commit46B2A0(restored);
        Assert.Equal((byte)5, next.Sound.ReadCommittedVoice());
        Assert.Null(next.Process.Rotation);
        Assert.Equal(123u, next.Process.OriginalEffectIds[9]);
        Assert.Equal(456u, next.Process.OriginalEffectIds[5]);
        NativeGlobalSoundProcess cold = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        using JsonDocument coldJson = JsonDocument.Parse(JsonSerializer.Serialize(cold));
        Assert.Equal(JsonValueKind.Number, coldJson.RootElement.GetProperty("Rotation").ValueKind);
        Assert.All(coldJson.RootElement.GetProperty("OriginalEffectIds").EnumerateArray(), value => Assert.Equal(JsonValueKind.Number, value.ValueKind));
        Assert.True(JsonSerializer.Deserialize<NativeGlobalSoundProcess>(coldJson.RootElement.GetRawText())!.IsColdProcess);
    }
}
