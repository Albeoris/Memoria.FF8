using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePlayerGroundContactContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(172);
        EventEngine Create(Action<JsonNode>? alterPeer = null)
        {
            GameSession session = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                CanUseNativePlayerMovement = actor => actor.ObjectId == 0 && actor.CharacterId == 0,
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z),
                ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, triangle, x, y)
            };
            engine.ResolveWalkmesh = (x, y) => field.Walkmesh.TryPosition(x, y, out Vector3 position, out int triangle, engine.Scene.LockedTriangles) ? new WalkmeshPosition(position, triangle) : null;
            engine.ResolveNativePlayerMovement = query =>
            {
                FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(field.Walkmesh, new FieldNativeMovementRequest(query.Triangle, query.FixedX, query.FixedY, query.FixedZ, (byte)query.Direction, query.Speed, (ushort)query.Radius, true, false, 0, true), engine.Scene.LockedTriangles);
                if (result.Status != FieldNativeMovementStatus.Accepted || result.RetryCount != 1 || result.Proposals.Count != 1 || result.Direction != query.Direction || result.Proposals[0].FanProbes.Any(probe => probe.NativeHeight is null))
                {
                    return null;
                }
                FieldNativeMovementProposal proposal = result.Proposals[0];
                return new NativePlayerMovementResult(result.FixedX, result.FixedY, result.FixedZ, result.Triangle, result.Direction, new[] { new NativeJoinMovementProposal(proposal.FixedX, proposal.FixedY, proposal.Direction) }, proposal.FanProbes.Select(probe => new NativePlayerGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value)).ToArray());
            };
            engine.LoadField(172, field.Jsm, field.Messages, session.State.FieldArrival);
            JsonNode saved = JsonNode.Parse(session.State.EventState!.Value.GetRawText())!;
            alterPeer?.Invoke(saved["Actors"]![6]!);
            engine.Restore(JsonSerializer.SerializeToElement(saved));
            return engine;
        }
        void StageNearPeer(JsonNode peer, bool collision, bool push, float x = 20, int height = 1344)
        {
            // Explicit synthetic peer placement near the first native ground
            // fan. The source checkpoint remains unmodified on disk.
            peer["X"] = x;
            peer["Y"] = -3165;
            peer["Z"] = height;
            peer["Visible"] = false;
            peer["Active"] = false;
            peer["CollisionEnabled"] = collision;
            peer["PushEnabled"] = push;
        }
        PlayerAction north = new(PlayerActionKind.Move, Vertical: -1);
        void RefusesWithoutMutation(EventEngine engine, string reason)
        {
            JsonElement before = engine.Capture();
            try
            {
                engine.Apply(north);
                throw new InvalidOperationException(reason);
            }
            catch (NotSupportedException)
            {
            }
            Require(JsonElement.DeepEquals(before, engine.Capture()), "Unsupported native fan contact mutated rawXYZ, animation, pending PUSH, trace, frame or VM.");
        }

        EventEngine ordinary = Create();
        ordinary.Apply(north);
        EventEngine independent = Create();
        independent.Restore(ordinary.Capture());
        ordinary.Apply(north);
        independent.Apply(north);
        Require(JsonElement.DeepEquals(ordinary.Capture(), independent.Capture()), "Native-height player movement and full VM restore diverged.");

        RefusesWithoutMutation(Create(peer => StageNearPeer(peer, true, false)), "Hidden inactive collision peer was omitted from native fan contact.");
        RefusesWithoutMutation(Create(peer => StageNearPeer(peer, false, true, 20.25f)), "Ambiguous near managed fractional PUSH peer was promoted to native raw bits.");

        EventEngine push = Create(peer => StageNearPeer(peer, false, true));
        push.Apply(north);
        Require(push.Actors[6].NativePushContactPending, "THROUGHON+PUSHON contact did not stage native byte584 after the movement update.");
        EventEngine pushedRestore = Create();
        pushedRestore.Restore(push.Capture());
        push.Apply(new PlayerAction(PlayerActionKind.Wait));
        pushedRestore.Apply(new PlayerAction(PlayerActionKind.Wait));
        Require(!push.Actors[6].NativePushContactPending && JsonElement.DeepEquals(push.Capture(), pushedRestore.Capture()), "Native push flag/local3 priority6 dispatch changed across independent restore.");

        foreach (int difference in new[] { -127, 128 })
        {
            EventEngine outsideHeight = Create(peer => StageNearPeer(peer, true, true, height: 1344 + difference));
            outsideHeight.Apply(north);
            Require(!outsideHeight.Actors[6].NativePushContactPending, "Native strict vertical contact bounds included their -127/128 endpoints.");
        }
        EventEngine ignored = Create(peer => StageNearPeer(peer, false, false));
        ignored.Apply(north);
        Require(!ignored.Actors[6].NativePushContactPending, "THROUGHON+PUSHOFF peer produced a native contact.");

        EventEngine raisedPlane = Create(peer => StageNearPeer(peer, true, true));
        Func<NativePlayerMovementQuery, NativePlayerMovementResult?> real = raisedPlane.ResolveNativePlayerMovement!;
        raisedPlane.ResolveNativePlayerMovement = query =>
        {
            NativePlayerMovementResult result = real(query)!;
            return result with { GroundProbes = result.GroundProbes!.Select(probe => probe with { NativeHeight = 1000 }).ToArray() };
        };
        raisedPlane.Apply(north);
        Require(!raisedPlane.Actors[6].NativePushContactPending, "Player contact used source/centre height instead of each supplied native fan plane.");

        foreach (int malformed in new[] { 0, 1, 2, 3 })
        {
            EventEngine invalid = Create();
            Func<NativePlayerMovementQuery, NativePlayerMovementResult?> resolver = invalid.ResolveNativePlayerMovement!;
            invalid.ResolveNativePlayerMovement = query =>
            {
                NativePlayerMovementResult result = resolver(query)!;
                NativePlayerGroundProbe[] probes = result.GroundProbes!.ToArray();
                if (malformed == 0)
                {
                    return result with { GroundProbes = probes.Take(2).ToArray() };
                }
                if (malformed == 1)
                {
                    Array.Reverse(probes);
                }
                if (malformed == 2)
                {
                    probes[0] = probes[0] with { FixedX = probes[0].FixedX + 1 };
                }
                if (malformed == 3)
                {
                    probes[0] = probes[0] with { NativeHeight = short.MaxValue + 1 };
                }
                return result with { GroundProbes = probes };
            };
            RefusesWithoutMutation(invalid, "Malformed native fan count/order/coordinate/height was accepted.");
        }
        Console.WriteLine("Native player fan contacts: real ordered ground planes and independent restore; hidden/inactive collision refusal, THROUGHON+PUSHON deferred584/local3 restore, strict vertical endpoints, uncertain fractional-peer refusal and atomic malformed-probe guards.");
    }
}
