using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect69Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect69ControllerTests
{
    [Theory]
    [InlineData(0, 0x47020, 1)]
    [InlineData(1, 0x3F020, 0)]
    [InlineData(255, 0x3F020, 0)]
    public void Root_selects_packet_bank_from_actual_byte_before_incrementing_clock(byte parity, uint offset, byte next)
    {
        NativeBattleEffect69Frame frame = Active(false);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0xE, parity));
        NativeBattleEffect69Step step = AdvanceRoot(frame, RootBank, null!, new Harness().Inputs());
        Assert.Equal(0x20DFAB8u + offset, step.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(step.Frame, RootBank).ReadByte(0xE));
        Assert.Equal((ushort)1, Read(step.Frame, RootBank).ReadWord(0xC));
        Assert.Equal(0, step.Result);
        Assert.Single(step.Frame.Effect.Pools);
        Assert.NotNull(step.Frame.State.FixedBanks[MainBank]);
    }

    [Fact]
    public void Private_random_uses_retained_seed_and_leaves_shared_crt_unchanged()
    {
        NativeBattleEffect69Frame frame = Active(false);
        frame = frame with { State = frame.State.WriteDword(RandomGlobal, 0xFFFFFFFF) };
        (NativeBattleEffect69Frame next, int value) = NextPrivateRandom6FFFC0(frame);
        Assert.Equal(unchecked((int)((0xFFFFFFFFu * 125u + 14u) & 0x7FFF)), value);
        Assert.Equal((uint)value, next.State.ReadDword(RandomGlobal));
        Assert.Same(frame.Random, next.Random);
        Assert.Equal(0xFFFFFFFFu, frame.State.ReadDword(RandomGlobal));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    public void Terminal_main_draws_before_pause_and_retires_without_increment(uint flags, int result)
    {
        Harness harness = new() { Flags = flags };
        NativeBattleEffect69Frame frame = AtAge(126);
        NativeBattleEffect69Step step = AdvanceMain(frame, MainBank, null!, harness.Inputs());
        Assert.Equal(result, step.Result);
        Assert.Equal((ushort)126, Read(step.Frame, MainBank).ReadWord(0xC));
        Assert.Collection(harness.Operations,
            operation => Assert.Equal(new NativeBattleEffect69StageFlags(1, 0xFF, 2), operation),
            operation => Assert.Equal(new NativeBattleEffect69StageFlags(2, 0xFF, 2), operation),
            operation => Assert.IsType<NativeBattleEffect69DeliverCommonBatch>(operation));
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), step.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Theory]
    [InlineData(-1, 110, false)]
    [InlineData(0, 111, true)]
    public void Stage_reload_waits_on_actual_signed_io_result_after_drawing(int io, ushort age, bool reload)
    {
        Harness harness = new() { Io = io };
        NativeBattleEffect69Step step = AdvanceMain(AtAge(110), MainBank, null!, harness.Inputs());
        Assert.Equal(age, Read(step.Frame, MainBank).ReadWord(0xC));
        Assert.Equal(reload, harness.Operations.OfType<NativeBattleEffect69ReloadStage512AC0>().Any());
        if (reload)
        {
            Assert.Equal(new NativeBattleEffect69ReloadStage512AC0(0, 0x20E7AD8), Assert.Single(harness.Operations.OfType<NativeBattleEffect69ReloadStage512AC0>()));
            Assert.Equal(new NativeBattleEffect69ModelFlags(4, 0xFFFF, 0x20, false), Assert.Single(harness.Operations.OfType<NativeBattleEffect69ModelFlags>()));
            Assert.Equal(new NativeBattleEffect69ModelColors44(0x32123456), Assert.Single(harness.Operations.OfType<NativeBattleEffect69ModelColors44>()));
            Assert.Equal(new NativeBattleEffect69ModelPositionZ(4, -2722, true), Assert.Single(harness.Operations.OfType<NativeBattleEffect69ModelPositionZ>()));
        }
        Assert.IsType<NativeBattleEffect69DeliverCommonBatch>(harness.Operations[2]);
    }

    [Fact]
    public void Physical_bank_reads_and_writes_use_active_pool_payload_only()
    {
        NativeBattleEffect69Frame frame = AtAge(126);
        frame = WriteBytes(frame, MainBank + 0x8EE, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0xBEEF)), 2);
        Assert.Equal((ushort)0xBEEF, Read(frame, MainBank).ReadWord(0x8EE));
        Assert.Null(frame.State.FixedBanks[MainBank]);
        Assert.Throws<InvalidDataException>(() => WriteBytes(frame, MainBank + 4, NativeBattleEffectArena.Clear(), 4));
    }

    private static NativeBattleEffect69Frame AtAge(ushort age)
    {
        NativeBattleEffect69Frame frame = Active(true);
        return Write(frame, MainBank, Read(frame, MainBank).WriteWord(0xC, age));
    }

    private static NativeBattleEffect69Frame Active(bool main)
    {
        NativeBattleOriginalEffectState effect = new(69, 68, new(69, Entry, Initializer), new(4, 0, 0x7F, 0, 0, 0), null);
        NativeBattleEffect69State state = NativeBattleEffect69State.FromColdProcess().WriteDword(ArenaGlobal, 0x20DFAB8).WriteDword(CasterGlobal, 0x1D972C0 + 4 * 156);
        var pools = ImmutableArray.CreateBuilder<NativeBattleEffectTaskPool>();
        for (int index = 0; index < (main ? 2 : 1); index++)
        {
            NativeBattleCureFamilyPool shape = Pools[index];
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity);
            (pool, int slot) = pool.Append(index == 0 ? RootCallback : MainCallback);
            NativeBattleModulePoolTask task = pool.Slots[slot];
            for (int offset = 12; offset < shape.Stride; offset++)
            {
                task = task.WriteByte(offset, 0);
            }
            pools.Add(pool with { Slots = pool.Slots.SetItem(slot, task) });
            state = state.DetachBank(index);
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        return new(memory, effect with { Pools = pools.ToImmutable(), ReturnedDescriptor = RootDescriptor }, NativeCrtRandomState.FromColdProcess(), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private sealed class Harness
    {
        public uint Flags { get; init; }
        public int Io { get; init; }
        public List<NativeBattleEffect69Operation> Operations { get; } = [];

        public NativeBattleEffect69Inputs Inputs() => new(new(0x00808080, 0x32808080, 1, 0), frame => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), frame => new(null, 100, 160, 108), (frame, actor) => new(actor, 2), (frame, actor) => throw new NotSupportedException(), frame => Flags, frame => 0, (frame, source) => 0x32123456, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        }) { ReadIoResult = frame => Io };
    }
}
