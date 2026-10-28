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
        Assert.Same(source, resources.GetSource<IFieldModelSource>());
        Assert.Same(source, resources.GetSource<IBattleModelSource>());
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
        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => source.ReadModel(new ResourceKey("field/test/chara.one"), [1], _ => throw new InvalidOperationException("Primary callback must not read donor materials."), 0));
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
        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => source.ReadModel(new ResourceKey("field/test/chara.one"), [1], _ => [], 0));
        Assert.Contains("field/test/chara.one", exception.Message);
        Assert.Contains("donor", exception.Message);
    }

    [Fact]
    public void StandaloneMainCharacterMaterialsUseSelectedResourceAndDescriptor()
    {
        TestSource primary = new("primary", GameDataEdition.Classic);
        TestSource donor = new("donor", GameDataEdition.Remastered);
        using SelectedModelResourceSource source = new(primary, donor);
        FieldModel materials = source.ReadMainCharacterMaterials(new ResourceKey("field/main_chr/d000.mch"), [9]);
        Assert.Same(donor.Descriptor, materials.TextureSourceDescriptor);
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
        TextureBuildContext selectedContext = new(key, donor.Descriptor, 1, "classic-battle", 1, 1, () => new PixelImage(1, 1));
        Assert.Equal("primary", source.Resolve(primaryContext)!.Provenance);
        Assert.Equal("donor", source.Resolve(selectedContext)!.Provenance);
        source.Dispose();
        source.Dispose();
        Assert.Equal(1, primary.DisposeCount);
        Assert.Equal(1, donor.DisposeCount);
    }

    private sealed class TestSource(string id, GameDataEdition edition) : IResourceSource, IResourcePrefixSource, IGameFileSource, IFieldModelSource, ITextureReplacementResolver
    {
        public ResourceSourceDescriptor Descriptor { get; } = new(id, edition, []);
        public List<string> Reads { get; } = [];
        public bool Missing { get; init; }
        public bool ReadMain { get; init; }
        public string ModelName { get; init; } = "d000";
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
            if (ReadMain)
            {
                _ = mainCharacter(ModelName);
            }
            return [new(ModelName, [], [], [], [], [])];
        }

        public TextureReplacement? Resolve(TextureBuildContext context) => new(new PixelImage(1, 1), Descriptor.Id, 1, 1);
        public FieldModel ReadMainCharacterMaterials(ResourceKey owner, byte[] content) => new(ModelName, [], [], [], [], []);
        public void Dispose() => DisposeCount++;
    }
}
