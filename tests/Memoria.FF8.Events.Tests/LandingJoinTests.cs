using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void GardenLandingJoinBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(137);
        const string source = "Events.Tests.LandingJoinTests.1";
        EventEngine Create()
        {
            GameSession session = new(GameSession.Load(TestResources.FixturePath(source)));
            EventEngine result = new(session)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                LoadSoundResource = _ => SoundResourceStatus.Muted,
                ResolveGlobalSoundResource = MutedGlobalSample,
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z)
            };
            result.LoadField(137, field.Jsm, field.Messages);
            result.Restore(session.State.EventState!.Value);
            return result;
        }
        EventEngine first = Create();
        EventEngine resumed = Create();
        Require(first.Frame == 226 && first.Actors[0].MovementTarget == new Vector3(0, -2879, 8341) && first.Actors[0].Y == -2877 && first.Actors[5].CollisionEnabled, "Actual137 pre-JOIN source lost its unfinished leader MOVE or enabled companion collision.");
        PlayerAction wait = new(PlayerActionKind.Wait);
        first.Apply(wait);
        resumed.Apply(wait);
        Require(first.Frame == 227 && first.Capture().GetRawText() == resumed.Capture().GetRawText() && first.Actors[0].MovementTarget is null && first.Actors[0].Y == -2879, "Original leader MOVE failed to finish before the reached JOIN fixed its targets.");
        JsonElement join = first.Capture().GetProperty("PartyJoin");
        Require(join.GetProperty("CallerObjectId").GetInt32() == 0 && join.GetProperty("Targets")[0].GetProperty("ObjectId").GetInt32() == 5 && join.GetProperty("Targets")[0].GetProperty("TargetY").GetInt32() == -2879 * 4096 && join.GetProperty("Targets")[0].GetProperty("Speed").GetInt32() == 7929 && first.Actors[5].AnimationId == 2 && first.Actors[5].CollisionEnabled && first.Actors[5].PushEnabled, "Landing JOIN lost its native run selection, completed leader target or unchanged companion flags.");
        resumed.Restore(first.Capture());
        FieldNativeMovementResult independentStep = FieldNativeMovementProbe.Resolve(field.Walkmesh, new FieldNativeMovementRequest(21, -88 * 4096, -2350 * 4096, 8341 * 4096, 7, 7929, 48, false, true, 0, true), first.Scene.LockedTriangles);
        Require(independentStep.Status == FieldNativeMovementStatus.Accepted && independentStep.FixedX == -338768 && independentStep.FixedY == -9750574 && independentStep.FixedZ == 8341 * 4096, "Independent original137 first JOIN geometry or signed native sine displacement differs.");
        int ticks = 0;
        while (first.Capture().GetProperty("PartyJoin").ValueKind != JsonValueKind.Null && ticks++ < 64)
        {
            first.Apply(wait);
            resumed.Apply(wait);
            Require(first.Capture().GetRawText() == resumed.Capture().GetRawText() && first.Actors[5].CollisionEnabled && first.Actors[5].PushEnabled, "Landing JOIN movement or unchanged enabled collision differed after mid-JOIN restore.");
            if (ticks == 1)
            {
                JsonElement target = first.Capture().GetProperty("PartyJoin").GetProperty("Targets")[0];
                Require(target.GetProperty("FixedX").GetInt32() == independentStep.FixedX && target.GetProperty("FixedY").GetInt32() == independentStep.FixedY && first.Actors[5].WalkmeshTriangle == independentStep.Triangle && first.Actors[5].Direction == 7, "Landing JOIN first step disagrees with the independently proved native movement component.");
            }
        }
        Require(first.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null && first.LastFault is null && first.Scene.NativePlayerPosition == new FieldFixedPosition(0, -2879 * 4096, 8341 * 4096) && first.Scene.FieldPartyNativeActorIds.SequenceEqual([0, 1, 255]), "Landing JOIN failed to refresh native trace/fixed leader position or changed compact bindings.");

        // Synthetic contact variant of the same component fixture. This is a
        // refusal test, not ordinary evidence of a different actor placement.
        EventEngine blocked = Create();
        blocked.Apply(wait);
        JsonNode variant = JsonNode.Parse(blocked.Capture().GetRawText())!;
        variant["Actors"]![11]!["X"] = -88;
        variant["Actors"]![11]!["Y"] = -2400;
        using JsonDocument variantDocument = JsonDocument.Parse(variant.ToJsonString());
        blocked.Restore(variantDocument.RootElement);
        JsonElement beforeContact = blocked.Capture();
        try
        {
            blocked.Apply(wait);
            throw new InvalidOperationException("Landing JOIN bypassed an enabled peer collision envelope.");
        }
        catch (EventScriptException) when (blocked.LastFault?.Opcode == "JOIN")
        {
        }
        JsonElement stopped = blocked.Capture();
        Require(beforeContact.GetProperty("Actors").GetRawText() == stopped.GetProperty("Actors").GetRawText() && beforeContact.GetProperty("PartyJoin").GetRawText() == stopped.GetProperty("PartyJoin").GetRawText(), "Enabled peer contact mutated JOIN actor positions or its fixed targets before refusal.");
        blocked.Restore(stopped);
        Require(stopped.GetRawText() == blocked.Capture().GetRawText() && blocked.LastFault is not null, "Landing JOIN contact boundary failed exact frozen restore.");
        Console.WriteLine($"Original137 JOIN: Wait1/frame226→227 finishes leaderMOVE at(0,-2879), SelphieCollisionEnabled remains true; run7929/flat native peers checked every step; completed frame{first.Frame}, next {first.NextTrigger.Kind}, fixed follower({first.Actors[5].X},{first.Actors[5].Y}) triangle{first.Actors[5].WalkmeshTriangle}; exact mid-JOIN restore and enabled-peer refusal pass.");
    }
}
