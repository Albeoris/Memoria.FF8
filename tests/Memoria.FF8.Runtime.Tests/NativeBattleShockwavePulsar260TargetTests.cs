using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleShockwavePulsar260Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShockwavePulsar260TargetTests
{
    [Fact]
    public void Rising_target_advances_ACT_before_motion_and_preserves_partial_context_and_rotation_tail()
    {
        Fixture fixture = new(TargetCallback, 0);
        NativeBattleShockwavePulsar260TaskStep step = fixture.Advance(fixture.Frame);
        NativeBattleShockwavePulsar260ActNode draw = Assert.Single(fixture.Operations.OfType<NativeBattleShockwavePulsar260ActNode>());
        Assert.Equal(0x111F09Cu, draw.SourceAddress);
        Assert.Equal((short)1000, Word(draw.Context20, 0));
        Assert.Equal((short)1850, Word(draw.Context20, 2));
        Assert.Equal((short)3000, Word(draw.Context20, 4));
        Assert.Equal((short)-1024, Word(draw.Context20, 0xC));
        Assert.Equal(0x112D190u, Dword(draw.Context20, 8));
        Assert.Equal(ArenaAddress + MorphOffset, Dword(draw.Context20, 0x10));
        Assert.Throws<NotSupportedException>(() => draw.Context20.Read(6, 2));
        Assert.Throws<NotSupportedException>(() => draw.Context20.Read(0xE, 2));
        Assert.Equal((short)4246, Word(fixture.Positions[3], 2));
        Assert.Equal((ushort)612, NativeBattleShockwavePulsar260Controller.ReadWord(step.Frame, RecordAddress + 0xC));
        Assert.Equal((ushort)312, NativeBattleShockwavePulsar260Controller.ReadWord(step.Frame, RecordAddress + 0xE));
        Assert.Throws<NotSupportedException>(() => NativeBattleShockwavePulsar260Controller.ReadView(step.Frame, RecordAddress + 0x12, 2).Read(0, 2));
        Assert.Equal((ushort)1, NativeBattleShockwavePulsar260Controller.Read(step.Frame, TaskAddress).ReadWord(0x10));
        Assert.Equal(1u, NativeBattleShockwavePulsar260Controller.Read(step.Frame, TaskAddress).ReadDword(0x18));
        Assert.Equal(0, step.Result);
        Assert.Collection(fixture.Operations, operation => Assert.IsType<NativeBattleShockwavePulsar260ActNode>(operation), operation => Assert.IsType<NativeBattleShockwavePulsar260ActorWrite>(operation));
    }

    [Fact]
    public void Rising_age29_finishes_at_new30_and_clears_only_native_flag8()
    {
        Fixture fixture = new(TargetCallback, 29);
        NativeBattleShockwavePulsar260TaskStep step = fixture.Advance(fixture.Frame);
        Assert.Equal(2, step.Result);
        Assert.Equal((short)150, Word(fixture.Positions[3], 2));
        Assert.Equal((ushort)30, NativeBattleShockwavePulsar260Controller.Read(step.Frame, TaskAddress).ReadWord(0x10));
        Assert.Equal((ushort)3, fixture.Models[3].Flags0);
        Assert.Equal(new NativeBattleShockwavePulsar260ActorFlags(3, 8, 0), fixture.Operations.Last());
        Assert.Equal(fixture.Frame.Random, step.Frame.Random);
    }

    [Fact]
    public void Rising_age5_emits16_puffs_with_two_CRT_draws_per_successful_slot_in_native_angle_order()
    {
        Fixture fixture = new(TargetCallback, 5);
        NativeCrtRandomState expected = fixture.Frame.Random.Draw(out ushort firstRadius).Draw(out ushort firstKind);
        for (int index = 2; index < 32; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleShockwavePulsar260TaskStep step = fixture.Advance(fixture.Frame);
        Assert.Equal(expected, step.Frame.Random);
        byte[] first = step.Frame.Memory.Arena!.Read(PuffsOffset, 16);
        Assert.Equal(0x111CF4Cu, BinaryPrimitives.ReadUInt32LittleEndian(first));
        Assert.Equal((short)1000, BinaryPrimitives.ReadInt16LittleEndian(first.AsSpan(4)));
        Assert.Equal((short)2000, BinaryPrimitives.ReadInt16LittleEndian(first.AsSpan(6)));
        Assert.Equal((short)3000, BinaryPrimitives.ReadInt16LittleEndian(first.AsSpan(8)));
        Assert.Equal((ushort)((firstKind & 0x800) + 6000), BinaryPrimitives.ReadUInt16LittleEndian(first.AsSpan(10)));
        Assert.Equal((byte)0, first[12]);
        Assert.Equal((byte)0, first[13]);
        Assert.Equal((byte)(2 * ((firstRadius & 15) + 16)), first[14]);
        Assert.Equal((byte)16, first[15]);
        Assert.Equal(ArenaAddress + PuffsOffset + 256u, step.Frame.State.ReadDword(0x2508614));
        Assert.Equal((byte)16, step.Frame.Memory.Arena.Read(PuffsOffset + 15 * 16 + 15, 1)[0]);
        Assert.Equal((byte)0, step.Frame.Memory.Arena.Read(PuffsOffset + 16 * 16 + 15, 1)[0]);
        Assert.Throws<NotSupportedException>(() => step.Frame.Memory.Arena.Read(PuffsOffset + 16 * 16, 4));
    }

    [Fact]
    public void Full_puff_bank_stops_before_random_consumption_and_target_still_moves()
    {
        Fixture fixture = new(TargetCallback, 5);
        NativeBattleEffectArena arena = fixture.Frame.Memory.Arena!;
        for (int index = 0; index < PuffsCount; index++)
        {
            arena = arena.Write(PuffsOffset + index * 16 + 15, [1]);
        }
        NativeBattleShockwavePulsar260Frame full = fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = arena } };
        NativeBattleShockwavePulsar260TaskStep step = fixture.Advance(full);
        Assert.Equal(full.Random, step.Frame.Random);
        Assert.Equal((ushort)6, NativeBattleShockwavePulsar260Controller.Read(step.Frame, TaskAddress).ReadWord(0x10));
        Assert.NotEqual((short)2000, Word(fixture.Positions[3], 2));
    }

    [Fact]
    public void Falling_early_scale_uses13_callbacks_then_draws_and_hides_without_terminating()
    {
        Fixture fixture = new(LateTargetCallback, 6);
        NativeBattleShockwavePulsar260TaskStep step = fixture.Advance(fixture.Frame);
        Assert.Equal(3151u, NativeBattleShockwavePulsar260Controller.ReadDword(step.Frame, RecordAddress + 0x1C));
        Assert.Equal((short)2000, Word(fixture.Positions[3], 2));
        NativeBattleShockwavePulsar260DrawBody body = Assert.Single(fixture.Operations.OfType<NativeBattleShockwavePulsar260DrawBody>());
        Assert.Equal((short)3151, body.Scale);
        Assert.Equal((short)-151, Word(body.Offset8, 2));
        Assert.Throws<NotSupportedException>(() => body.Offset8.Read(6, 2));
        Assert.Throws<NotSupportedException>(() => body.Rotation8.Read(6, 2));
        Assert.Equal(new NativeBattleShockwavePulsar260ActorFlags(3, 0, 4), fixture.Operations.Last());
        Assert.Equal((ushort)15, fixture.Models[3].Flags0);
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void Falling_elapsed0_appends_actual_child_and_draws_active_ring_before_hiding_each_member()
    {
        Fixture fixture = new(LateTargetCallback, 13, linked: true);
        NativeBattleShockwavePulsar260TaskStep step = fixture.Advance(fixture.Frame);
        NativeBattleModulePoolTask child = step.Frame.Effect.Pools[2].Slots[1];
        Assert.Equal(ChildActCallback, child.Callback);
        Assert.Equal((ushort)1000, child.ReadWord(0xC));
        Assert.Equal((ushort)0, child.ReadWord(0xE));
        Assert.Equal((ushort)3000, child.ReadWord(0x10));
        Assert.Equal(0x111D424u, child.ReadDword(0x14));
        Assert.Equal(0u, child.ReadDword(0x18));
        Assert.Equal((short)700, Word(fixture.Positions[3], 2));
        Assert.Equal(new byte[] { 3, 5 }, fixture.Operations.OfType<NativeBattleShockwavePulsar260DrawBody>().Select(operation => operation.Actor));
        NativeBattleShockwavePulsar260Operation[] bodyAndFlags = fixture.Operations.Where(operation => operation is NativeBattleShockwavePulsar260DrawBody or NativeBattleShockwavePulsar260ActorFlags).ToArray();
        Assert.IsType<NativeBattleShockwavePulsar260DrawBody>(bodyAndFlags[0]);
        Assert.IsType<NativeBattleShockwavePulsar260DrawBody>(bodyAndFlags[1]);
        Assert.Equal(new NativeBattleShockwavePulsar260ActorFlags(3, 0, 4), bodyAndFlags[2]);
        Assert.Equal(new NativeBattleShockwavePulsar260ActorFlags(5, 0, 4), bodyAndFlags[3]);
        Assert.Equal((ushort)0, fixture.Models[4].Flags0);
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void Falling_exact_duration_sets_terminal_height_then_descends256_per_callback_without_retiring()
    {
        Fixture fixture = new(LateTargetCallback, 23);
        NativeBattleShockwavePulsar260TaskStep first = fixture.Advance(fixture.Frame);
        Assert.Equal((short)-12394, Word(fixture.Positions[3], 2));
        Assert.Equal(0, first.Result);
        NativeBattleShockwavePulsar260TaskStep second = fixture.Advance(first.Frame);
        Assert.Equal((short)-12650, Word(fixture.Positions[3], 2));
        Assert.Equal(0, second.Result);
        Assert.Equal(1, second.Frame.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
    }

    [Fact]
    public void Child_returns2_only_when_actual_ACT_remaining_reaches_zero_and_keeps_XYZ_context_padding_unknown()
    {
        Fixture fixture = new(ChildActCallback, 0, animationDuration: 1);
        NativeBattleShockwavePulsar260TaskStep step = fixture.Advance(fixture.Frame);
        Assert.Equal(2, step.Result);
        NativeBattleShockwavePulsar260ActNode node = Assert.Single(fixture.Operations.OfType<NativeBattleShockwavePulsar260ActNode>());
        Assert.Equal(0x111D424u, node.SourceAddress);
        Assert.Equal((short)111, Word(node.Context20, 0));
        Assert.Equal((short)222, Word(node.Context20, 2));
        Assert.Equal((short)333, Word(node.Context20, 4));
        Assert.Equal((short)-128, Word(node.Context20, 0xC));
        Assert.Throws<NotSupportedException>(() => node.Context20.Read(6, 2));
        Assert.Throws<NotSupportedException>(() => node.Context20.Read(0xE, 2));
        Assert.Equal((ushort)333, NativeBattleShockwavePulsar260Controller.Read(step.Frame, TaskAddress).ReadWord(0x10));
    }

    [Fact]
    public void Active_target_JSON_next_retains_workspace_record_actor_motion_and_random()
    {
        Fixture fixture = new(LateTargetCallback, 19);
        NativeBattleShockwavePulsar260TaskStep first = fixture.Advance(fixture.Frame);
        NativeBattleShockwavePulsar260Frame restored = JsonSerializer.Deserialize<NativeBattleShockwavePulsar260Frame>(JsonSerializer.Serialize(first.Frame))!;
        Fixture other = new(LateTargetCallback, 19);
        Array.Copy(fixture.Positions, other.Positions, fixture.Positions.Length);
        Array.Copy(fixture.Models, other.Models, fixture.Models.Length);
        fixture.Operations.Clear();
        NativeBattleShockwavePulsar260TaskStep expected = fixture.Advance(first.Frame);
        NativeBattleShockwavePulsar260TaskStep actual = other.Advance(restored);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(JsonSerializer.Serialize(fixture.Operations), JsonSerializer.Serialize(other.Operations));
        Assert.Equal(JsonSerializer.Serialize(fixture.Positions), JsonSerializer.Serialize(other.Positions));
    }

    private const uint MainBank = ArenaAddress + MainOffset;
    private const uint TaskAddress = ArenaAddress + ChildrenOffset;
    private const uint RecordAddress = MainBank + 0x10;
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private sealed class Fixture
    {
        public NativeBattleShockwavePulsar260Frame Frame { get; }
        public NativeBattleEffectArena[] Positions { get; } = Enumerable.Range(0, 7).Select(_ => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes((short)1000)).Write(2, BitConverter.GetBytes((short)2000)).Write(4, BitConverter.GetBytes((short)3000))).ToArray();
        public NativeBattleGraphicsModel[] Models { get; } = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0xB)).ToArray();
        public List<NativeBattleShockwavePulsar260Operation> Operations { get; } = [];
        private NativeBattleShockwavePulsar260Resources Resources { get; }
        private bool Linked { get; }

        public Fixture(uint callback, ushort age, bool linked = false, int animationDuration = 50)
        {
            Linked = linked;
            if (linked)
            {
                Models[4] = Models[4] with { Flags0 = 0 };
            }
            Resources = new(null!, [Animation(0x111D424, animationDuration), Animation(0x111DEBC, animationDuration), Animation(0x111F09C, animationDuration)], [], null!, [], [], [], []);
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 1);
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, MainStride, 1);
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, TaskAddress, ChildStride, ChildCount);
            (children, int slot) = children.Append(callback);
            NativeBattleModulePoolTask task = children.Slots[slot].WriteDword(0xC, RecordAddress).WriteWord(0x10, age).WriteWord(0x12, 10);
            if (callback == ChildActCallback)
            {
                task = task.WriteWord(0xC, 111).WriteWord(0xE, 222).WriteWord(0x10, 333);
            }
            children = children with { Slots = children.Slots.SetItem(slot, task) };
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(PuffsOffset, PuffsCount * 16);
            for (int index = 0; index < PuffsCount; index++)
            {
                arena = arena.Write(PuffsOffset + index * 16 + 15, [0]);
            }
            NativeBattleOriginalEffectState effect = new(260, 259, new(260, Entry, Initializer), null, RootDescriptor) { Pools = [root, main, children] };
            NativeBattleShockwavePulsar260State state = NativeBattleShockwavePulsar260State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(0x2508614, ArenaAddress + PuffsOffset);
            NativeBattleShockwavePulsar260Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = arena }, effect, new(123), state);
            NativeBattleEffectArena record = NativeBattleEffectArena.Clear().MarkUnknown(0, 0x24).Write(0, BitConverter.GetBytes(0x1D972C0u + 3 * 156u)).Write(6, BitConverter.GetBytes((short)700)).Write(0xC, BitConverter.GetBytes((short)100)).Write(0xE, BitConverter.GetBytes((short)-200)).Write(0x10, BitConverter.GetBytes((short)300)).Write(0x14, BitConverter.GetBytes(linked ? 1u : 0u)).Write(0x18, BitConverter.GetBytes(2048u)).Write(0x1C, BitConverter.GetBytes(4096u)).Write(0x20, BitConverter.GetBytes((short)-400)).Write(0x22, BitConverter.GetBytes((short)99));
            frame = NativeBattleShockwavePulsar260Controller.WriteView(frame, RecordAddress, record, 0x24);
            int size = callback == TargetCallback ? 0xBC : callback == LateTargetCallback ? 0x100 : 0xD8;
            uint source = callback == TargetCallback ? 0x111F09Cu : callback == LateTargetCallback ? 0x111DEBCu : 0x111D424u;
            Frame = NativeBattleShockwavePulsar260Controller.InitializeAct(frame, TaskAddress, 0x14, size, source, Resources);
        }

        public NativeBattleShockwavePulsar260TaskStep Advance(NativeBattleShockwavePulsar260Frame frame) => NativeBattleShockwavePulsar260Controller.AdvanceTarget(frame, TaskAddress, Resources, new(NativeBattleActionPacketsState.Fresh(), 368, actor => Models[actor], actor => Positions[actor], actor => Linked ? actor switch { 3 => 4, 4 => 5, 5 => 3, _ => null } : null, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), Execute));

        private NativeBattleShockwavePulsar260Frame Execute(NativeBattleShockwavePulsar260Frame frame, NativeBattleShockwavePulsar260Operation operation)
        {
            Operations.Add(operation);
            if (operation is NativeBattleShockwavePulsar260ActorWrite write)
            {
                Positions[write.Actor] = NativeBattleEffectNodeContextMemory.Copy(write.Bytes, 0, Positions[write.Actor], write.Offset - 0x1C, write.Length);
            }
            if (operation is NativeBattleShockwavePulsar260ActorFlags flags)
            {
                NativeBattleGraphicsModel model = Models[flags.Actor];
                Models[flags.Actor] = model with { Flags0 = (ushort)((model.Flags0 & ~flags.ClearMask) | flags.SetMask) };
            }
            return frame;
        }

        private static NativeBattleGfAnimationSource Animation(uint address, int duration)
        {
            byte[] bytes = new byte[32];
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), duration);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 16);
            return new(address, bytes.ToImmutableArray());
        }
    }
}
