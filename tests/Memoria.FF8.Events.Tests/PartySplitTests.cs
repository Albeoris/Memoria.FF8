using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void GardenNativeSplitBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(137);
        Dictionary<EventEngine, GameSession> sessions = [];
        EventEngine Create()
        {
            GameSession session = new(GameSession.Load(TestResources.FixturePath("Events.Tests.PartySplitTests.1")));
            EventEngine result = new(session) { AnimationFrameCount = field.GetAnimationFrameCount, ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z) };
            result.LoadField(137, field.Jsm, field.Messages);
            result.Restore(session.State.EventState!.Value);
            sessions[result] = session;
            return result;
        }
        EventEngine first = Create();
        EventEngine resumed = Create();
        NativeSavedWorldState savedWorld = sessions[first].State.SavedWorld ?? throw new InvalidOperationException("Preferred308 source has no typed saved-world provenance.");
        JsonElement? traceAtSplit = null;
        first.CommandExecuted += command =>
        {
            if (command.Name == "SPLIT")
            {
                traceAtSplit ??= first.Capture().GetProperty("Scene").GetProperty("PartyTrace");
            }
        };
        PlayerAction wait = new(PlayerActionKind.Wait);
        first.Apply(wait);
        JsonElement begun = first.Capture();
        JsonElement split = begun.GetProperty("PartySplit");
        JsonElement leader = split.GetProperty("Targets")[0];
        JsonElement follower = split.GetProperty("Targets")[1];
        Require(first.Frame == 309 && leader.GetProperty("ObjectId").GetInt32() == 0 && follower.GetProperty("ObjectId").GetInt32() == 5 && split.GetProperty("Targets").GetArrayLength() == 2 && leader.GetProperty("TargetY").GetInt32() == -2252 * 4096 && follower.GetProperty("TargetX").GetInt32() == -88 * 4096 && follower.GetProperty("TargetY").GetInt32() == -2350 * 4096, "SPLIT lost native nine-operand order, compact targets or FF exclusion.");
        Require(leader.GetProperty("FixedX").GetInt32() == 0 && leader.GetProperty("FixedY").GetInt32() == -10607472 && follower.GetProperty("FixedX").GetInt32() == -29715 && follower.GetProperty("FixedY").GetInt32() == -11231194 && first.Actors[0].AnimationFrame == 0 && first.Actors[5].AnimationFrame == 1 && first.Actors[0].MovementStopDistance == 8 && first.Actors[5].MovementStopDistance == 8, "First-entry SPLIT motion, raw fractional source or per-native-actor animation phase differs.");
        Require(begun.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 4).GetProperty("Stack").GetArrayLength() == 0, "SPLIT did not consume its nine original coordinates once.");
        resumed.Restore(begun);
        int ticks = 0;
        while (first.Capture().GetProperty("PartySplit").ValueKind != JsonValueKind.Null && ticks++ < 100)
        {
            first.Apply(wait);
            resumed.Apply(wait);
            Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()) && sessions[first].State.SavedWorld == savedWorld && sessions[resumed].State.SavedWorld == savedWorld && JsonElement.DeepEquals(traceAtSplit!.Value, first.Capture().GetProperty("Scene").GetProperty("PartyTrace")), "SPLIT restore changed raw movement, trace, saved-world slots or native waits.");
        }
        Require(first.Capture().GetProperty("PartySplit").ValueKind == JsonValueKind.Null && first.LastFault is null && ticks < 100, "Reached native SPLIT did not complete bounded original movement.");
        Console.WriteLine($"Original137 SPLIT: frame308â†’309 consumes9, leader/follower move sameupdate with animation0/1; exact raw midmovement restore, unchangedtrace/savedworld; completed frame{first.Frame}, next {first.NextTrigger.Kind}, leader({first.Actors[0].X},{first.Actors[0].Y}) follower({first.Actors[5].X},{first.Actors[5].Y}).");

        EventEngine refused = Create();
        JsonElement before = refused.Capture();
        try
        {
            refused.Apply(wait);
            throw new InvalidOperationException("SPLIT second-actor geometry refusal silently completed movement.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "SPLIT")
        {
        }
        Require(refused.Actors[0].X == 0 && refused.Actors[0].Y == before.GetProperty("Actors")[0].GetProperty("Y").GetSingle() && refused.Actors[5].X == before.GetProperty("Actors")[5].GetProperty("X").GetSingle() && refused.Actors[5].Y == before.GetProperty("Actors")[5].GetProperty("Y").GetSingle(), "SPLIT committed the earlier actor before a later geometry refusal.");
        JsonElement frozen = refused.Capture();
        refused.Restore(frozen);
        Require(JsonElement.DeepEquals(frozen, refused.Capture()) && refused.LastFault?.Opcode == "SPLIT", "Failed native SPLIT lost its exact frozen raw targets/wait.");

        GameSession clearedSession = new(new GameState { SavedWorld = savedWorld, EventMemory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [256] = 9 }) });
        EventEngine clear = new(clearedSession);
        clear.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.CLEAR, 0), (Jsm.Opcode.RET, 0)), []);
        clear.Step();
        Require(clearedSession.State.Variables.Count == 0 && clearedSession.State.SavedWorld == savedWorld, "Field CLEAR erased typed saved-world slots along with event variables.");
    }
}
