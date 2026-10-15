using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeLadderShortClipContract()
    {
        foreach (bool up in new[] { false, true })
        {
            foreach (int frames in new[] { 2, 1 })
            {
                EventEngine Create()
                {
                    EventEngine engine = CreateNativeLadder(up, followers: true);
                    engine.AnimationFrameCount = (_, animation) => animation == (up ? 5 : 3) ? frames : animation is 13 or 15 or 23 or 25 ? 1 : 4;
                    return engine;
                }
                EventEngine live = Create();
                ReachNativeLadderClimb(live);
                ActorState actor = live.Actors[0];
                Require(live.Scene.FieldPartyNativeActorIds.SequenceEqual([0, 1, 2]) && actor.NativeLadder!.Duration == 4, "Short-clip fixture lost its actual three-party bindings or positive climbing duration.");
                NativeLadderButtons(live, up ? 0x3000 : 0xC000);
                for (int tick = 0; tick < 4; tick++)
                {
                    live.Step();
                }
                Require(actor.NativeMovementCompletion542 == 4 && actor.NativeTurnAccumulator474 == 0, "Short-clip fixture did not reach its genuine forward-exit phase.");
                NativeLadderButtons(live, 0);
                live.Step();
                short duration = checked((short)(frames - 2));
                Require(actor.NativeLadder is { Active: true } && actor.NativeMovementCompletion542 == 6 && actor.NativeTurnAccumulator474 == 0 && actor.NativeLadder.Duration == duration && actor.NativeLadder.FixedX == actor.NativeLadder.ClimbingEnd.X && actor.NativeLadder.FixedX != actor.NativeLadder.Exit.X && actor.WalkmeshTriangle == 9 && actor.AnimationId == (up ? 5 : 3), $"Short ladder exit changed native phase: up={up}, requestedFrames={frames}, model={actor.RenderModelId}, clip={actor.AnimationId}, stage={actor.NativeMovementCompletion542}, counter={actor.NativeTurnAccumulator474}, duration={actor.NativeLadder?.Duration}/{duration}, X={actor.NativeLadder?.FixedX}/{actor.NativeLadder?.ClimbingEnd.X}/{actor.NativeLadder?.Exit.X}, triangle={actor.WalkmeshTriangle}.");
                JsonElement shortExit = live.Capture();
                EventEngine restored = Create();
                restored.Restore(shortExit);
                foreach (Action<JsonNode> corrupt in new Action<JsonNode>[]
                {
                    state => state["Actors"]![0]!["NativeMovementCompletion542"] = 1,
                    state => state["Actors"]![0]!["NativeLadder"]!["Duration"] = frames == 1 ? 0 : -1,
                    state => state["Actors"]![0]!["NativeTurnAccumulator474"] = 1
                })
                {
                    JsonNode malformed = JsonNode.Parse(shortExit.GetRawText())!;
                    corrupt(malformed);
                    try
                    {
                        live.Restore(JsonSerializer.SerializeToElement(malformed));
                        throw new InvalidOperationException("Short ladder duration escaped its exact just-finished exit phase.");
                    }
                    catch (InvalidDataException)
                    {
                    }
                    Require(JsonElement.DeepEquals(shortExit, live.Capture()), "Malformed short ladder phase partially mutated its restore target.");
                }
                live.Step();
                restored.Step();
                Require(actor.NativeMovementCompletion542 == 6 && actor.NativeTurnAccumulator474 == 1 && actor.NativeLadder!.Duration == 32 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "First follower-drain callback retained the former nonpositive exit duration or diverged after independent restore.");
                JsonElement drain = live.Capture();
                JsonNode badDrain = JsonNode.Parse(drain.GetRawText())!;
                badDrain["Actors"]![0]!["NativeLadder"]!["Duration"] = duration;
                try
                {
                    live.Restore(JsonSerializer.SerializeToElement(badDrain));
                    throw new InvalidOperationException("Follower drain accepted a nonpositive earlier clip duration after its first callback.");
                }
                catch (InvalidDataException)
                {
                }
                Require(JsonElement.DeepEquals(drain, live.Capture()), "Invalid nonpositive follower-drain duration partially restored.");
                for (int tick = 0; tick < 31; tick++)
                {
                    live.Step();
                    restored.Step();
                    Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Short-clip ladder follower drain changed physical trace, animation or completion after independent restore.");
                }
                Require(actor.NativeMovementCompletion542 == 2 && actor.NativeTurnAccumulator474 == 32 && actor.NativeLadder is { Active: true } && !CapturedVariables(live.Capture()).TryGetProperty("6000", out _), "Short-clip traversal skipped the original32-callback two-follower drain or released its VM instruction too early.");
                live.Step();
                restored.Step();
                Require(actor.NativeLadder is { Active: false } && live.Scene.PartyTrace is { FirstOffset: 15, SecondOffset: 30 } && CapturedVariables(live.Capture()).GetProperty("6000").GetInt32() == 73 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Short ladder did not complete return2 with its immediate successor and restored normal follower delays.");
            }
        }
        Console.WriteLine("Native mode4 short clips: one/two-frame leader exit durations−1/0, one-frame follower animation resources, old-counter placement/triangle transfer, exact transient restore scope,32-callback drain and return2 successor pass in both directions.");
    }
}
