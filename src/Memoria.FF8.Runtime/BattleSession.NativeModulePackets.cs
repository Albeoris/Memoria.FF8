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
                batch = NativeFlashPacket(draw);
            }
            else
            {
                NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native module packet emission requires its prior projection/register owner.");
                batch = draw.Kind switch
                {
                    NativeBattleModuleDrawKind.Mesh => NativeBattleModulePackets.EmitMesh572200(draw, definition.NativeEffectMeshes.Single(resource => resource.Id == draw.MeshId), projection, 17, 2),
                    NativeBattleModuleDrawKind.Sprite => NativeBattleModulePackets.EmitSprite571DC0(draw, projection, NativeCamera!.Roll, 17, 2),
                    NativeBattleModuleDrawKind.Defensive => NativeBattleDefensivePackets.Emit570650(draw, definition.NativeDefensiveResources.Single(resource => resource.Variant == draw.Variant), projection),
                    _ => throw new NotSupportedException($"Native module draw{draw.Kind} requires its original primitive emission contract.")
                };
            }
            graphics = AppendNativeModulePacketBatch(graphics, batch, index);
        }
        return graphics;
    }

    private NativeBattleGraphicsState AppendNativeModulePacketBatch(NativeBattleGraphicsState graphics, NativeBattleModulePacketBatch batch, int moduleDraw)
    {
        NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native module packet emission requires its initialized OT owner.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native module packet emission requires its process owner.");
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
        foreach (NativeBattleAllocatedPrimitive allocated in batch.Primitives)
        {
            NativeBattleGeometryPrimitive primitive = allocated.Primitive;
            if (allocated.ByteOffset < 0 || allocated.ByteOffset + primitive.Words.Length * 4 > batch.AllocationBytes)
            {
                throw new InvalidDataException("Native module primitive leaves its original packet allocation.");
            }
            ordering = ordering.Prepend(primitive.Bucket, new(bank, process.PacketBuffer, checked(cursor + allocated.ByteOffset)), primitive.Words, allocated.Flags20, primitive.VertexPayload4, moduleDraw);
        }
        NativeEffectMemory = memory with { PrimitiveProcess = process with { ByteCursor = next } };
        return graphics with { PrimitiveFrame = frame with { Pending = ordering } };
    }

    private static NativeBattleModulePacketBatch NativeFlashPacket(NativeBattleModuleDraw draw)
    {
        uint rgb = (uint)(draw.Intensity | draw.Intensity << 8 | draw.Intensity << 16);
        byte command = draw.NativePrimitiveCommand ?? 0x62;
        int bucket = draw.NativeOrderingBucket ?? 17;
        ImmutableArray<uint> words;
        if (command == 0x62)
        {
            words = [0x3000000, 0x62000000 | rgb, 0, 216u << 16 | 320];
        }
        else if (command == 0x2A && draw.NativeScreenVertices.Length == 4)
        {
            words = ImmutableArray.Create(0x5000000u, 0x2A000000 | rgb).AddRange(draw.NativeScreenVertices.Select(point => unchecked((ushort)point.X) | unchecked((uint)point.Y << 16)));
        }
        else
        {
            throw new NotSupportedException("Native fullscreen module draw requires its original tile or four-vertex polygon command.");
        }
        NativeBattleGeometryPrimitive primitive = new(bucket, words, []);
        return new([new(primitive, 0, 1)], words.Length * 4);
    }
}
