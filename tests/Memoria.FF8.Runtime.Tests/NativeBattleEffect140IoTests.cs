using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect140IoTests
{
    [Fact]
    public void Type16_uses_current_common_base_and_two_tick_delay_with_live_tim_bytes()
    {
        const uint common = NativeBattleEffectNodeMemory.ArenaAddress + 0x100;
        int[] offsets = [0x1F8A0, 0x21AC0, 0x23CE0];
        NativeBattleEffectMemory memory = Begin(16);
        for (int index = 0; index < offsets.Length; index++)
        {
            memory = memory with { Arena = memory.Arena!.Write(offsets[index], Tim((ushort)(64 + index), (ushort)(index + 1))) };
        }
        NativeBattleEffectIoCompletion completed = NativeBattleEffectIo.Complete6A6360(memory, common, (_, _) => throw new InvalidOperationException());
        Assert.Null(completed.Stream);
        Assert.Equal(new byte[] { 0, 2, 1, 1, 1 }, completed.Memory.Arena!.Read(64, 5));
        Assert.Equal(offsets.Select(value => (int?)value), completed.Memory.Textures!.Slots.Take(3).Select(slot => slot!.ArenaOffset));
        NativeBattleEffectMemory restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(completed.Memory)))!);
        NativeBattleEffectArena current = restored.Arena!.Write(offsets[2] + 64, [0xCD, 0xAB]);
        byte[] vram = restored.Textures!.Drain505D20(current).State.ReadVram();
        Assert.Equal((ushort)0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(vram.AsSpan(2 * (256 * 1024 + 66))));
        NativeBattleEffectIoAdvance tick = NativeBattleEffectIo.Advance534210(restored);
        Assert.Null(tick.Request);
        Assert.Equal((byte)1, tick.Memory.Arena!.Read(65, 1)[0]);
        Assert.Equal((byte)0, NativeBattleEffectIo.Advance534210(tick.Memory).Memory.Arena!.Read(65, 1)[0]);
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    public void Stream_reads_only_current_leading_dword_and_acknowledges(short type, byte delay)
    {
        NativeBattleEffectMemory memory = Begin(type);
        memory = memory with { Textures = null, Arena = memory.Arena!.Write(512, [122, 0, 0, 0]).MarkUnknown(516, 8) };
        NativeBattleEffectIoCompletion result = NativeBattleEffectIo.Complete6A6360(memory, NativeBattleEffectNodeMemory.ArenaAddress, (address, length) =>
        {
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 512, address);
            Assert.Equal(4, length);
            return new("current-source", memory.Arena!.Read(512, 4).ToImmutableArray());
        });
        Assert.Equal(new NativeBattleSummonStreamRequest(NativeBattleEffectNodeMemory.ArenaAddress + 512, 690122), result.Stream);
        Assert.Equal(new byte[] { 0, delay, 1, 1, 1 }, result.Memory.Arena!.Read(64, 5));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena.Read(516, 1));
    }

    [Fact]
    public void Callback_must_match_selected_initializer()
    {
        NativeBattleEffectMemory memory = Begin(16);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectIo.Complete6A6360(memory with { EffectIo = memory.EffectIo! with { Callback = 0x534150 } }, NativeBattleEffectNodeMemory.ArenaAddress, (_, _) => throw new InvalidOperationException()));
    }

    private static NativeBattleEffectMemory Begin(short type)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectIo.Initialize534110(NativeBattleEffectMemory.FromColdProcess().BeginLoad(140), 64);
        memory = memory with { EffectIo = memory.EffectIo! with { Callback = 0x6A6360 } };
        memory = NativeBattleEffectIo.Queue5341D0(memory, 530, NativeBattleEffectNodeMemory.ArenaAddress + 512, type);
        return NativeBattleEffectIo.Advance534210(memory).Memory;
    }

    private static byte[] Tim(ushort x, ushort pixel)
    {
        byte[] data = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(56), x);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(58), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(62), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(64), pixel);
        return data;
    }
}
