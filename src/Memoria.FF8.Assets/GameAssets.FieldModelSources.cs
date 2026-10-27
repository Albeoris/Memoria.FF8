using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    /// <summary>Validates standalone geometry and materials without inventing field animation data.</summary>
    public static void ValidateMainCharacterResource(ResourceContent content, ResourceContent? original = null)
    {
        string[] parts = content.Key.Path.Split('/');
        if (parts.Length != 3 || parts[0] != "field" || parts[1] != "main_chr" || !parts[2].EndsWith(".mch", StringComparison.Ordinal))
        {
            throw new ArgumentException("A main-character source requires its canonical library resource identity.", nameof(content));
        }
        if (original is not null && original.Key != content.Key)
        {
            throw new ArgumentException("Selected and installed main-character resource identities must agree.", nameof(original));
        }
        using MemoryStream bytes = new();
        using (Stream input = content.OpenRead())
        {
            input.CopyTo(bytes);
        }
        byte[] selected = bytes.ToArray();
        if (original is not null)
        {
            using Stream installedInput = original.OpenRead();
            using MemoryStream installedBytes = new();
            installedInput.CopyTo(installedBytes);
            if (selected.AsSpan().SequenceEqual(installedBytes.ToArray()))
            {
                return;
            }
        }
        FieldModelReader.ValidateMainCharacter(parts[2][..^4], selected);
    }

    /// <summary>Loads the original library material table independently of field placement or animation.</summary>
    public IReadOnlyList<PixelImage> LoadFieldMainCharacterTextures(string name)
    {
        if (name.Length != 4 || name[0] != 'd' || name.Skip(1).Any(character => !char.IsAsciiDigit(character)))
        {
            throw new ArgumentException("A main-character material source requires its canonical library name.", nameof(name));
        }
        ResourceKey owner = new($"field/main_chr/{name}.mch");
        FieldModel model = FieldModelReader.ReadMainCharacterMaterials(name, resources.Read(owner));
        AssetHandle<TextureValue>[] handles = model.Textures.Select((image, ordinal) => textureScope.Register(new(owner, $"texture/{ordinal}"), image, "classic-field-model", nativeSheet: NativeFieldModelTextureSheets.Address(model, ordinal))!).ToArray();
        return new ResourceTextureList(handles);
    }

    /// <summary>Reads only field ONE directories; model geometry and scene resources remain unloaded.</summary>
    public IEnumerable<ModelCatalogEntry> EnumerateFieldModelSources()
    {
        for (int fieldId = 0; fieldId < fieldNames.Length; fieldId++)
        {
            string name = fieldNames[fieldId];
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }
            string path = $"field/{name}/chara.one";
            IReadOnlyList<NativeFieldOneModelEntry>? directory = null;
            string? error = null;
            try
            {
                ResourcePrefix prefix = resources.ReadPrefix(new ResourceKey(path), 65536);
                directory = FieldModelReader.ReadDirectory(prefix.Bytes, prefix.Length ?? prefix.Bytes.Length);
            }
            catch (Exception exception) when (CatalogError(exception))
            {
                error = exception.Message;
            }
            if (directory is null)
            {
                yield return new($"field/{name}/unavailable", name, "Field", path, $"field {fieldId} {name} {path}", fieldId) { LoadError = error };
                continue;
            }
            for (int index = 0; index < directory.Count; index++)
            {
                string model = directory[index].Name;
                yield return new($"field/{name}/{index}", $"{model} · {name}/{index}", "Field", path, $"field {fieldId} {name} {index} {model} {path}", fieldId, index);
            }
        }
    }

    /// <summary>Decodes and registers one original field model without initializing its scene.</summary>
    public FieldModel LoadFieldModelSource(ModelCatalogEntry entry)
    {
        if (entry.Category != "Field" || entry.FieldId is not int fieldId || (uint)fieldId >= fieldNames.Length || entry.ResourcePath != $"field/{fieldNames[fieldId]}/chara.one" || entry.LoadError is not null)
        {
            throw new ArgumentException("A field model source requires an available directory entry from this source catalog.", nameof(entry));
        }
        ResourceKey owner = new(entry.ResourcePath);
        byte[] one = resources.Read(owner);
        FieldModel model = resources.GetSource<IFieldModelSource>() is { } source ? source.ReadModel(owner, one, ReadMainCharacter, entry.ModelIndex) : FieldModelReader.ReadSingle(one, ReadMainCharacter, entry.ModelIndex);
        return RegisterFieldModelTextures(fieldNames[fieldId], entry.ModelIndex, model);
    }
}
