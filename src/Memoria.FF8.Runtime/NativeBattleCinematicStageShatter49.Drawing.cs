using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicStageShatter49Camera(NativeBattleWaterExecutionFrame Frame, uint Address, NativeBattleActionMatrix Matrix, NativeBattleCameraProjection Projection);
public sealed record NativeBattleCinematicStageShatter49Result(NativeBattleWaterExecutionFrame Frame, NativeBattleStageState Stage, uint? BackColor);
public sealed record NativeBattleCinematicStageShatter49Services(Func<NativeBattleWaterExecutionFrame, short, uint> ResolveSlot, Func<int, NativeBattleStageGroup, NativeBattleStageGroup> RestoreStageGroup508C90, Func<NativeBattleWaterExecutionFrame, NativeBattleCinematicStageShatter49Camera> SelectCameraB65520, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

public static partial class NativeBattleCinematicStageShatter49
{
    public static NativeBattleCinematicStageShatter49Result Draw(NativeBattleWaterExecutionFrame frame, NativeBattleStageState stage, ImmutableArray<NativeBattleRawMesh> geometry, NativeBattleCinematicStageShatter49Services services, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (stage.Groups.IsDefault || stage.Groups.Length != 4 || geometry.IsDefault)
        {
            throw new NotSupportedException("Native stage shatter requires its actual four stage descriptors and original geometry.");
        }
        uint payload = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
        uint records = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        if (records == 0)
        {
            (frame, records) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, AllocationBytes);
            frame = WriteSlotDword(frame, 0xBC, records);
            frame = InitializeRecords(frame, records, SignedWord(frame, payload + 2));
            stage = stage with { Groups = stage.Groups.Select((group, index) => group with { Flags = index < 3 ? (byte)(group.Flags & ~2) : (byte)(group.Flags | 2) }).ToImmutableArray() };
        }
        uint reference = services.ResolveSlot(frame, SignedWord(frame, payload + 4));
        int radius = unchecked((short)SlotWord(frame, 0x8C));
        frame = frame.WriteTemporaryDword(0x6C, unchecked((uint)radius)).WriteTemporaryDword(0x80, unchecked((uint)(radius * radius)));
        for (int axis = 0; axis < 3; axis++)
        {
            frame = frame.WriteTemporaryDword(0x60 + axis * 4, unchecked((uint)(int)(short)SlotWord(frame, 0x94 + axis * 2)));
        }
        frame = frame.WriteTemporaryDword(0x70, unchecked((uint)(int)SignedWord(frame, reference + 0x96))).WriteTemporaryDword(0x74, unchecked((uint)(int)SignedWord(frame, reference + 0x8E))).WriteTemporaryDword(0x90, unchecked((uint)(int)SignedWord(frame, reference + 0x98))).WriteTemporaryDword(0x94, unchecked((uint)(int)SignedWord(frame, reference + 0x90))).WriteTemporaryDword(0x98, unchecked(payload + (uint)(int)SignedWord(frame, payload))).WriteTemporaryDword(0x9C, unchecked((uint)(int)SignedWord(frame, payload + 2)));
        NativeBattleCinematicStageShatter49Capture capture = new(unchecked((int)frame.ReadTemporaryDword(0x60)), unchecked((int)frame.ReadTemporaryDword(0x64)), unchecked((int)frame.ReadTemporaryDword(0x68)), unchecked((int)frame.ReadTemporaryDword(0x80)), unchecked((short)frame.ReadTemporaryDword(0x90)), unchecked((short)frame.ReadTemporaryDword(0x94)), unchecked((short)frame.ReadTemporaryDword(0x70)), unchecked((short)frame.ReadTemporaryDword(0x74)));
        uint cursor = records;
        uint? backColor = null;
        for (int groupIndex = 0; groupIndex < 3; groupIndex++)
        {
            NativeBattleStageGroup group = stage.Groups[groupIndex];
            if ((group.Flags & 1) == 0)
            {
                cursor = 0;
                continue;
            }
            ImmutableArray<NativeBattleRawMesh> meshes = geometry.Where(mesh => mesh.StageGroup == groupIndex).ToImmutableArray();
            group = services.RestoreStageGroup508C90(groupIndex, group);
            uint captureCursor = cursor;
            foreach (NativeBattleRawMesh mesh in meshes)
            {
                mesh.Validate();
                NativeBattleVector[] transformed = mesh.Vertices.Select(vertex => CaptureVertex(RequireBone(group, vertex.Bone), vertex.Position)).ToArray();
                uint scratch = CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x74));
                frame = frame.WriteTemporaryDword(0x88, scratch);
                for (int vertex = 0; vertex < transformed.Length; vertex++)
                {
                    frame = StoreVector(frame, scratch + (uint)(vertex * 8), transformed[vertex]);
                }
                foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
                {
                    RequireRecord(records, captureCursor);
                    int[] indices = VertexIndices(polygon, transformed.Length);
                    ImmutableArray<NativeBattleVector> capturedVertices = indices.Select(index => transformed[index]).ToImmutableArray();
                    frame = pilotIdentity is { Effect: 219 } ? CaptureFacePresentation(frame, captureCursor, capturedVertices, capture) : CaptureFace(frame, captureCursor, capturedVertices, capture);
                    captureCursor += RecordSize;
                }
            }
            group = services.RestoreStageGroup508C90(groupIndex, group);
            NativeBattleCinematicStageShatter49Camera camera = services.SelectCameraB65520(frame);
            frame = camera.Frame.WriteTemporaryDword(0x84, camera.Address);
            group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(group.Matrices, camera.Matrix, 0) };
            stage = stage with { Groups = stage.Groups.SetItem(groupIndex, group) };
            backColor = group.ColorFlags & 0xFFFFFF;
            foreach (NativeBattleRawMesh mesh in meshes)
            {
                uint scratch = CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x74));
                frame = frame.WriteTemporaryDword(0x88, scratch);
                NativeBattleModuleProjectedVertex[] projected = mesh.Vertices.Select(vertex => NativeBattleModuleProjection.Project45FAA0(RequireBone(group, vertex.Bone), vertex.Position, camera.Projection, 0, 0)).ToArray();
                for (int vertex = 0; vertex < projected.Length; vertex++)
                {
                    frame = StoreDword(frame, scratch + (uint)(vertex * 8), projected[vertex].Screen);
                    frame = StoreWord(frame, scratch + (uint)(vertex * 8 + 4), projected[vertex].Depth);
                }
                uint manager = CoreDword(frame, 0x27973EC);
                uint output = CoreDword(frame, unchecked(manager + 0x7C));
                foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
                {
                    RequireRecord(records, cursor);
                    int[] indices = VertexIndices(polygon, projected.Length);
                    NativeBattleCinematicStageShatter49Emission emitted = RenderFace(frame, cursor, polygon, indices.Select(index => projected[index]).ToImmutableArray(), camera.Matrix, camera.Projection, frame.ReadTemporaryDword(0x98), pilotIdentity is null ? default : indices.Select(index => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(RequireBone(group, mesh.Vertices[index].Bone), mesh.Vertices[index].Position), camera.Projection)).ToImmutableArray(), pilotIdentity is { } identity ? identity with { Primitive = unchecked((int)cursor), ResourceGeneration = identity.Effect == 219 ? unchecked((NativeBattlePilotVisualLifetimes.EpochFor(mesh) * 397 ^ NativeBattlePilotVisualLifetimes.Identity(219, frame.Arena, cursor, 0).Generation) * 397 ^ identity.ResourceGeneration) : identity.ResourceGeneration } : null);
                    frame = services.Append(emitted.Frame, output, emitted.OrderingAddress, emitted.Packets);
                    output = unchecked(output + (uint)emitted.Packets.AllocationBytes);
                    cursor += RecordSize;
                }
                frame = WriteCoreDword(frame, unchecked(manager + 0x7C), output);
            }
            if (captureCursor != cursor)
            {
                throw new InvalidDataException("Native stage shatter capture and rendering traversed different original polygon owners.");
            }
        }
        return new(frame, stage, backColor);
    }

    public static NativeBattleStageGroup RestoreStageGroup508C90(NativeBattleStageGroup group)
    {
        NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Native stage shatter requires its retained current stage pose source.");
        return group with { Matrices = NativeBattlePoseMatrices.StageBoneMatrices(source, source.Sample(group.Animation, group.Counter, group.PoseFlags)) };
    }

    public static NativeBattleVector CaptureVertex(NativeBattleActionMatrix matrix, NativeBattleVector vertex)
    {
        NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(matrix, vertex);
        return new(unchecked((short)mac.X), unchecked((short)mac.Y), unchecked((short)mac.Z));
    }

    private static NativeBattleActionMatrix RequireBone(NativeBattleStageGroup group, ushort index)
    {
        if (group.Matrices.IsDefault || index >= group.Matrices.Length)
        {
            throw new NotSupportedException("Native stage shatter requires the selected original stage bone matrix.");
        }
        return group.Matrices[index];
    }

    private static int[] VertexIndices(NativeBattleRawPolygon polygon, int vertices)
    {
        if (polygon.Bytes.IsDefault || polygon.Bytes.Length != (polygon.Quad ? 24 : 20))
        {
            throw new NotSupportedException("Native stage shatter requires its original colored stage polygon layout.");
        }
        int count = polygon.Quad ? 4 : 3;
        int[] indices = Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(index * 2)) & 0xFFF).ToArray();
        if (indices.Any(index => index >= vertices))
        {
            throw new InvalidDataException("Native stage shatter face index leaves its actual current vertex scratch.");
        }
        return indices;
    }

    private static void RequireRecord(uint first, uint current)
    {
        if (current < first || current - first >= AllocationBytes || (current - first) % RecordSize != 0)
        {
            throw new NotSupportedException("Native stage shatter polygon count leaves its actual allocated record bank.");
        }
    }
}
