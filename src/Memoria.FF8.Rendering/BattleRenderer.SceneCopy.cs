using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private sealed class SceneReplayContext
    {
        internal readonly Dictionary<long, ushort[]> Words = [];
        internal readonly HashSet<long> Active = [];
        internal readonly HashSet<long> Requested = [];
        internal SceneReplayMemo? Memo;
    }

    private sealed class SceneReplayMemo
    {
        internal required WeakReference<ImmutableDictionary<long, NativeBattleSceneReplaySource>> Sources;
        internal required NativeBattlePrimitiveCapabilities? Capabilities;
        internal required INativeBattleEffectTextureSource? TextureSource;
        internal required long TextureGeneration;
        internal required long[] Dependencies;
        internal readonly Dictionary<long, (ushort[] Words, long[] Dependencies)> Roots = [];
        internal readonly Queue<long> Order = [];

        internal void Add(long id, ushort[] words)
        {
            if (Roots.ContainsKey(id))
            {
                return;
            }
            while (Roots.Count >= 64)
            {
                Roots.Remove(Order.Dequeue());
            }
            Roots.Add(id, (words, Dependencies));
            Order.Enqueue(id);
        }
    }

    private SceneReplayContext sceneReplayContext = new();
    private SceneReplayMemo? sceneReplayMemo;
    private bool[]? sceneReplaySampleColumns;
    private bool[]? sceneReplaySampleRows;
    private bool isSceneReplay;
    private Func<int, int, bool, PixelImage>? sceneCopyUiTexture;
    private readonly record struct SceneCopyTextureKey(PixelImage Image, ImmutableArray<NativeBattleSceneCopyRegion> Regions, NativeBattlePcTextureWindowSelection Window, ushort Page, ImmutableArray<ushort> Palette, int? Blend, NativeBattlePcDirectTextureFormat? Format, byte? PaletteShift);
    private readonly Dictionary<SceneCopyTextureKey, PixelImage> sceneCopyImages = [];
    private long sceneCopyImageBytes;

    private void ConfigureSceneReplaySampling(NativeBattlePreparedPrimitiveFrame frame)
    {
        if (!isSceneReplay || frame.LastPcRenderTail is not null || frame.LastCommands.Any(HasUnprovedReplayMaterial))
        {
            return;
        }
        sceneReplaySampleColumns = new bool[Width];
        sceneReplaySampleRows = new bool[Height];
        for (int x = 0; x < 320; x++)
        {
            int px = Math.Min(Width - 1, (int)(((long)x * 2 + 1) * Width / (320 * 2L)));
            sceneReplaySampleColumns[px] = true;
        }
        for (int y = 0; y < 216; y++)
        {
            int py = Math.Min(Height - 1, (int)(((long)y * 2 + 1) * Height / (216 * 2L)));
            sceneReplaySampleRows[py] = true;
        }
    }

    private static bool HasUnprovedReplayMaterial(NativeBattleLogicalResolvedCommand resolved)
    {
        byte opcode = resolved.Command.Opcode;
        bool drawable = (opcode & 0xE0) == 0x20 || opcode is 0x40 or 0x42 or 0x50 or 0x52 or 0x64 or 0x66 || (opcode & 0xFC) is 0x60 or 0x68;
        bool semi = (opcode & 2) != 0;
        if (!drawable || resolved.Pass == 0 || resolved.Pass == 1 && semi || resolved.Pass == 2 && !semi)
        {
            return false;
        }
        return resolved.DrawMode.BlendMode is not (>= 0 and <= 3);
    }

    private void BeginSceneReplay(NativeBattlePreparedPrimitiveFrame frame, NativeBattlePrimitiveCapabilities? capabilities, IReadOnlyList<NativeBattleTextureReplacement>? replacements, INativeBattleEffectTextureSource? textureSource)
    {
        sceneReplayContext = new();
        ImmutableDictionary<long, NativeBattleSceneReplaySource>? sources = frame.SceneReplaySources;
        bool populatedUi = sources?.Values.Any(source => source.UiFrame is { } ui && (!ui.Opaque.IsEmpty || ui.Semi.Any(batch => !batch.Quads.IsEmpty))) == true;
        if (sources is null || sources.Count > 64 || replacements is { Count: > 0 } || populatedUi)
        {
            sceneReplayMemo = null;
            return;
        }
        long generation = textureSource?.Generation ?? 0;
        if (sceneReplayMemo is not { } memo || !memo.Sources.TryGetTarget(out var previous) || !ReferenceEquals(previous, sources) || memo.Capabilities != capabilities || !ReferenceEquals(memo.TextureSource, textureSource) || memo.TextureGeneration != generation)
        {
            sceneReplayMemo = new()
            {
                Sources = new(sources),
                Capabilities = capabilities,
                TextureSource = textureSource,
                TextureGeneration = generation,
                // With at most64 pool IDs this conservative footprint cannot exceed a valid render's source bound.
                Dependencies = sources.Keys.ToArray()
            };
        }
        sceneReplayContext.Memo = sceneReplayMemo;
    }

    private PixelImage ApplySceneCopyTexture(NativeBattlePreparedPrimitiveFrame frame, NativeBattleLogicalResolvedCommand command, NativeBattleEffectTextureState? textures, NativeBattlePrimitiveCapabilities? capabilities, NativeBattlePrimitiveColorState colors, PixelImage image)
    {
        NativeBattlePcTextureWindowSelection window = command.DrawMode.TextureSelection ?? throw new InvalidDataException("Scene texture sampling requires its selected native window.");
        NativeBattlePcTextureWindowPixels? retained = command.DrawMode.TexturePixelSnapshot is int index
            ? (frame.LastTextureSnapshots ?? throw new InvalidDataException("Scene texture sampling requires its ordered pixels."))[index]
            : null;
        ImmutableArray<NativeBattleSceneCopyRegion>? regions = retained?.SceneCopies ?? (retained is null ? textures?.SceneCopies : null);
        if (regions is null)
        {
            return image;
        }
        ushort page = command.DrawMode.TexturePage ?? throw new InvalidDataException("Scene texture sampling requires its texture page.");
        int depth = page >> 7 & 3;
        int pixelsPerWord = depth == 0 ? 4 : depth == 1 ? 2 : depth == 2 ? 1 : throw new NotSupportedException("Scene texture sampling requires a supported pixel depth.");
        int bits = 16 / pixelsPerWord;
        int mask = (1 << bits) - 1;
        ImmutableArray<ushort> palette = command.DrawMode.SceneCopyPalette ?? throw new InvalidDataException("Scene texture sampling requires its ordered native palette receipt.");
        SceneCopyTextureKey key = new(image, regions.Value, window, page, palette, command.DrawMode.BlendMode, capabilities?.DirectTextureFormat, colors.AlternatePaletteShift);
        if (sceneCopyImages.TryGetValue(key, out PixelImage? cached))
        {
            return cached;
        }
        if (palette.IsDefault || palette.Length != (depth == 0 ? 16 : depth == 1 ? 256 : 0))
        {
            throw new InvalidDataException("Scene texture sampling requires a complete palette for its native depth.");
        }
        long imageBytes = checked((long)image.Width * image.Height * 4);
        if (sceneCopyImageBytes > 64L * 1024 * 1024 - imageBytes)
        {
            throw new NotSupportedException("Scene texture sampling exceeds its bounded decoded image budget.");
        }
        Rgba32[] pixels = image.Pixels.ToArray();
        for (int y = 0; y < image.Height; y++)
        {
            int physicalY = (page & 16) * 16 + ((window.Y + y) & 255);
            for (int x = 0; x < image.Width; x++)
            {
                int u = (window.X + x) & 255;
                int physicalX = (page & 15) * 64 + u / pixelsPerWord;
                NativeBattleSceneCopyRegion? selected = null;
                foreach (NativeBattleSceneCopyRegion region in regions.Value)
                {
                    if (physicalX >= region.X && physicalX < region.X + region.Width && physicalY >= region.Y && physicalY < region.Y + region.Height)
                    {
                        selected = region;
                        break;
                    }
                }
                if (selected is null)
                {
                    continue;
                }
                selected.Validate();
                int sourceX = selected.SourceX + physicalX - selected.X;
                int sourceY = selected.SourceY + physicalY - selected.Y;
                // The extra copied rows outside the native scene retain their canonical VRAM words.
                if (sourceY < selected.Capture.PaddingTop || sourceY >= selected.Capture.Height - selected.Capture.PaddingBottom)
                {
                    continue;
                }
                ushort[] source = ReplaySceneWords(frame, selected.Capture.ReplaySourceId!.Value, capabilities);
                int sampleX = Math.Clamp(sourceX, 0, 319);
                int sampleY = Math.Clamp(sourceY - selected.Capture.PaddingTop, 0, 215);
                ushort word = source[sampleY * 320 + sampleX];
                ushort color = depth == 2 ? word : palette[word >> ((u % pixelsPerWord) * bits) & mask];
                uint argb;
                if (depth == 2)
                {
                    argb = NativeBattlePcDirectTexture.DecodeArgb(color, capabilities?.DirectTextureFormat ?? throw new NotSupportedException("Scene texture sampling requires its direct device format."));
                }
                else if (command.DrawMode.BlendMode == 2)
                {
                    if (colors.SubtractUsesNormalizedColor)
                    {
                        throw new NotSupportedException("Scene texture sampling requires the selected native alpha-palette profile.");
                    }
                    argb = NativeBattlePcPalette.Decode467470(color, colors.AlternatePaletteShift ?? throw new NotSupportedException("Scene texture sampling requires its native palette shift."));
                }
                else
                {
                    argb = NativeBattlePcPalette.Decode467370(color);
                }
                pixels[y * image.Width + x] = new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
            }
        }
        PixelImage result = new(image.Width, image.Height, pixels);
        sceneCopyImages.Add(key, result);
        sceneCopyImageBytes += imageBytes;
        return result;
    }

    private ushort[] ReplaySceneWords(NativeBattlePreparedPrimitiveFrame frame, long id, NativeBattlePrimitiveCapabilities? capabilities)
    {
        if (sceneReplayContext.Words.TryGetValue(id, out ushort[]? cached))
        {
            return cached;
        }
        ImmutableDictionary<long, NativeBattleSceneReplaySource> sources = frame.SceneReplaySources ?? throw new InvalidDataException("Scene texture sampling requires its retained source pool.");
        if (!sources.TryGetValue(id, out NativeBattleSceneReplaySource? source))
        {
            throw new InvalidDataException("Scene texture sampling refers to a missing retained source.");
        }
        bool root = sceneReplayContext.Active.Count == 0;
        if (root && sceneReplayContext.Memo is { } memo && memo.Roots.TryGetValue(id, out var retained))
        {
            sceneReplayContext.Requested.UnionWith(retained.Dependencies);
            if (sceneReplayContext.Requested.Count > 64)
            {
                throw new InvalidDataException("Scene replay requires an acyclic bounded source graph.");
            }
            sceneReplayContext.Words.Add(id, retained.Words);
            return retained.Words;
        }
        sceneReplayContext.Requested.Add(id);
        if (!sceneReplayContext.Active.Add(id) || sceneReplayContext.Requested.Count > 64 || (long)sceneReplayContext.Active.Count * Width * Height * 8 > 256L * 1024 * 1024)
        {
            throw new InvalidDataException("Scene replay requires an acyclic bounded source graph.");
        }
        try
        {
            BattleSceneVisibleArea area = source.VisibleArea;
            if (!double.IsFinite(area.Left) || !double.IsFinite(area.Top) || !double.IsFinite(area.Right) || !double.IsFinite(area.Bottom) || area.Right <= area.Left || area.Bottom <= area.Top)
            {
                throw new InvalidDataException("Scene replay requires its finite capture-time projection domain.");
            }
            int projectionWidth = Math.Clamp((int)Math.Round(Width * 2560 / (area.Right - area.Left)), 1, Width);
            int projectionHeight = Math.Clamp((int)Math.Round(Height * 1728 / (area.Bottom - area.Top)), 1, Height);
            int projectionX = Math.Clamp((int)Math.Round(-area.Left * projectionWidth / 2560), 0, Width - projectionWidth);
            int projectionY = Math.Clamp((int)Math.Round(-area.Top * projectionHeight / 1728), 0, Height - projectionHeight);
            BattleRenderer renderer = new(Width, Height, new PixelBounds(projectionX, projectionY, projectionWidth, projectionHeight))
            {
                isSceneReplay = true,
                sceneReplayContext = sceneReplayContext
            };
            NativeBattlePreparedPrimitiveFrame sourceFrame = source.Frame with { SceneReplaySources = sources };
            renderer.ConfigureSceneReplaySampling(sourceFrame);
            byte[] rgba = renderer.RenderNativePrimitiveFrame(sourceFrame, source.Textures, capabilities: capabilities, replacements: nativeModelReplacements, uiFrame: source.UiFrame, uiTexture: sceneCopyUiTexture, effectTextureSource: nativeEffectTextureSource);
            ushort[] words = new ushort[320 * 216];
            for (int y = 0; y < 216; y++)
            {
                int py = Math.Min(Height - 1, (int)(((long)y * 2 + 1) * Height / (216 * 2L)));
                for (int x = 0; x < 320; x++)
                {
                    int px = Math.Min(Width - 1, (int)(((long)x * 2 + 1) * Width / (320 * 2L)));
                    int offset = (py * Width + px) * 4;
                    words[y * 320 + x] = (ushort)((rgba[offset] >> 3) | (rgba[offset + 1] >> 3) << 5 | (rgba[offset + 2] >> 3) << 10);
                }
            }
            sceneReplayContext.Words.Add(id, words);
            if (root)
            {
                sceneReplayContext.Memo?.Add(id, words);
            }
            return words;
        }
        finally
        {
            sceneReplayContext.Active.Remove(id);
        }
    }
}
