using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Events;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DemasterDonorConsumerTests
{
    [Fact]
    public void SelectedExportReachesFieldShopNewGameAndEnemyReaders()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        string? export = Environment.GetEnvironmentVariable("MEMORIA_FF8_DEMASTER_DATA_DIR");
        Assert.SkipUnless(Directory.Exists(game) && Directory.Exists(export), "Set the installed Remastered and optional Demaster fixture directories.");
        using RemasteredGameDataSource stock = new(game!);
        using RemasteredGameDataSource selected = new(game!);
        string[] stems = ["data/field", "data/lang-en/main", "data/lang-en/menu", "data/lang-en/battle"];
        selected.PrepareLooseData(stems.SelectMany(stem => new[] { ".fl", ".fi", ".fs" }.Select(ext => stem + ext)).Select(path =>
            new RemasteredLooseDataFile(path, path, 2, () => File.OpenRead(Path.Combine(export!, path)))));
        selected.SealLooseData();
        ResourceService original = new(stock);
        ResourceService mounted = new(selected);
        using GameAssets originalAssets = new(game!, resources: original);
        using GameAssets assets = new(game!, resources: mounted);
        using BattleAssets originalBattle = new(game!, original);
        using BattleAssets battle = new(game!, mounted);

        FieldAssets changedField = assets.LoadField(95);
        ResourceKey script = new("field/testbl6/testbl6.jsm");
        Assert.Equal("testbl6", changedField.Name);
        Assert.Equal(mounted.Read(script), changedField.Jsm);
        Assert.False(changedField.Jsm.AsSpan().SequenceEqual(original.Read(script)));
        JsmProgram program = JsmProgram.Read(changedField.Jsm);
        JsmProgram stockProgram = JsmProgram.Read(original.Read(script));
        Assert.False(program.Operations.Select(operation => (operation.Opcode, operation.Parameter)).SequenceEqual(stockProgram.Operations.Select(operation => (operation.Opcode, operation.Parameter))));

        NativeEconomy stockEconomy = originalAssets.ReadNewGameEconomy();
        NativeEconomy donorEconomy = assets.ReadNewGameEconomy();
        Assert.NotEqual(stockEconomy.InitOutSha256, donorEconomy.InitOutSha256);
        Assert.Equal(new NativeItemStock(1, 6), stockEconomy.Items[0]);
        Assert.Equal(new NativeItemStock(1, 12), donorEconomy.Items[0]);
        Assert.Equal(new NativeItemStock(7, 4), stockEconomy.Items[1]);
        Assert.Equal(new NativeItemStock(7, 8), donorEconomy.Items[1]);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        Assert.NotEqual(originalAssets.ReadNewGameCharacterVitals().KernelSha256, vitals.KernelSha256);
        NativeShopDefinitions originalShop = originalAssets.ReadNativeShopDefinitions();
        NativeShopDefinitions shop = assets.ReadNativeShopDefinitions();
        Assert.Equal(2000u, originalShop.BuyPrice(17, 0));
        Assert.Equal(10000u, shop.BuyPrice(17, 0));
        Assert.Equal("Remedy+", shop.ResolvePrice(17).Name);

        const int model = 1;
        Assert.False(mounted.Read(new("battle/c0m001.dat")).AsSpan().SequenceEqual(original.Read(new("battle/c0m001.dat"))));
        NativeBattleEnemyTemplate template = battle.ReadEnemyTemplate(model, 10);
        NativeBattleAiDefinition ai = battle.ReadEnemyAi(model, 10);
        Assert.NotEmpty(ai.Scripts);
        Assert.NotEmpty(template.Levels);
        Assert.NotEqual(originalBattle.EncounterSourceHash, battle.EncounterSourceHash);
        const ushort formationId = 111;
        BattleEncounter encounter = battle.LoadEncounter(formationId);
        Assert.Contains(encounter.Enemies, enemy => enemy.ModelId == model);

        GameSession session = GameSession.CreateColdProcess(assets.ReadColdProcessNativeBattleMemory());
        session.StartNewGame();
        using GameWorld world = new(session, game!, resources: mounted, audioEnabled: false);
        Assert.Equal(new NativeItemStock(1, 12), session.State.Economy!.Items[0]);
        Assert.Equal(new NativeItemStock(7, 8), session.State.Economy.Items[1]);
        BattleDefinition definition = world.CreateEncounterBattle(formationId);
        BattleCombatantDefinition enemy = definition.Combatants.First(actor => !actor.IsPlayer && actor.ModelId == model);
        Assert.NotNull(enemy.NativeAi);
        Assert.Equal(template.Levels[enemy.Level].MaximumHp, enemy.MaximumHp);
        NativeMusicProcessState buffers = new(1, null, null, null, 0, null);
        NativeBattleMusicEntryPreparation music = NativeBattleMusicEntry.Prepare52CE30(formationId, definition.NativeFlags, buffers, 5, -1, NativeGlobalSoundProcess.FromBufferlessColdProcess(), new NativeMusicCatalog(mounted).ReadBattleEntryResources());
        definition = definition with { NativeMusicProcess = music.MusicProcess, NativeGlobalSoundProcess = music.SoundProcess };
        BattleSession live = GameWorld.CreateLiveBattleSession(definition, session.State.GameplayPreferences, assets, world.BattleResources);
        Assert.Contains(live.Combatants, actor => actor.Id == enemy.Id && actor.IsActive);
        for (int callback = 0; callback < 1200 && !live.Events.Any(item => item.ActorId == enemy.Id && item.Kind == "ActionComplete"); callback++)
        {
            live.Tick();
        }
        Assert.Contains(live.Events, item => item.ActorId == enemy.Id && item.Kind == "ActionComplete");
    }
}
