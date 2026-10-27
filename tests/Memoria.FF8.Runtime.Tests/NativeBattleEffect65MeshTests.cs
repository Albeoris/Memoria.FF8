using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect65Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect65LowerFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect65MeshTests
{
    [Fact]
    public void Wave4810_wraps_signed_XYZ_and_preserves_each_current_W_lane()
    {
        NativeBattleEffect65Frame frame = Frame();
        uint destination = ArenaAddress + 0x6CB8;
        frame = Write(frame, destination, NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((ushort)0xCAFE)).MarkUnknown(14, 2), 24);
        NativeBattleEffect65Resources resources = Resources() with { Meshes = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(0x133822C, Raw(0x133822C, [(32760, -32760, -2), (1, -1, 0), (10, 20, -4)])) };
        NativeBattleEffect65Frame result = NativeBattleEffect65Mesh.Transform4810(frame, 16, -16, resources);
        NativeBattleEffectArena rows = Read(result, destination, 24);
        Assert.Equal((short)0, Word(rows, 0));
        Assert.Equal((short)0, Word(rows, 2));
        Assert.Equal((short)-16, Word(rows, 4));
        Assert.Equal((ushort)0xCAFE, BinaryPrimitives.ReadUInt16LittleEndian(rows.Read(6, 2)));
        Assert.Throws<NotSupportedException>(() => rows.Read(14, 2));
        Assert.Equal((short)18, Word(rows, 8));
        Assert.Equal((short)-18, Word(rows, 10));
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal(frame.Memory.GeometryRegisters, result.Memory.GeometryRegisters);
    }

    [Fact]
    public void Wave4B70_masks_negative_phase_and_changes_only_XYZ_six_bytes()
    {
        NativeBattleEffect65Frame frame = Frame();
        uint destination = ArenaAddress + 0x6CB8;
        frame = Write(frame, destination, NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((ushort)0xBEEF)), 8);
        byte[] trig = new byte[16384];
        BinaryPrimitives.WriteInt16LittleEndian(trig.AsSpan(2048 * 4), -4096);
        NativeBattleEffect65Resources resources = Resources() with { Meshes = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(0x133A0FC, Raw(0x133A0FC, [(32760, -123, -8192)])), StaticFronts = Resources().StaticFronts.SetItem(TrigonometryAddress, trig.ToImmutableArray()) };
        NativeBattleEffect65Frame result = NativeBattleEffect65Mesh.Transform4B70(frame, 128, 1024, resources);
        NativeBattleEffectArena row = Read(result, destination, 8);
        Assert.Equal((short)32632, Word(row, 0));
        Assert.Equal((short)-123, Word(row, 2));
        Assert.Equal((short)-8192, Word(row, 4));
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(row.Read(6, 2)));
    }

    [Fact]
    public void Color_motion_visits_all_1024_rows_and_reflects_byte_overflow_in_native_order()
    {
        NativeBattleEffect65Frame frame = Frame();
        uint first = ArenaAddress + 0xDC3;
        uint last = first + 1023 * 16;
        frame = Write(frame, first, NativeBattleEffectArena.Clear().Write(0, [3]).Write(4, [248]), 5);
        frame = Write(frame, last, NativeBattleEffectArena.Clear().Write(0, [250]).Write(4, [10]), 5);
        frame = frame with { State = frame.State.WriteWord(LiteralBase, 0xFFFF).WriteWord(LiteralBase + 2, 2).WriteWord(LiteralBase + 4, 1).WriteWord(LiteralBase + 6, 65535).WriteWord(0x133AC52, 0xCAFE) };
        NativeBattleEffect65Frame result = NativeBattleEffect65Mesh.Advance6E4570(frame);
        Assert.Equal(new byte[] { 4, 0, 0, 0, 8 }, Read(result, first, 5).Read(0, 5));
        Assert.Equal(new byte[] { 251, 0, 0, 0, 246 }, Read(result, last, 5).Read(0, 5));
        Assert.Equal((ushort)0, result.State.ReadWord(LiteralBase + 4));
        Assert.Equal((ushort)1, result.State.ReadWord(LiteralBase + 6));
        Assert.Equal((ushort)0xCAFE, result.State.ReadWord(0x133AC52));
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(0x800C)]
    [InlineData(0xC)]
    public void Ring_context_keeps_the_actual_billboard_branch_and_source_pointer_overwrite(uint flags)
    {
        NativeBattleEffect65Frame frame = Frame();
        NativeBattleStaticMeshResource mesh = Triangle();
        NativeBattleEffect65Resources resources = Resources() with { Meshes = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(mesh.Address, mesh) };
        NativeBattleActionMatrix view = NativeBattlePandemona291Restoration.Rotation701270(1024) with { X = 10, Y = 20, Z = 2000 };
        List<NativeBattleEffect65Operation> operations = [];
        NativeBattleEffect65Inputs inputs = Inputs(operations) with { ReadView = _ => NativeBattlePackedMatrix32.FromMatrix(view, null) };
        NativeBattleEffect65Frame result = NativeBattleEffect65Mesh.Draw6E4230(frame, 100, 200, 300, 0, 1024, 0, flags, -512, mesh.Address, CursorGlobal, resources, inputs);
        byte[] temporary = result.Staging!.Read(NativeBattleTemporaryMemory.Address, 140).ToArray();
        Assert.Equal(mesh.Address + 8, BinaryPrimitives.ReadUInt32LittleEndian(temporary.AsSpan(0x24)));
        Assert.Equal(flags, BinaryPrimitives.ReadUInt32LittleEndian(temporary.AsSpan(0x3C)));
        Assert.Equal(flags == 0x800C ? (short)1024 : (short)0, BinaryPrimitives.ReadInt16LittleEndian(temporary));
        Assert.Single(operations.OfType<NativeBattleEffect65DeliverBatch>());
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Point_draw_continues_into_the_next_128_row_bank_without_resetting_its_source()
    {
        NativeBattleEffect65Frame frame = Frame();
        byte[] rows = new byte[1024 * 16];
        for (int index = 0; index < 1024; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rows.AsSpan(index * 16 + 6), 32767);
        }
        foreach (int index in new[] { 127, 128 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(rows.AsSpan(index * 16 + 6), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(rows.AsSpan(index * 16 + 8), index == 127 ? 0x00332211u : 0x00665544u);
        }
        frame = Write(frame, ArenaAddress + 0xDB8, NativeBattleEffectArena.Clear().Write(0, rows), rows.Length);
        List<NativeBattleEffect65Operation> operations = [];
        NativeBattleEffect65Frame result = NativeBattleEffect65Mesh.Draw6E4350(frame, 0, 0, 0, 0, 1, CursorGlobal, Resources(), Inputs(operations));
        NativeBattleEffect65DeliverBatch[] packets = operations.OfType<NativeBattleEffect65DeliverBatch>().ToArray();
        Assert.Equal(2, packets.Length);
        Assert.Equal(new uint[] { 0x68332211, 0x68665544 }, packets.Select(packet => packet.Batch.Attempts.Single().Primitive!.Words[1]));
        Assert.All(packets, packet => Assert.Equal(12, packet.Batch.AllocationBytes));
        Assert.All(packets, packet => Assert.Equal(142, packet.Batch.Attempts.Single().Primitive!.Bucket));
        Assert.Equal(frame.Random, result.Random);
        NativeBattleEffect65Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect65Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
    }

    [Fact]
    public void Point_blending_loads_unscaled_RGB_bytes_and_stores_low_IR_bytes()
    {
        NativeBattleEffect65Frame frame = Frame();
        byte[] rows = new byte[1024 * 16];
        for (int index = 0; index < 1024; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rows.AsSpan(index * 16 + 6), 32767);
        }
        BinaryPrimitives.WriteInt16LittleEndian(rows.AsSpan(6), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(rows.AsSpan(8), 0x803264C8);
        BinaryPrimitives.WriteUInt32LittleEndian(rows.AsSpan(12), 0x003C2814);
        frame = Write(frame, ArenaAddress + 0xDB8, NativeBattleEffectArena.Clear().Write(0, rows), rows.Length);
        List<NativeBattleEffect65Operation> operations = [];
        NativeBattleEffect65Frame result = NativeBattleEffect65Mesh.Draw6E4350(frame, 0, 0, 0, 0, 1, CursorGlobal, Resources(), Inputs(operations));
        NativeBattleEffect65DeliverBatch packet = Assert.Single(operations.OfType<NativeBattleEffect65DeliverBatch>());
        Assert.Equal(0x6837466Eu, packet.Batch.Attempts.Single().Primitive!.Words[1]);
        Assert.Equal(new int?[] { 110, 70, 55 }, result.Memory.ScriptedColorRegisters!.Ir);
    }

    private static NativeBattleStaticMeshResource Raw(uint address, (short X, short Y, short Z)[] vertices)
    {
        byte[] data = new byte[8 + vertices.Length * 8];
        BinaryPrimitives.WriteInt32LittleEndian(data, data.Length);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), vertices.Length);
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8 + index * 8), vertices[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10 + index * 8), vertices[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12 + index * 8), vertices[index].Z);
        }
        return new(0, address, data.ToImmutableArray());
    }

    private static NativeBattleStaticMeshResource Triangle()
    {
        NativeBattleStaticMeshResource mesh = Raw(0x1334FFC, [(-100, -100, 0), (100, -100, 0), (0, 100, 0)]);
        byte[] bytes = new byte[84];
        mesh.Bytes.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), 0x26808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(50), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52), 4);
        return mesh with { Bytes = bytes.ToImmutableArray() };
    }
}
