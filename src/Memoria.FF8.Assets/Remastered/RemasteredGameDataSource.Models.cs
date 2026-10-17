using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Remastered;

public sealed partial class RemasteredGameDataSource
{
    private sealed record ImageBinding(string Path, bool EyeVariants);
    private readonly Dictionary<TextureKey, ImageBinding> imageBindings = [];
    private string[]? fieldNames;

    private bool UsesAlphaObject(ResourceKey owner)
    {
        string[] parts = owner.Path.Split('/');
        if (parts.Length != 3 || parts[0] != "field")
        {
            return false;
        }
        if (fieldNames is null)
        {
            ResourceContent maps = Find(new ResourceKey("field/mapdata/maplist")) ?? throw new InvalidDataException("The field catalog is unavailable.");
            using Stream stream = maps.OpenRead();
            fieldNames = System.Text.Encoding.ASCII.GetString(Read(stream)).Replace("\r", "").Split('\n');
        }
        // NG-007: the original resource loader selects this object material in two fields.
        return fieldNames.Length > 0x370 && (parts[1] == fieldNames[0x36F] || parts[1] == fieldNames[0x370]);
    }

    public IReadOnlyList<FieldModel> ReadModels(ResourceKey owner, byte[] one, Func<string, byte[]> mainCharacter, Action<NativeFieldOneModelEntry>? retainEntry = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        IReadOnlyList<FieldModel> models = FieldModelReader.Read(one, mainCharacter, retainEntry);
        for (int index = 0; index < models.Count; index++)
        {
            FieldModel model = models[index];
            if (model.Name.Length < 4)
            {
                throw new InvalidDataException("A field model has no complete logical name.");
            }
            string name = model.Name[..4].ToLowerInvariant();
            if (name[0] == 'c')
            {
                name = "n" + name[1..];
            }
            for (int texture = 0; texture < model.Textures.Count; texture++)
            {
                if (texture >= model.TextureRegions.Count)
                {
                    throw new NotSupportedException("A field model texture has no source rectangle metadata.");
                }
                TextureSourceRegion region = model.TextureRegions[texture];
                int row = region.Y / 128 & 1;
                string stem = $"{name}_{row}" + (name == "o006" && UsesAlphaObject(owner) ? "_alpha" : "");
                string preferred = $"textures/field.fs/field_hd_new/{stem}.png";
                string fallback = $"textures/field.fs/field_hd/{stem}.png";
                string? path = Contains(preferred) ? preferred : Contains(fallback) ? fallback : null;
                TextureKey key = new(owner, $"model/{index}/texture/{texture}");
                if (path is not null)
                {
                    imageBindings[key] = new ImageBinding(path, true);
                }
                else
                {
                    imageBindings.Remove(key);
                }
            }
        }
        return models;
    }

    public BattleModel ReadModel(ResourceKey owner, byte[] content)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        string name = Path.GetFileNameWithoutExtension(owner.Path);
        BattleModel model = BattleResourceReader.ReadModel(Path.GetFileName(owner.Path), content);
        if (name.Length == 6 && name[0] == 'd' && name[2] == 'c')
        {
            string preferred = $"textures/battle.fs/hd_new/{name}";
            string fallback = $"textures/battle.fs/hd/{name}";
            string? stem = Contains(preferred + "_0.png") && Contains(preferred + "_1.png") ? preferred : Contains(fallback + "_0.png") && Contains(fallback + "_1.png") ? fallback : null;
            if (stem is not null)
            {
                for (int texture = 0; texture < model.Textures.Count; texture++)
                {
                    int row = model.TextureRegions[texture].Y / 128 & 1;
                    imageBindings[new TextureKey(owner, $"texture/{texture}")] = new ImageBinding($"{stem}_{row}.png", true);
                }
            }
        }
        return model;
    }

    public TextureReplacement? Resolve(TextureBuildContext context)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!imageBindings.TryGetValue(context.Key, out ImageBinding? binding))
        {
            return null;
        }
        using Stream stream = Open(binding.Path);
        DecodedTextureImage decoded = TextureImageDecoder.Decode(Read(stream), binding.Path);
        PixelImage image = decoded.Image;
        IReadOnlyDictionary<string, PixelImage>? variants = null;
        if (binding.EyeVariants && (long)image.Width * context.LogicalHeight == (long)image.Height * context.LogicalWidth * 2)
        {
            variants = new Dictionary<string, PixelImage> { ["eyes-closed"] = Crop(image, image.Width / 2, 0, image.Width / 2, image.Height) };
            image = Crop(image, 0, 0, image.Width / 2, image.Height);
        }
        if ((long)image.Width * context.LogicalHeight != (long)image.Height * context.LogicalWidth)
        {
            throw new NotSupportedException("The selected model image does not fit its logical texture rectangle.");
        }
        return new TextureReplacement(image, $"{Descriptor.Id}:{context.Key}", context.LogicalWidth, context.LogicalHeight, [image], variants);
    }

    private static PixelImage Crop(PixelImage source, int x, int y, int width, int height)
    {
        Rgba32[] pixels = new Rgba32[checked(width * height)];
        for (int row = 0; row < height; row++)
        {
            source.Pixels.AsSpan((y + row) * source.Width + x, width).CopyTo(pixels.AsSpan(row * width));
        }
        return new PixelImage(width, height, pixels);
    }
}
