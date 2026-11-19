using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private IEnumerable<TextureCatalogEntry> EnumerateEffectTextureCatalog(CancellationToken cancellationToken, Action<TextureCatalogDiagnostic>? diagnostic)
    {
        for (int page = 0; page < 3; page++)
        {
            IReadOnlyList<TextureBuildContext> contexts;
            try
            {
                contexts = ReadCatalogCommonEffectPages(page);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                diagnostic?.Invoke(new("battle/a8def.tim", "Discovering common effect pages", exception.Message));
                continue;
            }
            foreach (TextureBuildContext context in contexts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return Entry(context, $"Common battle effects · page {page} · palette {context.Key.Palette}", "Battle and magic textures") with { Reader = "common-page", Index = page };
            }
        }
        foreach (string archiveName in new[] { "battle", "magic" })
        {
            IFlArchive? archive = null;
            try
            {
                archive = OpenArchive(archiveName);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                diagnostic?.Invoke(new(archiveName, "Discovering effect sources", exception.Message));
            }
            using (archive)
            {
                if (archive is null)
                {
                    continue;
                }
                foreach (IFlArchiveEntry source in archive.Entries.Where(entry => entry.Size > 0 && (entry.RelativePath.EndsWith(".tim", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(entry.RelativePath).StartsWith("mag", StringComparison.OrdinalIgnoreCase))).OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ResourceKey owner = new(archiveName + "/" + Path.GetFileName(source.RelativePath.Replace('\\', '/')));
                    if (owner.Path == "battle/a8def.tim")
                    {
                        continue;
                    }
                    IReadOnlyList<TextureBuildContext> contexts;
                    try
                    {
                        contexts = ReadCatalogStandaloneTim(owner);
                    }
                    catch (Exception exception) when (CatalogError(exception))
                    {
                        diagnostic?.Invoke(new(owner.Path, "Discovering TIM effect source", exception.Message));
                        continue;
                    }
                    foreach (TextureBuildContext context in contexts)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        yield return Entry(context, $"{Path.GetFileName(owner.Path)} · {context.Key.Subresource} · palette {context.Key.Palette}", "Battle and magic textures") with { Reader = "standalone-tim" };
                    }
                }
            }
        }
        byte[]? executable = null;
        try
        {
            executable = ReadNativeExecutable();
        }
        catch (Exception exception) when (CatalogError(exception))
        {
            diagnostic?.Invoke(new("magic", "Discovering cinematic texture profiles", exception.Message));
        }
        if (executable is null)
        {
            yield break;
        }
        for (int effect = 1; effect <= 400; effect++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!NativeBattleCinematicProfile.IsSupported(effect) && effect != 222)
            {
                continue;
            }
            IReadOnlyList<TextureBuildContext> contexts;
            try
            {
                contexts = ReadCatalogCinematicTextures(effect, executable);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                diagnostic?.Invoke(new($"magic/effect/{effect}", "Discovering cinematic material directory", exception.Message));
                continue;
            }
            foreach (TextureBuildContext context in contexts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return Entry(context, $"Effect {effect} · {Path.GetFileName(context.Key.Owner.Path)} · {context.Key.Subresource} · palette {context.Key.Palette}", "Guardian Forces and cinematics") with { Reader = "cinematic", Section = effect };
            }
        }
    }

    /// <summary>Replaces transient native crop identities with the complete persisted source resource.</summary>
    public IReadOnlyList<TextureBuildContext> LoadAuthoringTextures(TextureSourceSnapshot snapshot)
    {
        TextureBuildContext context = snapshot.Context;
        if (context.Layout != "native-effect-texture")
        {
            return [context];
        }
        TextureSourceAddress alias = context.NativeAlias ?? throw new NotSupportedException("The native texture crop has no persisted source identity.");
        IReadOnlyList<TextureBuildContext> sources;
        string filename = Path.GetFileName(alias.Owner.Path);
        if (alias.Owner.Path.StartsWith("battle/a8def", StringComparison.Ordinal) && filename.Length == 10 && filename[5] is >= '0' and <= '2')
        {
            sources = ReadCatalogCommonEffectPages(filename[5] - '0');
        }
        else if (filename.StartsWith("mag", StringComparison.Ordinal) && filename.Length >= 10 && int.TryParse(filename.AsSpan(3, 3), out int effectIndex) && (NativeBattleCinematicProfile.IsSupported(effectIndex + 1) || effectIndex == 221))
        {
            sources = ReadCatalogCinematicTextures(effectIndex + 1);
        }
        else
        {
            sources = ReadCatalogStandaloneTim(alias.Owner);
        }
        TextureBuildContext[] selected = sources.Where(source => source.Key.Owner == alias.Owner && source.Key.Subresource == alias.Subresource && source.Key.Palette == alias.Palette).ToArray();
        if (selected.Length == 0)
        {
            throw new NotSupportedException($"The captured native sampling window references {alias.Owner}/{alias.Subresource}, which has no compatible persisted material in the current source catalog.");
        }
        return selected;
    }

    private IReadOnlyList<TextureBuildContext> ReadCatalogCommonEffectPages(int page)
    {
        ResourceKey encodedOwner = new("battle/a8def.tim");
        TimImage tim = WorldAssetReader.ReadTim(resources.Read(encodedOwner), 0).Image;
        if (tim.PixelWidth != 768 || tim.Height != 256 || page is < 0 or > 2)
        {
            throw new InvalidDataException("The common battle texture requires its three native 256x256 pages.");
        }
        ResourceContent encoded = resources.Get(encodedOwner);
        ResourceKey owner = new($"battle/a8def{page}.tim");
        return Enumerable.Range(0, Math.Max(1, tim.Palettes.Count)).Select(palette => new TextureBuildContext(new(owner, "image", palette), resources.Source, encoded.Revision, "native-effect-source", 256, 256, () =>
        {
            PixelImage image = DecodeEffectTexture(tim, palette);
            PixelImage crop = new(256, 256);
            for (int row = 0; row < 256; row++)
            {
                image.Pixels.AsSpan(row * image.Width + page * 256, 256).CopyTo(crop.Pixels.AsSpan(row * 256, 256));
            }
            return crop;
        }, encodedSource: encoded)).ToArray();
    }

    private IReadOnlyList<TextureBuildContext> ReadCatalogStandaloneTim(ResourceKey owner)
    {
        byte[] bytes = resources.Read(owner);
        if (bytes.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 16)
        {
            return [];
        }
        List<TextureBuildContext> result = [];
        int offset = 0;
        int member = 0;
        while (offset <= bytes.Length - 8 && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) == 16)
        {
            (TimImage tim, int end) = WorldAssetReader.ReadTim(bytes, offset);
            if (end <= offset || member >= 256)
            {
                throw new InvalidDataException("Effect TIM sequence exceeds its supported source bounds.");
            }
            result.AddRange(EffectTimContexts(owner, $"texture/{member}", tim));
            member++;
            offset = end;
        }
        return result;
    }

    private IReadOnlyList<TextureBuildContext> ReadCatalogCinematicTextures(int effect, byte[]? executable = null)
    {
        INativeBattleScriptedTextureProfile textureProfile;
        if (effect == 222)
        {
            ImmutableArray<NativeBattleEffectHeapAllocation> currentFiles = Enumerable.Range(0, 2).Select(index => new NativeBattleEffectHeapAllocation($"mag221_b.{index:00}", resources.Read(new($"magic/mag221_b.{index:00}")).ToImmutableArray())).ToImmutableArray();
            textureProfile = NativeBattleWater222ResourcesReader.Read(executable ?? ReadNativeExecutable(), currentFiles).Profile;
        }
        else
        {
            NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
            ResourceKey initialOwner = new("magic/" + profile.InitialFile);
            byte[] initial = resources.Read(initialOwner);
            textureProfile = NativeBattleCinematicResourcesReader.Read(executable ?? ReadNativeExecutable(), initial.ToImmutableArray(), profile);
        }
        Dictionary<int, byte[]> files = [];
        byte[] Source(int index)
        {
            if (!files.TryGetValue(index, out byte[]? bytes))
            {
                int identity = NativeBattleEffectIdentity.IndexFromBindingEffect(effect);
                bytes = resources.Read(new($"magic/mag{identity:000}_b.{index:00}"));
                files.Add(index, bytes);
            }
            return bytes;
        }
        List<TextureBuildContext> result = [];
        for (int member = 0; member < textureProfile.TextureCount; member++)
        {
            NativeBattleEffectTextureRectangle pixels = textureProfile.Rectangle(member, false);
            NativeBattleEffectTextureRectangle palette = textureProfile.Rectangle(member, true);
            if (pixels.Width <= 0 || pixels.Height <= 0)
            {
                continue;
            }
            int pixelSource = textureProfile.Source(member, false);
            int paletteSource = textureProfile.Source(member, true);
            int bits = textureProfile.BitsPerPixel(member);
            byte[] words = ReadCinematicMember(Source(pixelSource), member, false, checked(pixels.Width * pixels.Height * 2));
            TimClut? clut = null;
            if (bits is 4 or 8)
            {
                byte[] colors = ReadCinematicMember(Source(paletteSource), member, true, checked(palette.Width * palette.Height * 2));
                ushort[] values = new ushort[colors.Length / 2];
                for (int index = 0; index < values.Length; index++)
                {
                    values[index] = BinaryPrimitives.ReadUInt16LittleEndian(colors.AsSpan(index * 2));
                }
                clut = new(palette.X, palette.Y, palette.Width, palette.Height, values);
            }
            byte[] decodedWords;
            if (bits == 4)
            {
                decodedWords = new byte[words.Length * 2];
                for (int index = 0; index < words.Length; index++)
                {
                    decodedWords[index * 2] = (byte)(words[index] & 15);
                    decodedWords[index * 2 + 1] = (byte)(words[index] >> 4);
                }
            }
            else
            {
                decodedWords = words;
            }
            TimPixelMode mode = bits == 4 ? TimPixelMode.Indexed4 : bits == 8 ? TimPixelMode.Indexed8 : bits == 16 ? TimPixelMode.Direct16 : throw new NotSupportedException("The cinematic source uses an unsupported pixel depth.");
            TimImage tim = new(mode, pixels.X, pixels.Y, pixels.Width, pixels.Height, decodedWords, clut);
            int effectIndex = NativeBattleEffectIdentity.IndexFromBindingEffect(effect);
            ResourceKey owner = new($"magic/mag{effectIndex:000}_b.{pixelSource:00}");
            result.AddRange(EffectTimContexts(owner, $"texture/{member}", tim));
        }
        return result;
    }

    private static byte[] ReadCinematicMember(byte[] bytes, int member, bool palette, int length)
    {
        int directory = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(palette ? 8 : 0x14));
        uint relative = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(checked(directory + member * 4)));
        int offset = checked(directory + (int)(palette ? relative : relative & 0xFFFFFF));
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("Cinematic material leaves its authenticated source file.");
        }
        return bytes.AsSpan(offset, length).ToArray();
    }

    private IReadOnlyList<TextureBuildContext> EffectTimContexts(ResourceKey owner, string member, TimImage tim)
    {
        if (tim.PixelMode is not (TimPixelMode.Indexed4 or TimPixelMode.Indexed8))
        {
            throw new NotSupportedException("Direct-color native effect artwork requires the runtime's selected 555/565 device format; an indexed CLUT source can be exported independently.");
        }
        return Enumerable.Range(0, tim.Palettes.Count).Select(palette => new TextureBuildContext(new(owner, member, palette), resources.Source, resources.Get(owner).Revision, "native-effect-source", tim.PixelWidth, tim.Height, () => DecodeEffectTexture(tim, palette), new(tim.X, tim.Y, tim.PixelMode == TimPixelMode.Indexed4 ? 4 : 8), value => DecodeEffectTexture(tim, value), tim.Palettes.Count, resources.Get(owner))).ToArray();
    }

    /// <summary>Expands an indexed effect with the native PC CLUT RGB contract and ordinary editor coverage alpha.</summary>
    public static PixelImage DecodeEffectTexture(TimImage tim, int palette = 0)
    {
        if (tim.Clut is null || tim.PixelMode is not (TimPixelMode.Indexed4 or TimPixelMode.Indexed8))
        {
            throw new NotSupportedException("Native effect source decoding requires its original indexed palette.");
        }
        int colors = tim.PixelMode == TimPixelMode.Indexed4 ? 16 : 256;
        int paletteStart = checked(palette * colors);
        if (paletteStart < 0 || paletteStart > tim.Clut.Colors.Length - colors || tim.Pixels.Length != tim.PixelWidth * tim.Height)
        {
            throw new InvalidDataException("Effect palette or source index canvas is incomplete.");
        }
        PixelImage image = new(tim.PixelWidth, tim.Height);
        for (int index = 0; index < image.Pixels.Length; index++)
        {
            int colorIndex = tim.Pixels[index];
            if (colorIndex >= colors)
            {
                throw new InvalidDataException("Effect source pixel indexes outside its selected palette.");
            }
            uint color = NativeBattlePcPalette.Decode467370(tim.Clut.Colors[paletteStart + colorIndex]);
            image.Pixels[index] = new((byte)(color >> 16), (byte)(color >> 8), (byte)color, color >> 24 == 0 ? (byte)0 : (byte)255);
        }
        return image;
    }
}
