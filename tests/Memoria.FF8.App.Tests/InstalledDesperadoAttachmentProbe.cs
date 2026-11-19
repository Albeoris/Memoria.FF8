using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>One opt-in read-only observation of the authored hanging phase, without retaining native assets.</summary>
internal static class InstalledDesperadoAttachmentProbe
{
    internal static void CaptureIfRequested(GameWorld world, string actorId, int recipient, BattleLimitFamily family)
    {
        InstalledDesperadoOwnershipTrace.CaptureIfRequested(world, actorId, recipient, family);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_DESPERADO_ATTACHMENT_PROBE");
        if (string.IsNullOrWhiteSpace(output) || family != BattleLimitFamily.Desperado || recipient is not (0 or 6 or 8) || world.Battle?.Presentation is not { } process || process.Graphics.Callback != 201)
        {
            return;
        }
        string path = Path.Combine(output, $"attachment-{recipient:D2}.json");
        if (File.Exists(path))
        {
            return;
        }
        BattleSession battle = world.Battle!;
        NativeBattleGraphicsState graphics = process.Graphics;
        Assert.Equal(261, graphics.OriginalEffect!.Binding.Effect);
        NativeBattleEffectArena arena = process.Memory.Arena!;
        NativeBattleScriptedEffectMemory core = process.Memory.ScriptedEffects!;
        NativeBattleGraphicsModel actor = graphics.ActorModels.Get(battle.ReadPresentationModelOwner(actorId));
        BattleCombatantDefinition definition = battle.GetCombatantDefinition(actorId);
        var body = world.BattleResources.LoadPlayer(recipient, definition.Costume);
        NativeBattlePoseSource skeleton = body.NativePoseSource!;
        NativeBattleDecodedPose decoded = actor.SequenceState!.DecodedPose!;
        ImmutableArray<NativeBattleActionMatrix> localBones = NativeBattlePoseMatrices.StageBoneMatrices(skeleton, decoded);
        NativeBattleActionMatrix worldMatrix = actor.GeometryWorldMatrix!;
        var pairs = Memoria.FF8.Assets.BattleHumanoidRigProfiles.SemanticPairs(recipient, body, recipient, body);
        int hand = pairs.Single(pair => pair.Joint == "hand-a").Source;
        Vector3 HandPoint(int bone) => Vector3.Transform(new(localBones[bone].X, localBones[bone].Y, localBones[bone].Z), Matrix(worldMatrix));
        Vector3 handWorld = HandPoint(hand);
        Vector3[] handVertices = body.Meshes.SelectMany(mesh => mesh.Vertices).Where(vertex => vertex.Bone == hand).Select(vertex => World(localBones[hand], vertex.NativePosition!.Value)).ToArray();
        NativeBattlePreparedPresentation prepared = battle.PreparedPresentation ?? throw new InvalidOperationException("The attachment observation requires the retained actual draw.");
        List<NativeBattleLogicalCommandReference> retained = [];
        foreach (ref readonly NativeBattleLogicalResolvedCommand command in prepared.Primitives.LastCommands)
        {
            retained.Add(command.Command);
        }
        var emissions = retained.GroupBy(command => new
        {
            command.Pilot.Identity.Effect,
            command.Pilot.Identity.Bank,
            command.Pilot.Identity.Slot,
            command.Pilot.Identity.Generation,
            command.Pilot.Identity.ResourceGeneration,
            command.Pilot.Identity.ModelOwner,
            Pose = command.Pilot.ModelPose
        }).ToArray();
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, $"emissions-{recipient:D2}.json"), JsonSerializer.Serialize(new
        {
            Recipient = recipient,
            definition.Costume,
            Actor = actorId,
            ActorOwner = battle.ReadPresentationModelOwner(actorId).ToString(),
            Callback = graphics.Callback,
            PreparedCallback = prepared.Callback,
            PreparedFrame = prepared.Frame,
            TotalCommands = retained.Count,
            TotalEmissionGroups = emissions.Length,
            Truncated = emissions.Length > 32,
            Emissions = emissions.OrderByDescending(group => group.Key.ModelOwner is not null).ThenByDescending(group => group.Key.Pose is not null).Take(32).Select(group => new
            {
                group.Key.Effect,
                group.Key.Bank,
                group.Key.Slot,
                group.Key.Generation,
                group.Key.ResourceGeneration,
                Owner = group.Key.ModelOwner?.ToString(),
                OwnerType = group.Key.ModelOwner?.GetType().Name,
                FirstPrimitive = group.First().Pilot.Identity.Primitive,
                Commands = group.Count(),
                ModelPose = group.Key.Pose is not null,
                FirstMeshVertices = group.Key.Pose?.Positions(0).Length,
                ActorIds = group.Select(command => command.PresentationActorId).Distinct().ToArray()
            }).ToArray(),
            Boundary = "At most32 actual prepared emission groups, ordered with model-owned groups first, then other ModelPose groups. No geometry retained. First-mesh vertex count is an observation, not admission."
        }, new JsonSerializerOptions { WriteIndented = true }));
        List<(NativeBattlePilotVisualIdentity Identity, Vector3[] Vertices, (Vector3 A, Vector3 B, Vector3 C)[] Triangles)> renderedHands = [];
        foreach (var emission in emissions.Where(group => group.Key.ModelOwner == battle.ReadPresentationModelOwner(actorId) && group.Key.Bank == 0x502D40 && group.Key.Pose is not null))
        {
            // NativePrimitiveFrame assigns this bank to the ordinary body producer; linked geometry uses507BF0.
            ImmutableArray<Vector3> positions = emission.Key.Pose!.Positions(0);
            Assert.Equal(body.Meshes[0].Vertices.Count, positions.Length);
            Vector3[] vertices = body.Meshes[0].Vertices.Select((vertex, index) => (vertex, index)).Where(item => item.vertex.Bone == hand).Select(item => positions[item.index]).ToArray();
            List<(Vector3 A, Vector3 B, Vector3 C)> triangles = [];
            foreach (var face in body.Meshes[0].Faces)
            {
                if (face.Vertices.Any(index => body.Meshes[0].Vertices[index].Bone != hand))
                {
                    continue;
                }
                Assert.True(face.Vertices.Length is 3 or 4);
                Vector3 At(int corner) => positions[face.Vertices[corner]];
                triangles.Add((At(0), At(1), At(face.Vertices.Length == 4 ? 3 : 2)));
                if (face.Vertices.Length == 4)
                {
                    triangles.Add((At(3), At(2), At(0)));
                }
            }
            Assert.NotEmpty(vertices);
            Assert.NotEmpty(triangles);
            renderedHands.Add((emission.First().Pilot.Identity, vertices, triangles.ToArray()));
        }
        uint manager = DwordCore(0x27973EC);
        uint owner = DwordCore(0x279744C);
        uint bank = DwordCore(manager + 0x90);
        int capacity = BinaryPrimitives.ReadUInt16LittleEndian(core.Read(owner + 0x38, 2));
        Assert.InRange(capacity, 1, 256);
        List<object> models = [];
        HashSet<int> observed = [];
        for (int order = 0; order < 256; order++)
        {
            byte slot = core.Read(0x2797554u + (uint)order, 1)[0];
            if (slot == byte.MaxValue)
            {
                break;
            }
            int index = slot;
            if (!observed.Add(index))
            {
                continue;
            }
            Assert.InRange(index, 0, capacity - 1);
            uint node = bank + (uint)(index * 0x100);
            if (Byte(node + 0x1C) != 3)
            {
                continue;
            }
            uint model = Dword(node + 0xBC);
            uint table = Dword(model + 0x14);
            uint rig = Dword(table);
            uint geometry = Dword(table + 4);
            int boneCount = Byte(rig);
            Assert.InRange(boneCount, 1, 128);
            ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => arena.Read(Offset(geometry + (uint)offset), length), boneCount);
            NativeBattleActionMatrix[] bones = Enumerable.Range(0, boneCount).Select(bone => NativeBattleArenaPose.ReadMatrix(arena, Offset(rig + 0x20u + (uint)(bone * 0x30)))).ToArray();
            var vertices = meshes.SelectMany(mesh => mesh.Vertices).Select((vertex, vertexIndex) => new { Index = vertexIndex, vertex.Bone, Local = vertex.Position, World = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(bones[vertex.Bone], vertex.Position), process.Camera.Projection!) }).ToArray();
            Assert.NotEmpty(vertices);
            var lower = vertices.MaxBy(vertex => vertex.World.Y)!;
            var upper = vertices.MinBy(vertex => vertex.World.Y)!;
            var endRing = vertices.Where(vertex => vertex.Bone == lower.Bone && vertex.Local.Z == lower.Local.Z).DistinctBy(vertex => vertex.Local).ToArray();
            Assert.NotEmpty(endRing);
            Vector3 ringCenter = endRing.Aggregate(Vector3.Zero, (sum, vertex) => sum + vertex.World) / endRing.Length;
            Vector3 sourceCenter = endRing.Aggregate(Vector3.Zero, (sum, vertex) => sum + new Vector3(vertex.Local.X, vertex.Local.Y, vertex.Local.Z)) / endRing.Length;
            NativeBattleResourcePointer? pointer = arena.ReadResourcePointer(Offset(table + 4));
            models.Add(new
            {
                Bank = bank,
                Slot = index,
                Node = node,
                Model = model,
                Argument = Word(model + 8),
                Shape = Word(model + 8) & 255,
                SelectedAnimation = Byte(model + 9),
                Pose = Byte(model + 0x20),
                PoseFlags = Byte(model + 0x21),
                PoseFrame = Byte(model + 0x26),
                PoseLimit = Byte(model + 0x27),
                Geometry = geometry,
                ResourcePointer = pointer is null ? null : new { CpuBank = pointer.Cpu?.Bank, CpuOffset = pointer.Cpu?.Offset, pointer.StagingAddress, pointer.Length },
                File0Base = DwordCore(0x2798A68),
                File1Base = DwordCore(0x2798A6C),
                GeometryOffsetFromFile0 = (long)geometry - DwordCore(0x2798A68),
                GeometryOffsetFromFile1 = (long)geometry - DwordCore(0x2798A6C),
                BoneCount = boneCount,
                Vertices = vertices.Length,
                Faces = meshes.Sum(mesh => mesh.Polygons.Length),
                Lower = new { lower.Index, lower.Bone, Source = Point(lower.Local), World = Point(lower.World) },
                Upper = new { upper.Index, upper.Bone, Source = Point(upper.Local), World = Point(upper.World) },
                EndRing = new
                {
                    lower.Bone,
                    SourcePlaneZ = lower.Local.Z,
                    UniqueVertices = endRing.Length,
                    SourceCenter = Point(sourceCenter),
                    WorldCenter = Point(ringCenter),
                    CenterToHandPivot = Vector3.Distance(ringCenter, handWorld),
                    CenterToHandMeshVertex = handVertices.Select(vertex => Vector3.Distance(ringCenter, vertex)).Min(),
                    RenderedHands = renderedHands.Select(rendered => new
                    {
                        rendered.Identity.Effect,
                        rendered.Identity.Bank,
                        rendered.Identity.Slot,
                        Vertices = rendered.Vertices.Length,
                        Triangles = rendered.Triangles.Length,
                        Center = Point(rendered.Vertices.Aggregate(Vector3.Zero, (sum, vertex) => sum + vertex) / rendered.Vertices.Length),
                        Minimum = Point(rendered.Vertices.Aggregate(Vector3.Min)),
                        Maximum = Point(rendered.Vertices.Aggregate(Vector3.Max)),
                        CenterToHandSurface = rendered.Triangles.Min(triangle => SurfaceDistance(ringCenter, triangle)),
                        RingVertexToHandSurface = endRing.Min(vertex => rendered.Triangles.Min(triangle => SurfaceDistance(vertex.World, triangle)))
                    }).ToArray(),
                    Selection = "Distinct authored vertices on the selected lower extremum's bone and exact local Z plane; no distance threshold or attachment offset."
                },
                LowerToHandPivot = Vector3.Distance(lower.World, handWorld),
                LowerToHandMeshVertex = handVertices.Select(vertex => Vector3.Distance(lower.World, vertex)).Min()
            });
        }
        Directory.CreateDirectory(output);
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            Recipient = recipient,
            definition.Costume,
            Actor = actorId,
            Callback = graphics.Callback,
            Frame = battle.Frame,
            Pose = actor.SequenceState.Pose,
            PoseFrame = actor.SequenceState.PoseFrame114,
            PoseLimit = actor.SequenceState.PoseLimit115,
            ActiveBank = actor.ActiveAnimationBank,
            HandA = new { Bone = hand, World = Point(handWorld), HandVertices = handVertices.Length },
            CameraView = process.Camera.Projection!.View,
            Models = models,
            Boundary = "Retained post-draw node skeleton matrices are camera-composed; prop corners use the actual current camera inverse. The separate emissions inventory observes prepared ownership and clock before any body-emission selection. HandA remains the ordinary actor-basis observation and is not an accepted cinematic grip distance. Only extrema and ring center are retained; no asset geometry or extra game callback."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(201, process.Graphics.Callback);

        int Offset(uint address) => checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
        uint DwordCore(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(core.Read(address, 4));
        uint Dword(uint address) => arena.ReadUInt32(Offset(address));
        ushort Word(uint address) => arena.ReadUInt16(Offset(address));
        byte Byte(uint address) => arena.ReadByte(Offset(address));
        Vector3 World(NativeBattleActionMatrix bone, NativeBattleVector vertex)
        {
            Vector3 local = NativeBattlePilotVisualGeometry.Transform(bone, vertex);
            return Vector3.Transform(local, Matrix(worldMatrix));
        }
    }

    private static object Point(NativeBattleVector vector) => new { vector.X, vector.Y, vector.Z };
    private static object Point(Vector3 vector) => new { vector.X, vector.Y, vector.Z };

    private static float SurfaceDistance(Vector3 point, (Vector3 A, Vector3 B, Vector3 C) triangle)
    {
        (Vector3 a, Vector3 b, Vector3 c) = triangle;
        Vector3 normal = Vector3.Cross(b - a, c - a);
        float squared = normal.LengthSquared();
        if (squared > 0)
        {
            Vector3 projected = point - normal * (Vector3.Dot(point - a, normal) / squared);
            if (Vector3.Dot(Vector3.Cross(b - a, projected - a), normal) >= 0 && Vector3.Dot(Vector3.Cross(c - b, projected - b), normal) >= 0 && Vector3.Dot(Vector3.Cross(a - c, projected - c), normal) >= 0)
            {
                return Vector3.Distance(point, projected);
            }
        }
        return MathF.Min(Segment(a, b), MathF.Min(Segment(b, c), Segment(c, a)));

        float Segment(Vector3 from, Vector3 to)
        {
            Vector3 edge = to - from;
            float length = edge.LengthSquared();
            float fraction = length == 0 ? 0 : Math.Clamp(Vector3.Dot(point - from, edge) / length, 0, 1);
            return Vector3.Distance(point, from + edge * fraction);
        }
    }

    private static Matrix4x4 Matrix(NativeBattleActionMatrix matrix)
    {
        var r = matrix.Rotation;
        return new(r[0] / 4096f, r[3] / 4096f, r[6] / 4096f, 0, r[1] / 4096f, r[4] / 4096f, r[7] / 4096f, 0, r[2] / 4096f, r[5] / 4096f, r[8] / 4096f, 0, matrix.X, matrix.Y, matrix.Z, 1);
    }
}
