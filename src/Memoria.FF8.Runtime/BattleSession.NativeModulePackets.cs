using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState SubmitNativeModulePackets(NativeBattleGraphicsState graphics)
    {
        if (definition.NativeGraphics?.PrimitiveTables is null || graphics.ModuleDraws.IsEmpty)
        {
            return graphics;
        }
        for (int index = 0; index < graphics.ModuleDraws.Length; index++)
        {
            NativeBattleModuleDraw draw = graphics.ModuleDraws[index];
            if (draw.Kind == NativeBattleModuleDrawKind.Sound)
            {
                continue;
            }
            NativeBattleModulePacketBatch batch;
            if (draw.Kind == NativeBattleModuleDrawKind.Flash)
            {
                batch = NativeBattleFlashPackets.Emit(draw);
            }
            else
            {
                NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native module packet emission requires its prior projection/register owner.");
                if (draw.Kind == NativeBattleModuleDrawKind.Impact570860)
                {
                    graphics = SubmitNativeImpactPackets(graphics, draw, projection, index);
                    continue;
                }
                batch = draw.Kind switch
                {
                    NativeBattleModuleDrawKind.Mesh => NativeBattleModulePackets.EmitMesh572200(draw, definition.NativeEffectMeshes.Single(resource => resource.Id == draw.MeshId), projection, 17, 2, definition.NativeGraphics.Rules.SummonColors ?? throw new NotSupportedException("Native572200 requires its authenticated interpolation constants.")),
                    NativeBattleModuleDrawKind.Sprite => NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera!.Roll, 17, 2),
                    NativeBattleModuleDrawKind.Defensive => NativeBattleDefensivePackets.Emit570650(draw, definition.NativeDefensiveResources.Single(resource => resource.Variant == draw.Variant), projection, definition.NativeGraphics.Rules.SummonColors ?? throw new NotSupportedException("Native570650 requires its authenticated interpolation constants.")),
                    _ => throw new NotSupportedException($"Native module draw{draw.Kind} requires its original primitive emission contract.")
                };
            }
            graphics = AppendNativeModulePacketBatch(graphics, batch, index, draw.NativeEffectArenaOffset);
        }
        return graphics;
    }

    private NativeBattleGraphicsState SubmitNativeImpactPackets(NativeBattleGraphicsState graphics, NativeBattleModuleDraw draw, NativeBattleCameraProjection projection, int moduleDraw)
    {
        if (draw.NativeEffectArenaOffset is not null)
        {
            throw new InvalidDataException("Native570860 writes only its ordinary COMMON packet owner.");
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native570860 requires its current camera.");
        NativeBattleModuleImpactPacketStep rendered = NativeBattleModuleImpactPackets.Draw570860(NativeEffectMemory ?? throw new NotSupportedException("Native570860 requires its current process memory."), NativeFileStaging ?? throw new NotSupportedException("Native570860 requires its retained temporary memory."), draw, camera.CapturePackedView1D97778(), projection, definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native570860 requires its authenticated color constants."), definition.NativeEffectMeshes.SingleOrDefault(resource => resource.Id == 5), definition.NativeModuleResources, camera.Roll);
        NativeEffectMemory = rendered.Memory;
        NativeFileStaging = rendered.Staging;
        foreach (NativeBattleGfGeometryBatch batch in rendered.Batches)
        {
            NativeBattlePrimitiveProcess process = (NativeEffectMemory ?? throw new NotSupportedException("Native570860 lost its current process memory.")).PrimitiveProcess ?? throw new NotSupportedException("Native570860 requires its current COMMON packet owner.");
            int cursor = process.ByteCursor ?? throw new NotSupportedException("Native570860 requires its current COMMON cursor.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(batch, cursor, process.CommonPacketWriteCapacity, address => process.ReadCommonPacket(address, 1)[0]);
            graphics = AppendNativeModulePacketBatch(graphics, new NativeBattleModulePacketBatch([], batch.AllocationBytes) { CapturedWrites = resolved }, moduleDraw, null);
        }
        return graphics;
    }

    private NativeBattleGraphicsState AppendNativeModulePacketBatch(NativeBattleGraphicsState graphics, NativeBattleModulePacketBatch batch, int? moduleDraw, int? effectArenaOffset)
    {
        NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native module packet emission requires its initialized OT owner.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native module packet emission requires its process owner.");
        if (batch.CapturedWrites is { } captured)
        {
            if (effectArenaOffset is int capturedCursor)
            {
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, capturedCursor, frame.Pending, captured, moduleDraw: moduleDraw);
                NativeEffectMemory = written.Memory;
                return graphics with { PrimitiveFrame = frame with { Pending = written.Ordering } };
            }
            NativeBattleCommonPacketWrite common = NativeBattleCommonPacketArena.Append(memory.PrimitiveProcess ?? throw new NotSupportedException("Native module packet emission requires its original arena cursor."), frame.Pending, captured, moduleDraw: moduleDraw);
            NativeEffectMemory = memory with { PrimitiveProcess = common.Process };
            return graphics with { PrimitiveFrame = frame with { Pending = common.Ordering } };
        }
        if (effectArenaOffset is int privateCursor)
        {
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, privateCursor, frame.Pending, batch, NativeBattleEffectGouraudWrites.FullDword, moduleDraw);
            NativeEffectMemory = written.Memory;
            return graphics with { PrimitiveFrame = frame with { Pending = written.Ordering } };
        }
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native module packet emission requires its original arena cursor.");
        int cursor = process.ByteCursor ?? throw new NotSupportedException("Native module packet emission has no observed packet cursor.");
        string bank = process.PacketBank ?? throw new NotSupportedException("Native module packet emission has no allocated logical arena.");
        int capacity = process.CommonPacketWriteCapacity;
        int next = checked(cursor + batch.AllocationBytes);
        if (batch.AllocationBytes < 0 || next > capacity)
        {
            throw new NotSupportedException("Native module packet batch reaches its original arena capacity.");
        }
        NativeBattleLogicalOrdering ordering = frame.Pending;
        // TODO(TD-009): Logical module words do not establish every physical padding byte.
        ImmutableArray<NativeBattleCommonPacketStore>.Builder stores = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>();
        using NativeBattleLogicalOrdering.Builder links = new(ordering);
        if (batch.AllocationBytes != 0)
        {
            links.InvalidatePacketRange(new(bank, process.PacketBuffer, cursor), batch.AllocationBytes);
            stores.Add(new(cursor, NativeBattleTelemetry.Immutable(1151, new byte[batch.AllocationBytes]), Enumerable.Repeat(NativeBattleGfPacketByteState.Unknown, batch.AllocationBytes).ToImmutableArray()));
        }
        foreach (NativeBattleAllocatedPrimitive allocated in batch.Primitives)
        {
            NativeBattleGeometryPrimitive primitive = allocated.Primitive;
            if (allocated.ByteOffset < 0 || allocated.ByteOffset + primitive.Words.Length * 4 > batch.AllocationBytes)
            {
                throw new InvalidDataException("Native module primitive leaves its original packet allocation.");
            }
            int address = checked(cursor + allocated.ByteOffset);
            links.InvalidatePacketRange(new(bank, process.PacketBuffer, address), primitive.Words.Length * 4);
            bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            links.Prepend(primitive.Bucket, new(bank, process.PacketBuffer, address), primitive.Words, allocated.Flags20, primitive.VertexPayload4, moduleDraw, pilotGeometry: primitive.PilotGeometry);
            stores.AddRange(NativeBattleCommonPacketArena.PrimitiveStores(address, primitive, linked));
            if (linked)
            {
                stores.Add(new(address, NativeBattleTelemetry.Immutable(1152, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink)), [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known]));
            }
        }
        NativeEffectMemory = memory with { PrimitiveProcess = process.ApplyCommonWrites(stores.ToImmutable()) with { ByteCursor = next } };
        return graphics with { PrimitiveFrame = frame with { Pending = links.ToImmutable() } };
    }
}
