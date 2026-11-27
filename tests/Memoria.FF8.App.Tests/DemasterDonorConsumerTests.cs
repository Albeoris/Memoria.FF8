using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Events;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DemasterDonorConsumerTests(ITestOutputHelper output)
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
        NativeShopHistory history = assets.ReadNewGameShopHistory();
        int shopArgument = Enumerable.Range(0, shop.ArgumentMap.Length).First(argument =>
        {
            int catalog = shop.ArgumentMap[argument];
            return catalog < shop.Catalogs.Length && shop.Catalogs[catalog].Select((entry, index) => (entry, index))
                .Any(row => row.entry.ItemId == 17 && history.Bytes[20 * catalog + row.index] != 0);
        });
        NativeEconomy funded = donorEconomy with { Gil = 70000 };
        NativeShopTransaction transaction = NativeShopTransaction.Begin(shopArgument, shop, funded, vitals, history);
        NativeEconomy purchased = transaction.Buy(17, 1, out bool accepted).Commit(funded);
        Assert.True(accepted);
        Assert.Equal(60000u, purchased.Gil);
        Assert.Equal(funded.Items.Where(item => item.ItemId == 17).Sum(item => item.Count) + 1, purchased.Items.Where(item => item.ItemId == 17).Sum(item => item.Count));
        foreach (string member in new[] { "cyocobo.bin", "magsort.bin", "mitem.bin", "mngrp.bin", "mwepon.bin", "pet_exp.msg", "price.bin", "shop.bin" })
        {
            ResourceKey key = new("menu/" + member);
            Assert.False(mounted.Read(key).AsSpan().SequenceEqual(original.Read(key)), member);
        }

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

        int changedFormations = 0;
        for (int index = 0; index < battle.FormationCount; index++)
        {
            if (battle.FormationSourceHash(index) != originalBattle.FormationSourceHash(index))
            {
                _ = battle.LoadEncounter(index);
                changedFormations++;
            }
        }
        int changedEnemies = 0;
        foreach (int enemyModel in battle.EnemyModelIds)
        {
            ResourceKey enemyKey = new($"battle/c0m{enemyModel:000}.dat");
            if (stock.Find(enemyKey) is null || mounted.Read(enemyKey).AsSpan().SequenceEqual(original.Read(enemyKey)))
            {
                continue;
            }
            _ = battle.ReadEnemyTemplate(enemyModel, 10);
            _ = battle.ReadEnemyAi(enemyModel, 10);
            changedEnemies++;
        }
        Assert.Equal(143, changedEnemies);
        Assert.True(changedFormations > 0);
        output.WriteLine($"Decoded {changedEnemies} changed enemy models and {changedFormations} changed formations.");

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
