using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlizzardFacePacketContext(uint PrimaryColor, uint Flags, uint? FarColor, int? Fade, int VOffset, NativeBattleTextureWindow Before, NativeBattleTextureWindow After, int TableBaseBucket, byte DepthShift);

/// <summary>61B9A0's FT3 stores after the controller's original face motion and RTPT; no face-bank or random state is owned here.</summary>
public static class NativeBattleBlizzardFacePackets
{
    public static NativeBattleGfGeometryBatch EmitFace61B9A0(ImmutableArray<byte> primaryFace, ImmutableArray<byte> partnerFace, ImmutableArray<NativeBattleModuleProjectedVertex> corners, short age, short duration, NativeBattleBlizzardFacePacketContext context, NativeBattleSummonColors? colors = null)
    {
        if (primaryFace.IsDefault || primaryFace.Length != 20 || corners.IsDefault || corners.Length != 3 || context.DepthShift > 31)
        {
            throw new InvalidDataException("Native61B9A0 requires two original20-byte FT3 records, three actual projected corners and its depth shift.");
        }
        Span<byte> source = stackalloc byte[20];
        NativeBattleTelemetry.Copy(1224, primaryFace.AsSpan(), source);
        //The controller supplies either61BC56's stationary color or the moving branch's updated-age gray command26.
        BinaryPrimitives.WriteUInt32LittleEndian(source, context.PrimaryColor);
        NativeBattleEffectMeshPacketContext shared = new(context.Flags & 0x41, context.FarColor, context.Fade, null, null, 0, false, null, context.TableBaseBucket, context.DepthShift);
        NativeBattleGfGeometryBatch first = NativeBattleEffectMeshPackets.EmitTextured(source, corner => corners[corner], false, false, shared, colors, 0, NativeBattleEffectMeshClip.None);
        if (first.AllocationBytes == 0 || age < duration)
        {
            return first;
        }
        if (partnerFace.IsDefault || partnerFace.Length != 20 || context.Before is null || context.After is null)
        {
            throw new NotSupportedException("Native61B9A0 paired FT3 requires its original partner face and both explicit texture-window rectangles.");
        }

        NativeBattleGeometryPrimitive primary = first.Attempts[^1].Primitive ?? throw new InvalidDataException("Native61B9A0 accepted FT3 lacks its original linked primitive.");
        uint firstUv = BinaryPrimitives.ReadUInt32LittleEndian(partnerFace.AsSpan()[12..]);
        uint secondUv = BinaryPrimitives.ReadUInt32LittleEndian(partnerFace.AsSpan()[16..]);
        uint finalUv = BinaryPrimitives.ReadUInt16LittleEndian(partnerFace.AsSpan()[10..]);
        if (context.VOffset != 0)
        {
            ImmutableArray<byte> shifted = NativeBattleEffectTextureWindows.OffsetAxisSigned([(byte)(firstUv >> 8), (byte)(secondUv >> 8), (byte)(finalUv >> 8)], context.VOffset, context.Before.Height);
            firstUv = (firstUv & 0xFFFF00FFu) | (uint)shifted[0] << 8;
            secondUv = (secondUv & 0xFFFF00FFu) | (uint)shifted[1] << 8;
            finalUv = (finalUv & 0xFFFF00FFu) | (uint)shifted[2] << 8;
        }
        SkinWrites composite = new(40, 32);
        composite.Word(0, 0x09000000);
        composite.Word(4, NativeBattleEffectTextureWindows.Encode(context.Before));
        composite.Word(8, primary.Words[1] | 0x02000000);
        composite.Word(12, primary.Words[2]);
        composite.Word(16, firstUv);
        composite.Word(20, primary.Words[4]);
        composite.Word(24, secondUv);
        composite.Word(28, primary.Words[6]);
        composite.Word(32, finalUv);
        composite.Word(36, NativeBattleEffectTextureWindows.Encode(context.After));
        int relativeDepth = unchecked(primary.Bucket - context.TableBaseBucket);
        int bucket = relativeDepth > 0 ? primary.Bucket - 1 : primary.Bucket;
        NativeBattleGeometryPrimitive paired = new(bucket, composite.Words(), [0, 0, 0, 0]);
        return new(first.Attempts.Add(composite.Attempt(paired)), 72);
    }
}
