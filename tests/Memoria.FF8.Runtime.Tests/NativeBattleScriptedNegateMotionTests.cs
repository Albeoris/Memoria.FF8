using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedNegateMotionTests
{
    [Theory]
    [InlineData((short)0, 123u, 0xFFFFFF85u)]
    [InlineData((short)-1, 0x80000000u, 0x80000000u)]
    [InlineData((short)20, 0xFFFFFFFFu, 1u)]
    [InlineData((short)-20, 0u, 0u)]
    public void Negation_uses_signed_unrestricted_index_and_updates_before_cache_and_current_PC_advance(short index, uint value, uint expected)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0xFED2);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x80001000);
        byte[] operand = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(operand, index);
        int offset = 0x2000 + 0x50 + 4 * index;
        frame = frame with { Arena = frame.Arena.Write(0x1002, operand).Write(offset, BitConverter.GetBytes(value)).MarkUnknown(0x1004, 4) };
        List<string> calls = [];
        NativeBattleWaterExecutionFrame result = NativeBattleWaterScalarReferenceCommands.NegateMotion(frame, new(0x1000, 4, 0x80001000), current =>
        {
            calls.Add("update");
            Assert.Equal(expected, BinaryPrimitives.ReadUInt32LittleEndian(current.Arena.Read(offset, 4)));
            return WaterSchedulerFixture.CoreDword(current, 0x2797450, 0x80002000);
        }, current =>
        {
            calls.Add("cache");
            Assert.Equal(0x80002000u, WaterSchedulerFixture.ReadCoreDword(current, 0x2797450));
            return WaterSchedulerFixture.CoreDword(current, 0x2797450, 0x80003000);
        });
        Assert.Equal(new[] { "update", "cache" }, calls);
        Assert.Equal(0x80003004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        string json = NativeBattleTestSnapshots.Json(result);
        Assert.Equal(json, NativeBattleTestSnapshots.Json(NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(json)!));
    }
}
