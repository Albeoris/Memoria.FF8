namespace Memoria.FF8.Runtime;

/// <summary>The shared battle texture's three native pages and their later raw archive uploads.</summary>
public static class NativeBattleSharedEffectTexture
{
    public static NativeBattleTextureFileIdentity File { get; } = new("a8def.tim", NativeBattleTextureDomain.Battle);

    internal static bool IsCommonImage(NativeBattleTextureOrigin origin) => origin is { Domain: NativeBattleTextureDomain.Battle, Kind: NativeBattleTextureSourceKind.Tim, Owner: "a8def.tim", Role: NativeBattleTextureRole.Pixels, BitsPerPixel: 4, WidthWords: 192, Height: 256 };

    internal static NativeBattleTextureOrigin? RawFile(NativeBattleTextureFileIdentity identity, NativeBattleEffectTextureRectangle rectangle)
    {
        string name = identity.Name.ToLowerInvariant();
        if (identity.Domain != NativeBattleTextureDomain.Battle || rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return null;
        }
        NativeBattleTextureOrigin origin = new(name, 0, NativeBattleTextureRole.Pixels, 8, rectangle.Width, rectangle.Height) { Domain = NativeBattleTextureDomain.Battle, Kind = NativeBattleTextureSourceKind.Image, SourceOwner = (identity.SourceOwner ?? identity.Name).ToLowerInvariant(), SourceOffset = identity.SourceOffset };
        if (name == "b0wave.dat")
        {
            origin = origin with { Owner = "a8def.tim" };
        }
        if (name.Length == 10 && name.StartsWith("ma8def_p.", StringComparison.Ordinal) && name[9] is >= '0' and <= '2' && rectangle.Width == 64 && rectangle.Height == 256)
        {
            origin = origin with { Owner = $"a8def{name[9]}.tim", BitsPerPixel = 4 };
        }
        return origin;
    }
}
