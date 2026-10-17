namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaNodeLifetimeTests
{
    private const int First = 0xC000;
    private const int Node = First + 0x25F8;

    [Theory]
    [InlineData(0, 1, 0, false, 1)]
    [InlineData(0, 1, 1, true, 1)]
    [InlineData(1, 0, 0, true, 65535)]
    [InlineData(1, 32768, 1, false, 32767)]
    [InlineData(2, 2, 1, true, 1)]
    [InlineData(2, 1, 0, false, 0)]
    [InlineData(2, 0, 0, true, 65535)]
    [InlineData(3, 0, 1, false, 0)]
    public void Original_lifetime_selector_combines_signed_wrapped_countdown_with_exact_animation_marker(byte mode, ushort remaining, byte marker, bool complete, ushort expectedRemaining)
    {
        var fixture = Fixture(mode, remaining, marker);
        NativeBattleCuragaNodeLifetimeStep result = NativeBattleCuragaNodeKinematics.CompleteGeometry881650(fixture.Frame, fixture.Task, Node);
        Assert.Equal(complete, result.Complete);
        Assert.Equal(new byte[] { (byte)expectedRemaining, (byte)(expectedRemaining >> 8) }, result.Frame.Memory.Arena!.Read(Node + 0x1C8, 2));
    }

    [Fact]
    public void Completion_appends_the_literal_signed_child_source_before_reporting_completion_without_unlinking_geometry()
    {
        var fixture = Fixture(1, 0, 0);
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source =>
        {
            source[0x30] = 1;
            source[0x31] = 255;
        });
        NativeBattleCuragaNodeLifetimeStep result = NativeBattleCuragaNodeKinematics.CompleteGeometry881650(frame, fixture.Task, Node);
        Assert.True(result.Complete);
        int child = First + 108;
        Assert.Equal(new byte[] { 255, 3 }, result.Frame.Memory.Arena!.Read(child + 0x6A, 2));
        Assert.Equal(new uint[] { 0x20DFAB8 + Node }, NativeBattleCuragaEmitterTests.Dwords(result.Frame, child + 0x5C, 1));
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(result.Frame.Effect.Pools).Read(fixture.Task);
        Assert.Equal((ushort)2, task.ReadWord(0x34 + 0x14));
        Assert.Equal((ushort)1, task.ReadWord(0x34 + 0x16));
        Assert.Equal(new byte[] { 1 }, result.Frame.Memory.Arena.Read(Node + 0x1D7, 1));
    }

    [Fact]
    public void Four_birth_flags_admit_only_byte_one_and_keep_source_order_target_and_parent_pointer()
    {
        var fixture = Fixture(0, 0, 0);
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source =>
        {
            source[0x28] = 1;
            source[0x29] = 2;
            source[0x2A] = 1;
            source[0x2B] = 1;
            source[0x2C] = 255;
            source[0x2D] = 5;
            source[0x2E] = 10;
            source[0x2F] = 128;
        });
        NativeBattleCuragaFrame result = NativeBattleCuragaNodeKinematics.AppendChildren882D90(frame, fixture.Task, Node);
        Assert.Equal(new byte[] { 255, 3 }, result.Memory.Arena!.Read(First + 108 + 0x6A, 2));
        Assert.Equal(new byte[] { 10, 3 }, result.Memory.Arena.Read(First + 216 + 0x6A, 2));
        Assert.Equal(new byte[] { 128, 3 }, result.Memory.Arena.Read(First + 324 + 0x6A, 2));
        Assert.Equal(new uint[] { 0x20DFAB8 + Node }, NativeBattleCuragaEmitterTests.Dwords(result, First + 324 + 0x5C, 1));
    }

    [Fact]
    public void Marker_only_mode_preserves_unknown_countdown_while_countdown_mode_requires_its_actual_prior_WORD()
    {
        var fixture = Fixture(0, 0, 1);
        NativeBattleCuragaFrame frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = fixture.Frame.Memory.Arena!.MarkUnknown(Node + 0x1C8, 2) } };
        NativeBattleCuragaNodeLifetimeStep result = NativeBattleCuragaNodeKinematics.CompleteGeometry881650(frame, fixture.Task, Node);
        Assert.True(result.Complete);
        Assert.Throws<NotSupportedException>(() => result.Frame.Memory.Arena!.Read(Node + 0x1C8, 2));
        frame = NativeBattleCuragaEmitterTests.Source(frame, source => source[0x1E] = 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaNodeKinematics.CompleteGeometry881650(frame, fixture.Task, Node));
    }

    private static (NativeBattleCuragaFrame Frame, uint Task) Fixture(byte mode, ushort remaining, byte marker)
    {
        var fixture = NativeBattleCuragaNodeEngineTests.Fixture([(0, 0, false)]);
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source => source[0x1E] = mode);
        frame = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Task, 0, 0, 3).Frame;
        frame = NativeBattleCuragaNodes.AllocateGeometry882080(frame, fixture.Task, First, 0).Frame;
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(Node + 0x1C8, [(byte)remaining, (byte)(remaining >> 8)]).Write(Node + 0x1D2, [marker]) } };
        return (frame, fixture.Task);
    }
}
