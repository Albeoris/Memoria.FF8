using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect68Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect68Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect68LowerTests
{
    [Fact]
    public void Record_lines_consume_only_the_two_byte_sentinel_and_use_native_zero_submission_flags()
    {
        byte[] records = new byte[18];
        BinaryPrimitives.WriteInt16LittleEndian(records.AsSpan(4), -3);
        BinaryPrimitives.WriteInt16LittleEndian(records.AsSpan(6), 3);
        BinaryPrimitives.WriteInt16LittleEndian(records.AsSpan(8), 7);
        BinaryPrimitives.WriteInt16LittleEndian(records.AsSpan(10), -7);
        records[12] = 1;
        records[13] = 2;
        records[14] = 3;
        BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(16), 0xFF);
        NativeBattleModulePoolTask task = TaskRecord(0x78).WriteWord(0x64, 2048);
        NativeBattleModulePacketBatch result = NativeBattleEffect68Lower.RecordLines(task, records);
        NativeBattleAllocatedPrimitive line = Assert.Single(result.Primitives);
        Assert.Equal(0, line.Flags20);
        Assert.Equal(0x40030201u, line.Primitive.Words[1]);
        Assert.Equal(159u | 109u << 16, line.Primitive.Words[2]);
        Assert.Equal(163u | 105u << 16, line.Primitive.Words[3]);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, line.Primitive.VertexPayload4);
    }

    [Fact]
    public void Camera_interpolation_wraps_progress_before_signed_comparison_and_preserves_unwritten_W()
    {
        NativeBattleEffectArena camera = NativeBattleEffectArena.Clear().Write(0x30, BitConverter.GetBytes((ushort)32760)).Write(0x32, BitConverter.GetBytes((ushort)16)).Write(0x36, BitConverter.GetBytes((short)-100)).Write(0x38, BitConverter.GetBytes((short)100)).Write(14, BitConverter.GetBytes((ushort)0xAABB));
        NativeBattleEffectArena result = NativeBattleEffect68Lower.InterpolateCamera(camera, out bool complete);
        Assert.False(complete);
        Assert.Equal(unchecked((short)(32760 + 16)), S16(result, 0x30));
        Assert.Equal(unchecked((short)(-100 + (200 * unchecked((short)(32760 + 16))) / 4096)), S16(result, 0x34));
        Assert.Equal((ushort)0xAABB, U16(result, 14));
    }

    [Fact]
    public void Camera_interpolation_completes_at_exact_4096_and_leaves_unconsumed_bytes_unknown()
    {
        NativeBattleEffectArena camera = NativeBattleEffectArena.Clear().Write(0x30, BitConverter.GetBytes((ushort)3840)).Write(0x32, BitConverter.GetBytes((ushort)256)).Write(0x18, BitConverter.GetBytes((short)-123)).MarkUnknown(6, 2);
        NativeBattleEffectArena result = NativeBattleEffect68Lower.InterpolateCamera(camera, out bool complete);
        Assert.True(complete);
        Assert.Equal(-123, S16(result, 8));
        Assert.DoesNotContain(true, result.ReadSnapshot(6, 2).Known);
    }

    [Theory]
    [InlineData(0, 999, 16384, 0x1593554u)]
    [InlineData(1, 999, 16384, 0x1593554u)]
    [InlineData(2, 127, 16384, 0x15935DCu)]
    [InlineData(7, 25, 16384, 0x1593884u)]
    public void Stat_rows_use_both_HP_selectors_and_current_208_byte_descriptor_offsets(int selector, int value, int expectedScale, uint source)
    {
        NativeBattleModulePoolTask task = TaskRecord(0x6C).WriteWord(0x62, (ushort)selector);
        NativeBattleModulePoolTask parent = TaskRecord(0x78).WriteWord(0x1C, 10).WriteWord(0x1E, 20).WriteWord(0x20, 30);
        NativeBattleEffectArena actor = NativeBattleEffectArena.Clear().Write(0x18, BitConverter.GetBytes(value)).Write(selector == 7 ? 0xC3 : 0xBD, [(byte)value]);
        NativeBattleEffect68Resources resources = Resources() with { StaticFronts = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0x15954D0, new byte[80].ToImmutableArray()) };
        NativeBattleModulePoolTask result = NativeBattleEffect68Lower.InitializeStat(task, parent, actor, resources);
        Assert.Equal(source, result.ReadDword(0x4C));
        Assert.Equal(expectedScale, result.ReadWord(0x64));
        Assert.Equal(10, result.ReadWord(0x1C));
        Assert.Equal(20, result.ReadWord(0x1E));
        Assert.Equal(30, result.ReadWord(0x20));
        Assert.Equal(4, result.ReadByte(0x26) & 4);
    }

    [Fact]
    public void Results_timer_advances_pages_without_wrapping_and_closes_after_last_page()
    {
        Harness harness = new();
        (NativeBattleEffect68Frame frame, uint address) = Frame(harness, 0x7DE540);
        frame = Write(frame, address, Read(frame, address).WriteByte(0x29, 2).WriteWord(0x66, 1).WriteWord(0x6A, 2));
        NativeBattleEffect68Step first = NativeBattleEffect68Lower.Advance(frame, address, Resources(), harness.Inputs());
        Assert.Equal(1, Read(first.Frame, address).ReadByte(0x29));
        Assert.Equal(1, Read(first.Frame, address).ReadWord(0x6C));
        NativeBattleEffect68Step displayed = NativeBattleEffect68Lower.Advance(first.Frame, address, Resources(), harness.Inputs());
        Assert.Equal(new NativeBattleEffect68WindowText(1, 0x12340001), harness.Operations.OfType<NativeBattleEffect68WindowText>().Last());
        Assert.Equal(45, Read(displayed.Frame, address).ReadWord(0x66));
        frame = Write(displayed.Frame, address, Read(displayed.Frame, address).WriteWord(0x66, 1));
        NativeBattleEffect68Step last = NativeBattleEffect68Lower.Advance(frame, address, Resources(), harness.Inputs());
        Assert.Equal(3, Read(last.Frame, address).ReadByte(0x29));
        Assert.Equal(2, Read(last.Frame, address).ReadWord(0x6C));
        NativeBattleEffect68Step closed = NativeBattleEffect68Lower.Advance(last.Frame, address, Resources(), harness.Inputs());
        Assert.Equal(2, closed.Result);
        Assert.Equal((ushort)1, closed.Frame.State.ReadWord(0x26005B2));
        Assert.Collection(harness.Operations.TakeLast(2), operation => Assert.IsType<NativeBattleEffect68CloseWindow>(operation), operation => Assert.Equal(new NativeBattleEffect68WindowText(1, 0), operation));
    }

    [Fact]
    public void Results_held_button_requires_a_new_edge_but_confirm_exits_immediately()
    {
        Harness harness = new() { Held = 0x20 };
        (NativeBattleEffect68Frame frame, uint address) = Frame(harness, 0x7DE540);
        frame = Write(frame, address, Read(frame, address).WriteByte(0x29, 2).WriteDword(0x5C, 0x20).WriteWord(0x66, 5).WriteWord(0x6A, 3));
        NativeBattleEffect68Step held = NativeBattleEffect68Lower.Advance(frame, address, Resources(), harness.Inputs());
        Assert.Equal(4, Read(held.Frame, address).ReadWord(0x66));
        Assert.Equal(0, Read(held.Frame, address).ReadWord(0x6C));
        harness.Held = 0x40;
        NativeBattleEffect68Step exit = NativeBattleEffect68Lower.Advance(held.Frame, address, Resources(), harness.Inputs());
        Assert.Equal(3, Read(exit.Frame, address).ReadByte(0x29));
        Assert.Equal(0, Read(exit.Frame, address).ReadWord(0x6C));
    }

    [Fact]
    public void Restore_camera_publishes_saved_packed_W_and_retires_on_following_callback()
    {
        Harness harness = new();
        (NativeBattleEffect68Frame frame, uint address) = Frame(harness, 0x7DE830);
        NativeBattleEffect68Step first = NativeBattleEffect68Lower.Advance(frame, address, Resources(), harness.Inputs());
        NativeBattleEffect68PublishCamera operation = Assert.IsType<NativeBattleEffect68PublishCamera>(Assert.Single(harness.Operations));
        Assert.Equal((ushort)0xCAFE, U16(operation.Eye8, 6));
        Assert.Equal((ushort)0xBEEF, U16(operation.Target8, 6));
        Assert.Equal(0, first.Result);
        Assert.Equal(1, Read(first.Frame, address).ReadWord(0x66));
        NativeBattleEffect68Step next = NativeBattleEffect68Lower.Advance(first.Frame, address, Resources(), harness.Inputs());
        Assert.Equal(2, next.Result);
    }

    [Fact]
    public void Morph_uses_saturated_IR_W_retention_stateful_two_weights_and_balanced_temporary()
    {
        Harness harness = new();
        (NativeBattleEffect68Frame frame, _) = Frame(harness, 0x7DE830);
        NativeBattleEffect68Resources resources = Resources();
        frame = NativeBattleEffect68Lower.MorphVertices(frame, 0x159390C, 0x1593A94, 0x1591C44, 2048, resources, harness.Inputs());
        NativeBattleScanByteOwner current = frame.Memory.Scan!.MutableMeshes[0x1591C44];
        Assert.Equal((ushort)100, current.ReadWord(8));
        Assert.Equal(unchecked((ushort)-100), current.ReadWord(10));
        Assert.Equal((ushort)0xCAFE, current.ReadWord(14));
        Assert.Equal(new int?[] { 100, -100, 100 }, frame.Memory.ScriptedColorRegisters!.Ir);
        Assert.Equal(2048u, frame.Memory.ScriptedColorRegisters.Ir0);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        frame = JsonSerializer.Deserialize<NativeBattleEffect68Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleEffect68Frame second = NativeBattleEffect68Lower.MorphVertices(frame, 0x159390C, 0x1593A94, 0x1591C44, 0, resources, harness.Inputs());
        Assert.Equal((ushort)0xCAFE, second.Memory.Scan!.MutableMeshes[0x1591C44].ReadWord(14));
        Assert.True(second.State.MeshSourcesBound);
    }

    [Theory]
    [InlineData(false, 88, 44)]
    [InlineData(true, 44, 0)]
    public void Spinner_has_inline_mode_and_reuses_rejected_allocation_with_current_stateful_depth(bool rejectFirst, int allocation, int secondOffset)
    {
        byte[] mesh = new byte[136];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 56);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 6);
        short[] coordinates = [-100, -100, 100, -100, -100, 100, 100, 100, 0, 0, 0, 0];
        for (int vertex = 0; vertex < 6; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + vertex * 8), coordinates[vertex * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(10 + vertex * 8), coordinates[vertex * 2 + 1]);
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(12 + vertex * 8), 1000);
        }
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(68), 2);
        for (int face = 0; face < 2; face++)
        {
            int offset = 72 + face * 24;
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(offset), 0x2C808080);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(offset + 4 + corner * 2), (ushort)(face == 0 && rejectFirst ? 0 : corner * 2));
            }
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, 0), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(0x15934CCu)).Write(8, BitConverter.GetBytes(0u)).Write(12, BitConverter.GetBytes(0u)).Write(0x1C, BitConverter.GetBytes(0xF0u));
        NativeBattleStaticMeshContext88Step result = NativeBattleEffect68Lower.DrawSpinner(memory, new(0, 0x15934CC, mesh.ToImmutableArray()), new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(allocation, result.Batch.AllocationBytes);
        Assert.Equal(secondOffset, result.Batch.Attempts[1].ByteOffsetRelative);
        Assert.Equal(0xE1000220u, result.Batch.Attempts[1].Primitive!.Words[^1]);
        Assert.Equal(0xE1000220u, result.Memory.PrimitiveProcess!.DrawMode2795BD0);
        Assert.Equal(result.Memory.GeometryRegisters!.Otz, BinaryPrimitives.ReadUInt32LittleEndian(result.Context.Read(0x2C, 4)));
        if (rejectFirst)
        {
            Assert.Null(result.Batch.Attempts[0].Primitive);
            Assert.Equal(0x0A000000u, BinaryPrimitives.ReadUInt32LittleEndian(result.Batch.Attempts[0].Data.AsSpan()));
        }
    }

    private static NativeBattleModulePoolTask TaskRecord(int length) => new(0, 0, null, 0, new byte[length - 12].ToImmutableArray());

    private static short S16(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadInt16LittleEndian(row.Read(offset, 2));
    private static ushort U16(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(row.Read(offset, 2));
    private static NativeBattleEffect68Resources Resources()
    {
        byte[] mesh = new byte[1000];
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(14), 0xCAFE);
        byte[] first = new byte[16];
        byte[] second = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(first.AsSpan(4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(second.AsSpan(4), 1);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(first.AsSpan(8 + axis * 2), (short)(axis == 1 ? -200 : 200));
        }
        return new(new("texture", []), 0, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(0x1591C44, new(0, 0x1591C44, mesh.ToImmutableArray())).Add(0x159202C, new(0, 0x159202C, new byte[1896].ToImmutableArray())), ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0x159390C, first.ToImmutableArray()).Add(0x1593A94, second.ToImmutableArray()), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
    }

    private static (NativeBattleEffect68Frame Frame, uint Address) Frame(Harness harness, uint callback)
    {
        ImmutableArray<NativeBattleEffectTaskPool> pools = Pools.Select(shape => NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity)).ToImmutableArray();
        NativeBattleOriginalEffectState effect = new(68, 67, new(68, Entry, Initializer), new(3, 0, 1, 0, 0, 0), null) { Pools = pools, ReturnedDescriptor = RootDescriptor };
        NativeBattleEffect68State state = NativeBattleEffect68State.FromColdProcess();
        for (int index = 0; index < Pools.Length; index++)
        {
            state = state.DetachBank(index);
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleEffect68Frame frame = new(memory, effect, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        (frame, _) = AppendHierarchy(frame, 0, RootCallback, 0x64, 0, harness.Inputs());
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(12, 0x1D99A78));
        (frame, uint address) = AppendHierarchy(frame, 2, callback, 0x78, RootBank, harness.Inputs());
        return (frame, address);
    }

    private sealed class Harness
    {
        public ushort Held { get; set; }
        public List<NativeBattleEffect68Operation> Operations { get; } = [];
        public NativeBattleEffect68Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Packets(), _ => 0, _ => Enumerable.Repeat(NativeBattleEffectArena.Clear(), 7).ToImmutableArray(), (_, _) => NativeBattleEffectArena.Clear(), (_, _) => 1, _ => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), 100, 0, 4096, NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((ushort)0xCAFE)), NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((ushort)0xBEEF)), 200), null!, _ => new(null, 100, 160, 108), null!, _ => Held, (frame, _) => new(frame, 2), (_, index) => 0x12340000u + index, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });

        private static NativeBattleActionPacketsState Packets()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
            (packets, _) = packets.Prepare(new(3, 8, 11, -1, 135, 68, null));
            (packets, _) = packets.AppendContact(new(new(3, 3, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            return packets;
        }
    }
}
