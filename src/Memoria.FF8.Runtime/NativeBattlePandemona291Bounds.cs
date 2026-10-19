using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattlePandemona291Bounds(short Maximum, short Minimum);

public static class NativeBattlePandemona291ActorBounds
{
    public static NativeBattlePandemona291Bounds Capture6ED770(ImmutableArray<NativeBattleRawMesh> meshes, uint objectMask, byte modelId4, Func<NativeBattleRawVertex, short> projectIr2)
    {
        if (meshes.IsDefault)
        {
            throw new NotSupportedException("Native Pandemona bounds require the actual current actor mesh directory.");
        }
        int minimum = 65536;
        int maximum = -65536;
        for (int meshIndex = 0; meshIndex < meshes.Length; meshIndex++)
        {
            if ((objectMask & (1u << (meshIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = meshes[meshIndex];
            mesh.Validate();
            foreach (NativeBattleRawVertex vertex in mesh.Vertices)
            {
                short value = projectIr2(vertex);
                if (value < minimum)
                {
                    minimum = value;
                }
                if (value > maximum)
                {
                    maximum = value;
                }
            }
        }
        if (modelId4 == 0x8E)
        {
            maximum = 0;
        }
        return new(unchecked((short)maximum), unchecked((short)minimum));
    }
}
