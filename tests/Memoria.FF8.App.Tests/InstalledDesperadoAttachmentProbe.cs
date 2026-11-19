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
        var body = world.BattleResources.LoadPlayer(recipient);
        NativeBattlePoseSource skeleton = body.NativePoseSource!;
        NativeBattleDecodedPose decoded = actor.SequenceState!.DecodedPose!;
        ImmutableArray<NativeBattleActionMatrix> localBones = NativeBattlePoseMatrices.StageBoneMatrices(skeleton, decoded);
        NativeBattleActionMatrix worldMatrix = actor.GeometryWorldMatrix!;
        var pairs = Memoria.FF8.Assets.BattleHumanoidRigProfiles.SemanticPairs(recipient, body, recipient, body);
        int hand = pairs.Single(pair => pair.Joint == "hand-a").Source;
        Vector3 HandPoint(int bone) => Vector3.Transform(new(localBones[bone].X, localBones[bone].Y, localBones[bone].Z), Matrix(worldMatrix));
        Vector3 handWorld = HandPoint(hand);
        Vector3[] handVertices = body.Meshes.SelectMany(mesh => mesh.Vertices).Where(vertex => vertex.Bone == hand).Select(vertex => World(localBones[hand], vertex.NativePosition!.Value)).ToArray();
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
            Boundary = "Retained post-draw node skeleton matrices are camera-composed; prop corners are converted through the actual current camera inverse. Complete meshes are examined in memory; only two extrema per model are retained. No game callback is executed by this probe."
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

    private static Matrix4x4 Matrix(NativeBattleActionMatrix matrix)
    {
        var r = matrix.Rotation;
        return new(r[0] / 4096f, r[3] / 4096f, r[6] / 4096f, 0, r[1] / 4096f, r[4] / 4096f, r[7] / 4096f, 0, r[2] / 4096f, r[5] / 4096f, r[8] / 4096f, 0, matrix.X, matrix.Y, matrix.Z, 1);
    }
}
