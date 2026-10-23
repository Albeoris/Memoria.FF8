using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBreath129Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBreath129ControllerTests
{
    private const uint Arena = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint MainAddress = Arena + 0x1690;
    private const uint FibreAddress = Arena + 0x690;
    private const uint FirstPoint = Arena + 0x19C0;

    [Fact]
    public void Fibre_spawn_retains_native_body_row_and_initializes_six_byte_controls_without_advancing_random()
    {
        NativeBattleBreath129Frame frame = Frame();
        NativeBattleBreath129Frame spawned = SpawnFibres(frame, 0x812345, 0x1229318, 0x1229338, 12, 7, 0x100);
        NativeBattleModulePoolTask task = Read(spawned, FibreAddress);
        Assert.Equal(0x6B68C0u, task.Callback);
        Assert.Equal(0x812345u, task.ReadDword(28));
        Assert.Equal(0x1229318u, task.ReadDword(20));
        Assert.Equal(0x1229338u, task.ReadDword(24));
        Assert.Equal((ushort)0x100, task.ReadWord(16));
        Assert.Equal((byte)12, task.ReadByte(18));
        Assert.Equal((byte)7, task.ReadByte(19));
        Assert.Equal((ushort)0, task.ReadWord(32));
        Assert.Equal((ushort)128, task.ReadWord(34));
        Assert.Equal((ushort)0, task.ReadWord(36));
        Assert.Equal((ushort)0, task.ReadWord(122));
        Assert.Equal((ushort)128, task.ReadWord(124));
        Assert.Equal((ushort)0, task.ReadWord(126));
        Assert.Equal(frame.Random, spawned.Random);
    }

    [Theory]
    [InlineData(3840, 256, 4096, 0, 128)]
    [InlineData(4096, 256, 4096, 1, 88)]
    [InlineData(32767, 1, -32768, 0, 128)]
    public void Fibre_tail_distinguishes_equality_wrap_and_strict_completion_boundary(short initial, short increment, short progress, ushort age, ushort intensity)
    {
        NativeBattleBreath129Frame frame = SpawnFibres(Frame(), 0x812345, 0x1229318, 0x1229338, 12, 7, increment);
        frame = Write(frame, FibreAddress, Read(frame, FibreAddress).WriteWord(14, unchecked((ushort)initial)));
        var advanced = AdvanceFibreTail6B6C3F(frame, FibreAddress);
        NativeBattleModulePoolTask task = Read(advanced.Frame, FibreAddress);
        Assert.Equal(unchecked((ushort)progress), task.ReadWord(14));
        Assert.Equal(age, task.ReadWord(12));
        Assert.Equal(intensity, task.ReadWord(34));
        Assert.Equal(0, advanced.Result);
    }

    [Fact]
    public void Fibre_tail_clamps_negative_intensity_and_finishes_on_tenth_fade_callback()
    {
        NativeBattleBreath129Frame frame = SpawnFibres(Frame(), 0x812345, 0x1229318, 0x1229338, 12, 5, 1);
        frame = Write(frame, FibreAddress, Read(frame, FibreAddress).WriteWord(12, 9).WriteWord(14, 4096));
        var advanced = AdvanceFibreTail6B6C3F(frame, FibreAddress);
        Assert.Equal(2, advanced.Result);
        Assert.Equal((ushort)10, Read(advanced.Frame, FibreAddress).ReadWord(12));
        Assert.Equal((ushort)0, Read(advanced.Frame, FibreAddress).ReadWord(34));
    }

    [Fact]
    public void Point_allocator_uses_word_zero_sentinel_preserving_unwritten_projection_bytes()
    {
        NativeBattleBreath129Frame frame = Frame();
        frame = WriteBytes(frame, FirstPoint + 4, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xAB, 16).ToArray()), 16);
        var allocated = AllocatePoint(frame, 86, 320, 1000, -125);
        Assert.Equal(FirstPoint, allocated.Address);
        NativeBattleEffectArena point = ReadBytes(allocated.Frame, FirstPoint, 28);
        Assert.Equal((short)86, BitConverter.ToInt16(point.Read(0, 2)));
        Assert.Equal((short)320, BitConverter.ToInt16(point.Read(2, 2)));
        Assert.Equal(Enumerable.Repeat((byte)0xAB, 16), point.Read(4, 16));
        Assert.Equal((short)1000, BitConverter.ToInt16(point.Read(20, 2)));
        Assert.Equal((short)-125, BitConverter.ToInt16(point.Read(22, 2)));
        Assert.Equal(0u, BitConverter.ToUInt32(point.Read(24, 4)));
        Assert.Equal(FirstPoint + 28, AllocatePoint(allocated.Frame, 1, 2, 3, 4).Address);
    }

    [Fact]
    public void Main_finishes_at73_after_empty_linked_queue_but_preserves_mean_W_and_updates_phase_words()
    {
        NativeBattleBreath129Frame frame = Frame();
        List<string> order = [];
        NativeBattleBreath129Inputs inputs = Inputs(order);
        frame = Write(frame, MainAddress, Read(frame, MainAddress).WriteWord(12, 73).WriteWord(0x36, 0xCAFE));
        var advanced = AdvanceMain(frame, MainAddress, Resources(), inputs);
        Assert.Equal(2, advanced.Result);
        Assert.Equal((ushort)73, Read(advanced.Frame, MainAddress).ReadWord(12));
        Assert.Equal((ushort)0xCAFE, Read(advanced.Frame, MainAddress).ReadWord(0x36));
        Assert.Equal(unchecked((ushort)-100), Read(advanced.Frame, MainAddress).ReadWord(0x28));
        Assert.Equal((ushort)200, Read(advanced.Frame, MainAddress).ReadWord(0x2A));
        Assert.Equal(new[] { "bone" }, order);
    }

    [Fact]
    public void Root_interleaves_composed_body_fibres_local_body_targets_and_stage_in_native_order()
    {
        NativeBattleBreath129Frame frame = Frame();
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 2).WriteByte(15, 1));
        frame = Write(frame, MainAddress, Read(frame, MainAddress).WriteWord(12, 73));
        frame = WriteBytes(frame, Arena + 0x35E4, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x1D9735Cu)), 4);
        List<string> order = [];
        var advanced = AdvanceRoot(frame, RootBank, Resources(), Inputs(order));
        Assert.Equal(2, advanced.Result);
        Assert.Equal(new[] { "bone", "compose:1D9735C", "compose:1D972C0", "publish:9001", "publish:9000", "stage" }, order);
        Assert.Equal(Arena + 0x1BB70, advanced.Frame.State.ReadDword(CursorGlobal));
    }

    private static NativeBattleBreath129Frame Frame()
    {
        NativeBattleEffectTaskPool[] pools = [NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1), NativeBattleEffectTaskPool.Initialize(0x2521180, Arena, 0x18, 9), NativeBattleEffectTaskPool.Initialize(0x2521190, Arena + 0xD8, 0x1E8, 3), NativeBattleEffectTaskPool.Initialize(0x25211A0, FibreAddress, 0x80, 16), NativeBattleEffectTaskPool.Initialize(0x25211B0, Arena + 0xE90, 0x20, 64), NativeBattleEffectTaskPool.Initialize(0x25211C0, MainAddress, 0x330, 1)];
        (pools[0], _) = pools[0].Append(0x6B4CB0);
        (pools[5], _) = pools[5].Append(0x6B5140);
        NativeBattleOriginalEffectState effect = new(129, 128, new(129, 0x6B4C00, 0x6B4C10), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = pools.ToImmutableArray() };
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 256; index++)
        {
            arena = arena.Write(0x19C0 + index * 28, BitConverter.GetBytes((ushort)0xFFFF));
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = arena, GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleBreath129State state = NativeBattleBreath129State.FromColdProcess().WriteDword(WorkingGlobal, Arena).WriteDword(CasterGlobal, 0x1D972C0) with { RootBankStorage = null };
        return new(memory, effect, new(55), state);
    }

    private static NativeBattleBreath129Resources Resources() => new(new("unused", []), new(0x12239B8, []), new(0x1225468, []), new(0x1226098, []), new(0x122688C, []), new(0, 0x12238C0, 0, 0, []), new("unused", []), [], [], [], [], [], [], [], 0);

    private static NativeBattleBreath129Inputs Inputs(List<string> order) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, actor) => new(checked((int)((actor - 0x1D972C0) / 156)), 3) { ActiveStagedResourceRow100 = 0x9000 + (actor - 0x1D972C0) / 156 }, (_, _) => throw new InvalidOperationException(), (frame, actor, selector, scale) =>
    {
        Assert.Equal(0x1D972C0u, actor);
        Assert.Equal(9, selector);
        Assert.Equal((short)0x2000, scale);
        order.Add("bone");
        return new(frame, NativeBattleEffectArena.Clear().Write(0, new byte[] { 1, 0, 2, 0, 3, 0 }).MarkUnknown(6, 2));
    }, (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (frame, row) =>
    {
        order.Add($"publish:{row:X}");
        return new(frame, new(row, 0, (_, _) => throw new InvalidOperationException(), []));
    }, (_, _, _, _) => throw new InvalidOperationException(), (frame, operation) =>
    {
        switch (operation)
        {
            case NativeBattleBreath129ComposeBody5095B0 composed:
                order.Add($"compose:{composed.ActorAddress:X}");
                break;
            case NativeBattleBreath129Stage5713E0 stage:
                Assert.Equal(0, stage.Accumulator);
                Assert.Equal(0, stage.Mode);
                order.Add("stage");
                break;
            default:
                throw new InvalidOperationException();
        }
        return frame;
    });
}
