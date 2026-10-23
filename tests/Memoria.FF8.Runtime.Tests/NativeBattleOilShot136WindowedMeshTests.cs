using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOilShot136WindowedMeshTests
{
    [Fact]
    public void Dedicated_draw_uses_current_arena_and_preserves_native_rejected_prefix()
    {
        NativeBattleStaticMeshResource source = Source(7, badFourth: true);
        NativeBattleEffectMemory memory = SelectedMemory(0);
        NativeBattleWindowedMeshStep step = NativeBattleOilShot136Mesh.Draw(memory, source, Context().MarkUnknown(0x10, 20), Projection);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(step.Batch.Attempts);
        Assert.Equal(0, step.Batch.AllocationBytes);
        Assert.Null(attempt.Primitive);
        Assert.Equal(52, attempt.Data.Length);
        Assert.Equal(0x0C000000u, U32(attempt.Data, 0));
        Assert.Equal(0x3E112233u, U32(attempt.Data, 4));
        Assert.All(attempt.WriteMask.Skip(8).Take(4), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.Equal(Address + (uint)source.Bytes.Length, U32(step.Context, 0x24));
        Assert.Throws<NotSupportedException>(() => step.Context.Read(0x30, 4));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Quad_admission_has_no_NCLIP_gate(bool reverse, bool degenerate)
    {
        NativeBattleWindowedMeshStep step = Draw(Source(7, reverse: reverse, degenerate: degenerate), Context(), SelectedMemory());
        Assert.Equal(84, step.Batch.AllocationBytes);
        Assert.Equal(4, step.Batch.Attempts.Count(attempt => attempt.Primitive is not null));
    }

    [Theory]
    [InlineData(7, 84)]
    public void Typed92_context_uses_actual_mutable_XYZ_and_retains_unknown_selected_W(int group, int allocated)
    {
        NativeBattleStaticMeshResource source = Source(group);
        byte[] changed = source.Bytes.ToArray();
        changed.AsSpan(8, 32).Clear();
        source = source with { Bytes = changed.ToImmutableArray() };
        NativeBattleWindowedMeshStep step = Draw(source, Context(), SelectedMemory());
        Assert.Equal(allocated, step.Batch.AllocationBytes);
        Assert.Equal(5, step.Batch.Attempts.Length);
        Assert.Equal(new[] { 0, 52, 64, 0, 72 }, step.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Equal(new uint[] { 0x02000000, 0x01000000, 0x0C000000, 0x02000000 }, step.Batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.Primitive!.Words[0]));
        Assert.Equal(0xE1000220u, step.Batch.Attempts[2].Primitive!.Words[1]);
        Assert.Equal(SelectedAddress, U32(step.Context, 4));
        Assert.Equal(Address + (uint)source.Bytes.Length, U32(step.Context, 0x24));
        Assert.Equal(step.Memory.GeometryRegisters!.Otz, U32(step.Context, 0x30));
        Assert.False(step.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(step.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Throws<NotSupportedException>(() => step.Context.Read(0x38, 4));
        Assert.Equal(new uint[] { 0xE2000000, 0xE2000010 }, step.Batch.Attempts.Where(attempt => attempt.Primitive is not null && attempt.Primitive.Words.Length == 3).Select(attempt => attempt.Primitive!.Words[1]).ToArray());
    }

    [Theory]
    [InlineData(7)]
    public void Near_rejection_does_not_read_fourth_selected_XYZ_or_unreached_windows(int group)
    {
        List<uint> reads = [];
        NativeBattleWindowedMeshStep step = Draw(Source(group, badFourth: true), Context().MarkUnknown(0x10, 20), SelectedMemory(0), reads);
        Assert.Equal(new uint[] { SelectedAddress, SelectedAddress + 8, SelectedAddress + 16 }, reads);
        Assert.Null(Assert.Single(step.Batch.Attempts).Primitive);
        Assert.Equal(0, step.Batch.AllocationBytes);
        Assert.NotEqual(0u, U32(step.Context, 0x34) & 0x60000);
        Assert.Throws<NotSupportedException>(() => step.Context.Read(0x30, 4));
    }

    [Fact]
    public void Dispatcher_reads_only_group7_despite_other_group_counts()
    {
        NativeBattleStaticMeshResource source = Source(7);
        byte[] bytes = source.Bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), int.MaxValue);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(44), int.MaxValue);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(48), int.MaxValue);
        NativeBattleWindowedMeshStep step = Draw(source with { Bytes = bytes.ToImmutableArray() }, Context(), SelectedMemory());
        Assert.Equal(84, step.Batch.AllocationBytes);
        Assert.Equal(Address + (uint)bytes.Length, U32(step.Context, 0x24));
    }

    [Theory]
    [InlineData(7)]
    public void U_offset_and_both_window_layouts_survive_JSON_without_alias_context(int group)
    {
        NativeBattleEffectArena context = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(Context(240)))!;
        NativeBattleWindowedMeshStep step = Draw(Source(group), context, SelectedMemory());
        NativeBattleGfGeometryAttempt polygon = Assert.Single(step.Batch.Attempts, attempt => attempt.Primitive is not null && attempt.ByteOffsetRelative == 0);
        int[] offsets = group == 3 ? [12, 20, 28, 36] : [12, 24, 36, 48];
        Assert.Equal(new byte[] { 128, 144, 160, 176 }, offsets.Select(index => polygon.Data[index]).ToArray());
        Assert.Equal(new byte[] { 16, 32, 48, 64 }, offsets.Select(index => polygon.Data[index + 1]).ToArray());
        Assert.Throws<NotSupportedException>(() => step.Context.Read(0x38, 4));
    }

    private const uint Address = 0x1234000;
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleEffectMemory Memory(int z = 2000)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        return memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, null), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    }

    private const int SelectedOffset = 0x5000;
    private const uint SelectedAddress = NativeBattleEffectNodeMemory.ArenaAddress + SelectedOffset;
    private static NativeBattleEffectArena Context(ushort offset = 0) => NativeBattleEffectArena.Clear().MarkUnknown(0, 92).Write(0, BitConverter.GetBytes(Address)).Write(4, BitConverter.GetBytes(SelectedAddress)).Write(0x10, BitConverter.GetBytes(offset)).Write(0x14, Window(0, 0, 256, 256)).Write(0x1C, Window(0, 0, 128, 256));

    private static NativeBattleWindowedMeshStep Draw(NativeBattleStaticMeshResource source, NativeBattleEffectArena context, NativeBattleEffectMemory memory, List<uint>? reads = null)
    {
        return NativeBattleWindowedMesh.Draw(memory, source, context, Projection, NativeBattleWindowedMeshLayout.OilShot136, ReadVertex);

        (NativeBattleVector Vertex, short? W) ReadVertex(uint address)
        {
            reads?.Add(address);
            long offset = (long)address - NativeBattleEffectNodeMemory.ArenaAddress;
            if (offset < SelectedOffset || offset > SelectedOffset + 24)
            {
                throw new InvalidDataException("Selected vertex leaves its actual mutable bank.");
            }
            NativeBattleEffectArena arena = memory.Arena!;
            byte[] xyz = arena.Read((int)offset, 6);
            (byte[] bytes, bool[] known) = arena.ReadSnapshot((int)offset + 6, 2);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null);
        }
    }

    private static NativeBattleEffectMemory SelectedMemory(int z = 2000)
    {
        NativeBattleStaticMeshResource source = Source(7);
        return Memory(z) with { Arena = NativeBattleEffectArena.Clear().Write(SelectedOffset, source.Bytes.AsSpan().Slice(8, 32)).MarkUnknown(SelectedOffset + 30, 2) };
    }

    private static byte[] Window(ushort x, ushort y, ushort width, ushort height)
    {
        byte[] bytes = new byte[8];
        ushort[] values = [x, y, width, height];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), values[index]);
        }
        return bytes;
    }
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static NativeBattleStaticMeshResource Source(int group, bool badFourth = false, bool reverse = false, bool degenerate = false)
    {
        int faceBytes = group switch { 3 or 5 => 24, 4 => 20, 7 => 36, _ => throw new ArgumentOutOfRangeException(nameof(group)) };
        byte[] bytes = new byte[40 + 32 + faceBytes];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        NativeBattleVector[] vertices = [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0), new(400, 400, 0)];
        for (int vertex = 0; vertex < 4; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + vertex * 8), vertices[vertex].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + vertex * 8), vertices[vertex].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + vertex * 8), vertices[vertex].Z);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + vertex * 8), unchecked((short)((vertex + 1) * 0x1111)));
        }
        int cursor = 40;
        for (int current = 0; current < 8; current++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), current == group ? 1 : 0);
            cursor += 4;
            if (current != group)
            {
                continue;
            }
            uint command = group switch { 3 => 0x2E112233, 4 => 0x32112233, 5 => 0x3A112233, _ => 0x3E112233 };
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), command);
            int count = group == 4 ? 3 : 4;
            for (int corner = 0; corner < count; corner++)
            {
                int selected = degenerate ? 0 : reverse && corner < 3 ? 2 - corner : corner;
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 4 + corner * 2), badFourth && corner == 3 ? (ushort)0xFFFE : (ushort)(selected * 2));
            }
            if (group is 3 or 7)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 12), 0x12341010);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 16), 0x56782020);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 20), 0x40403030);
            }
            if (group >= 4)
            {
                int colors = cursor + (group == 7 ? 24 : 12);
                uint[] values = [0xA1224466, 0xB16688AA, 0xC1AACCEE];
                for (int color = 0; color < count - 1; color++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(colors + color * 4), values[color]);
                }
            }
            cursor += faceBytes;
        }
        return new(0, Address, bytes.ToImmutableArray());
    }
}
