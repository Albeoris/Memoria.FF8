using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTonberry90ActorTests
{
    [Fact]
    public void Inverse_companion_waits_thirty_ticks_then_changes_only_canonical_stage_depth_for_four_ticks()
    {
        var fixture = Fixture(4, 0x7682A0, 0x7C);
        NativeBattleTonberry90Frame frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteWord(0x24, 29));
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess();
        stage = stage with { Groups = stage.Groups.Select((group, index) => group with { Flags = (byte)(index + 1), ColorDepth = 1024, ColorFlags = 0xAB123456 }).ToImmutableArray() };
        NativeBattleTonberry90CompanionStep step = NativeBattleTonberry90Actors.Advance7682A0(frame, fixture.Address, stage);
        Assert.Same(stage, step.Stage);
        Assert.Equal((byte)0, Read(step.Frame, fixture.Address).ReadByte(0x29));
        step = NativeBattleTonberry90Actors.Advance7682A0(step.Frame, fixture.Address, step.Stage);
        Assert.Same(stage, step.Stage);
        Assert.Equal((byte)1, Read(step.Frame, fixture.Address).ReadByte(0x29));
        for (int tick = 1; tick <= 4; tick++)
        {
            step = NativeBattleTonberry90Actors.Advance7682A0(step.Frame, fixture.Address, step.Stage);
            Assert.Equal(tick == 4, step.Complete);
            Assert.Equal(stage.Groups.Select(group => group.Flags), step.Stage.Groups.Select(group => group.Flags));
            Assert.All(step.Stage.Groups, group =>
            {
                Assert.Equal((short)(1024 - tick * 256), group.ColorDepth);
                Assert.Equal(0xAB123456u, group.ColorFlags);
            });
        }
        Assert.Equal((byte)2, Read(step.Frame, fixture.Address).ReadByte(0x29));
    }

    [Fact]
    public void Creature_programme_initializes_only_selected_workspace_then_draws_current_raw_anchor_and_bias()
    {
        var fixture = Fixture(3, 0x767B40, 0x520);
        NativeBattleModulePoolTask task = Read(fixture.Frame, fixture.Address).WriteDword(0x74, 0x1000).WriteDword(0x78, 20);
        byte[] payload = task.Payload.ToArray();
        payload.AsSpan(0x94 - 12).Fill(0xA5);
        NativeBattleTonberry90Frame frame = Write(fixture.Frame, fixture.Address, task with { Payload = payload.ToImmutableArray() });
        frame = frame with { Memory = frame.Memory with { Tonberry90 = frame.Memory.Tonberry90!.WriteDword(0x25A4C10, 0x0201FFFE).WriteDword(0x25A4C14, 0xA5A5FFFD).Write(0x25A2B78, BitConverter.GetBytes((ushort)0xFF00)) } };
        byte[] source = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 16);
        int draws = 0;
        NativeBattleTonberry90ActorServices services = new((_, address) => new(address, source.ToImmutableArray()), (_, _, _, _) => throw new InvalidOperationException("This programme never captures ground."), (current, address) =>
        {
            NativeBattleModulePoolTask actual = Read(current, address);
            Assert.Equal(0x0201FFFEu, actual.ReadDword(0x1C));
            Assert.Equal(0xA5A5FFFDu, actual.ReadDword(0x20));
            Assert.Equal((ushort)0xFF00, actual.ReadWord(0x88));
            Assert.Equal(0x1000u, actual.ReadDword(0x94));
            Assert.Equal(0u, actual.ReadDword(0x98));
            Assert.Equal(0xA5A5A5A5u, actual.ReadDword(0xA8));
            draws++;
            return new(Write(current, address, actual.WriteDword(0x98, 1)), 10);
        });
        NativeBattleTonberry90TaskStep step = NativeBattleTonberry90Actors.Advance767B40(frame, fixture.Address, services);
        Assert.Equal(1, draws);
        task = Read(step.Frame, fixture.Address);
        Assert.Equal((byte)1, task.ReadByte(0x29));
        Assert.Equal(1u, task.ReadDword(0x98));
        Assert.Equal((ushort)1, task.ReadWord(0x24));
        Assert.False(step.Complete);
    }

    [Fact]
    public void Creature_programme_draws_after_anchor_completion_and_preserves_both_native_phase_increments()
    {
        var fixture = Fixture(3, 0x767B40, 0x520, 1);
        NativeBattleTonberry90Frame frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Tonberry90 = fixture.Frame.Memory.Tonberry90!.WriteDword(0x25A4C10, 0xABCD7FFF).WriteDword(0x25A4C14, 0xA55A1234) } };
        NativeBattleTonberry90ActorServices services = new((_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (current, address) =>
        {
            Assert.Equal((byte)2, Read(current, address).ReadByte(0x29));
            Assert.Equal((byte)1, (byte)(Read(current, address).ReadByte(0x26) & 1));
            return new(current, 0);
        });
        NativeBattleTonberry90TaskStep step = NativeBattleTonberry90Actors.Advance767B40(frame, fixture.Address, services);
        Assert.True(step.Complete);
        Assert.Equal((byte)3, Read(step.Frame, fixture.Address).ReadByte(0x29));
    }

    [Fact]
    public void Companion_updates_canonical_stage_colors_and_preserves_flags_and_high_color_byte()
    {
        var fixture = Fixture(4, 0x7681B0, 0x7C);
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess();
        stage = stage with { Groups = stage.Groups.Select((group, index) => group with { Flags = (byte)(index + 1), ColorDepth = 123, ColorFlags = 0xAB123456 }).ToImmutableArray() };
        NativeBattleTonberry90CompanionStep step = NativeBattleTonberry90Actors.Advance7681B0(fixture.Frame, fixture.Address, stage);
        Assert.False(step.Complete);
        Assert.Equal(stage.Groups.Select(group => group.Flags), step.Stage.Groups.Select(group => group.Flags));
        Assert.All(step.Stage.Groups, group =>
        {
            Assert.Equal((short)0, group.ColorDepth);
            Assert.Equal(0xAB000000u, group.ColorFlags);
        });
        for (int tick = 1; tick <= 4; tick++)
        {
            step = NativeBattleTonberry90Actors.Advance7681B0(step.Frame, fixture.Address, step.Stage);
            Assert.Equal(tick == 4, step.Complete);
            Assert.All(step.Stage.Groups, group => Assert.Equal((short)(tick * 0x100), group.ColorDepth));
        }
        Assert.Equal((byte)2, Read(step.Frame, fixture.Address).ReadByte(0x29));
        Assert.Equal((ushort)5, Read(step.Frame, fixture.Address).ReadWord(0x24));
    }

    [Theory]
    [InlineData(0xFF00, 0)]
    [InlineData(0x7F00, 0x8000)]
    public void Companion_wraps_before_signed_clamp_preserves_RGB_and_literal_RET_does_not_touch_stage(int before, int after)
    {
        var fixture = Fixture(4, 0x7681B0, 0x7C, 1);
        NativeBattleTonberry90Frame frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteWord(0x76, (ushort)before));
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess();
        stage = stage with { Groups = stage.Groups.Select(group => group with { ColorFlags = 0xABCDEF12 }).ToImmutableArray() };
        var step = NativeBattleTonberry90Actors.Advance7681B0(frame, fixture.Address, stage);
        Assert.False(step.Complete);
        Assert.All(step.Stage.Groups, group =>
        {
            Assert.Equal(unchecked((short)after), group.ColorDepth);
            Assert.Equal(0xABCDEF12u, group.ColorFlags);
        });
        frame = Write(frame, fixture.Address, Read(frame, fixture.Address).WriteByte(0x29, 2));
        Assert.Same(stage, NativeBattleTonberry90Actors.Advance7681B0(frame, fixture.Address, stage).Stage);
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
