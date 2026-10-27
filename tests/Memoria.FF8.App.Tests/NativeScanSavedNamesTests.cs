using System.Text.Json;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeScanSavedNamesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Encounter_entry_preserves_sparse_saved_names_and_resolves_default_or_custom_Scan_names(bool renamed)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original encounter and participant resources are required.");
        using GameAssets assets = new(directory);
        using WorldAssets worldAssets = new(directory);
        GameSession session = new(GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.Field,
            FieldId = 999,
            FieldName = "synthetic-scan-saved-names-component",
            CharacterVitals = assets.ReadNewGameCharacterVitals().Rest(),
            CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
            InputConfiguration = assets.ReadNewGameInputConfiguration(),
            Economy = assets.ReadNewGameEconomy(),
            BattleVariables = NativeBattleVariableState.CreateNewGame(),
            GlobalRandom = assets.ReadNewGameGlobalRandom(),
            NativeBattleMemory = assets.ReadColdProcessNativeBattleMemory()
        });
        using GameWorld world = new(session, directory);
        string squall = renamed ? "Leon" : "Squall";
        world.Events.Scene.Names[0] = squall;
        world.Events.Scene.Names[16] = "Quezacotl";
        if (renamed)
        {
            world.Events.Scene.Names[1] = "Rin";
            world.Events.Scene.Names[GameNames.GrieverSubject] = "Lionheart";
        }
        string names = JsonSerializer.Serialize(world.Events.Scene.Names);
        NativeWorldEncounterDefinition encounters = Assert.IsType<NativeWorldEncounterDefinition>(worldAssets.ReadRandomEncounterDefinition(24576, -29406, 6, 1));
        ushort formation = encounters.Formations[0];
        BattleDefinition definition = world.CreateEncounterBattle(formation);
        NativeBattleMusicEntryPreparation music = NativeBattleMusicEntry.Prepare52CE30(formation, definition.NativeFlags, new(1, null, null, null, 0, null), 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), new NativeMusicCatalog(directory).ReadBattleEntryResources());
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition with { NativeMusicProcess = music.MusicProcess, NativeGlobalSoundProcess = music.SoundProcess }, BattleAtbMode.Normal);
        AssertNames(battle, squall, renamed ? "Rin" : "Rinoa", renamed ? "Lionheart" : "Griever");
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        AssertNames(restored, squall, renamed ? "Rin" : "Rinoa", renamed ? "Lionheart" : "Griever");
        Assert.Equal(names, JsonSerializer.Serialize(world.Events.Scene.Names));
    }

    [Fact]
    public void Configured_standalone_enters_battle_and_cold_restores_its_first_callback_without_initializing_names()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_SCAN_NAME_ENTRY_SNAPSHOT");
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_SCAN_NAME_EVIDENCE");
        Assert.SkipUnless(source is not null && File.Exists(source) && !string.IsNullOrWhiteSpace(evidence), "Configure a standalone before battle entry and a diagnostic output directory.");
        byte[] sourceBytes = File.ReadAllBytes(source!);
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        string names = JsonSerializer.Serialize(world.Events.Scene.Names);
        Assert.False(world.Events.Scene.Names.ContainsKey(1));
        Assert.False(world.Events.Scene.Names.ContainsKey(GameNames.GrieverSubject));
        string squall = world.Events.Scene.Names.GetValueOrDefault(0) ?? "Squall";
        for (int tick = 0; tick < 1500 && !world.HasPendingBattle; tick++)
        {
            Assert.Null(world.Events.CurrentDialogue?.FirstChoice);
            world.Apply(new(world.Events.CurrentDialogue is { CanConfirm: true } ? PlayerActionKind.Confirm : PlayerActionKind.Wait));
        }
        Assert.True(world.HasPendingBattle, $"Field {world.Events.FieldId}, frame {world.Events.Frame}, trigger {world.NextTrigger}");
        string id = "scan-names-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        Save(world, session, evidence!, id + "-preentry");
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: session.State.GameplayPreferences.AtbMode, NativeSeed: 0));
        BattleSession active = Assert.IsType<BattleSession>(world.Battle);
        AssertNames(active, squall, "Rinoa", "Griever");
        string entry = Save(world, session, evidence!, id + "-entry");
        GameSession restoredSession = new(GameSession.Load(entry));
        using GameWorld restored = new(restoredSession, directory);
        restored.EnsureField();
        AssertNames(restored.Battle!, squall, "Rinoa", "Griever");
        Assert.Equal(JsonSerializer.Serialize(active.Capture()), JsonSerializer.Serialize(restored.Battle!.Capture()));
        long callbacks = active.NativeGraphics!.Callback;
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        active.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        restored.Battle.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        world.SynchronizeBattlePresentation();
        restored.SynchronizeBattlePresentation();
        Assert.Equal(callbacks + 1, active.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(active.Capture()), JsonSerializer.Serialize(restored.Battle.Capture()));
        Assert.Equal(names, JsonSerializer.Serialize(world.Events.Scene.Names));
        Assert.Equal(names, JsonSerializer.Serialize(restored.Events.Scene.Names));
        Save(world, session, evidence!, id + "-resumed");
        Assert.Equal(sourceBytes, File.ReadAllBytes(source!));
        Console.WriteLine($"Field {world.Events.FieldId}, field frame {world.Events.Frame}, battle frame {active.Frame}, callback {active.NativeGraphics.Callback}; entry {entry}");
    }

    private static void AssertNames(BattleSession battle, string squall, string rinoa, string griever)
    {
        NativeBattleScanTextExpansionServices services = battle.CreateNativeScanTextExpansionServices();
        foreach ((byte operand, string text) in new (byte, string)[] { (0x30, squall), (0x34, rinoa), (0x50, griever) })
        {
            Assert.Equal(FF8Encoding.CreateEuropean(FF8TextLanguage.EN).GetBytes(text).Append((byte)0).ToArray(), services.ReadNameBytes!(operand).ToArray());
        }
    }

    private static string Save(GameWorld world, GameSession session, string directory, string name)
    {
        world.CaptureBattleState();
        session.SetEventState(world.Events.Capture());
        NativeBattleProductionEffectAdmissionTests.Publish(directory, name, session.State);
        return Path.Combine(directory, name + ".json");
    }
}
