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

    public NativeTextTerms(IEnumerable<string> entries)
    {
        this.entries = entries.ToImmutableArray();
    }

    private NativeTextTerms(ResourceService resources, string[] entries) : this(entries)
    {
        this.resources = resources;
        Generation = resources.Generation;
    }

    public long Generation { get; }

    public static NativeTextTerms Read(ResourceService resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return new(resources, NamedicBin.ReadStrings(resources.Read(new ResourceKey("main/namedic.bin")), FF8Encoding.CreateEuropean()));
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
