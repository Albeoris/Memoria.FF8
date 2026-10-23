using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModuleImpactPacketStep(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, ImmutableArray<NativeBattleGfGeometryBatch> Batches);

public static class NativeBattleModuleImpactPackets
{
    public static NativeBattleModuleImpactPacketStep Draw570860(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, NativeBattleModuleDraw draw, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleStaticMeshResource? mesh, ImmutableArray<NativeBattleModuleResource> resources, short cameraRoll)
    {
        if (draw.Kind != NativeBattleModuleDrawKind.Impact570860)
        {
            throw new InvalidDataException("Native570860 requires its actual common-module draw.");
        }
        NativeBattleModuleAnchor anchor = draw.Anchor ?? throw new NotSupportedException("Native570860 requires its retained packed position.");
        NativeBattleActionMatrix view = camera.ReadMatrix();
        short phase = unchecked((short)draw.Phase);
        int scaleXY = unchecked((short)draw.Scale);
        int scaleZ = unchecked((short)draw.ImpactScaleZ);
        int offsetX = scaleXY;
        int offsetZ = scaleZ;
        var batches = ImmutableArray.CreateBuilder<NativeBattleGfGeometryBatch>();
        if (phase < 1)
        {
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CD50(new(0, draw.Rotation, draw.Orientation)) with { X = anchor.X, Y = anchor.Y, Z = anchor.Z };
            local = NativeBattlePoseMatrices.Scale56BEF0(local, scaleXY, scaleXY, scaleZ);
            (offsetX, _, offsetZ) = NativeBattleGfMatrices.RotateLong56C600(local, 0, 0, -4096);
            memory = Load(memory, NativeBattleGfMatrices.Compose56C2F0(view, local));
            NativeBattleStaticMeshResource source = mesh ?? throw new NotSupportedException("Native570860 requires its current mesh query5 binding.");
            if (source.Id != 5)
            {
                throw new InvalidDataException("Native570860 requires its actual query5 mesh.");
            }
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
            staging = allocation.Staging;
            NativeBattleEffectArena context = Read(staging, allocation.Address, 88).Write(0, BitConverter.GetBytes(source.Address)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(unchecked(1365 * phase))).Write(28, BitConverter.GetBytes(0xF3u));
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(memory, source, projection, context, colors, 17, 2);
            memory = drawn.Memory;
            staging = NativeBattleTemporaryMemory.PublishView(staging, allocation.Address, drawn.Context, 88);
            staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88);
            batches.Add(drawn.Batch);
        }
        NativeBattleActionMatrix sprite = NativeBattleGfMatrices.Euler56CD50(new(1024, 0, draw.Orientation));
        sprite = sprite with { X = unchecked(anchor.X + (unchecked(200 * offsetX) >> 12)), Y = 0, Z = unchecked(anchor.Z + (unchecked(200 * offsetZ) >> 12)) };
        int scale = unchecked(scaleXY + (scaleXY >> 1));
        sprite = NativeBattlePoseMatrices.Scale56BEF0(sprite, scale, scale, scale);
        memory = Load(memory, NativeBattleGfMatrices.Compose56C2F0(view, sprite));
        NativeBattleModuleResource selected = resources.FirstOrDefault(resource => resource.Id == 31) ?? throw new NotSupportedException("Native570860 requires its current primary sprite slot31.");
        if ((BinaryPrimitives.ReadUInt32LittleEndian(selected.Bytes.AsSpan()) & 0xFF000000) != 0)
        {
            selected = resources.FirstOrDefault(resource => resource.Id == 0) ?? throw new NotSupportedException("Native570860 requires its current fallback sprite slot0.");
        }
        NativeBattleTemporaryAllocation spriteAllocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 180);
        staging = spriteAllocation.Staging;
        NativeBattleEffectArena spriteContext = Read(staging, spriteAllocation.Address, 180).Write(0, BitConverter.GetBytes(selected.Address)).Write(4, BitConverter.GetBytes(draw.Phase)).Write(36, BitConverter.GetBytes((ushort)8));
        NativeBattleModuleSpriteDraw spriteDraw = NativeBattleModuleSpriteDrawing.Draw571C80(memory, spriteContext, address => resources.FirstOrDefault(resource => resource.Address == address) ?? throw new NotSupportedException("Native570860 selected an unbound current sprite resource."), projection, cameraRoll, 17, 2);
        memory = spriteDraw.Memory;
        staging = NativeBattleTemporaryMemory.PublishView(staging, spriteAllocation.Address, spriteDraw.Temporary180, 180);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 180);
        batches.Add(spriteDraw.Batch);
        return new(memory, staging, batches.ToImmutable());
    }

    private static NativeBattleEffectMemory Load(NativeBattleEffectMemory memory, NativeBattleActionMatrix matrix)
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters ?? throw new NotSupportedException("Native570860 requires its current geometry registers."), matrix, true, true, null);
        return memory with { GeometryRegisters = geometry };
    }

    private static NativeBattleEffectArena Read(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
}
