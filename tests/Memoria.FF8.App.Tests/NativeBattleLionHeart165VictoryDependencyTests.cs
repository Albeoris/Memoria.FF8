using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleLionHeart165VictoryDependencyTests
{
    [Fact]
    public void Completed_Lion_Heart_snapshot_binds_its_original_victory_file_and_continues_without_replaying_the_effect()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_LION_HEART_VICTORY_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LION_HEART_VICTORY_OUTPUT");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(source is not null && File.Exists(source) && output is not null && directory is not null && Directory.Exists(directory), "Select a completed standalone, a distinct migration output directory and the installed original resources.");
        Directory.CreateDirectory(output!);
        byte[] original = File.ReadAllBytes(source!);
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(original)!;
        Assert.Equal((ushort)165, snapshot.NativeGraphics!.OriginalEffect!.Binding.Effect);
        Assert.Equal(0u, snapshot.NativeGraphics.OriginalEffect.ReturnedDescriptor);
        Assert.Equal((byte)4, snapshot.NativeGraphics.NativeOutcome!.ResultByte6E7);
        Assert.Null(snapshot.NativeTimedAction);
        Assert.Empty(snapshot.NativeLimits);
        Assert.Null(snapshot.Definition.NativeVictoryFile);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        NativeBattleArchiveFile victory = assets.ReadNativeBattleFile(766);
        BattleSnapshot migrated = snapshot with { Definition = snapshot.Definition with { NativeVictoryFile = victory } };
        string path = Path.Combine(output!, "victory-file-bound.battle.json");
        Assert.False(string.Equals(Path.GetFullPath(source!), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        byte[] migratedBytes = JsonSerializer.SerializeToUtf8Bytes(migrated);
        using (FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write))
        {
            stream.Write(migratedBytes);
        }
        Assert.Equal(original, File.ReadAllBytes(source!));
        File.WriteAllText(Path.Combine(output!, "migration.json"), JsonSerializer.Serialize(new { Status = "pending-validation", Source = source, SourceSha256 = Convert.ToHexString(SHA256.HashData(original)), Migrated = path, MigratedSha256 = Convert.ToHexString(SHA256.HashData(migratedBytes)), ChangedPaths = new[] { "Definition.NativeVictoryFile" }, VictoryFileId = victory.FileId, snapshot.Frame, NativeCallback = snapshot.NativeGraphics!.Callback }));
        Console.WriteLine("Lion Heart victory dependency migration: " + path);
        BattleSession battle = Restore(migrated);
        BattleSession independent = Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(path))!);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
        BattleScenePresentation scene = new(assets, migrated.Definition, true);
        Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, migrated.Definition, true).Render(independent, 320, 224).ToArray());
        for (int callback = 0; callback < 2; callback++)
        {
            battle.Tick();
            independent.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(independent.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, migrated.Definition, true).Render(independent, 320, 224).ToArray());
        }
        BattleSnapshot after = battle.Capture();
        Assert.Equal(0u, after.NativeGraphics!.OriginalEffect!.ReturnedDescriptor);
        Assert.Null(after.NativeTimedAction);
        Assert.Empty(after.NativeLimits);
        Assert.Equal(snapshot.Events.Count(item => item.Kind == "Damage"), after.Events.Count(item => item.Kind == "Damage"));
        NativeBattleRestorationPresentationTests.Publish(output!, "victory-file-bound-next", after, scene.Render(battle, 320, 224).ToArray());
        File.WriteAllText(Path.Combine(output!, "migration.json"), JsonSerializer.Serialize(new { Status = "passed", Source = source, SourceSha256 = Convert.ToHexString(SHA256.HashData(original)), Migrated = path, MigratedSha256 = Convert.ToHexString(SHA256.HashData(migratedBytes)), ChangedPaths = new[] { "Definition.NativeVictoryFile" }, VictoryFileId = victory.FileId, snapshot.Frame, NativeCallback = snapshot.NativeGraphics!.Callback, AfterFrame = after.Frame, AfterCallback = after.NativeGraphics!.Callback, Validation = "Two actual host polls; independent JSON and RGBA agree; no effect entry or damage replay." }));

        BattleSession Restore(BattleSnapshot state)
        {
            BattleSession restored = BattleSession.Restore(state);
            restored.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return restored;
        }
    }
}
