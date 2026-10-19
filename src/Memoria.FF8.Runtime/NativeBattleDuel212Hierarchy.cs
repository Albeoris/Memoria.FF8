using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel212HierarchyStep(NativeBattleDuel212Frame Frame, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleDuel212Hierarchy
{
    public static NativeBattleDuel212HierarchyStep Draw6A2B80(NativeBattleDuel212Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleDuel212Program context, int workspaceFrame, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(frame, new([], 0));
        }
        NativeBattleDuel212Resources resources = frame.Memory.Duel212?.Resources ?? throw new NotSupportedException("Native Burning Rave hierarchy requires original entered resources.");
        uint meshAddress = checked(source.Address + resources.Dword(checked(source.Address + 8u + (uint)(4 * unchecked((short)node.GeometryIndex)))));
        int geometry = checked((int)(meshAddress - NativeBattleDuel212Resources.Address));
        int count = checked((int)resources.Dword(meshAddress + 4));
        bool morphed = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        uint selected;
        if (morphed)
        {
            ImmutableArray<NativeBattleVector> vertices = NativeBattleGfMorphGeometry.MorphVertices701390(resources.StaticData, geometry, node.Word40, node.Word42, node.Scalar38);
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Burning Rave interpolation requires its actual destination arena.");
            selected = 0x20DFAB8 + 42008;
            for (int index = 0; index < vertices.Length; index++)
            {
                NativeBattleVector vertex = vertices[index];
                int offset = NativeBattleWaterExecutionFields.ArenaOffset(checked(selected + (uint)index * 8), 6);
                arena = arena.Write(offset, BitConverter.GetBytes(vertex.X)).Write(offset + 2, BitConverter.GetBytes(vertex.Y)).Write(offset + 4, BitConverter.GetBytes(vertex.Z));
            }
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
        }
        else
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = checked(meshAddress + 12u + (uint)(shape * count * 8));
        }
        NativeBattleActionMatrix? billboard = (node.Header & 0x1000) == 0 ? null : ReadMatrix(frame.Staging, 0x2510280);
        //6A2B80 always composes the rotation for an indirect node; it does not consume6C53A0's bit8000 bypass.
        byte[] nodeBytes = node.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(nodeBytes.AsSpan(4), node.Header & ~0x8000u);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose6C53A0(node with { Data = nodeBytes.ToImmutableArray() }, context.Basis, billboard);
        int nodeIndex = unchecked((short)node.GeometryIndex);
        char window = context.Windows[nodeIndex];
        ushort vertical = window is '1' or '2' ? (ushort)(unchecked((byte)context.VerticalOffset) & 0x7F) : (ushort)0;
        NativeBattleTextureWindow after = window == '1' ? new(0, 0, 256, 128) : window == '2' ? new(0, 128, 256, 128) : new(0, 0, 256, 256);
        uint flags = node.Scalar36 != 0 ? 0x20F0u : 0x2030u;
        uint farColor = node.Scalar36 != 0 ? node.Color32 : 0;
        int fade = node.Scalar36;
        int depth = context.Depth;
        if (context.DepthFlags is not null)
        {
            depth = context.SecondaryDepth;
            if (context.DepthFlags[nodeIndex] == '1')
            {
                fade = 2300;
                farColor = 0;
                flags |= 0xC0;
            }
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 108);
        NativeBattleFileStaging staging = allocation.Staging;
        staging = Dword(Dword(Dword(Dword(Dword(staging, allocation.Address, meshAddress), allocation.Address + 4, selected), allocation.Address + 28, flags), allocation.Address + 24, unchecked((uint)depth)), allocation.Address + 32, (uint)vertical << 16);
        staging = Word(Word(Word(Word(staging, allocation.Address + 36, 0), allocation.Address + 38, 0), allocation.Address + 40, 256), allocation.Address + 42, 256);
        if (window is '1' or '2')
        {
            staging = Word(Word(Word(Word(staging, allocation.Address + 44, after.X), allocation.Address + 46, after.Y), allocation.Address + 48, after.Width), allocation.Address + 50, after.Height);
        }
        staging = Dword(staging, allocation.Address + 12, unchecked((uint)fade));
        if (fade != 0)
        {
            staging = Dword(staging, allocation.Address + 8, farColor);
        }
        frame = frame with { Staging = staging };
        if (context.EmitFlags is not null && context.EmitFlags[nodeIndex] == '1')
        {
            NativeBattleEffectTaskPool particles = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel212Controller.Fragments);
            NativeBattleEffectTaskPool target = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel212Controller.Targets);
            NativeBattleVector origin = new(unchecked((short)target.Slots[0].ReadWord(328)), 0, unchecked((short)target.Slots[0].ReadWord(332)));
            NativeBattleDuel212Step born = NativeBattleDuel212Controller.Spawn6A3510(frame, particles, workspaceFrame, node, ReadSelected, origin);
            frame = born.Frame with { Effect = born.Frame.Effect with { Pools = born.Frame.Effect.Pools.Select(pool => pool.DescriptorAddress == particles.DescriptorAddress ? born.Pool : pool).ToImmutableArray() } };
        }
        NativeBattleEsunaMeshContext operands = new(flags, fade != 0 ? farColor : null, fade != 0 ? fade : null, depth, 0, vertical, new(0, 0, 256, 256), after, 17, 2);
        NativeBattleWindowedMesh701DD0Result drawn = NativeBattleWindowedMesh701DD0.Emit(frame.Staging, allocation.Address, meshAddress, resources.Read(meshAddress, checked((int)(NativeBattleDuel212Resources.EndAddress - meshAddress))), matrix, projection, operands, colors, ReadSelected);
        return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(drawn.Staging, 108) }, drawn.Batch);

        NativeBattleVector ReadSelected(int offset)
        {
            if (offset < 0 || (offset & 7) != 0 || offset >= count * 8)
            {
                throw new InvalidDataException("Native Burning Rave selected vertex leaves its actual shape.");
            }
            byte[] bytes = morphed ? frame.Memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(checked(selected + (uint)offset), 6), 6) : resources.Read(checked(selected + (uint)offset), 6).ToArray();
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        }
    }

    private static NativeBattleActionMatrix ReadMatrix(NativeBattleFileStaging staging, uint address)
    {
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(staging.Read(address + (uint)index * 2, 2))).ToImmutableArray();
        return new(rotation, BinaryPrimitives.ReadInt32LittleEndian(staging.Read(address + 20, 4)), BinaryPrimitives.ReadInt32LittleEndian(staging.Read(address + 24, 4)), BinaryPrimitives.ReadInt32LittleEndian(staging.Read(address + 28, 4)), 4096);
    }
    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));
    private static NativeBattleFileStaging Word(NativeBattleFileStaging staging, uint address, int value) => staging.Write(address, BitConverter.GetBytes(unchecked((ushort)value)));
}
