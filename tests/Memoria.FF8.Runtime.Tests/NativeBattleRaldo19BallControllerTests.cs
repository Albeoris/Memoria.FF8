using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRaldo19BallControllerTests
{
    [Fact]
    public void Initialization_clears_only_ball_and_preserves_descriptor_padding_and_saved_unknown_rotation_padding()
    {
        var fixture = Fixture();
        NativeBattleRaldo19Frame frame = fixture.Frame with { State = fixture.Frame.State with { Descriptor48 = fixture.Frame.State.Descriptor48.Write(0x1F, new byte[] { 0xA5 }) } };
        frame = NativeBattleRaldo19BallController.Advance(frame, fixture.Address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal((byte)1, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal(0x1D972C0u, Dword(frame.State.BallC4, 0x68));
        Assert.Equal(0x1D8E054u, Dword(frame.State.BallC4, 0x6C));
        Assert.Equal((ushort)0x140, Word(frame.State.Descriptor48, 0x18));
        Assert.Equal((byte)0xA5, frame.State.Descriptor48.Read(0x1F, 1)[0]);
        Assert.All(frame.State.BallC4.ReadSnapshot(0xBE, 2).Known, known => Assert.False(known));
        Assert.Equal((ushort)0x1234, Word(frame.State.BallC4, 0xBC));
    }

    [Fact]
    public void Birth_sets_actual_radii_packed_positions_and_children_before_rendering()
    {
        var fixture = Fixture();
        NativeBattleRaldo19Frame frame = Phase(fixture.Frame, fixture.Address, 2);
        frame = NativeBattleRaldo19BallController.Advance(frame, fixture.Address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal((byte)3, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)448, Word(frame.State.BallC4, 0xA6));
        Assert.Equal((ushort)448, Word(frame.State.BallC4, 0xA8));
        Assert.Equal((ushort)448, Word(frame.State.BallC4, 0xAA));
        Assert.Equal((ushort)0xBEEF, Word(frame.State.BallC4, 0x82));
        Assert.Equal((ushort)0xBEEF, Word(frame.State.BallC4, 0x92));
        NativeBattleModulePoolTask debris = Assert.Single(frame.Effect.Pools[4].Slots, task => task.Callback == 0x8A9F10);
        Assert.Equal((ushort)2, debris.ReadWord(0xB6));
        Assert.Equal((ushort)0, debris.ReadWord(0x1E));
        NativeBattleModulePoolTask overlay = Assert.Single(frame.Effect.Pools[5].Slots, task => task.Callback == 0x8DDC30);
        Assert.Equal(0x162A470u, overlay.ReadDword(0x34));
        Assert.Equal((ushort)9, overlay.ReadWord(0x38));
        Assert.IsType<NativeBattleRaldo19BallRender>(fixture.Operations[^1]);
        Assert.Equal((byte)2, Task(frame, fixture.Address).ReadByte(0x28));
    }

    [Fact]
    public void Forward_morph_contact_is_eighth_step_and_uses_signed_truncation_for_negative_delta()
    {
        var fixture = Fixture();
        NativeBattleRaldo19Frame frame = Phase(fixture.Frame, fixture.Address, 3);
        frame = frame with { State = frame.State with { BallC4 = SetWord(SetWord(SetWord(frame.State.BallC4, 0x84, -9), 0x96, -1000), 0x7E, -2000) } };
        for (int step = 0; step < 7; step++)
        {
            frame = NativeBattleRaldo19BallController.Advance(frame, fixture.Address, fixture.Resources, fixture.Inputs).Frame;
            Assert.Equal((short)0, frame.State.Word(0x48));
        }
        Assert.Equal(unchecked((ushort)-7), Word(frame.State.BallC4, 0x7C));
        frame = NativeBattleRaldo19BallController.Advance(frame, fixture.Address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal((short)1, frame.State.Word(0x48));
        Assert.Equal((ushort)4096, Word(frame.State.BallC4, 0xC0));
        Assert.Equal((byte)4, Task(frame, fixture.Address).ReadByte(0x29));
    }

    [Fact]
    public void Return_bounce_emits_four_children_once_and_then_reduces_morph_before_restoring_actor()
    {
        var fixture = Fixture();
        NativeBattleRaldo19Frame frame = Phase(fixture.Frame, fixture.Address, 4);
        NativeBattleEffectArena ball = SetWord(SetWord(SetWord(SetWord(frame.State.BallC4, 0xC2, 1), 0xC0, 512), 0x7E, -10), 0xA4, 100);
        frame = frame with { State = frame.State with { BallC4 = ball } };
        frame = NativeBattleRaldo19BallController.Advance(frame, fixture.Address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal((ushort)2, Word(frame.State.BallC4, 0xC2));
        Assert.Equal(unchecked((ushort)-384), Word(frame.State.BallC4, 0x96));
        Assert.Equal((ushort)4, Assert.Single(frame.Effect.Pools[4].Slots, task => task.Callback == 0x8A9F10).ReadWord(0xB6));
        frame = NativeBattleRaldo19BallController.Advance(frame, fixture.Address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal((byte)5, Task(frame, fixture.Address).ReadByte(0x29));
        frame = NativeBattleRaldo19BallController.Advance(frame, fixture.Address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal((byte)6, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)0, Word(frame.State.BallC4, 0xA4));
        fixture.Operations.Clear();
        frame = NativeBattleRaldo19BallController.Advance(frame, fixture.Address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal((byte)7, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)8, Task(frame, fixture.Address).ReadWord(0xB4));
        Assert.DoesNotContain(fixture.Operations, operation => operation is NativeBattleRaldo19BallRender);
        Assert.Equal(new[] { 0, 0x1E, 0x1C, 0x20 }, fixture.Operations.Cast<NativeBattleRaldo19BallActorWrite>().Select(write => write.Offset));
    }

    [Fact]
    public void Sound_wait_uses_signed_age_before_increment_and_restore_keeps_unknown_padding()
    {
        var fixture = Fixture();
        NativeBattleRaldo19Frame frame = Phase(fixture.Frame, fixture.Address, 1);
        frame = Put(frame, fixture.Address, Task(frame, fixture.Address).WriteWord(0x24, 27));
        frame = NativeBattleRaldo19BallController.Advance(frame, fixture.Address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Empty(fixture.Operations);
        frame = NativeBattleRaldo19BallController.Advance(frame, fixture.Address, fixture.Resources, fixture.Inputs).Frame;
        NativeBattleRaldo19BallSound sound = Assert.IsType<NativeBattleRaldo19BallSound>(Assert.Single(fixture.Operations));
        Assert.Equal((byte)128, sound.Sound.Pan);
        frame = Phase(frame, fixture.Address, 7);
        frame = frame with { State = frame.State with { BallC4 = frame.State.BallC4.MarkUnknown(0xBE, 2) } };
        frame = Put(frame, fixture.Address, Task(frame, fixture.Address).WriteWord(0xB4, 1));
        frame = NativeBattleRaldo19BallController.Advance(frame, fixture.Address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal((byte)8, Task(frame, fixture.Address).ReadByte(0x29));
        NativeBattleRaldo19BallActorWrite restore = Assert.IsType<NativeBattleRaldo19BallActorWrite>(fixture.Operations[^1]);
        Assert.Equal(0x10, restore.Offset);
        Assert.All(restore.Source.ReadSnapshot(restore.SourceOffset + 2, 2).Known, known => Assert.False(known));
        NativeBattleRaldo19LeafStep done = NativeBattleRaldo19BallController.Advance(frame, fixture.Address, fixture.Resources, fixture.Inputs);
        Assert.Equal(2, done.Result);
    }

    private static (NativeBattleRaldo19Frame Frame, uint Address, NativeBattleRaldo19Resources Resources, NativeBattleRaldo19BallInputs Inputs, List<NativeBattleRaldo19BallOperation> Operations) Fixture()
    {
        var fixture = NativeBattleRaldo19ControllerTests.Fixture();
        NativeBattleEffectHierarchyAppend ball = new NativeBattleEffectHierarchy(fixture.Frame.Effect.Pools).Append8DC540(4, 0x8A93E0, 184, new NativeBattleEffectHierarchy(fixture.Frame.Effect.Pools).Address(0, 0), (_, _, _) => new(0, 3));
        NativeBattleRaldo19Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = ball.Hierarchy.Pools } };
        NativeBattleEffectArena actor = SetWord(SetWord(SetWord(NativeBattleEffectArena.Clear(), 0x10, 0x1234), 0x1C, 17), 0x22, 0xBEEF).MarkUnknown(0x12, 2);
        List<NativeBattleRaldo19BallOperation> operations = [];
        NativeBattleRaldo19BallInputs inputs = new(_ => actor, (current, operation) =>
        {
            operations.Add(operation);
            switch (operation)
            {
                case NativeBattleRaldo19BallActorWrite write:
                    actor = NativeBattleRaldo19BallController.Copy(write.Source, write.SourceOffset, actor, write.Offset, write.Length);
                    break;
                case NativeBattleRaldo19BallPivot:
                    current = current with { State = current.State with { BallC4 = current.State.BallC4.Write(0x74, new byte[6]) } };
                    break;
                case NativeBattleRaldo19BallTargetAnchor:
                    current = current with { State = current.State with { BallC4 = SetWord(current.State.BallC4, 0x84, 4096) } };
                    break;
            }
            return current;
        }, (_, _, _) => new(0, 3));
        return (frame, ball.Address, fixture.Resources, inputs, operations);
    }

    private static NativeBattleModulePoolTask Task(NativeBattleRaldo19Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleRaldo19Frame Phase(NativeBattleRaldo19Frame frame, uint address, byte phase) => Put(frame, address, Task(frame, address).WriteByte(0x29, phase));
    private static NativeBattleRaldo19Frame Put(NativeBattleRaldo19Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena SetWord(NativeBattleEffectArena arena, int offset, int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return arena.Write(offset, bytes);
    }
}
