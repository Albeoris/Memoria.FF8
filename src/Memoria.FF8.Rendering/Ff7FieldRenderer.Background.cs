using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7FieldRenderer
{
    private Vector2 BackgroundTileOrigin(PcFieldTile tile, Ff7FieldRenderFrame frame)
    {
        if (tile.Layer is not (2 or 3))
        {
            return new(160 + tile.DestinationX + frame.CameraOffsetPixels.X, 120 + tile.DestinationY + frame.CameraOffsetPixels.Y);
        }
        int index = (tile.Layer - 2) * 2;
        IReadOnlyList<short> dimensions = field.Triggers.LayerDimensions;
        IReadOnlyList<short> factors = field.Triggers.LayerCameraFactors;
        if (factors.Count is not (0 or 4))
        {
            throw new InvalidDataException("FFVII background camera factors require four native signed words.");
        }
        short factorX = factors.Count == 0 ? (short)256 : factors[index];
        short factorY = factors.Count == 0 ? (short)256 : factors[index + 1];
        short phaseX = tile.Layer == 2 ? frame.Layer2ScrollX16 : frame.Layer3ScrollX16;
        short phaseY = tile.Layer == 2 ? frame.Layer2ScrollY16 : frame.Layer3ScrollY16;
        float cameraX = BackgroundCameraAxis(frame.CameraOffsetPixels.X - frame.BackgroundCameraShakePixels.X, factorX, phaseX, dimensions.Count > index ? dimensions[index] : 0) - frame.BackgroundCameraShakePixels.X;
        float cameraY = BackgroundCameraAxis(frame.CameraOffsetPixels.Y - frame.BackgroundCameraShakePixels.Y, factorY, phaseY, dimensions.Count > index + 1 ? dimensions[index + 1] : 0) - frame.BackgroundCameraShakePixels.Y;
        int x = tile.DestinationX;
        int y = tile.DestinationY;
        if (dimensions.Count > index && dimensions[index] > 0)
        {
            float originX = 160 + cameraX;
            if (x <= originX - 352 || x >= originX)
            {
                x = unchecked((short)(x + (x < originX - 160 ? dimensions[index] : -dimensions[index])));
            }
        }
        if (dimensions.Count > index + 1 && dimensions[index + 1] > 0)
        {
            float originY = 112 + cameraY;
            if (y <= originY - 256 || y >= originY)
            {
                float threshold = tile.Layer == 2 ? originY - 112 : originY;
                y = unchecked((short)(y + (y < threshold ? dimensions[index + 1] : -dimensions[index + 1])));
            }
        }
        return new(160 + x - cameraX, 120 + y - cameraY);
    }

    private static float BackgroundCameraAxis(float screenOffset, short factor, short phase, int dimension)
    {
        float nativeCamera = -screenOffset;
        int lower = checked((int)MathF.Floor(nativeCamera));
        int first = unchecked((short)((lower * factor) >> 8));
        int next = unchecked((short)(((lower + 1) * factor) >> 8));
        float scaled = first + (next - first) * (nativeCamera - lower) + (phase >> 4);
        scaled -= MathF.Floor((scaled + 32768) / 65536) * 65536;
        return dimension > 0 ? scaled % dimension : scaled;
    }
}
