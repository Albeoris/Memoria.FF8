using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaPacketArenaTests
{
    [Fact]
    public void Extended_sprite_E1_has_two_logical_words_and_twelve_physical_bytes_with_known_and_unknown_padding_preserved()
    {
        byte[] resource = new byte[60];
        resource[4] = 255;
        BinaryPrimitives.WriteUInt16LittleEndian(resource.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(resource.AsSpan(10), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(resource.AsSpan(16), 0x80000002);
        for (int index = 0; index < 2; index++)
        {
            int quad = 20 + index * 20;
            resource[quad] = 16;
            resource[quad + 1] = 128;
            resource[quad + 2] = 8;
            resource[quad + 3] = 0x2E;
            BinaryPrimitives.WriteUInt16LittleEndian(resource.AsSpan(quad + 14), 0x123);
        }
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 1000 };
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource.ToImmutableArray(), 0, matrix, new(matrix, 216, 160, 108), 0, 17, 2);
        Assert.Equal(92, batch.AllocationBytes);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(27);
        memory = memory with { Arena = memory.Arena!.Write(128, Enumerable.Repeat((byte)0xA5, 92).ToArray()).MarkUnknown(178, 1) };
        NativeBattleEffectPacketWrite result = NativeBattleEffectPacketArena.Append(memory, 128, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(220, result.Cursor);
        Assert.Equal(new[] { 168, 128, 180 }, result.Ordering.Proxies.Select(proxy => proxy.Packet.ByteOffset));
        NativeBattleLogicalPacket mode = result.Ordering.Packets.Single(packet => packet.Reference.ByteOffset == 168);
        Assert.Equal(new uint[] { 0x01000000, 0xE1000220 }, mode.Words);
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.Memory.Arena!.Read(176, 2));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena.Read(178, 1));
        Assert.Equal(new byte[] { 0xA5 }, result.Memory.Arena.Read(179, 1));
        NativeBattleEffectPacketWrite clone = JsonSerializer.Deserialize<NativeBattleEffectPacketWrite>(JsonSerializer.Serialize(result))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(clone));
    }

    [Fact]
    public void Physical_padding_does_not_allow_logical_words_beyond_receipt_or_a_tag_past_the_logical_prefix()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(27);
        ImmutableArray<byte> bytes = ImmutableArray.CreateRange(new byte[12]);
        ImmutableArray<NativeBattleGfPacketByteState> mask = ImmutableArray.CreateRange(Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 12));
        NativeBattleGfGeometryAttempt attempt = new(0, bytes, mask, new(17, [0x02000000, 0xE1000220], [0, 0, 0, 0]));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectPacketArena.Append(memory, 0, NativeBattleLogicalOrdering.Clear(), new([attempt], 12)));
        attempt = attempt with { Primitive = new(17, [0x03000000, 0xE1000220, 0, 0], [0, 0, 0, 0]) };
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectPacketArena.Append(memory, 0, NativeBattleLogicalOrdering.Clear(), new([attempt], 12)));
    }
}
