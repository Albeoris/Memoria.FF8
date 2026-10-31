using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleZantetsukenRenderObject(NativeBattleRawMesh Mesh, uint? FaceAddress);
public sealed record NativeBattleZantetsukenRenderInputs(NativeBattlePackedMatrix32 Main, NativeBattlePackedMatrix32 Alternate, ImmutableArray<NativeBattleActionMatrix> Bones, Func<int, short?> ReadBonePadding, NativeBattleCameraProjection Projection, NativeBattleSummonColors Constants, uint TemporaryAddress, uint? VertexScratchAddress, uint? PacketAddress, uint? OrderingAddress, int TableBaseBucket, byte DepthShift, ImmutableArray<uint> ClipIndices)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleZantetsukenRenderStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary1D8, NativeBattleEffectArena VertexScratch, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleZantetsukenRendering
{
    public static NativeBattleZantetsukenRenderStep DrawBody(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary1D8, NativeBattleEffectArena vertexScratch, ImmutableArray<NativeBattleZantetsukenRenderObject> objects, NativeBattleZantetsukenRenderInputs inputs, uint objectMask, uint color)
    {
        return new Drawing(memory, temporary1D8, vertexScratch, inputs, false, color, 0).Draw(objects, objectMask);
    }

    public static NativeBattleZantetsukenRenderStep DrawStage(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary1D8, NativeBattleEffectArena vertexScratch, ImmutableArray<NativeBattleZantetsukenRenderObject> objects, NativeBattleZantetsukenRenderInputs inputs, uint color, short depth)
    {
        return new Drawing(memory, temporary1D8, vertexScratch, inputs, true, color, depth).Draw(objects, uint.MaxValue);
    }

    public static NativeBattleEffectArena PreparePlane(NativeBattleEffectArena temporary1D8, NativeBattleModuleAnchor anchor, NativeBattleVector normal, bool writeAnchorW = true)
    {
        short[] values = [anchor.X, anchor.Y, anchor.Z, anchor.W, normal.X, normal.Y, normal.Z, unchecked((short)-(unchecked(anchor.X * normal.X + anchor.Y * normal.Y + anchor.Z * normal.Z) >> 12))];
        for (int index = 0; index < 4; index++)
        {
            if (index != 3 || writeAnchorW)
            {
                temporary1D8 = Word(temporary1D8, 0x48 + index * 2, values[index]);
            }
            temporary1D8 = Word(temporary1D8, 0xA8 + index * 2, values[4 + index]);
        }
        return temporary1D8;
    }

    private sealed partial class Drawing
    {
        private NativeBattleEffectMemory memory;
        private NativeBattleEffectArena temporary;
        private NativeBattleEffectArena scratch;
        private readonly NativeBattleZantetsukenRenderInputs inputs;
        private readonly bool stage;
        private readonly uint color;
        private readonly short depth;
        private readonly ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        private int allocated;
        private readonly Dictionary<int, NativeBattleVector>? capturedPoints;
        private int capturedObject;
        private int capturedFace;
        private int capturedTriangle;
        private int capturedPart;
        private int capturedMatrixRole;

        public Drawing(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary, NativeBattleEffectArena scratch, NativeBattleZantetsukenRenderInputs inputs, bool stage, uint color, short depth)
        {
            if (inputs.DepthShift > 31 || inputs.ClipIndices.IsDefault || inputs.ClipIndices.Length != 56 || inputs.ClipIndices.Any(index => index > 5) || inputs.Bones.IsDefault)
            {
                throw new InvalidDataException("Zantetsuken drawing requires its original clipping table, bone publication and ordering operands.");
            }
            this.memory = memory;
            this.temporary = temporary;
            this.scratch = scratch;
            this.inputs = inputs;
            this.stage = stage;
            this.color = color;
            this.depth = depth;
            capturedPoints = inputs.PilotIdentity.HasValue ? new() : null;
        }

        public NativeBattleZantetsukenRenderStep Draw(ImmutableArray<NativeBattleZantetsukenRenderObject> objects, uint objectMask)
        {
            if (objects.IsDefault)
            {
                throw new InvalidDataException("Zantetsuken drawing requires the actual original object directory.");
            }
            temporary = Matrix(temporary, 0x198, inputs.Main);
            temporary = Matrix(temporary, 0x1B8, inputs.Alternate);
            temporary = Pointer(temporary, 4, inputs.VertexScratchAddress);
            temporary = Put(temporary, 0x10, objectMask);
            if (stage)
            {
                temporary = Put(temporary, 0x14, color);
                temporary = Word(temporary, 0x18, depth);
            }
            else
            {
                temporary = Put(temporary, 0x20, color);
            }
            memory = NativeBattleZantetsukenGte.LoadPlane(memory, temporary.Read(0xA8, 8));
            for (int index = 0; index < inputs.ClipIndices.Length; index++)
            {
                temporary = Put(temporary, 0xB8 + index * 4, checked(inputs.TemporaryAddress + 0x48 + inputs.ClipIndices[index] * 16));
            }
            if (stage)
            {
                NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Zantetsuken stage requires its current far-color owner.");
                NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Zantetsuken stage requires its retained far-color controls.");
                memory = memory with { PrimitiveProcess = process with { ModelRendererRegisters = far.PublishModelColor(color) } };
            }
            for (int index = 0; index < objects.Length; index++)
            {
                if ((objectMask & (1u << (index & 31))) == 0)
                {
                    continue;
                }
                capturedObject = index;
                NativeBattleZantetsukenRenderObject selected = objects[index];
                selected.Mesh.Validate();
                TransformVertices(selected.Mesh);
                int[] counts = new int[4];
                foreach (NativeBattleRawPolygon face in selected.Mesh.Polygons)
                {
                    int group = face.Bytes.Length == (face.Quad ? 20 : 16) ? face.Quad ? 1 : 0 : face.Quad ? 3 : 2;
                    counts[group]++;
                }
                for (int group = 0; group < 4; group++)
                {
                    temporary = Word(temporary, 8 + group * 2, unchecked((short)counts[group]));
                }
                temporary = Pointer(temporary, 0, selected.FaceAddress);
                LoadMatrix(inputs.Main);
                temporary = Pointer(temporary, 0x2C, inputs.OrderingAddress);
                temporary = Put(temporary, 0x30, inputs.DepthShift);
                temporary = Pointer(temporary, 0x34, inputs.PacketAddress is uint address ? unchecked(address + (uint)allocated) : null);
                int faceIndex = 0;
                foreach (NativeBattleRawPolygon face in selected.Mesh.Polygons)
                {
                    capturedFace = faceIndex++;
                    int expected = (face.Quad ? 20 : 16) + (stage ? 4 : 0);
                    if (face.Bytes.Length != expected)
                    {
                        continue;
                    }
                    ProcessFace(face);
                }
            }
            return new(memory, temporary, scratch, new(attempts.ToImmutable(), allocated));
        }

        private NativeBattleGeometryRegisterStep Registers => new(memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        private NativeBattleScriptedGraphicsMemory Controls => memory.ScriptedGraphics ?? throw new NotSupportedException("Zantetsuken drawing requires its actual projection and depth averaging controls.");
        private void Publish(NativeBattleGeometryRegisterStep step) => memory = memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors };

        private void LoadMatrix(NativeBattlePackedMatrix32 matrix, int capturedRole = 0)
        {
            short? padding = matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
            NativeBattleGeometryRegisterStep step = Registers;
            Publish(step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix.ReadMatrix(), true, true, padding) });
            capturedMatrixRole = capturedRole;
        }

        private void TransformVertices(NativeBattleRawMesh mesh)
        {
            ImmutableArray<NativeBattleRawVertexGroup> groups = mesh.PackedVertexGroups ?? throw new NotSupportedException("Zantetsuken requires its original packed six-byte vertex groups and adjacent read bytes.");
            int destination = 0;
            foreach (NativeBattleRawVertexGroup group in groups)
            {
                if (group.Bone >= inputs.Bones.Length || group.Count < 0 || group.Bytes.Length != checked(group.Count * 6 + 6))
                {
                    throw new InvalidDataException("Zantetsuken vertex group leaves its actual current bone or source owner.");
                }
                NativeBattleGeometryRegisterStep step = Registers;
                step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, inputs.Bones[group.Bone], true, true, inputs.ReadBonePadding(group.Bone)) };
                for (int index = 0; index < group.Count; index++)
                {
                    ReadOnlySpan<byte> bytes = group.Bytes.AsSpan().Slice(index * 6, 6);
                    NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
                    step = NativeBattleGeometry.LoadVector(step, 0, point, 0);
                    step = NativeBattleGeometry.Transform(step);
                    for (int lane = 0; lane < 3; lane++)
                    {
                        scratch = Word(scratch, destination + lane * 2, unchecked((short)step.Colors.Ir[lane]!.Value));
                    }
                    destination = checked(destination + 8);
                }
                Publish(step);
            }
        }

        private void ProcessFace(NativeBattleRawPolygon polygon)
        {
            byte[] face = polygon.Bytes.ToArray();
            int count = polygon.Quad ? 4 : 3;
            int[] indices = Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(index * 2, 2)) & 0xFFF).ToArray();
            ushort[] uvs = polygon.Quad
                ? [BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(8)), BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(12)), BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(16)), BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(18))]
                : [BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(8)), BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(12)), BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(6))];
            uint firstUv = BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(8));
            uint secondUv = BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12));
            if (stage)
            {
                temporary = Put(temporary, 0x20, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(polygon.Quad ? 20 : 16)) & 0x02FFFFFF);
            }
            temporary = Put(temporary, 0x24, firstUv);
            temporary = Put(temporary, 0x28, secondUv);
            capturedTriangle = 0;
            if (!Triangle([indices[0], indices[1], indices[2]], [uvs[0], uvs[1], uvs[2]], true))
            {
                return;
            }
            if (polygon.Quad)
            {
                capturedTriangle = 1;
                Triangle([indices[3], indices[1], indices[2]], [uvs[3], uvs[1], uvs[2]], false);
            }
        }

        private bool Triangle(int[] indices, ushort[] uv, bool initial)
        {
            capturedPart = 0;
            NativeBattleGeometryRegisterStep step = Registers;
            for (int index = 0; index < 3; index++)
            {
                temporary = Copy(scratch, checked(indices[index] * 8), temporary, 0x48 + index * 16, 8);
                step = LoadVertex(step, index, 0x48 + index * 16);
            }
            step = NativeBattleGeometry.ProjectTriple(step, inputs.Projection, Controls.Dqa, Controls.Dqb);
            uint flags = step.Colors.Flags!.Value;
            temporary = Put(temporary, 0x44, flags);
            for (int index = 0; index < 3; index++)
            {
                temporary = Word(temporary, 0x4E + index * 16, unchecked((short)uv[index]));
            }
            Publish(step);
            if (initial)
            {
                if ((flags & 0x60000) != 0)
                {
                    return false;
                }
                step = NativeBattleGeometry.Clip45EE10(step);
                temporary = Put(temporary, 0x38, step.Geometry.Mac0!.Value);
                Publish(step);
                if (stage && unchecked((int)step.Geometry.Mac0.Value) <= 0)
                {
                    return false;
                }
            }
            int[] distance = new int[3];
            for (int index = 0; index < 3; index++)
            {
                memory = NativeBattleZantetsukenGte.EvaluatePlane(memory, index);
                distance[index] = memory.ScriptedColorRegisters!.Ir[0] ?? throw new NotSupportedException("Zantetsuken plane distance requires its actually loaded first light row and back control.");
                temporary = Put(temporary, 0x80 + index * 16, 0);
                temporary = Put(temporary, 0x84 + index * 16, unchecked((uint)distance[index]));
            }
            int mask = 0;
            for (int index = 0; index < 3; index++)
            {
                if (distance[index] != 0 && (distance[index] ^ distance[(index + 1) % 3]) < 0)
                {
                    int bit = 1 << index;
                    temporary = Put(temporary, 0x80 + index * 16, (uint)bit);
                    mask |= bit;
                }
            }
            if (stage && initial)
            {
                NativeBattleModelRendererRegisters far = memory.PrimitiveProcess!.ModelRendererRegisters!;
                NativeBattleScriptedColorRegisters shaded = NativeBattleScriptedDpcs.Transform45F270(memory.ScriptedColorRegisters!, Read(temporary, 0x20), unchecked((uint)(int)depth), far, inputs.Constants).Registers;
                memory = memory with { ScriptedColorRegisters = shaded };
                temporary = Put(temporary, 0x20, shaded.Rgb[2] ?? throw new NotSupportedException("Zantetsuken stage color requires its actual source material code."));
            }
            temporary = Put(temporary, 0x1C, (uint)mask);
            if (mask != 0)
            {
                Clip(mask);
            }
            else if ((distance[0] | distance[1] | distance[2]) > 0)
            {
                LoadMatrix(inputs.Alternate, 1);
                Emit([0x48, 0x58, 0x68], unchecked((int)Read(temporary, 0x38)) <= 0, true);
                LoadMatrix(inputs.Main);
            }
            else if (unchecked((int)Read(temporary, 0x38)) > 0)
            {
                Emit([0x48, 0x58, 0x68], false, false);
            }
            return true;
        }
    }

    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, short value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static uint Read(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short Short(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static NativeBattleEffectArena Pointer(NativeBattleEffectArena arena, int offset, uint? value) => value is uint address ? Put(arena, offset, address) : arena.MarkUnknown(offset, 4);
    private static NativeBattleEffectArena Copy(NativeBattleEffectArena source, int sourceOffset, NativeBattleEffectArena destination, int destinationOffset, int length) => NativeBattleEffectNodeContextMemory.Copy(source, sourceOffset, destination, destinationOffset, length);
    private static NativeBattleEffectArena Matrix(NativeBattleEffectArena arena, int offset, NativeBattlePackedMatrix32 matrix)
    {
        matrix.Validate();
        arena = arena.Write(offset, matrix.Bytes.AsSpan());
        for (int index = 0; index < 32; index++)
        {
            if (!matrix.Known[index])
            {
                arena = arena.MarkUnknown(offset + index, 1);
            }
        }
        return arena;
    }
}
