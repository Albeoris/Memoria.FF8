using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Selects complete visual model bundles while preserving the primary gameplay resources and capabilities.</summary>
public sealed class SelectedModelResourceSource : IResourceSource, IResourceSourceProvider, IResourcePrefixSource, IFieldModelSource, IBattleModelSource, ITextureReplacementResolver
{
    private readonly IResourceSource primary;
    private readonly IResourceSource models;
    private bool disposed;

    public SelectedModelResourceSource(IResourceSource primary, IResourceSource models)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(models);
        this.primary = primary;
        this.models = models;
    }

    public ResourceSourceDescriptor Descriptor => primary.Descriptor;
    public ResourceSourceDescriptor ModelSourceDescriptor => models.Descriptor;

    public object? GetSource(Type capability)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (capability.IsInstanceOfType(this))
        {
            return this;
        }
        return Capability(primary, capability);
    }

    public ResourceContent? Find(ResourceKey key)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return primary.Find(key);
    }

    public ResourcePrefix? ReadPrefix(ResourceKey key, int maximumBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return (Capability(primary, typeof(IResourcePrefixSource)) as IResourcePrefixSource)?.ReadPrefix(key, maximumBytes);
    }

    public IReadOnlyList<FieldModel> ReadModels(ResourceKey owner, byte[] one, Func<string, byte[]> mainCharacter, Action<NativeFieldOneModelEntry>? retainEntry = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        IFieldModelSource? originalDecoder = Capability(primary, typeof(IFieldModelSource)) as IFieldModelSource;
        IReadOnlyList<FieldModel> original = originalDecoder?.ReadModels(owner, one, mainCharacter, retainEntry) ?? FieldModelReader.Read(one, mainCharacter, retainEntry);
        byte[] selectedOne = ReadSelected(owner);
        IFieldModelSource? selectedDecoder = Capability(models, typeof(IFieldModelSource)) as IFieldModelSource;
        IReadOnlyList<FieldModel> selected = selectedDecoder?.ReadModels(owner, selectedOne, ReadSelectedMainCharacter) ?? FieldModelReader.Read(selectedOne, ReadSelectedMainCharacter);
        if (original.Count != selected.Count)
        {
            throw new NotSupportedException($"Selected model resource {owner} from {models.Descriptor.Id} has {selected.Count} model slots; the primary resource requires {original.Count}.");
        }
        return original.Select((model, index) => MapField(owner, index, model, selected[index])).ToArray();
    }

    public FieldModel ReadModel(ResourceKey owner, byte[] one, Func<string, byte[]> mainCharacter, int index)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        IFieldModelSource? originalDecoder = Capability(primary, typeof(IFieldModelSource)) as IFieldModelSource;
        FieldModel original = originalDecoder?.ReadModel(owner, one, mainCharacter, index) ?? FieldModelReader.ReadSingle(one, mainCharacter, index);
        byte[] selectedOne = ReadSelected(owner);
        IFieldModelSource? selectedDecoder = Capability(models, typeof(IFieldModelSource)) as IFieldModelSource;
        FieldModel selected = selectedDecoder?.ReadModel(owner, selectedOne, ReadSelectedMainCharacter, index) ?? FieldModelReader.ReadSingle(selectedOne, ReadSelectedMainCharacter, index);
        return MapField(owner, index, original, selected);
    }

    public BattleModel ReadModel(ResourceKey owner, byte[] content)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        BattleModel original = DecodeBattle(primary, owner, content);
        BattleModel selected = DecodeBattle(models, owner, ReadSelected(owner));
        return ModelResourceMapping.Select(original, selected, models.Descriptor);
    }

    public FieldModel ReadMainCharacterMaterials(ResourceKey owner, byte[] content)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        byte[] selectedContent = ReadSelected(owner);
        IFieldModelSource? selectedDecoder = Capability(models, typeof(IFieldModelSource)) as IFieldModelSource;
        FieldModel selected = selectedDecoder?.ReadMainCharacterMaterials(owner, selectedContent) ?? FieldModelReader.ReadMainCharacterMaterials(Path.GetFileNameWithoutExtension(owner.Path), selectedContent);
        return selected with { TextureSourceDescriptor = models.Descriptor };
    }

    public BattleModel ReadWeapon(ResourceKey owner, byte[] content, BattleModel character)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        IBattleModelSource? originalDecoder = Capability(primary, typeof(IBattleModelSource)) as IBattleModelSource;
        BattleModel original = originalDecoder?.ReadWeapon(owner, content, character) ?? BattleResourceReader.ReadWeapon(Path.GetFileName(owner.Path), content, character);
        ResourceKey characterOwner = new("battle/" + Path.GetFileName(character.Name));
        BattleModel selectedCharacter = DecodeBattle(models, characterOwner, ReadSelected(characterOwner));
        byte[] selectedContent = ReadSelected(owner);
        IBattleModelSource? selectedDecoder = Capability(models, typeof(IBattleModelSource)) as IBattleModelSource;
        BattleModel selected = selectedDecoder?.ReadWeapon(owner, selectedContent, selectedCharacter) ?? BattleResourceReader.ReadWeapon(Path.GetFileName(owner.Path), selectedContent, selectedCharacter);
        return ModelResourceMapping.Select(original, selected, models.Descriptor);
    }

    public TextureReplacement? Resolve(TextureBuildContext context)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        IResourceSource selected = context.Source.Id == models.Descriptor.Id ? models : primary;
        return (Capability(selected, typeof(ITextureReplacementResolver)) as ITextureReplacementResolver)?.Resolve(context);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        try
        {
            models.Dispose();
        }
        finally
        {
            if (!ReferenceEquals(primary, models))
            {
                primary.Dispose();
            }
        }
    }

    private FieldModel MapField(ResourceKey owner, int index, FieldModel original, FieldModel selected)
    {
        if (original.Name.Length < 4 || selected.Name.Length < 4 || !original.Name.AsSpan(0, 4).Equals(selected.Name.AsSpan(0, 4), StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Selected model resource {owner} slot {index} from {models.Descriptor.Id} identifies {selected.Name}; the primary resource requires {original.Name}.");
        }
        return ModelResourceMapping.Select(original, selected, models.Descriptor);
    }

    private byte[] ReadSelectedMainCharacter(string name) => ReadSelected(new ResourceKey($"field/main_chr/{name}.mch"));

    private byte[] ReadSelected(ResourceKey owner)
    {
        ResourceContent content = models.Find(owner) ?? throw new FileNotFoundException($"Selected model resource {owner} is unavailable from {models.Descriptor.Id}. Configure a complete model resource installation.");
        using Stream input = content.OpenRead();
        using MemoryStream bytes = ResourceTrafficTelemetry.CreateStagingStream();
        input.CopyTo(bytes);
        return bytes.ToArray();
    }

    private static BattleModel DecodeBattle(IResourceSource source, ResourceKey owner, byte[] content) => (Capability(source, typeof(IBattleModelSource)) as IBattleModelSource)?.ReadModel(owner, content) ?? BattleResourceReader.ReadModel(Path.GetFileName(owner.Path), content);

    private static object? Capability(IResourceSource source, Type capability) => source is IResourceSourceProvider provider ? provider.GetSource(capability) : capability.IsInstanceOfType(source) ? source : null;
}
