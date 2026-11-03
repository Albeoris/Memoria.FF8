using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;

namespace Memoria.FF8.Assets.Remastered;

public sealed partial class RemasteredGameDataSource
{
    public NativeWorldStaticTextureSheet? ReadWorldStaticTextureSheet(string? sourceFieldName, TimImage upload, ushort texturePage)
    {
        if (sourceFieldName is null || !new[] { "bcport_2", "dosea_2", "dotown_1" }.Any(name => sourceFieldName.Contains(name, StringComparison.Ordinal)) || upload.X != 992 || upload.Y != 64 || upload.WidthInWords != 32 || upload.Height != 64 || texturePage != 15)
        {
            return null;
        }
        _ = RequireNativeRuntimeProfile();
        const string path = "textures/field.fs/field_hd_new/wmset_014_0.png";
        PixelImage image = ReadArtworkImage(path) ?? throw new InvalidDataException("The native world static texture registration requires its full-color source sheet.");
        _ = NativeArtworkTexture.Scale(image, 256, 256);
        return new(image, 256, 256, 0, 0);
    }
}
