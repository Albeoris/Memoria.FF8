using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaNodeCycleTests
{
    private const int Node = NativeBattleCuragaSpawnTests.Node;
    private const int Emitter = NativeBattleCuragaSpawnTests.Emitter;

    [Fact]
    public void Appended_geometry_updates_immediately_and_completion_clears_resource_one_callback_before_unlink()
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaFrame frame = EmitOnce(fixture.Frame, 1);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0xF0, 10, true);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0xF4, 20, true);
        frame = NativeBattleCuragaSpawnTests.Channel(frame, 0xF8, 30, true);
        NativeBattleCuragaNodeCycleStep step = Advance(frame);
        Assert.Equal(new byte[] { 1, 0 }, step.Frame.Memory.Arena!.Read(Node + 0x1CA, 2));
        Assert.Equal(new byte[] { 1, 0 }, step.Frame.Memory.Arena.Read(Node + 0x1C8, 2));
        Assert.Equal(new byte[] { 10, 20, 30, 0 }, step.Frame.Memory.Arena.Read(Node + 0x16C, 4));
        Assert.NotEqual(0u, NativeBattleCuragaEmitterTests.Dwords(step.Frame, Node + 0x170, 1)[0]);
        step = Advance(step.Frame);
        Assert.Equal(new byte[] { 2, 0 }, step.Frame.Memory.Arena!.Read(Node + 0x1CA, 2));
        NativeBattleCuragaFrame clone = NativeBattleCuragaSpawnTests.Clone(step.Frame);
        NativeBattleCuragaNodeCycleStep completed = Advance(step.Frame);
        NativeBattleCuragaNodeCycleStep restored = Advance(clone);
        Assert.Equal(completed.Frame.Memory.Arena!.Read(Node, 672), restored.Frame.Memory.Arena!.Read(Node, 672));
        Assert.Equal(new byte[] { 1, 0 }, completed.Frame.Memory.Arena.Read(Node + 0x1CC, 2));
        Assert.Equal(new byte[] { 1 }, completed.Frame.Memory.Arena.Read(Node + 0x1D7, 1));
        Assert.Equal(0u, NativeBattleCuragaEmitterTests.Dwords(completed.Frame, Node + 0x170, 1)[0]);
        Assert.Equal((ushort)1, Count(completed.Frame));
        NativeBattleCuragaFrame unlinked = Advance(completed.Frame).Frame;
        Assert.Equal(new byte[] { 0 }, unlinked.Memory.Arena!.Read(Node + 0x1D7, 1));
        Assert.Equal((ushort)0, Count(unlinked));

        NativeBattleCuragaNodeCycleStep Advance(NativeBattleCuragaFrame value) => NativeBattleCuragaNodeKinematics.AdvanceList8804D0(value, fixture.Task, new(1), _ => throw new InvalidOperationException());
        ushort Count(NativeBattleCuragaFrame value) => new NativeBattleEffectHierarchy(value.Effect.Pools).Read(fixture.Task).ReadWord(0x34 + 0x16);
    }

    [Theory]
    [InlineData(6, 4, new byte[] { 4, 2 })]
    [InlineData(6, -4, new byte[] { 2 })]
    public void Emission_uses_signed_divisor_and_groups_then_positive_remainder(byte count, sbyte divisor, byte[] groups)
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaFrame frame = EmitOnce(fixture.Frame, count);
        frame = NativeBattleCuragaEmitterTests.Source(frame, source => source[0x36] = unchecked((byte)divisor));
        NativeBattleCuragaFrame result = NativeBattleCuragaNodeKinematics.AdvanceList8804D0(frame, fixture.Task, new(1), _ => throw new InvalidOperationException()).Frame;
        Assert.Equal(groups, Enumerable.Range(0, groups.Length).Select(index => result.Memory.Arena!.Read(Node + index * 672 + 0x1D8, 1)[0]).ToArray());
        Assert.Equal((ushort)groups.Length, new NativeBattleEffectHierarchy(result.Effect.Pools).Read(fixture.Task).ReadWord(0x34 + 0x16));
    }

    [Fact]
    public void Zero_emission_divisor_fails_at_reached_IDIV_but_zero_emission_does_not_read_it()
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaFrame frame = NativeBattleCuragaEmitterTests.Source(fixture.Frame, source => source[0x36] = 0);
        NativeBattleCuragaNodeKinematics.AdvanceList8804D0(frame, fixture.Task, new(1), _ => throw new InvalidOperationException());
        frame = EmitOnce(frame, 1);
        Assert.Throws<InvalidDataException>(() => NativeBattleCuragaNodeKinematics.AdvanceList8804D0(frame, fixture.Task, new(1), _ => throw new InvalidOperationException()));
    }

    [Fact]
    public void Newly_born_sprite_advances_to_phase_one_before_its_first_drawable_update()
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaFrame frame = EmitOnce(fixture.Frame, 1);
        frame = NativeBattleCuragaEmitterTests.Source(frame, source => source[0x16] = 0);
        frame = NativeBattleCuragaSpawnTests.Task(frame, fixture.Task, task => task.WriteDword(0x34 + 0x228, NativeBattleCuragaExecutableState.Address + 0x44));
        frame = NativeBattleCuragaSpawnTests.Bank(frame, data =>
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x44), NativeBattleCuragaExecutableState.Address + 0x400);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x408), 8);
        });
        NativeBattleCuragaFrame result = NativeBattleCuragaNodeKinematics.AdvanceList8804D0(frame, fixture.Task, new(1), _ => throw new InvalidOperationException()).Frame;
        Assert.Equal(new byte[] { 1, 7, 0 }, result.Memory.Arena!.Read(Node + 0x1D0, 3));
        Assert.Equal(1u, NativeBattleCuragaEmitterTests.Dwords(result, Emitter + 0x60, 1)[0] & 0xFFFF);
    }

    internal static NativeBattleCuragaFrame EmitOnce(NativeBattleCuragaFrame frame, byte count)
    {
        return NativeBattleCuragaSpawnTests.Bank(frame, data =>
        {
            int offset = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x104 + 0xC8)) - NativeBattleCuragaExecutableState.Address));
            Array.Clear(data, offset, 40);
            data[offset] = count;
        });
    }
}
