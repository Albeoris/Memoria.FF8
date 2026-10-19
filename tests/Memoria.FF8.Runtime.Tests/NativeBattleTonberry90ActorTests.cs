using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTonberry90ActorTests
{
    [Fact]
    public void Companion_defines_only_reached_color_fields_and_preserves_shared_flag_knownness()
    {
        var fixture = Fixture(4, 0x7681B0, 0x7C);
        NativeBattleTonberry90Frame frame = fixture.Frame;
        NativeBattleScriptedProcessFlagsState owner = new([1, 2, 3, 4], [true, true, false, true]);
        frame = frame with { Memory = frame.Memory with { ScriptedProcessFlags = owner } };
        NativeBattleTonberry90TaskStep step = NativeBattleTonberry90Actors.Advance7681B0(frame, fixture.Address);
        Assert.False(step.Complete);
        Assert.Equal(owner.Flags, step.Frame.Memory.ScriptedProcessFlags!.Flags);
        Assert.Equal(owner.Known, step.Frame.Memory.ScriptedProcessFlags.Known);
        Assert.All(step.Frame.Memory.ScriptedProcessFlags.ColorRows!.Value, row => Assert.Equal(new NativeBattleScriptedProcessColorRow(0, 0, 0, 0), row));
        for (int tick = 1; tick <= 4; tick++)
        {
            step = NativeBattleTonberry90Actors.Advance7681B0(step.Frame, fixture.Address);
            Assert.Equal(tick == 4, step.Complete);
            Assert.All(step.Frame.Memory.ScriptedProcessFlags!.ColorRows!.Value, row => Assert.Equal((ushort?)(tick * 0x100), row.Factor));
        }
        Assert.Equal((byte)2, Read(step.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)5, Read(step.Frame, fixture.Address).ReadWord(0x24));
    }

    [Theory]
    [InlineData(0xFF00, 0)]
    [InlineData(0x7F00, 0x8000)]
    public void Companion_wraps_before_signed_clamp_without_fabricating_prior_RGB(int before, int after)
    {
        var fixture = Fixture(4, 0x7681B0, 0x7C, 1);
        NativeBattleTonberry90Frame frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteWord(0x76, (ushort)before));
        frame = frame with { Memory = frame.Memory with { ScriptedProcessFlags = frame.Memory.ScriptedProcessFlags! with { ColorRows = null } } };
        var step = NativeBattleTonberry90Actors.Advance7681B0(frame, fixture.Address);
        Assert.False(step.Complete);
        Assert.All(step.Frame.Memory.ScriptedProcessFlags!.ColorRows!.Value, row =>
        {
            Assert.Equal((ushort?)after, row.Factor);
            Assert.Null(row.Red);
            Assert.Null(row.Green);
            Assert.Null(row.Blue);
        });
        frame = Write(frame, fixture.Address, Read(frame, fixture.Address).WriteByte(0x29, 2));
        frame = frame with { Memory = frame.Memory with { ScriptedProcessFlags = null } };
        Assert.Null(NativeBattleTonberry90Actors.Advance7681B0(frame, fixture.Address).Frame.Memory.ScriptedProcessFlags);
    }

    [Fact]
    public void Actor_waits_six_ticks_then_initializes_actual_workspace_and_calls_required_bounds_renderer()
    {
        var fixture = Fixture(3, 0x763EB0, 0x520);
        NativeBattleTonberry90Frame frame = fixture.Frame;
        NativeBattleModulePoolTask task = Read(frame, fixture.Address).WriteWord(0x24, 5).WriteDword(0x74, 0x1000).WriteDword(0x78, 0x10);
        byte[] payload = task.Payload.ToArray();
        payload.AsSpan(0x94 - 12).Fill(0xAA);
        frame = Write(frame, fixture.Address, task with { Payload = payload.ToImmutableArray() });
        NativeBattleTonberry90State shared = frame.Memory.Tonberry90!;
        uint director = shared.DirectorAddress1547168!.Value;
        shared = shared.Write(director, BitConverter.GetBytes(100u)).Write(director + 4, BitConverter.GetBytes(200u)).Write(director + 0x46, BitConverter.GetBytes((ushort)30));
        frame = frame with { Memory = frame.Memory with { Tonberry90 = shared } };
        byte[] source = new byte[64];
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(4), 0x30);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(0x24), 0x3B);
        int reads = 0;
        int bounds = 0;
        NativeBattleTonberry90ActorServices services = new((_, pointer) =>
        {
            Assert.Equal(0x1000u, pointer);
            reads++;
            return new(pointer, source.ToImmutableArray());
        }, (current, address, _, geometry) =>
        {
            bounds++;
            Assert.Equal(0x1038u, geometry);
            NativeBattleModulePoolTask initialized = Read(current, address);
            Assert.Equal((ushort)100, initialized.ReadWord(0x1C));
            Assert.Equal((ushort)0, initialized.ReadWord(0x1E));
            Assert.Equal((ushort)(200 + 0x180), initialized.ReadWord(0x20));
            Assert.Equal(4096u, initialized.ReadDword(0x50));
            Assert.Equal(0x1547454u, current.Memory.Tonberry90!.ReadDword(0x25A4C04));
            return current;
        }, (_, _) => throw new InvalidOperationException("Draw was not reached."));
        frame = NativeBattleTonberry90Actors.Advance763EB0(frame, fixture.Address, services).Frame;
        Assert.Equal(0, reads);
        Assert.Equal(0x1547468u, frame.Memory.Tonberry90!.ReadDword(0x25A4C04));
        frame = NativeBattleTonberry90Actors.Advance763EB0(frame, fixture.Address, services).Frame;
        Assert.Equal(1, reads);
        Assert.Equal(1, bounds);
        task = Read(frame, fixture.Address);
        Assert.Equal((byte)1, task.ReadByte(0x29));
        Assert.Equal((ushort)7, task.ReadWord(0x24));
        Assert.Equal(0x1000u, task.ReadDword(0x94));
        Assert.Equal(0u, task.ReadDword(0xA0));
        Assert.Equal((byte)0xAA, task.ReadByte(0xA4));
    }

    [Fact]
    public void Actor_draw_result_zero_is_exact_and_spawned_phase_three_short_circuits_draw()
    {
        var fixture = Fixture(3, 0x763EB0, 0x520, 2);
        NativeBattleTonberry90Frame frame = fixture.Frame;
        uint director = frame.Memory.Tonberry90!.DirectorAddress1547168!.Value;
        frame = frame with { Memory = frame.Memory with { Tonberry90 = frame.Memory.Tonberry90.Write(director + 0x40, BitConverter.GetBytes((ushort)2)) } };
        int draws = 0;
        int remaining = 0;
        NativeBattleTonberry90ActorServices services = new((_, _) => throw new InvalidOperationException("Initialization was not reached."), (_, _, _, _) => throw new InvalidOperationException("Bounds were not reached."), (current, _) =>
        {
            draws++;
            return new(current, remaining);
        });
        var step = NativeBattleTonberry90Actors.Advance763EB0(frame, fixture.Address, services);
        Assert.False(step.Complete);
        Assert.Equal((byte)3, Read(step.Frame, fixture.Address).ReadByte(0x29));
        remaining = -1;
        step = NativeBattleTonberry90Actors.Advance763EB0(step.Frame, fixture.Address, services);
        Assert.False(step.Complete);
        Assert.Equal((byte)3, Read(step.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal(2, draws);
        frame = step.Frame with { Memory = step.Frame.Memory with { Tonberry90 = step.Frame.Memory.Tonberry90!.Write(director + 0x40, BitConverter.GetBytes((ushort)3)) } };
        step = NativeBattleTonberry90Actors.Advance763EB0(frame, fixture.Address, services);
        Assert.True(step.Complete);
        Assert.Equal(2, draws);
        Assert.Equal((byte)4, Read(step.Frame, fixture.Address).ReadByte(0x29));
    }

    private static (NativeBattleTonberry90Frame Frame, uint Address) Fixture(int pool, uint callback, int size, byte phase = 0)
    {
        var fixture = NativeBattleTonberry90ControllerTests.Fixture();
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy(fixture.Frame.Effect.Pools).Append8DC540(pool, callback, size, NativeBattleTonberry90Controller.MainBank, (_, _, _) => new(0, 3));
        NativeBattleEffectHierarchy hierarchy = appended.Hierarchy.Write(appended.Address, appended.Hierarchy.Read(appended.Address).WriteByte(0x29, phase));
        return (fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }, appended.Address);
    }
    private static NativeBattleModulePoolTask Read(NativeBattleTonberry90Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleTonberry90Frame Write(NativeBattleTonberry90Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
