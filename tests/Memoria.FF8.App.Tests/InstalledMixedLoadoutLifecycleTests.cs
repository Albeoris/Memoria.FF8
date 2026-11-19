using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Opt-in ordinary native combat from an externally supplied standalone field checkpoint.</summary>
public sealed class InstalledMixedLoadoutLifecycleTests
{
    [Fact]
    public void Three_mixed_styles_complete_contacts_revival_escape_and_the_following_battle()
    {
        RequireOptIn();
        Measurement[] baseline = Run([0, 4, 10], null, lifecycle: false);
        Console.WriteLine("LOADOUT-LIFECYCLE-BASELINE " + JsonSerializer.Serialize(baseline));
        Measurement[] mixed = Run([0, 4, 10], [8, 1, 9], lifecycle: true);
        Assert.Equal(baseline.Select(sample => sample.Character), mixed.Take(3).Select(sample => sample.Character));
        Console.WriteLine("LOADOUT-LIFECYCLE " + JsonSerializer.Serialize(new { Baseline = baseline, Mixed = mixed, Interpretation = "Measured headless native callbacks and current-thread allocations; no GPU/frame-pacing or threshold claim." }));
    }

    [Fact]
    public void Two_actors_sharing_Desperado_keep_action_owners_and_saved_progress_independent()
    {
        RequireOptIn();
        Measurement[] measured = Run([0, 6, 10], [8, 8, 9], lifecycle: false, sharedFamily: true);
        Assert.Equal(2, measured.Count(sample => sample.Kind == "Limit"));
        Console.WriteLine("LOADOUT-SHARED-FAMILY " + JsonSerializer.Serialize(measured));
    }

    private static Measurement[] Run(int[] recipients, int[]? donors, bool lifecycle, bool sharedFamily = false)
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("weapon-limit-field-source");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        using GameResourceHost selected = new(directory, watch: false);
        using GameAssets game = new(directory, resources: selected.Resources, modelOverrides: selected);
        GameSession session = new(PlayerStateLoader.Load(source, directory, sharedAssets: game, resources: selected.Resources));
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Null(session.State.Battle);
        using GameWorld world = new(session, directory, resources: selected.Resources, sharedAssets: game, modelOverrides: selected, readSceneMods: () => selected.SceneMods, readGameplayPolicy: () => selected.GameplayPolicy, validateWorldPresentation: false, applyEncounterMods: (definition, _) => definition with
        {
            Combatants = definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with { MaximumHpMultiplier = 32, MaximumHp = actor.MaximumHp * 32, CurrentHp = actor.MaximumHp * 32 }).ToArray()
        });
        world.EnsureField();
        NativeCharacterVitals vitals = session.CharacterVitals!.WithExtendedRoster(true);
        Assert.False(vitals.DreamEnabled);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        int itemCommand = vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)4);
        Assert.True(itemCommand >= 0);
        foreach (int recipient in recipients)
        {
            records[recipient] = records[recipient] with { Status = 0, MemberFlags = (ushort)(records[recipient].MemberFlags | 1), Junction = records[recipient].Junction with { Commands = checked((uint)(20 + itemCommand)) } };
        }
        session.SetCharacterVitals((vitals with { Records = records, CombatParty = recipients.Select(recipient => checked((byte)recipient)).ToArray(), AdditionalCombatParty = null, AdditionalCombatPartyMinimumCapacities = null }).RebuildParty().Rest());
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = 3, AdditionalEnemies = 0, LimitOncePerTurn = false });
        BattleAssets assets = world.BattleResources;
        BattleActionDefinition revive = assets.ActionDefinitions.First(action => action.Ability == BattleAbility.Item && action.Effect == BattleActionEffect.Revive);
        session.SetEconomy(session.State.Economy!.AddItem(revive.Id, 2, out _));
        BattleLoadoutDraft draft = session.CreateBattleLoadoutDraft(recipients.Select(recipient => new BattleLoadoutDefaults(recipient, session.CharacterVitals.GetBattleWeapon(recipient), BattleLimitFamilyRules.NativeFamilies(recipient))));
        if (donors is not null)
        {
            for (int index = 0; index < recipients.Length; index++)
            {
                draft.AssignWeapon(recipients[index], assets.WeaponDefinitions.First(weapon => weapon.CharacterId == donors[index]).Id);
                if (sharedFamily && index < 2 || lifecycle && index == 0)
                {
                    draft.RemoveAllLimits(recipients[index]);
                    draft.AddLimit(recipients[index], BattleLimitFamily.Desperado);
                }
            }
            session.ApplyBattleLoadoutDraft(draft);
        }
        string overrides = JsonSerializer.Serialize(session.State.BattleLoadouts);
        string progression = JsonSerializer.Serialize(session.State.LimitProgress);
        List<Measurement> samples = [];
        int field = session.State.FieldId;
        int encounters = lifecycle ? 2 : 1;
        for (int encounter = 0; encounter < encounters; encounter++)
        {
            string program = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
            NativeRandomEncounterState random = session.State.RandomEncounters?.Native ?? NativeRandomEncounterState.FromColdProcess();
            session.SetRandomEncounters(new(true, random, new(SceneKind.Field, session.State.FieldId, world.Events.Frame, ExpandedBattleInstalledEntry.Formation, program)));
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: ExpandedBattleInstalledEntry.Seed));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true);
            ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
            BattleCombatant[] party = recipients.Select(recipient => battle.Combatants.Single(actor => actor.IsPlayer && actor.ModelId == recipient)).ToArray();
            Assert.Equal(3, battle.Combatants.Count(actor => actor.IsPlayer));
            for (int index = 0; index < party.Length; index++)
            {
                BattleCombatant actor = party[index];
                BattleCombatantDefinition definition = battle.GetCombatantDefinition(actor.Id);
                Assert.Equal(recipients[index], definition.NativePlayerStatRules!.CharacterId);
                Assert.Equal(donors?[index] ?? recipients[index], definition.WeaponProfile!.ResourceOwner);
                BattleCombatant target = Target(battle);
                Ready(actor);
                samples.Add(Execute(actor, "Attack", () => world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: actor.Id, TargetId: target.Id) { ForcePhysical = true })));
            }
            if (sharedFamily)
            {
                for (int index = 0; index < 2; index++)
                {
                    BattleCombatant actor = party[index];
                    BattleCombatant other = party[1 - index];
                    string otherDefinition = JsonSerializer.Serialize(battle.GetCombatantDefinition(other.Id));
                    NativeBattleGraphicsModel otherModel = Model(battle, other);
                    string otherSelection = JsonSerializer.Serialize(battle.Capture().NativeLimits.Where(selection => selection.ActorId == other.Id));
                    actor.CurrentHp = 1;
                    Ready(actor);
                    SelectLimit(actor);
                    battle.SelectNativeLimitFamily(actor.Id, BattleLimitFamily.Desperado);
                    BattleActionDefinition action = Assert.Single(battle.GetNativePlayerActionChoices(actor.Id, limits: true));
                    BattleCombatant target = Target(battle);
                    samples.Add(Execute(actor, "Limit", () => world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: actor.Id, TargetId: target.Id, Ability: BattleAbility.LimitBreak, ActionId: action.Id))));
                    Assert.Equal(otherDefinition, JsonSerializer.Serialize(battle.GetCombatantDefinition(other.Id)));
                    Assert.Equal(otherSelection, JsonSerializer.Serialize(battle.Capture().NativeLimits.Where(selection => selection.ActorId == other.Id)));
                    Assert.Equal(otherModel.ActiveAnimationBank, Model(battle, other).ActiveAnimationBank);
                    Assert.Equal(otherModel.HasLinkedModelRing140, Model(battle, other).HasLinkedModelRing140);
                    Assert.Equal(otherModel.ModelId4, Model(battle, other).ModelId4);
                    Assert.Null(battle.GetNativeShot(other.Id));
                    Assert.Null(battle.GetNativeDuel(other.Id));
                    Assert.Null(battle.GetNativeRenzokuken(other.Id));
                    Assert.Equal(progression, JsonSerializer.Serialize(session.State.LimitProgress));
                    Assert.Equal(progression, JsonSerializer.Serialize(battle.ReadDefinition().LimitProgress));
                }
            }
            if (lifecycle && encounter == 0)
            {
                party[0].CurrentHp = 1;
                Ready(party[0]);
                SelectLimit(party[0]);
                battle.SelectNativeLimitFamily(party[0].Id, BattleLimitFamily.Desperado);
                BattleActionDefinition limit = Assert.Single(battle.GetNativePlayerActionChoices(party[0].Id, limits: true));
                samples.Add(Execute(party[0], "Limit", () => world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: party[0].Id, TargetId: Target(battle).Id, Ability: BattleAbility.LimitBreak, ActionId: limit.Id))));
                BattleCombatant fallen = party[2];
                fallen.CurrentHp = 1;
                Ready(party[0]);
                samples.Add(Execute(party[0], "FriendlyKo", () => world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: party[0].Id, TargetId: fallen.Id) { ForcePhysical = true })));
                Assert.False(fallen.IsAlive);
                Assert.Equal(0, battle.ReadPresentedVital(fallen.Id).HitPoints);
                Ready(party[1]);
                samples.Add(Execute(party[1], "Revive", () => world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: party[1].Id, TargetId: fallen.Id, Ability: BattleAbility.Item, ActionId: revive.Id)), requireDamage: false));
                Assert.True(fallen.IsAlive);
                Assert.Equal(fallen.CurrentHp, battle.ReadPresentedVital(fallen.Id).HitPoints);
                Ready(fallen);
                samples.Add(Execute(fallen, "PostRevivalAttack", () => world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: fallen.Id, TargetId: Target(battle).Id) { ForcePhysical = true })));
                Assert.False(battle.EscapeDisabled);
                battle.PublishNativeEscapeButtons(3);
                TickUntil(() => battle.CanReturnToField, "native escape");
                Assert.Equal(BattleOutcome.Escaped, battle.Outcome);
                battle.PublishNativeEscapeButtons(0);
            }
            else
            {
                ExpandedBattleInstalledEntry.CompleteDebugVictory(world);
            }
            world.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Null(world.Battle);
            Assert.Equal(SceneKind.Field, session.State.Scene);
            Assert.Equal(field, session.State.FieldId);
            Assert.Equal(overrides, JsonSerializer.Serialize(session.State.BattleLoadouts));

            void Ready(BattleCombatant actor)
            {
                actor.Atb = BattleSession.AtbMaximum;
                TickUntil(() => battle.CanChoosePlayerCommand(actor.Id), "readiness " + actor.Id);
            }

            void SelectLimit(BattleCombatant actor)
            {
                for (int selection = 0; selection < 256 && battle.GetNativeLimitLevel(actor.Id) == 0; selection++)
                {
                    battle.RefreshNativeLimit(actor.Id);
                }
                Assert.InRange(battle.GetNativeLimitLevel(actor.Id), (byte)1, (byte)4);
            }

            Measurement Execute(BattleCombatant actor, string kind, Action submit, bool requireDamage = true)
            {
                int start = battle.Events.Count;
                long frame = battle.Frame;
                long callbacks = battle.NativeHostCallbacks ?? 0;
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                long timestamp = Stopwatch.GetTimestamp();
                submit();
                TickUntil(() => battle.Events.Skip(start).Any(entry => entry.Kind == "ActionComplete" && entry.ActorId == actor.Id) && battle.ActiveAction is null && battle.Presentation?.HasActiveAction != true, kind + " " + actor.Id);
                double elapsed = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
                long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
                BattleEvent[] damage = battle.Events.Skip(start).Where(entry => entry.Kind == "Damage" && entry.ActorId == actor.Id).ToArray();
                if (requireDamage)
                {
                    Assert.Contains(damage, entry => entry.Amount > 0);
                }
                Assert.All(damage, entry => Assert.InRange(entry.Frame, frame, battle.Frame));
                return new(actor.ModelId, encounter, kind, battle.Frame - frame, (battle.NativeHostCallbacks ?? 0) - callbacks, damage.Select(entry => entry.Frame - frame).ToArray(), damage.Sum(entry => entry.Amount), allocated, elapsed);
            }

            void TickUntil(Func<bool> complete, string boundary)
            {
                for (int callback = 0; callback < 2400 && !complete(); callback++)
                {
                    world.ApplyBattleInput(new(BattleInputKind.Tick));
                }
                Assert.True(complete(), $"Encounter {encounter}: {boundary}; frame {battle.Frame}, outcome {battle.Outcome}.");
            }
        }
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
        return samples.ToArray();
    }

    private static NativeBattleGraphicsModel Model(BattleSession battle, BattleCombatant actor)
    {
        NativeBattleGraphicsState graphics = battle.Presentation!.NativeGraphics!;
        BattlePartyActorModel owner = graphics.ActorModels.Owners.OfType<BattlePartyActorModel>().Single(candidate => candidate.Actor.ActorId == actor.Id);
        return graphics.ActorModels.Get(owner);
    }

    private static BattleCombatant Target(BattleSession battle)
    {
        return battle.Combatants.First(actor => !actor.IsPlayer && actor.IsAlive && actor.IsActive && !actor.Untargetable);
    }

    private static void RequireOptIn()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_LIFECYCLE") == "1", "Set MEMORIA_FF8_LOADOUT_LIFECYCLE=1 with the external weapon-limit-field-source fixture catalog.");
    }

    private sealed record Measurement(int Character, int Encounter, string Kind, long Frames, long NativeCallbacks, long[] DamageFrames, int Damage, long AllocatedBytes, double ElapsedMilliseconds);
}
