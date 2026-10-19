using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278ChildDrawingTests
{
    [Fact]
    public void Ray_applies_Z_column_scale_and_X_order_without_rotating_its_projected_translation()
    {
        byte[] table = new byte[16384];
        BinaryPrimitives.WriteInt16LittleEndian(table.AsSpan(2), 4096);
        NativeBattleModulePoolTask parent = new(1, 0, null, 0x6822F0, new byte[256].ToImmutableArray());
        parent = parent.WriteWord(0x54, 0).WriteWord(0x56, 100).WriteWord(0x58, 200);
        NativeBattleModulePoolTask task = new(1, 0, null, 0x683760, new byte[16].ToImmutableArray());
        task = task.WriteWord(0xC, 13).WriteWord(0xE, 1024);
        NativeBattleActionMatrix cached = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4000, 4096);
        NativeBattleActionMatrix result = NativeBattleCarbuncle278ChildDrawing.Prepare683760(task, parent, cached, table.ToImmutableArray());
        Assert.Equal(new short[] { 4096, 0, 0, 0, 0, -2048, 0, 4096, 0 }, result.Rotation);
        Assert.Equal((0, 100, 4200), (result.X, result.Y, result.Z));
        NativeBattleActionMatrix full = NativeBattleCarbuncle278ChildDrawing.Prepare683760(task.WriteWord(0xC, 10).WriteWord(0xE, 0), parent, cached, table.ToImmutableArray());
        Assert.Equal(cached.Rotation, full.Rotation);
    }

    [Fact]
    public void Cached_matrix_reads_only_native_eighteen_rotation_and_twelve_translation_bytes()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        const int row = 0x1FA0;
        byte[] rotation = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2), (short)(index + 1));
        }
        byte[] translation = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(translation, -100000);
        BinaryPrimitives.WriteInt32LittleEndian(translation.AsSpan(4), 200000);
        BinaryPrimitives.WriteInt32LittleEndian(translation.AsSpan(8), -300000);
        arena = arena.Write(row + 0x34, rotation).Write(row + 0x48, translation).MarkUnknown(row + 0x46, 2);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = arena };
        NativeBattleActionMatrix result = NativeBattleCarbuncle278ChildDrawing.ReadCached(memory, 0x20DFAB8 + row);
        Assert.Equal(new short[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, result.Rotation);
        Assert.Equal((-100000, 200000, -300000), (result.X, result.Y, result.Z));
        Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278ChildDrawing.ReadCached(memory with { Arena = arena.MarkUnknown(row + 0x35, 1) }, 0x20DFAB8 + row));
    }
}
