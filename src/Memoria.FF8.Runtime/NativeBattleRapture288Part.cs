using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRapture288Draw(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleRapture288Part
{
    public static NativeBattleRapture288Draw Draw(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleActionMatrix context, int scale, int depth, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(memory, staging, new([], 0));
        }
        ImmutableArray<byte> data = source.Data;
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(checked(8 + unchecked((short)node.GeometryIndex) * 4)));
        int count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(geometry + 4));
        if (count is < 0 or > 4096)
        {
            throw new InvalidDataException("Native Rapture requires its bounded original vertex count.");
        }
        ImmutableArray<byte> selected;
        uint selectedAddress;
        if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            int offset = checked(geometry + 12 + shape * count * 8);
            selected = data.Slice(offset, checked(count * 8));
            selectedAddress = checked(source.Address + (uint)offset);
        }
        else
        {
            NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Rapture morph requires its current vertex workspace.");
            selected = NativeBattleGfMorphGeometry.Morph701390(data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(9024, checked(count * 8)).ToImmutableArray());
            for (int index = 0; index < count; index++)
            {
                arena = arena.Write(9024 + index * 8, selected.AsSpan().Slice(index * 8, 6));
            }
            memory = memory with { Arena = arena };
            selectedAddress = 0x20DFAB8 + 9024;
        }
        NativeBattleActionMatrix matrix = NativeBattleRapture288Geometry.Compose(node, context, scale);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 0x58);
        staging = allocation.Staging;
        uint header = allocation.Address;
        Store(0, checked(source.Address + (uint)geometry));
        Store(4, selectedAddress);
        Store(12, unchecked((uint)(int)node.Scalar36));
        Store(28, node.Scalar36 == 0 ? 0x2030u : 0x20F0u);
        if (node.Scalar36 != 0)
        {
            Store(8, node.Color32);
        }
        Store(24, unchecked((uint)depth));
        NativeBattleModuleMeshTemporaryResult drawn = NativeBattleModulePackets.EmitRetainedBatch7043B0(staging, header, checked(source.Address + (uint)geometry), data.Slice(geometry, data.Length - geometry), matrix, projection, new(node.Scalar36 == 0 ? 0x2030u : 0x20F0u, node.Color32, node.Scalar36, null, null, depth, 17, 2), colors, offset => new(BinaryPrimitives.ReadInt16LittleEndian(selected.AsSpan().Slice(offset)), BinaryPrimitives.ReadInt16LittleEndian(selected.AsSpan().Slice(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(selected.AsSpan().Slice(offset + 4))), pilotIdentity: pilotIdentity);
        staging = drawn.Staging;
        NativeBattleGfGeometryBatch batch = drawn.Batch;
        if (node.Scalar36 != 0)
        {
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Rapture requires its retained color registers.");
            NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
            memory = memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers.PublishModelColor(node.Color32) } };
        }
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x58);
        return new(memory, staging, NativeBattleSlot280288Presentation.Depths(288, batch));

        void Store(int offset, uint value)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            staging = staging.Write(checked(header + (uint)offset), bytes);
        }
    }

}
