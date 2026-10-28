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
        ModelSourceDescriptor = models.Descriptor with { Id = primary.Descriptor.Id + "/models/" + models.Descriptor.Id };
    }

    public ResourceSourceDescriptor Descriptor => primary.Descriptor;
    public ResourceSourceDescriptor ModelSourceDescriptor { get; }

    public object? GetSource(Type capability, ResourceService? resources = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (resources is not null && (capability == typeof(IFieldModelSource) || capability == typeof(IBattleModelSource)))
        {
            return new BoundModelSource(this, resources);
        }
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

    public IReadOnlyList<FieldModel> ReadModels(ResourceKey owner, byte[] one, Func<string, byte[]> mainCharacter, Action<NativeFieldOneModelEntry>? retainEntry = null) => ReadModels(owner, one, mainCharacter, retainEntry, null);

    private IReadOnlyList<FieldModel> ReadModels(ResourceKey owner, byte[] one, Func<string, byte[]> mainCharacter, Action<NativeFieldOneModelEntry>? retainEntry, ResourceService? resources)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        IFieldModelSource? originalDecoder = Capability(primary, typeof(IFieldModelSource)) as IFieldModelSource;
        HashSet<string> overriddenMaterials = new(StringComparer.OrdinalIgnoreCase);
        byte[] ReadMain(string name) => ReadPrimaryMainCharacter(mainCharacter, name, overriddenMaterials, resources);
        IReadOnlyList<FieldModel> original = originalDecoder?.ReadModels(owner, one, ReadMain, retainEntry) ?? FieldModelReader.Read(one, ReadMain, retainEntry);
        if (resources?.HasEncodedReplacement(owner) == true || original.All(model => HasOverriddenMaterials(model, overriddenMaterials)))
        {
            return original;
        }
        byte[] selectedOne = ReadSelected(owner);
        IFieldModelSource? selectedDecoder = Capability(models, typeof(IFieldModelSource)) as IFieldModelSource;
        if (overriddenMaterials.Count > 0)
        {
            return original.Select((model, index) => HasOverriddenMaterials(model, overriddenMaterials) ? model : MapField(owner, index, model, selectedDecoder?.ReadModel(owner, selectedOne, ReadSelectedMainCharacter, index) ?? FieldModelReader.ReadSingle(selectedOne, ReadSelectedMainCharacter, index))).ToArray();
        }
        IReadOnlyList<FieldModel> selected = selectedDecoder?.ReadModels(owner, selectedOne, ReadSelectedMainCharacter) ?? FieldModelReader.Read(selectedOne, ReadSelectedMainCharacter);
        if (original.Count != selected.Count)
        {
            throw new NotSupportedException($"Selected model resource {owner} from {models.Descriptor.Id} has {selected.Count} model slots; the primary resource requires {original.Count}.");
        }
        return original.Select((model, index) => HasOverriddenMaterials(model, overriddenMaterials) ? model : MapField(owner, index, model, selected[index])).ToArray();
    }

    public FieldModel ReadModel(ResourceKey owner, byte[] one, Func<string, byte[]> mainCharacter, int index) => ReadModel(owner, one, mainCharacter, index, null);

    private FieldModel ReadModel(ResourceKey owner, byte[] one, Func<string, byte[]> mainCharacter, int index, ResourceService? resources)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        IFieldModelSource? originalDecoder = Capability(primary, typeof(IFieldModelSource)) as IFieldModelSource;
        HashSet<string> overriddenMaterials = new(StringComparer.OrdinalIgnoreCase);
        byte[] ReadMain(string name) => ReadPrimaryMainCharacter(mainCharacter, name, overriddenMaterials, resources);
        FieldModel original = originalDecoder?.ReadModel(owner, one, ReadMain, index) ?? FieldModelReader.ReadSingle(one, ReadMain, index);
        if (resources?.HasEncodedReplacement(owner) == true || HasOverriddenMaterials(original, overriddenMaterials))
        {
            return original;
        }
        byte[] selectedOne = ReadSelected(owner);
        IFieldModelSource? selectedDecoder = Capability(models, typeof(IFieldModelSource)) as IFieldModelSource;
        FieldModel selected = selectedDecoder?.ReadModel(owner, selectedOne, ReadSelectedMainCharacter, index) ?? FieldModelReader.ReadSingle(selectedOne, ReadSelectedMainCharacter, index);
        return MapField(owner, index, original, selected);
    }

    public BattleModel ReadModel(ResourceKey owner, byte[] content) => ReadModel(owner, content, null);

    private BattleModel ReadModel(ResourceKey owner, byte[] content, ResourceService? resources)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        BattleModel original = DecodeBattle(primary, owner, content);
        if (resources?.HasEncodedReplacement(owner) == true)
        {
            return original;
        }
        BattleModel selected = DecodeBattle(models, owner, ReadSelected(owner));
        return ModelResourceMapping.Select(original, selected, ModelSourceDescriptor);
    }

    public FieldModel ReadMainCharacterMaterials(ResourceKey owner, byte[] content) => ReadMainCharacterMaterials(owner, content, null);

    private FieldModel ReadMainCharacterMaterials(ResourceKey owner, byte[] content, ResourceService? resources)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (resources?.HasEncodedReplacement(owner) == true)
        {
            IFieldModelSource? originalDecoder = Capability(primary, typeof(IFieldModelSource)) as IFieldModelSource;
            return originalDecoder?.ReadMainCharacterMaterials(owner, content) ?? FieldModelReader.ReadMainCharacterMaterials(Path.GetFileNameWithoutExtension(owner.Path), content);
        }
        byte[] selectedContent = ReadSelected(owner);
        IFieldModelSource? selectedDecoder = Capability(models, typeof(IFieldModelSource)) as IFieldModelSource;
        FieldModel selected = selectedDecoder?.ReadMainCharacterMaterials(owner, selectedContent) ?? FieldModelReader.ReadMainCharacterMaterials(Path.GetFileNameWithoutExtension(owner.Path), selectedContent);
        return selected with { TextureSourceDescriptor = ModelSourceDescriptor };
    }

    public BattleModel ReadWeapon(ResourceKey owner, byte[] content, BattleModel character) => ReadWeapon(owner, content, character, null);

    private BattleModel ReadWeapon(ResourceKey owner, byte[] content, BattleModel character, ResourceService? resources)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        IBattleModelSource? originalDecoder = Capability(primary, typeof(IBattleModelSource)) as IBattleModelSource;
        BattleModel original = originalDecoder?.ReadWeapon(owner, content, character) ?? BattleResourceReader.ReadWeapon(Path.GetFileName(owner.Path), content, character);
        if (resources?.HasEncodedReplacement(owner) == true)
        {
            return original;
        }
        ResourceKey characterOwner = new("battle/" + Path.GetFileName(character.Name));
        BattleModel selectedCharacter = DecodeBattle(models, characterOwner, ReadSelected(characterOwner));
        byte[] selectedContent = ReadSelected(owner);
        IBattleModelSource? selectedDecoder = Capability(models, typeof(IBattleModelSource)) as IBattleModelSource;
        BattleModel selected = selectedDecoder?.ReadWeapon(owner, selectedContent, selectedCharacter) ?? BattleResourceReader.ReadWeapon(Path.GetFileName(owner.Path), selectedContent, selectedCharacter);
        return ModelResourceMapping.Select(original, selected, ModelSourceDescriptor);
    }

    public TextureReplacement? Resolve(TextureBuildContext context)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (context.Source.Id != ModelSourceDescriptor.Id)
        {
            return (Capability(primary, typeof(ITextureReplacementResolver)) as ITextureReplacementResolver)?.Resolve(context);
        }
        TextureBuildContext donorContext = new(context.Key, models.Descriptor, context.Revision, context.Layout, context.LogicalWidth, context.LogicalHeight, () => context.Original, context.Region, context.PaletteImage, context.PaletteCount, context.EncodedSource) { NativeAlias = context.NativeAlias, NativePage = context.NativePage, NativeSheet = context.NativeSheet, RequestedVariant = context.RequestedVariant };
        return (Capability(models, typeof(ITextureReplacementResolver)) as ITextureReplacementResolver)?.Resolve(donorContext);
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
        return ModelResourceMapping.Select(original, selected, ModelSourceDescriptor);
    }

    private byte[] ReadSelectedMainCharacter(string name) => ReadSelected(new ResourceKey($"field/main_chr/{name}.mch"));

    private static byte[] ReadPrimaryMainCharacter(Func<string, byte[]> read, string name, HashSet<string> overriddenMaterials, ResourceService? resources)
    {
        byte[] content = read(name);
        if (resources?.HasEncodedReplacement(new ResourceKey($"field/main_chr/{name}.mch")) == true)
        {
            overriddenMaterials.Add(name);
        }
        return content;
    }

    private static bool HasOverriddenMaterials(FieldModel model, HashSet<string> overriddenMaterials) => model.MainCharacterTextures && model.TextureResourceName is { } name && overriddenMaterials.Contains(name);

    private sealed class BoundModelSource(SelectedModelResourceSource source, ResourceService resources) : IFieldModelSource, IBattleModelSource
    {
        public IReadOnlyList<FieldModel> ReadModels(ResourceKey owner, byte[] one, Func<string, byte[]> mainCharacter, Action<NativeFieldOneModelEntry>? retainEntry = null) => source.ReadModels(owner, one, mainCharacter, retainEntry, resources);
        public FieldModel ReadModel(ResourceKey owner, byte[] one, Func<string, byte[]> mainCharacter, int index) => source.ReadModel(owner, one, mainCharacter, index, resources);
        public FieldModel ReadMainCharacterMaterials(ResourceKey owner, byte[] content) => source.ReadMainCharacterMaterials(owner, content, resources);
        public BattleModel ReadModel(ResourceKey owner, byte[] content) => source.ReadModel(owner, content, resources);
        public BattleModel ReadWeapon(ResourceKey owner, byte[] content, BattleModel character) => source.ReadWeapon(owner, content, character, resources);
    }

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
