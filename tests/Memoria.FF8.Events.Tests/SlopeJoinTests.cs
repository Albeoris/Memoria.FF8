using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void GardenSlopeJoinBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(172);
        EventEngine Create(bool certified = true)
        {
            GameSession session = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
            EventEngine result = new(session, session.State.Variables)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                LoadSoundResource = _ => SoundResourceStatus.Muted,
                ResolveGlobalSoundResource = MutedGlobalSample,
                ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, triangle, x, y),
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z),
                CanUseNativeJoinMovement = actor => certified && actor.ObjectId == 6 && actor.CharacterId == 5,
                CanSetBackgroundScrollPosition = (layer, x, y) => layer == 0 && x == 0 && y is 0 or 224,
                CanSetCosineBackgroundScroll = (layer, x, y, duration) => layer == 0 && x == 0 && y == 0 && duration == 40
            };
            result.ResolveWalkmesh = (x, y) => field.Walkmesh.TryPosition(x, y, out Vector3 position, out int triangle, result.Scene.LockedTriangles) ? new WalkmeshPosition(position, triangle) : null;
            result.ResolvePartyJoinStep = query =>
            {
                FieldNativeWalkmeshPosition? point = FieldNativeWalkmeshProbe.ResolveFlatStep(field.Walkmesh, query.Triangle, query.FixedX, query.FixedY, query.Radius, query.Direction, result.Scene.LockedTriangles);
                return point is FieldNativeWalkmeshPosition resolved && resolved.NativeHeight * 4096 == query.FixedZ ? new WalkmeshPosition(new Vector3(resolved.FixedX / 4096f, resolved.FixedY / 4096f, resolved.NativeHeight), resolved.Triangle) : null;
            };
            result.ResolveNativeJoinMovement = query =>
            {
                FieldNativeMovementRequest request = new(query.Triangle, query.FixedX, query.FixedY, query.FixedZ, (byte)query.Direction, query.Speed, (ushort)query.Radius, false, true, 0, true);
                FieldNativeMovementResult native = FieldNativeMovementProbe.Resolve(field.Walkmesh, request, result.Scene.LockedTriangles);
                return native.Status == FieldNativeMovementStatus.Unsupported ? null : new NativeJoinMovementResult(native.Status == FieldNativeMovementStatus.Accepted, native.FixedX, native.FixedY, native.FixedZ, native.Triangle, native.Direction, native.Proposals.Select(proposal => new NativeJoinMovementProposal(proposal.FixedX, proposal.FixedY, proposal.Direction)).ToArray());
            };
            result.LoadField(172, field.Jsm, field.Messages, session.State.FieldArrival);
            result.Restore(session.State.EventState!.Value);
            return result;
        }
        EventEngine first = Create();
        first.Step();
        JsonElement entered = first.Capture();
        Require(first.Frame == 32 && entered.GetProperty("PartyJoin").GetProperty("UsesNativeMovement").GetBoolean() && first.Actors[0].Z == 1344 && first.Actors[6].Z == 1347 && entered.GetProperty("PartyJoin").GetProperty("Targets")[0].GetProperty("Speed").GetInt32() == 7929, "Original slope JOIN entry did not preserve the script MOVE endpoint, distinct follower height or native run speed.");
        EventEngine resumed = Create();
        resumed.Restore(entered);
        HashSet<int> visited = [];
        HashSet<float> heights = [];
        for (int tick = 0; tick < 100 && first.Capture().GetProperty("PartyJoin").ValueKind != JsonValueKind.Null; tick++)
        {
            first.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "Native slope JOIN raw positions, retries, animation or scroll phase diverged after independent restore.");
            visited.Add(first.Actors[6].WalkmeshTriangle);
            heights.Add(first.Actors[6].Z);
            if (tick == 0)
            {
                JsonElement target = first.Capture().GetProperty("PartyJoin").GetProperty("Targets")[0];
                Require(target.GetProperty("FixedX").GetInt32() == -501578 && target.GetProperty("FixedY").GetInt32() == -9583134 && first.Actors[6].WalkmeshTriangle == 89 && first.Actors[6].Direction == 6, "First native slope JOIN step differs from independent original direction6/run7929/92→89 proof.");
            }
        }
        Require(first.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null && first.LastFault is null && heights.Contains(1344) && heights.Contains(1347) && visited.Contains(87) && first.Scene.PartyTrace is not null && first.Scene.BackgroundScrollPositions[0].Status == 2, "Original JOIN failed to traverse the selected ramp, preserve native height or complete trace/scroll state.");
        Require(first.Scene.Party.SequenceEqual([0, 5]) && first.Scene.FieldPartyNativeActorIds.SequenceEqual([0, 1, 255]), "Slope JOIN changed original party or compact bindings.");
        Require(first.Frame == 54 && first.Actors[6].X == -141686 / 4096f && first.Actors[6].Y == -12094173 / 4096f && first.Actors[6].Z == 1344 && first.Actors[6].WalkmeshTriangle == 61 && first.Actors[0].WalkmeshTriangle == 93, "Native slope JOIN completion differs from independent21-step final raw coordinates or changed the original stale leader triangle.");

        EventEngine refused = Create(false);
        try
        {
            refused.Step();
            throw new InvalidOperationException("Unequal-height JOIN ignored its initial native movement certificate.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "JOIN" && refused.LastFault.Operation == 62)
        {
        }
        Require(refused.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null && refused.Actors[6].X == -127 && refused.Actors[6].Y == -2309 && refused.Actors[6].Z == 1347, "Uncertified slope JOIN changed companion raw coordinates or started its movement state.");
        EventEngine missing = Create();
        missing.Restore(entered);
        missing.ResolveNativeJoinMovement = _ => null;
        try
        {
            missing.Step();
            throw new InvalidOperationException("Saved native JOIN silently fell back to the historical flat callback.");
        }
        catch (EventScriptException) when (missing.LastFault?.Opcode == "JOIN")
        {
        }
        Require(missing.Actors[6].X == -127 && missing.Actors[6].Y == -2309 && missing.Actors[6].Z == 1347 && missing.Capture().GetProperty("PartyJoin").GetProperty("Targets").GetRawText() == entered.GetProperty("PartyJoin").GetProperty("Targets").GetRawText(), "Unsupported native JOIN callback changed saved raw coordinates or targets.");
        EventEngine peer = Create();
        peer.Restore(entered);
        Func<NativeJoinMovementQuery, NativeJoinMovementResult?> resolve = peer.ResolveNativeJoinMovement!;
        peer.ResolveNativeJoinMovement = query =>
        {
            NativeJoinMovementResult movement = resolve(query)!;
            // A rejected early retry must not be hidden by a clear final proposal.
            int x = (int)(peer.Actors[0].X * 4096) - query.Radius * FieldNativeWalkmeshProbe.Sine(32);
            int y = (int)(peer.Actors[0].Y * 4096) + query.Radius * FieldNativeWalkmeshProbe.Sine(96);
            return movement with { Proposals = new[] { new NativeJoinMovementProposal(x, y, 0) }.Concat(movement.Proposals).ToArray() };
        };
        try
        {
            peer.Step();
            throw new InvalidOperationException("Native JOIN ignored a conflicting actor envelope on an earlier retry.");
        }
        catch (EventScriptException) when (peer.LastFault?.Opcode == "JOIN")
        {
        }
        Require(peer.Actors[6].X == -127 && peer.Actors[6].Y == -2309 && peer.Actors[6].Z == 1347 && peer.Capture().GetProperty("PartyJoin").GetProperty("Targets").GetRawText() == entered.GetProperty("PartyJoin").GetProperty("Targets").GetRawText(), "Earlier native retry contact partially committed final clear movement.");
        Console.WriteLine($"Original172 slope JOIN: Wait1/frame31→32 leaderZ1344/followerZ1347; original92 adjacency ramp87 reaches1344, distinct native run7929; completed frame{first.Frame}, exact midmovement restore, trace/scroll retained.");
    }
}
