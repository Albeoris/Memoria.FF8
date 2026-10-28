using System.Text.Json;
using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadPracticeOptionsTests
{
    [Fact]
    public void ModifiedSamePlusPracticeOptionPersistsAcrossMatchReload()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed card resources are required.");
        string path = TestResources.ArtifactFile("practice-modified-same-plus.json");
        string resumedPath = TestResources.ArtifactFile("practice-modified-same-plus-resumed.json");
        Assert.Equal(0, TripleTriadPractice.Run(["--game-dir", directory, "--modified-same-plus", "On", "--blocked-cells", "2", "--rules", "Open,Same,Plus", "--save", path, "--autoplay"]));
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(directory);
        TripleTriadPractice.SavedMatch saved = TripleTriadPractice.Read(path, catalog);
        Assert.True(saved.Options!.UseModifiedSamePlus);
        Assert.True(saved.Match.UseModifiedSamePlus);
        Assert.Equal(2, saved.Options.BlockedCellCount);
        Assert.Equal(2, saved.Match.BlockedCells.Length);
        Assert.Equal(0, TripleTriadPractice.Run(["--game-dir", directory, "--match", path, "--save", resumedPath, "--autoplay"]));
        Assert.True(TripleTriadPractice.Read(resumedPath, catalog).Match.UseModifiedSamePlus);
        File.WriteAllText(resumedPath, JsonSerializer.Serialize(saved with { Options = saved.Options with { UseModifiedSamePlus = false } }));
        Assert.Throws<InvalidDataException>(() => TripleTriadPractice.Read(resumedPath, catalog));
    }
}
