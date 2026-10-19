using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectIoCompletionTests
{
    [Theory]
    [InlineData(-1, 255)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void Generic_completion_queues_actual_arena_tim_and_keeps_signed_type_low_byte_delay(short type, byte delay)
    {
        NativeBattleEffectMemory source = NativeBattleEffectIo.Initialize534110(NativeBattleEffectMemory.FromColdProcess().BeginLoad(277), 64);
        Assert.Equal(0x534150u, source.EffectIo!.Callback);
        source = source with { Arena = source.Arena!.Write(512, Tim()) };
        source = NativeBattleEffectIo.Queue5341D0(source, 717, NativeBattleEffectNodeMemory.ArenaAddress + 512, type);
        NativeBattleEffectIoAdvance begun = NativeBattleEffectIo.Advance534210(source);
        Assert.Equal(new NativeBattleEffectFileRequest(717, NativeBattleEffectNodeMemory.ArenaAddress + 512, 0x534150), begun.Request);
        NativeBattleEffectIoCompletion result = NativeBattleEffectIo.Complete534150(begun.Memory);
        Assert.Null(result.Stream);
        Assert.Equal(new byte[] { 0, delay, 1, 1, 1 }, result.Memory.Arena!.Read(64, 5));
        Assert.Equal(1, result.Memory.Textures!.Count);
        Assert.Equal(512, result.Memory.Textures.Slots[0]!.ArenaOffset);
        NativeBattleEffectArena current = result.Memory.Arena.Write(576, [0xCD, 0xAB]);
        NativeBattleEffectTextureDrain uploaded = result.Memory.Textures.Drain505D20(current);
        Assert.Equal((ushort)0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(uploaded.State.ReadVram().AsSpan(2 * (256 * 1024 + 64))));
        Assert.Equal(new byte[] { 1, 0, 1, 1, 0 }, begun.Memory.Arena!.Read(64, 5));
        NativeBattleEffectMemory independent = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(begun.Memory))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleEffectIo.Complete534150(independent)));
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    public void Generic_stream_reads_only_current_leading_word_owner_and_avoids_unreached_texture_owner(short type, byte delay)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectIo.Initialize534110(NativeBattleEffectMemory.FromColdProcess().BeginLoad(277), 64);
        memory = memory with { Textures = null, Arena = memory.Arena!.Write(512, new byte[] { 77, 0, 0, 0 }).MarkUnknown(516, 64) };
        memory = NativeBattleEffectIo.Queue5341D0(memory, 717, NativeBattleEffectNodeMemory.ArenaAddress + 512, type);
        memory = NativeBattleEffectIo.Advance534210(memory).Memory;
        NativeBattleEffectIoCompletion result = NativeBattleEffectIo.Complete534150(memory);
        Assert.Equal(new NativeBattleSummonStreamRequest(NativeBattleEffectNodeMemory.ArenaAddress + 512, 690077), result.Stream);
        Assert.Equal(new byte[] { 0, delay, 1, 1, 1 }, result.Memory.Arena!.Read(64, 5));
        Assert.Null(result.Memory.Textures);
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena.Read(516, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectIo.Complete534150(memory with { EffectIo = memory.EffectIo! with { Callback = 0x6C3580 } }));
    }

    [Fact]
    public void Generic_type16_does_not_borrow_Quezacotl_three_uploads_and_existing_special_remains_exact()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectIo.Initialize534110(NativeBattleEffectMemory.FromColdProcess().BeginLoad(277), 64);
        memory = NativeBattleEffectIo.Queue5341D0(memory, 717, NativeBattleEffectNodeMemory.ArenaAddress + 512, 16);
        memory = NativeBattleEffectIo.Advance534210(memory).Memory;
        NativeBattleEffectIoCompletion generic = NativeBattleEffectIo.Complete534150(memory with { Textures = null });
        Assert.Null(generic.Memory.Textures);
        Assert.Equal(new byte[] { 0, 0, 1, 1, 1 }, generic.Memory.Arena!.Read(64, 5));
        foreach (int offset in new[] { 0x2C6A4, 0x348C4, 0x38AE4 })
        {
            memory = memory with { Arena = memory.Arena!.Write(offset, Tim()) };
        }
        memory = memory with { EffectIo = memory.EffectIo! with { Callback = 0x6C3580 } };
        NativeBattleEffectIoCompletion quezacotl = NativeBattleEffectIo.Complete6C3580(memory, (_, _) => throw new InvalidOperationException());
        Assert.Equal(3, quezacotl.Memory.Textures!.Count);
        Assert.Equal(new int?[] { 0x2C6A4, 0x348C4, 0x38AE4 }, quezacotl.Memory.Textures.Slots.Take(3).Select(slot => slot!.ArenaOffset));
    }
    private static byte[] Tim()
    {
        byte[] data = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(56), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(58), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(62), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(64), 0x3210);
        return data;
    }
}
