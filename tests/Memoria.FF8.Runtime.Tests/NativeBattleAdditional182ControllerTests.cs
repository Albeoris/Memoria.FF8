using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleAdditional182Controller;
using static Memoria.FF8.Runtime.NativeBattleAdditional182Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAdditional182ControllerTests
{
    [Theory]
    [InlineData(0, 1536, 0)]
    [InlineData(-4096, 1536, -2200)]
    [InlineData(4096, 4096, 2200)]
    [InlineData(8192, 4352, 2337)]
    public void Initialization_uses_live_bounds_and_growth_before_lower_scale_clamp(short scale, ushort displayed, short growth)
    {
        NativeBattleAdditional182Frame frame = Frame(MainSetCallback);
        List<NativeBattleAdditional182Operation> operations = [];
        NativeBattleAdditional182Inputs inputs = Inputs(operations, scale);
        Assert.Equal(frame, AdvanceMain(frame, ChildBank, Resources(), inputs with { GraphicsFlags = 0x201 }).Frame);
        frame = AdvanceMain(frame, ChildBank, Resources(), inputs).Frame;
        NativeBattleModulePoolTask task = Read(frame, ChildBank);
        Assert.Equal((ushort)8, task.ReadWord(14));
        Assert.Equal(new byte[] { 100, 0, 200, 0, 44, 1 }, TaskView(task, 16, 6).Read(0, 6));
        Assert.Equal((short)-700, Signed(task, 22));
        Assert.Equal(displayed, task.ReadWord(28));
        Assert.Equal(growth, Signed(task, 30));
        Assert.Equal(new NativeBattleAdditional182ActorFlags(3, 8, 0), operations[0]);
        Assert.Equal(new NativeBattleAdditional182Stage5713E0(0, 0), operations[1]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Opening_preserves_unwritten_second_ring_growth_and_consumes_sixty_three_draws(bool set)
    {
        NativeBattleAdditional182Frame frame = Frame(set ? MainSetCallback : MainClearCallback, 1);
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        children = children with { Slots = children.Slots.SetItem(3, children.Slots[3].WriteWord(30, 0xABCD).MarkPayloadUnknown(32, 2)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children) } };
        List<NativeBattleAdditional182Operation> operations = [];
        NativeBattleAdditional182Frame result = AdvanceMain(frame, ChildBank, Resources(), Inputs(operations)).Frame;
        Assert.Equal(16, result.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(new uint[] { 0x5CA490, 0x5CA6B0, 0x5CA6B0 }, result.Effect.Pools[1].Slots.Skip(1).Take(3).Select(task => task.Callback));
        Assert.Equal((ushort)0xABCD, Read(result, ChildBank + 108).ReadWord(30));
        Assert.Throws<NotSupportedException>(() => Read(result, ChildBank + 108).ReadWord(32));
        NativeBattleModulePoolTask first = Read(result, ChildBank + 72);
        NativeBattleModulePoolTask second = Read(result, ChildBank + 108);
        Assert.Equal(-Signed(first, 26), Signed(second, 26));
        Assert.Equal((short)160, Signed(first, 28));
        Assert.Equal(first.ReadWord(28), first.ReadWord(30));
        Assert.Equal(first.ReadWord(28), second.ReadWord(28));
        Assert.Equal(AfterDraws(frame.Random, 63), result.Random);
        Assert.Equal(new ushort[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, result.Effect.Pools[1].Slots.Skip(4).Take(12).Select(task => task.ReadWord(14)));
        Assert.All(result.Effect.Pools[1].Slots.Skip(4).Take(12), task =>
        {
            int speed = Signed(task, 26);
            Assert.Equal((Math.Abs(speed) & 1) != 0, speed < 0);
            Assert.Equal(Signed(task, 28) / 10, Signed(task, 30));
            Assert.Equal(task.ReadWord(32), task.ReadWord(34));
        });
        Assert.Equal(new NativeBattleAdditional182Stage5713E0(256, 0), operations[0]);
        NativeBattleAdditional182Sound5013A0 sound = Assert.IsType<NativeBattleAdditional182Sound5013A0>(operations[1]);
        Assert.Equal(set ? 0x494C1u : 0x494ABu, sound.Packed);
        Assert.Equal(PointView(new(100, 200, 300), 777).Read(0, 8), sound.Point8.Read(0, 8));
    }

    [Theory]
    [InlineData(true, 35)]
    [InlineData(false, 64)]
    public void Contacts_restore_only_saved_mask_before_applying_captured_header(bool set, ushort age)
    {
        NativeBattleAdditional182Frame frame = Frame(set ? MainSetCallback : MainClearCallback, age);
        List<NativeBattleAdditional182Operation> operations = [];
        frame = AdvanceMain(frame, ChildBank, Resources(), Inputs(operations)).Frame;
        Assert.Equal(new NativeBattleAdditional182ActorFlags(3, 0, 8), operations[0]);
        Assert.Equal(new NativeBattleAdditional182ContactsAll(0), operations[1]);
        Assert.Equal((ushort)(age + 1), Read(frame, ChildBank).ReadWord(12));
    }

    [Theory]
    [InlineData(true, 8, 2048, 0)]
    [InlineData(true, 32, 2048, 0)]
    [InlineData(true, 40, 0, 2)]
    [InlineData(false, 8, 2048, 0)]
    [InlineData(false, 57, 2048, 0)]
    [InlineData(false, 65, 0, 2)]
    public void Branches_keep_distinct_stage_and_terminal_ages(bool set, ushort age, int stage, int code)
    {
        List<NativeBattleAdditional182Operation> operations = [];
        var result = AdvanceMain(Frame(set ? MainSetCallback : MainClearCallback, age), ChildBank, Resources(), Inputs(operations));
        Assert.Equal(code, result.Result);
        int[] stages = operations.OfType<NativeBattleAdditional182Stage5713E0>().Select(operation => operation.Value).ToArray();
        Assert.Equal(code == 2 ? new int[] { stage, 0 } : new int[] { stage }, stages);
    }

    [Fact]
    public void Clear_later_births_use_live_actor_mask_and_exact_random_counts_without_clearing_suffixes()
    {
        NativeBattleAdditional182Frame frame = Frame(MainClearCallback, 20);
        frame = AdvanceMain(frame, ChildBank, Resources(), Inputs([])).Frame;
        NativeBattleModulePoolTask fade = Read(frame, ChildBank + 36);
        Assert.Equal(0x5CB390u, fade.Callback);
        Assert.Equal((ushort)3, fade.ReadWord(14));
        Assert.Equal((ushort)6, fade.ReadWord(24));
        Assert.Equal((ushort)0x800, fade.ReadWord(26));
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, 24));
        NativeCrtRandomState random = frame.Random;
        frame = AdvanceMain(frame, ChildBank, Resources(), Inputs([])).Frame;
        Assert.Equal(AfterDraws(random, 72), frame.Random);
        NativeBattleModulePoolTask[] producers = frame.Effect.Pools[1].Slots.Skip(2).Take(12).ToArray();
        Assert.All(producers, task => Assert.Equal(0x5CAED0u, task.Callback));
        Assert.Equal(Enumerable.Range(0, 12).Select(index => (ushort)(1 << index)), producers.Select(task => task.ReadWord(14)));
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, 26));
        frame = frame with { State = frame.State.WriteDword(HistoryAddress, 0xFFFFFFFF).WriteWord(HistoryAddress + 4, 0xCAFE) };
        random = frame.Random;
        frame = AdvanceMain(frame, ChildBank, Resources(), Inputs([])).Frame;
        Assert.Equal(AfterDraws(random, 3), frame.Random);
        for (int index = 0; index < 5; index++)
        {
            Assert.Equal(0u, frame.State.ReadDword(HistoryAddress + (uint)(index * 36)));
        }
        Assert.Equal((ushort)0xCAFE, frame.State.ReadWord(HistoryAddress + 4));
        NativeBattleModulePoolTask history = Read(frame, ChildBank + 14 * 36);
        Assert.Equal(0x5CB130u, history.Callback);
        Assert.Equal((short)-1200, Signed(history, 18));
    }

    [Fact]
    public void Json_continuation_preserves_random_unknown_suffix_and_root_detaches_only_drained_banks()
    {
        NativeBattleAdditional182Frame frame = Frame(MainClearCallback, 24);
        NativeBattleAdditional182Frame restored = JsonSerializer.Deserialize<NativeBattleAdditional182Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleAdditional182Frame expected = AdvanceMain(frame, ChildBank, Resources(), Inputs([])).Frame;
        NativeBattleAdditional182Frame actual = AdvanceMain(restored, ChildBank, Resources(), Inputs([])).Frame;
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children) } };
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        frame = Advance(frame, Resources(), Inputs([]));
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Empty(frame.Effect.Pools);
        Assert.NotNull(frame.State.RootBankStorage);
        Assert.NotNull(frame.State.ChildBankStorage);
        Assert.Equal(ArenaAddress + 0xC000u, frame.State.ReadDword(CursorGlobal));
        Assert.Equal((ushort)2, BitConverter.ToUInt16(frame.State.RootBankStorage!.Read(12, 2)));
    }

    [Fact]
    public void Second_ring_keeps_unknown_growth_and_json_retains_the_unknown_owner()
    {
        NativeBattleAdditional182Frame frame = Frame(MainSetCallback, 1);
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        children = children with { Slots = children.Slots.SetItem(3, children.Slots[3].MarkPayloadUnknown(30, 2)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children) } };
        frame = AdvanceMain(frame, ChildBank, Resources(), Inputs([])).Frame;
        frame = JsonSerializer.Deserialize<NativeBattleAdditional182Frame>(JsonSerializer.Serialize(frame))!;
        Assert.Throws<NotSupportedException>(() => Read(frame, ChildBank + 108).ReadWord(30));
        Assert.Equal((ushort)160, Read(frame, ChildBank + 108).ReadWord(28));
    }
    private static NativeBattleAdditional182Frame Frame(uint callback, ushort age = 0)
    {
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        (roots, _) = roots.Append(RootCallback);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (children, _) = children.Append(callback);
        children = children with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, age).WriteWord(14, 8).WriteWord(16, 100).WriteWord(18, 200).WriteWord(20, 300).WriteWord(22, unchecked((ushort)-700)).WriteWord(28, 4096).WriteWord(30, 2200)) };
        NativeBattleOriginalEffectState effect = new(182, 181, new(182, Entry, Initializer), new(0, 0, 8, 0, 0, 0), RootDescriptor) { Pools = [roots, children] };
        NativeBattleAdditional182State state = NativeBattleAdditional182State.FromColdProcess().WriteDword(FirstTargetGlobal, 3).WriteDword(WorkingGlobal, ArenaAddress) with { RootBankStorage = null, ChildBankStorage = null };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(55), state);
    }

    private static NativeBattleAdditional182Resources Resources() => new(null!, null!, [], null!, null!, null!, null!, 0x494AB, 0x494C1, []);

    private static NativeBattleAdditional182Inputs Inputs(List<NativeBattleAdditional182Operation> operations, short scale = 4096) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, actor) => new(actor, 0x80B) { GeometryBounds52 = new(new(0, -1000, 0), new(0, -700, 0)), Transform = new(new(0, 0, 0), new(100, 200, 300), 0, 0, scale) }, (current, actor) => new(current, PointView(new(100, 200, 300), 777)), (current, operation) =>
    {
        operations.Add(operation);
        return current;
    });

    private static NativeCrtRandomState AfterDraws(NativeCrtRandomState random, int count)
    {
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random;
    }
}
