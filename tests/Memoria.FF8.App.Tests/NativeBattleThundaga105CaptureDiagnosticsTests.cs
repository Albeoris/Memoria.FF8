using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleThundaga105CaptureDiagnosticsTests
{
    [Fact]
    public void External_capture_input_receipt_uses_only_complete_snapshot_and_current_CSharp_contracts()
    {
        string? sourcePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_THUNDAGA105_CAPTURE_DIAGNOSTIC_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourcePath), "Set MEMORIA_FF8_THUNDAGA105_CAPTURE_DIAGNOSTIC_SNAPSHOT to the complete phase28-before standalone.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_THUNDAGA105_CAPTURE_DIAGNOSTIC_OUTPUT") ?? throw new InvalidDataException("A distinct diagnostic output directory is required.");
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        BattleSnapshot snapshot = NativeBattleEffectFamilyRunner.LoadSnapshot(sourcePath!);
        BattleSession battle = BattleSession.Restore(snapshot);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
        NativeBattleOriginalEffectState effect = snapshot.NativeGraphics!.OriginalEffect!;
        Assert.Equal((ushort)105, effect.Binding.Effect);
        Assert.Equal(0x6D3DB0u, effect.Binding.Entry);
        NativeBattleThundaga105Resources resources = effect.Binding.Thundaga105Resources!;
        NativeBattleModulePoolTask primary = Assert.Single(effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleThundaga105Controller.PrimaryDescriptor).Slots, task => (task.Flags & 1) != 0);
        Assert.Equal((ushort)28, primary.ReadWord(0xC));
        uint actorAddress = primary.ReadDword(0x10);
        uint actorDelta = unchecked(actorAddress - 0x1D972C0u);
        Assert.True(actorDelta % 156 == 0 && actorDelta / 156 < 7);
        int actor = (int)(actorDelta / 156);
        NativeBattleGraphicsModel model = snapshot.NativeGraphics.Models[actor];
        Assert.True(model.ModelId4 < 16);
        Assert.Null(model.ActiveStagedResourceRow100);
        Assert.Null(model.ResourceMagicId);
        NativeBattlePoseSource actorPose = snapshot.Definition.NativeGraphics!.Bindings.Single(binding => binding.ActorId == snapshot.NativeGraphics.ActorIds[actor]).Resource.PoseSource ?? throw new InvalidDataException("The diagnostic requires the retained party pose source.");
        NativeBattleVector xyz = NativeBattlePoseMatrices.ReadBone(model, actorPose, 241, 0);
        short angle = snapshot.NativeGraphics.Models[actor].Transform!.Angle36;
        byte[] anchor = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(anchor, xyz.X);
        BinaryPrimitives.WriteInt16LittleEndian(anchor.AsSpan(2), xyz.Y);
        BinaryPrimitives.WriteInt16LittleEndian(anchor.AsSpan(4), xyz.Z);
        BinaryPrimitives.WriteInt16LittleEndian(anchor.AsSpan(6), angle);
        int ownerScale = unchecked((int)primary.ReadDword(0x18));
        NativeBattleThundaga105State retained = snapshot.NativeEffectMemory!.Thundaga105!;
        NativeBattleEffectTaskPool secondary = effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleThundaga105Controller.SecondaryDescriptor);
        NativeCrtRandomState random = snapshot.NativeCrtRandom!;
        NativeBattleThundaga105Bounds bounds = NativeBattleThundaga105Bounds.Begin6D5FA0();
        List<object> fragments = [];
        for (int index = 0; index < resources.FragmentPairs.Length; index++)
        {
            if (!secondary.Slots.Any(task => (task.Flags & 1) == 0))
            {
                fragments.Add(new { Index = index, Allocated = false });
                continue;
            }
            (secondary, int slot) = secondary.Append(0x6D5AD0);
            NativeBattleThundaga105FragmentInitialization initialized = NativeBattleThundaga105Fragments.Initialize6D5990(secondary.Slots[slot], resources.FragmentPairs[index], retained.MutableFragments[index], xyz, ownerScale, bounds, random);
            secondary = secondary with { Slots = secondary.Slots.SetItem(slot, initialized.Task) };
            bounds = initialized.Bounds;
            random = initialized.Random;
            fragments.Add(new { Index = index, Allocated = true, Slot = slot, initialized.Task, initialized.Bounds, MeshSha256 = Convert.ToHexString(SHA256.HashData(initialized.Mesh.Bytes.AsSpan())) });
        }
        NativeBattleCameraProjection priorProjection = snapshot.NativeCamera!.Projection!;
        NativeBattleThundaga105CaptureTransform capture = NativeBattleThundaga105CaptureGeometry.Transform6D5620(anchor.ToImmutableArray(), bounds, priorProjection);
        NativeBattleStageState stage = snapshot.NativeStage!;
        stage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = 0, ColorFlags = 0 }).ToImmutableArray() };
        ImmutableArray<NativeBattleRawMesh> geometry = snapshot.Definition.NativeStageSource!.Geometry ?? throw new InvalidDataException("The diagnostic requires original stage geometry.");
        NativeBattleSummonColors colors = snapshot.Definition.NativeGraphics!.Rules.SummonColors!;
        NativeBattleThundaga105CaptureStage drawn = NativeBattleThundaga105CaptureGeometry.DrawGroup6D5620(stage, geometry, capture, colors);
        NativeBattleStageGroup group0 = stage.Groups[0];
        NativeBattlePoseSource poseSource = group0.Source!;
        NativeBattleDecodedPose pose = poseSource.Sample(group0.Animation, group0.Counter, group0.PoseFlags);
        ImmutableArray<NativeBattleActionMatrix> boneMatrices = NativeBattlePoseMatrices.StageBoneMatrices(poseSource, pose);
        List<(int X, int Y, int Z)> worldVertices = [];
        foreach (NativeBattleRawMesh mesh in geometry.Where(mesh => mesh.StageGroup == 0))
        {
            foreach (NativeBattleRawVertex vertex in mesh.Vertices)
            {
                NativeBattleActionMatrix matrix = boneMatrices[vertex.Bone];
                (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(matrix, vertex.Position.X, vertex.Position.Y, vertex.Position.Z);
                worldVertices.Add((unchecked(x + matrix.X), unchecked(y + matrix.Y), unchecked(z + matrix.Z)));
            }
        }
        List<object> packets = [];
        foreach (NativeBattleGeometryPrimitive packet in drawn.Packets)
        {
            byte opcode = (byte)(packet.Words[1] >> 24);
            Assert.True((opcode & 0xFC) is 0x24 or 0x2C);
            int count = (opcode & 0xFC) == 0x2C ? 4 : 3;
            List<object> corners = [];
            int shared = 15;
            for (int index = 0; index < count; index++)
            {
                int offset = new[] { 2, 4, 6, 8 }[index];
                uint screen = packet.Words[offset];
                short x = unchecked((short)screen);
                short y = unchecked((short)(screen >> 16));
                int softwareX = unchecked((x << 13) + (704 << 16));
                int softwareY = unchecked((y << 13) + (256 << 16)) >> 16;
                int code = (softwareX < 704 << 16 ? 1 : 0) | (softwareX > 832 << 16 ? 2 : 0) | (softwareY < 256 ? 4 : 0) | (softwareY > 384 ? 8 : 0);
                shared &= code;
                corners.Add(new { X = x, Y = y, SoftwareX16_16 = softwareX, SoftwareY = softwareY, Outcode = code });
            }
            packets.Add(new { packet.Bucket, Opcode = opcode, packet.Words, packet.VertexPayload4, Corners = corners, SharedOutcode = shared, TriviallyRejected = shared != 0 });
        }
        object[] groupAnalyses = [AnalyzeGroup(0), AnalyzeGroup(1)];

        object AnalyzeGroup(int groupIndex)
        {
            NativeBattleStageGroup selectedGroup = stage.Groups[groupIndex];
            NativeBattlePoseSource selectedSource = selectedGroup.Source!;
            NativeBattleDecodedPose selectedPose = selectedSource.Sample(selectedGroup.Animation, selectedGroup.Counter, selectedGroup.PoseFlags);
            ImmutableArray<NativeBattleActionMatrix> worldBones = NativeBattlePoseMatrices.StageBoneMatrices(selectedSource, selectedPose);
            ImmutableArray<NativeBattleActionMatrix> captureBones = NativeBattlePoseMatrices.ComposeStageCamera(worldBones, capture.Matrix, 0);
            double x0 = (-64d - capture.Matrix.X) * 4096d / capture.Matrix.Rotation[0];
            double x1 = (64d - capture.Matrix.X) * 4096d / capture.Matrix.Rotation[0];
            double z0 = (-64d - capture.Matrix.Y) * 4096d / capture.Matrix.Rotation[5];
            double z1 = (64d - capture.Matrix.Y) * 4096d / capture.Matrix.Rotation[5];
            double minX = Math.Min(x0, x1);
            double maxX = Math.Max(x0, x1);
            double minZ = Math.Min(z0, z1);
            double maxZ = Math.Max(z0, z1);
            List<object> faces = [];
            int overlaps = 0;
            int emitted = 0;
            int survivedSoftwareClip = 0;
            Dictionary<string, int> classifications = [];
            for (int meshIndex = 0; meshIndex < geometry.Length; meshIndex++)
            {
                NativeBattleRawMesh mesh = geometry[meshIndex];
                if (mesh.StageGroup != groupIndex)
                {
                    continue;
                }
                var world = mesh.Vertices.Select(vertex =>
                {
                    NativeBattleActionMatrix matrix = worldBones[vertex.Bone];
                    (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(matrix, vertex.Position.X, vertex.Position.Y, vertex.Position.Z);
                    return (X: unchecked(x + matrix.X), Y: unchecked(y + matrix.Y), Z: unchecked(z + matrix.Z));
                }).ToArray();
                NativeBattleProjectedVertex[] projected = mesh.Vertices.Select(vertex => NativeBattleGeometryProjection.Project50F900(captureBones[vertex.Bone], vertex.Position, capture.Projection, new(0, 0, 320, 216))).ToArray();
                for (int polygonIndex = 0; polygonIndex < mesh.Polygons.Length; polygonIndex++)
                {
                    NativeBattleRawPolygon polygon = mesh.Polygons[polygonIndex];
                    int count = polygon.Quad ? 4 : 3;
                    int[] indices = Enumerable.Range(0, count).Select(corner => BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(corner * 2)) & (corner == 0 ? 0xFFF : 0xFFFF)).ToArray();
                    var selectedWorld = indices.Select(index => world[index]).ToArray();
                    int faceMinX = selectedWorld.Min(vertex => vertex.X);
                    int faceMaxX = selectedWorld.Max(vertex => vertex.X);
                    int faceMinZ = selectedWorld.Min(vertex => vertex.Z);
                    int faceMaxZ = selectedWorld.Max(vertex => vertex.Z);
                    bool overlap = faceMaxX >= minX && faceMinX <= maxX && faceMaxZ >= minZ && faceMinZ <= maxZ;
                    overlaps += overlap ? 1 : 0;
                    NativeBattleProjectedVertex[] corners = indices.Select(index => projected[index]).ToArray();
                    uint anyClip = corners.Aggregate(0u, (value, vertex) => value | vertex.DepthAndClip);
                    uint sharedClip = corners.Aggregate(uint.MaxValue, (value, vertex) => value & vertex.DepthAndClip);
                    bool front = NativeBattleGeometryPackets.Accept510680(corners[0].Screen, corners[1].Screen, corners[2].Screen);
                    string classification = (anyClip & 0x60000) != 0 ? "near-or-depth-flags" : (sharedClip & 0x3F00000) != 0 ? "shared-projection-clip" : !front ? "nclip-rejected" : "emitted";
                    classifications[classification] = classifications.GetValueOrDefault(classification) + 1;
                    NativeBattleGeometryPrimitive? primitive = NativeBattleGeometryPackets.EmitRaw50FDF0(polygon.Bytes.AsSpan(), projected, polygon.Quad, 0, 17, 16, 0, colors);
                    Assert.Equal(classification == "emitted", primitive is not null);
                    bool softwareAccepted = false;
                    int clippedVertices = 0;
                    if (primitive is not null)
                    {
                        emitted++;
                        int[] xy = polygon.Quad ? [2, 4, 8, 6] : [2, 4, 6];
                        int[] uv = polygon.Quad ? [3, 5, 9, 7] : [3, 5, 7];
                        ImmutableArray<NativeSoftwareClipVertex>.Builder softwareVertices = ImmutableArray.CreateBuilder<NativeSoftwareClipVertex>();
                        for (int corner = 0; corner < count; corner++)
                        {
                            uint screen = primitive.Words[xy[corner]];
                            short x = unchecked((short)screen);
                            short y = unchecked((short)(screen >> 16));
                            uint texture = primitive.Words[uv[corner]];
                            softwareVertices.Add(new(unchecked((x << 13) + (704 << 16)), unchecked((y << 13) + (256 << 16)) >> 16, (int)(texture & 255) << 10, (int)((texture >> 8) & 255) << 10, null, null, null));
                        }
                        uint color = primitive.Words[1];
                        uint page = primitive.Words[5] >> 16;
                        NativeSoftwareClippedPolygon softwarePolygon = new([2, 0, 0, 0, (byte)color, (byte)(color >> 8), (byte)(color >> 16), (byte)((page >> 5) & 3)], softwareVertices.ToImmutable());
                        NativeSoftwarePolygonClip.Submit466880(softwarePolygon, new(704 << 16, 256, 832 << 16, 384), Accepted, Accepted);
                        void Accepted(NativeSoftwareClippedPolygon accepted)
                        {
                            clippedVertices = accepted.Vertices.Length;
                            softwareAccepted = clippedVertices >= 3;
                        }
                        survivedSoftwareClip += softwareAccepted ? 1 : 0;
                    }
                    faces.Add(new { MeshIndex = meshIndex, PolygonIndex = polygonIndex, polygon.Quad, WorldXZBounds = new { MinimumX = faceMinX, MaximumX = faceMaxX, MinimumZ = faceMinZ, MaximumZ = faceMaxZ }, OverlapsCaptureWorldXZBounds = overlap, ProjectedCorners = corners.Select(vertex => new { X = unchecked((short)vertex.Screen), Y = unchecked((short)(vertex.Screen >> 16)), vertex.DepthAndClip }).ToArray(), AnyClip = anyClip, SharedClip = sharedClip, AcceptedNclip = front, Classification = classification, SoftwareClipSurvives = softwareAccepted, ClippedVertices = clippedVertices });
                }
            }
            return new { StageGroup = groupIndex, selectedGroup.Flags, selectedGroup.Animation, selectedGroup.Counter, selectedGroup.PoseFlags, CaptureWorldXZBounds = new { MinimumX = minX, MaximumX = maxX, MinimumZ = minZ, MaximumZ = maxZ, Contract = "Inverse axis-aligned capture projection at Z=H; bounding-box overlap only, not polygon intersection" }, SourceFaces = faces.Count, FaceBoundsOverlappingCapture = overlaps, ProjectionRejections = classifications, EmittedPackets = emitted, SurvivingSoftwareClip = survivedSoftwareClip, Faces = faces };
        }

        var receipt = new { Kind = "ExistingCSharpSnapshotInputReceiptNotNativeExecution", SourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath!))), NativeCallback = snapshot.NativeGraphics.Callback, Actor = actor, ActorAddress = actorAddress, Anchor241 = xyz, Angle36 = angle, AnchorRaw8 = anchor, OwnerScale = ownerScale, CrtBefore = snapshot.NativeCrtRandom, CrtAfterFragmentSetup = random, AggregateBounds = bounds, Capture = capture, SourceStageGroup0 = new { group0.Animation, group0.Counter, group0.PoseFlags, group0.Flags, VertexCount = worldVertices.Count, WorldBounds = new { Minimum = new { X = worldVertices.Min(vertex => vertex.X), Y = worldVertices.Min(vertex => vertex.Y), Z = worldVertices.Min(vertex => vertex.Z) }, Maximum = new { X = worldVertices.Max(vertex => vertex.X), Y = worldVertices.Max(vertex => vertex.Y), Z = worldVertices.Max(vertex => vertex.Z) } }, WorldCoordinateContract = "Existing StageBoneMatrices plus RotateLong56C600 and matrix translation; before capture camera composition" }, GroupAnalyses = groupAnalyses, Fragments = fragments, CapturedPackets = packets, CapturedPacketCount = packets.Count, Boundary = "No presentation callback or original native call executed; snapshot bone state used directly, fragment setup runs in local copies only; no pixel or native-equivalence claim" };
        File.WriteAllText(Path.Combine(output, "capture-input-receipt.json"), JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
    }
}
