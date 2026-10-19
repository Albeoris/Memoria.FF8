using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTonberry90ControllerTests
{
    private const uint Root = NativeBattleTonberry90Controller.MainBank;
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;

    [Fact]
    public void Entry_queues_three_current_arena_TIM_sources_in_order_and_skip_mode_does_not_read_unused_pointers()
    {
        var fixture = Fixture();
        NativeBattleTonberry90Frame frame = fixture.Frame;
        byte[] tim = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(tim, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(tim.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tim.AsSpan(62), 1);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        foreach (int offset in new[] { 0x10000, 0x20000, 0x30000 })
        {
            arena = arena.Write(offset, tim);
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena }, Effect = frame.Effect with { Parameters = frame.Effect.Parameters! with { Flags79 = 0 } } };
        frame = NativeBattleTonberry90Controller.Initialize762360(frame, fixture.Inputs.Packets);
        Assert.Equal(3, frame.Memory.Textures!.Count);
        Assert.Equal(new int?[] { 0x10000, 0x20000, 0x30000 }, frame.Memory.Textures.Slots.Take(3).Select(slot => slot!.ArenaOffset));
        NativeBattleTonberry90State state = fixture.Frame.Memory.Tonberry90!;
        state = state with { Globals = state.Globals.MarkUnknown(checked((int)(0x259EEAC - NativeBattleTonberry90State.GlobalAddress)), 8) };
        NativeBattleTonberry90Frame skipped = fixture.Frame with { Memory = fixture.Frame.Memory with { Tonberry90 = state } };
        Assert.Equal(0, NativeBattleTonberry90Controller.Initialize762360(skipped, fixture.Inputs.Packets).Memory.Textures!.Count);
    }

    [Theory]
    [InlineData(0, 1, 255)]
    [InlineData(3, 1, 2)]
    [InlineData(1, 5, 4)]
    public void Entry_uses_seven_physical_banks_and_unsigned_wrapped_contact_limit(byte targets, byte repeats, byte last)
    {
        var fixture = Fixture(targets, repeats);
        NativeBattleTonberry90Frame frame = fixture.Frame;
        Assert.Equal(new[] { 2, 4, 3, 2, 100, 1, 2 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new ushort[] { 0x64, 0x58, 0x48, 0x520, 0x7C, 0x148, 0x30 }, frame.Effect.Pools.Select(pool => pool.Stride));
        NativeBattleModulePoolTask root = frame.Effect.Pools[0].Slots[0];
        Assert.Equal(0x1D99A78u, root.ReadDword(0xC));
        Assert.Equal((byte)0, root.ReadByte(0x2C));
        Assert.Equal((byte)3, root.ReadByte(0x2D));
        Assert.Equal(last, root.ReadByte(0x2F));
        Assert.Equal((ushort)targets, root.ReadWord(0x5A));
        Assert.Equal((ushort)(repeats - 1), root.ReadWord(0x58));
        Assert.Equal(Base + 0x10000, frame.Memory.Tonberry90!.ReadDword(0x25A2CF4));
        Assert.Equal(Base + 0x16000, frame.Memory.Tonberry90.ReadDword(0x25A2CF0));
        Assert.Equal(Base + 0x1C000, frame.Memory.Tonberry90.ReadDword(0x25A4C1C));
        Assert.Equal(0x12345678u, frame.Memory.Tonberry90.ReadDword(0x25A4D94));
    }

    [Fact]
    public void Master_wait_reads_previous_survivors_preserves_raw_camera_padding_and_wraps_clocks()
    {
        var fixture = Fixture();
        NativeBattleTonberry90Frame frame = WriteRoot(fixture.Frame, task => task.WriteByte(0x29, 6).WriteWord(0x5E, 1).WriteWord(0x5C, ushort.MaxValue).WriteWord(0x24, ushort.MaxValue));
        frame = NativeBattleTonberry90Controller.Advance7624D0(frame, fixture.Inputs);
        Assert.Equal((byte)6, RootTask(frame).ReadByte(0x29));
        Assert.Equal((ushort)0, RootTask(frame).ReadWord(0x5E));
        Assert.Equal((ushort)0, RootTask(frame).ReadWord(0x5C));
        Assert.Equal((ushort)0, RootTask(frame).ReadWord(0x24));
        Assert.Equal(Base + 0x10000, frame.Memory.Tonberry90!.ReadDword(0x259EEA8));
        Assert.Equal(Base + 0x16000, frame.Memory.Tonberry90.ReadDword(0x259EFB4));
        Assert.Equal(fixture.Inputs.CameraMatrix.Bytes, frame.Memory.SharedEffectView2793E58!.Bytes);
        Assert.False(frame.Memory.SharedEffectView2793E58.Known[18]);
        Assert.False(frame.Memory.SharedEffectView2793E58.Known[19]);
        frame = NativeBattleTonberry90Controller.Advance7624D0(frame, fixture.Inputs);
        Assert.Equal((byte)7, RootTask(frame).ReadByte(0x29));
        Assert.Equal(Base + 0x16000, frame.Memory.Tonberry90!.ReadDword(0x259EEA8));
        Assert.Equal(Base + 0x1C000, frame.Memory.Tonberry90.ReadDword(0x259EFB4));
    }

    [Fact]
    public void Resource_completion_keeps_actual_pointer_mutations_and_waits_for_its_completion_byte()
    {
        var fixture = Fixture();
        NativeBattleTonberry90Frame frame = WriteRoot(fixture.Frame, task => task.WriteByte(0x29, 7));
        List<NativeBattleChoco97Operation> operations = [];
        NativeBattleTonberry90Inputs inputs = fixture.Inputs with
        {
            Execute = (current, operation) =>
            {
                operations.Add(operation);
                return current;
            }
        };
        frame = NativeBattleTonberry90Controller.Advance7624D0(frame, inputs);
        NativeBattleChoco97CommonTextureLoad load = Assert.IsType<NativeBattleChoco97CommonTextureLoad>(Assert.Single(operations));
        Assert.Equal(0x10000, load.DestinationArenaOffset);
        Assert.Equal(Root + 0x62, load.CompletionAddress);
        Assert.Equal((byte)8, RootTask(frame).ReadByte(0x29));
        frame = NativeBattleTonberry90Controller.Advance7624D0(frame, inputs);
        Assert.Equal((byte)8, RootTask(frame).ReadByte(0x29));
        frame = frame with { Effect = NativeBattleTonberry90Controller.WriteCompletionByte(frame.Effect, load.CompletionAddress, 1) };
        frame = NativeBattleTonberry90Controller.Advance7624D0(frame, inputs);
        Assert.Equal((byte)9, RootTask(frame).ReadByte(0x29));
        frame = NativeBattleTonberry90Controller.Advance7624D0(frame, inputs);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Null(frame.Effect.Pools[0].Head);
        Assert.Throws<NotSupportedException>(() => NativeBattleTonberry90Controller.WriteCompletionByte(fixture.Frame.Effect, Root + 0x61, 1));
    }

    [Fact]
    public void New_target_runs_boot_director_then_IO_in_the_same_tick_and_preserves_parent_counts()
    {
        var fixture = Fixture();
        List<uint> order = [];
        NativeBattleTonberry90Inputs inputs = fixture.Inputs with
        {
            InitializeTarget = (current, address) =>
            {
                order.Add(0x762690);
                return current;
            },
            AdvanceLeaf = (current, address) =>
            {
                order.Add(new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address).Callback);
                return new(current, false);
            }
        };
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Controller.Advance7624D0(WriteRoot(fixture.Frame, task => task.WriteByte(0x29, 3)), inputs);
        Assert.Equal(new uint[] { 0x762690, 0x762C60, 0x762850 }, order);
        Assert.Equal((byte)1, RootTask(frame).ReadByte(0x28));
        Assert.Equal((ushort)3, RootTask(frame).ReadWord(0x5E));
        NativeBattleModulePoolTask target = frame.Effect.Pools[1].Slots[0];
        Assert.Equal((byte)2, target.ReadByte(0x28));
        Assert.Equal((byte)1, target.ReadByte(0x29));
        Assert.Equal(Root, target.ReadDword(0x10));
        Assert.Equal((ushort)1, target.ReadWord(0x24));
    }

    [Fact]
    public void Independent_restores_keep_queue_links_and_unknown_camera_padding()
    {
        var fixture = Fixture();
        NativeBattleTonberry90Inputs inputs = fixture.Inputs with { InitializeTarget = (current, _) => current, AdvanceLeaf = (current, _) => new(current, false) };
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Controller.Advance7624D0(WriteRoot(fixture.Frame, task => task.WriteByte(0x29, 3)), inputs);
        string saved = JsonSerializer.Serialize(frame);
        NativeBattleTonberry90Frame first = JsonSerializer.Deserialize<NativeBattleTonberry90Frame>(saved)!;
        NativeBattleTonberry90Frame second = JsonSerializer.Deserialize<NativeBattleTonberry90Frame>(saved)!;
        for (int tick = 0; tick < 3; tick++)
        {
            first = NativeBattleTonberry90Controller.Advance7624D0(first, inputs);
            second = NativeBattleTonberry90Controller.Advance7624D0(second, inputs);
            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleTonberry90Controller.Advance7624D0(WriteRoot(fixture.Frame, task => task.WriteByte(0x29, 255)), fixture.Inputs));
    }

    private static NativeBattleModulePoolTask RootTask(NativeBattleTonberry90Frame frame) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(Root);

    private static NativeBattleTonberry90Frame WriteRoot(NativeBattleTonberry90Frame frame, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> update)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(Root, update(hierarchy.Read(Root))).Pools } };
    }

    internal static (NativeBattleTonberry90Frame Frame, NativeBattleTonberry90Inputs Inputs) Fixture(byte targets = 1, byte repeats = 1)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 21, 1, null));
        for (int index = 0; index < Math.Max(1, (int)targets); index++)
        {
            (packets, _) = packets.AppendContact(new(new((byte)(3 + index), 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        packets = packets.WritePlayerRepeatMode(0, repeats);
        (packets, _) = packets.Publish(targets);
        NativeBattleEffectParameters parameters = new(0, 1, 8, 0, 0, 0);
        NativeBattleOriginalEffectState effect = new(90, 89, new(90, 0x762360, 0x762320), parameters, null)
        {
            ResourcePointers = [new(0x25A4C20, 0x10000), new(0x259EEAC, 0x20000), new(0x259EEB0, 0x30000)]
        };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        memory = memory with { Tonberry90 = memory.Tonberry90!.WriteDword(0x25A4D94, 0x12345678) };
        NativeBattleTonberry90Frame frame = NativeBattleTonberry90Controller.Initialize762320(new(memory, effect, new(1)));
        frame = NativeBattleTonberry90Controller.Initialize762360(frame, packets);
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), new byte[] { 1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0 }.ToImmutableArray());
        ImmutableArray<bool> known = Enumerable.Repeat(true, 32).ToImmutableArray().SetItem(18, false).SetItem(19, false);
        NativeBattlePackedMatrix32 camera = new(Enumerable.Range(0, 32).Select(index => (byte)index).ToImmutableArray(), known);
        NativeBattleTonberry90Inputs inputs = new(packets, camera, index => new(index, 0) { SequenceState = new() { Pose = 0, PoseFrame114 = 1 } }, _ => source, (_, _) => throw new InvalidOperationException("Target boot was not reached."), (_, _) => throw new InvalidOperationException("No leaf was admitted."), (_, _) => throw new InvalidOperationException("Contact was not reached."), (_, _) => throw new InvalidOperationException("Resource request was not reached."));
        return (frame, inputs);
    }
}
