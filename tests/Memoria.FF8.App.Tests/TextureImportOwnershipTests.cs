using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class TextureImportOwnershipTests
{
    [Fact]
    public void Global_sheet_matches_each_live_local_consumer_and_reports_the_selected_variant_owner()
    {
        TextureTarget target = new("ff8", "global/chara/squall.mch", "texture/0", Variant: "eyes-closed");
        TextureSourceSnapshot first = Snapshot("field/one/one.mch", target with { Variant = "" }, "original/source; variant eyes-closed: Memoria/closed/texture.png");
        TextureSourceSnapshot second = Snapshot("field/two/chara.one", target with { Variant = "" }, "original/source; variant eyes-closed: Memoria/closed/texture.png");
        TextureSourceSnapshot unrelated = Snapshot("field/three/chara.one", target with { Owner = "global/chara/zell.mch", Variant = "" }, "Memoria/closed/unrelated.png");
        TextureImportOwnershipResult closed = TextureImportOwnership.Inspect(target, [first, second, unrelated], "Memoria/closed");
        Assert.Equal(2, closed.Consumers);
        Assert.Equal(2, closed.OwnedConsumers);
        Assert.Equal("Memoria/closed/texture.png", Assert.Single(closed.Owners));
        TextureImportOwnershipResult open = TextureImportOwnership.Inspect(target with { Variant = "" }, [first, second], "Memoria/closed");
        Assert.Equal(0, open.OwnedConsumers);
        Assert.Equal("original/source", Assert.Single(open.Owners));
        Assert.False(TextureImportOwnership.Matches(target with { Game = "ff7" }, first));
        Assert.False(TextureImportOwnership.Matches(target with { Palette = 1 }, first));
        Assert.False(TextureImportOwnership.Matches(target with { Variant = "undeclared" }, first));
    }

    [Fact]
    public void Native_alias_ownership_uses_the_actual_priority_winner_instead_of_the_local_source_key()
    {
        TextureTarget alias = new("ff8", "battle/shared.tim", "image/0", 3);
        PixelImage image = new(1, 1, [new Rgba32(10, 20, 30)]);
        TextureBuildContext context = new(new(new("battle/local.dat"), "texture/0"), new("synthetic", GameDataEdition.Classic, []), 1, "classic-battle", 1, 1, () => image)
        {
            NativeAlias = new(alias.Key.Owner, alias.Subresource, alias.Palette, 0, 0, 1, 1, 1, 1)
        };
        TextureSourceSnapshot snapshot = new(context, new(image, 1, 1, "Memoria/higher-priority/shared.png"), 1);
        TextureImportOwnershipResult result = TextureImportOwnership.Inspect(alias, [snapshot], "Memoria/imported");
        Assert.Equal(1, result.Consumers);
        Assert.Equal(0, result.OwnedConsumers);
        Assert.Contains("overridden by Memoria/higher-priority/shared.png", result.Describe());
        Assert.False(TextureImportOwnership.Matches(alias with { Palette = 0 }, snapshot));
    }

    private static TextureSourceSnapshot Snapshot(string localOwner, TextureTarget sheet, string provenance)
    {
        PixelImage image = new(1, 1, [new Rgba32(10, 20, 30)]);
        TextureBuildContext context = new(new(new(localOwner), "registered/texture/0"), new("synthetic", GameDataEdition.Classic, []), 1, "classic-field-model", 1, 1, () => image)
        {
            NativeSheet = new(sheet.Key.Owner, sheet.Subresource, sheet.Palette, 0, 0, 1, 1, 1, 1)
        };
        return new(context, new(image, 1, 1, provenance, Variants: new Dictionary<string, PixelImage> { ["eyes-closed"] = image }), 1);
    }
}
