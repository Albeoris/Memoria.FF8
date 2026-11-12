using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverEnemyDrawTests
{
    [Fact]
    public void Original_resources_support_separate_Draw_source_and_healing_recipient_without_stock_cost()
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
        int drawCommand = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)6) + 20;
        origin.Records[row] = origin.Records[row] with { Junction = origin.Records[row].Junction with { Commands = (uint)drawCommand } };
        origin = origin.ChangeMagicStock(row, 21, 100, MagicOperationReason.ScriptReward, out _);
        origin = origin.WithSavedHitPoints(row, 100);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(origin, assets, 0);
        short enemyId = formation.EnemyRewards.First(enemy => enemy.EnemyId >= 0).EnemyId;
        Ff7EnemyExtensionCatalog catalog = new([new(1, "test.draw", [new() { EnemyId = enemyId, ScanDescription = "A sentinel left behind by a city that no longer sleeps.", DrawTiers = [new(0, [new(21, 0), new(1, 0)])] }])]);
        Ff7BattleSession host = new(formation, original.Progress, kernel, Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers, 1, animations: animations, enemyExtensions: catalog);
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, 0)], []);
        Ff7BattleGuestProjection projection = CrossoverGuestHostActions.Projection(kit);
        host.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, 0, row, kit.HitPoints, projection.MaximumHp, projection.Level, projection.Luck, projection.Defense, projection.MagicDefense));
        CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128);
        CrossoverGuestHostActions actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets);
        ushort ability = (ushort)(0xD000 | 21);
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
        Ff7BattleActionReceipt healed = Ff7NativeBattleActionResolver.Resolve(spell.Source, new(100, 100, 0, 0, 0), recipient, wounded, spell.Attack, spell.StatusRecord, new(kernel[2].Data.Span, Ff7BattleRng.BeginBattle()));
        Assert.True(healed.Target.Hp > wounded.Hp);
        Assert.Equal(before, JsonSerializer.Serialize(actions.WorkingStock));
        Assert.Equal(100, actions.WorkingStock.GetMagicCount(row, 21));
        string result = actions.ExecuteOperation(0xE001, 4, () => 31);
        Assert.Contains("Drew", result);
        CrossoverGuestHostActionsSnapshot snapshot = JsonSerializer.Deserialize<CrossoverGuestHostActionsSnapshot>(JsonSerializer.Serialize(actions.Capture()))!;
        Assert.Equal(2, snapshot.NextOrdinal);
        Assert.True(Assert.Single(snapshot.CommittedSpells!.Value).DrawQuantity > 0);
        CrossoverGuestMagicSettlement settlement = new(roster, snapshot.Stock, snapshot.OriginCrtRandom, snapshot.CrtRandom, snapshot.CommittedSpells.Value);
        NativeCharacterVitals settled = CrossoverBattleVitalsSettlement.RestoreCommittedStock(settlement, origin, [row]);
        Assert.True(settled.GetMagicCount(row, 1) > 0);
        Assert.Equal(actions.WorkingStock.GetMagicCount(row, 1), settled.GetMagicCount(row, 1));
        Assert.Equal(100, settled.GetMagicCount(row, 21));
        Assert.Equal(JsonSerializer.Serialize(settled), JsonSerializer.Serialize(CrossoverBattleVitalsSettlement.RestoreCommittedStock(settlement, origin, [row])));
        attack.PrepareStanding(host.VisualPlacement(0));
        host.BindGuestAttack(actions);
        for (int tick = 0; tick < 1000 && host.ReadyPlayer != 0; tick++)
        {
            host.Advance();
        }
        Assert.Equal(0, host.ReadyPlayer);
        Ff7BattleCommandOption option = Assert.Single(host.Commands(0), option => option.Argument == ability && option.Command == Ff7BattleCommand.Magic);
        Assert.Null(option.UnsupportedReason);
        Assert.True((option.ValidTargets & 1) != 0);
        Assert.True(host.SubmitGuestMagic(0, 0, ability));
        for (int tick = 0; tick < 1000 && actions.ActiveAbility is null && host.GuestAbilityFailure is null; tick++)
        {
            host.Advance();
        }
        Assert.Null(host.GuestAbilityFailure);
        CrossoverGuestFireHostState active = Assert.IsType<CrossoverGuestFireHostState>(actions.ActiveAbility);
        Assert.Equal(4, active.DrawCast!.SourceSlot);
        Assert.Equal(0, active.Invocation.TargetSlot);
        Assert.Null(active.StockAction);
        Ff7BattleSnapshot continuation = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(host.Capture()))!;
        CrossoverGunbladeHostSnapshot caster = JsonSerializer.Deserialize<CrossoverGunbladeHostSnapshot>(JsonSerializer.Serialize(attack.Capture()))!;
        CrossoverGuestHostActionsSnapshot actionState = JsonSerializer.Deserialize<CrossoverGuestHostActionsSnapshot>(JsonSerializer.Serialize(actions.Capture()))!;
        host = new(formation, original.Progress, kernel, Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers, 1, snapshot: continuation, animations: animations, enemyExtensions: catalog);
        attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128, caster);
        actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets, actionState);
        host.BindGuestAttack(actions);
        int publications = 0;
        bool effect = false;
        for (int tick = 0; tick < 1000 && actions.Invocation is not null; tick++)
        {
            host.Advance();
            effect |= actions.ActiveAbility?.Presentation.Effect is not null;
            if (host.LastHit is { Source: 0, Target: 0 } contact && contact.AttackId == ability && contact.Tick == host.Tick)
            {
                publications++;
            }
        }
        Assert.True(effect);
        Assert.Null(actions.Invocation);
        Assert.Equal(1, publications);
        Assert.Equal(active.Prepared.PreparedContact!.Receipt.Target.Hp, host.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints);
        Assert.Equal(100, actions.WorkingStock.GetMagicCount(row, 21));
        Assert.Single(actions.Capture().CommittedSpells!.Value);
        Assert.True(actions.Capture().CommittedSpells!.Value[0].DrawQuantity > 0);
        host.Advance();
        Assert.Equal(100, actions.WorkingStock.GetMagicCount(row, 21));
    }
}
