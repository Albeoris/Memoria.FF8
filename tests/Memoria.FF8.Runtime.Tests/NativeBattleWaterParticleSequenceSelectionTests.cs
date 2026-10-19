using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattleWaterParticlesTests
{
    [Theory]
    [InlineData(0x7F10, 123u, 127, 123u)]
    [InlineData(0x8810, 0x12345678u, 3, 3018423131u)]
    [InlineData(0x8010, 0u, 0, 2531011u)]
    public void Sequence_selection_10_uses_literal_or_one_unsigned_random_index(ushort instruction, uint seed, int index, uint expectedSeed)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction);
        uint sequence = NativeBattleEffectNodeMemory.ArenaAddress + 0x5100;
        int record = 0x5100 + 8 * index;
        frame = frame with { Random = new(seed), Arena = frame.Arena.Write(Particle + 0xC, BitConverter.GetBytes(sequence)).Write(Particle + 0x4C, BitConverter.GetBytes(0xFFFFFF00u)).Write(record, BitConverter.GetBytes(0x300u)).Write(record + 4, BitConverter.GetBytes((ushort)0xF0A1)).MarkUnknown(record + 6, 2) };
        NativeBattleWaterExecutionFrame result = One(frame);
        Assert.Equal(sequence + (uint)(8 * index), Dword(result, 8));
        Assert.Equal(0x200u, Dword(result, 0x30));
        Assert.Equal((ushort)0xF0A1, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(result.Arena.Read(Particle + 0x34, 2)));
        Assert.Equal(sequence, Dword(result, 0xC));
        Assert.Equal(expectedSeed, result.Random.Seed);
        Assert.Equal(0x1002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(One(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!)));
    }
}
