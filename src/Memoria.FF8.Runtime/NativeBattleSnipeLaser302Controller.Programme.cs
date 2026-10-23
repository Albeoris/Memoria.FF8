using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSnipeLaser302Controller
{
    public static (NativeBattleSnipeLaser302Frame Frame, int Result) AdvanceProgramme66D670(NativeBattleSnipeLaser302Frame frame, uint address, NativeBattleSnipeLaser302Resources resources, NativeBattleSnipeLaser302Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix local = new(Enumerable.Range(0, 9).Select(index => Signed(task, 12 + index * 2)).ToImmutableArray(), unchecked((int)task.ReadDword(32)), unchecked((int)task.ReadDword(36)), unchecked((int)task.ReadDword(40)), 4096);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        (frame, int remaining) = PlayProgramme302(frame, address, 44, 124, resources.ProgrammeD, ProgrammeCaller302(matrix, 0), resources, inputs);
        return (frame, remaining == 0 ? 2 : 0);
    }

    private static (NativeBattleSnipeLaser302Frame Frame, int Remaining) PlayProgramme302(NativeBattleSnipeLaser302Frame frame, uint address, int workspaceOffset, int workspaceBytes, NativeBattleGfAnimationSource source, NativeBattleEffectArena caller40, NativeBattleSnipeLaser302Resources resources, NativeBattleSnipeLaser302Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Workspace()
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            task.RequireKnownPayload(workspaceOffset, workspaceBytes);
            return new(task.Payload.AsSpan().Slice(workspaceOffset - 12, workspaceBytes).ToArray().ToImmutableArray());
        }
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(source, 0, (current, node) =>
        {
            frame = WriteBytes(frame, address + (uint)workspaceOffset, NativeBattleEffectArena.Clear().Write(0, current.Data.AsSpan()), workspaceBytes);
            frame = DrawProgrammeNode66D3C0(frame, source, node, caller40, resources, inputs);
            return Workspace();
        });
        frame = WriteBytes(frame, address + (uint)workspaceOffset, NativeBattleEffectArena.Clear().Write(0, advanced.Workspace.Data.AsSpan()), workspaceBytes);
        return (frame, advanced.Remaining);
    }

    private static NativeBattleEffectArena ProgrammeCaller302(NativeBattleActionMatrix matrix, int depth) => PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(32, BitConverter.GetBytes(depth)).Write(36, BitConverter.GetBytes(NativeBattleSnipeLaser302Profile.ArenaAddress + 0x1588));

    public static NativeBattleSnipeLaser302Frame DrawProgrammeNode66D3C0(NativeBattleSnipeLaser302Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller40, NativeBattleSnipeLaser302Resources resources, NativeBattleSnipeLaser302Inputs inputs)
    {
        NativeBattleVector scale = node.Vector24;
        if (scale is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return frame;
        }
        int geometryOffset = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
        uint geometryAddress = checked(source.Address + (uint)geometryOffset);
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(geometryAddress);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
        int first = node.Word40;
        int second = node.Word42;
        int ratio = node.Scalar38;
        if (first < 0 || second < 0 || first >= shapes || second >= shapes)
        {
            throw new InvalidDataException("Snipe Laser programme shape leaves its original geometry.");
        }
        (frame, uint temporary) = Allocate(frame, 88);
        uint vertices;
        if (first == second || ratio == 0 || ratio == 4096)
        {
            int selected = ratio == 4096 && first != second ? second : first;
            vertices = checked(geometryAddress + 12 + (uint)(selected * count * 8));
        }
        else
        {
            frame = MorphProgramme302(frame, mesh, first, second, ratio, BinaryPrimitives.ReadUInt32LittleEndian(caller40.Read(36, 4)));
            vertices = BinaryPrimitives.ReadUInt32LittleEndian(caller40.Read(36, 4));
        }
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        (byte[] callerData, bool[] callerKnown) = caller40.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 caller = new(callerData.ToImmutableArray(), callerKnown.ToImmutableArray());
        NativeBattleActionMatrix callerMatrix = caller.ReadMatrix();
        int x = node.Vector8.X;
        int y = node.Vector8.Y;
        int z = node.Vector8.Z;
        if ((node.Header & 0x200) == 0)
        {
            frame = LoadMatrix(frame, caller, true, false);
            NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Snipe Laser node requires its current arithmetic owner."));
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, node.Vector8, null), false);
            x = registers.Colors.Mac[0]!.Value;
            y = registers.Colors.Mac[1]!.Value;
            z = registers.Colors.Mac[2]!.Value;
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
            if ((node.Header & 0x8000) == 0)
            {
                rotation = NativeBattleGfMatrices.Multiply56C090(callerMatrix, rotation);
            }
        }
        rotation = rotation with { X = unchecked(callerMatrix.X + x), Y = unchecked(callerMatrix.Y + y), Z = unchecked(callerMatrix.Z + z) };
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if ((node.Header & 0x100) != 0)
            {
                NativeBattleActionMatrix scaled = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096));
                rotation = rotation with { Rotation = scaled.Rotation };
            }
            else
            {
                rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
            }
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        int depthBias = BinaryPrimitives.ReadInt32LittleEndian(caller40.Read(32, 4));
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(geometryAddress)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(24, BitConverter.GetBytes(depthBias)).Write(28, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleSnipeLaser302DrawMesh7043B0(temporary, geometryAddress, vertices));
        return Release(frame, 88);
    }

    private static NativeBattleSnipeLaser302Frame MorphProgramme302(NativeBattleSnipeLaser302Frame frame, NativeBattleStaticMeshResource mesh, int first, int second, int ratio, uint destination)
    {
        if (destination != NativeBattleSnipeLaser302Profile.ArenaAddress + 0x1588)
        {
            throw new InvalidDataException("Snipe Laser morph requires its current programme scratch.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        if (count < 1 || count * 8 > 392)
        {
            throw new InvalidDataException("Snipe Laser morph leaves its actual current scratch extent.");
        }
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Snipe Laser morph requires its geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Snipe Laser morph requires its arithmetic owner."));
        for (int vertex = 0; vertex < count; vertex++)
        {
            int a = 12 + (first * count + vertex) * 8;
            int b = 12 + (second * count + vertex) * 8;
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - ratio)) } }, ShapeWord(a), ShapeWord(a + 2), ShapeWord(a + 4));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, NativeBattleGteColorInterpolation.CodeConstants45F270);
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)ratio) } }, ShapeWord(b), ShapeWord(b + 2), ShapeWord(b + 4));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, NativeBattleGteColorInterpolation.CodeConstants45F270);
            NativeBattleEffectArena result = NativeBattleEffectArena.Clear();
            for (int axis = 0; axis < 3; axis++)
            {
                result = result.Write(axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
            }
            frame = WriteBytes(frame, checked(destination + (uint)(vertex * 8)), result, 6);
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };

        ushort ShapeWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset));
    }

}
