using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattlePrimitiveDepthVertex(float Depth, float ReciprocalW);

/// <summary>Retained460B60 blend of original proxy vertex depth and45D310 ordering-table depth.</summary>
public sealed record NativeBattlePrimitiveDepthState(float BucketMix, float RemainingWeight)
{
    public const float DepthBias = 0.0015f;
    public const float VertexDepthScale = 1.5235901E-05f;
    public static NativeBattlePrimitiveDepthState FromColdProcess() => new(0, 0.99f);

    public NativeBattlePrimitiveDepthState SetBucketMix460B60(float value)
    {
        //Original comparison also clamps NaN to zero through its failed >=0 branch.
        double selected = value >= 0 ? Math.Min((double)value, 1) : 0;
        return new((float)selected, (float)(1 - selected));
    }

    public ImmutableArray<NativeBattlePrimitiveDepthVertex> Project461220(int bucket, bool descriptorPerspective, ushort flags20, ImmutableArray<int> vertexDepths)
    {
        NativeBattlePrimitiveDepthVertex[] result = new NativeBattlePrimitiveDepthVertex[4];
        Project461220(bucket, descriptorPerspective, flags20, vertexDepths, result);
        return ImmutableCollectionsMarshal.AsImmutableArray(result);
    }

    public void Project461220(int bucket, bool descriptorPerspective, ushort flags20, ImmutableArray<int> vertexDepths, Span<NativeBattlePrimitiveDepthVertex> output)
    {
        if (output.Length != 4)
        {
            throw new ArgumentException("Native461220 requires four output corners.", nameof(output));
        }
        if ((uint)bucket >= NativeBattlePrimitiveOrdering.BattleTableLength || vertexDepths.IsDefault || vertexDepths.Length is not (0 or 4) || !float.IsFinite(BucketMix) || !float.IsFinite(RemainingWeight))
        {
            throw new InvalidDataException("Native461220 requires its actual OT bucket, bounded proxy depths and retained depth blend.");
        }
        float step = (float)(0.9985 / (2 * NativeBattlePrimitiveOrdering.BattleTableLength));
        step = Math.Max(step, 1f / 65536);
        float bucketDepth = (float)((double)bucket * step);
        if (!descriptorPerspective || vertexDepths.IsEmpty || vertexDepths[0] == 0)
        {
            float depth = (float)((double)bucketDepth + DepthBias);
            output.Fill(new(depth, 1));
            return;
        }
        //45D310 invokes460B20(65536) before dispatching its primitives.
        float vertexWeight = (float)((RemainingWeight < 0.99999999 ? RemainingWeight : (double)RemainingWeight - DepthBias) / 65536);
        double weightedBucket = (double)BucketMix * bucketDepth;
        for (int corner = 0; corner < output.Length; corner++)
        {
            int vertex = vertexDepths[corner];
            float depth = (flags20 & 4) != 0 ? (float)((double)vertex * VertexDepthScale + DepthBias) : (float)((double)vertexWeight * vertex + weightedBucket + DepthBias);
            output[corner] = new(depth, (float)(1d / vertex));
        }
    }
}
