using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using System.Numerics;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void GardenAutomaticFieldPartyLoad(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(137);
        string path = Path.Combine("../fixtures/external-fixture.json");
        EventEngine Create(GameSession session)
        {
            EventEngine result = new(session, session.State.Variables)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                LoadSoundResource = _ => SoundResourceStatus.Muted,
                CanDisableAdditionalControl = _ => true,
                ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, triangle, x, y),
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z),
                ResolveWalkmesh = (x, y) => field.Walkmesh.TryPosition(x, y, out Vector3 position, out int triangle) ? new WalkmeshPosition(position, triangle) : null
            };
            result.LoadField(137, field.Jsm, field.Messages, session.State.FieldArrival);
            result.Restore(session.State.EventState!.Value);
            return result;
        }
        EventEngine first = Create(new GameSession(GameSession.Load(path)));
        EventEngine resumed = Create(new GameSession(GameSession.Load(path)));
        Require(first.Frame == 0 && first.Scene.Party.SequenceEqual(new[] { 0, 5 }) && first.Scene.NativeFieldLoadVersion == 0, "Actual original137 entry source changed.");
        for (int tick = 0; tick < 12 && first.Scene.NativeFieldLoadVersion == 0; tick++)
        {
            first.Step();
            if (tick == 0)
            {
                resumed.Restore(first.Capture());
            }
            else
            {
                resumed.Step();
            }
            Require(first.Capture().GetRawText() == resumed.Capture().GetRawText(), "Field137 native init/entry policy failed exact restored phase.");
        }
        Require(first.Scene.NativeFieldLoadVersion == 1 && first.Scene.NativePartyTraceVersion == 1, "Actual field137 failed to establish automatic native load trace.");
        Console.WriteLine($"Automatic137 load frame{first.Frame}: retained[{string.Join(',', first.Scene.NativeActorObjectIds)}], bindings[{string.Join(',', first.Scene.FieldPartyNativeActorIds)}]; player({first.Actors[0].X},{first.Actors[0].Y},{first.Actors[0].Z}) triangle{first.Actors[0].WalkmeshTriangle}; follower5({first.Actors[5].X},{first.Actors[5].Y},{first.Actors[5].Z}) triangle{first.Actors[5].WalkmeshTriangle}.");
        PartyTraceState trace = first.Scene.PartyTrace!;
        Require(trace.FirstTrail[0] is null && trace.SecondTrail[0] is null && trace.FirstTrail[15] is null && trace.FirstTrail.Skip(49).All(sample => sample is not null && sample.Animation == 0) && trace.FirstTrail.SequenceEqual(trace.SecondTrail), "Automatic normalentry trace invented unobservable metadata or used JOIN idle trace policy.");
        int followerNative = first.Scene.FieldPartyNativeActorIds[1];
        ActorState follower = first.Actors[first.Scene.NativeActorObjectIds[followerNative]];
        PartyTraceSample originalFollower = trace.FirstTrail[49]!;
        Require(follower.X == originalFollower.X && follower.Y == originalFollower.Y && follower.Z == originalFollower.Z && follower.WalkmeshTriangle == originalFollower.Triangle, "Native loader placed a present follower at the leader instead of trace slot49.");
        Require(first.Scene.FieldPartyNativeActorIds.Select((id, slot) => first.Capture().GetProperty("Variables").GetProperty((194 + slot).ToString()).GetInt32() == id).All(value => value), "Automatic loader failed to persist original save194..196 compact bindings.");
        for (int tick = 0; tick < 12 && first.LastFault is null; tick++)
        {
            foreach (EventEngine bounded in new[] { first, resumed })
            {
                try
                {
                    bounded.Step();
                }
                catch (EventScriptException) when (bounded.LastFault?.Opcode == "EFFECTPLAY")
                {
                }
            }
            Require(first.Capture().GetRawText() == resumed.Capture().GetRawText(), "Restored automatic137 trace changed original default script execution.");
        }
        Require(first.LastFault?.Operation == 5510 && first.LastFault.ObjectId == 20, "Automatic137 replay changed the next original safe EFFECTPLAY boundary.");
        var frozen = first.Capture();
        first.Restore(frozen);
        Require(frozen.GetRawText() == first.Capture().GetRawText(), "Native137 completed loader failed exact fault-boundary restore.");
        Console.WriteLine($"Automatic137 next original trigger {first.NextTrigger.Kind}/frame{first.Frame}: EFFECTPLAY188 module20/script187/operation5510 safely unsupported.");

        // Refusing a record that is observable before the first movement must
        // stop the loader before compaction, bindings or arrival commits.
        EventEngine unknown = Create(new GameSession(GameSession.Load(path)));
        unknown.ResolvePartyTraceTriangle = (_, _, _) => null;
        try
        {
            for (int tick = 0; tick < 12; tick++)
            {
                unknown.Step();
            }
            throw new InvalidOperationException("Native field entry invented observable trace geometry.");
        }
        catch (NotSupportedException exception) when (exception.Message.Contains("record63"))
        {
        }
        Require(unknown.Scene.NativeActorLayoutVersion == 0 && unknown.Scene.NativeFieldLoadVersion == 0 && unknown.Scene.FieldPartyNativeActorIds.Count == 0 && unknown.Scene.PartyTrace is null && unknown.Capture().GetProperty("PendingArrival").ValueKind != System.Text.Json.JsonValueKind.Null, "Failed automatic load committed a partial layout, party or trace.");
        unknown.Restore(unknown.Capture());

        // Replay the genuine source gateway action to retain known old record0
        // metadata through the natural new LoadField, instead of promoting the
        // historical137 snapshot's lost metadata to a guessed record.
        FieldAssets departing = assets.LoadField(139);
        GameSession seamSession = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
        EventEngine seam = new(seamSession, seamSession.State.Variables)
        {
            AnimationFrameCount = departing.GetAnimationFrameCount,
            ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(departing.Walkmesh, triangle, x, y),
            ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(departing.Walkmesh, x, y, z),
            ResolveWalkmesh = (x, y) => departing.Walkmesh.TryPosition(x, y, out Vector3 position, out int triangle) ? new WalkmeshPosition(position, triangle) : null
        };
        seam.LoadField(139, departing.Jsm, departing.Messages);
        seam.Restore(seamSession.State.EventState!.Value);
        seam.ResolvePartyJoinStep = query =>
        {
            FieldNativeWalkmeshPosition? point = FieldNativeWalkmeshProbe.ResolveFlatStep(departing.Walkmesh, query.Triangle, query.FixedX, query.FixedY, query.Radius, query.Direction, seam.Scene.LockedTriangles);
            return point is FieldNativeWalkmeshPosition native ? new WalkmeshPosition(new Vector3(native.FixedX / 4096f, native.FixedY / 4096f, native.NativeHeight), native.Triangle) : null;
        };
        seam.InfGateways = departing.Information!.Gateways.Where(gateway => gateway.Defined).Select(gateway => new InfGateway(gateway.Start, gateway.End, new FieldTransition(gateway.DestinationField, gateway.DestinationX, gateway.DestinationY, gateway.DestinationTriangle, gateway.Direction))).ToArray();
        seam.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: 1));
        Require(seamSession.State.FieldId == 137 && seamSession.State.FieldArrival == new FieldTransition(137, -14, -5203, 72, 140), "Genuine source139 gateway action did not request the original137 packet.");
        PartyTraceSample previousRecord = seam.Scene.PartyTrace!.FirstTrail[0]!;
        seam.ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, triangle, x, y);
        seam.ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z);
        seam.AnimationFrameCount = field.GetAnimationFrameCount;
        seam.LoadField(137, field.Jsm, field.Messages, seamSession.State.FieldArrival);
        seam.Step();
        GameSession midInitSession = new(seamSession.State with { EventState = seam.Capture(), Variables = new Dictionary<int, int>(seamSession.State.Variables) });
        EventEngine midInit = Create(midInitSession);
        for (int tick = 0; tick < 12 && seam.Scene.NativeFieldLoadVersion == 0; tick++)
        {
            seam.Step();
            midInit.Step();
            // Live Dictionary.Remove retains internal free slots across the
            // field boundary; Restore reinserts densely. Property order is not
            // part of native variable state, but every value and VM field is.
            Require(System.Text.Json.JsonElement.DeepEquals(seam.Capture(), midInit.Capture()), "Independent mid-init restore changed native field placement or any VM value.");
        }
        Require(seam.Scene.PartyTrace!.FirstTrail[0] == previousRecord with { X = -14, Y = -5203, FirstFlag = 1, SecondFlag = 1 } && seam.Scene.PartyTrace.FirstTrail.Skip(1).SequenceEqual(trace.FirstTrail.Skip(1)), "Natural field seam failed to preserve known record0 metadata or changed generated137 records.");
    }
}
