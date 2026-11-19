using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Native family execution from a detached copy of an externally selected standalone field state.</summary>
public sealed class InstalledLimitLoadoutBattleTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (int recipient in new[] { 0, 6 })
        {
            foreach (BattleLimitFamily family in Enum.GetValues<BattleLimitFamily>())
            {
                yield return [recipient, family, false];
                if (family == BattleLimitFamily.Slot)
                {
                    yield return [recipient, family, true];
                }
            }
        }
        yield return [5, BattleLimitFamily.Combine, false];
        yield return [5, BattleLimitFamily.AngelWing, false];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Assigned_family_completes_its_original_native_process_on_recipient_body(int recipient, BattleLimitFamily family, bool cardSlots)
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("weapon-limit-field-source");
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(source));
        using GameResourceHost selected = new(directory, watch: false);
        using GameAssets game = new(directory, resources: selected.Resources, modelOverrides: selected);
        GameState loaded = PlayerStateLoader.Load(source, directory, sharedAssets: game, resources: selected.Resources);
        Assert.Equal(SceneKind.Field, loaded.Scene);
        Assert.Null(loaded.Battle);
        NativeLimitProgress progress = loaded.LimitProgress ?? game.ReadNewGameLimitProgress();
        NativeEconomy economy = Assert.IsType<NativeEconomy>(loaded.Economy);
        NativeItemStock[] items = economy.Items.ToArray();
        int ammunition = Array.FindIndex(items, item => item.ItemId == 101);
        items[ammunition >= 0 ? ammunition : Array.FindIndex(items, item => item.ItemId == 0)] = new(101, 2);
        // These explicit unlocks belong solely to this detached diagnostic component.
        GameSession session = new(loaded with
        {
            LimitProgress = progress with { BlueMagicFlags = ushort.MaxValue, DuelFlags = ushort.MaxValue, AngeloFlags = byte.MaxValue, SpecialMagicFlags = 0x3F, Options978 = 0 },
            ShotAvailability = game.ReadNewGameShotAvailability() with { Flags770 = byte.MaxValue },
            SavedBattleFlags = new(NativeInputConfiguration.OriginalInitHash, 0x20),
            Economy = economy with { Items = items },
            GlobalRandom = new(917)
        });
        using GameWorld world = new(session, directory, resources: selected.Resources, sharedAssets: game, modelOverrides: selected, readSceneMods: () => selected.SceneMods, readGameplayPolicy: () => selected.GameplayPolicy, validateWorldPresentation: false);
        world.EnsureField();
        Assert.False(session.CharacterVitals!.DreamEnabled);
        NativeCharacterVitals vitals = session.CharacterVitals.WithExtendedRoster(true).Rest();
        NativeCharacterVital[] records = vitals.Records.ToArray();
        NativeMagicStock[] spells = records[recipient].MagicInventory.Select(_ => new NativeMagicStock(0, 0)).ToArray();
        spells[0] = new(1, 100);
        records[recipient] = records[recipient] with { MemberFlags = (ushort)(records[recipient].MemberFlags | 1), HitPoints = new(1, 1), Status = 0, MagicInventory = spells };
        session.SetCharacterVitals((vitals with { Records = records, CombatParty = [checked((byte)recipient), 255, 255], AdditionalCombatParty = null, AdditionalCombatPartyMinimumCapacities = null }).RebuildParty());
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 3, AdditionalEnemies = 0, LimitOncePerTurn = false, SelphieCardSlots = cardSlots, SharedMagicStock = false, GunbladeMode = BattleGunbladeMode.Manual });
        BattleAssets assets = world.BattleResources;
        BattleWeaponCapabilities required = BattleLimitFamilyRules.RequiredWeapon(family);
        int weapon = required == BattleWeaponCapabilities.None ? session.CharacterVitals.GetBattleWeapon(recipient) : assets.WeaponDefinitions.First(entry => assets.ReadWeaponProfile(entry.Id).Supports(required)).Id;
        BattleLoadoutDraft draft = session.CreateBattleLoadoutDraft([new(recipient, session.CharacterVitals.GetBattleWeapon(recipient), BattleLimitFamilyRules.NativeFamilies(recipient))]);
        draft.AssignWeapon(recipient, weapon);
        draft.SetLimits(recipient, [family]);
        session.ApplyBattleLoadoutDraft(draft);
        string program = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
        NativeRandomEncounterState random = session.State.RandomEncounters?.Native ?? NativeRandomEncounterState.FromColdProcess();
        session.SetRandomEncounters(new(true, random, new(SceneKind.Field, session.State.FieldId, world.Events.Frame, 94, program)));
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant actor = Assert.Single(battle.Combatants, candidate => candidate.IsPlayer);
        int contacts = 0;
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true, readinessActorId: actor.Id);
        TickUntil(() => battle.NativeEntryReady, "entry");
        actor.Atb = BattleSession.AtbMaximum;
        actor.CurrentHp = 1;
        TickUntil(() => battle.CanChoosePlayerCommand(actor.Id), "readiness");
        Assert.True(battle.SelectDiagnosticLimit(actor.Id, new(1, 4, 256)).Accepted);
        battle.SelectNativeLimitFamily(actor.Id, family);
        Assert.True(Assert.Single(battle.GetNativeLimitFamilies(actor.Id)).Available);
        Assert.Equal(recipient, actor.ModelId);
        Assert.Equal(recipient, battle.GetCombatantDefinition(actor.Id).NativePlayerStatRules!.CharacterId);
        Assert.Equal(BattleLimitFamilyRules.SourceCharacter(family), actor.NativeLimit!.CharacterId);
        int start = battle.Events.Count;
        int shotsBefore = Ammo();
        bool fired = false;
        bool duelInput = false;

        if (family == BattleLimitFamily.Slot && cardSlots)
        {
            BattleNativeSlotsDeal deal = battle.BeginNativeSlotsDeal(actor.Id);
            int index = Enumerable.Range(0, deal.Candidates.Length).First(candidate => battle.CanCastNativeSlotsCard(deal.Candidates[candidate]));
            Assert.True(battle.SelectNativeSlotsCard(deal.Id, index));
            Assert.True(battle.SubmitNativeSlotsCard(deal.Id));
        }
        else
        {
            if (family == BattleLimitFamily.Slot)
            {
                for (int roll = 0; roll < 256; roll++)
                {
                    battle.RollNativeSlots(actor.Id);
                    if (battle.GetNativePlayerActionChoices(actor.Id, true).Any(choice => choice.Available))
                    {
                        break;
                    }
                }
            }
            BattleActionDefinition choice = battle.GetNativePlayerActionChoices(actor.Id, true).First(action => action.Available);
            BattleCombatant target = choice.Allies ? actor : battle.Combatants.First(candidate => !candidate.IsPlayer && candidate.IsActive && candidate.IsAlive && !candidate.Untargetable);
            world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: actor.Id, TargetId: target.Id, Ability: BattleAbility.LimitBreak, ActionId: choice.Id));
        }

        TickUntil(() => Completed() && battle.GetNativeShot(actor.Id) is null && battle.GetNativeDuel(actor.Id) is null && battle.GetNativeRenzokuken(actor.Id) is null && battle.ActiveAction is null && battle.Presentation?.HasActiveAction != true, "family completion", Input);
        Assert.True(contacts > 0, "The native invocation must publish actual contact receipts.");
        Assert.Equal(recipient, actor.ModelId);
        Assert.Equal(weapon, battle.GetCombatantDefinition(actor.Id).WeaponId);
        if (family == BattleLimitFamily.Shot)
        {
            Assert.True(fired);
            Assert.Equal(shotsBefore - 1, Ammo());
        }
        if (family == BattleLimitFamily.Duel)
        {
            Assert.True(duelInput);
        }
        if (family == BattleLimitFamily.AngelWing)
        {
            Assert.NotEqual(0u, actor.NativeStatus1 & 0x2000000);
            int casts = battle.Events.Count(entry => entry.ActorId == actor.Id && entry.Kind == "ActionComplete");
            actor.Atb = BattleSession.AtbMaximum;
            TickUntil(() => battle.Events.Count(entry => entry.ActorId == actor.Id && entry.Kind == "ActionComplete") > casts, "Angel Wing automatic cast");
        }
        if (!battle.CanReturnToField)
        {
            ExpandedBattleInstalledEntry.CompleteDebugVictory(world);
        }
        TickUntil(() => battle.CanReturnToField, "return");
        world.ApplyBattleInput(new(BattleInputKind.Return));
        Assert.Null(world.Battle);
        Assert.Equal(new[] { family }, session.State.BattleLoadouts!.Get(recipient)!.LimitFamilies);
        Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(source)));

        bool Completed() => battle.Events.Skip(start).Any(entry => entry.Kind == "ActionComplete" && entry.ActorId == actor.Id);
        int Ammo() => battle.Items.SingleOrDefault(item => item.Id == 101)?.Count ?? 0;

        void Input()
        {
            if (family == BattleLimitFamily.Renzokuken && battle.NativeRenzokukenTriggerWindowOpen)
            {
                world.ApplyBattleInput(new(BattleInputKind.GunbladeTrigger, ActorId: actor.Id));
            }
            if (family == BattleLimitFamily.Shot && battle.GetNativeShot(actor.Id) is { Phase: BattleNativeShotPhase.Ready })
            {
                if (!fired)
                {
                    world.ApplyBattleInput(new(BattleInputKind.ShotTrigger, ActorId: actor.Id));
                    fired = true;
                }
                else if (Ammo() < shotsBefore)
                {
                    battle.EndNativeShot(actor.Id);
                }
            }
            if (family == BattleLimitFamily.Duel && !duelInput && battle.GetNativeDuel(actor.Id) is { Phase: BattleNativeDuelPhase.Ready })
            {
                BattleNativeDuelChoice move = battle.GetNativeDuelChoices(actor.Id).First(choice => choice.Learned && !choice.Finisher);
                foreach (ushort button in move.Buttons)
                {
                    battle.SubmitNativeDuelInput(0);
                    world.ApplyBattleInput(new(BattleInputKind.Tick));
                    battle.SubmitNativeDuelInput(button);
                    world.ApplyBattleInput(new(BattleInputKind.Tick));
                }
                battle.SubmitNativeDuelInput(0);
                duelInput = true;
            }
        }

        void TickUntil(Func<bool> complete, string boundary, Action? beforeTick = null)
        {
            for (int callback = 0; callback < 6000 && !complete(); callback++)
            {
                beforeTick?.Invoke();
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                if (battle.Presentation?.LastFrameServices is { } services)
                {
                    contacts += services.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().Count();
                }
            }
            Assert.True(complete(), $"{family} on {recipient} timed out at {boundary}: " + string.Join(", ", battle.Events.TakeLast(8).Select(entry => entry.Kind + "/" + entry.ActorId)));
        }
    }
}
