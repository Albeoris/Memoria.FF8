using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

internal static class NativeFieldModelTextureSheets
{
    public static TextureSourceAddress? Address(FieldModel model, int ordinal)
    {
        string? name = model.TextureResourceName;
        if (name is not { Length: 4 } || !char.IsAsciiLetter(name[0]) || name.Skip(1).Any(character => !char.IsAsciiDigit(character)) || ordinal < 0 || ordinal >= model.TextureRegions.Count || ordinal >= model.Textures.Count)
        {
            return null;
        }
        TextureSourceRegion region = model.TextureRegions[ordinal];
        if (region.BitsPerPixel is not (4 or 8) || region.CanvasWidth is <= 0 or > 128 || region.CanvasHeight is <= 0 or > 128 || model.Textures[ordinal].Width != region.CanvasWidth || model.Textures[ordinal].Height != region.CanvasHeight || model.TextureSourceVariant is not (null or "alpha"))
        {
            return null;
        }
        string variant = model.TextureSourceVariant is { } sourceVariant ? "/" + sourceVariant : "";
        return new(new($"field/model/{(model.MainCharacterTextures ? "main" : "second")}_chr/{name}"), $"row/{ordinal}{variant}", 0, 0, 0, region.CanvasWidth, region.CanvasHeight, 128, 128);
    }

    public static string? Variant(string? name, int fieldId)
    {
        // NG-007: the native field identity selects this alternate source material.
        return name == "o006" && fieldId is 0x36F or 0x370 ? "alpha" : null;
    }
}
