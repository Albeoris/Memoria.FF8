using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    private WorldTextureAnimationSequence[]? waterAnimations;
    private WorldTextureAnimationSequence[]? paletteAnimations;
    private readonly Dictionary<string, AssetHandle<TextureValue>> animatedWaterAtlases = [];
    private readonly Dictionary<string, TextureKey> animatedWaterTiles = [];
    private readonly Dictionary<int, TimImage> waterSources = [];
    private readonly Dictionary<int, AssetHandle<TextureValue>> currentWaterAtlases = [];
    private NativeWorldTextureAnimationState? currentWaterAnimation;
    private bool waterAnimationPrepared;

    private PixelImage ReadWaterTexture(int selector, NativeWorldTextureAnimationState? animation)
    {
        animation?.Validate();
        if (!waterAnimationPrepared || currentWaterAnimation != animation)
        {
            currentWaterAtlases.Clear();
            currentWaterAnimation = animation;
            waterAnimationPrepared = true;
        }
        if (currentWaterAtlases.TryGetValue(selector, out AssetHandle<TextureValue>? current))
        {
            return current.Value.Image;
        }
        current = PrepareWaterTexture(selector, animation);
        currentWaterAtlases[selector] = current;
        return current.Value.Image;
    }

    private AssetHandle<TextureValue> PrepareWaterTexture(int selector, NativeWorldTextureAnimationState? animation)
    {
        waterAnimations ??= ReadTextureAnimationTable(16, false);
        paletteAnimations ??= ReadTextureAnimationTable(40, true);
        int[] frames = waterAnimations.Select(sequence => animation is null ? -1 : sequence.SelectFrame(animation.TextureClock)).ToArray();
        int paletteEntry = Array.FindIndex(paletteAnimations, sequence => sequence.DestinationX == 768 && sequence.DestinationY == 192 + selector);
        int paletteFrame = animation is null || paletteEntry < 0 ? -1 : paletteAnimations[paletteEntry].SelectFrame(animation.PaletteClock, palette: true);
        string phase = selector + "/" + string.Join('-', frames) + "/" + paletteFrame;
        if (animatedWaterAtlases.TryGetValue(phase, out AssetHandle<TextureValue>? cached))
        {
            return cached;
        }
        int[] members = [20, 16, 21, 22, 17, 18, 19, 23];
        TimImage paletteSource = members.Select(ReadWaterSource).SingleOrDefault(source => source.Clut?.X == 768 && source.Clut.Y == 192 + selector) ?? throw new NotSupportedException($"World water CLUT selector {selector} has no original palette row.");
        List<TextureKey> dependencies = [];
        List<(int X, int Y)> positions = [];
        for (int index = 0; index < members.Length; index++)
        {
            int member = members[index];
            TimImage source = ReadWaterSource(member);
            if (source.PixelMode != TimPixelMode.Indexed8 || source.Clut is not { Width: 256, Height: 1 } clut)
            {
                throw new NotSupportedException("Animated world water requires its original indexed8 image and single palette row.");
            }
            List<int> overlays = [];
            for (int entry = 0; entry < waterAnimations.Length; entry++)
            {
                WorldTextureAnimationSequence sequence = waterAnimations[entry];
                if (animation is not null && sequence.DestinationX >= source.X && sequence.DestinationX < source.X + source.WidthInWords && sequence.DestinationY >= source.Y && sequence.DestinationY < source.Y + source.Height)
                {
                    overlays.Add(entry);
                }
            }
            string tilePhase = member + "/clut/" + selector + "/" + string.Join('-', overlays.Select(entry => frames[entry])) + "/" + paletteFrame;
            if (!animatedWaterTiles.TryGetValue(tilePhase, out TextureKey key))
            {
                Rgba32[] colors = paletteSource.Palettes[0].Colors;
                if (paletteFrame >= 0)
                {
                    int offset = paletteAnimations[paletteEntry].FrameOffsets[paletteFrame];
                    colors = ReadAnimationPalette(offset);
                }
                byte[] indices = source.Pixels.ToArray();
                foreach (int entry in overlays)
                {
                    WorldTextureAnimationSequence sequence = waterAnimations[entry];
                    CopyAnimationIndices(sequence, frames[entry], source, indices);
                }
                PixelImage image = new(source.PixelWidth, source.Height);
                for (int pixel = 0; pixel < indices.Length; pixel++)
                {
                    image.Pixels[pixel] = colors[indices[pixel]];
                }
                key = new(worldSettingsKey, "water/animated/" + tilePhase);
                TextureSourceAddress alias = new(worldSettingsKey, $"section/38/texture/{member}", 0, 0, 0, source.PixelWidth, source.Height, source.PixelWidth, source.Height);
                TextureSourceRegion region = new(source.X, source.Y, 8, PaletteX: 768, PaletteY: 192 + selector);
                textureScope.Register(key, image, "classic-world-water-region", region, nativeAlias: alias);
                animatedWaterTiles.Add(tilePhase, key);
            }
            dependencies.Add(key);
            positions.Add((index % 4 * 64, index / 4 * 64));
        }
        AssetHandle<TextureValue> atlas = textureScope.RegisterAtlas(new TextureKey(worldSettingsKey, "water/animated-atlas/" + phase), dependencies, positions, 256, 128) ?? throw new InvalidOperationException("World animation requires an active texture resource service.");
        animatedWaterAtlases.Add(phase, atlas);
        return atlas;
    }

    private WorldTextureAnimationSequence[] ReadTextureAnimationTable(int section, bool palette)
    {
        return WorldAssetReader.InnerPointers(wmset, sections[section]).Select(pointer => WorldTextureAnimationSequence.Read(wmset, sections[section] + pointer, palette)).ToArray();
    }

    private TimImage ReadWaterSource(int member)
    {
        if (!waterSources.TryGetValue(member, out TimImage? source))
        {
            int[] pointers = WorldAssetReader.InnerPointers(wmset, sections[37]);
            source = WorldAssetReader.ReadTim(wmset, sections[37] + pointers[member]).Image;
            waterSources.Add(member, source);
        }
        return source;
    }

    private Rgba32[] ReadAnimationPalette(int offset)
    {
        ReadOnlySpan<byte> frame = wmset.AsSpan(offset);
        if (BinaryPrimitives.ReadUInt32LittleEndian(frame) != 17 || BinaryPrimitives.ReadUInt32LittleEndian(frame[4..]) != 1 || BinaryPrimitives.ReadInt32LittleEndian(frame[8..]) != 524 || BinaryPrimitives.ReadInt16LittleEndian(frame[16..]) != 256 || BinaryPrimitives.ReadInt16LittleEndian(frame[18..]) != 1)
        {
            throw new NotSupportedException("World palette animation requires its original 256-word row.");
        }
        Rgba32[] colors = new Rgba32[256];
        for (int color = 0; color < colors.Length; color++)
        {
            colors[color] = Rgba32.FromPsxBgr555(BinaryPrimitives.ReadUInt16LittleEndian(frame.Slice(20 + color * 2, 2)));
        }
        return colors;
    }

    private void CopyAnimationIndices(WorldTextureAnimationSequence sequence, int frameIndex, TimImage target, byte[] indices)
    {
        ReadOnlySpan<byte> frame = wmset.AsSpan(sequence.FrameOffsets[frameIndex]);
        if (BinaryPrimitives.ReadUInt32LittleEndian(frame) != 18 || BinaryPrimitives.ReadUInt32LittleEndian(frame[4..]) != 1)
        {
            throw new NotSupportedException("World texture animation requires its original indexed8 frame.");
        }
        int width = BinaryPrimitives.ReadInt16LittleEndian(frame[16..]) * 2;
        int height = BinaryPrimitives.ReadInt16LittleEndian(frame[18..]);
        int x = (sequence.DestinationX - target.X) * 2;
        int y = sequence.DestinationY - target.Y;
        if (width <= 0 || height <= 0 || x < 0 || y < 0 || x + width > target.PixelWidth || y + height > target.Height || BinaryPrimitives.ReadInt32LittleEndian(frame[8..]) != 12 + width * height)
        {
            throw new NotSupportedException("World animation frame leaves its destination texture.");
        }
        for (int row = 0; row < height; row++)
        {
            frame.Slice(20 + row * width, width).CopyTo(indices.AsSpan((y + row) * target.PixelWidth + x, width));
        }
    }
}
