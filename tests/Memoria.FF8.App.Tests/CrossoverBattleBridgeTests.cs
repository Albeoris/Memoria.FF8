using System.Text.Json;
using Memoria.FF7.Events;
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
    public void World_switch_uses_permanent_guest_rows_and_original_Squall_during_a_dream()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root), "Select the original FFVIII installation.");
        using GameAssets game = new(root!);
        using BattleAssets assets = new(root!);
        NativeCharacterVitals initial = game.ReadNewGameCharacterVitals();
        NativeCharacterVitals dream = initial.PrepareJunction(3, [0, 255, 255], 1).Vitals;
        Assert.Equal(8, dream.Records[0].CharacterId);

        CrossoverFf8BattleKit squall = CrossoverFf8BattleKit.Read(initial, assets, 0);
        CrossoverFf8BattleKit dreamSquall = CrossoverFf8BattleKit.Read(dream, assets, 0);
        Assert.Equal(0, dreamSquall.RecordRow);
        Assert.Equal(0, dreamSquall.Attack.CharacterId);
        Assert.Equal(squall.Weapon, dreamSquall.Weapon);
        Assert.Equal(squall.MaximumHitPoints, dreamSquall.MaximumHitPoints);
        Assert.Equal(squall.Stats.Strength, dreamSquall.Stats.Strength);
        Assert.Equal(8, dream.Records[0].CharacterId);

        for (int character = 6; character <= 10; character++)
        {
            Assert.Throws<NotSupportedException>(() => CrossoverFf8BattleKit.Read(dream, assets, character));
        }
        CrossoverFf8BattleKit legacyLaguna = CrossoverFf8BattleKit.Read(dream, assets, 8, admittedRow: 0);
        Assert.Equal(0, legacyLaguna.RecordRow);
        Assert.Equal(dream.DreamWeapons[0], legacyLaguna.Weapon);

        NativeCharacterVitals extended = dream.WithExtendedRoster(true);
        Assert.Throws<InvalidDataException>(() => CrossoverFf8BattleKit.Read(extended, assets, 8, admittedRow: 0));
        for (int character = 6; character <= 10; character++)
        {
            CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(extended, assets, character);
            Assert.Equal(character, kit.RecordRow);
            Assert.Equal(character, kit.Attack.CharacterId);
            Assert.Equal(extended.Records[character].Weapon, kit.Weapon);
            Assert.Null(kit.OpeningCombatUnavailableReason());
        }

        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(game, world: false) with { Characters = GameplaySaveCharacters.Capture(extended) };
        using MemoryStream payload = new();
        GameplaySaveCodec.Write(payload, document);
        CombinedSessionSave envelope = new() { ActiveGame = SessionGame.Ff8, Ff8Payload = payload.ToArray(), Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec, MixedCombatBindings = [new(0, 0), new(1, 8)] };
        using CrossoverGuestFieldVitals field = new(root!, () => envelope, updated => envelope = updated);
        field.AddHitPoints(1, -1);
        field.AddHitPoints(0, -2);
        NativeCharacterVital[] updatedRows = CrossoverSaveAdapter.ReadFf8(envelope).Characters.Records;
        Assert.Equal(8, updatedRows[0].CharacterId);
        Assert.Equal(8, updatedRows[8].CharacterId);
        Assert.Equal(extended.Records[0].HitPoints.Minimum - 2, updatedRows[0].HitPoints.Minimum);
        Assert.Equal(extended.Records[8].HitPoints.Minimum - 1, updatedRows[8].HitPoints.Minimum);
    }

    [Fact]
    public void Mixed_battle_cold_restore_keeps_dream_Squall_and_permanent_Laguna_on_separate_rows()
    {
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff8Root) && !string.IsNullOrWhiteSpace(ff7Root), "Select the installed original FFVII and FFVIII resources.");
        using GameAssets game = new(ff8Root!);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals().PrepareJunction(3, [0, 255, 255], 1).Vitals.WithExtendedRoster(true);
        origin.Records[0] = origin.Records[0] with { HitPoints = new(9999, 9999) };
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(game, world: false) with { Characters = GameplaySaveCharacters.Capture(origin) };
        using MemoryStream payload = new();
        GameplaySaveCodec.Write(payload, document);

        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using Ff7Session host = Ff7Session.NewGame(installation);
        host.SetFullCombatEnabled(true);
        host.Host.Execute(new(1L << 59, 0, 7, 0, new FieldPartyCommand(FieldPartyOperation.Replace, 0, 1, 2)));
        host.Host.StartEncounter(new(host.Progress.NextEncounterRequestId, 0, 7, 0, new FieldBattleCommand(364)), 0);
        Ff7BattleSession battle = Assert.IsType<Ff7BattleSession>(host.Battle);
        CombinedSessionSave source = new()
        {
            ActiveGame = SessionGame.Ff7,
            Ff7 = host.Capture(),
            Ff8Payload = payload.ToArray(),
            Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec,
            Mappings = CharacterMappingAssociations.Assign(CharacterMappingAssociations.Assign([], 0, 0), 1, 8),
            MixedCombatBindings = [new(0, 0), new(1, 8)]
        };
        CrossoverFf8BattleKit.ValidateWorldSwitchMappings(source, ff8Root!);
        using CrossoverMixedBattle mixed = new(ff8Root!, source, battle);
        Assert.Equal(new[] { (0, 0, 0), (1, 8, 8) }, battle.GuestVitalsBindings.Select(binding => (binding.StoryCharacter, binding.OriginCharacter, binding.OriginRow)));
        Ff7BattleGuestVitalsBinding squall = battle.GuestVitalsBindings[0];
        Assert.Equal(squall.MaximumHp, squall.InitialHp);
        Assert.True(squall.InitialHp < 9999);

        using MemoryStream envelope = new();
        CombinedSessionCodec.Write(envelope, source with { Ff7 = host.Capture(), MixedBattlePayload = mixed.Capture() });
        envelope.Position = 0;
        CombinedSessionSave restored = CombinedSessionCodec.Read(envelope);
        Assert.Equal(source.Ff8Payload, restored.Ff8Payload);
        using Ff7Session cold = Ff7Session.Restore(installation, restored.Ff7!);
        Ff7BattleSession coldBattle = Assert.IsType<Ff7BattleSession>(cold.Battle);
        using CrossoverMixedBattle coldMixed = new(ff8Root!, restored, coldBattle);
        Assert.Equal(battle.GuestVitalsBindings.ToArray(), coldBattle.GuestVitalsBindings.ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, coldBattle.Actors.Where(actor => actor.Actor.Slot < 3).Select(actor => actor.Identity));
        Assert.Equal(11, coldMixed.GuestAt(1)!.WorkingStock.Records.Length);
        Assert.Equal(8, coldMixed.GuestAt(1)!.WorkingStock.Records[0].CharacterId);
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
