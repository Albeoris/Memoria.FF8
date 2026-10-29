using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleZombiePart
{
    public static NativeBattleZombieDraw Draw(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, NativeBattleZombieResources resources, NativeBattleGfAnimationNode node, NativeBattleVector anchor, int reach, int depth, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(memory, staging, new([], 0));
        }
        ImmutableArray<byte> data = resources.Layout.Data;
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(checked(8 + unchecked((short)node.GeometryIndex) * 4)));
        int count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(geometry + 4));
        if (count is < 0 or > 4096)
        {
            throw new InvalidDataException("Native Zombie requires its bounded original vertex count.");
        }
        ImmutableArray<byte> selected;
        uint selectedAddress;
        if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            int offset = checked(geometry + 12 + shape * count * 8);
            selected = data.Slice(offset, checked(count * 8));
            selectedAddress = checked(resources.Layout.Address + (uint)offset);
        }
        else
        {
            NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Zombie morph requires its current vertex workspace.");
            selected = NativeBattleGfMorphGeometry.Morph701390(data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(0x1530, checked(count * 8)).ToImmutableArray());
            for (int index = 0; index < count; index++)
            {
                arena = arena.Write(0x1530 + index * 8, selected.AsSpan().Slice(index * 8, 6));
            }
            memory = memory with { Arena = arena };
            selectedAddress = 0x20DFAB8 + 0x1530;
        }
        NativeBattleActionMatrix matrix = NativeBattleZombieDrawing.Matrix(node, anchor, reach, projection.View ?? throw new NotSupportedException("Native Zombie requires its actual camera matrix."));
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 0x58);
        staging = allocation.Staging;
        uint header = allocation.Address;
        Store(0, checked(resources.Layout.Address + (uint)geometry));
        Store(4, selectedAddress);
        Store(12, unchecked((uint)(int)node.Scalar36));
        Store(28, node.Scalar36 == 0 ? 0x2030u : 0x20F0u);
        if (node.Scalar36 != 0)
        {
            Store(8, node.Color32);
        }
        Store(24, unchecked((uint)depth));
        NativeBattleGfGeometryBatch batch;
        if (node.GeometryIndex is 2 or 3)
        {
            batch = NativeBattleAdditiveQuadPackets.Emit(data, geometry, selected, matrix, projection, node.Scalar36, node.Color32, colors, false, depth, 2, pilotIdentity);
        }
        else
        {
            batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(data, geometry, selected, matrix, projection, new(node.Scalar36 == 0 ? 0x2030u : 0x20F0u, node.Color32, node.Scalar36, null, null, depth, 17, 2), colors, pilotIdentity: pilotIdentity);
        }
        if (node.Scalar36 != 0)
        {
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Zombie requires its retained color registers.");
            NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
            memory = memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers.PublishModelColor(node.Color32) } };
        }
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x58);
        return new(memory, staging, batch);

        void Store(int offset, uint value)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            staging = staging.Write(checked(header + (uint)offset), bytes);
        }
    }

}
