using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class SelectedModelResourceSourceTests
{
    [Fact]
    public void PrimaryResourcesAndCapabilitiesRemainPrimary()
    {
        TestSource primary = new("primary", GameDataEdition.Classic);
        TestSource donor = new("donor", GameDataEdition.Remastered);
        using SelectedModelResourceSource source = new(primary, donor);
        ResourceService resources = new(source);
        Assert.Same(primary.Descriptor, source.Descriptor);
        Assert.Same(primary, resources.GetSource<IGameFileSource>());
        Assert.IsAssignableFrom<IFieldModelSource>(resources.GetSource<IFieldModelSource>());
        Assert.IsAssignableFrom<IBattleModelSource>(resources.GetSource<IBattleModelSource>());
        Assert.Equal("primary", resources.Get(new ResourceKey("battle/test.dat")).Provenance);
        Assert.Equal(new byte[] { 1 }, resources.ReadPrefix(new ResourceKey("battle/test.dat"), 1).Bytes);
        Assert.Empty(donor.Reads);
    }

    [Fact]
    public void SelectedFieldDependenciesNeverUsePrimaryCallback()
    {
        TestSource primary = new("primary", GameDataEdition.Classic);
        TestSource donor = new("donor", GameDataEdition.Remastered) { ReadMain = true, ModelName = "d001" };
        using SelectedModelResourceSource source = new(primary, donor);
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => source.ReadModel(new ResourceKey("field/test/chara.one"), [1, 2], _ => throw new InvalidOperationException("Primary callback must not read donor materials."), 0));
        Assert.Contains("slot 0", exception.Message);
        Assert.Equal(new[] { "field/test/chara.one", "field/main_chr/d001.mch" }, donor.Reads);
        Assert.Empty(primary.Reads);
    }

    [Fact]
    public void MissingSelectedResourcesFailWithLogicalIdentityAndSource()
    {
        TestSource primary = new("primary", GameDataEdition.Classic);
        TestSource donor = new("donor", GameDataEdition.Remastered) { Missing = true };
        using SelectedModelResourceSource source = new(primary, donor);
        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => source.ReadModel(new ResourceKey("field/test/chara.one"), [1, 2], _ => [], 0));
        Assert.Contains("field/test/chara.one", exception.Message);
        Assert.Contains("donor", exception.Message);
    }

    [Fact]
    public void StandaloneMainCharacterMaterialsUseSelectedResourceAndDescriptor()
    {
        TestSource primary = new("primary", GameDataEdition.Classic);
        TestSource donor = new("donor", GameDataEdition.Remastered);
        using SelectedModelResourceSource source = new(primary, donor);
        FieldModel materials = source.ReadMainCharacterMaterials(new ResourceKey("field/main_chr/d000.mch"), [1, 2]);
        Assert.Same(source.ModelSourceDescriptor, materials.TextureSourceDescriptor);
        Assert.Equal(new[] { "field/main_chr/d000.mch" }, donor.Reads);
        Assert.Empty(primary.Reads);
    }

    [Fact]
    public void OriginalImageResolutionUsesTheSelectedContextAndDisposesBothSources()
    {
        TestSource primary = new("primary", GameDataEdition.Classic);
        TestSource donor = new("donor", GameDataEdition.Remastered);
        SelectedModelResourceSource source = new(primary, donor);
        TextureKey key = new(new ResourceKey("battle/test.dat"), "texture/0");
        TextureBuildContext primaryContext = new(key, primary.Descriptor, 1, "classic-battle", 1, 1, () => new PixelImage(1, 1));
        TextureBuildContext selectedContext = new(key, source.ModelSourceDescriptor, 1, "classic-battle", 1, 1, () => new PixelImage(1, 1));
        Assert.Equal("primary", source.Resolve(primaryContext)!.Provenance);
        Assert.Equal("donor", source.Resolve(selectedContext)!.Provenance);
        source.Dispose();
        source.Dispose();
        Assert.Equal(1, primary.DisposeCount);
        Assert.Equal(1, donor.DisposeCount);
    }

    [Fact]
    public void ChangedFieldFileWinsOverSelectedInstallation()
    {
        TestSource primary = new("primary", GameDataEdition.Classic);
        TestSource donor = new("donor", GameDataEdition.Remastered);
        using SelectedModelResourceSource source = new(primary, donor);
        ResourceKey owner = new("field/test/chara.one");
        ResourceService resources = WithOverrides(source, new EncodedResource(owner, "one", 1, "explicit", [9]));
        IFieldModelSource decoder = resources.GetSource<IFieldModelSource>()!;
        FieldModel single = decoder.ReadModel(owner, resources.Read(owner), _ => [], 0);
        IReadOnlyList<FieldModel> all = decoder.ReadModels(owner, resources.Read(owner), _ => []);
        Assert.Null(single.TextureSourceDescriptor);
        Assert.Null(Assert.Single(all).TextureSourceDescriptor);
        Assert.Equal(new byte[] { 9 }, primary.LastPayload);
        Assert.Empty(donor.Reads);
        ResourceService independent = new(source);
        FieldModel baseModel = independent.GetSource<IFieldModelSource>()!.ReadModel(owner, [9], _ => [], 0);
        Assert.Same(source.ModelSourceDescriptor, baseModel.TextureSourceDescriptor);
        Assert.Null(decoder.ReadModel(owner, resources.Read(owner), _ => [], 0).TextureSourceDescriptor);
    }

    [Fact]
    public void IdenticalExplicitBattleAndWeaponFilesWinOverSelectedInstallation()
    {
        TestSource primary = new("primary", GameDataEdition.Classic);
        TestSource donor = new("donor", GameDataEdition.Remastered) { Missing = true };
        using SelectedModelResourceSource source = new(primary, donor);
        ResourceKey bodyOwner = new("battle/d0c000.dat");
        ResourceKey weaponOwner = new("battle/d0w000.dat");
        ResourceService resources = WithOverrides(source, new EncodedResource(bodyOwner, "dat", 1, "explicit", [1, 2]), new EncodedResource(weaponOwner, "dat", 1, "explicit", [1, 2]));
        IBattleModelSource decoder = resources.GetSource<IBattleModelSource>()!;
        BattleModel body = decoder.ReadModel(bodyOwner, resources.Read(bodyOwner));
        BattleModel weapon = decoder.ReadWeapon(weaponOwner, resources.Read(weaponOwner), body);
        Assert.Null(body.TextureSourceDescriptor);
        Assert.Null(weapon.TextureSourceDescriptor);
        Assert.Equal(new byte[] { 1, 2 }, primary.LastPayload);
        Assert.Empty(donor.Reads);
    }

    [Fact]
    public void ChangedMainCharacterDependencyOnlyOverridesItsOwnModels()
    {
        TestSource primary = new("primary", GameDataEdition.Classic) { ReadMain = true, ModelNames = ["d000", "d001", "a000"], MaterialAliases = new() { ["a000"] = "d000" } };
        TestSource donor = new("donor", GameDataEdition.Remastered) { ReadMain = true, ModelNames = ["d000", "d001", "a000"], MaterialAliases = new() { ["a000"] = "d000" } };
        using SelectedModelResourceSource source = new(primary, donor);
        ResourceKey owner = new("field/test/chara.one");
        ResourceKey materialOwner = new("field/main_chr/d000.mch");
        ResourceService resources = WithOverrides(source, new EncodedResource(materialOwner, "mch", 1, "explicit", [1, 2]));
        IFieldModelSource decoder = resources.GetSource<IFieldModelSource>()!;
        byte[] ReadMain(string name) => resources.Read(new ResourceKey($"field/main_chr/{name}.mch"));
        IReadOnlyList<FieldModel> all = decoder.ReadModels(owner, [1, 2], ReadMain);
        Assert.Null(all[0].TextureSourceDescriptor);
        Assert.Same(source.ModelSourceDescriptor, all[1].TextureSourceDescriptor);
        Assert.Null(all[2].TextureSourceDescriptor);
        Assert.DoesNotContain("field/main_chr/d000.mch", donor.Reads);
        Assert.Contains("field/main_chr/d001.mch", donor.Reads);
        donor.Reads.Clear();
        FieldModel single = decoder.ReadModel(owner, [1, 2], ReadMain, 0);
        Assert.Null(single.TextureSourceDescriptor);
        Assert.Empty(donor.Reads);
        FieldModel materials = decoder.ReadMainCharacterMaterials(materialOwner, resources.Read(materialOwner));
        Assert.Null(materials.TextureSourceDescriptor);
        Assert.Empty(donor.Reads);
    }

    [Fact]
    public void SameEditionInstallationsRouteOnlySelectedModelRoleToDonor()
    {
        TestSource primary = new("classic", GameDataEdition.Classic);
        TestSource donor = new("classic", GameDataEdition.Classic);
        using SelectedModelResourceSource source = new(primary, donor);
        Assert.Equal("classic/models/classic", source.ModelSourceDescriptor.Id);
        Assert.Equal(donor.Descriptor.Edition, source.ModelSourceDescriptor.Edition);
        Assert.Equal(donor.Descriptor.Layouts, source.ModelSourceDescriptor.Layouts);
        ResourceKey owner = new("battle/d0c000.dat");
        TextureKey texture = new(owner, "texture/0");
        TextureBuildContext Context(ResourceSourceDescriptor descriptor) => new(texture, descriptor, 1, "classic-battle", 1, 1, () => new PixelImage(1, 1));
        source.Resolve(Context(primary.Descriptor));
        Assert.Equal(1, primary.ResolveCount);
        Assert.Equal(0, donor.ResolveCount);
        BattleModel selected = source.ReadModel(owner, [1, 2]);
        Assert.Same(source.ModelSourceDescriptor, selected.TextureSourceDescriptor);
        source.Resolve(Context(selected.TextureSourceDescriptor!));
        Assert.Equal(1, primary.ResolveCount);
        Assert.Equal(1, donor.ResolveCount);
        Assert.Same(donor.Descriptor, donor.LastResolvedSource);
        ResourceService resources = WithOverrides(source, new EncodedResource(owner, "dat", 1, "explicit", [1, 2]));
        BattleModel explicitModel = resources.GetSource<IBattleModelSource>()!.ReadModel(owner, resources.Read(owner));
        Assert.Null(explicitModel.TextureSourceDescriptor);
        source.Resolve(Context(explicitModel.TextureSourceDescriptor ?? resources.Source));
        Assert.Equal(2, primary.ResolveCount);
        Assert.Equal(1, donor.ResolveCount);
    }

    private static ResourceService WithOverrides(IResourceSource source, params EncodedResource[] overrides)
    {
        ResourceService resources = new(source);
        resources.Prepare(null, encodedResources: overrides).Commit();
        return resources;
    }

    private sealed class TestSource(string id, GameDataEdition edition) : IResourceSource, IResourcePrefixSource, IGameFileSource, IFieldModelSource, IBattleModelSource, ITextureReplacementResolver
    {
        public ResourceSourceDescriptor Descriptor { get; } = new(id, edition, []);
        public List<string> Reads { get; } = [];
        public bool Missing { get; init; }
        public bool ReadMain { get; init; }
        public string ModelName { get; init; } = "d000";
        public string[]? ModelNames { get; init; }
        public Dictionary<string, string> MaterialAliases { get; init; } = [];
        public byte[]? LastPayload { get; private set; }
        public ResourceSourceDescriptor? LastResolvedSource { get; private set; }
        public int ResolveCount { get; private set; }
        public int DisposeCount { get; private set; }

        public ResourceContent? Find(ResourceKey key)
        {
            Reads.Add(key.Path);
            return Missing ? null : new(key, "bin", 1, Descriptor.Id, () => new MemoryStream([1, 2]));
        }

        public ResourcePrefix? ReadPrefix(ResourceKey key, int maximumBytes) => new([1], 2);
        public string ResolveFile(string relativeDataPath) => relativeDataPath;

        public IReadOnlyList<FieldModel> ReadModels(ResourceKey owner, byte[] one, Func<string, byte[]> mainCharacter, Action<NativeFieldOneModelEntry>? retainEntry = null)
        {
            return (ModelNames ?? [ModelName]).Select(name => DecodeField(name, one, mainCharacter)).ToArray();
        }

        public FieldModel ReadModel(ResourceKey owner, byte[] one, Func<string, byte[]> mainCharacter, int index) => DecodeField((ModelNames ?? [ModelName])[index], one, mainCharacter);

        private FieldModel DecodeField(string name, byte[] one, Func<string, byte[]> mainCharacter)
        {
            LastPayload = one;
            string material = MaterialAliases.GetValueOrDefault(name, name);
            if (ReadMain)
            {
                _ = mainCharacter(material);
            }
            return new(name, [], [], [], [], []) { MainCharacterTextures = ReadMain, TextureResourceName = ReadMain ? material : null };
        }

        public BattleModel ReadModel(ResourceKey owner, byte[] content)
        {
            LastPayload = content;
            return new(Path.GetFileName(owner.Path), [], [], [], [], 1);
        }

        public BattleModel ReadWeapon(ResourceKey owner, byte[] content, BattleModel character) => ReadModel(owner, content);

        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            ResolveCount++;
            LastResolvedSource = context.Source;
            return new(new PixelImage(1, 1), Descriptor.Id, 1, 1);
        }
        public FieldModel ReadMainCharacterMaterials(ResourceKey owner, byte[] content) => new(ModelName, [], [], [], [], []);
        public void Dispose() => DisposeCount++;
    }
}
