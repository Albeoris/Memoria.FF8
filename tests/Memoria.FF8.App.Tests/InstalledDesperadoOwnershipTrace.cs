using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class InstalledDesperadoOwnershipTrace
{
    private static readonly ConditionalWeakTable<GameWorld, Observation> observations = new();

    internal static void CaptureIfRequested(GameWorld world, string actorId, int recipient, BattleLimitFamily family)
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_DESPERADO_OWNERSHIP_TRACE");
        if (string.IsNullOrWhiteSpace(output) || recipient != 8 || family != BattleLimitFamily.Desperado || world.Battle?.Presentation is not { } process)
        {
            return;
        }
        Observation observation = observations.GetValue(world, _ => new());
        if (observation.Finished || observation.LastCallback == process.Graphics.Callback)
        {
            return;
        }
        observation.LastCallback = process.Graphics.Callback;
        BattleSession battle = world.Battle!;
        if (!process.Graphics.ActorModels.TryGet(battle.ReadPresentationModelOwner(actorId), out NativeBattleGraphicsModel? actor))
        {
            return;
        }
        if (process.Graphics.OriginalEffect?.Binding.Effect != 261 || process.Memory is not { Arena: { } arena, ScriptedEffects: { } core })
        {
            Finish("OriginalEffectLeft261");
            return;
        }
        var managerSnapshot = core.ReadSnapshot(0x27973EC, 4);
        if (!managerSnapshot.Known.All(known => known))
        {
            return;
        }
        uint manager = BinaryPrimitives.ReadUInt32LittleEndian(managerSnapshot.Bytes);
        if (manager == 0)
        {
            return;
        }
        var bankSnapshot = core.ReadSnapshot(manager + 0x90, 4);
        if (!bankSnapshot.Known.All(known => known))
        {
            return;
        }
        uint bank = BinaryPrimitives.ReadUInt32LittleEndian(bankSnapshot.Bytes);
        if (bank == 0)
        {
            return;
        }
        uint? rope = null;
        uint ropeNode = 0;
        int ropeSlot = -1;
        for (int order = 0; order < 256; order++)
        {
            var orderSnapshot = core.ReadSnapshot(0x2797554u + (uint)order, 1);
            if (!orderSnapshot.Known[0])
            {
                return;
            }
            byte slot = orderSnapshot.Bytes[0];
            if (slot == byte.MaxValue)
            {
                break;
            }
            uint node = bank + (uint)(slot * 0x100);
            if (arena.ReadByte(Offset(node + 0x1C)) != 3)
            {
                continue;
            }
            uint model = Dword(node + 0xBC);
            if ((arena.ReadUInt16(Offset(model + 8)) & 255) == 13)
            {
                Assert.Null(rope);
                rope = model;
                ropeNode = node;
                ropeSlot = slot;
            }
        }
        if (rope is not uint selected)
        {
            observation.LastAbsent = new { Callback = process.Graphics.Callback, battle.Frame, actor.ActiveAnimationDirectory, actor.ActiveStagedResourceRow100, actor.SequenceState?.Pose, actor.SequenceState?.PoseFrame114 };
            Finish("RopeDespawned");
            return;
        }
        uint table = Dword(selected + 0x14);
        uint rig = Dword(table);
        uint geometry = Dword(table + 4);
        byte ropePose = arena.ReadByte(Offset(selected + 0x20));
        byte ropeFrame = arena.ReadByte(Offset(selected + 0x26));
        byte ropeLimit = arena.ReadByte(Offset(selected + 0x27));
        observation.Birth ??= new
        {
            Callback = process.Graphics.Callback,
            battle.Frame,
            Bank = bank,
            Slot = ropeSlot,
            Node = ropeNode,
            Model = selected,
            ShapeArgument = arena.ReadUInt16(Offset(selected + 8)),
            File0Base = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x2798A68, 4)),
            File1Base = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x2798A6C, 4)),
            GeometryFile0Offset = (long)geometry - BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x2798A68, 4)),
            NodeActor = arena.TryReadExecutionReference<BattleExecutionReference.Model>(Offset(ropeNode + 0x1B), 1, out var nodeActor) ? nodeActor.Owner.ToString() : null,
            NodeOriginalCell = arena.TryReadExecutionReference<BattleExecutionReference.CinematicOriginalCell>(Offset(ropeNode + 0x1B), 1, out var originalCell) ? originalCell.Slot.ToString() : null,
            Key = NativeBattleCinematicNodeKeys.Read(arena, Offset(ropeNode + 0x14)).ToString(),
            ParentKey = NativeBattleCinematicNodeKeys.Read(arena, Offset(ropeNode + 0x16)).ToString(),
            Scripts = Enumerable.Range(0, 3).Select(stream => new { Stream = stream, Pc = Dword(ropeNode + (uint)(stream * 4)), File1Offset = (long)(Dword(ropeNode + (uint)(stream * 4)) & 0x7FFFFFFF) - BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x2798A6C, 4)) }).ToArray(),
            PreviousAbsent = observation.LastAbsent
        };
        if (observation.LastClock == (ropePose, ropeFrame, actor.SequenceState?.Pose, actor.SequenceState?.PoseFrame114))
        {
            return;
        }
        observation.LastClock = (ropePose, ropeFrame, actor.SequenceState?.Pose, actor.SequenceState?.PoseFrame114);
        if (observation.Rows.Count == 155)
        {
            observation.Truncated = true;
            return;
        }
        Assert.Equal(17, arena.ReadByte(Offset(rig)));
        observation.Ring ??= NativeBattleRawGeometryReader.Read((offset, length) => arena.Read(Offset(geometry + (uint)offset), length), 17).SelectMany(mesh => mesh.Vertices).Where(vertex => vertex.Bone == 1 && vertex.Position.Z == 0).DistinctBy(vertex => vertex.Position).ToImmutableArray();
        Assert.Equal(4, observation.Ring.Value.Length);
        NativeBattleActionMatrix endpointBone = NativeBattleArenaPose.ReadMatrix(arena, Offset(rig + 0x20 + 0x30));
        Vector3 endpoint = observation.Ring.Value.Aggregate(Vector3.Zero, (sum, vertex) => sum + NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(endpointBone, vertex.Position), process.Camera.Projection!)) / observation.Ring.Value.Length;
        var body = world.BattleResources.LoadPlayer(recipient, battle.GetCombatantDefinition(actorId).Costume);
        int hand = Memoria.FF8.Assets.BattleHumanoidRigProfiles.SemanticPairs(recipient, body, recipient, body).Single(pair => pair.Joint == "hand-a").Source;
        NativeBattlePilotModelPose? bodyPose = null;
        if (battle.PreparedPresentation is { } prepared)
        {
            foreach (ref readonly NativeBattleLogicalResolvedCommand command in prepared.Primitives.LastCommands)
            {
                if (command.Command.Pilot.Identity is { Bank: 0x502D40 } identity && identity.ModelOwner == battle.ReadPresentationModelOwner(actorId))
                {
                    bodyPose = command.Command.Pilot.ModelPose;
                    if (bodyPose is not null)
                    {
                        break;
                    }
                }
            }
        }
        Vector3? handCenter = null;
        if (bodyPose is not null)
        {
            ImmutableArray<Vector3> positions = bodyPose.Positions(0);
            Assert.Equal(body.Meshes[0].Vertices.Count, positions.Length);
            Vector3[] vertices = body.Meshes[0].Vertices.Select((vertex, index) => (vertex, index)).Where(item => item.vertex.Bone == hand).Select(item => positions[item.index]).ToArray();
            handCenter = vertices.Aggregate(Vector3.Zero, (sum, vertex) => sum + vertex) / vertices.Length;
        }
        observation.Rows.Add(new
        {
            Callback = process.Graphics.Callback,
            battle.Frame,
            PreparedCallback = battle.PreparedPresentation?.Callback,
            actor.ActiveAnimationDirectory,
            actor.ActiveStagedResourceRow100,
            actor.Sequence,
            BodyPose = actor.SequenceState?.Pose,
            BodyFrame = actor.SequenceState?.PoseFrame114,
            BodyLimit = actor.SequenceState?.PoseLimit115,
            RopePose = ropePose,
            RopeFrame = ropeFrame,
            RopeLimit = ropeLimit,
            ScriptStreams = Enumerable.Range(0, 3).Select(stream => Dword(ropeNode + (uint)(stream * 4))).ToArray(),
            Endpoint = Point(endpoint),
            HandCenter = handCenter is Vector3 handPoint ? Point(handPoint) : null,
            Relative = handCenter is Vector3 center ? Point(endpoint - center) : null
        });

        void Finish(string reason)
        {
            if (observation.Birth is null)
            {
                return;
            }
            observation.Finished = true;
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "ownership-08.json"), JsonSerializer.Serialize(new { observation.Birth, observation.Rows, observation.Truncated, End = new { Reason = reason, Callback = process.Graphics.Callback, battle.Frame, actor.ActiveAnimationDirectory, actor.SequenceState?.Pose, actor.SequenceState?.PoseFrame114 }, Boundary = "At most155 changed paired clocks and one despawn boundary; only original shape13 terminal ring and actual prepared own-hand centroid are measured. Native source only; no game callback or geometry dump." }, new JsonSerializerOptions { WriteIndented = true }));
        }

        int Offset(uint address) => checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
        uint Dword(uint address) => arena.ReadUInt32(Offset(address));
    }

    private static object Point(Vector3 value) => new { value.X, value.Y, value.Z };

    private sealed class Observation
    {
        internal long LastCallback = -1;
        internal bool Finished;
        internal bool Truncated;
        internal object? Birth;
        internal object? LastAbsent;
        internal List<object> Rows { get; } = [];
        internal (byte, byte, byte?, byte?)? LastClock;
        internal ImmutableArray<NativeBattleRawVertex>? Ring;
    }
}
