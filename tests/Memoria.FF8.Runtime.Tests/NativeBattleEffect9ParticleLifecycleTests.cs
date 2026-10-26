using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect9Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect9ParticleLifecycleTests
{
    [Fact]
    public void Schedule_uses_the_current_counter_and_terminal_parent_waits_for_both_actual_children()
    {
        Fixture fixture = new();
        NativeBattleEffect9Frame frame = Write(fixture.Frame, fixture.Birth, Read(fixture.Frame, fixture.Birth).WriteWord(0x24, 1));
        frame = JsonSerializer.Deserialize<NativeBattleEffect9Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleEffect9Step birth = NativeBattleEffect9Particles.Advance(frame, fixture.Birth, fixture.Inputs([9, 2, 255]));
        Assert.Equal(0, birth.Result);
        Assert.Equal((ushort)2, Read(birth.Frame, fixture.Birth).ReadWord(0x24));
        Assert.Equal((byte)2, Read(birth.Frame, fixture.Birth).ReadByte(0x28));
        uint[] children = birth.Frame.Effect.Pools[5].Slots.Select((task, index) => (task, index)).Where(row => row.task.Callback == 0x8C3E00).Select(row => birth.Frame.Effect.Pools[5].BankAddress + (uint)(row.index * 92)).ToArray();
        Assert.Equal(2, children.Length);
        foreach (uint child in children)
        {
            Assert.Equal(fixture.Root, Read(birth.Frame, child).ReadDword(0x10));
            Assert.Equal(fixture.Birth, Read(birth.Frame, child).ReadDword(0x14));
            Assert.Equal(fixture.Birth, Read(birth.Frame, child).ReadDword(0x18));
            Assert.Equal((byte)3, Read(birth.Frame, child).ReadByte(0x2D));
        }
        NativeBattleEffect9Step stopped = NativeBattleEffect9Particles.Advance(birth.Frame, fixture.Birth, fixture.Inputs([9, 2, 255]));
        Assert.Equal(0, stopped.Result);
        Assert.Equal((byte)1, Read(stopped.Frame, fixture.Birth).ReadByte(0x29));
        Assert.Equal((byte)1, Read(stopped.Frame, fixture.Root).ReadByte(0x28));
        frame = stopped.Frame;
        foreach (uint child in children)
        {
            frame = Write(frame, child, Read(frame, child).WriteByte(0x26, 5).WriteByte(0x29, 2));
            NativeBattleEffect9Step completed = NativeBattleEffect9Particles.Advance(frame, child, fixture.Inputs([]));
            Assert.Equal(2, completed.Result);
            frame = completed.Frame;
        }
        Assert.Equal((byte)0, Read(frame, fixture.Birth).ReadByte(0x28));
        NativeBattleEffect9Step parent = NativeBattleEffect9Particles.Advance(frame, fixture.Birth, fixture.Inputs([]));
        Assert.Equal(2, parent.Result);
        Assert.Equal((byte)0, Read(parent.Frame, fixture.Root).ReadByte(0x28));
        Assert.Equal(fixture.Frame.Random, parent.Frame.Random);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(-1, false)]
    public void Birth_stop_uses_the_signed_control_state_threshold(int state, bool stop)
    {
        Fixture fixture = new();
        NativeBattleEffect9Frame frame = fixture.Frame with { State = fixture.Frame.State.WriteWord(NativeBattleEffect9State.ControlAddress + 0x40, unchecked((ushort)state)) };
        NativeBattleEffect9Step result = NativeBattleEffect9Particles.Advance(frame, fixture.Birth, fixture.Inputs([2]));
        Assert.Equal(stop ? 2 : 0, result.Result);
        Assert.Equal(stop ? 0 : 2, result.Frame.Effect.Pools[5].Slots.Count(task => task.Callback == 0x8C3E00));
        Assert.Equal(fixture.Frame.Random, result.Frame.Random);
    }

    [Fact]
    public void FF_schedule_byte_terminates_without_attempting255_births()
    {
        Fixture fixture = new();
        NativeBattleEffect9Step result = NativeBattleEffect9Particles.Advance(fixture.Frame, fixture.Birth, fixture.Inputs([255]));
        Assert.Equal(2, result.Result);
        Assert.All(result.Frame.Effect.Pools[5].Slots, task => Assert.Equal(0u, task.Callback));
        Assert.Equal((byte)0, Read(result.Frame, fixture.Root).ReadByte(0x28));
        Assert.Equal(fixture.Frame.Random, result.Frame.Random);
    }

    [Fact]
    public void D8_draw_publishes_actual_temporary_and_registers_then_releases_without_consuming_unknown_packed_W()
    {
        Fixture fixture = new();
        NativeBattleEffectHierarchyAppend append = new NativeBattleEffectHierarchy(fixture.Frame.Effect.Pools).Append8DC540(5, 0x8C3E00, 92, fixture.Birth, (_, _, _) => new(0, 3));
        NativeBattleEffect9Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = append.Hierarchy.Pools } };
        frame = Write(frame, append.Address, Read(frame, append.Address).MarkPayloadUnknown(0x22, 2));
        uint cursor = Cursor(frame);
        int deliveries = 0;
        NativeBattleEffect9ParticleInputs inputs = fixture.Inputs([]) with
        {
            AppendCommon = (current, batch) =>
            {
                Assert.Equal(cursor + 0xD8, Cursor(current));
                Assert.Empty(batch.Attempts);
                Assert.Equal(0x1634B84u, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.Read(cursor, 4)));
                Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(current.Staging.Read(cursor + 4, 2)));
                Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(current.Staging.Read(cursor + 0x24, 2)));
                Assert.Throws<NotSupportedException>(() => current.Staging.Read(cursor + 0xCA, 2));
                deliveries++;
                return current;
            }
        };
        NativeBattleEffect9Step result = NativeBattleEffect9Particles.Advance(frame, append.Address, inputs);
        Assert.Equal(0, result.Result);
        Assert.Equal(1, deliveries);
        Assert.Equal(cursor, Cursor(result.Frame));
        Assert.NotNull(result.Frame.Memory.GeometryRegisters);
        Assert.NotNull(result.Frame.Memory.ScriptedColorRegisters);
        Assert.Throws<NotSupportedException>(() => Read(result.Frame, append.Address).ReadWord(0x22));
        NativeBattleModulePoolTask task = Read(result.Frame, append.Address);
        for (int axis = 0; axis < 3; axis++)
        {
            Assert.Equal((int)unchecked((short)task.ReadWord(0x1C + axis * 2)), BinaryPrimitives.ReadInt32LittleEndian(result.Frame.Staging!.Read(cursor + 0xCC + (uint)(axis * 4), 4)));
        }
    }

    private static uint Cursor(NativeBattleEffect9Frame frame) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));

    private sealed class Fixture
    {
        public NativeBattleEffect9Frame Frame { get; }
        public NativeBattleActionPacketsState Packets { get; }
        public uint Root { get; }
        public uint Birth { get; }
        private readonly NativeBattleModuleResource sprite;
        private readonly NativeBattlePackedMatrix32 identity = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);

        public Fixture()
        {
            byte[] header = new byte[20];
            header[16] = 1;
            NativeBattleContactHalf half = new(3, 0, 0, 0, 0, 7, 0);
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            Packets = packets with { Headers = packets.Headers.SetItem(0, new(header.ToImmutableArray(), 0, 0, null)), Contacts = packets.Contacts.SetItem(0, new(half, half)) };
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleEffect9State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, RootCallback, 100);
            Root = root.Address;
            hierarchy = root.Hierarchy.Write(Root, root.Hierarchy.Read(Root).WriteDword(0xC, 0x1D99A78));
            NativeBattleEffectHierarchyAppend birth = hierarchy.Append8DC540(4, 0x8C3D40, 124, Root, (_, _, _) => new(0, 3));
            Birth = birth.Address;
            NativeBattleOriginalEffectState effect = new(9, 2, new(9, Entry, 0x8C0360), new(0, 0, 0, 0, 0, 0), NativeBattleEffect9State.Pools[0].Descriptor) { Pools = birth.Hierarchy.Pools };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
            Frame = new(memory, effect, new(1), NativeBattleEffect9State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            byte[] bytes = new byte[38];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8, 2), 11);
            for (int index = 0; index < 12; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + index * 2, 2), 34);
            }
            sprite = new(0, 0x1634B84, 0, 0, bytes.ToImmutableArray());
        }

        public NativeBattleEffect9ParticleInputs Inputs(ImmutableArray<byte> schedule) => new(Packets, schedule, sprite, _ => identity, _ => new(null, 100, 160, 108), _ => 0, (frame, _) => frame);
    }
}
