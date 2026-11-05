using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OriginalSaveEditionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria-native-save-editions-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void An_unknown_selected_installation_does_not_guess_a_native_bank()
    {
        string documents = Path.Combine(directory, "documents");
        Directory.CreateDirectory(Path.Combine(documents, "Square Enix", "FINAL FANTASY VIII Steam", "user_111"));
        string installation = Directory.CreateDirectory(Path.Combine(directory, "unknown-installation")).FullName;
        Assert.Empty(OriginalSaveLocation.FindCandidates(documents, installation));
    }

    [Fact]
    public void Selected_edition_discovers_only_its_existing_native_profiles()
    {
        string documents = Path.Combine(directory, "documents");
        string classic = Directory.CreateDirectory(Path.Combine(documents, "Square Enix", "FINAL FANTASY VIII Steam", "user_111")).FullName;
        string remasteredRoot = Path.Combine(documents, "My Games", "FINAL FANTASY VIII Remastered");
        string steam = Directory.CreateDirectory(Path.Combine(remasteredRoot, "Steam", "111", "game_data", "user", "saves")).FullName;
        string gog = Directory.CreateDirectory(Path.Combine(remasteredRoot, "GOG", "0", "game_data", "user", "saves")).FullName;
        Directory.CreateDirectory(Path.Combine(remasteredRoot, "Steam", "unrelated", "game_data", "user", "saves"));
        Directory.CreateDirectory(Path.Combine(remasteredRoot, "Steam", "222"));
        Assert.Equal(new[] { classic }, OriginalSaveLocation.FindCandidates(documents));
        Assert.Equal(new[] { classic }, OriginalSaveLocation.FindCandidates(documents, Installation(false)));
        Assert.Equal(new[] { gog, steam }, OriginalSaveLocation.FindCandidates(documents, Installation(true)));
        Assert.False(Directory.Exists(Path.Combine(remasteredRoot, "Steam", "222", "game_data")));
    }

    [Fact]
    public void Missing_remastered_profiles_do_not_fall_back_to_classic_or_create_an_account()
    {
        string documents = Path.Combine(directory, "documents");
        Directory.CreateDirectory(Path.Combine(documents, "Square Enix", "FINAL FANTASY VIII Steam", "user_111"));
        string remasteredRoot = Path.Combine(documents, "My Games", "FINAL FANTASY VIII Remastered");
        Assert.Empty(OriginalSaveLocation.FindCandidates(documents, Installation(true)));
        Assert.False(Directory.Exists(remasteredRoot));
    }

    [Fact]
    public void Explicit_native_directory_remains_authoritative_for_either_selected_edition()
    {
        string selected = Directory.CreateDirectory(Path.Combine(directory, "chosen-native-bank")).FullName;
        PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "memoria"), originalDirectory: selected, discoverOriginalSaves: false, gameDirectory: Installation(true));
        Assert.Equal(selected, store.OriginalDirectory);
        Assert.Equal(Path.Combine(selected, "slot2_save30.ff8"), store.GetPath(160));
        Assert.Empty(store.OriginalDirectoryCandidates);
    }

    [Theory]
    [InlineData("Steam", "111")]
    [InlineData("GOG", "0")]
    public void Remastered_publication_uses_the_common_payload_without_classic_metadata(string provider, string profile)
    {
        string saves = Directory.CreateDirectory(Path.Combine(directory, "documents", "My Games", "FINAL FANTASY VIII Remastered", provider, profile, "game_data", "user", "saves")).FullName;
        string metadata = Path.Combine(saves, "metadata.xml");
        File.WriteAllText(metadata, "unrelated data must remain untouched");
        byte[] block = new byte[NativePcSaveData.BlockSize];
        block[0] = (byte)'S';
        block[1] = (byte)'C';
        block[386] = 0xFF;
        block[387] = 0x08;
        byte[] payload = NativePcSaveCodec.Encode(new(block));
        string source = Path.Combine(directory, "pending.ff8");
        File.WriteAllBytes(source, payload);
        string destination = Path.Combine(saves, "slot1_save01.ff8");
        Assert.Null(OriginalSaveMetadata.Prepare(destination, payload));
        OriginalSavePublication.Publish(source, destination, overwrite: false);
        Assert.Equal(payload, File.ReadAllBytes(destination));
        Assert.Equal(NativePcSaveCodec.Decode(payload).Block, NativePcSaveCodec.Decode(File.ReadAllBytes(destination)).Block);
        Assert.Equal("unrelated data must remain untouched", File.ReadAllText(metadata));
    }

    private string Installation(bool remastered)
    {
        string installation = Directory.CreateDirectory(Path.Combine(directory, remastered ? "remastered" : "classic")).FullName;
        foreach (string marker in remastered ? new[] { "main.zzz", "other.zzz", "FFVIII_EN.exe.dat" } : new[] { "FF8_EN.exe" })
        {
            File.WriteAllBytes(Path.Combine(installation, marker), []);
        }
        return installation;
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
