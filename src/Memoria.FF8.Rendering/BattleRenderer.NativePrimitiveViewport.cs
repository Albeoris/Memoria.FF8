using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private readonly struct NativeBattlePrimitiveViewportProjector
    {
        private readonly PixelBounds bounds;
        private readonly float unit;
        private readonly float scaleX;
        private readonly float scaleY;
        private readonly float offsetX;
        private readonly float offsetY;
        private readonly int width;
        private readonly int height;
        private readonly bool hasNormalViewport;

        internal NativeBattlePrimitiveViewportProjector(PixelBounds bounds, uint flags, NativeBattlePrimitiveDrawMode mode)
        {
            this.bounds = bounds;
            bool full = (flags & 1) != 0;
            unit = full ? 1 : 0.125f;
            if (mode.NormalViewport is not null || mode.ViewportInputs is not null)
            {
                NativeSoftwareNormalViewportDerived viewport = mode.NormalViewport ?? throw new NotSupportedException("Native primitive viewport requires its retained derived outputs.");
                NativeSoftwareViewportInputs inputs = mode.ViewportInputs ?? throw new NotSupportedException("Native primitive viewport requires its actual input dimensions.");
                if (inputs.Width <= 0 || inputs.Height <= 0)
                {
                    throw new InvalidDataException("Native primitive viewport requires positive dimensions.");
                }
                hasNormalViewport = true;
                width = inputs.Width;
                height = inputs.Height;
                scaleX = full ? viewport.ScaleX : viewport.LowCoordinateScaleX;
                scaleY = full ? viewport.ScaleY : viewport.LowCoordinateScaleY;
                offsetX = viewport.NormalOffsetX;
                offsetY = viewport.NormalOffsetY;
            }
            IsPrepared = true;
        }

        internal bool IsPrepared { get; }

        internal Vector2 Project(double x, double y)
        {
            if (hasNormalViewport)
            {
                float nativeX = (float)(x * scaleX + offsetX);
                float nativeY = (float)(y * scaleY + offsetY);
                return new(bounds.X + (float)(((double)nativeX + 0.5) * bounds.Width / width), bounds.Y + (float)(((double)nativeY + 0.5) * bounds.Height / height));
            }
            return new(bounds.X + (float)x * unit * bounds.Width / 320, bounds.Y + (float)y * unit * bounds.Height / 216);
        }
    }
}
