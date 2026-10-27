using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public readonly record struct NativeWorldTextureCopy(int SourceX, int SourceY, int DestinationX, int DestinationY, int WidthWords, int Height);

public sealed partial class WorldAssets
{
    private readonly Dictionary<int, TimImage> nativeStaticCopyTargets = [];
    private readonly Dictionary<(int Member, int Phase), IReadOnlyList<PixelImage>> animatedStaticCopies = [];

    public static IReadOnlyList<NativeWorldTextureCopy> ReadNativeWorldTextureCopies(byte discNumber, uint mainLoopCounter)
    {
        if (discNumber != 4)
        {
            return Array.Empty<NativeWorldTextureCopy>();
        }
        int phase = (int)((mainLoopCounter >> 2) & 3);
        return [new(832 + 8 * phase, 160, 856, 96, 8, 32), new(832 + 8 * ((phase + 1) & 3), 160, 864, 96, 8, 32)];
    }

    public IReadOnlyList<PixelImage> ReadAnimatedStaticTextures(WorldStaticModel model, byte? discNumber, uint? mainLoopCounter)
    {
        // A separately authored model retains its own material ownership.
        if (!scriptedStaticModels.TryGetValue(model.ShapeId, out WorldStaticModel? originalModel) || !ReferenceEquals(model.Textures, originalModel.Textures))
        {
            return model.Textures;
        }
        if (!nativeStaticCopyTargets.TryGetValue(model.ModelIndex, out TimImage? target))
        {
            int start = sections[41] + SectionPointer(41, model.ModelIndex);
            target = WorldAssetReader.ReadTim(wmset, start).Image;
            nativeStaticCopyTargets.Add(model.ModelIndex, target);
        }
        // Match the destination of the original animated VRAM copies, independently of model identity.
        bool intersectsCopy = target.X < 872 && target.X + target.WidthInWords > 856 && target.Y < 128 && target.Y + target.Height > 96;
        if (!intersectsCopy)
        {
            return model.Textures;
        }
        if (target.X != 856 || target.Y != 96 || target.WidthInWords != 16 || target.Height != 32)
        {
            throw new NotSupportedException("Native animated world texture destination requires an unrepresented partial VRAM copy.");
        }
        byte disc = discNumber ?? throw new NotSupportedException("Native animated world textures require the retained disc number.");
        if (disc != 4)
        {
            return model.Textures;
        }
        uint counter = mainLoopCounter ?? throw new NotSupportedException("Native animated world textures require the retained main-loop counter.");
        int phase = (int)((counter >> 2) & 3);
        if (animatedStaticCopies.TryGetValue((model.ModelIndex, phase), out IReadOnlyList<PixelImage>? cached))
        {
            return cached;
        }
        if (target.PixelMode != TimPixelMode.Indexed4)
        {
            throw new NotSupportedException("Native animated world texture copies require their original indexed4 destination.");
        }
        List<TextureKey> dependencies = [];
        List<(int X, int Y)> positions = [];
        ResourceKey owner = worldSettingsKey;
        foreach (NativeWorldTextureCopy copy in ReadNativeWorldTextureCopies(disc, counter))
        {
            (TimImage source, int member) = FindNativeCopySource(copy);
            if (source.PixelMode != TimPixelMode.Indexed4 || source.Palettes.Count != 1 || Enumerable.Range(0, target.Palettes.Count).Any(palette => !Enumerable.Range(0, 16).All(color => target.Palettes[palette][color] == source.Palettes[0][color])))
            {
                throw new NotSupportedException("Native animated world texture copies require their proved source/destination palette equivalence.");
            }
            int sourceX = (copy.SourceX - source.X) * 4;
            int sourceY = copy.SourceY - source.Y;
            int width = copy.WidthWords * 4;
            PixelImage decoded = source.Decode();
            PixelImage crop = new(width, copy.Height);
            for (int row = 0; row < copy.Height; row++)
            {
                decoded.Pixels.AsSpan((sourceY + row) * decoded.Width + sourceX, width).CopyTo(crop.Pixels.AsSpan(row * width));
            }
            TextureKey key = new(owner, $"native-copy/source/section/38/texture/{member}/region/{sourceX}/{sourceY}/{width}/{copy.Height}");
            TextureSourceAddress alias = new(owner, $"section/38/texture/{member}", 0, sourceX, sourceY, width, copy.Height, source.PixelWidth, source.Height);
            textureScope.Register(key, crop, "classic-world-vram-copy", nativeAlias: alias);
            dependencies.Add(key);
            positions.Add(((copy.DestinationX - target.X) * 4, copy.DestinationY - target.Y));
        }
        AssetHandle<TextureValue>[] handles = Enumerable.Range(0, target.Palettes.Count).Select(palette => textureScope.RegisterAtlas(new TextureKey(owner, $"native-copy/section/42/texture/{model.ModelIndex}/phase/{phase}", palette), dependencies, positions, target.PixelWidth, target.Height) ?? throw new InvalidOperationException("Native world texture copies require the active resource service.")).ToArray();
        IReadOnlyList<PixelImage> result = new ResourceTextureList(handles);
        animatedStaticCopies.Add((model.ModelIndex, phase), result);
        return result;
    }

    private (TimImage Image, int Member) FindNativeCopySource(NativeWorldTextureCopy copy)
    {
        int[] pointers = WorldAssetReader.InnerPointers(wmset, sections[37]);
        for (int member = 0; member < pointers.Length; member++)
        {
            TimImage image = WorldAssetReader.ReadTim(wmset, sections[37] + pointers[member]).Image;
            if (copy.SourceX >= image.X && copy.SourceY >= image.Y && copy.SourceX + copy.WidthWords <= image.X + image.WidthInWords && copy.SourceY + copy.Height <= image.Y + image.Height)
            {
                return (image, member);
            }
        }
        throw new InvalidDataException("Native world VRAM copy leaves its original source texture table.");
    }
}
