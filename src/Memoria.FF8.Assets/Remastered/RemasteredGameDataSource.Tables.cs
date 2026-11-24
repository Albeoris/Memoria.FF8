using System.Security.Cryptography;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Remastered;

public sealed partial class RemasteredGameDataSource : IGameRuntimeSource, IGameNativeImageSource
{
    private const string GuestImageName = "FFVIII_EN.exe.dat";
    private const string GuestImageAlias = "ff8_en.exe.dat";
    private RemasteredGuestImage? guestImage;
    private bool runtimeCodeVerified;

    public string? NativeExecutableHash
    {
        get
        {
            lock (archiveGate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                return guestImage is not null || File.Exists(Path.Combine(gameDirectory, GuestImageName)) || Contains(GuestImageName) || Contains(GuestImageAlias) ? SourceHash : null;
            }
        }
    }
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
    public bool IsExecutableAddress(uint address) => RequireGuestImage().IsExecutableAddress(address);
    public bool IsWritableRange(uint address, int count) => RequireGuestImage().IsWritableRange(address, count);
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
        if (key.Path == "runtime/disc-wrapper")
        {
            _ = RequireNativeRuntimeProfile();
            return new ResourceContent(key, "dll", 1, $"{Descriptor.Id}:{key}", OpenAuthenticatedRuntimeCode);
        }
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
        lock (archiveGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return guestImage ??= LoadGuestImage();
        }
    }

    private RemasteredGuestImage LoadGuestImage()
    {
        string path = Path.Combine(gameDirectory, GuestImageName);
        if (File.Exists(path))
        {
            using FileStream looseImage = File.OpenRead(path);
            if (ReadAuthenticatedGuestImage(looseImage) is { } image)
            {
                return image;
            }
        }

        foreach (string name in new[] { GuestImageName, GuestImageAlias })
        {
            if (Contains(name))
            {
                using Stream archivedImage = Open(name);
                if (ReadAuthenticatedGuestImage(archivedImage) is { } image)
                {
                    return image;
                }
            }
        }

        throw new NotImplementedException("The English Remastered guest image in FFVIII_EN.exe.dat and its mounted archive copies did not match a supported source. Verify the game files in Steam and try again.");
    }

    private static RemasteredGuestImage? ReadAuthenticatedGuestImage(Stream image)
    {
        if (Convert.ToHexString(SHA256.HashData(image)) != Memoria.FF8.Runtime.NativeFieldRuntimeProfile.RemasteredEnImageHash)
        {
            return null;
        }
        image.Position = 0;
        return new RemasteredGuestImage(image, leaveOpen: true);
    }
}
