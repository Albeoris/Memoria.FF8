using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFieldRegistrationTests
{
    [Theory]
    [InlineData(0x1DCA328u, 0xC00)]
    [InlineData(0x1DCB0F0u, 0x100)]
    [InlineData(0x1DCB340u, 0x80)]
    public void Actual5310F0_clears_only_its_three_complete_native_ranges(uint address, int length)
    {
        NativeFieldModelCacheResetState reset = NativeFieldModelCacheResetState.Execute5310F0(NativeFieldRuntimeProfile.FF8En);
        Assert.Equal(length, reset.Read(address, length).Length);
        Assert.All(reset.Read(address, length), value => Assert.Equal((byte)0, value));
        Assert.Throws<NotSupportedException>(() => reset.Read(address - 1, 1));
        Assert.Throws<NotSupportedException>(() => reset.Read(address + (uint)length, 1));
    }

    [Fact]
    public void Adjacent_mapping_and_shared_staging_are_not_cleared_or_fabricated_by_model_cache_reset()
    {
        NativeFieldModelCacheResetState reset = NativeFieldModelCacheResetState.Execute5310F0(NativeFieldRuntimeProfile.FF8En);
        Assert.Throws<NotSupportedException>(() => reset.Read(0x1DCAF28, 64));
        Assert.Throws<NotSupportedException>(() => reset.Read(0x199B018, 163916));
        Assert.Throws<NotSupportedException>(() => reset.Read(0x1DCB0F0, 0x101));
        Assert.Throws<NotSupportedException>(() => reset.Read(uint.MaxValue, 4));
        Assert.Throws<NotSupportedException>(() => reset.Read(0x1DCB340, -1));
        NativeFieldModelCacheResetState restored = JsonSerializer.Deserialize<NativeFieldModelCacheResetState>(JsonSerializer.Serialize(reset))!;
        Assert.Equal(reset, restored);
        Assert.Equal(reset.Read(0x1DCB340, 0x80).ToArray(), restored.Read(0x1DCB340, 0x80).ToArray());
    }

    [Fact]
    public void Original5318D0_part_and_skin_initialization_preserves_unselected_copied_words()
    {
        NativeModelPartsDefinition source = Definition();
        NativeModelPartResourceState state = NativeModelPartResourceState.Create(source);
        state.Validate(source);
        var flags = state.SkinFlags ?? throw new InvalidOperationException("Fresh5318D0 registration omitted skin words.");
        Assert.Equal(new ushort[] { 0x1111, 0, 0x3333 }, flags);
        Assert.Equal((ushort)0, state.ReadSkinFlags(1));
        for (int word = 0; word < 16; word++)
        {
            Assert.Equal(word == 7 ? (short)0 : source.Parts[0].Words[word], state.Parts[0][word]);
        }
        Assert.Equal((ushort)0x2222, source.SkinBindings[1].OriginalFlags);
        Assert.Throws<InvalidDataException>(() => (state with { SkinFlags = flags.SetItem(0, 0) }).Validate(source));
    }

    [Fact]
    public void Old_missing_skin_flags_stay_unknown_and_current_flags_round_trip()
    {
        NativeModelPartsDefinition source = Definition();
        NativeModelPartResourceState current = NativeModelPartResourceState.Create(source);
        NativeModelPartResourceState restored = JsonSerializer.Deserialize<NativeModelPartResourceState>(JsonSerializer.Serialize(current))!;
        restored.Validate(source);
        Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(restored));
        NativeModelPartResourceState old = current with { SkinFlags = null };
        old.Validate(source);
        Assert.DoesNotContain("SkinFlags", JsonSerializer.Serialize(old));
        Assert.Throws<NotSupportedException>(() => old.ReadSkinFlags(1));
    }

    private static NativeModelPartsDefinition Definition()
    {
        short[] row = [1, 1, -7, 0, 20, 30, 40, 0x1234, 0, 0, 0, 0, 50, 60, 70, 90];
        return new(new string('A', 64), 100, [new(row)], [new(0, 1, 0, 0x1111), new(1, 1, 0, 0x2222), new(2, 1, 0, 0x3333)]);
    }
}
