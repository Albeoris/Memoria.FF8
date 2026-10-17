using System.Security.Cryptography;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Remastered;

public sealed partial class RemasteredGameDataSource : IGameRuntimeSource, IGameNativeMemorySource
{
    private RemasteredGuestImage? guestImage;
    private bool runtimeCodeVerified;

    public string? NativeExecutableHash => language == "en" && File.Exists(Path.Combine(gameDirectory, "FFVIII_EN.exe.dat")) ? SourceHash : null;
    public string SourceHash
    {
        get
        {
            _ = RequireGuestImage();
            return Memoria.FF8.Runtime.NativeFieldRuntimeProfile.RemasteredEnImageHash;
        }
    }
    public byte[] Read(uint address, int count) => RequireGuestImage().Read(address, count);
    public byte[] ReadLoader(uint address, int count) => RequireGuestImage().ReadLoader(address, count);
    public Memoria.FF8.Runtime.NativeFieldRuntimeProfile? NativeRuntimeProfile
    {
        get
        {
            if (NativeExecutableHash is null)
            {
                return null;
            }
            RequireRuntimeCode();
            return Memoria.FF8.Runtime.NativeFieldRuntimeProfile.RemasteredEn;
        }
    }

    private void RequireRuntimeCode()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!runtimeCodeVerified)
        {
            using FileStream file = OpenAuthenticatedRuntimeCode();
            runtimeCodeVerified = true;
        }
    }

    private FileStream OpenAuthenticatedRuntimeCode()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        FileStream file = File.OpenRead(Path.Combine(gameDirectory, "FFVIII_EFIGS.dll"));
        try
        {
            if (Convert.ToHexString(SHA256.HashData(file)) != "B0097E4EDD96FA2C32745B04328939EB8D182C2E89121D51405BC486B13F9108")
            {
                throw new NotImplementedException("Remastered runtime contracts require authenticated translated English code.");
            }
            file.Position = 0;
            return file;
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    public Memoria.FF8.Runtime.NativeFieldRuntimeProfile RequireNativeRuntimeProfile()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return NativeRuntimeProfile ?? throw new NotImplementedException("Remastered runtime contracts require the authenticated English image.");
    }

    private ResourceContent? FindNamedTable(ResourceKey key)
    {
        if (key.Path.StartsWith("runtime/", StringComparison.Ordinal))
        {
            throw new NotImplementedException($"Remastered runtime resource '{key}' has no implemented edition contract.");
        }
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
        Lazy<byte[]> bytes = new(() => Read(layout.Address, layout.Length));
        return new ResourceContent(key, "bin", 1, $"{Descriptor.Id}:{key}", () =>
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return new MemoryStream(bytes.Value, false);
        });
    }

    private RemasteredGuestImage RequireGuestImage()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (language != "en")
        {
            throw new NotImplementedException("Native guest data has no authenticated layout for the selected language.");
        }
        if (guestImage is null)
        {
            string path = Path.Combine(gameDirectory, "FFVIII_EN.exe.dat");
            using FileStream file = File.OpenRead(path);
            if (Convert.ToHexString(SHA256.HashData(file)) != Memoria.FF8.Runtime.NativeFieldRuntimeProfile.RemasteredEnImageHash)
            {
                throw new NotImplementedException("Native guest data requires an authenticated source image.");
            }
            guestImage = new RemasteredGuestImage(path);
        }
        return guestImage;
    }
}
