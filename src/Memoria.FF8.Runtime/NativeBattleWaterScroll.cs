using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterScrollPayload(short TextureId, short DestinationX, short DestinationY);
public sealed record NativeBattleWaterRectangleWrite(int RingIndex, byte NextCounter, ImmutableArray<byte> RectangleBytes, NativeBattleWater222UploadPlan Upload);
public sealed record NativeBattleWaterScrollStep(byte Counter43, ImmutableArray<NativeBattleWaterRectangleWrite> Writes);

/// <summary>Selected vertical texture transfer over explicit current resource and retained rectangle-ring owners.</summary>
public static class NativeBattleWaterScroll
{
    public static NativeBattleWaterScrollStep DrawA9C140(INativeBattleScriptedTextureProfile profile, ImmutableArray<NativeBattleEffectHeapAllocation> currentFiles, NativeBattleWaterScrollPayload payload, short scroll96, byte counter43, Action<NativeBattleWaterRectangleWrite> submit)
    {
        NativeBattleWater222UploadPlan source = NativeBattleWater222TexturePair.Resolve(profile, currentFiles, payload.TextureId, false);
        int row = NativeBattleCinematicScroll.ComputeRow(source.Rectangle, scroll96);
        ImmutableArray<NativeBattleWaterRectangleWrite>.Builder writes = ImmutableArray.CreateBuilder<NativeBattleWaterRectangleWrite>();
        NativeBattleCinematicScroll.EmitGeometry(source.Rectangle, payload, checked((uint)source.Offset), row, (rectangle, address) => Emit(rectangle, checked((int)address)));
        return new(counter43, writes.ToImmutable());

        void Emit(NativeBattleEffectTextureRectangle rectangle, int offset)
        {
            int index = counter43 & 15;
            counter43 = unchecked((byte)(counter43 + 1));
            byte[] bytes = NativeBattleCinematicScroll.EncodeRectangle(rectangle);
            NativeBattleWaterRectangleWrite write = new(index, counter43, bytes.ToImmutableArray(), new(source.SourceIndex, offset, rectangle));
            submit(write);
            writes.Add(write);
        }
    }

    public static NativeBattleEffectTextureState EnqueueArena505DF0(NativeBattleEffectTextureState textures, NativeBattleEffectArena arena, int file0ArenaOffset, int file1ArenaOffset, NativeBattleWaterRectangleWrite write)
    {
        int fileOffset = write.Upload.SourceIndex switch
        {
            0 => file0ArenaOffset,
            1 => file1ArenaOffset,
            _ => throw new NotSupportedException("Native Water scroll source leaves its two current resource owners.")
        };
        return textures.EnqueueArenaRectangle505DF0(write.Upload.Rectangle, arena, checked(fileOffset + write.Upload.Offset));
    }
}
