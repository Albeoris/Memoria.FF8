using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSoloSlopedSplitContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(172);
        EventEngine Create(bool certified = true)
        {
            GameSession session = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
            EventEngine result = new(session, session.State.Variables)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                CanUseNativeJoinMovement = actor => certified && actor.ObjectId == 0 && actor.ModelId == 0 && actor.CharacterId == 0,
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z),
                ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, triangle, x, y)
            };
            result.ResolveNativeJoinMovement = query =>
            {
                FieldNativeMovementRequest request = new(query.Triangle, query.FixedX, query.FixedY, query.FixedZ, (byte)query.Direction, query.Speed, (ushort)query.Radius, false, true, 0, true);
                FieldNativeMovementResult movement = FieldNativeMovementProbe.Resolve(field.Walkmesh, request, result.Scene.LockedTriangles);
                return movement.Status == FieldNativeMovementStatus.Unsupported ? null : new NativeJoinMovementResult(movement.Status == FieldNativeMovementStatus.Accepted, movement.FixedX, movement.FixedY, movement.FixedZ, movement.Triangle, movement.Direction, movement.Proposals.Select(proposal => new NativeJoinMovementProposal(proposal.FixedX, proposal.FixedY, proposal.Direction)).ToArray());
            };
            result.LoadField(172, field.Jsm, field.Messages, session.State.FieldArrival);
            result.Restore(session.State.EventState!.Value);
            return result;
        }
        EventEngine first = Create();
        first.Step();
        JsonElement entered = first.Capture();
        JsonElement split = entered.GetProperty("PartySplit");
        Require(first.Frame == 60 && split.GetProperty("UsesNativeMovement").GetBoolean() && split.GetProperty("Targets").GetArrayLength() == 1 && split.GetProperty("Targets")[0].GetProperty("TargetZ").GetInt32() == 1347 * 4096 && first.Actors[0].CollisionEnabled, "Original solo SPLIT did not preserve its unequal target height, enabled self collision or certified movement provenance.");
        EventEngine resumed = Create();
        resumed.Restore(entered);
        bool heightChanged = false;
        int ticks = 0;
        while (first.Capture().GetProperty("PartySplit").ValueKind != JsonValueKind.Null && ticks++ < 100)
        {
            first.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "Solo native SPLIT raw height, animation or issuing-PC wait diverged after independent restore.");
            heightChanged |= first.Actors[0].Z != 1344;
        }
        Require(ticks < 100 && heightChanged && first.LastFault is null && first.Actors[0].CollisionEnabled && first.Scene.Party.SequenceEqual([0]), "Original solo SPLIT did not complete its native sloped geometry with unchanged party/collision state.");

        EventEngine missing = Create();
        missing.Restore(entered);
        missing.ResolveNativeJoinMovement = _ => null;
        JsonElement before = missing.Capture();
        try
        {
            missing.Step();
            throw new InvalidOperationException("Saved native SPLIT silently fell back to flat geometry.");
        }
        catch (EventScriptException) when (missing.LastFault?.Opcode == "SPLIT")
        {
        }
        Require(missing.Actors[0].X == before.GetProperty("Actors")[0].GetProperty("X").GetSingle() && missing.Actors[0].Y == before.GetProperty("Actors")[0].GetProperty("Y").GetSingle() && missing.Actors[0].Z == before.GetProperty("Actors")[0].GetProperty("Z").GetSingle() && JsonElement.DeepEquals(before.GetProperty("PartySplit").GetProperty("Targets"), missing.Capture().GetProperty("PartySplit").GetProperty("Targets")), "Refused native SPLIT committed actor XYZ or raw targets.");

        JsonNode malformed = JsonNode.Parse(entered.GetRawText())!;
        malformed["PartySplit"]!["Targets"]![0]!["FixedZ"] = split.GetProperty("Targets")[0].GetProperty("FixedZ").GetInt32() + 1;
        try
        {
            Create().Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Saved native SPLIT accepted a fractional native plane height.");
        }
        catch (InvalidDataException)
        {
        }
        Console.WriteLine($"Original172 solo SPLIT: frame59→60 certified sloped movement, one target/FF exclusion, midmovement full VM restore, completed frame{first.Frame}; missing callback preserves XYZ.");
    }
}
