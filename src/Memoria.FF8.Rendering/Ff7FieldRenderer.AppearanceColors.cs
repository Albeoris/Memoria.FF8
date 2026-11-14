using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7FieldRenderer
{
    public static bool[] CaptureAppearanceVertexColorMask(IFf7ActorAppearance appearance, Ff7ActorVisual pose, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, Ff7FieldWaterColorRegion region)
    {
        region.Validate();
        if ((pose.AnimationFrame16 & 15) != 0 || pose.AnimationBlend is not null)
        {
            throw new InvalidDataException("Appearance vertex colors require a committed native pose, independent of presentation interpolation.");
        }
        if (!appearance.SupportsVertexColorBindings || !appearance.TrySampleVertices(pose, descriptor, original, out IReadOnlyList<Vector3> positions))
        {
            throw new NotSupportedException("The selected appearance requires an explicit committed-pose vertex-color sampling binding.");
        }
        return CaptureAppearanceVertexColorMask(positions, region);
    }

    public static bool[] CaptureAppearanceVertexColorMask(IReadOnlyList<Vector3> positions, Ff7FieldWaterColorRegion region)
    {
        region.Validate();
        bool[] modified = new bool[positions.Count];
        for (int vertex = 0; vertex < positions.Count; vertex++)
        {
            Vector3 position = positions[vertex];
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            {
                throw new InvalidDataException("Appearance vertex colors require finite sampled world positions.");
            }
            modified[vertex] = region.Contains(position);
        }
        return modified;
    }
}
