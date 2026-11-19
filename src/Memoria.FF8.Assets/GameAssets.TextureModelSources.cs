using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    // Material-only readers share the existing DAT/ONE directories and TIM codec. No skeleton,
    // animation, geometry or decoded image is needed to discover an original material slot.
    private IReadOnlyList<TextureBuildContext> ReadClassicCatalogBattleTextures(ModelCatalogEntry entry)
    {
        ResourceKey owner = new(entry.ResourcePath);
        byte[] bytes = resources.Read(owner);
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (entry.ResourcePath == "battle/c0m127.dat" && count == 2)
        {
            return [];
        }
        int section;
        if (entry.Category == "Battle weapon")
        {
            section = count is 7 or 8 ? 6 : count == 5 ? 4 : throw new NotSupportedException("Weapon material directory has an unsupported section layout.");
        }
        else
        {
            section = count == 11 ? 10 : count is 9 or 10 ? 8 : count is 7 or 8 ? 5 : throw new NotSupportedException("Battle material directory has an unsupported section layout.");
        }
        int start = NativeBattleDatDirectory.Section(bytes, section);
        int textures = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(start));
        if (textures is < 0 or > 16 || 4L + textures * 4 > bytes.Length - start)
        {
            throw new InvalidDataException("Battle texture directory exceeds its supported bounds.");
        }
        List<TextureBuildContext> result = [];
        for (int index = 0; index < textures; index++)
        {
            int offset = checked(start + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(start + 4 + index * 4)));
            TimImage tim = WorldAssetReader.ReadTim(bytes, offset).Image;
            TextureSourceRegion region = CatalogTimRegion(tim);
            result.Add(CatalogTimContext(owner, $"texture/{index}", tim, "classic-battle", nativeSheet: BattleAssets.BattleModelSheetAddress(owner, region, textures), nativeAlias: new(owner, $"texture/{index}", 0, 0, 0, tim.PixelWidth, tim.Height, tim.PixelWidth, tim.Height)));
        }
        return result;
    }

    private IReadOnlyList<TextureBuildContext> ReadClassicCatalogFieldTextures(ModelCatalogEntry entry)
    {
        ResourceKey owner = new(entry.ResourcePath);
        byte[] one = resources.Read(owner);
        IReadOnlyList<NativeFieldOneModelEntry> directory = FieldModelReader.ReadDirectory(one);
        HashSet<int> resolving = [];
        (NativeFieldOneModelEntry Header, IReadOnlyList<TimImage> Images, bool Main) Resolve(int index)
        {
            if ((uint)index >= directory.Count || !resolving.Add(index))
            {
                throw new InvalidDataException("Field texture alias references an absent material or a cycle.");
            }
            NativeFieldOneModelEntry header = directory[index];
            try
            {
                if (header.Flag >> 28 == 0xA)
                {
                    var shared = Resolve((byte)(header.Flag >> 20));
                    if (shared.Images.Count == 0)
                    {
                        throw new InvalidDataException("Field texture alias references an empty material source.");
                    }
                    return (shared.Header, [shared.Images[0]], shared.Main);
                }
                if (header.Flag >> 28 == 0xD)
                {
                    if (header.Name.Length < 4)
                    {
                        throw new InvalidDataException("Main-character material has no complete library name.");
                    }
                    byte[] mch = ReadMainCharacter(header.Name[..4]);
                    List<TimImage> images = [];
                    int position = 0;
                    while (true)
                    {
                        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(mch.AsSpan(position));
                        position += 4;
                        if (pointer == uint.MaxValue)
                        {
                            break;
                        }
                        if (images.Count >= 100)
                        {
                            throw new InvalidDataException("Main-character material directory is unbounded.");
                        }
                        images.Add(WorldAssetReader.ReadTim(mch, checked((int)(pointer & 0xFFFFFF))).Image);
                    }
                    return (header, images, true);
                }
                return (header, header.Textures.Select(offset => WorldAssetReader.ReadTim(one, checked(header.Offset + offset)).Image).ToArray(), false);
            }
            finally
            {
                resolving.Remove(index);
            }
        }
        var source = Resolve(entry.ModelIndex);
        string name = source.Header.Name[..Math.Min(4, source.Header.Name.Length)];
        string prefix = $"model/{entry.ModelIndex}/{(source.Main ? "main" : "second")}/{name}/texture";
        string? variant = NativeFieldModelTextureSheets.Variant(name, entry.FieldId ?? -1);
        return source.Images.Select((tim, index) => CatalogTimContext(owner, $"{prefix}/{index}", tim, "classic-field-model", nativeSheet: NativeFieldModelTextureSheets.Address(name, source.Main, variant, index, CatalogTimRegion(tim), tim.PixelWidth, tim.Height))).ToArray();
    }

    private static TextureSourceRegion CatalogTimRegion(TimImage tim)
    {
        int bits = tim.PixelMode == TimPixelMode.Indexed4 ? 4 : tim.PixelMode == TimPixelMode.Indexed8 ? 8 : tim.PixelMode == TimPixelMode.Direct16 ? 16 : 24;
        return new(tim.X, tim.Y, bits, tim.PixelWidth, tim.Height, tim.Clut?.X, tim.Clut?.Y);
    }

    private TextureBuildContext CatalogTimContext(ResourceKey owner, string member, TimImage tim, string layout, TextureSourceAddress? nativeSheet = null, TextureSourceAddress? nativeAlias = null) => new(new(owner, member), resources.Source, resources.Get(owner).Revision, layout, tim.PixelWidth, tim.Height, () => tim.Decode(), CatalogTimRegion(tim), tim.Decode, Math.Max(1, tim.Palettes.Count), resources.Get(owner)) { NativeSheet = nativeSheet, NativeAlias = nativeAlias };
}
