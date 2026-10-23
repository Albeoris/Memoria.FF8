using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTaskBankCaptureTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Packed_bank_preserves_physical_links_typed_pointers_unknown_suffix_and_JSON(bool cpuRow)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 32, 2);
        (pool, _) = pool.Append(0x600000);
        (pool, _) = pool.Append(0x600100);
        NativeBattleModulePoolTask task = pool.Slots[0].WriteResourceRowReference(12, cpuRow ? new(8, null) : new(null, 0x1939018)).WriteWord(24, 0xCAFE).MarkPayloadUnknown(18, 2) with { State = 9 };
        pool = pool with { Slots = pool.Slots.SetItem(0, task) };
        NativeBattleEffectArena captured = NativeBattleEffectTaskMemory.CaptureBank(pool);
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(captured.Read(0, 2)));
        Assert.Equal((ushort)9, BinaryPrimitives.ReadUInt16LittleEndian(captured.Read(2, 2)));
        Assert.Equal(0x2020u, BinaryPrimitives.ReadUInt32LittleEndian(captured.Read(4, 4)));
        Assert.Equal(0x600000u, BinaryPrimitives.ReadUInt32LittleEndian(captured.Read(8, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(captured.Read(36, 4)));
        Assert.Equal(0x600100u, BinaryPrimitives.ReadUInt32LittleEndian(captured.Read(40, 4)));
        Assert.Equal(task.ReadResourcePointer(12), captured.ReadResourcePointer(12));
        Assert.Throws<NotSupportedException>(() => captured.Read(18, 2));
        Assert.Equal((ushort)0xCAFE, BinaryPrimitives.ReadUInt16LittleEndian(captured.Read(24, 2)));
        NativeBattleEffectArena restored = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(captured))!;
        Assert.Equal(captured.ReadResourcePointer(12), restored.ReadResourcePointer(12));
        Assert.Throws<NotSupportedException>(() => restored.Read(18, 2));
        Assert.Equal(captured.Read(20, 44), restored.Read(20, 44));
        Assert.Equal((ushort)1, pool.Slots[0].Flags);
        Assert.Equal(0, pool.Head);
    }
}
