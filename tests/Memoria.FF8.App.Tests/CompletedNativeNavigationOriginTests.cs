using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CompletedNativeNavigationOriginTests
{
    [Fact]
    public void CompletedNativeMoveAdmitsOnlyObservedTriangleAndAppliesTheCurrentProposal()
    {
        var fixture = Create();
        ActorState player = Assert.Single(fixture.Engine.Actors);
        Assert.Equal(49, player.WalkmeshTriangle);
        Assert.Equal(new FieldFixedPosition(10 * 4096, 20 * 4096, 0), fixture.Engine.Scene.NativePlayerPosition);
        Assert.Equal(0, fixture.Session.State.Variables[194]);
        Assert.Equal((byte)0, fixture.Engine.Scene.PartyBindingMemory!.First);
        Assert.True(player.NativeScriptMovement is { Version: 1, Active: false, Completion: 2, Cancelled: false });
        List<int> observedTriangles = [];
        fixture.Engine.CanUseNativeScriptMove = actor =>
        {
            observedTriangles.Add(actor.WalkmeshTriangle);
            return true;
        };
        long before = fixture.Engine.Frame;
        PlayerActionResult result = fixture.Engine.ReplayNavigation(Entry(fixture.Engine));
        Assert.True(result.Accepted);
        Assert.Equal(1, result.FramesAdvanced);
        Assert.Equal(before + 1, fixture.Engine.Frame);
        Assert.Equal([49], observedTriangles);
        Assert.Equal(48, player.WalkmeshTriangle);
        Assert.Equal(11, player.X);
        Assert.Equal(20, player.Y);
        Assert.Equal(new FieldFixedPosition(11 * 4096, 20 * 4096, 0), fixture.Engine.Scene.NativePlayerPosition);
        Assert.Null(player.NativeScriptMovement);
        Assert.NotEqual(65, player.WalkmeshTriangle);
    }

    [Theory]
    [InlineData("no-receipt")]
    [InlineData("changed-x")]
    [InlineData("changed-y")]
    [InlineData("changed-z")]
    [InlineData("cancelled")]
    [InlineData("active-move")]
    [InlineData("uncertified")]
    [InlineData("no-certificate")]
    [InlineData("missing-authority")]
    [InlineData("raw-disagreement")]
    [InlineData("saved-leader-disagreement")]
    public void SameTriangleDifferenceRefusesWithoutTheEntireCurrentNativeAuthority(string missing)
    {
        var fixture = Create(move: missing != "no-receipt", cancel: missing == "cancelled", steps: missing == "active-move" ? 2 : 3);
        EventEngine engine = fixture.Engine;
        ReplayJournalEntry entry = Entry(engine);
        if (missing.StartsWith("changed-", StringComparison.Ordinal))
        {
            ReplayNavigationStimulus stimulus = entry.Navigation[0];
            ReplayPlayerPose start = stimulus.Start;
            start = missing switch
            {
                "changed-x" => start with { X = start.X + 1 },
                "changed-y" => start with { Y = start.Y + 1 },
                _ => start with { Z = start.Z + 1 }
            };
            entry = entry with { Navigation = [stimulus with { Start = start }] };
        }
        if (missing == "uncertified")
        {
            engine.CanUseNativeScriptMove = _ => false;
        }
        if (missing == "no-certificate")
        {
            engine.CanUseNativeScriptMove = null;
        }
        if (missing is "missing-authority" or "raw-disagreement")
        {
            // This is a synthetic negative fixture from this freshly executed
            // program; no historical checkpoint, pose or journal is seeded.
            JsonNode malformed = JsonNode.Parse(engine.Capture().GetRawText())!;
            if (missing == "missing-authority")
            {
                malformed["Actors"]![0]!.AsObject().Remove("NativeMotionAuthority");
            }
            else
            {
                malformed["Scene"]!["NativePlayerPosition"]!["X"] = 10 * 4096 + 1;
            }
            engine.Restore(JsonSerializer.SerializeToElement(malformed));
        }
        if (missing == "saved-leader-disagreement")
        {
            fixture.Session.State.Variables[194] = 1;
        }
        if (missing == "cancelled")
        {
            Assert.True(engine.Actors[0].NativeScriptMovement is { Active: false, Cancelled: true });
        }
        if (missing == "active-move")
        {
            Assert.True(engine.Actors[0].NativeScriptMovement is { Active: true, Completion: 2 });
        }
        JsonElement before = engine.Capture();
        Assert.Throws<InvalidDataException>(() => engine.ReplayNavigation(entry));
        Assert.True(JsonElement.DeepEquals(before, engine.Capture()));
        Assert.Equal(49, engine.Actors[0].WalkmeshTriangle);
        Assert.Equal(10, engine.Actors[0].X);
    }

    private static ReplayJournalEntry Entry(EventEngine engine)
    {
        ReplayPlayerPose start = new(600, 10, 20, 0, 65, 0, 10 * 4096, 20 * 4096, 0);
        ReplayPlayerPose end = new(600, 11, 20, 0, 48, 0, 11 * 4096, 20 * 4096, 0);
        return new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 600, engine.Frame)
        {
            Code = (int)PlayerActionKind.Move,
            Accepted = true,
            Frames = 1,
            Advanced = 1,
            Horizontal = 1,
            Navigation = [new ReplayNavigationStimulus(start, end, true)]
        };
    }

    private static (GameSession Session, EventEngine Engine) Create(bool move = true, bool cancel = false, int steps = 3)
    {
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 600 });
        EventEngine engine = new(session, session.State.Variables)
        {
            AnimationFrameCount = (_, _) => 4,
            CanUseNativeScriptMove = _ => true,
            ResolveNativeTriangleHeight = (_, _, _) => 0,
            ResolvePartyTraceTriangle = (_, _, _) => 49,
            ResolveNativeScriptDirection = query => new NativeScriptDirectionResult(cancel ? NativeScriptDirectionStatus.Moving : NativeScriptDirectionStatus.Stopped, query.FixedX, query.FixedY, query.MovementDirection, query.TurnAccumulator),
            ResolveNativeScriptMovement = query => new NativeScriptMovementResult(NativeScriptMovementStatus.Blocked, query.FixedX, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new NativeScriptMovementProposal(query.FixedX, query.FixedY, query.Direction, [new(query.FixedX, query.FixedY, 0), new(query.FixedX, query.FixedY, 0), new(query.FixedX, query.FixedY, 0)])])
        };
        engine.LoadField(600, Program(move, cancel), [], new FieldTransition(600, 10, 20, 49, 0), nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        for (int index = 0; index < steps; index++)
        {
            engine.Step();
        }
        return (session, engine);
    }

    private static byte[] Program(bool move, bool cancel)
    {
        List<(Jsm.Opcode Opcode, int Parameter)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0), (Jsm.Opcode.LBL, 1)];
        if (move)
        {
            operations.AddRange([(Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (cancel ? Jsm.Opcode.RFMOVE : Jsm.Opcode.MOVE, 0)]);
        }
        if (cancel)
        {
            operations.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVECANCEL, 0)]);
        }
        operations.AddRange([(Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
        byte[] bytes = new byte[16 + operations.Count * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 7);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), checked((ushort)operations.Count));
        for (int index = 0; index < operations.Count; index++)
        {
            var operation = operations[index];
            int encoded = operation.Parameter != 0 || operation.Opcode == Jsm.Opcode.PSHN_L ? (int)operation.Opcode << 24 | operation.Parameter & 0xFFFFFF : (int)operation.Opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + 4 * index), encoded);
        }
        return bytes;
    }
}
