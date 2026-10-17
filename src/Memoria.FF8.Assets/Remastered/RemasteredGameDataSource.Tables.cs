using System.Security.Cryptography;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Remastered;

public sealed partial class RemasteredGameDataSource
{
    private RemasteredGuestImage? guestImage;

    private ResourceContent? FindNamedTable(ResourceKey key)
    {
        string? textName = key.Path switch
        {
            "text/draw-points" => "off_text_draw_point",
            "text/card-names" => "off_cards_names",
            "text/card-names-alternate" => "off_cards_names2",
            "text/card-descriptions" => "byte_card_texts",
            "text/enemy-scan" => "desc_scan",
            _ => null
        };
        if (textName is not null)
        {
            string path = $"data/lang-{language}/{textName}_{language}.dat";
            return Contains(path) ? Content(key, path) : null;
        }
        if (NamedTableResources.Find(key) is not { } layout)
        {
            return null;
        }
        Lazy<byte[]> bytes = new(() =>
        {
            if (language == "jp")
            {
                throw new NotSupportedException("Named guest tables have no authenticated layout for the selected language.");
            }
            if (guestImage is null)
            {
                string path = Path.Combine(gameDirectory, "FFVIII_EN.exe.dat");
                using FileStream file = File.OpenRead(path);
                if (!Convert.ToHexString(SHA256.HashData(file)).Equals("C52C42DBFE0EB3BBE17E3AA5A1E0607921B5A23CB9B2906D81844905106EA26F", StringComparison.Ordinal))
                {
                    throw new NotSupportedException("Named guest tables require an authenticated source image.");
                }
                guestImage = new RemasteredGuestImage(path);
            }
            return guestImage.Read(layout.Address, layout.Length);
        });
        return new ResourceContent(key, "bin", 1, $"{Descriptor.Id}:{key}", () =>
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return new MemoryStream(bytes.Value, false);
        });
    }
}
