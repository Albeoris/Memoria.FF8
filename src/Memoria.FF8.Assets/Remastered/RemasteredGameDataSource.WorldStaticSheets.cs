using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Remastered;

public sealed partial class RemasteredGameDataSource
{
    public const string WorldStaticSheetStem = "textures/field.fs/field_hd_new/wmset_014_0";

    public NativeWorldStaticTextureSheet? ReadWorldStaticTextureSheet(string? sourceFieldName, TimImage upload, ushort texturePage)
    {
        if (sourceFieldName is null || !new[] { "bcport_2", "dosea_2", "dotown_1" }.Any(name => sourceFieldName.Contains(name, StringComparison.Ordinal)) || upload.X != 992 || upload.Y != 64 || upload.WidthInWords != 32 || upload.Height != 64 || texturePage != 15)
        {
            return null;
        }
        _ = RequireNativeRuntimeProfile();
        PixelImage image = ReadArtworkImage(WorldStaticSheetStem + ".png") ?? throw new InvalidDataException("The native world static texture registration requires its full-color source sheet.");
        _ = NativeArtworkTexture.Scale(image, 256, 256);
        return new(image, 256, 256, 0, 0) { SourceAlias = new(new ResourceKey(WorldStaticSheetStem), "", 0, 0, 0, 256, 256, 256, 256) };
    }
}
