using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeGameOverTests
{
    [Fact]
    public void GameOverWriterRetainsEveryOtherKnownOrUnknownPacketWord()
    {
        NativeFieldRequestPacket known = new() { Kind = 9, FieldId = 601, X = -32768, Y = 32767, ExtraWord = -17, Triangle = 0xFEDC, DirectionWord = 0xABCD };
        Assert.Equal(known with { Kind = 4 }, known.WithGameOverRequest());
        Assert.Equal(new NativeFieldRequestPacket { Kind = 4 }, new NativeFieldRequestPacket().WithGameOverRequest());
        Assert.Throws<InvalidDataException>(() => (known with { Version = 2 }).WithGameOverRequest());
    }

    [Fact]
    public void LossAcceptanceRequiresFinalKindFourAndRetainsAllOtherSessionOwners()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.RequestField(600, 0);
        NativeGameOverReceipt receipt = Receipt();
        GameState before = session.State;
        Assert.Throws<InvalidOperationException>(() => session.SetNativeGameOver(receipt));
        Assert.Same(before, session.State);
        session.SetFieldRequestPacket(new NativeFieldRequestPacket { Kind = 4 });
        GameState packetPublished = session.State;
        session.SetNativeGameOver(receipt);
        Assert.Equal(packetPublished with { NativeGameOver = receipt }, session.State);
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(receipt, JsonSerializer.Deserialize<NativeGameOverReceipt>(JsonSerializer.Serialize(receipt)));
        session.SetNativeGameOver(null);
        Assert.Equal(packetPublished, session.State);
    }

    [Fact]
    public void ReceiptRejectsInventedKindsSourcesAndVersions()
    {
        NativeGameOverReceipt receipt = Receipt();
        foreach (NativeGameOverReceipt invalid in new[] { receipt with { RequestKind = 1 }, receipt with { Version = 2 }, receipt with { Operation = -1 }, receipt with { Frame = 0 }, receipt with { ProgramHash = "unknown" }, receipt with { ExecutableHash = new string('0', 64) } })
        {
            Assert.Throws<InvalidDataException>(invalid.Validate);
        }
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeGameOverReceipt>("{}"));
    }

    private static NativeGameOverReceipt Receipt() => new(1, 600, new string('A', 64), NativeFieldRuntimeProfile.FF8EnExecutableHash, 0, 1, 42, 12, 4);
}
