using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class RescueMspeedTests
{
    [Theory]
    [InlineData(32768, -32768)]
    [InlineData(65535, -1)]
    [InlineData(42704, -22832)]
    [InlineData(341632, 13952)]
    [InlineData(0x123FFFF, -1)]
    public void MspeedConsumesFullOperandAndWritesSignedLowWord(int operand, int expected)
    {
        (Jsm.Opcode Opcode, int Parameter)[] ops = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8), (Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, operand >> 16), (Jsm.Opcode.PSHN_L, 65536), (Jsm.Opcode.CAL, 2), (Jsm.Opcode.PSHN_L, operand & 65535), (Jsm.Opcode.CAL, 0), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] bytes = new byte[18 + ops.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 18);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), (ushort)ops.Length);
        for (int index = 0; index < ops.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = ops[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.CAL ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18 + index * 4), encoded);
        }
        EventEngine engine = new(new GameSession(new GameState()));
        int? consumed = null;
        engine.CommandExecuted += command =>
        {
            if (command.Name == "MSPEED")
            {
                consumed = command.Arguments.Single();
            }
        };
        engine.LoadField(878, bytes, []);
        engine.Step();
        engine.Step();
        Assert.Null(engine.LastFault);
        Assert.Equal(operand, consumed);
        Assert.Equal(expected, engine.Actors[0].MovementSpeed);
        Assert.Equal((short)expected, engine.Actors[0].NativeMovementBaseSpeed!.Value);
        JsonElement fiber = engine.Capture().GetProperty("Fibers").EnumerateArray().Single(value => value.GetProperty("ScriptId").GetInt32() == 1);
        Assert.Empty(fiber.GetProperty("Stack").EnumerateArray());
    }

    [Theory]
    [InlineData(-32768)]
    [InlineData(-22832)]
    [InlineData(-1)]
    public void SignedSpeedReversesGeometryWithoutReversingFanDirection(short speed)
    {
        WalkmeshTriangle first = new(new Vector3(0, 0, 40), new Vector3(0, 1000, 40), new Vector3(1000, 0, 40), -1, 1, -1);
        WalkmeshTriangle second = new(new Vector3(0, 1000, 40), new Vector3(1000, 1000, 40), new Vector3(1000, 0, 40), -1, -1, 0);
        FieldNativeMovementRequest request = new(0, 400 << 12, 400 << 12, 40 << 12, 64, speed, 48, false, true, 0, true);
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(new([first, second]), request);
        Assert.Equal(FieldNativeMovementStatus.Accepted, result.Status);
        Assert.Equal((400 << 12) + 16 * speed, result.FixedX);
        Assert.Equal(400 << 12, result.FixedY);
        FieldNativeMovementProposal proposal = Assert.Single(result.Proposals);
        Assert.Equal(64, proposal.Direction);
        Assert.Equal(3, proposal.FanProbes.Count);
        Assert.All(proposal.FanProbes, fan => Assert.True(fan.Supported));
    }

    [Theory]
    [InlineData(-22832, 89, FieldNativeScriptDirectionStatus.Snapped)]
    [InlineData(-22832, 90, FieldNativeScriptDirectionStatus.Moving)]
    [InlineData(-32768, 128, FieldNativeScriptDirectionStatus.Moving)]
    public void SignedSpeedUsesSquaredStrictSnapThreshold(short speed, int target, FieldNativeScriptDirectionStatus expected)
    {
        Assert.Equal(expected, FieldNativeScriptDirectionProbe.Resolve(new(0, 0, target << 12, 0, speed, 0, 48, 64, 64, 0, 0)).Status);
    }

    [Fact]
    public void Actual878ReleaseExecutesOrdinaryMspeedAndColdRestoresSignedMovement()
    {
        GameSession session = new(GameSession.Load(RootFile("../fixtures/external-fixture.json")));
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory);
        bool ordinary = false;
        world.Events.CommandExecuted += command => ordinary |= command.Name == "MSPEED" && command.Arguments.Single() == 42704;
        Assert.True(world.Apply(new(PlayerActionKind.FieldButtons, Value: 0)).Accepted);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 4)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.True(ordinary);
        Assert.Equal(-22832, world.Events.Actors[2].MovementSpeed);
        Assert.Equal((short)-22832, world.Events.Actors[2].NativeMovementBaseSpeed!.Value);
        session.SetEventState(world.Events.Capture());
        string snapshot = Path.Combine(Path.GetDirectoryName(RootFile("../fixtures/external-fixture.md"))!, "rescue-signed-mspeed-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(snapshot);
            GameSession restoredSession = new(GameSession.Load(snapshot));
            using GameWorld restored = new(restoredSession, LaunchOptions.DefaultGameDirectory);
            for (int callback = 0; callback < 12; callback++)
            {
                Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
                Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
                Assert.Null(world.Events.LastFault);
                Assert.Null(restored.Events.LastFault);
                Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
            }
        }
        finally
        {
            File.Delete(snapshot);
        }
    }

    private static string RootFile(string relative)
    {
        return TestResources.FixtureFile(relative);
    }
}
