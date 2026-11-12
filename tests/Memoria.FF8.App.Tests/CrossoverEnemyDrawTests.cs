using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverEnemyDrawTests
{
    [Fact]
    public void Original_Scan_spends_one_stock_and_keeps_literature_separate_from_statistics_without_rng()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7) && !string.IsNullOrWhiteSpace(ff8), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using GameAssets game = new(ff8!);
        using BattleAssets assets = new(ff8!);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(scene => scene.FormationId == 300);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals().DeepCopy();
        int row = origin.FindPhysicalRow(0);
        int magicCommand = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2) + 20;
        origin.Records[row] = origin.Records[row] with { Junction = origin.Records[row].Junction with { Commands = (uint)magicCommand } };
        origin = origin.ChangeMagicStock(row, 50, 2, MagicOperationReason.ScriptReward, out _);
        origin = origin.ChangeMagicStock(row, 21, 13, MagicOperationReason.ScriptReward, out _);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(origin, assets, 0);
        short enemyId = formation.EnemyRewards.First(enemy => enemy.EnemyId >= 0).EnemyId;
        const string description = "A sentinel left behind by a city that no longer sleeps.";
        Ff7EnemyExtensionCatalog catalog = new([new(1, "test.scan", [new() { EnemyId = enemyId, ScanDescription = description }])]);
        Ff7BattleSession host = new(formation, original.Progress, kernel, Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers, 1, animations: animations, enemyExtensions: catalog);
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, 0)], []);
        Ff7BattleGuestProjection projection = CrossoverGuestHostActions.Projection(kit);
        host.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, 0, row, kit.HitPoints, projection.MaximumHp, projection.Level, projection.Luck, projection.Defense, projection.MagicDefense));
        CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128);
        CrossoverGuestHostActions actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets);
        string randomBefore = JsonSerializer.Serialize(host.Capture().Random);
        NativeCrtRandomState crtBefore = actions.Capture().CrtRandom;
        Assert.True(actions.HandlesOperation(50));
        string result = actions.ExecuteOperation(50, 4, () => throw new InvalidOperationException("Scan must not draw a random byte."));
        Assert.Equal("Scan complete.", result);
        Assert.Equal(randomBefore, JsonSerializer.Serialize(host.Capture().Random));
        Assert.Equal(crtBefore, actions.Capture().CrtRandom);
        CrossoverEnemyScan scan = Assert.IsType<CrossoverEnemyScan>(actions.LastScan);
        Assert.Equal(host.Name(4), scan.Name);
        Assert.Equal(description, scan.Description);
        Assert.DoesNotContain(scan.Name, scan.Description);
        Assert.DoesNotContain("ATK", scan.Description);
        Assert.Contains("ATK", scan.CombatStatistics);
        Assert.Contains("MDEF", scan.CombatStatistics);
        Assert.Equal(host.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints, scan.HitPoints);
        Assert.Equal(host.EnemyExtension(4)!.Level, scan.Level);
        Assert.Equal(1, actions.WorkingStock.GetMagicCount(row, 50));
        Assert.Equal(13, actions.WorkingStock.GetMagicCount(row, 21));
        Assert.Equal(2, origin.GetMagicCount(row, 50));
        CrossoverGuestHostActionsSnapshot snapshot = JsonSerializer.Deserialize<CrossoverGuestHostActionsSnapshot>(JsonSerializer.Serialize(actions.Capture()))!;
        CrossoverGuestCommittedSpell cost = Assert.Single(snapshot.CommittedSpells!.Value);
        Assert.Equal((ushort)50, cost.Spell);
        Assert.Equal(0, cost.DrawQuantity);
        CrossoverGuestMagicSettlement settlement = new(roster, snapshot.Stock, snapshot.OriginCrtRandom, snapshot.CrtRandom, snapshot.CommittedSpells.Value);
        NativeCharacterVitals settled = CrossoverBattleVitalsSettlement.RestoreCommittedStock(settlement, origin, [row]);
        Assert.Equal(1, settled.GetMagicCount(row, 50));
        Assert.Equal(13, settled.GetMagicCount(row, 21));
        Assert.Equal(JsonSerializer.Serialize(settled), JsonSerializer.Serialize(CrossoverBattleVitalsSettlement.RestoreCommittedStock(settlement, origin, [row])));
    }

    [Theory]
    [InlineData("Cure", false)]
    [InlineData("Full-cure", true)]
    public void Original_resources_support_separate_Draw_source_and_healing_recipient_without_stock_cost(string spellName, bool grouped)
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7) && !string.IsNullOrWhiteSpace(ff8), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using GameAssets game = new(ff8!);
        using BattleAssets assets = new(ff8!);
        BattleNativeAction originalSpell = Assert.Single(assets.NativeActions, action => action.Command == 2 && action.Name == spellName);
        byte spellId = checked((byte)originalSpell.ActionId);
        Assert.Equal(grouped, (originalSpell.TargetFlags!.Value & 0x30) != 0x10);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(scene => scene.FormationId == 300);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals().DeepCopy();
        int row = origin.FindPhysicalRow(0);
        int drawCommand = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)6) + 20;
        origin.Records[row] = origin.Records[row] with { Junction = origin.Records[row].Junction with { Commands = (uint)drawCommand } };
        origin = origin.ChangeMagicStock(row, spellId, 100, MagicOperationReason.ScriptReward, out _);
        origin = origin.WithSavedHitPoints(row, 100);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(origin, assets, 0);
        short enemyId = formation.EnemyRewards.First(enemy => enemy.EnemyId >= 0).EnemyId;
        Ff7EnemyExtensionCatalog catalog = new([new(1, "test.draw", [new() { EnemyId = enemyId, ScanDescription = "A sentinel left behind by a city that no longer sleeps.", DrawTiers = [new(0, [new(spellId, 0), new(1, 0)])] }])]);
        Ff7Progress progress = original.Progress with { Party = [0, 1, 255] };
        Ff7BattleSession host = new(formation, progress, kernel, Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers, 1, animations: animations, enemyExtensions: catalog);
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, 0)], []);
        Ff7BattleGuestProjection projection = CrossoverGuestHostActions.Projection(kit);
        host.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, 0, row, kit.HitPoints, projection.MaximumHp, projection.Level, projection.Luck, projection.Defense, projection.MagicDefense));
        Ff7BattleSnapshot woundedParty = host.Capture() with { Actors = host.Actors.Select(actor => actor.Actor.Slot == 1 ? actor with { Actor = actor.Actor with { HitPoints = 100 } } : actor).ToArray() };
        host = new(formation, progress, kernel, Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers, 1, snapshot: woundedParty, animations: animations, enemyExtensions: catalog);
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        MethodInfo readVisual = typeof(Ff7Scene).GetMethod("BattleActorVisual", BindingFlags.Static | BindingFlags.NonPublic)!;
        CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128);
        CrossoverGuestHostActions actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets);
        ushort ability = (ushort)(0xD000 | spellId);
        string before = JsonSerializer.Serialize(actions.WorkingStock);
        string randomBefore = JsonSerializer.Serialize(host.Capture().Random);
        _ = actions.Abilities;
        _ = actions.Abilities;
        Assert.Equal(randomBefore, JsonSerializer.Serialize(host.Capture().Random));
        int rolls = 0;
        Assert.True(actions.PrepareSource(ability, () =>
        {
            rolls++;
            return 31;
        }, out string? failure), failure);
        Assert.Equal(1, rolls);
        Ff7BattleGuestPhysicalHit spell = actions.DescribeMagic(ability);
        Assert.True(spell.Attack.Healing);
        Ff7BattleActionVitals wounded = new(10, 100, 0, 0, 0);
        Ff7BattleDamageActor recipient = new(10, 10, 10, 10, 0, 0, 0, 20, 20);
        Ff7BattleActionReceipt healed = Ff7NativeBattleActionResolver.ResolveGuestContact(spell.Source, new(100, 100, 0, 0, 0), recipient, wounded, spell.Attack, spell.StatusRecord, new(kernel[2].Data.Span, Ff7BattleRng.BeginBattle()));
        Assert.True(healed.Target.Hp > wounded.Hp);
        Assert.Equal(before, JsonSerializer.Serialize(actions.WorkingStock));
        Assert.Equal(100, actions.WorkingStock.GetMagicCount(row, spellId));
        Assert.Null(actions.DescribeScriptInteraction(2, 0xE001, 4).DrawSourceSlot);
        string result = actions.ExecuteOperation(0xE001, 4, () => 31);
        Assert.Contains("Drew", result);
        Assert.Equal(4, actions.DescribeScriptInteraction(2, 0xE001, 4).DrawSourceSlot);
        CrossoverGuestHostActionsSnapshot snapshot = JsonSerializer.Deserialize<CrossoverGuestHostActionsSnapshot>(JsonSerializer.Serialize(actions.Capture()))!;
        Assert.Equal(2, snapshot.NextOrdinal);
        Assert.True(Assert.Single(snapshot.CommittedSpells!.Value).DrawQuantity > 0);
        CrossoverGuestMagicSettlement settlement = new(roster, snapshot.Stock, snapshot.OriginCrtRandom, snapshot.CrtRandom, snapshot.CommittedSpells.Value);
        NativeCharacterVitals settled = CrossoverBattleVitalsSettlement.RestoreCommittedStock(settlement, origin, [row]);
        Assert.True(settled.GetMagicCount(row, 1) > 0);
        Assert.Equal(actions.WorkingStock.GetMagicCount(row, 1), settled.GetMagicCount(row, 1));
        Assert.Equal(100, settled.GetMagicCount(row, spellId));
        Assert.Equal(JsonSerializer.Serialize(settled), JsonSerializer.Serialize(CrossoverBattleVitalsSettlement.RestoreCommittedStock(settlement, origin, [row])));
        Assert.True(actions.PrepareSource(0, () => throw new InvalidOperationException("Clearing a prepared source must not draw a random byte."), out _));
        attack.PrepareStanding(host.VisualPlacement(0));
        BindForeignGeometry();
        host.BindGuestAttack(actions);
        for (int tick = 0; tick < 1000 && host.ReadyPlayer != 0; tick++)
        {
            host.Advance();
        }
        Assert.Equal(0, host.ReadyPlayer);
        Ff7BattleCommandOption option = Assert.Single(host.Commands(0), option => option.Argument == ability && option.Command == Ff7BattleCommand.Magic);
        Assert.Null(option.UnsupportedReason);
        Assert.True((option.ValidTargets & 1) != 0);
        ushort targetMask = grouped ? (ushort)3 : (ushort)1;
        Assert.Equal(targetMask, (ushort)(option.ValidTargets & targetMask));
        string stockBeforeCast = JsonSerializer.Serialize(actions.WorkingStock);
        Assert.True(grouped ? host.Submit(0, Ff7BattleCommand.Magic, ability, targetMask) : host.SubmitGuestMagic(0, 0, ability));
        for (int tick = 0; tick < 1000 && actions.ActiveAbility is null && host.GuestAbilityFailure is null; tick++)
        {
            host.Advance();
        }
        Assert.Null(host.GuestAbilityFailure);
        CrossoverGuestFireHostState active = Assert.IsType<CrossoverGuestFireHostState>(actions.ActiveAbility);
        Assert.Equal(4, active.DrawCast!.SourceSlot);
        Assert.Equal(spellId, active.DrawCast.Magic);
        Assert.InRange(active.DrawCast.Quantity, 1, 9);
        Assert.Equal(0, active.Invocation.TargetSlot);
        Assert.Equal(grouped ? 2 : 0, active.GroupPrepared.Length);
        Assert.Null(active.StockAction);
        Ff7BattleSnapshot continuation = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(host.Capture()))!;
        CrossoverGunbladeHostSnapshot caster = JsonSerializer.Deserialize<CrossoverGunbladeHostSnapshot>(JsonSerializer.Serialize(attack.Capture()))!;
        CrossoverGuestHostActionsSnapshot actionState = JsonSerializer.Deserialize<CrossoverGuestHostActionsSnapshot>(JsonSerializer.Serialize(actions.Capture()))!;
        host = new(formation, progress, kernel, Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers, 1, snapshot: continuation, animations: animations, enemyExtensions: catalog);
        attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128, caster);
        actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets, actionState);
        BindForeignGeometry();
        Assert.Null(actions.DescribeScriptInteraction(2, 0xE001, 4).DrawSourceSlot);
        host.BindGuestAttack(actions);
        IReadOnlyList<Ff7BattleGuestPhysicalHit> preparedHits = grouped ? active.GroupPrepared : [active.Prepared];
        HashSet<int> healedTargets = [];
        bool effect = false;
        for (int tick = 0; tick < 1000 && actions.Invocation is not null; tick++)
        {
            host.Advance();
            effect |= actions.ActiveAbility?.Presentation.Effect is not null;
            foreach (Ff7BattleGuestPhysicalHit hit in preparedHits)
            {
                Ff7BattleGuestPreparedContact receipt = hit.PreparedContact!;
                if (host.Actors.Single(actor => actor.Actor.Slot == receipt.Invocation.TargetSlot).Actor.HitPoints > receipt.TargetBefore.Hp)
                {
                    healedTargets.Add(receipt.Invocation.TargetSlot);
                }
            }
        }
        Assert.True(effect);
        Assert.Null(actions.Invocation);
        Assert.Equal(grouped ? new[] { 0, 1 } : new[] { 0 }, healedTargets.Order());
        foreach (Ff7BattleGuestPhysicalHit hit in preparedHits)
        {
            Ff7BattleGuestPreparedContact receipt = hit.PreparedContact!;
            Assert.True(receipt.Receipt.Target.Hp > receipt.TargetBefore.Hp);
            Assert.Equal(receipt.Receipt.Target.Hp, host.Actors.Single(actor => actor.Actor.Slot == receipt.Invocation.TargetSlot).Actor.HitPoints);
        }
        Assert.Equal(stockBeforeCast, JsonSerializer.Serialize(actions.WorkingStock));
        Assert.Equal(100, actions.WorkingStock.GetMagicCount(row, spellId));
        Assert.Single(actions.Capture().CommittedSpells!.Value);
        Assert.True(actions.Capture().CommittedSpells!.Value[0].DrawQuantity > 0);
        host.Advance();
        Assert.Equal(100, actions.WorkingStock.GetMagicCount(row, spellId));

        void BindForeignGeometry()
        {
            if (grouped)
            {
                actions.BindForeignGeometry(slot =>
                {
                    Ff7BattleActorState actor = host.Actors.Single(actor => actor.Actor.Slot == slot);
                    Ff7BattleActorVisual visual = (Ff7BattleActorVisual)readVisual.Invoke(null, [host, actor])!;
                    return renderer.CaptureActorGeometry(visual);
                });
            }
        }
    }
}
