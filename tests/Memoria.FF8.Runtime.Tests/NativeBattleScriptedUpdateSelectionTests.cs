using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedUpdateSelectionTests
{
    [Theory]
    [InlineData(0x004C)]
    [InlineData(0xFE4C)]
    public void Update4C_writes_full_kind_byte_before_dispatch_and_reloads_PC_without_consuming_padding(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x80001000);
        frame = frame with { Arena = frame.Arena.Write(0x1002, [0xE7]).MarkUnknown(0x1003, 1).MarkUnknown(0x2018, 2) };
        int calls = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleWaterScalarReferenceCommands.ChangeUpdate(frame, new(0x1000, 4, 0x80001000), current =>
        {
            calls++;
            Assert.Equal((byte)0xE7, current.Arena.Read(0x2018, 1)[0]);
            Assert.Throws<NotSupportedException>(() => current.Arena.Read(0x2019, 1));
            return WaterSchedulerFixture.CoreDword(current, 0x2797450, 0x80002000);
        });
        Assert.Equal(1, calls);
        Assert.Equal(0x80002004u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        string json = JsonSerializer.Serialize(result);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(json)!));
    }
}
