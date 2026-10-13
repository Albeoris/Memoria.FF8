using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePlayerAcceptedRetryContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(237);
        PlayerAction action = new(PlayerActionKind.Move, Horizontal: 0.99919f, Vertical: 0.04016f);
        EventEngine Create(Action<JsonNode>? alter = null)
        {
            GameSession session = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = field.GetAnimationFrameCount,
                CanUseNativePlayerMovement = actor => actor.ObjectId == 0,
                ResolvePartyTraceTriangle = (x, y, z) => FieldPartyTraceGeometry.Resolve(field.Walkmesh, x, y, z),
                ResolveNativeTriangleHeight = (triangle, x, y) => FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, triangle, x, y)
            };
            engine.ResolveWalkmesh = (x, y) => field.Walkmesh.TryPosition(x, y, out Vector3 position, out int triangle, engine.Scene.LockedTriangles) ? new WalkmeshPosition(position, triangle) : null;
            engine.ResolveNativePlayerMovement = query =>
            {
                FieldNativeMovementRequest request = new(query.Triangle, query.FixedX, query.FixedY, query.FixedZ, (byte)query.Direction, query.Speed, (ushort)query.Radius, true, false, 0, true);
                FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(field.Walkmesh, request, engine.Scene.LockedTriangles);
                FieldNativeMovementResult alternate = FieldNativeMovementProbe.Resolve(field.Walkmesh, request with { EarlyRetryState = 1 }, engine.Scene.LockedTriangles);
                Require(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(result), JsonSerializer.SerializeToElement(alternate)), "Selected accepted retry depends on the unknown early-retry byte.");
                if (result.Status != FieldNativeMovementStatus.Accepted || result.Proposals.Count is < 1 or > 2)
                {
                    return null;
                }
                NativeJoinMovementProposal[] proposals = result.Proposals.Select(proposal => new NativeJoinMovementProposal(proposal.FixedX, proposal.FixedY, proposal.Direction)).ToArray();
                IReadOnlyList<NativePlayerGroundProbe>[] probes = result.Proposals.Select(proposal => (IReadOnlyList<NativePlayerGroundProbe>)proposal.FanProbes.Select(probe => new NativePlayerGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight!.Value)).ToArray()).ToArray();
                return new NativePlayerMovementResult(result.FixedX, result.FixedY, result.FixedZ, result.Triangle, result.Direction, proposals, ProposalGroundProbes: probes);
            };
            engine.LoadField(237, field.Jsm, field.Messages, session.State.FieldArrival);
            JsonNode saved = JsonNode.Parse(session.State.EventState!.Value.GetRawText())!;
            alter?.Invoke(saved);
            engine.Restore(JsonSerializer.SerializeToElement(saved));
            return engine;
        }
        void Refuses(EventEngine engine)
        {
            JsonElement before = engine.Capture();
            try
            {
                engine.Apply(action);
                throw new InvalidOperationException("Unsupported retry transaction was accepted.");
            }
            catch (NotSupportedException)
            {
            }
            Require(JsonElement.DeepEquals(before, engine.Capture()), "Rejected retry changed position, direction, PUSH, trace, frame or VM.");
        }
        EventEngine accepted = Create();
        accepted.Apply(action);
        Require(accepted.Scene.NativePlayerPosition == new FieldFixedPosition(-627337, -723012, 0) && accepted.Actors[0].WalkmeshTriangle == 87 && accepted.Actors[0].Direction == 242, "Original237 second-attempt fixed result or final direction changed.");
        EventEngine restored = Create();
        restored.Restore(accepted.Capture());
        for (int tick = 0; tick < 4; tick++)
        {
            accepted.Apply(new PlayerAction(PlayerActionKind.Wait));
            restored.Apply(new PlayerAction(PlayerActionKind.Wait));
            Require(JsonElement.DeepEquals(accepted.Capture(), restored.Capture()), "Accepted retry full VM diverged after independent restore and ordinary waits.");
        }
        foreach (int malformed in new[] { 0, 1, 2, 3, 4, 5 })
        {
            EventEngine invalid = Create();
            Func<NativePlayerMovementQuery, NativePlayerMovementResult?> real = invalid.ResolveNativePlayerMovement!;
            invalid.ResolveNativePlayerMovement = query =>
            {
                NativePlayerMovementResult result = real(query)!;
                IReadOnlyList<NativePlayerGroundProbe>[] groups = result.ProposalGroundProbes!.ToArray();
                if (malformed == 0)
                {
                    return result with { ProposalGroundProbes = groups.Take(1).ToArray() };
                }
                if (malformed == 1)
                {
                    groups[0] = groups[0].Reverse().ToArray();
                }
                if (malformed == 2)
                {
                    NativePlayerGroundProbe[] probes = groups[0].ToArray();
                    probes[0] = probes[0] with { NativeHeight = short.MaxValue + 1 };
                    groups[0] = probes;
                }
                if (malformed == 3)
                {
                    return result with { GroundProbes = groups[0] };
                }
                if (malformed == 4)
                {
                    return result with { Proposals = result.Proposals.Concat(new[] { result.Proposals[^1] }).ToArray() };
                }
                if (malformed == 5)
                {
                    return result with { ProposalGroundProbes = null };
                }
                return result with { ProposalGroundProbes = groups };
            };
            Refuses(invalid);
        }
        EventEngine collision = Create(saved =>
        {
            JsonNode peer = saved["Actors"]![15]!;
            peer["X"] = -143;
            peer["Y"] = -215;
            peer["Z"] = 0;
            peer["CollisionEnabled"] = true;
            peer["PushEnabled"] = false;
            peer["NativeScriptMovement"] = null;
        });
        Func<NativePlayerMovementQuery, NativePlayerMovementResult?> collisionResolver = collision.ResolveNativePlayerMovement!;
        collision.ResolveNativePlayerMovement = query =>
        {
            NativePlayerMovementResult result = collisionResolver(query)!;
            IReadOnlyList<NativePlayerGroundProbe>[] groups = result.ProposalGroundProbes!.ToArray();
            groups[1] = groups[1].Select(probe => probe with { NativeHeight = 1000 }).ToArray();
            return result with { ProposalGroundProbes = groups };
        };
        Refuses(collision);
        Console.WriteLine("Native player retry: original237 accepted second attempt, early0/1 invariance, final rawXYZ/direction242 and independent restore; every ordered fan checked before mutation, including earlier-attempt collision and malformed/missing/ambiguous probe rejection.");
    }
}
