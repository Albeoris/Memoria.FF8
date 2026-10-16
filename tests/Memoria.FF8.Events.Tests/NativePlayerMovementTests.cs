using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void GardenNativePlayerMovementBoundary(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(172);
        EventEngine Create()
        {
            GameSession session = new(GameSession.Load(TestResources.FixturePath("Events.Tests.NativePlayerMovementTests.1")));
            EventEngine engine = new(session)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                LoadSoundResource = _ => SoundResourceStatus.Muted,
                ResolveGlobalSoundResource = MutedGlobalSample,
                CanUseNativePlayerMovement = actor => actor.ObjectId == 0 && actor.CharacterId == 0,
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z),
                ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, triangle, x, y)
            };
            engine.ResolveWalkmesh = (x, y) => field.Walkmesh.TryPosition(x, y, out Vector3 position, out int triangle, engine.Scene.LockedTriangles) ? new WalkmeshPosition(position, triangle) : null;
            engine.ResolveNativePlayerMovement = query =>
            {
                FieldNativeMovementRequest request = new(query.Triangle, query.FixedX, query.FixedY, query.FixedZ, (byte)query.Direction, query.Speed, (ushort)query.Radius, true, false, 0, true);
                FieldNativeMovementResult native = FieldNativeMovementProbe.Resolve(field.Walkmesh, request, engine.Scene.LockedTriangles);
                return native.Status != FieldNativeMovementStatus.Accepted || native.RetryCount != 1 || native.Proposals.Count != 1 || native.Direction != query.Direction ? null : new NativePlayerMovementResult(native.FixedX, native.FixedY, native.FixedZ, native.Triangle, native.Direction, native.Proposals.Select(proposal => new NativeJoinMovementProposal(proposal.FixedX, proposal.FixedY, proposal.Direction)).ToArray());
            };
            engine.LoadField(172, field.Jsm, field.Messages, session.State.FieldArrival);
            engine.Restore(session.State.EventState!.Value);
            return engine;
        }
        EventEngine reverse = Create();
        reverse.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 0, Vertical: 1));
        Require(reverse.Scene.NativePlayerPosition == new FieldFixedPosition(-57344, -12570736, 1344 * 4096) && reverse.Actors[0].Direction == 128, "Original +Y probe changed the saved zero-axis basis or its direction128 native result.");
        PlayerAction north = new(PlayerActionKind.Move, Horizontal: 0, Vertical: -1);
        EventEngine actual = Create();
        Require(actual.Frame == 57 && actual.Actors[0].WalkmeshTriangle == 93 && actual.Scene.NativePlayerMovementVersion == 0, "Actual ordinary movement source changed its historical script-MOVE triangle or driver version.");
        actual.Apply(north);
        Require(actual.Frame == 58 && actual.Scene.NativePlayerPosition == new FieldFixedPosition(-57344, -12824464, 1344 * 4096) && actual.Actors[0].WalkmeshTriangle == 58 && actual.Actors[0].Direction == 0 && actual.Scene.NativePlayerMovementVersion == 1, $"First accepted native player movement differs from original direction0/run7929/center58 golden: frame{actual.Frame}, raw{actual.Scene.NativePlayerPosition}, triangle{actual.Actors[0].WalkmeshTriangle}, direction{actual.Actors[0].Direction}, version{actual.Scene.NativePlayerMovementVersion}.");
        JsonElement moved = actual.Capture();
        EventEngine independent = Create();
        independent.Restore(moved);
        for (int tick = 0; tick < 5; tick++)
        {
            actual.Apply(north);
            independent.Apply(north);
            Require(JsonElement.DeepEquals(actual.Capture(), independent.Capture()), "Independent restored native player movement changed fixed coordinates, trace cursor, triggers or VM state.");
        }
        foreach (bool missing in new[] { false, true })
        {
            EventEngine refused = Create();
            if (missing)
            {
                refused.Restore(moved);
                refused.ResolveNativePlayerMovement = null;
            }
            else
            {
                refused.ResolveNativePlayerMovement = _ => null;
            }
            JsonElement before = refused.Capture();
            try
            {
                refused.Apply(north);
                throw new InvalidOperationException("Unsupported native ordinary movement fell back or partially mutated the VM.");
            }
            catch (NotSupportedException)
            {
            }
            Require(JsonElement.DeepEquals(before, refused.Capture()) && refused.ActionLog.Count == 0, "Unsupported native first-attempt movement changed VM, position, trace, version or action history.");
        }
        EventEngine retry = Create();
        Func<NativePlayerMovementQuery, NativePlayerMovementResult?> resolve = retry.ResolveNativePlayerMovement!;
        retry.ResolveNativePlayerMovement = query =>
        {
            NativePlayerMovementResult movement = resolve(query)!;
            return movement with { Proposals = movement.Proposals.Concat(movement.Proposals).ToArray() };
        };
        JsonElement beforeRetry = retry.Capture();
        try
        {
            retry.Apply(north);
            throw new InvalidOperationException("Multiple native player geometry attempts silently passed the bounded contract.");
        }
        catch (NotSupportedException)
        {
        }
        Require(JsonElement.DeepEquals(beforeRetry, retry.Capture()), "Rejected geometry retry changed state before original trigger preflight.");
        EventEngine peer = Create();
        peer.ResolveNativePlayerMovement = query =>
        {
            int x = (int)(peer.Actors[6].X * 4096) - query.Radius * FieldNativeWalkmeshProbe.Sine(query.Direction + 32);
            int y = (int)(peer.Actors[6].Y * 4096) + query.Radius * FieldNativeWalkmeshProbe.Sine(query.Direction + 96);
            return new NativePlayerMovementResult(x, y, query.FixedZ, query.Triangle, query.Direction, [new NativeJoinMovementProposal(x, y, query.Direction)]);
        };
        JsonElement beforePeer = peer.Capture();
        try
        {
            peer.Apply(north);
            throw new InvalidOperationException("Native ordinary movement ignored an enabled actor envelope.");
        }
        catch (NotSupportedException)
        {
        }
        Require(JsonElement.DeepEquals(beforePeer, peer.Capture()), "Rejected enabled peer envelope partially committed native movement.");
        Console.WriteLine("Original172 ordinary Move(0,-1,1): frame57→58 raw(-57344,-12824464,5505024), direction0/run7929, historical triangle93→native58; +1 maps saved-axis0 to direction128; exact independent trace/VM restore, blocked/retry/missing-adapter/peer refusals preserve preaction state.");
    }
}
