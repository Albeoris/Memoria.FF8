namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaSpriteAnimationTests
{
    private const int Node = 0xC000 + 0x25F8;

    [Theory]
    [InlineData(0, 2, 2, 0, 0, 0, 0, 1)]
    [InlineData(1, 2, 2, 0, 0, 0, 0, 0)]
    [InlineData(2, 3, 5, 1, 3, 2, 1, 0)]
    [InlineData(2, 6, 5, 1, 3, 0, 0, 1)]
    [InlineData(2, 3, 5, 1, 3, 255, 4, 0)]
    [InlineData(0, 127, 127, 0, 0, 0, 128, 0)]
    public void Original_sprite_kind_controls_signed_frame_loop_repeats_and_completion_marker(sbyte kind, byte phase, byte last, byte loopFirst, byte loopLast, byte repeats, byte expectedPhase, byte expectedMarker)
    {
        var fixture = NativeBattleCuragaNodeEngineTests.Fixture();
        NativeBattleCuragaFrame frame = WithBytes(fixture.Frame, phase, last, 0, loopFirst, loopLast, repeats);
        NativeBattleCuragaFrame result = NativeBattleCuragaSpriteAnimation.Advance880CE0(frame, fixture.Task, Node, kind);
        byte[] actual = result.Memory.Arena!.Read(Node + 0x1D0, 6);
        Assert.Equal(expectedPhase, actual[0]);
        Assert.Equal(expectedMarker, actual[2]);
        Assert.Equal(kind == 2 && repeats == 2 ? (byte)1 : repeats, actual[5]);
    }

    [Fact]
    public void Looping_kind_does_not_clear_a_prior_marker_and_non_sprite_kind_preserves_all_node_bytes()
    {
        var fixture = NativeBattleCuragaNodeEngineTests.Fixture();
        NativeBattleCuragaFrame frame = WithBytes(fixture.Frame, 2, 2, 9, 0, 0, 0);
        NativeBattleCuragaFrame result = NativeBattleCuragaSpriteAnimation.Advance880CE0(frame, fixture.Task, Node, 1);
        Assert.Equal(new byte[] { 0, 2, 9, 0, 0, 0 }, result.Memory.Arena!.Read(Node + 0x1D0, 6));
        Assert.Same(frame, NativeBattleCuragaSpriteAnimation.Advance880CE0(frame, fixture.Task, Node, 4));
    }

    private static NativeBattleCuragaFrame WithBytes(NativeBattleCuragaFrame frame, byte phase, byte last, byte marker, byte loopFirst, byte loopLast, byte repeats)
    {
        return frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(Node + 0x1D0, [phase, last, marker, loopFirst, loopLast, repeats]) } };
    }
}
