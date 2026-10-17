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

    private NativeBattleGraphicsState AppendNativeModulePacketBatch(NativeBattleGraphicsState graphics, NativeBattleModulePacketBatch batch, int moduleDraw, int? effectArenaOffset)
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
        int capacity = bank == "off_B6D070" ? 114688 : bank == "off_B6D094" ? 147456 : throw new NotSupportedException("Native module packet emission requires an unknown original arena.");
        int next = checked(cursor + batch.AllocationBytes);
        if (batch.AllocationBytes < 0 || next > capacity)
        {
            throw new NotSupportedException("Native module packet batch reaches its original arena capacity.");
        }
        NativeBattleLogicalOrdering ordering = frame.Pending;
        // TODO(TD-009): Logical module words do not establish every physical padding byte.
        ImmutableArray<NativeBattleCommonPacketStore>.Builder stores = ImmutableArray.CreateBuilder<NativeBattleCommonPacketStore>();
        if (batch.AllocationBytes != 0)
        {
            stores.Add(new(cursor, new byte[batch.AllocationBytes].ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Unknown, batch.AllocationBytes).ToImmutableArray()));
        }
        foreach (NativeBattleAllocatedPrimitive allocated in batch.Primitives)
        {
            NativeBattleGeometryPrimitive primitive = allocated.Primitive;
            if (allocated.ByteOffset < 0 || allocated.ByteOffset + primitive.Words.Length * 4 > batch.AllocationBytes)
            {
                throw new InvalidDataException("Native module primitive leaves its original packet allocation.");
            }
            int address = checked(cursor + allocated.ByteOffset);
            bool linked = ordering.Proxies.Length < NativeBattlePrimitiveOrdering.ProxyCapacity;
            ordering = ordering.Prepend(primitive.Bucket, new(bank, process.PacketBuffer, address), primitive.Words, allocated.Flags20, primitive.VertexPayload4, moduleDraw);
            stores.AddRange(NativeBattleCommonPacketArena.PrimitiveStores(address, primitive, linked));
        }
        NativeEffectMemory = memory with { PrimitiveProcess = process.ApplyCommonWrites(stores.ToImmutable()) with { ByteCursor = next } };
        return graphics with { PrimitiveFrame = frame with { Pending = ordering } };
    }
}
