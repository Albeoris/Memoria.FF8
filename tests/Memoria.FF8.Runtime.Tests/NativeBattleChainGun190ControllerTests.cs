using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleChainGun190Controller;
using static Memoria.FF8.Runtime.NativeBattleChainGun190Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChainGun190ControllerTests
{
    [Fact]
    public void Main_queries_current_bone_before_pause_preserving_global_W_and_age()
    {
        NativeBattleChainGun190Frame frame = Frame(0);
        frame = frame with { State = frame.State.WriteWord(BonePointGlobal + 6, 0xABCD) };
        List<(int Actor, int Selector, int Distance)> bones = [];
        NativeBattleChainGun190Inputs inputs = Inputs([], bones) with { GraphicsFlags = 0x201 };
        NativeBattleChainGun190Frame result = AdvanceMain(frame, ChildBank, Resources(), inputs).Frame;
        Assert.Equal(new[] { (3, 11, 6144) }, bones);
        Assert.Equal((ushort)0, Read(result, ChildBank).ReadWord(12));
        Assert.Equal((ushort)0xABCD, result.State.ReadWord(BonePointGlobal + 6));
        Assert.Equal(new byte[] { 11, 0, 3, 0, 0, 24 }, result.State.Read(BonePointGlobal, 6));
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(2, 1, 0)]
    [InlineData(30, 0, 0)]
    [InlineData(36, 0, 2)]
    public void Main_uses_original_sound_birth_contact_and_terminal_ages(ushort age, int births, int code)
    {
        NativeBattleChainGun190Frame frame = Frame(age);
        List<NativeBattleChainGun190Operation> operations = [];
        var step = AdvanceMain(frame, ChildBank, Resources(), Inputs(operations, []));
        Assert.Equal(code, step.Result);
        Assert.Equal(1 + births, step.Frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Equal((ushort)(age + 1), Read(step.Frame, ChildBank).ReadWord(12));
        if (age == 1)
        {
            Assert.Equal(new NativeBattleChainGun190Sound501330(0x61AD6, 0, 128), Assert.Single(operations));
        }
        if (age == 2)
        {
            Assert.Equal(0x5BB6F0u, Read(step.Frame, ChildBank + 36).Callback);
        }
        if (age == 30)
        {
            Assert.Equal(new NativeBattleChainGun190Contacts506BA0(0), Assert.Single(operations));
        }
    }

    [Theory]
    [InlineData(0, 1, 2, 0)]
    [InlineData(-1, 2, 4, 1)]
    [InlineData(2, 2, 2, 1)]
    public void Odd_births_select_actual_record_type_and_leave_inactive_cursor_unchanged(int recordKind, int births, int draws, ushort nextRecord)
    {
        NativeBattleChainGun190Frame frame = Frame(3);
        if (recordKind != 0)
        {
            frame = frame with { State = frame.State.WriteDword(RecordsAddress, 1).WriteDword(RecordsAddress + 4, unchecked((uint)recordKind)).WriteView(RecordsAddress + 8, PointView(new(100, 200, 300), 0x1234), 8) };
        }
        var step = AdvanceMain(frame, ChildBank, Resources(), Inputs([], []));
        Assert.Equal(1 + births, step.Frame.Effect.Pools[1].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(AfterDraws(frame.Random, draws), step.Frame.Random);
        Assert.Equal(nextRecord, Read(step.Frame, ChildBank).ReadWord(14));
        NativeBattleModulePoolTask flash = Read(step.Frame, ChildBank + 36);
        Assert.Equal(0x5BB5A0u, flash.Callback);
        Assert.Equal((ushort)123, flash.ReadWord(24));
        if (recordKind != 0)
        {
            NativeBattleModulePoolTask impact = Read(step.Frame, ChildBank + 72);
            Assert.Equal(recordKind < 0 ? 0x5BB9B0u : 0x5BBEC0u, impact.Callback);
            Assert.Equal((ushort)1, impact.ReadWord(24));
            Assert.Equal((ushort)0x1234, impact.ReadWord(22));
            Assert.Equal((ushort)0, impact.ReadWord(14));
        }
    }

    [Theory]
    [InlineData(0, 1251796034u)]
    [InlineData(5, 154509407u)]
    public void Fill_uses_native_even_parity_and_conditional_count_draw_preserving_bone_output_W(int count, uint seed)
    {
        NativeBattleChainGun190Frame frame = Frame(0);
        for (int index = 0; index < 4; index++)
        {
            frame = frame with { State = frame.State.WriteWord(RecordsAddress + (uint)(index * 16 + 14), 0xBEEF) };
        }
        List<(int Actor, int Selector, int Distance)> bones = [];
        NativeBattleChainGun190Frame result = FillShots(frame, 0, 4, 2, Inputs([], bones) with { ReadBoneCount = (_, _) => count });
        Assert.Equal(new NativeCrtRandomState(seed), result.Random);
        Assert.Equal(uint.MaxValue, result.State.ReadDword(RecordsAddress + 4));
        Assert.Equal((ushort)702, result.State.ReadWord(RecordsAddress + 14));
        Assert.Equal(new[] { 2, 2, 2 }, bones.Select(call => call.Actor));
        Assert.Equal(count == 0 ? new[] { 0, 0, 0 } : new[] { 2, 0, 0 }, bones.Select(call => call.Selector));
        Assert.Equal(count == 0 ? new[] { 865, 2911, 2716 } : new[] { 247, 2487, 2357 }, bones.Select(call => call.Distance));
        for (int index = 1; index < 4; index++)
        {
            Assert.Equal((ushort)0xBEEF, result.State.ReadWord(RecordsAddress + (uint)(index * 16 + 14)));
        }
    }

    [Fact]
    public void Interpolation_truncates_signed_steps_and_preserves_unknown_or_known_W_without_rng()
    {
        NativeBattleChainGun190Frame frame = Frame(0);
        frame = frame with { State = frame.State.WriteWord(RecordsAddress + 14, 0x1234).WriteView(RecordsAddress + 30, NativeBattleEffectArena.Clear().MarkUnknown(0, 2), 2) };
        NativeBattleChainGun190Frame result = InterpolateShots(frame, 0, 2, PointView(new(5, 100, -5)), PointView(new(-5, -200, 5)));
        Assert.Equal((ushort)2, result.State.ReadWord(RecordsAddress + 8));
        Assert.Equal((short)-2, unchecked((short)result.State.ReadWord(RecordsAddress + 12)));
        Assert.Equal((short)-1, unchecked((short)result.State.ReadWord(RecordsAddress + 24)));
        Assert.Equal((ushort)1, result.State.ReadWord(RecordsAddress + 28));
        Assert.Equal((ushort)0, result.State.ReadWord(RecordsAddress + 10));
        Assert.Equal((ushort)0x1234, result.State.ReadWord(RecordsAddress + 14));
        Assert.Throws<NotSupportedException>(() => result.State.ReadWord(RecordsAddress + 30));
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Pair_setup_falls_through_to_physical_third_contact_after_retaining_pair_rng()
    {
        NativeBattleChainGun190Frame frame = Frame(0);
        NativeBattleActionPacketsState pair = Packets(2, false);
        pair = pair with { Contacts = pair.Contacts.SetItem(2, Contact(2)) };
        NativeBattleChainGun190Inputs inputs = Inputs([], []) with { Packets = pair, ReadBoneCount = (_, _) => 0 };
        NativeBattleChainGun190Frame pairResult = PrepareShots(frame, inputs);
        NativeBattleChainGun190Frame tripleResult = PrepareShots(frame, inputs with { Packets = Packets(3, false) });
        Assert.Equal(new NativeCrtRandomState(1852780124), pairResult.Random);
        Assert.Equal(new NativeCrtRandomState(4145464656), tripleResult.Random);
        Assert.Equal((ushort)133, pairResult.State.ReadWord(RecordsAddress + 2 * 16 + 8));
        Assert.Equal((ushort)166, pairResult.State.ReadWord(RecordsAddress + 3 * 16 + 8));
        Assert.Equal((ushort)134, pairResult.State.ReadWord(RecordsAddress + 7 * 16 + 8));
        Assert.Throws<NotSupportedException>(() => PrepareShots(frame, inputs with { Packets = Packets(2, false) }));
        NativeBattleChainGun190Frame cold = PrepareShots(frame, inputs with { Packets = Packets(2, true) });
        Assert.Equal((ushort)33, cold.State.ReadWord(RecordsAddress + 2 * 16 + 8));
    }

    [Fact]
    public void Setup_clears_all_forty_eight_heads_only_and_json_continues_identically()
    {
        NativeBattleChainGun190Frame frame = Frame(0);
        frame = frame with { State = frame.State.WriteDword(RecordsAddress + 47 * 16, 7).WriteDword(RecordsAddress + 47 * 16 + 4, 0xCAFE).WriteWord(RecordsAddress + 14, 0x5678) };
        frame = PrepareShots(frame, Inputs([], []) with { Packets = Packets(1, true) });
        Assert.Equal(0u, frame.State.ReadDword(RecordsAddress + 47 * 16));
        Assert.Equal(0xCAFEu, frame.State.ReadDword(RecordsAddress + 47 * 16 + 4));
        frame = Write(frame, ChildBank, Read(frame, ChildBank).WriteWord(12, 3));
        NativeBattleChainGun190Frame restored = JsonSerializer.Deserialize<NativeBattleChainGun190Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleChainGun190Frame expected = AdvanceMain(frame, ChildBank, Resources(), Inputs([], [])).Frame;
        NativeBattleChainGun190Frame actual = AdvanceMain(restored, ChildBank, Resources(), Inputs([], [])).Frame;
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Fact]
    public void Root_uses_private_bank_parity_and_transfers_banks_after_actual_drain_without_common_alias()
    {
        NativeBattleChainGun190Frame frame = Frame(0);
        NativeBattleEffectArena common = NativeBattleEffectArena.Clear().Write(0, new byte[] { 0x77 });
        frame = frame with { Memory = frame.Memory with { Arena = common }, State = frame.State.Write(PacketBase, new byte[] { 0xA5 }).Write(PacketBase + 0x8000, new byte[] { 0x5A }) };
        NativeBattleChainGun190Inputs paused = Inputs([], []) with { GraphicsFlags = 0x201 };
        frame = Advance(frame, Resources(), paused);
        Assert.Equal(PacketBase, frame.State.ReadDword(CursorGlobal));
        frame = Advance(frame, Resources(), paused);
        Assert.Equal(PacketBase + 0x8000u, frame.State.ReadDword(CursorGlobal));
        NativeBattleEffectTaskPool empty = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, empty) } };
        frame = Advance(frame, Resources(), paused);
        Assert.Empty(frame.Effect.Pools);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.NotNull(frame.State.RootBankStorage);
        Assert.NotNull(frame.State.ChildBankStorage);
        Assert.Same(common, frame.Memory.Arena);
        Assert.Equal(new byte[] { 0xA5 }, frame.State.Read(PacketBase, 1));
        Assert.Equal(new byte[] { 0x5A }, frame.State.Read(PacketBase + 0x8000, 1));
    }

    private static NativeBattleChainGun190Frame Frame(ushort age)
    {
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, RootCount);
        (roots, _) = roots.Append(RootCallback);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, ChildStride, ChildCount);
        (children, _) = children.Append(MainCallback);
        children = children with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, age)) };
        NativeBattleOriginalEffectState effect = new(190, 189, new(190, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = [roots, children] };
        NativeBattleChainGun190State state = NativeBattleChainGun190State.FromColdProcess().WriteDword(CasterGlobal, 3) with { RootBankStorage = null, ChildBankStorage = null };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(55), state);
    }

    private static NativeBattleChainGun190Resources Resources() => new(null!, null!, null!, null!, null!, null!, null!, 0x61AD6);

    private static NativeBattleChainGun190Inputs Inputs(List<NativeBattleChainGun190Operation> operations, List<(int Actor, int Selector, int Distance)> bones) => new(Packets(1, true), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, actor) => new(actor, 3) { Rotation14 = 123, Transform = new(new(0, 0, 0), new((short)(actor * 100), 200, (short)(actor * 300)), (short)(700 + actor), 0, 4096) }, (_, _) => throw new InvalidOperationException(), (_, _) => 5, (current, actor, selector, distance) =>
    {
        bones.Add((actor, selector, distance));
        return new(current, PointView(new((short)selector, (short)actor, (short)distance)));
    }, (current, operation) =>
    {
        operations.Add(operation);
        return current;
    });

    private static NativeBattleActionPacketsState Packets(byte count, bool cold)
    {
        NativeBattleActionPacketsState state = cold ? NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]) : NativeBattleActionPacketsState.Fresh();
        (state, _) = state.Prepare(new(3, 8, 14, -1, 154, 190, null));
        for (byte actor = 0; actor < count; actor++)
        {
            (state, _) = state.AppendContact(Contact(actor));
        }
        (state, _) = state.Publish(count);
        return state;
    }

    private static NativeBattleContactPacket Contact(byte actor) => new(new(actor, 4, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0));

    private static NativeCrtRandomState AfterDraws(NativeCrtRandomState random, int count)
    {
        for (int index = 0; index < count; index++)
        {
            random = random.Draw(out _);
        }
        return random;
    }
}
