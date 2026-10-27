using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect338Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect338Controller
{
    public static (NativeBattleEffect338Frame Frame, int Result) AdvanceProgramme(NativeBattleEffect338Frame frame, uint address, NativeBattleEffect338Resources resources, NativeBattleEffect338Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint pointer = task.ReadDword(20);
        NativeBattleGfAnimationSource source = resources.Programme(pointer);
        int bytes = pointer == 0x13755B8 ? 0xF8 : 0x184;
        NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(Signed(task, 18)) with { X = Signed(task, 12), Y = Signed(task, 14), Z = Signed(task, 16) };
        NativeBattleActionMatrix current = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(current, 0)).Write(32, BitConverter.GetBytes(-128)).Write(36, BitConverter.GetBytes(ArenaAddress + ScratchOffset));
        NativeBattleGfAnimationWorkspace Workspace()
        {
            NativeBattleModulePoolTask retained = Read(frame, address);
            retained.RequireKnownPayload(20, bytes);
            return new(retained.ReadRawPayloadSpan().Slice(8, bytes).ToArray().ToImmutableArray());
        }
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(source, 0, (workspace, node) =>
        {
            frame = WriteBytes(frame, address + 20, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan()), bytes);
            frame = DrawProgrammeNode6ECCE0(frame, source, node, caller, resources, inputs);
            return Workspace();
        });
        frame = WriteBytes(frame, address + 20, NativeBattleEffectArena.Clear().Write(0, advanced.Workspace.Data.AsSpan()), bytes);
        return (frame, advanced.Remaining != 0 ? 0 : 2);
    }

    public static NativeBattleEffect338Frame DrawProgrammeNode6ECCE0(NativeBattleEffect338Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller, NativeBattleEffect338Resources resources, NativeBattleEffect338Inputs inputs)
    {
        NativeBattleVector scale = node.Vector24;
        if (scale is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return frame;
        }
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometry + 4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometry + 8));
        int first = node.Word40;
        int second = node.Word42;
        int ratio = node.Scalar38;
        if (count < 1 || count > 56 || first < 0 || first >= shapes || second < 0 || second >= shapes)
        {
            throw new InvalidDataException("Effect338 programme geometry or morph selectors leave their original source.");
        }
        uint geometryAddress = checked(source.Address + (uint)geometry);
        (frame, uint temporary) = Allocate(frame, 88);
        uint vertices;
        if (first == second || ratio is 0 or 4096)
        {
            int selected = first == second || ratio == 0 ? first : second;
            vertices = checked(geometryAddress + 12 + (uint)(selected * count * 8));
        }
        else
        {
            NativeBattleStaticMeshResource mesh = new(0, geometryAddress, source.Data.Slice(geometry, source.Data.Length - geometry));
            frame = MorphProgramme338(frame, mesh, first, second, ratio, Dword338(caller, 36));
            vertices = Dword338(caller, 36);
        }
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattlePackedMatrix32 contextMatrix = Packed338(caller);
        NativeBattleActionMatrix basis = contextMatrix.ReadMatrix();
        int x;
        int y;
        int z;
        if ((node.Header & 0x200) != 0)
        {
            x = node.Vector8.X;
            y = node.Vector8.Y;
            z = node.Vector8.Z;
        }
        else
        {
            frame = LoadMatrix(frame, contextMatrix, true, false);
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers338(frame), 0, node.Vector8, null), false);
            x = registers.Colors.Mac[0]!.Value;
            y = registers.Colors.Mac[1]!.Value;
            z = registers.Colors.Mac[2]!.Value;
            frame = Publish338(frame, registers);
            rotation = NativeBattleGfMatrices.Multiply56C090(basis, rotation);
        }
        rotation = rotation with { X = unchecked(x + basis.X), Y = unchecked(y + basis.Y), Z = unchecked(z + basis.Z) };
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (node.Header & 0x100) != 0 ? NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)) with { X = rotation.X, Y = rotation.Y, Z = rotation.Z } : NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(geometryAddress)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(24, caller.Read(32, 4)).Write(28, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        NativeBattleDefaultActMeshStep drawn = NativeBattleDefaultActMesh.Draw(frame.Memory, source, geometry, context, inputs.ReadProjection(frame), inputs.Colors, readPackedVertex: ReadVertex);
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context88, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect338DeliverBatch(drawn.Batch, CursorGlobal));
        return Release(frame, 88);

        NativeBattleEffectArena ReadVertex(int offset)
        {
            if (offset < 0 || offset % 8 != 0 || offset > count * 8 - 8)
            {
                throw new InvalidDataException("Effect338 selected vertex leaves its current geometry shape.");
            }
            uint pointer = checked(vertices + (uint)offset);
            if (vertices == ArenaAddress + ScratchOffset)
            {
                return ReadBytes(frame, pointer, 8);
            }
            if (pointer < source.Address || (ulong)pointer + 8 > (ulong)source.Address + (uint)source.Data.Length)
            {
                throw new InvalidDataException("Effect338 inline vertex leaves its selected original programme.");
            }
            return NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(checked((int)(pointer - source.Address)), 8));
        }
    }

    private static NativeBattlePackedMatrix32 Packed338(NativeBattleEffectArena view)
    {
        (byte[] bytes, bool[] known) = view.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleEffect338Frame MorphProgramme338(NativeBattleEffect338Frame frame, NativeBattleStaticMeshResource mesh, int first, int second, int ratio, uint destination)
    {
        if (destination != NativeBattleEffect338Profile.ArenaAddress + 0x1C90)
        {
            throw new InvalidDataException("Effect338 morph requires its current programme scratch.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        if (count < 1 || count * 8 > 448)
        {
            throw new InvalidDataException("Effect338 morph leaves its actual current scratch extent.");
        }
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect338 morph requires its geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect338 morph requires its arithmetic owner."));
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
