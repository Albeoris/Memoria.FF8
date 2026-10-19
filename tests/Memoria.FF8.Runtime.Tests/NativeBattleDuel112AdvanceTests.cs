namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel112AdvanceTests
{
    [Theory]
    [InlineData(114, 3072, true)]
    [InlineData(119, 3071, true)]
    [InlineData(114, 3071, false)]
    [InlineData(119, 3072, false)]
    public void ContactThresholdUsesActualSignedScaleAndClearsTheTargetFlagBeforeConsumingExactlyItsSequence(short phase, short scale, bool expected)
    {
        NativeBattleDuel112Fixture fixture = new(phase);
        fixture.Enemy = fixture.Enemy with { Transform = fixture.Enemy.Transform! with { Scale38 = scale } };
        int calls = 0;
        NativeBattleDuel112TargetInputs inputs = new(new(fixture.Drawing, _ => throw new InvalidOperationException()), _ => throw new InvalidOperationException(), intensity => Assert.Equal(1024, intensity), (frame, sequence) =>
        {
            calls++;
            Assert.Equal(2, sequence);
            Assert.Equal(0, fixture.Enemy.Flags0 & 8);
            return frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x90000, new byte[] { 0x7B }) } };
        });
        NativeBattleDuel112TargetStep step = NativeBattleDuel112Target.Advance6CBF90(fixture.Frame, 0, inputs, NativeBattleLogicalOrdering.Clear());
        Assert.Equal(expected ? 1 : 0, calls);
        Assert.Equal((byte)(expected ? 0x7B : 0), step.Frame.Memory.Arena!.Read(0x90000, 1)[0]);
        Assert.Equal((ushort)(phase + 1), step.Pool.Slots[0].ReadWord(12));
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void SoundPrecedesStageAndTheTargetClosesOnlyAfterItsRealTerminalStores()
    {
        NativeBattleDuel112Fixture fixture = new(2);
        List<string> calls = [];
        NativeBattleDuel112TargetInputs inputs = new(new(fixture.Drawing, _ => throw new InvalidOperationException()), descriptor => calls.Add($"sound:{descriptor:X}"), intensity => calls.Add($"stage:{intensity}"), (_, _) => throw new InvalidOperationException());
        NativeBattleDuel112TargetStep step = NativeBattleDuel112Target.Advance6CBF90(fixture.Frame, 0, inputs, NativeBattleLogicalOrdering.Clear());
        Assert.Equal(new[] { "sound:12345678", "stage:512" }, calls);
        Assert.Equal(0, step.Result);
        fixture = new(152);
        inputs = new(new(fixture.Drawing, _ => throw new InvalidOperationException()), _ => throw new InvalidOperationException(), intensity => Assert.Equal(0, intensity), (_, _) => throw new InvalidOperationException());
        step = NativeBattleDuel112Target.Advance6CBF90(fixture.Frame, 0, inputs, NativeBattleLogicalOrdering.Clear());
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)152, step.Pool.Slots[0].ReadWord(12));
        Assert.Equal(0x001E3C78u, fixture.Actor.Color40);
        Assert.Equal(0x32142850u, fixture.Actor.Color44);
        Assert.Equal((byte)0xAB, fixture.Actor.Alpha7);
        Assert.Equal(0, fixture.Actor.Flags0 & 0x1800);
    }
}
