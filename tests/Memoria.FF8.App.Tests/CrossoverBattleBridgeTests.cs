using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverBattleBridgeTests
{
    [Fact]
    public void Guest_combat_binding_is_separate_from_story_and_appearance_and_survives_JSON()
    {
        Ff7BattleSnapshot source = Opening();
        CharacterAppearanceMapping[] appearances = [new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, "explicit"), new(SessionGame.Ff7, 1, SessionGame.Ff8, 0, "explicit")];
        Guid occurrence = Guid.NewGuid();
        Assert.Throws<NotSupportedException>(() => CrossoverBattleRoster.Create(occurrence, source, [], appearances));
        Assert.Throws<NotSupportedException>(() => CrossoverBattleRoster.Create(occurrence, source, [new(0, 0, 0), new(1, 1, 0)], appearances));
        Assert.Throws<InvalidDataException>(() => CrossoverBattleRoster.Create(occurrence, source, [new(0, 1, 0)], appearances));
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(occurrence, source, [new(0, 0, 0)], appearances);
        Assert.Equal(new(SessionGame.Ff7, CrossoverBattleActorKind.Character, 0, 0), roster.Guest.Story);
        Assert.Equal(new(SessionGame.Ff8, CrossoverBattleActorKind.Character, 0, 0), roster.Guest.Combat);
        CrossoverBattleParticipant ally = roster.Participants.Single(actor => actor.HostSlot == 1);
        Assert.Equal(new(SessionGame.Ff8, CrossoverBattleActorKind.Character, 0, 1), ally.Presentation);
        Assert.Equal(ally.Story, ally.Combat);
        Assert.Equal(2, roster.Participants.Count(actor => actor.Combat.Kind == CrossoverBattleActorKind.Enemy && actor.Combat.NativeId == 16));
        Assert.NotEqual(roster.Participants[2].Combat, roster.Participants[3].Combat);

        CrossoverBattleRoster restored = JsonSerializer.Deserialize<CrossoverBattleRoster>(JsonSerializer.Serialize(roster))!;
        restored.Validate();
        Assert.Equal(roster.Guest, restored.Guest);
        Assert.Equal(roster.Participants.ToArray(), restored.Participants.ToArray());
        Assert.Throws<NotSupportedException>(() => (roster with { Version = 2 }).Validate());
    }

    [Fact]
    public void Original_Squall_kit_quotes_its_own_stock_and_rejects_unsupported_target_capabilities()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(ff7Root), "Select the installed original FFVII and FFVIII resources.");
        using GameAssets game = new(root!);
        using BattleAssets assets = new(root!);
        NativeCharacterVitals initial = game.ReadNewGameCharacterVitals().DeepCopy();
        int row = initial.FindPhysicalRow(0);
        int magicAbility = initial.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2) + 20;
        NativeCharacterVital original = initial.Records[row];
        initial.Records[row] = original with { Junction = original.Junction with { Commands = (uint)magicAbility } };
        const ushort fire = 1;
        initial = initial.ChangeMagicStock(row, fire, 4, MagicOperationReason.ScriptReward, out MagicStockMutation granted);
        Assert.Equal(4, granted.Accepted);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(initial, assets, 0);
        Assert.Equal(row, kit.RecordRow);
        Assert.Equal(original.Weapon, kit.Weapon);
        Assert.Equal(original.HitPoints.Minimum, kit.HitPoints);
        Assert.Equal((byte)5, kit.Limit.Command);
        Assert.Contains((byte)2, kit.Commands);

        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using Ff7Session originalFf7 = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation scene = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7BattleSession host = new(scene, originalFf7.Progress, kernel, Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers, 1);
        string hostBefore = JsonSerializer.Serialize(host.Capture());
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, 0)], []);
        CrossoverBattleActorId enemy = roster.Participants.Single(actor => actor.HostSlot == 4).Combat;
        CrossoverBattleAbilityId spell = new(SessionGame.Ff8, CrossoverBattleAbilityKind.Spell, fire);
        string before = JsonSerializer.Serialize(initial);
        Assert.Contains("Draw supply", CrossoverBattleBridge.Prepare(roster, kit, initial, 1, new(SessionGame.Ff8, CrossoverBattleAbilityKind.Draw, 0), enemy).Reason);
        Assert.Contains("Card conversion", CrossoverBattleBridge.Prepare(roster, kit, initial, 1, new(SessionGame.Ff8, CrossoverBattleAbilityKind.Card, 0), enemy).Reason);
        Assert.Contains("cannot prepare this ability", CrossoverBattleBridge.Prepare(roster, kit, initial, 1, spell with { NativeId = ushort.MaxValue }, enemy).Reason);
        Assert.Equal(before, JsonSerializer.Serialize(initial));

        CrossoverBattleAdmission pending = CrossoverBattleBridge.Prepare(roster, kit, initial, 1, spell, enemy);
        CrossoverBattleAction action = Assert.IsType<CrossoverBattleAction>(pending.Action);
        BattleNativeAction nativeFire = Assert.IsType<BattleNativeAction>(pending.NativeAction);
        Assert.Equal((2, 1, 1), ((int)nativeFire.Command, (int)nativeFire.ActionId, nativeFire.KernelSection));
        Assert.Equal(4, pending.Vitals.GetMagicCount(row, fire));
        Assert.Single(pending.Vitals.MagicSystem.Reservations);
        Assert.Empty(initial.MagicSystem.Reservations);
        NativeCharacterVitals released = CrossoverBattleBridge.ReleaseStock(roster, pending.Vitals, action);
        Assert.Equal(4, released.GetMagicCount(row, fire));
        Assert.Empty(released.MagicSystem.Reservations);
        NativeCharacterVitals committed = CrossoverBattleBridge.CommitStock(roster, pending.Vitals, action);
        Assert.Equal(3, committed.GetMagicCount(row, fire));
        Assert.Same(committed, CrossoverBattleBridge.CommitStock(roster, committed, action));
        Assert.Null(CrossoverBattleBridge.Prepare(roster, kit, committed, 1, spell, enemy).Action);
        Assert.Equal(before, JsonSerializer.Serialize(initial));
        Assert.Equal(hostBefore, JsonSerializer.Serialize(host.Capture()));
    }

    [Fact]
    public void GenericNumericReceiptRequiresNoHostSpellCalibrationRecord()
    {
        BattleNativeAction native = new(2, 57, 1, 57)
        {
            MagicId = 511, AttackType = 2, Power = 18, HitCount = 1, TargetFlags = 0x10, AttackFlags = 1, DamagePresentation = 0, Element = 1, ElementPercent = null, StatusAttack = 255, Statuses0 = 2, Statuses1 = 0, Hit = 100, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        BattlePlayerStats stats = new(17, 8, 6, 8, 20, 16, 100, 0, new int[8], 0, 0);
        CrossoverFf8BattleKit kit = new(0, 100, 100, 7, 0, stats, new(0, 0, []), native, new(0, 5, 0, 10, []), 0, [2], []);
        Ff7BattleGuestPhysicalHit hit = CrossoverBattleAbilityRules.Describe(kit, native);
        Ff7BattleDamageActor targetStats = new(6, 10, 10, 10, 0, 0, 0, 16, 16, IsEnemy: true);
        Ff7BattleActionVitals source = new(100, 100, 0, 0, 0);
        Ff7BattleActionVitals target = new(600, 600, 0, 0, 0);
        Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.Resolve(hit.Source, source, targetStats, target, hit.Attack, hit.StatusRecord, new(new byte[3988], Ff7BattleRng.BeginBattle()));

        Assert.False(receipt.Damage.Missed);
        Assert.InRange(receipt.Damage.Damage, 1, 599);
        Assert.Equal(target.Hp - (uint)receipt.Damage.Damage, receipt.Target.Hp);
        Assert.Equal(Ff7NativeBattleActionResolver.Poison, receipt.AddedStatuses);
        Assert.Equal(source, receipt.Source);
        byte[] projected = Enumerable.Range(0, 256).Select(CrossoverBattleAbilityRules.ProjectMagic).ToArray();
        Assert.Equal((0, 23, 255), ((int)projected[0], (int)projected[6], (int)projected[^1]));
        Assert.Equal(projected.Order(), projected);
    }

    private static Ff7BattleSnapshot Opening()
    {
        Ff7BattleActorState[] actors =
        [
            new(0, new(0, 0, 8, 10, 10, 100, 100), 0, []),
            new(1, new(1, 0, 8, 10, 10, 100, 100), 0, []),
            new(16, new(4, 0, 31, 0, 0, 30, 30), 0, []),
            new(16, new(5, 0, 31, 0, 0, 30, 30), 0, [])
        ];
        return new(1, 300, 0, 0, actors, Ff7BattleRng.BeginBattle(), Ff7BattleCommandQueueState.Create(), []);
    }
}
