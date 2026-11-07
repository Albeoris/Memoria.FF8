using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private const int MaximumSceneCaptureImages = 64;
    private const long MaximumSceneCaptureImageBytes = 64L * 1024 * 1024;
    private readonly record struct SceneCaptureImageKey(ImmutableArray<NativeBattlePcTextureWindowPixels> Snapshots, NativeBattleSceneCaptureSample Sample, int Depth, int? Blend, NativeBattlePcDirectTextureFormat? Format, byte? PaletteShift, bool SubtractUsesNormalizedColor);
    private sealed record SceneCaptureImage(SceneCaptureImageKey Key, PixelImage Image, long Bytes);
    private sealed class SceneCaptureSnapshotOwner(long bytes)
    {
        public long Bytes { get; } = bytes;
        public int References { get; set; }
    }
    private readonly Dictionary<SceneCaptureImageKey, LinkedListNode<SceneCaptureImage>> sceneCaptureImages = [];
    private readonly Dictionary<ImmutableArray<NativeBattlePcTextureWindowPixels>, SceneCaptureSnapshotOwner> sceneCaptureSnapshotOwners = [];
    private readonly LinkedList<SceneCaptureImage> recentSceneCaptureImages = [];
    private long sceneCaptureRetainedBytes;

    private PixelImage ResolveSceneCaptureTexture(NativeBattlePreparedPrimitiveFrame frame, NativeBattleLogicalResolvedCommand command, NativeBattlePrimitiveCapabilities? capabilities, NativeBattlePrimitiveColorState colors)
    {
        NativeBattleSceneCaptureSample sample = command.DrawMode.SceneCaptureSample ?? throw new InvalidDataException("Scene sampling requires its retained source pixels.");
        sample.Capture.Validate();
        ushort page = command.DrawMode.TexturePage ?? throw new InvalidDataException("Scene sampling requires its native texture page.");
        int depth = page >> 7 & 3;
        int pixelsPerWord = depth == 0 ? 4 : depth == 1 ? 2 : depth == 2 ? 1 : throw new NotSupportedException("Scene sampling requires a supported native texture depth.");
        int paletteLength = depth == 0 ? 16 : depth == 1 ? 256 : 0;
        ImmutableArray<NativeBattlePcTextureWindowPixels> snapshots = frame.LastTextureSnapshots ?? throw new InvalidDataException("Scene sampling requires retained ordered texture snapshots.");
        if (sample.PixelSnapshots.IsDefault || sample.PixelSnapshots.Length != (sample.Capture.Width + 255) / 256 || sample.Palette.IsDefault || sample.Palette.Length != paletteLength)
        {
            throw new InvalidDataException("Scene sampling requires every source page and its native palette.");
        }
        SceneCaptureImageKey key = new(snapshots, sample, depth, command.DrawMode.BlendMode, capabilities?.DirectTextureFormat, colors.AlternatePaletteShift, colors.SubtractUsesNormalizedColor);
        if (sceneCaptureImages.TryGetValue(key, out LinkedListNode<SceneCaptureImage>? cached))
        {
            recentSceneCaptureImages.Remove(cached);
            recentSceneCaptureImages.AddLast(cached);
            return cached.Value.Image;
        }
        Rgba32[] palette = new Rgba32[paletteLength];
        for (int index = 0; index < palette.Length; index++)
        {
            uint argb;
            if (command.DrawMode.BlendMode == 2)
            {
                if (colors.SubtractUsesNormalizedColor)
                {
                    throw new NotSupportedException("Scene sampling requires the selected native alpha-palette profile.");
                }
                argb = NativeBattlePcPalette.Decode467470(sample.Palette[index], colors.AlternatePaletteShift ?? throw new NotSupportedException("Scene sampling requires its native alternate palette shift."));
            }
            else
            {
                argb = NativeBattlePcPalette.Decode467370(sample.Palette[index]);
            }
            palette[index] = new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
        }
        Span<ulong> packedPalette = depth == 0 ? stackalloc ulong[256] : Span<ulong>.Empty;
        if (depth == 0)
        {
            Span<Rgba32> pair = stackalloc Rgba32[2];
            for (int index = 0; index < packedPalette.Length; index++)
            {
                pair[0] = palette[index & 15];
                pair[1] = palette[index >> 4];
                packedPalette[index] = MemoryMarshal.Read<ulong>(MemoryMarshal.AsBytes(pair));
            }
        }
        PixelImage image = new(sample.Capture.Width * pixelsPerWord, sample.Capture.Height);
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
            ReadOnlySpan<byte> data = pixels.Data.AsSpan();
            for (int y = 0; y < pixels.Height; y++)
            {
                ReadOnlySpan<byte> row = data.Slice(y * pixels.Width * 2, pixels.Width * 2);
                Span<Rgba32> destination = image.Pixels.AsSpan(y * image.Width + left * pixelsPerWord, pixels.Width * pixelsPerWord);
                if (depth == 0)
                {
                    Span<ulong> pairs = MemoryMarshal.Cast<Rgba32, ulong>(destination);
                    for (int index = 0; index < row.Length; index++)
                    {
                        pairs[index] = packedPalette[row[index]];
                    }
                }
                else if (depth == 1)
                {
                    for (int index = 0; index < row.Length; index++)
                    {
                        destination[index] = palette[row[index]];
                    }
                }
                else
                {
                    NativeBattlePcDirectTextureFormat format = capabilities?.DirectTextureFormat ?? throw new NotSupportedException("Scene sampling requires its selected direct texture format.");
                    for (int index = 0; index < pixels.Width; index++)
                    {
                        ushort word = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(index * 2, 2));
                        uint argb = NativeBattlePcDirectTexture.DecodeArgb(word, format);
                        destination[index] = new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
                    }
                }
            }
        }
        RetainSceneCaptureImage(key, image);
        return image;
    }

    private void RetainSceneCaptureImage(SceneCaptureImageKey key, PixelImage image)
    {
        long imageBytes = checked((long)image.Pixels.Length * 4);
        // Each snapshot array retains its complete payload, shared by all of its decoded images.
        long ownerBytes = sceneCaptureSnapshotOwners.TryGetValue(key.Snapshots, out SceneCaptureSnapshotOwner? knownOwner) ? knownOwner.Bytes : key.Snapshots.Sum(pixels => (long)pixels.Data.Length);
        if (imageBytes > MaximumSceneCaptureImageBytes - ownerBytes)
        {
            return;
        }
        while (sceneCaptureImages.Count >= MaximumSceneCaptureImages || sceneCaptureRetainedBytes > MaximumSceneCaptureImageBytes - imageBytes - (sceneCaptureSnapshotOwners.ContainsKey(key.Snapshots) ? 0 : ownerBytes))
        {
            LinkedListNode<SceneCaptureImage> oldest = recentSceneCaptureImages.First!;
            recentSceneCaptureImages.RemoveFirst();
            sceneCaptureImages.Remove(oldest.Value.Key);
            sceneCaptureRetainedBytes -= oldest.Value.Bytes;
            SceneCaptureSnapshotOwner owner = sceneCaptureSnapshotOwners[oldest.Value.Key.Snapshots];
            owner.References--;
            if (owner.References == 0)
            {
                sceneCaptureSnapshotOwners.Remove(oldest.Value.Key.Snapshots);
                sceneCaptureRetainedBytes -= owner.Bytes;
            }
        }
        if (!sceneCaptureSnapshotOwners.TryGetValue(key.Snapshots, out SceneCaptureSnapshotOwner? retainedOwner))
        {
            retainedOwner = new(ownerBytes);
            sceneCaptureSnapshotOwners.Add(key.Snapshots, retainedOwner);
            sceneCaptureRetainedBytes += ownerBytes;
        }
        retainedOwner.References++;
        SceneCaptureImage retained = new(key, image, imageBytes);
        sceneCaptureImages.Add(key, recentSceneCaptureImages.AddLast(retained));
        sceneCaptureRetainedBytes += imageBytes;
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
