using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShockwavePulsar260ActDrawingStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary88, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleShockwavePulsar260ActDrawing
{
    public static bool Visible(NativeBattleGfAnimationNode node) => NativeBattleActMeshRendering.Visible(node);

    public static NativeBattleShockwavePulsar260Frame EmitNode(NativeBattleShockwavePulsar260Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context20, NativeBattlePackedMatrix32 camera, byte dispatch, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, Func<NativeBattleShockwavePulsar260Frame, NativeBattleGfGeometryBatch, NativeBattleShockwavePulsar260Frame> deliverPrivate, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (!Visible(node))
        {
            return frame;
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 88);
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
        NativeBattlePackedMatrix32 retainedView = camera;
        if ((node.Header & 0x1000) != 0)
        {
            (byte[] bytes, bool[] known) = frame.State.ReadView(NativeBattleShockwavePulsar260Profile.CopiedViewAddress, 32).ReadSnapshot(0, 32);
            retainedView = new(bytes.ToImmutableArray(), known.ToImmutableArray());
        }
        uint morph = checked(frame.State.ReadDword(NativeBattleShockwavePulsar260Profile.WorkingGlobal) + NativeBattleShockwavePulsar260Profile.MorphOffset);
        NativeBattleShockwavePulsar260ActDrawingStep drawn = Draw(source, node, context20, camera, retainedView, temporary, frame.Memory, morph, NativeBattleShockwavePulsar260Profile.MorphBytes, dispatch, projection, constants, pilotIdentity: pilotIdentity);
        frame = frame with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Temporary88, 88) };
        frame = deliverPrivate(frame, drawn.Batch);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 88) };
    }

    private static NativeBattleFileStaging RequireStaging(NativeBattleShockwavePulsar260Frame frame) => frame.Staging ?? throw new NotSupportedException("Shockwave Pulsar requires its current physical temporary memory.");

    public static NativeBattleShockwavePulsar260ActDrawingStep Draw(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context20, NativeBattlePackedMatrix32 camera, NativeBattlePackedMatrix32 retainedView, NativeBattleEffectArena temporary88, NativeBattleEffectMemory memory, uint morphAddress, int morphLength, byte dispatch, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, short? localVectorW = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (!Visible(node))
        {
            throw new InvalidOperationException("Shockwave Pulsar ACT mesh allocation requires a visible node.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Shockwave Pulsar requires its current shared arena.");
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Shockwave Pulsar requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Shockwave Pulsar requires its current color registers."));
        int geometry = I32(source.Data, checked(8 + node.GeometryIndex * 4));
        int count = I32(source.Data, checked(geometry + 4));
        if (count < 1)
        {
            throw new InvalidDataException("Shockwave Pulsar ACT shape leaves its original morph-to-packet boundary.");
        }
        int first = checked(geometry + 12 + node.Word40 * count * 8);
        int second = checked(geometry + 12 + node.Word42 * count * 8);
        bool interpolate = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        int selected = node.Word40 != node.Word42 && node.Scalar38 == 4096 ? second : first;
        int morphOffset = checked((int)(morphAddress - NativeBattleEffectNodeMemory.ArenaAddress));
        if (interpolate && (morphLength < 0 || count > morphLength / 8 || morphOffset < 0 || morphOffset > NativeBattleEffectArena.Length - morphLength))
        {
            throw new InvalidDataException("Shockwave Pulsar ACT requires its actual bounded morph owner.");
        }
        uint selectedAddress;
        if (interpolate)
        {
            uint morph = Dword(context20, 0x10);
            if (morph != morphAddress)
            {
                throw new InvalidDataException("Shockwave Pulsar ACT morph pointer does not identify its actual shared working arena.");
            }
            for (int vertex = 0; vertex < count; vertex++)
            {
                NativeBattleVector a = Vector(source.Data, checked(first + vertex * 8));
                NativeBattleVector b = Vector(source.Data, checked(second + vertex * 8));
                registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - node.Scalar38)), Ir = [unchecked((ushort)a.X), unchecked((ushort)a.Y), unchecked((ushort)a.Z)] } };
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, constants);
                registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)node.Scalar38), Ir = [unchecked((ushort)b.X), unchecked((ushort)b.Y), unchecked((ushort)b.Z)] } };
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, constants);
                byte[] xyz = new byte[6];
                for (int axis = 0; axis < 3; axis++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(axis * 2), unchecked((short)registers.Colors.Ir[axis]!.Value));
                }
                arena = arena.Write(checked(morphOffset + vertex * 8), xyz);
            }
            selectedAddress = morph;
        }
        else
        {
            _ = Slice(source.Data, selected, checked(count * 8));
            selectedAddress = checked(source.Address + (uint)selected);
        }
        NativeBattleActionMatrix matrix = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : (node.Header & 0x80000) != 0 ? NativeBattleGfMatrices.Euler701630(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector anchor = new(Signed(context20, 0), Signed(context20, 2), Signed(context20, 4));
        NativeBattleVector point = node.Vector8;
        NativeBattleActionMatrix view = camera.ReadMatrix();
        (int X, int Y, int Z) transformed;
        if ((node.Header & 0x1000) != 0)
        {
            registers = Transform(registers, camera, anchor, ContextW(context20));
            (int X, int Y, int Z) baseTranslation = Mac(registers);
            registers = Transform(registers, retainedView, point, localVectorW);
            (int X, int Y, int Z) offset = Mac(registers);
            transformed = (unchecked(baseTranslation.X + offset.X), unchecked(baseTranslation.Y + offset.Y), unchecked(baseTranslation.Z + offset.Z));
            matrix = NativeBattleGfMatrices.Multiply56C090(retainedView.ReadMatrix(), matrix);
        }
        else if ((node.Header & 0x200) != 0)
        {
            registers = Transform(registers, camera, anchor, ContextW(context20));
            (int X, int Y, int Z) baseTranslation = Mac(registers);
            transformed = (unchecked(baseTranslation.X + point.X), unchecked(baseTranslation.Y + point.Y), unchecked(baseTranslation.Z + point.Z));
        }
        else
        {
            point = new(unchecked((short)(point.X + anchor.X)), unchecked((short)(point.Y + anchor.Y)), unchecked((short)(point.Z + anchor.Z)));
            registers = Transform(registers, camera, point, localVectorW);
            transformed = Mac(registers);
            matrix = NativeBattleGfMatrices.Multiply56C090(view, matrix);
        }
        matrix = matrix with { X = unchecked(transformed.X + view.X), Y = unchecked(transformed.Y + view.Y), Z = unchecked(transformed.Z + view.Z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if ((node.Header & 0x100) != 0)
            {
                NativeBattleActionMatrix scaled = NativeBattleGfMatrices.Multiply56C090(matrix, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096));
                matrix = matrix with { Rotation = scaled.Rotation };
            }
            else
            {
                matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
            }
        }
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null) };
        uint shapeAddress = checked(source.Address + (uint)geometry);
        temporary88 = Store(temporary88, 0, shapeAddress);
        temporary88 = Store(temporary88, 4, selectedAddress);
        temporary88 = Store(temporary88, 12, unchecked((uint)(int)node.Scalar36));
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        if (node.Scalar36 != 0)
        {
            flags |= 0xC0;
        }
        temporary88 = Store(temporary88, 0x1C, flags);
        if (node.Scalar36 != 0)
        {
            temporary88 = Store(temporary88, 8, node.Color32);
        }
        temporary88 = Store(temporary88, 0x18, unchecked((uint)(int)Signed(context20, 0xC)));
        memory = memory with { Arena = arena, GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
        if (dispatch == (byte)'1')
        {
            NativeBattleShockwavePulsar260MeshStep drawn = NativeBattleShockwavePulsar260Mesh.Draw(memory, source, geometry, temporary88, projection, constants, offset => ReadVertex(offset).Vertex, offset => ReadVertex(offset).W, pilotIdentity: pilotIdentity);
            return new(drawn.Memory, drawn.Context88, drawn.Batch);
        }
        var ordinary = NativeBattleDefaultActMesh.Draw(memory, source.Data, source.Address, geometry, temporary88, projection, constants, offset => ReadVertex(offset).Vertex, offset => ReadVertex(offset).W, pilotIdentity: pilotIdentity);
        return new(ordinary.Memory, ordinary.Context88, ordinary.Batch);

        (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
        {
            if (offset < 0 || offset > count * 8 - 8)
            {
                throw new InvalidDataException("Shockwave Pulsar ACT face leaves its selected packed vertex bank.");
            }
            if (!interpolate)
            {
                int at = checked(selected + offset);
                return (Vector(source.Data, at), BinaryPrimitives.ReadInt16LittleEndian(Slice(source.Data, at + 6, 2)));
            }
            byte[] xyz = arena.Read(checked(morphOffset + offset), 6);
            (byte[] w, bool[] known) = arena.ReadSnapshot(checked(morphOffset + offset + 6), 2);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
        }
    }

    private static NativeBattleGeometryRegisterStep Transform(NativeBattleGeometryRegisterStep registers, NativeBattlePackedMatrix32 matrix, NativeBattleVector point, short? w)
    {
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, false, Padding(matrix)) };
        registers = NativeBattleGeometry.LoadVector(registers, 0, point, w);
        return NativeBattleGeometry.Transform(registers, false);
    }
    private static (int X, int Y, int Z) Mac(NativeBattleGeometryRegisterStep registers) => (registers.Colors.Mac[0]!.Value, registers.Colors.Mac[1]!.Value, registers.Colors.Mac[2]!.Value);
    private static short? Padding(NativeBattlePackedMatrix32 matrix) => matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18)) : null;
    private static int I32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(Slice(bytes, offset, 4));
    private static NativeBattleVector Vector(ImmutableArray<byte> bytes, int offset)
    {
        ReadOnlySpan<byte> data = Slice(bytes, offset, 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(data), BinaryPrimitives.ReadInt16LittleEndian(data[2..]), BinaryPrimitives.ReadInt16LittleEndian(data[4..]));
    }
    private static ReadOnlySpan<byte> Slice(ImmutableArray<byte> bytes, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > bytes.Length - count)
        {
            throw new InvalidDataException("Shockwave Pulsar ACT read leaves its original source.");
        }
        return bytes.AsSpan().Slice(offset, count);
    }
    private static short? ContextW(NativeBattleEffectArena context)
    {
        (byte[] bytes, bool[] known) = context.ReadSnapshot(6, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
    private static short Signed(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Store(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
}
