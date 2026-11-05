using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplaySaveFieldInputResetTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Logical_resume_starts_released_at_its_epoch_without_claiming_a_hardware_sample(bool knownConfiguration)
    {
        NativeInputConfiguration? configuration = knownConfiguration ? new(new string('0', 64), 0x00020001, Enumerable.Range(1, 12).Select(value => (byte)value).ToImmutableArray()) : null;
        NativeFieldButtonInput reset = NativeFieldButtonInput.FromGameplayContinuation(106, 12, configuration);
        Assert.Equal(NativeFieldInputOrigin.GameplayContinuationReset, reset.Origin);
        Assert.Equal((ushort)0, reset.RawHeld);
        Assert.Equal((ushort)0, reset.RawPressed);
        Assert.Equal((ushort)0, reset.PhysicalHeld);
        Assert.Equal((ushort)0, reset.PendingPulse);
        Assert.Equal(configuration?.Map(0), reset.Held);
        reset.ValidateSample(106, 12, configuration);
        Assert.False(reset.IsColdProcess);
        Assert.Throws<InvalidDataException>(() => reset.ValidateSample(106, 13, configuration));
        Assert.Throws<InvalidDataException>(() => (reset with { RawHeld = 1, RawPressed = 1 }).Validate());
    }

    [Fact]
    public void Previous_input_documents_default_to_their_original_origin()
    {
        const string previous = "{\"Version\":3,\"RawHeld\":0,\"RawPressed\":0,\"SampledFrame\":0,\"SampledFieldId\":-1,\"PhysicalHeld\":0,\"PendingPulse\":0,\"Held\":null,\"Pressed\":null}";
        NativeFieldButtonInput input = JsonSerializer.Deserialize<NativeFieldButtonInput>(previous)!;
        input.Validate();
        Assert.Equal(NativeFieldInputOrigin.Sampled, input.Origin);
        Assert.True(input.IsColdProcess);
        Assert.DoesNotContain("Origin", JsonSerializer.Serialize(input));
    }
}
