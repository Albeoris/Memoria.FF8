using Memoria.FF8.Runtime;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class SaveTransferCommandTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-save-transfer-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ImportCanSelectModelResourcesWithoutChangingTheGameSource()
    {
        string primary = Path.Combine(directory, "game");
        string models = Path.Combine(directory, "models");
        SaveTransferCommand.Options options = SaveTransferCommand.Parse([Path.Combine(directory, "source.ff8"), "--output", Path.Combine(directory, "new.json"), "--game-dir", primary, "--model-source-dir", models], true);
        Assert.Equal(Path.GetFullPath(primary), options.GameDirectory);
        Assert.Equal(Path.GetFullPath(models), options.ModelSourceDirectory);
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse(["source.ff8", "--output", "new.ff8", "--model-source-dir", models], false));
    }

    [Fact]
    public void ExportCanDeclareTargetGameDefinitionsAndExplicitGuardianForceLoss()
    {
        SaveTransferCommand.Options options = SaveTransferCommand.Parse(["source.json", "--output", "out.ff8", "--game-dir", "game", "--allow-gf-loss"], false);
        Assert.Equal(Path.GetFullPath("game"), options.GameDirectory);
        Assert.True(options.AllowGuardianForceLoss);
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse(["source.json", "--output", "out.ff8", "--game-dir", "game", "--allow-gf-loss", "--allow-gf-loss"], false));
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse(["source.ff8", "--output", "out.json", "--game-dir", "game", "--allow-gf-loss"], true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Transfer_requires_a_source_and_separate_output(bool import)
    {
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse([], import));
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse(["--output", "out.ff8"], import));
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse(["source.ff8"], import));
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse(["source.ff8", "--output"], import));
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse(["source.ff8", "--output", "--game-dir"], import));
        string source = Path.Combine(directory, "source.ff8");
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse([source, "--output", Path.Combine(directory, ".", "source.ff8")], import));
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse([source, "--output", source.ToUpperInvariant()], import));
    }

    [Fact]
    public void Transfer_rejects_unknown_duplicate_and_inapplicable_options()
    {
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse(["source.ff8", "--output", "out.json", "--force"], true));
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse(["source.ff8", "--output", "one.json", "--output", "two.json"], true));
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse(["source.ff8", "--output", "out.json", "--game-dir", "one", "--game-dir", "two"], true));
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse(["source.json", "--output", "out.ff8", "--allow-gf-loss"], false));
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse(["source.json", "--output", "out.ff8", "--mods-dir", "mods"], false));
        Assert.Throws<ArgumentException>(() => SaveTransferCommand.Parse(["source.ff8", "--output", "out.json", "--mods-dir", "one", "--mods-dir", "two"], true));
    }

    [Fact]
    public void Import_resolves_explicit_paths_before_loading_installed_resources()
    {
        SaveTransferCommand.Options options = SaveTransferCommand.Parse(["source.ff8", "--output", "out.json", "--game-dir", "game", "--mods-dir", "mods"], true);
        Assert.Equal(Path.GetFullPath("source.ff8"), options.SourcePath);
        Assert.Equal(Path.GetFullPath("out.json"), options.OutputPath);
        Assert.Equal(Path.GetFullPath("game"), options.GameDirectory);
        Assert.Equal(Path.GetFullPath("mods"), options.ModsDirectory);
        SaveTransferCommand.Options export = SaveTransferCommand.Parse(["source.json", "--output", "out.ff8"], false);
        Assert.Null(export.GameDirectory);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Transfer_preserves_existing_output_and_source_before_decoding(bool import)
    {
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "source.bin");
        string output = Path.Combine(directory, "output.bin");
        File.WriteAllText(source, "source bytes");
        File.WriteAllText(output, "existing output");
        Assert.Throws<IOException>(() => SaveTransferCommand.Run([source, "--output", output], import));
        Assert.Equal("source bytes", File.ReadAllText(source));
        Assert.Equal("existing output", File.ReadAllText(output));
        Assert.Equal(2, Directory.GetFiles(directory).Length);
    }

    [Fact]
    public void Transfer_requires_an_existing_source_and_preserves_output_directory()
    {
        string source = Path.Combine(directory, "missing.ff8");
        string output = Path.Combine(directory, "output");
        Assert.Throws<FileNotFoundException>(() => SaveTransferCommand.Run([source, "--output", output], true));
        Directory.CreateDirectory(output);
        File.WriteAllText(source, "source bytes");
        Assert.Throws<IOException>(() => SaveTransferCommand.Run([source, "--output", output], false));
        Assert.True(Directory.Exists(output));
        Assert.Empty(Directory.GetFileSystemEntries(output));
    }

    [Fact]
    public void Original_pc_fixture_imports_and_exports_without_changing_source()
    {
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF8_PC_SAVE_FIXTURE");
        string gameDirectory = LaunchOptions.ConfiguredGameDirectory;
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(fixture) && Directory.Exists(gameDirectory), "Set MEMORIA_FF8_PC_SAVE_FIXTURE and MEMORIA_FF8_GAME_DIR to test original PC save transfer.");
        byte[] source = File.ReadAllBytes(fixture!);
        string snapshot = Path.Combine(directory, "imported.json");
        string exported = Path.Combine(directory, "exported.ff8");
        Assert.Equal(0, SaveTransferCommand.Run([fixture!, "--output", snapshot, "--game-dir", gameDirectory], true));
        GameState state = GameSession.Load(snapshot);
        Assert.NotNull(state.OriginalSave);
        Assert.True(state.Scene == SceneKind.WorldMap || state.EventState is not null);
        Assert.Equal(0, SaveTransferCommand.Run([snapshot, "--output", exported], false));
        Assert.NotEmpty(File.ReadAllBytes(exported));
        Assert.Equal(source, File.ReadAllBytes(fixture!));
        GameSession resumed = new(state);
        using GameWorld world = new(resumed, gameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1)).Accepted);
        Assert.Null(world.Events.LastFault);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
