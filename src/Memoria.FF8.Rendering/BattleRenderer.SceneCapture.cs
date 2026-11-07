using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private readonly Dictionary<(NativeBattleSceneCaptureSample Sample, int Depth, int? Blend, NativeBattlePcDirectTextureFormat? Format, byte? PaletteShift), PixelImage> sceneCaptureImages = [];

    private PixelImage ResolveSceneCaptureTexture(NativeBattlePreparedPrimitiveFrame frame, NativeBattleLogicalResolvedCommand command, NativeBattlePrimitiveCapabilities? capabilities, NativeBattlePrimitiveColorState colors)
    {
        NativeBattleSceneCaptureSample sample = command.DrawMode.SceneCaptureSample ?? throw new InvalidDataException("Scene sampling requires its retained source pixels.");
        sample.Capture.Validate();
        ushort page = command.DrawMode.TexturePage ?? throw new InvalidDataException("Scene sampling requires its native texture page.");
        int depth = page >> 7 & 3;
        var key = (sample, depth, command.DrawMode.BlendMode, capabilities?.DirectTextureFormat, colors.AlternatePaletteShift);
        if (sceneCaptureImages.TryGetValue(key, out PixelImage? image))
        {
            return image;
        }
        ushort[]? replay = sample.Capture.ReplaySourceId is long sourceId ? ReplaySceneWords(frame, sourceId, capabilities) : null;
        int pixelsPerWord = depth == 0 ? 4 : depth == 1 ? 2 : depth == 2 ? 1 : throw new NotSupportedException("Scene sampling requires a supported native texture depth.");
        int paletteLength = depth == 0 ? 16 : depth == 1 ? 256 : 0;
        ImmutableArray<NativeBattlePcTextureWindowPixels> snapshots = frame.LastTextureSnapshots ?? throw new InvalidDataException("Scene sampling requires retained ordered texture snapshots.");
        if (sample.PixelSnapshots.IsDefault || sample.PixelSnapshots.Length != (sample.Capture.Width + 255) / 256 || sample.Palette.IsDefault || sample.Palette.Length != paletteLength)
        {
            throw new InvalidDataException("Scene sampling requires every source page and its native palette.");
        }
        image = new(sample.Capture.Width * pixelsPerWord, sample.Capture.Height);
        int mask = (1 << (16 / pixelsPerWord)) - 1;
        for (int part = 0; part < sample.PixelSnapshots.Length; part++)
        {
            int snapshotIndex = sample.PixelSnapshots[part];
            if ((uint)snapshotIndex >= snapshots.Length)
            {
                throw new InvalidDataException("Scene sampling refers outside its retained texture snapshots.");
            }
            NativeBattlePcTextureWindowPixels pixels = snapshots[snapshotIndex];
            pixels.Validate();
            int left = part * 256;
            if (pixels.X != sample.Capture.X + left || pixels.Y != sample.Capture.Y || pixels.Width != Math.Min(256, sample.Capture.Width - left) || pixels.Height != sample.Capture.Height)
            {
                throw new InvalidDataException("Scene sampling retained an incomplete or differently mapped source page.");
            }
            for (int y = 0; y < pixels.Height; y++)
            {
                for (int x = 0; x < pixels.Width; x++)
                {
                    ushort word = replay is not null && y >= sample.Capture.PaddingTop && y < sample.Capture.Height - sample.Capture.PaddingBottom
                        ? replay[(y - sample.Capture.PaddingTop) * 320 + left + x]
                        : pixels.ReadWord(pixels.X + x, pixels.Y + y);
                    for (int subpixel = 0; subpixel < pixelsPerWord; subpixel++)
                    {
                        ushort color = depth == 2 ? word : sample.Palette[word >> (subpixel * (16 / pixelsPerWord)) & mask];
                        uint argb;
                        if (depth == 2)
                        {
                            argb = NativeBattlePcDirectTexture.DecodeArgb(color, capabilities?.DirectTextureFormat ?? throw new NotSupportedException("Scene sampling requires its selected direct texture format."));
                        }
                        else if (command.DrawMode.BlendMode == 2)
                        {
                            if (colors.SubtractUsesNormalizedColor)
                            {
                                throw new NotSupportedException("Scene sampling requires the selected native alpha-palette profile.");
                            }
                            argb = NativeBattlePcPalette.Decode467470(color, colors.AlternatePaletteShift ?? throw new NotSupportedException("Scene sampling requires its native alternate palette shift."));
                        }
                        else
                        {
                            argb = NativeBattlePcPalette.Decode467370(color);
                        }
                        image.Pixels[y * image.Width + (left + x) * pixelsPerWord + subpixel] = new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
                    }
                }
            }
        }
        sceneCaptureImages.Add(key, image);
        return image;
    }

    private static Vector2 SceneCaptureUv(NativeBattleSceneCaptureSample sample, ushort page, uint uv, BattleSceneScreenPoint? samplePoint = null)
    {
        int depth = page >> 7 & 3;
        int pixelsPerWord = depth == 0 ? 4 : depth == 1 ? 2 : depth == 2 ? 1 : throw new NotSupportedException("Scene sampling requires a supported native texture depth.");
        double x = samplePoint is { } point ? point.X / 8d : (page & 15) * 64 - sample.Capture.X + (byte)uv / (double)pixelsPerWord;
        double y = samplePoint is { } value ? value.Y / 8d : (page & 16) * 16 - sample.Capture.Y + (byte)(uv >> 8);
        BattleSceneVisibleArea source = sample.Capture.Source;
        double sourceY = sample.Capture.PaddingTop + (y * 8 - sample.Capture.ProjectionOffsetY * 8 - source.Top) * 216 / (source.Bottom - source.Top);
        return new((float)((x * 8 - source.Left) / (source.Right - source.Left)), (float)(sourceY / sample.Capture.Height));
    }
}
