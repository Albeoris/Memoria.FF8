using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeSoftwareTextureState([property: JsonRequired] uint? LastTPage, [property: JsonRequired] ushort? EncodedClut, [property: JsonRequired] int? TextureX, [property: JsonRequired] int? TextureY, [property: JsonRequired] int? TextureByteOffset, [property: JsonRequired] int? PaletteByteOffset, [property: JsonRequired] int? Depth, [property: JsonRequired] byte? Abr, [property: JsonRequired] int? PageId)
{
    [JsonRequired]
    public bool? TexturePointerIsNull { get; init; }
    [JsonRequired]
    public bool? PalettePointerIsNull { get; init; }
    public static NativeSoftwareTextureState FromColdProcess() => new(0, 0, 0, 0, null, null, 0, 0, 0) { TexturePointerIsNull = true, PalettePointerIsNull = true };

    public static NativeSoftwareTextureState Unknown() => new(null, null, null, null, null, null, null, null, null);
}
public sealed record NativeSoftwareTexturePagePreparation(NativeSoftwareTextureState State, bool Depth3Diagnostic);

public static class NativeSoftwareTexturePreparation
{
    public static NativeSoftwareTexturePagePreparation PrepareTPage45B960(NativeSoftwareTextureState state, ushort page)
    {
        ArgumentNullException.ThrowIfNull(state);
        uint previousPage = state.LastTPage ?? throw new NotSupportedException("Native texture page preparation requires its retained cached page comparison owner.");
        if (previousPage == page)
        {
            return new(state, false);
        }
        int x = (page & 15) << 6;
        int y = (page & 0x10) << 4;
        int depth = (page >> 7) & 3;
        bool diagnostic = depth == 3;
        return new(state with { LastTPage = page, TextureX = x, TextureY = y, TextureByteOffset = 2 * (y * 1024 + x), TexturePointerIsNull = false, Depth = diagnostic ? 1 : depth, Abr = (byte)((page >> 5) & 3), PageId = y / 256 * 16 + x / 64 }, diagnostic);
    }

    public static NativeSoftwareTextureState PrepareClut45B920(NativeSoftwareTextureState state, ushort clut, int imageLength)
    {
        ArgumentNullException.ThrowIfNull(state);
        ushort encoded = (ushort)(clut & 0x7FFF);
        int offset = encoded * 32;
        if (imageLength < 0 || offset + 512L > imageLength)
        {
            throw new NotSupportedException("Native software CLUT requires its complete256-entry palette inside the actual image owner.");
        }
        return state with { EncodedClut = encoded, PaletteByteOffset = offset, PalettePointerIsNull = false };
    }
}
