using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAquaRendering
{
    public static NativeBattleAquaFrame DrawWave(NativeBattleAquaFrame frame, NativeBattleModulePoolTask task, NativeBattleAquaResources resources, NativeBattleAquaInputs inputs)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return frame;
        }
        if (task.ReadDword(0x4C) != frame.Profile.MeshAddress || resources.Effect != frame.Profile.Effect)
        {
            throw new InvalidDataException("The Aqua wave draw requires its retained physical mesh pointer.");
        }
        NativeBattleAquaFrame current = frame;
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.Identity8DD770();
        Rotate('Y', Word(task, 0x46));
        Rotate('X', Word(task, 0x44));
        Rotate('Z', Word(task, 0x48));
        NativeBattleActionMatrix matrix = ComposeWave(task, inputs.Camera);
        Emit(new NativeBattleAquaMatrix(matrix, true, true));
        Emit(new NativeBattleAquaTemporaryBegin(104));
        PatchDword(0, task.ReadDword(0x4C));
        PatchDword(8, task.ReadDword(0x40));
        PatchDword(12, unchecked((uint)Word(task, 0x50)));
        PatchWord(0x18, task.ReadWord(0x5E));
        PatchWord(0x1A, 0);
        PatchWord(0x1E, 0);
        PatchWord(0x1C, 0);
        PatchWord(0x24, 0);
        PatchWord(0x26, 0);
        PatchWord(0x22, 256);
        PatchWord(0x20, 256);
        PatchWord(0x2A, 256);
        PatchDword(0x10, unchecked((uint)Word(task, 0x52)));
        PatchDword(0x14, 0xF0);
        PatchWord(0x28, 128);
        PatchDword(4, checked(frame.Profile.MeshAddress + 8));
        ImmutableArray<byte> mesh = current.State.WaveMesh ?? throw new NotSupportedException("The Aqua wave has no retained current mesh bytes.");
        int directory = BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan());
        PatchDword(0x2C, checked(frame.Profile.MeshAddress + (uint)directory));
        Emit(new NativeBattleAquaFarColor(task.ReadDword(0x40)));
        NativeBattleEsunaMeshContext context = WaveContext(task);
        int[] cursors = new int[8];
        int cursor = directory;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan().Slice(cursor));
            int stride = group switch { 0 or 1 => 12, 2 or 4 => 20, 3 or 5 => 24, 6 => 28, _ => 36 };
            cursors[group] = checked(cursor + 4);
            cursor = checked(cursor + 4 + count * stride);
        }
        NativeBattleGfGeometryBatch batch = NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, inputs.Projection, context.Flags, null, Face, pilotIdentity: frame.Profile.Effect is 55 or 72 ? NativeBattlePilotVisualLifetimes.Identity(frame.Profile.Effect, task) : null);
        PatchDword(0x2C, checked(frame.Profile.MeshAddress + (uint)cursor));
        Emit(new NativeBattleAquaPackets(batch));
        Emit(new NativeBattleAquaTemporaryEnd(104));
        return current;

        void Emit(NativeBattleAquaOperation operation) => current = inputs.Execute(current, operation);
        void PatchDword(int offset, uint value) => Emit(DwordPatch(offset, value));
        void PatchWord(int offset, ushort value) => Emit(WordPatch(offset, value));
        void Rotate(char axis, short angle)
        {
            Emit(new NativeBattleAquaRotate(rotation, axis, angle));
            rotation = axis switch
            {
                'X' => NativeBattleCuragaMatrices.RotateX8DD7E0(rotation, angle),
                'Y' => NativeBattleCuragaMatrices.RotateY8DD8A0(rotation, angle),
                _ => NativeBattleCuragaMatrices.RotateZ8DD960(rotation, angle)
            };
        }
        NativeBattleGfGeometryBatch Face(int group, ReadOnlySpan<byte> source, Func<int, NativeBattleModuleProjectedVertex> project, int byteOffset)
        {
            if (group is not (3 or 7))
            {
                throw new NotSupportedException("The Aqua wave source reached a polygon group absent from its authenticated mesh.");
            }
            byte[] polygon = source.ToArray();
            PatchDword(0x2C, checked(frame.Profile.MeshAddress + (uint)cursors[group]));
            NativeBattleVector[] positions = new NativeBattleVector[4];
            NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[4];
            short?[] ws = new short?[4];
            uint flags = 0;
            NativeBattleModuleProjectedVertex Corner(int corner)
            {
                ushort index = BinaryPrimitives.ReadUInt16LittleEndian(polygon.AsSpan(4 + corner * 2));
                int offset = checked(8 + index * 4);
                ReadOnlySpan<byte> point = mesh.AsSpan().Slice(offset, 8);
                ws[corner] = BinaryPrimitives.ReadInt16LittleEndian(point[6..]);
                positions[corner] = new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point[2..]), BinaryPrimitives.ReadInt16LittleEndian(point[4..]));
                projected[corner] = project(corner);
                if (corner < 3)
                {
                    flags |= projected[corner].Flags;
                    if (corner == 2)
                    {
                        Emit(new NativeBattleAquaProjection(matrix, inputs.Projection, positions.Take(3).ToImmutableArray(), false) { Ws = ws.Take(3).ToImmutableArray() });
                        Emit(new NativeBattleAquaTemporaryFlags(0x3C));
                        if ((flags & 0x60000) == 0)
                        {
                            ImmutableArray<uint> screens = projected.Take(3).Select(value => value.Screen).ToImmutableArray();
                            Emit(new NativeBattleAquaClip(screens));
                            PatchDword(0x30, unchecked((uint)NativeBattleModulePackets.Nclip(screens[0], screens[1], screens[2])));
                        }
                    }
                }
                else
                {
                    Emit(new NativeBattleAquaProjection(matrix, inputs.Projection, [positions[3]], true) { Ws = [ws[3]] });
                    Emit(new NativeBattleAquaAverageDepth(projected.Select(value => value.Depth).ToImmutableArray()));
                }
                return projected[corner];
            }
            NativeBattleGfGeometryBatch emitted = group == 3 ? NativeBattleRestorationMeshPackets.EmitFt4_88ECA0(polygon, Corner, context, inputs.Colors, byteOffset) : NativeBattleRestorationMeshPackets.EmitGt4_88FC50(polygon, Corner, context, inputs.Colors, byteOffset);
            if (emitted.Attempts.Any(attempt => attempt.Primitive is not null))
            {
                uint first = BinaryPrimitives.ReadUInt32LittleEndian(polygon);
                ImmutableArray<uint> materials = group == 3 ? [first] : [first, BinaryPrimitives.ReadUInt32LittleEndian(polygon.AsSpan(24)), BinaryPrimitives.ReadUInt32LittleEndian(polygon.AsSpan(28)), BinaryPrimitives.ReadUInt32LittleEndian(polygon.AsSpan(32))];
                int average = projected.Sum(value => (int)value.Depth) >> 4;
                PatchDword(0x38, unchecked((uint)average));
                Emit(new NativeBattleAquaColor(materials, context.Fade!.Value, context.FarColor!.Value, group == 7));
                PatchDword(0x38, unchecked((uint)Math.Max(0, average + context.DepthBias)));
            }
            cursors[group] = checked(cursors[group] + polygon.Length);
            PatchDword(0x2C, checked(frame.Profile.MeshAddress + (uint)cursors[group]));
            return emitted;
        }
    }

    public static NativeBattleAquaFrame DrawSprite(NativeBattleAquaFrame frame, NativeBattleModulePoolTask task, NativeBattleAquaResources resources, NativeBattleAquaInputs inputs)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return frame;
        }
        if (resources.Effect != frame.Profile.Effect)
        {
            throw new InvalidDataException("The Aqua sprite resource belongs to another module.");
        }
        NativeBattleAquaSpriteComposition composed = ComposeSprite(task, resources, inputs.Camera, inputs.Projection, inputs.CameraRoll, frame.Profile.Effect is 55 or 72 ? NativeBattlePilotVisualLifetimes.Identity(frame.Profile.Effect, task) with { ResourceGeneration = task.ReadDword(0x4C) } : null);
        NativeBattleAquaFrame current = inputs.Execute(frame, new NativeBattleAquaTemporaryBegin(216));
        Matrix(0xB8, composed.Billboard);
        ImmutableArray<short> cameraRotation = inputs.Camera.Rotation;
        NativeBattleActionMatrix transpose = inputs.Camera with { Rotation = [cameraRotation[0], cameraRotation[3], cameraRotation[6], cameraRotation[1], cameraRotation[4], cameraRotation[7], cameraRotation[2], cameraRotation[5], cameraRotation[8]] };
        Emit(new NativeBattleAquaMatrixProduct(inputs.Camera, transpose) { Padding18 = CameraPadding(current.Memory) });
        Emit(new NativeBattleAquaLongTransform(inputs.Camera, new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20))));
        Emit(new NativeBattleAquaMatrix(composed.Billboard, true, true) { TemporaryMatrixOffset = 0xB8 });
        PatchDword(0, task.ReadDword(0x4C));
        PatchWord(4, task.ReadWord(0x50));
        PatchWord(0x24, 0);
        PatchWord(0xB4, task.ReadWord(0x56));
        PatchWord(0x26, BinaryPrimitives.ReadUInt16LittleEndian(composed.Resource.AsSpan().Slice(8)));
        PatchWord(0x28, BinaryPrimitives.ReadUInt16LittleEndian(composed.Resource.AsSpan().Slice(12 + composed.Phase * 2)));
        PatchDword(0x30, 1);
        PatchDword(0x2C, checked(task.ReadDword(0x4C) + composed.FrameOffset + 4u));
        foreach (int offset in new[] { 0x60, 0x58, 0x50, 0x48 })
        {
            PatchWord(offset, 0);
        }
        Matrix(0x84, composed.Main);
        Emit(new NativeBattleAquaTemporaryCopy(0xCA, 0x96, 2));
        PatchDword(0x1C, 0x808080);
        Emit(new NativeBattleAquaTemporaryPatch(0x20, composed.Resource.AsSpan().Slice(4, 4).ToArray().ToImmutableArray()));
        PatchDword(0xB0, 0);
        PatchDword(0xAC, 0);
        PatchDword(0xA4, 4096);
        PatchDword(0xA8, 0);
        PatchDword(0x3C, uint.MaxValue);
        for (int index = 0; index < 4; index++)
        {
            int offset = 0x44 + index * 8;
            PatchWord(offset, unchecked((ushort)composed.Corners[index].X));
            PatchWord(offset + 2, unchecked((ushort)composed.Corners[index].Y));
        }
        Matrix(0x64, composed.Quad);
        ReadOnlySpan<byte> sourceQuad = composed.Resource.AsSpan().Slice(composed.FrameOffset + 4, 20);
        bool scaled = (BinaryPrimitives.ReadUInt16LittleEndian(sourceQuad[14..]) & 0xC00) != 0;
        short scaleX = scaled ? BinaryPrimitives.ReadInt16LittleEndian(sourceQuad[16..]) : (short)4096;
        short scaleY = scaled ? BinaryPrimitives.ReadInt16LittleEndian(sourceQuad[18..]) : (short)4096;
        NativeBattleActionMatrix local = new([scaleX, 0, 0, 0, scaleY, 0, 0, 0, 4096], 0, 0, 0, 4096);
        Emit(new NativeBattleAquaMatrixProduct(composed.Main, local) { TemporaryLeftMatrixOffset = 0x84 });
        NativeBattleVector localPosition = new(unchecked((short)(8 * sourceQuad[0] + 16 * BinaryPrimitives.ReadInt16LittleEndian(sourceQuad[8..]))), unchecked((short)(8 * sourceQuad[2] + 16 * BinaryPrimitives.ReadInt16LittleEndian(sourceQuad[10..]))), 0);
        Emit(new NativeBattleAquaLongTransform(composed.Main, localPosition) { TemporaryMatrixOffset = 0x84 });
        Emit(new NativeBattleAquaMatrix(composed.Quad, true, true) { TemporaryMatrixOffset = 0x64 });
        NativeBattleModuleProjectedVertex[] projected = composed.Corners.Select(point => NativeBattleModuleProjection.Project45FE10(composed.Quad, point, inputs.Projection)).ToArray();
        Emit(new NativeBattleAquaProjection(composed.Quad, inputs.Projection, composed.Corners.Take(3).ToImmutableArray(), false) { TemporaryVectorOffset = 0x44 });
        uint flags = projected[0].Flags | projected[1].Flags | projected[2].Flags;
        Emit(new NativeBattleAquaTemporaryFlags(0x40));
        if ((flags & 0x60000) == 0)
        {
            Emit(new NativeBattleAquaProjection(composed.Quad, inputs.Projection, [composed.Corners[3]], true) { TemporaryVectorOffset = 0x5C });
            Emit(new NativeBattleAquaAverageDepth(projected.Select(value => value.Depth).ToImmutableArray()));
            int average = projected.Sum(value => (int)value.Depth) >> 4;
            PatchDword(0x3C, unchecked((uint)Math.Max(0, average + Word(task, 0x56))));
        }
        Emit(new NativeBattleAquaPackets(composed.Batch));
        Emit(new NativeBattleAquaTemporaryEnd(216));
        return current;

        void Emit(NativeBattleAquaOperation operation) => current = inputs.Execute(current, operation);
        void PatchDword(int offset, uint value) => Emit(DwordPatch(offset, value));
        void PatchWord(int offset, ushort value) => Emit(WordPatch(offset, value));
        void Matrix(int offset, NativeBattleActionMatrix matrix)
        {
            byte[] rotation = new byte[18];
            for (int lane = 0; lane < 9; lane++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(lane * 2), matrix.Rotation[lane]);
            }
            Emit(new NativeBattleAquaTemporaryPatch(offset, rotation.ToImmutableArray()));
            PatchDword(offset + 20, unchecked((uint)matrix.X));
            PatchDword(offset + 24, unchecked((uint)matrix.Y));
            PatchDword(offset + 28, unchecked((uint)matrix.Z));
        }
    }

    private static ushort? CameraPadding(NativeBattleEffectMemory memory)
    {
        NativeBattlePackedMatrix32 copied = memory.SharedEffectView2793E58 ?? throw new NotSupportedException("The Aqua drawing requires its actual shared camera copy.");
        return copied.Known[18] && copied.Known[19] ? BinaryPrimitives.ReadUInt16LittleEndian(copied.Bytes.AsSpan().Slice(18)) : null;
    }

    private static NativeBattleAquaTemporaryPatch WordPatch(int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return new(offset, bytes.ToImmutableArray());
    }

    private static NativeBattleAquaTemporaryPatch DwordPatch(int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return new(offset, bytes.ToImmutableArray());
    }
}
