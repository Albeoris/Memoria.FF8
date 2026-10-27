using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89ProgramContext(NativeBattleActionMatrix Matrix, uint Morph, short Depth);
public sealed record NativeBattleRinoa89ProgramDraw(NativeBattleRinoa89Frame Frame, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleRinoa89ProgramDrawing
{
    public static NativeBattleRinoa89ProgramDraw Draw6FBB80(NativeBattleRinoa89Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleRinoa89ProgramContext context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(frame, new([], 0));
        }
        int index = unchecked((short)node.GeometryIndex);
        if (index < 0 || source.Data.IsDefault || source.Data.Length < 12 || index >= BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan()))
        {
            throw new InvalidDataException("Native6FBB80 geometry selector leaves its actual programme.");
        }
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + index * 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(geometry + 4));
        bool morph = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        uint selected;
        if (morph)
        {
            ImmutableArray<NativeBattleVector> vertices = NativeBattleGfMorphGeometry.MorphVertices701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38);
            selected = context.Morph;
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native6FBB80 requires its retained morph owner.");
            int destination = NativeBattleWaterExecutionFields.ArenaOffset(selected, checked(count * 8));
            using NativeBattleEffectArenaWriter writer = new(arena);
            for (int vertex = 0; vertex < vertices.Length; vertex++)
            {
                byte[] xyz = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(xyz, vertices[vertex].X);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), vertices[vertex].Y);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), vertices[vertex].Z);
                writer.Write(destination + vertex * 8, xyz);
            }
            frame = frame with { Memory = frame.Memory with { Arena = writer.Freeze() } };
        }
        else
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = checked(source.Address + (uint)geometry + 12u + (uint)(shape * count * 8));
        }
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(node.Vector16);
        (int x, int y, int z) = (node.Header & 0x200) != 0 ? (node.Vector8.X, node.Vector8.Y, node.Vector8.Z) : NativeBattleGfMatrices.Rotate56C3D0(context.Matrix, node.Vector8);
        if ((node.Header & 0x200) == 0)
        {
            local = NativeBattleGfMatrices.Multiply56C090(context.Matrix, local);
        }
        if (node.Vector24 is not { X: 4096, Y: 4096, Z: 4096 })
        {
            local = NativeBattlePoseMatrices.Scale56BEF0(local, node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
        }
        NativeBattleActionMatrix matrix = local with { X = unchecked(context.Matrix.X + x), Y = unchecked(context.Matrix.Y + y), Z = unchecked(context.Matrix.Z + z) };
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        if (node.Scalar36 != 0)
        {
            flags |= 0xC0;
        }
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 108);
        NativeBattleFileStaging staging = temporary.Staging;
        uint address = temporary.Address;
        uint meshAddress = checked(source.Address + (uint)geometry);
        staging = Dword(Dword(Dword(Dword(staging, address, meshAddress), address + 4, selected), address + 12, unchecked((uint)(int)node.Scalar36)), address + 28, flags);
        if (node.Scalar36 != 0)
        {
            staging = Dword(staging, address + 8, node.Color32);
        }
        staging = Dword(Dword(staging, address + 24, unchecked((uint)(int)context.Depth)), address + 32, 0);
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleEsunaMeshContext operands = new(flags, node.Scalar36 != 0 ? node.Color32 : null, node.Scalar36 != 0 ? node.Scalar36 : null, context.Depth, 0, 0, full, full, 17, 2);
        NativeBattleWindowedMesh701DD0Result result = NativeBattleWindowedMesh701DD0.Emit(staging, address, meshAddress, source.Data.Slice(geometry, source.Data.Length - geometry), matrix, projection, operands, colors, ReadSelected);
        return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(result.Staging, 108) }, result.Batch);

        NativeBattleVector ReadSelected(int offset)
        {
            byte[] bytes = morph ? (frame.Memory.Arena ?? throw new NotSupportedException("Native6FBB80 lost its current morph owner.")).Read(NativeBattleWaterExecutionFields.ArenaOffset(checked(selected + (uint)offset), 6), 6) : frame.Staging.Read(checked(selected + (uint)offset), 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        }
    }

    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }
}
