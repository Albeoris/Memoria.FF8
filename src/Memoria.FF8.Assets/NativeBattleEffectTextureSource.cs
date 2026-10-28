using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Resolves original live texture identities through the common resource service.</summary>
public interface INativeBattleEffectTextureSource
{
    long Generation { get; }
    PixelImage Apply(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null);

    int GetPrimitiveSampleKey(TexturePrimitiveSample sample)
    {
        return 0;
    }

    PixelImage Apply(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels, byte? alternatePaletteShift, NativeBattlePcDirectTextureFormat? directTextureFormat, TexturePrimitiveSample sample)
    {
        return Apply(original, state, page, clut, u, v, width, height, retainedPixels, alternatePaletteShift, directTextureFormat);
    }

    PixelImage ApplyDecoded(PixelImage original, PixelImage? decodedNative, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null)
        => Apply(original, state, page, clut, u, v, width, height, retainedPixels, alternatePaletteShift, directTextureFormat);

    /// <summary>Sources consuming both decoded native pixels and primitive variants override this overload.</summary>
    PixelImage ApplyDecoded(PixelImage original, PixelImage? decodedNative, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels, byte? alternatePaletteShift, NativeBattlePcDirectTextureFormat? directTextureFormat, TexturePrimitiveSample sample)
    {
        if (GetPrimitiveSampleKey(sample) == 0)
        {
            return ApplyDecoded(original, decodedNative, state, page, clut, u, v, width, height, retainedPixels, alternatePaletteShift, directTextureFormat);
        }
        return Apply(original, state, page, clut, u, v, width, height, retainedPixels, alternatePaletteShift, directTextureFormat, sample);
    }
}

public sealed class NativeBattleEffectTextureSource(ResourceService resources) : INativeBattleEffectTextureSource, IDisposable
{
    private const int MaximumResidentContexts = 256;
    private static long nextRevision;
    private static long nextScope;
    private readonly long scope = Interlocked.Increment(ref nextScope);
    private readonly ConditionalWeakTable<NativeBattleEffectTextureState, SourceRevision> revisions = new();
    private readonly ConditionalWeakTable<NativeBattlePcTextureWindowPixels, SourceRevision> retainedRevisions = new();
    private readonly Dictionary<TextureKey, LinkedListNode<TextureKey>> retained = [];
    private readonly LinkedList<TextureKey> recent = [];
    private object? lastVramPublication;
    private ImmutableArray<NativeBattleTextureSourceRegion>? lastLiveSources;
    private ImmutableArray<NativeBattleTextureRegistration>? lastRegistrations;
    private ImmutableArray<byte>? lastVramReadTail;
    private SourceRevision? lastRevision;

    private sealed class SourceRevision
    {
        public long Value { get; } = Interlocked.Increment(ref nextRevision);
    }

    public long Generation => resources.CatalogGeneration;

    public int GetPrimitiveSampleKey(TexturePrimitiveSample sample)
    {
        if (!float.IsFinite(sample.FirstU) || !float.IsFinite(sample.FirstV))
        {
            throw new InvalidDataException("Native texture sampling requires finite primitive coordinates.");
        }
        return resources.Source.Layouts.Contains("native-battle-gf-atlas") ? sample.FirstV < 0.5f ? 1 : 2 : 0;
    }

    public PixelImage Apply(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null)
        => ApplyCore(original, null, state, page, clut, u, v, width, height, retainedPixels, alternatePaletteShift, directTextureFormat, null);

    public PixelImage Apply(PixelImage original, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels, byte? alternatePaletteShift, NativeBattlePcDirectTextureFormat? directTextureFormat, TexturePrimitiveSample sample)
        => ApplyCore(original, null, state, page, clut, u, v, width, height, retainedPixels, alternatePaletteShift, directTextureFormat, sample);

    public PixelImage ApplyDecoded(PixelImage original, PixelImage? decodedNative, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels = null, byte? alternatePaletteShift = null, NativeBattlePcDirectTextureFormat? directTextureFormat = null)
        => ApplyCore(original, decodedNative, state, page, clut, u, v, width, height, retainedPixels, alternatePaletteShift, directTextureFormat, null);

    public PixelImage ApplyDecoded(PixelImage original, PixelImage? decodedNative, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels, byte? alternatePaletteShift, NativeBattlePcDirectTextureFormat? directTextureFormat, TexturePrimitiveSample sample)
        => ApplyCore(original, decodedNative, state, page, clut, u, v, width, height, retainedPixels, alternatePaletteShift, directTextureFormat, sample);

    private PixelImage ApplyCore(PixelImage original, PixelImage? decodedNative, NativeBattleEffectTextureState state, ushort page, ushort clut, int u, int v, int width, int height, NativeBattlePcTextureWindowPixels? retainedPixels, byte? alternatePaletteShift, NativeBattlePcDirectTextureFormat? directTextureFormat, TexturePrimitiveSample? sample)
    {
        int depth = (page >> 7) & 3;
        if (depth == 3 || width <= 0 || height <= 0)
        {
            throw new InvalidDataException("Native effect texture queries require a supported depth and positive logical crop.");
        }
        long retainedRevision = retainedPixels is null ? 0 : retainedRevisions.GetValue(retainedPixels, _ => new()).Value;
        ImmutableArray<NativeBattleTextureRegistration>? registrations = retainedPixels is null ? state.TextureRegistrations : retainedPixels.TextureRegistrations;
        if (resources.Source.Layouts.Contains("native-battle-gf-atlas") && registrations is { } atlasRegistrations && resources.GetSource<INativeBattleGfAtlasSource>()?.TryGetGfAtlasAddress(atlasRegistrations, page, clut, u, v, width, height, sample) is { } atlas)
        {
            int sampleKey = sample is { } primitiveSample ? GetPrimitiveSampleKey(primitiveSample) : 0;
            TextureKey atlasKey = new(new("battle/native-gf-atlas"), $"atlas/{scope}/{page}/{clut}/{u}/{v}/{width}/{height}/retained/{retainedRevision}/sample/{sampleKey}");
            TextureBuildContext atlasContext = new(atlasKey, resources.Source, revisions.GetValue(state, _ => new()).Value, "native-battle-gf-atlas", width, height, () => original)
            {
                NativeAtlas = atlas
            };
            TextureValue atlasValue = resources.GetTexture(atlasContext, !retained.ContainsKey(atlasKey)).Value;
            Retain(atlasKey);
            if (atlasValue.IsReplacement)
            {
                return alternatePaletteShift is byte atlasShift ? Alternate(atlasValue.Image, atlasShift) : atlasValue.Image;
            }
        }
        TextureSourceUpload? upload = resources.Source.Layouts.Contains("demaster-content-upload") ? NativeBattlePcTextureUploadSource.Create(state, page, clut, u, v, width, height, retainedPixels) : null;
        if (upload is not null)
        {
            TextureKey uploadKey = new(new("battle/native-pc-texture"), $"upload/{scope}/{page}/{clut}/{u}/{v}/{width}/{height}/retained/{retainedRevision}");
            TextureBuildContext uploadContext = new(uploadKey, resources.Source, revisions.GetValue(state, _ => new()).Value, "native-pc-texture-upload", width, height, () => original)
            {
                NativeUpload = upload
            };
            TextureValue uploadValue = resources.GetTexture(uploadContext, !retained.ContainsKey(uploadKey)).Value;
            Retain(uploadKey);
            if (uploadValue.IsReplacement)
            {
                return alternatePaletteShift is byte uploadShift ? Alternate(uploadValue.Image, uploadShift) : uploadValue.Image;
            }
        }
        ImmutableArray<NativeBattleTextureSourceRegion>? sources = retainedPixels is null ? state.LiveTextureSources : retainedPixels.LiveTextureSources;
        if (sources is null)
        {
            return original;
        }
        int pixelsPerWord = 4 >> depth;
        int pageX = (page & 15) * 64;
        int pageY = (page & 16) * 16;
        int queryX = pageX * pixelsPerWord + u;
        int queryY = pageY + v;
        int paletteX = (clut & 63) * 16;
        int paletteY = (clut & 0x7FFF) >> 6;
        List<(PixelImage Image, int X, int Y, int Width, int Height)> replacements = [];
        foreach (NativeBattleTextureSourceRegion source in sources.Value)
        {
            if (source.Origin.Role != NativeBattleTextureRole.Pixels || source.Origin.BitsPerPixel != 4 << depth)
            {
                continue;
            }
            int left = Math.Max(queryX, source.X * pixelsPerWord);
            int top = Math.Max(queryY, source.Y);
            int right = Math.Min(queryX + width, (source.X + source.WidthWords) * pixelsPerWord);
            int bottom = Math.Min(queryY + height, source.Y + source.Height);
            if (left >= right || top >= bottom)
            {
                continue;
            }
            int sourceX = source.SourceXWords * pixelsPerWord + left - source.X * pixelsPerWord;
            int sourceY = source.SourceY + top - source.Y;
            int cropWidth = right - left;
            int cropHeight = bottom - top;
            int paletteRow = PaletteRow(source, paletteX, paletteY);
            string domain = source.Origin.Domain == NativeBattleTextureDomain.Magic ? "magic" : "battle";
            ResourceKey owner = new($"battle/{source.Origin.Owner}");
            TextureKey key = new(owner, $"effect/{scope}/{source.Origin.Domain}/{source.Origin.Kind}/{source.Origin.Member}/{source.X}/{source.Y}/{sourceX}/{sourceY}/{cropWidth}/{cropHeight}/{page}/{clut}/window/{u}/{v}/{width}/{height}/retained/{retainedRevision}", paletteRow);
            TextureSourceAddress alias = new(new($"{domain}/{source.Origin.Owner}"), source.Origin.Kind == NativeBattleTextureSourceKind.Image ? "image" : $"texture/{source.Origin.Member}", paletteRow, sourceX, sourceY, cropWidth, cropHeight, source.Origin.WidthWords * pixelsPerWord, source.Origin.Height);
            int localX = left - queryX;
            int localY = top - queryY;
            TextureSourcePageAddress nativePage = Page(retainedPixels is null ? state.TextureRegistrations : retainedPixels.TextureRegistrations, source, page, paletteX, paletteY, u, v, width, height, left - pageX * pixelsPerWord, top - pageY, cropWidth, cropHeight);
            PixelImage? contextOriginal = alternatePaletteShift is null && localX == 0 && localY == 0 && cropWidth == width && cropHeight == height && decodedNative is { } native && native.Width == width && native.Height == height ? native : null;
            TextureBuildContext context = new(key, resources.Source, Revision(state).Value, "native-effect-texture", cropWidth, cropHeight, () => contextOriginal ?? NativeBattleEffectTextureReader.DecodePcRegion(state, page, clut, u + localX, v + localY, cropWidth, cropHeight, directTextureFormat: directTextureFormat, pixelWords: retainedPixels), new(source.X, source.Y, source.Origin.BitsPerPixel, source.Origin.WidthWords * pixelsPerWord, source.Origin.Height, paletteX, paletteY), paletteCount: source.Origin.PaletteRectangle?.Height ?? 1)
            {
                NativeAlias = alias,
                NativePage = nativePage
            };
            AssetHandle<TextureValue> handle = resources.GetTexture(context, !retained.ContainsKey(key));
            Retain(key);
            TextureValue value = handle.Value;
            if (value.IsReplacement)
            {
                replacements.Add((alternatePaletteShift is byte shift ? Alternate(value.Image, shift) : value.Image, localX, localY, cropWidth, cropHeight));
            }
        }
        return Compose(original, width, height, replacements);
    }

    private SourceRevision Revision(NativeBattleEffectTextureState state)
    {
        if (!revisions.TryGetValue(state, out SourceRevision? revision))
        {
            bool samePublication = ReferenceEquals(lastVramPublication, state.VramPublicationIdentity)
                && lastLiveSources == state.LiveTextureSources
                && lastRegistrations == state.TextureRegistrations
                && lastVramReadTail == state.VramReadTail;
            revision = samePublication && lastRevision is not null ? lastRevision : new();
            revisions.Add(state, revision);
        }
        lastVramPublication = state.VramPublicationIdentity;
        lastLiveSources = state.LiveTextureSources;
        lastRegistrations = state.TextureRegistrations;
        lastVramReadTail = state.VramReadTail;
        lastRevision = revision;
        return revision;
    }

    private static int PaletteRow(NativeBattleTextureSourceRegion pixels, int x, int y)
    {
        if (pixels.Origin.PaletteRectangle is not { } canvas)
        {
            return 0;
        }
        return x == canvas.X && y >= canvas.Y && y < canvas.Y + canvas.Height ? y - canvas.Y : 0;
    }

    private static PixelImage Alternate(PixelImage image, byte shift)
    {
        Rgba32[] pixels = new Rgba32[image.Pixels.Length];
        for (int index = 0; index < pixels.Length; index++)
        {
            Rgba32 color = image.Pixels[index];
            ushort word = (ushort)((int)MathF.Round(color.R * 31f / 255) | (int)MathF.Round(color.G * 31f / 255) << 5 | (int)MathF.Round(color.B * 31f / 255) << 10);
            uint packed = color.A == 0 ? 0 : NativeBattlePcPalette.Decode467470(word, shift);
            pixels[index] = new(0, 0, 0, (byte)(packed >> 24));
        }
        return new(image.Width, image.Height, pixels);
    }

    private static TextureSourcePageAddress Page(ImmutableArray<NativeBattleTextureRegistration>? registrations, NativeBattleTextureSourceRegion selected, int page, int paletteX, int paletteY, int queryU, int queryV, int queryWidth, int queryHeight, int x, int y, int width, int height)
    {
        int pageX = (page & 15) * 64;
        int pageY = (page & 16) * 16;
        int pixelsPerWord = 16 / selected.Origin.BitsPerPixel;
        int queryX = pageX * pixelsPerWord + queryU;
        int queryY = pageY + queryV;
        NativeBattleTextureRegistration[] owners = (registrations ?? []).Where(source => source.Origin.BitsPerPixel == selected.Origin.BitsPerPixel && source.OwnedSpans.Any(span => span.X * pixelsPerWord < queryX + queryWidth && (span.X + span.WidthWords) * pixelsPerWord > queryX && span.Y < queryY + queryHeight && span.Y + span.Height > queryY)).OrderBy(source => source.Y).ThenBy(source => source.X).ToArray();
        TexturePageMember[] members = owners.Select(source => new TexturePageMember("effect", source.Origin.Owner, source.Origin.Kind == NativeBattleTextureSourceKind.Image ? -1 : source.Origin.Member, source.X, source.Y, source.WidthWords, source.Height, source.Origin.BitsPerPixel, source.ExactFrame)).ToArray();
        return new(page & 31, x, y, width, height, paletteX, paletteY, members) { Namespace = owners.Length == 0 ? null : owners[0].Origin.Domain == NativeBattleTextureDomain.Magic ? "magic" : "battle" };
    }

    private void Retain(TextureKey key)
    {
        if (retained.Remove(key, out LinkedListNode<TextureKey>? node))
        {
            recent.Remove(node);
        }
        retained.Add(key, recent.AddLast(key));
        while (retained.Count > MaximumResidentContexts)
        {
            TextureKey oldest = recent.First!.Value;
            recent.RemoveFirst();
            retained.Remove(oldest);
            resources.Release([oldest]);
        }
    }

    private static PixelImage Compose(PixelImage original, int width, int height, IReadOnlyList<(PixelImage Image, int X, int Y, int Width, int Height)> replacements)
    {
        if (replacements.Count == 0)
        {
            return original;
        }
        int scaleX = Math.Max(1, original.Width / width);
        int scaleY = Math.Max(1, original.Height / height);
        foreach (var replacement in replacements)
        {
            scaleX = Math.Max(scaleX, replacement.Image.Width / replacement.Width);
            scaleY = Math.Max(scaleY, replacement.Image.Height / replacement.Height);
        }
        PixelImage result = new(width * scaleX, height * scaleY);
        for (int row = 0; row < result.Height; row++)
        {
            for (int column = 0; column < result.Width; column++)
            {
                result[column, row] = original[column * original.Width / result.Width, row * original.Height / result.Height];
            }
        }
        foreach (var replacement in replacements)
        {
            for (int row = 0; row < replacement.Height * scaleY; row++)
            {
                for (int column = 0; column < replacement.Width * scaleX; column++)
                {
                    result[replacement.X * scaleX + column, replacement.Y * scaleY + row] = replacement.Image[column * replacement.Image.Width / (replacement.Width * scaleX), row * replacement.Image.Height / (replacement.Height * scaleY)];
                }
            }
        }
        return result;
    }

    public void Dispose()
    {
        resources.Release(retained.Keys.ToArray());
        retained.Clear();
        recent.Clear();
        lastVramPublication = null;
        lastLiveSources = null;
        lastRegistrations = null;
        lastVramReadTail = null;
        lastRevision = null;
    }
}
