using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeMainCharacterNoOpResourceTests
{
    [Fact]
    public void ExactInstalledPlaceholderIntroducesNoNewGeometryContract()
    {
        ResourceKey key = new("field/main_chr/d000.mch");
        EncodedResource original = new(key, "mch", 1, "installed", "Installed placeholder content."u8.ToArray());
        EncodedResource selected = new(key, "mch", 2, "fixture", "Installed placeholder content."u8.ToArray());
        GameAssets.ValidateMainCharacterResource(selected.Content, original.Content);
        Assert.Throws<EndOfStreamException>(() => GameAssets.ValidateMainCharacterResource(selected.Content));
        EncodedResource changed = new(key, "mch", 3, "fixture", "Modified placeholder content."u8.ToArray());
        Assert.Throws<EndOfStreamException>(() => GameAssets.ValidateMainCharacterResource(changed.Content, original.Content));
    }

    [Fact]
    public void MatchingBytesDoNotAliasAnotherInstalledIdentity()
    {
        EncodedResource selected = new(new("field/main_chr/d000.mch"), "mch", 1, "fixture", [1, 2, 3]);
        EncodedResource other = new(new("field/main_chr/d001.mch"), "mch", 1, "installed", [1, 2, 3]);
        Assert.Throws<ArgumentException>(() => GameAssets.ValidateMainCharacterResource(selected.Content, other.Content));
    }
}
