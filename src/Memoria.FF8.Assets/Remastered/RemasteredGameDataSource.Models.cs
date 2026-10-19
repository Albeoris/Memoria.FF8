using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Remastered;

public sealed partial class RemasteredGameDataSource
{
    private sealed record ImageBinding(string Path, bool EyeVariants);
    private readonly Dictionary<TextureKey, ImageBinding> imageBindings = [];
    private string[]? fieldNames;
    private Dictionary<uint, double>? modelVertexDivisors;

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
        IReadOnlyList<FieldModel> models = FieldModelReader.Read(one, mainCharacter, retainEntry).Select(NormalizeModelVertices).ToArray();
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
                // HD suffixes identify source materials; TIM rectangles identify their VRAM allocation.
                string stem = $"{name}_{texture}" + (name == "o006" && UsesAlphaObject(owner) ? "_alpha" : "");
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

    private FieldModel NormalizeModelVertices(FieldModel model)
    {
        if (model.Name.Length < 4 || model.Name.Skip(1).Take(3).Any(character => character is < '0' or > '9'))
        {
            throw new InvalidDataException("A field model has no native four-character identity.");
        }
        uint identity = checked((uint)(model.Name[0] * 1000 + (model.Name[1] - '0') * 100 + (model.Name[2] - '0') * 10 + model.Name[3] - '0'));
        modelVertexDivisors ??= ReadModelVertexDivisors();
        double divisor = modelVertexDivisors.GetValueOrDefault(identity, 1d);
        if (divisor == 1d)
        {
            return model;
        }
        // Native precision correction divides local coordinates before bone rotation and translation.
        return model with { Vertices = model.Vertices.Select(vertex => vertex with { Position = new((float)(vertex.Position.X / divisor), (float)(vertex.Position.Y / divisor), (float)(vertex.Position.Z / divisor)) }).ToArray() };
    }

    private Dictionary<uint, double> ReadModelVertexDivisors()
    {
        using FileStream file = OpenAuthenticatedRuntimeCode();
        using PEReader image = new(file);
        const uint address = 0x11771CE8;
        const int recordCount = 142;
        byte[] records = image.GetSectionData(checked((int)(address - image.PEHeaders.PEHeader!.ImageBase))).GetContent(0, recordCount * 16).ToArray();
        Dictionary<uint, double> result = [];
        for (int index = 0; index < recordCount; index++)
        {
            ReadOnlySpan<byte> record = records.AsSpan(index * 16, 16);
            uint identity = BinaryPrimitives.ReadUInt32LittleEndian(record);
            double divisor = BitConverter.ToDouble(record[8..]);
            if (identity == uint.MaxValue)
            {
                if (index != recordCount - 1 || divisor != 1d)
                {
                    throw new InvalidDataException("Native field model precision table has an invalid terminating default.");
                }
                return result;
            }
            if (!double.IsFinite(divisor) || divisor <= 0 || !result.TryAdd(identity, divisor))
            {
                throw new InvalidDataException("Native field model precision table has an invalid identity or divisor.");
            }
        }
        throw new InvalidDataException("Native field model precision table has no terminating default.");
    }

    public BattleModel ReadModel(ResourceKey owner, byte[] content)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        string name = Path.GetFileNameWithoutExtension(owner.Path);
        BattleModel model = BattleResourceReader.ReadModel(Path.GetFileName(owner.Path), content);
        ClearBattleImageBindings(owner);
        BindBattleMonsterImages(owner, name, model);
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
        if (ResolveArtwork(context) is { } artwork)
        {
            return artwork;
        }
        if (!imageBindings.TryGetValue(context.Key, out ImageBinding? binding))
        {
            return null;
        }
        using Stream stream = Open(binding.Path);
        DecodedTextureImage decoded = TextureImageDecoder.Decode(Read(stream), binding.Path);
        PixelImage image = decoded.Image;
        IReadOnlyDictionary<string, PixelImage>? variants = null;
        const int nativeScale = 3;
        const int nativeRowWidth = 384;
        int width = checked(context.LogicalWidth * nativeScale);
        int height = checked(context.LogicalHeight * nativeScale);
        if (width != nativeRowWidth || image.Width is not (nativeRowWidth or nativeRowWidth * 2) || image.Height > nativeRowWidth)
        {
            throw new NotSupportedException($"The selected model image {binding.Path} ({image.Width}x{image.Height}) does not fit the native texture row {context.LogicalWidth}x{context.LogicalHeight} at scale {nativeScale}.");
        }
        if (binding.EyeVariants && image.Width == nativeRowWidth * 2)
        {
            variants = new Dictionary<string, PixelImage> { ["eyes-closed"] = RowPlane(image, nativeRowWidth, width, height) };
        }
        image = RowPlane(image, 0, width, height);
        return new TextureReplacement(image, $"{Descriptor.Id}:{context.Key}", context.LogicalWidth, context.LogicalHeight, [image], variants);
    }

    private static PixelImage RowPlane(PixelImage source, int x, int width, int height)
    {
        if (x == 0 && source.Width == width && source.Height == height)
        {
            return source;
        }
        // Native uploads each image into a fixed atlas row. Unuploaded GL storage is unspecified;
        // the managed backing allocation uses transparent pixels for that uncovered area.
        Rgba32[] pixels = new Rgba32[checked(width * height)];
        for (int row = 0; row < Math.Min(source.Height, height); row++)
        {
            source.Pixels.AsSpan(row * source.Width + x, width).CopyTo(pixels.AsSpan(row * width));
        }
        return new PixelImage(width, height, pixels);
    }
}
