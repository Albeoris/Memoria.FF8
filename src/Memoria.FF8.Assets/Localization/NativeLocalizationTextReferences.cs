using System.Collections;
using System.Reflection;
using Albeoris.Games.FF8.KernelBin;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Assets.Localization;

/// <summary>Resolves original text identities against the selected resource generation.</summary>
public sealed class NativeLocalizationTextReferences
{
    private readonly ResourceService resources;
    private readonly Lazy<IReadOnlyDictionary<string, string>> kernelTexts;
    private readonly string kernelPrefix;

    public NativeLocalizationTextReferences(ResourceService resources)
    {
        this.resources = resources;
        kernelPrefix = LocalizationSourceExporter.IndexedKey(resources.Source.Language, "kernel", 0)[..^4];
        kernelTexts = new(ReadKernelTexts);
    }

    public string ResolveOriginal(string key)
    {
        key = new LocalizedTextReference(key).ResolveKey(resources.Source.Language);
        foreach ((string id, string original) in SystemTextCatalog.Defaults)
        {
            if (key == SystemTextCatalog.Key(id))
            {
                return original;
            }
        }
        if (key.StartsWith(kernelPrefix, StringComparison.Ordinal) && kernelTexts.Value.TryGetValue(key, out string? text))
        {
            return text;
        }
        throw new InvalidDataException($"Unknown original localization text identity: {key}.");
    }

    private IReadOnlyDictionary<string, string> ReadKernelTexts()
    {
        KernelData kernel = KernelData.ReadFromArray(resources.Read(new ResourceKey("main/kernel.bin")), NativeTextEncoding.Create(resources.Source.Language));
        Dictionary<string, string> texts = new(StringComparer.Ordinal);
        foreach (PropertyInfo collectionProperty in typeof(KernelData).GetProperties().Where(property => typeof(IList).IsAssignableFrom(property.PropertyType)))
        {
            IList collection = (IList)collectionProperty.GetValue(kernel)!;
            for (int ordinal = 0; ordinal < collection.Count; ordinal++)
            {
                object? item = collection[ordinal];
                if (item is string value)
                {
                    Add(collectionProperty.Name, "text", ordinal, value);
                }
                else if (item is not null)
                {
                    foreach (PropertyInfo property in item.GetType().GetProperties().Where(property => property.PropertyType == typeof(string) && property.CanRead))
                    {
                        if (property.GetValue(item) is string text)
                        {
                            Add(collectionProperty.Name, property.Name, ordinal, text);
                        }
                    }
                }
            }
        }
        return texts;

        void Add(string collection, string role, int ordinal, string text)
        {
            texts.Add(LocalizationSourceExporter.IndexedKey(resources.Source.Language, LocalizationSourceExporter.KernelTable(collection, role), ordinal), text);
        }
    }
}
