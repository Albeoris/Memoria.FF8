using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleScanFrame SubmitNativeScanPackets(NativeBattleScanFrame current, NativeBattleModulePacketBatch batch, ref NativeBattleGraphicsState graphics)
    {
        if (batch.CapturedWrites is { } captured)
        {
            return SubmitNativeScanGeometry(current, captured, ref graphics);
        }
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Scan drawing requires its current ordering table.");
        int cursor = current.State.CurrentDrawOffset ?? throw new NotSupportedException("Native Scan drawing requires its current private packet cursor.");
        NativeBattleEffectPacketWrite written;
        if (!batch.Primitives.IsDefaultOrEmpty && batch.Primitives.All(allocation => allocation.Primitive.Words.Length == 4 && (allocation.Primitive.Words[1] >> 24 & 0xFC) == 0x40))
        {
            ushort flags = batch.Primitives[0].Flags20;
            if (batch.Primitives.Any(allocation => allocation.Flags20 != flags))
            {
                throw new InvalidDataException("Native Scan line batch requires its actual shared submission kind.");
            }
            ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>(batch.Primitives.Length);
            foreach (NativeBattleAllocatedPrimitive allocation in batch.Primitives)
            {
                byte[] bytes = new byte[16];
                for (int word = 0; word < 4; word++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(word * 4), allocation.Primitive.Words[word]);
                }
                attempts.Add(new(allocation.ByteOffset, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 16).ToImmutableArray(), allocation.Primitive));
            }
            written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, new NativeBattleGfGeometryBatch(attempts.MoveToImmutable(), batch.AllocationBytes), flags);
        }
        else
        {
            written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
        }
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        return current with { Memory = written.Memory, State = current.State with { CurrentDrawOffset = written.Cursor } };
    }

    private NativeBattleScanFrame SubmitNativeScanGeometry(NativeBattleScanFrame current, NativeBattleGfGeometryBatch batch, ref NativeBattleGraphicsState graphics)
    {
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Scan geometry requires its current ordering table.");
        int cursor = current.State.CurrentDrawOffset ?? throw new NotSupportedException("Native Scan geometry requires its current private packet cursor.");
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, batch);
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        return current with { Memory = written.Memory, State = current.State with { CurrentDrawOffset = written.Cursor } };
    }

    private NativeBattleScanReticleFrame DrawNativeScanRing(NativeBattleScanReticleFrame current, NativeBattleModulePoolTask task, short radius, short angle, short count, ref NativeBattleGraphicsState graphics)
    {
        NativeBattleScanReticleRing ring = NativeBattleScanReticleLines.Ring84D6D0(unchecked((short)task.ReadWord(0x1C)), unchecked((short)task.ReadWord(0x1E)), radius, angle, count);
        return current with { Scan = SubmitNativeScanPackets(current.Scan, ring.Packets, ref graphics) };
    }

    private NativeBattleScanReticleFrame DrawNativeScanFourLines(NativeBattleScanReticleFrame current, NativeBattleModulePoolTask task, ref NativeBattleGraphicsState graphics)
    {
        NativeBattleModulePacketBatch lines = NativeBattleScanReticleLines.FourLines84E1F0(unchecked((short)task.ReadWord(0x1C)), unchecked((short)task.ReadWord(0x1E)));
        return current with { Scan = SubmitNativeScanPackets(current.Scan, lines, ref graphics) };
    }

    private NativeBattleScanPairMorphFrame DrawNativeScanMesh(NativeBattleScanPairMorphFrame current, NativeBattleScanMeshDraw draw, NativeBattleScanVisualResources resources, ref NativeBattleGraphicsState graphics)
    {
        ImmutableArray<byte> source;
        if (draw.ResourceAddress is 0x15CDDEC or 0x15CE1D4)
        {
            if (!current.MutableMeshes.TryGetValue(draw.ResourceAddress, out NativeBattleScanByteOwner? mutable) || mutable.Bytes.IsDefault || mutable.Known.IsDefault || mutable.Bytes.Length != mutable.Known.Length || mutable.Known.Any(known => !known))
            {
                throw new NotSupportedException("Native Scan morph drawing requires its actual retained process mesh bytes.");
            }
            source = mutable.Bytes;
        }
        else if (resources.MeshesByAddress.TryGetValue(draw.ResourceAddress, out NativeBattleStaticMeshResource? mesh))
        {
            source = mesh.Bytes;
        }
        else
        {
            throw new NotSupportedException("Native Scan mesh drawing requires its selected original resource.");
        }
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Scan mesh drawing requires its current projection registers.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Scan mesh drawing requires its original interpolation constants.");
        NativeBattleModuleMeshContext context = new(0xF0, draw.Color, draw.Fade, null, null, null, 17, 2);
        NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(source, 0, default, draw.Matrix, projection, context, colors);
        return current with { Scan = SubmitNativeScanGeometry(current.Scan, batch, ref graphics) };
    }

    private NativeBattleScanReticleFrame DrawNativeScanSprite(NativeBattleScanReticleFrame current, NativeBattleModulePoolTask task, NativeBattleScanVisualResources resources, ref NativeBattleGraphicsState graphics)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return current;
        }
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Scan sprite drawing requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Scan sprite drawing requires its current projection registers.");
        NativeBattleModulePacketBatch batch = NativeBattleScanOpeningChildren.Sprite84E580(task, NativeScanSprite(resources, task.ReadDword(0x4C)), projection, camera.Roll, 0x400);
        return current with { Scan = SubmitNativeScanPackets(current.Scan, batch, ref graphics) };
    }

    private static ImmutableArray<byte> NativeScanLineRecords(NativeBattleScanVisualResources resources, uint address)
    {
        uint offset = unchecked(address - 0x15D1718u);
        if (resources.LineArrays15D1718.IsDefault || offset % 0x30 != 0 || offset / 0x30 >= resources.LineArrays15D1718.Length)
        {
            throw new NotSupportedException("Native Scan corner drawing requires its selected original line records.");
        }
        return resources.LineArrays15D1718[checked((int)(offset / 0x30))];
    }

    private static NativeBattleModuleResource NativeScanSprite(NativeBattleScanVisualResources resources, uint address)
    {
        return address switch
        {
            0x15D07D4 => resources.Sprite15D07D4,
            0x15D0AF8 => resources.Sprite15D0AF8,
            _ => throw new NotSupportedException("Native Scan sprite drawing requires its selected original resource.")
        };
    }
}
