using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeLadderAnimationTests
{
    [Fact]
    public void Configuration_narrows_three_bytes_consumes_two_operands_and_continues_without_starting_animation()
    {
        (Jsm.Opcode, int)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.PSHN_L, 0x1234A), (Jsm.Opcode.LADDERANIME, 0x103), (Jsm.Opcode.POPM_W, 1100), (Jsm.Opcode.RET, 0)];
        byte[] program = new byte[16 + operations.Length * 4];
        program[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(12), (ushort)operations.Length);
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int operand) = operations[index];
            int raw = operand != 0 || opcode == Jsm.Opcode.PSHN_L ? (int)opcode << 24 | operand & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(program.AsSpan(16 + 4 * index), raw);
        }
        Dictionary<int, int> variables = [];
        EventEngine engine = new(new GameSession(new GameState()), variables);
        engine.LoadField(600, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Assert.Equal(new NativeLadderAnimationState(0, 0, 0), engine.Actors[0].NativeLadderAnimation);
        engine.Step();
        Assert.Equal(new NativeLadderAnimationState(3, 0x4A, 0xFF), engine.Actors[0].NativeLadderAnimation);
        Assert.Equal(77, variables[1100]);
        Assert.Equal((0, 0f), (engine.Actors[0].AnimationId, engine.Actors[0].AnimationFrame));
        Assert.Null(engine.Actors[0].NativeScriptMovement);
        JsonElement snapshot = engine.Capture();
        EventEngine restored = new(new GameSession(new GameState()), new Dictionary<int, int>());
        restored.LoadField(600, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        restored.Restore(snapshot);
        Assert.Equal(engine.Actors[0].NativeLadderAnimation, restored.Actors[0].NativeLadderAnimation);
        JsonObject missing = JsonNode.Parse(snapshot.GetRawText())!.AsObject();
        missing["Actors"]![0]!.AsObject().Remove("NativeLadderAnimation");
        Assert.Throws<JsonException>(() => restored.Restore(JsonSerializer.SerializeToElement(missing)));
    }
}
