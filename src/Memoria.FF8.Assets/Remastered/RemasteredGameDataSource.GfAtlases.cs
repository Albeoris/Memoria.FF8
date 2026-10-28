using System.Collections.Immutable;
using System.Globalization;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Remastered;

public sealed partial class RemasteredGameDataSource : INativeBattleGfAtlasSource
{
    public TextureSourceAtlasAddress? TryGetGfAtlasAddress(ImmutableArray<NativeBattleTextureRegistration> registrations, ushort page, ushort clut, int x, int y, int width, int height, TexturePrimitiveSample? sample = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return RemasteredBattleGfAtlasAddressing.Create(ReadBattleImageTable(), registrations, page, x, y, width, height, language, sample);
    }
}

/// <summary>Native upload recognition and sampled-page ownership, using the selected source's image table.</summary>
public static class RemasteredBattleGfAtlasAddressing
{
    public static TextureSourceAtlasAddress? Create(ImmutableArray<RemasteredBattleImageRecord> table, ImmutableArray<NativeBattleTextureRegistration> registrations, ushort page, int x, int y, int width, int height, string language = "en", TexturePrimitiveSample? sample = null)
    {
        if (table.IsDefault || registrations.IsDefault || x < 0 || y < 0 || width <= 0 || height <= 0 || x + (long)width > 256 || y + (long)height > 256)
        {
            throw new InvalidDataException("Native GF image lookup requires its initialized table, retained registrations and bounded sampled page rectangle.");
        }
        int physicalPage = page & 31;
        int depth = (page >> 7) & 3;
        if (depth != 1)
        {
            return null;
        }
        foreach (NativeBattleTextureRegistration registration in registrations)
        {
            if (!registration.Active || registration.OwnedSpans.IsEmpty || !CoversPage(registration.X, registration.Y, registration.WidthWords, registration.Height, physicalPage))
            {
                continue;
            }
            NativeBattleTextureOrigin origin = registration.Origin;
            if (origin.Domain is not (NativeBattleTextureDomain.Magic or NativeBattleTextureDomain.Battle) || origin.Role != NativeBattleTextureRole.Pixels || origin.BitsPerPixel != 8 || !TryEffectIdentity(origin.Owner, out int identity))
            {
                return null;
            }
            RemasteredBattleImageRecord? match = table.FirstOrDefault(record => record.Identity == identity && record.Mode != 0 && record.X == registration.X && record.Y == registration.Y && record.WidthWords == registration.WidthWords && record.Height == registration.Height);
            if (match is null && identity == 205 && registration.Y == 256 && registration.WidthWords == 64 && registration.Height == 128 && registration.X is 704 or 768 or 832)
            {
                int rawComponent = (832 - registration.X) / 64;
                match = table.FirstOrDefault(record => record.Identity == identity && (record.Member == rawComponent || record.Member < 0));
                if (match is not null)
                {
                    match = match with { Member = rawComponent };
                }
            }
            if (match is null && origin.Kind == NativeBattleTextureSourceKind.Tim && TryTimComponent(identity, physicalPage, origin.Member, out int component))
            {
                match = table.FirstOrDefault(record => record.Identity == identity && (record.Member == component || record.Member < 0));
                if (match is not null)
                {
                    match = match with { Member = component };
                }
            }
            if (match is null)
            {
                return null;
            }
            if (match.Member is < 0 or > 3 || match.Kind is < 1 or > 2)
            {
                throw new NotSupportedException("Native GF image selector has an unsupported atlas component contract.");
            }
            TextureSourceAtlasAddress address = new(match.Name, match.Member * 2, match.Kind, 256, 256, x, y, width, height) { Variant = identity == 991 && language == "jp" ? "_jp" : "" };
            return RemasteredBattleGfAtlasBanks.Apply(address, identity, match.Member, registration, sample);
        }
        return null;
    }

    private static bool TryTimComponent(int identity, int page, int sourceMember, out int component)
    {
        component = sourceMember;
        if (identity is 95 or 184 or 902)
        {
            component = 0;
            return page == 29;
        }
        if (identity == 99)
        {
            component = 28 - page;
            return page is 27 or 28;
        }
        if (identity is 186 or 190 or 324 or 325)
        {
            component = 29 - page;
            return page is 28 or 29;
        }
        if (identity == 326)
        {
            component = 29 - page;
            return page is >= 26 and <= 29;
        }
        if (identity == 217)
        {
            component = 29 - page;
            return page is >= 27 and <= 29;
        }
        return identity is >= 85 and <= 94 or 115 or 139 or 277 or 290 or 900 or 901;
    }

    private static bool CoversPage(int x, int y, int widthWords, int height, int page)
    {
        int left = x / 64;
        int right = (x + widthWords + 63) / 64;
        int column = page & 15;
        int row = page / 16;
        return column >= left && column < right && y < (row + 1) * 256 && y + height > row * 256;
    }

    private static bool TryEffectIdentity(string owner, out int identity)
    {
        identity = 0;
        string name = Path.GetFileName(owner);
        return name.Length > 6 && name.StartsWith("mag", StringComparison.OrdinalIgnoreCase) && name[6] is '_' or '.' && int.TryParse(name.AsSpan(3, 3), NumberStyles.None, CultureInfo.InvariantCulture, out identity);
    }
}
