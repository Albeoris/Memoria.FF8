using System.Collections.Immutable;
using Albeoris.Games.FF8.NamedicBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>The native counted WORD dictionary, read from the selected logical source.</summary>
public sealed class NativeTextTerms : ITextTermSource
{
    private readonly ImmutableArray<string> entries;
    private readonly ResourceService? resources;

    public NativeTextTerms(string language, IEnumerable<string> entries)
    {
        Language = FF8TextLanguages.Parse(language).ToString().ToLowerInvariant();
        this.entries = entries.ToImmutableArray();
    }

    private NativeTextTerms(ResourceService resources, string[] entries) : this(resources.Source.Language, entries)
    {
        this.resources = resources;
        Generation = resources.Generation;
    }

    public long Generation { get; }
    public string Language { get; }

    public static NativeTextTerms Read(ResourceService resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return new(resources, NamedicBin.ReadStrings(resources.Read(new ResourceKey("main/namedic.bin")), NativeTextEncoding.Create(resources.Source.Language)));
    }

    public static NativeTextTerms Capture(ResourceService resources)
    {
        long generation = resources.Generation;
        NativeTextTerms source = Read(resources);
        string[] values = Enumerable.Range(0, source.entries.Length).Select(source.Resolve).ToArray();
        if (resources.Generation != generation)
        {
            throw new IOException("The text dictionary changed while its preview context was being captured.");
        }
        return new(source.Language, values);
    }

    public static ITextTermSource FromResources(ResourceService resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return new Source(resources);
    }

    private sealed class Source(ResourceService resources) : ITextTermSource
    {
        private NativeTextTerms? dictionary;
        public long Generation => resources.Generation;
        public string Language => resources.Source.Language;

        public string Resolve(int index)
        {
            if (dictionary is null || dictionary.Generation != resources.Generation)
            {
                dictionary = Read(resources);
            }
            return dictionary.Resolve(index);
        }
    }

    public string Resolve(int index)
    {
        if ((uint)index >= entries.Length)
        {
            return string.Empty;
        }
        string original = entries[index];
        return resources is null ? original : resources.Localize(LocalizationSourceExporter.IndexedKey(resources.Source.Language, "namedic_menu", index), original);
    }
}

public sealed partial class GameAssets
{
    private ITextTermSource? nativeTextTerms;

    public ITextTermSource ReadNativeTextTerms() => nativeTextTerms ??= NativeTextTerms.FromResources(resources);
}
