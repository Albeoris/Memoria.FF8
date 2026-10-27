using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMegaSpark279Controller
{
    public static (NativeBattleMegaSpark279Frame Frame, int Remaining) DrawProgramme(NativeBattleMegaSpark279Frame frame, uint address, int offset, int bytes, NativeBattleGfAnimationSource source, NativeBattleEffectArena caller40, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Workspace()
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            task.RequireKnownPayload(offset, bytes);
            return new(task.ReadRawPayloadSpan().Slice(offset - 12, bytes).ToArray().ToImmutableArray());
        }
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(source, 0, (current, node) =>
        {
            frame = WriteBytes(frame, address + (uint)offset, NativeBattleEffectArena.Clear().Write(0, current.Data.AsSpan()), bytes);
            frame = DrawProgrammeNode67F7E0(frame, source, node, caller40, resources, inputs);
            return Workspace();
        });
        frame = WriteBytes(frame, address + (uint)offset, NativeBattleEffectArena.Clear().Write(0, advanced.Workspace.Data.AsSpan()), bytes);
        return (frame, advanced.Remaining);
    }

    public static (NativeBattleMegaSpark279Frame Frame, int Result) AdvanceProgramme680170(NativeBattleMegaSpark279Frame frame, uint address, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix local = Packed279(TaskView(task, 12, 32)).ReadMatrix();
        NativeBattleActionMatrix combined = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(combined, null)).Write(32, BitConverter.GetBytes(-128)).Write(36, BitConverter.GetBytes(0x20DFAB8u + 0x7A6C));
        NativeBattleGfAnimationSource source = resources.ReadProgramme(task.ReadDword(44));
        (frame, int remaining) = DrawProgramme(frame, address, 44, source.Address == resources.ProgrammeC.Address ? 136 : 272, source, caller, resources, inputs);
        return (frame, remaining == 0 ? 2 : 0);
    }

    public static (NativeBattleMegaSpark279Frame Frame, int Result) AdvanceSprite6800F0(NativeBattleMegaSpark279Frame frame, uint address, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Signed(task, 12), Signed(task, 14), Signed(task, 16)), Signed(task, 18), 2048, -512) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(resources.Sprite.Address)).Write(4, BitConverter.GetBytes(task.ReadWord(18))).Write(36, BitConverter.GetBytes((ushort)0));
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleMegaSpark279DrawSprite180(temporary, resources.Sprite.Address));
        frame = Release(frame, 180);
        task = Read(frame, address);
        short phase = unchecked((short)(task.ReadWord(18) + 1));
        return (Write(frame, address, task.WriteWord(18, unchecked((ushort)phase))), phase >= 8 ? 2 : 0);
    }

    private static NativeBattlePackedMatrix32 Packed279(NativeBattleEffectArena view)
    {
        (byte[] bytes, bool[] known) = view.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    private static NativeBattleGeometryRegisterStep Registers279(NativeBattleMegaSpark279Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Mega Spark requires its current geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Mega Spark requires its current arithmetic owner."));
    private static NativeBattleMegaSpark279Frame Publish279(NativeBattleMegaSpark279Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    public static NativeBattleMegaSpark279Frame DrawProgrammeNode67F7E0(NativeBattleMegaSpark279Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller40, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
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
            throw new InvalidDataException("Mega Spark programme shape leaves its original geometry.");
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
            frame = MorphProgramme279(frame, mesh, first, second, ratio, BinaryPrimitives.ReadUInt32LittleEndian(caller40.Read(36, 4)));
            vertices = BinaryPrimitives.ReadUInt32LittleEndian(caller40.Read(36, 4));
        }
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        (byte[] callerBytes, bool[] callerKnown) = caller40.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 caller = new(callerBytes.ToImmutableArray(), callerKnown.ToImmutableArray());
        NativeBattleActionMatrix parent = caller.ReadMatrix();
        int x = node.Vector8.X;
        int y = node.Vector8.Y;
        int z = node.Vector8.Z;
        if ((node.Header & 0x200) == 0)
        {
            frame = LoadMatrix(frame, caller, true, false);
            NativeBattleGeometryRegisterStep registers = Registers279(frame);
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, node.Vector8, null), false);
            x = registers.Colors.Mac[0]!.Value;
            y = registers.Colors.Mac[1]!.Value;
            z = registers.Colors.Mac[2]!.Value;
            frame = Publish279(frame, registers);
            rotation = NativeBattleGfMatrices.Multiply56C090(parent, rotation);
        }
        rotation = rotation with { X = unchecked(parent.X + x), Y = unchecked(parent.Y + y), Z = unchecked(parent.Z + z) };
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
        frame = inputs.Execute(frame, new NativeBattleMegaSpark279DrawMesh7043B0(temporary, geometryAddress, vertices));
        return Release(frame, 88);
    }

    private static NativeBattleMegaSpark279Frame MorphProgramme279(NativeBattleMegaSpark279Frame frame, NativeBattleStaticMeshResource mesh, int first, int second, int ratio, uint destination)
    {
        if (destination != NativeBattleMegaSpark279Profile.ArenaAddress + 0x7A6C)
        {
            throw new InvalidDataException("Mega Spark morph requires its current programme scratch.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        if (count < 1 || count * 8 > 512)
        {
            throw new InvalidDataException("Mega Spark morph leaves its actual current scratch extent.");
        }
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Mega Spark morph requires its geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Mega Spark morph requires its arithmetic owner."));
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
