using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect9NodeAnchor(NativeBattleEffect9Frame Frame, NativeBattleVector Point);

public sealed record NativeBattleEffect9NodeInputs(Func<uint, NativeBattleGfAnimationSource> ReadSource, Func<NativeBattleEffect9Frame, int, NativeBattleEffect9NodeAnchor> ReadTargetF1, NativeBattlePackedMatrix32 View, NativeBattleCameraProjection Projection, NativeBattleSummonColors Colors, Func<NativeBattleEffect9Frame, NativeBattleGfGeometryBatch, NativeBattleEffect9Frame> Append);

public static class NativeBattleEffect9Nodes
{
    public static NativeBattleEffect9Step Advance(NativeBattleEffect9Frame frame, uint address, NativeBattleEffect9NodeInputs inputs)
    {
        NativeBattleModulePoolTask task = NativeBattleEffect9Controller.Read(frame, address);
        bool secondary = task.Callback == 0x8C3C30;
        if (task.Callback is not (0x8C0BC0 or 0x8C3C30))
        {
            throw new InvalidDataException("Effect9 Gf task requires its original callback.");
        }
        NativeBattleGfAnimationSource source = inputs.ReadSource(task.ReadDword(0x74));
        if (source.Address != (secondary ? 0x1817EE8u : 0x18155F0u) || task.ReadDword(0x78) != (secondary ? 128u : 840u))
        {
            throw new InvalidDataException("Effect9 Gf task requires its actual source and workspace clear extent.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 0x94, 840);
                workspace = workspace.Initialize7016B0(source, unchecked((int)task.ReadDword(0x78)));
                task = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, 0x94, workspace);
                if (secondary)
                {
                    task = task.WriteDword(0x1C, task.ReadDword(0x3DC)).WriteDword(0x20, task.ReadDword(0x3E0));
                }
                else
                {
                    NativeBattleEffect9NodeAnchor anchor = inputs.ReadTargetF1(frame, task.ReadByte(0x2D));
                    frame = anchor.Frame;
                    NativeBattleVector point = anchor.Point;
                    task = task.WriteWord(0x1C, unchecked((ushort)point.X)).WriteWord(0x1E, unchecked((ushort)point.Y)).WriteWord(0x20, unchecked((ushort)point.Z));
                }
                task = task.WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x88, unchecked((ushort)-512));
                frame = NativeBattleEffect9Controller.Write(frame, address, task);
                frame = Draw(frame, address, source, inputs).Frame;
                frame = NativeBattleEffect9Controller.NextPhase(frame, address);
                break;
            case 1:
                if (secondary)
                {
                    task = task.WriteDword(0x1C, frame.State.ReadDword(0x27571B8)).WriteDword(0x20, frame.State.ReadDword(0x27571BC));
                    frame = NativeBattleEffect9Controller.Write(frame, address, task);
                }
                NativeBattleEffect9Step drawn = Draw(frame, address, source, inputs);
                frame = drawn.Frame;
                if (drawn.Result == 0)
                {
                    task = NativeBattleEffect9Controller.Read(frame, address);
                    frame = NativeBattleEffect9Controller.Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    frame = NativeBattleEffect9Controller.NextPhase(frame, address);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect9 Gf signed phase exceeds its three handlers.");
        }
        return NativeBattleEffect9Controller.Finish(frame, address);
    }

    private static NativeBattleEffect9Step Draw(NativeBattleEffect9Frame frame, uint address, NativeBattleGfAnimationSource source, NativeBattleEffect9NodeInputs inputs)
    {
        NativeBattleModulePoolTask task = NativeBattleEffect9Controller.Read(frame, address);
        NativeBattleActionMatrix world = NativeBattleCuragaMatrices.Identity8DD770();
        short yaw = unchecked((short)task.ReadWord(0x62));
        short pitch = unchecked((short)task.ReadWord(0x60));
        short roll = unchecked((short)task.ReadWord(0x64));
        if (yaw != 0)
        {
            world = NativeBattleCuragaMatrices.RotateY8DD8A0(world, yaw);
        }
        if (pitch != 0)
        {
            world = NativeBattleCuragaMatrices.RotateX8DD7E0(world, pitch);
        }
        if (roll != 0)
        {
            world = NativeBattleCuragaMatrices.RotateZ8DD960(world, roll);
        }
        world = NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)task.ReadDword(0x50)), unchecked((int)task.ReadDword(0x54)), unchecked((int)task.ReadDword(0x58)));
        world = world with { X = unchecked((short)task.ReadWord(0x1C)), Y = unchecked((short)task.ReadWord(0x1E)), Z = unchecked((short)task.ReadWord(0x20)) };
        NativeBattleActionMatrix context = NativeBattleGfMatrices.Compose56C2F0(inputs.View.ReadMatrix(), world);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 0x94, 840);
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (currentWorkspace, node) =>
        {
            frame = Emit(frame, task, source, node, context, inputs);
            return currentWorkspace;
        });
        task = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(NativeBattleEffect9Controller.Read(frame, address), 0x94, advanced.Workspace);
        frame = NativeBattleEffect9Controller.Write(frame, address, task);
        return new(frame, advanced.Remaining);
    }

    public static NativeBattleActionMatrix PrepareMatrix(NativeBattleGfAnimationNode node, NativeBattleActionMatrix parent)
    {
        NativeBattleActionMatrix matrix = (node.Header & 0x400) != 0 ? NativeBattleGfMatrices.Euler701430(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        (int x, int y, int z) = (node.Header & 0x200) != 0 ? (node.Vector8.X, node.Vector8.Y, node.Vector8.Z) : NativeBattleGfMatrices.Rotate56C3D0(parent, node.Vector8);
        if ((node.Header & 0x200) == 0)
        {
            matrix = NativeBattleGfMatrices.Multiply56C090(parent, matrix);
        }
        matrix = matrix with { X = unchecked(x + parent.X), Y = unchecked(y + parent.Y), Z = unchecked(z + parent.Z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = (node.Header & 0x100) != 0
                ? NativeBattleGfMatrices.Multiply56C090(matrix, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096))
                : NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        return matrix;
    }

    private static NativeBattleEffect9Frame Emit(NativeBattleEffect9Frame frame, NativeBattleModulePoolTask task, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleActionMatrix parent, NativeBattleEffect9NodeInputs inputs)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return frame;
        }
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometry + 4));
        int first = node.Word40;
        int second = node.Word42;
        int ratio = node.Scalar38;
        ImmutableArray<byte> vertices;
        if (first == second || ratio is 0 or 4096)
        {
            int selected = first == second || ratio == 0 ? first : second;
            vertices = source.Data.Slice(checked(geometry + 12 + selected * count * 8), checked(count * 8));
        }
        else
        {
            var morphed = NativeBattleGfMorphGeometry.MorphVertices701390(source.Data, geometry, first, second, ratio);
            for (int index = 0; index < morphed.Length; index++)
            {
                byte[] bytes = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(bytes, morphed[index].X);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), morphed[index].Y);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), morphed[index].Z);
                frame = frame with { State = frame.State.Write(0x2758178u + (uint)(index * 8), bytes) };
            }
            vertices = frame.State.Read(0x2758178, checked(count * 8)).ToImmutableArray();
        }
        NativeBattleActionMatrix matrix = PrepareMatrix(node, parent);
        bool cue = node.Scalar36 != 0;
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        if (cue)
        {
            flags |= 0xC0;
        }
        NativeBattleGfGeometryContext context = new(flags, cue ? node.Color32 : 0, node.Scalar36, null, null, unchecked((short)task.ReadWord(0x88)), 17, 2);
        NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(source.Data, geometry, vertices, matrix, inputs.Projection, context, inputs.Colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(9, task, node.NodeIndex) with { ResourceGeneration = source.Address });
        return inputs.Append(frame, batch);
    }
}
