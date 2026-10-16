using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeUnenteredReturnTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(unchecked((int)0xFEDCBA98))]
    public void PhysicalMemoryRetainsPoppedFullDwordsAndKeepsUnknownsUnknown(int operand)
    {
        NativeActorOperandMemory memory = NativeActorOperandMemory.Unknown() with { LiveDepth = -1 };
        memory = memory.Push(NativeScalarValue.Exact(operand));
        memory = memory.Push(NativeScalarValue.Exact(37));
        memory = memory.Pop();
        Assert.Equal(0, memory.LiveDepth);
        Assert.Equal(operand, memory.Words[0]!.RequireExact());
        Assert.Equal(37, memory.Words[1]!.RequireExact());
        Assert.Null(memory.Words[2]);
        Assert.All(memory.SavedDepths, depth => Assert.Null(depth));
        NativeActorOperandMemory unknownDepth = NativeActorOperandMemory.Unknown().Push(NativeScalarValue.Exact(9));
        Assert.Null(unknownDepth.LiveDepth);
        Assert.All(unknownDepth.Words, word => Assert.Null(word));
        byte?[] invalid = new byte?[8];
        invalid[5] = 128;
        Assert.Throws<InvalidDataException>(() => (memory with { SavedDepths = invalid }).Validate());
        NativeActorOperandMemory allocated = NativeActorOperandMemory.Allocated();
        Assert.Equal((byte)255, allocated.SavedDepths[0]);
        Assert.Equal((byte)0, allocated.SavedDepths[5]);
        Assert.Throws<NotSupportedException>(() => allocated.Pop());
    }

    [Fact]
    public void Original948Ret5ConsumesStalePhysicalWordAndEntersQueuedLowerLbl()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.NativeUnenteredReturnTests.1");
        GameState initial = GameSession.Load(source);
        JsonNode explicitMigration = JsonNode.Parse(initial.EventState!.Value.GetRawText())!;
        NativeActorOperandMemory partial = NativeActorOperandMemory.Unknown() with { LiveDepth = -1 };
        partial.SavedDepths[5] = 0;
        explicitMigration["Actors"]![11]!["NativeOperandMemory"] = JsonSerializer.SerializeToNode(partial);
        GameSession session = new(initial with { EventState = JsonSerializer.SerializeToElement(explicitMigration) });
        using GameWorld world = new(session, directory);
        world.EnsureField();
        JsonElement beforeReturn = default;
        JsonElement? targetLbl = null;
        world.Events.CommandExecuted += command =>
        {
            if (command is { ObjectId: 11, ScriptId: 61, Name: "LBL" })
            {
                targetLbl = world.Events.Capture();
            }
        };
        for (int callback = 0; callback < 110; callback++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
            JsonElement completed = world.Events.Capture();
            if (completed.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ObjectId").GetInt32() == 11 && fiber.GetProperty("ScriptId").GetInt32() == 62 && fiber.GetProperty("Position").GetInt32() == 854 && !fiber.GetProperty("Complete").GetBoolean()))
            {
                beforeReturn = completed;
            }
        }
        Assert.Null(world.Events.LastFault);
        Assert.Equal(2734, world.Events.Frame);
        Assert.Equal(120, session.State.EventMemory.Read(1031, 1).Values[0]);
        Assert.Equal(108, session.State.EventMemory.Read(1032, 1).Values[0]);
        Assert.NotEqual(JsonValueKind.Undefined, beforeReturn.ValueKind);
        JsonElement physical = beforeReturn.GetProperty("Actors")[11].GetProperty("NativeOperandMemory");
        Assert.Equal(7, physical.GetProperty("LiveDepth").GetInt32());
        Assert.Equal(131, physical.GetProperty("Words")[0].GetInt32());
        Assert.Equal(1, physical.GetProperty("Words")[8].GetInt32());
        Assert.Equal(0, physical.GetProperty("SavedDepths")[5].GetInt32());
        Assert.NotNull(targetLbl);
        JsonElement lower = targetLbl!.Value.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 11 && fiber.GetProperty("ScriptId").GetInt32() == 61);
        Assert.True(lower.GetProperty("NativeResultFrameEntered").GetBoolean());
        Assert.Equal(1, lower.GetProperty("NativeDoorSavedResults")[0].GetInt32());
        NativeActorOperandMemory memory = world.Events.Actors[11].NativeOperandMemory!;
        Assert.Equal((byte)0, memory.SavedDepths[5]);
        Assert.NotNull(memory.Words[8]);
        GameSession restoredSession = new(initial with { EventState = beforeReturn });
        using GameWorld restored = new(restoredSession, directory);
        restored.EnsureField();
        restored.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: checked((int)(world.Events.Frame - restored.Events.Frame))));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        JsonNode unknownWord = JsonNode.Parse(beforeReturn.GetRawText())!;
        unknownWord["Actors"]![11]!["NativeOperandMemory"]!["Words"]![8] = null;
        GameSession unknownSession = new(initial with { EventState = JsonSerializer.SerializeToElement(unknownWord) });
        using GameWorld unknown = new(unknownSession, directory);
        unknown.EnsureField();
        Assert.Throws<EventScriptException>(() => unknown.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.Contains("DWORD8", unknown.Events.LastFault!.Message);
        JsonElement blocked = unknown.Events.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 11 && fiber.GetProperty("ScriptId").GetInt32() == 62);
        Assert.Equal(854, blocked.GetProperty("Position").GetInt32());
        Assert.Equal(0, blocked.GetProperty("Stack").GetArrayLength());
        GameSession legacySession = new(initial);
        using GameWorld legacy = new(legacySession, directory);
        legacy.EnsureField();
        Assert.Throws<EventScriptException>(() => legacy.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 110)));
        Assert.Contains("saved BYTE380 depth", legacy.Events.LastFault!.Message);
    }

    private static string Repository()
    {
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        return directory!.FullName;
    }
}
