using Memoria.FF8.Assets;
using System.Reflection;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(21, 14)]
    [InlineData(615, 41)]
    public void Installed_enemy_turn_preserves_logical_last_attacker(ushort formation, int model)
    {
        AssertInstalledActorReferenceTurn(formation, model, local: 0, retaliationSlot: model == 14 ? 2 : 5, usesFire: false);
    }

    [Fact]
    public void Installed_DAT6_turn_uses_the_retained_Fire_caster()
    {
        AssertInstalledActorReferenceTurn(41, 6, local: 1, retaliationSlot: 4, usesFire: true);
    }

    private static void AssertInstalledActorReferenceTurn(ushort formation, int model, int local, int retaliationSlot, bool usesFire)
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleActionDefinition? fire = usesFire ? Assert.Single(assets.ActionDefinitions, action => action is { Ability: BattleAbility.Magic, Id: 1 }) : null;
        using GameWorld world = StartBattle(directory, PrepareField(directory), formation, fire, prepareHigherBand: usesFire, additionalEnemies: 0);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true, readinessActorId: caster.Id);
        for (int callback = 0; callback < 1600 && (!battle.CanChoosePlayerCommand(caster.Id) || battle.ActiveAction is not null || battle.QueuedActions.Count != 0); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanChoosePlayerCommand(caster.Id));
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.Null(caster.NativeActorIndex);

        BattleCombatant owner = battle.Combatants.First(actor => !actor.IsPlayer && actor.ModelId == model && actor.IsActive && actor.Slot is not null);
        NativeBattleAiDefinition ai = Assert.IsType<NativeBattleAiDefinition>(owner.NativeAi);
        NativeBattleAiDefinition source = assets.ReadEnemyAi(model, owner.Level);
        Assert.Equal(source.LevelBand, ai.LevelBand);
        for (int callback = 0; callback < 3; callback++)
        {
            Assert.Equal(source.Scripts[callback], ai.Scripts[callback]);
        }
        if (usesFire)
        {
            Assert.InRange(ai.LevelBand, 1, 2);
        }
        Assert.Equal(model == 6 ? 0u : 240u, owner.AiVariables[0]);
        Assert.Empty(owner.AiActorReferences);

        int firstEvent = battle.Events.Count;
        if (fire is null)
        {
            owner.NativeStatusTimers[0] = 60;
            owner.NativeStatus1 |= 1;
            battle.QueueAttack(caster.Id, owner.Id);
        }
        else
        {
            battle.QueueAction(caster.Id, owner.Id, BattleAbility.Magic, fire.Id);
        }
        for (int callback = 0; callback < 1600 && (!owner.AiActorReferences.ContainsKey(local) || battle.ActiveAction is not null || battle.QueuedActions.Count != 0); callback++)
        {
            battle.Tick();
        }
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.Contains(battle.Events.Skip(firstEvent), entry => entry is { Kind: "EffectContact" } && entry.ActorId == caster.Id && entry.TargetId == owner.Id);
        Assert.True(owner.IsAlive);
        Assert.Equal(caster.Id, owner.LastAttackerId);
        Assert.Equal<byte?>(usesFire ? (byte)1 : (byte)0, owner.LastNativeAction?.AttackType);
        Assert.Equal(new NativeBattleAiActorValue(caster.Id, null), owner.AiActorReferences[local]);
        if (usesFire)
        {
            Assert.Equal(1u, owner.AiVariables[0]);
            Assert.True(owner.CurrentHp > owner.MaximumHp >> 2, $"DAT6 has {owner.CurrentHp}/{owner.MaximumHp} HP and would enter its emergency branch.");
        }

        NativeBattleAiAbility retaliation = source.Abilities[16 * source.LevelBand + retaliationSlot];
        BattleNativeAction native = Assert.Single(battle.ReadDefinition().NativeActions, action => action.Command == retaliation.Command && action.ActionId == retaliation.Id);
        BattleDiagnosticEnemyAbility selection = new(model, owner.SceneSlot!.Value, source.LevelBand, retaliationSlot, retaliation.Command, retaliation.Id, retaliation.Sequence, null)
        {
            UseTurnScript = true,
            TurnScriptSha256 = Convert.ToHexString(SHA256.HashData(source.Scripts[1]))
        };
        NativeBattleRandomState selectedRandom = SelectActorReferenceTurnPhase(battle, model);
        owner.Atb = BattleSession.AtbMaximum;
        Assert.True(battle.CanSubmitDiagnosticEnemyAbility(owner.Id, selection));
        battle.SubmitDiagnosticEnemyAbility(owner.Id, selection, native.MagicId);
        BattleSession fork = battle.ForkForContinuation();
        Assert.Equal(owner.AiActorReferences[local], fork.Combatants.Single(actor => actor.Id == owner.Id).AiActorReferences[local]);

        BattleModelOwner attacker = battle.ReadPresentationModelOwner(caster.Id);
        BattleModelOwner enemy = battle.ReadPresentationModelOwner(owner.Id);
        int callbacks = 0;
        List<string> observedHeaders = [];
        byte initialTurn = owner.AiTurnCount;
        while (callbacks < 120 && owner.AiTurnCount == initialTurn && !HasRetaliation(battle))
        {
            battle.Tick();
            fork.Tick();
            callbacks++;
            foreach (var entry in battle.Presentation!.ActionPackets.PublishedHeaders().Where(entry => entry.Slot.Source == enemy))
            {
                string header = $"{entry.Slot.Command}/{entry.Slot.Action}/{entry.Slot.Sequence}";
                if (!observedHeaders.Contains(header))
                {
                    observedHeaders.Add(header);
                }
            }
        }
        NativeBattleRandomState observedRandom = battle.Capture().Random!;
        Assert.True(HasRetaliation(battle), $"DAT{model} slot {retaliationSlot} missing after {callbacks} callbacks. Enemy turn {initialTurn}->{owner.AiTurnCount}, local220={owner.AiVariables[0]}, RNG {selectedRandom.Indexes[selectedRandom.Lane]}->{observedRandom.Indexes[observedRandom.Lane]}, headers [{string.Join(", ", observedHeaders)}].");
        AssertRetaliation(battle);
        AssertRetaliation(fork);
        Assert.Equal(battle.Capture().Random!.Indexes.ToArray(), fork.Capture().Random!.Indexes.ToArray());

        bool HasRetaliation(BattleSession current)
        {
            return current.Presentation!.ActionPackets.PublishedHeaders().Any(entry => entry.Slot.Source == enemy && entry.Slot.Command == retaliation.Command && entry.Slot.Action == retaliation.Id);
        }

        void AssertRetaliation(BattleSession current)
        {
            NativeBattleActionPacketsState packets = current.Presentation!.ActionPackets;
            var header = Assert.Single(packets.PublishedHeaders(), entry => entry.Slot.Source == enemy && entry.Slot.Command == retaliation.Command && entry.Slot.Action == retaliation.Id);
            Assert.Equal(native.MagicId, header.Slot.Effect);
            Assert.Equal(retaliation.Sequence, header.Slot.Sequence);
            Assert.Equal(attacker, packets.ReadContact(packets.ResolveContact(header.Reference, 0)).Primary.Recipient);
        }
    }

    private static NativeBattleRandomState SelectActorReferenceTurnPhase(BattleSession battle, int model)
    {
        NativeBattleRandomState current = Assert.IsType<NativeBattleRandomState>(battle.Capture().Random);
        FieldInfo random = typeof(BattleSession).GetField("random", BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (int offset = 0; offset < 256; offset++)
        {
            NativeBattleRandomState candidate = current with { Indexes = current.Indexes.SetItem(current.Lane, unchecked((byte)(current.Indexes[current.Lane] + offset))) };
            _ = candidate.Draw(out byte first).Draw(out byte second).Draw(out byte third);
            bool selectsAttacker = model switch
            {
                14 => first % 2 == 0,
                // DAT6 PC63 branches past the competing slot5 only when its RNG value is divisible by three.
                6 => first % 3 != 0 && second % 3 == 0 && third % 3 != 0,
                41 => first % 3 != 0 && second % 2 == 0,
                _ => throw new InvalidOperationException("The installed actor-reference fixture lacks the selected source model.")
            };
            if (selectsAttacker)
            {
                random.SetValue(battle, candidate);
                return candidate;
            }
        }
        throw new InvalidOperationException("The installed RNG table has no phase that selects the actual last-attacker branch.");
    }

    [Fact]
    public void Installed_player_counter_publishes_its_reaction_without_writing_an_enemy_ai_target()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using GameWorld world = StartBattle(directory, PrepareField(directory), 513, additionalEnemies: 0);
        BattleSession source = Assert.IsType<BattleSession>(world.Battle);
        BattleDefinition definition = source.ReadDefinition();
        BattleCombatantDefinition[] actors = definition.Combatants.Select(actor => actor.Id != CasterId ? actor : actor with
        {
            NativePlayerStatRules = actor.NativePlayerStatRules! with { NativeStatusPassiveFlags = actor.NativePlayerStatRules!.NativeStatusPassiveFlags!.Value | 4 }
        }).ToArray();
        BattleSession battle = GameWorld.CreateLiveBattleSession(definition with { Combatants = actors }, new GameplayPreferences { AtbMode = BattleAtbMode.TurnBased }, game, world.BattleResources);
        BattleCombatant player = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
        battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true, readinessActorId: player.Id);
        for (int callback = 0; callback < 1600 && (!battle.CanChoosePlayerCommand(player.Id) || battle.ActiveAction is not null || battle.QueuedActions.Count != 0); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanChoosePlayerCommand(player.Id));
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.Null(player.NativeAi);
        Assert.Null(player.PreviousAiTarget);

        BattleCombatant enemy = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive && actor.Slot is not null);
        NativeBattleAiDefinition ai = Assert.IsType<NativeBattleAiDefinition>(enemy.NativeAi);
        Assert.Equal(28, enemy.ModelId);
        const int abilitySlot = 0;
        NativeBattleAiAbility ability = ai.Abilities[16 * ai.LevelBand + abilitySlot];
        Assert.True(ability is { Command: 8, Id: 2, Sequence: 12, AttackType: 1, NativeAttackFlags: 0 });
        BattleNativeAction native = Assert.Single(definition.NativeActions, action => action.Command == ability.Command && action.ActionId == ability.Id);

        enemy.NativeStatusTimers[0] = 60;
        enemy.NativeStatus1 |= 1;
        battle.QueueAttack(player.Id, enemy.Id);
        for (int callback = 0; callback < 1600 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); callback++)
        {
            battle.Tick();
        }
        Assert.Equal(player.Id, enemy.LastAttackerId);
        Assert.True(enemy.IsAlive);
        enemy.Atb = BattleSession.AtbMaximum;
        BattleDiagnosticEnemyAbility selection = new(enemy.ModelId, enemy.SceneSlot!.Value, ai.LevelBand, abilitySlot, ability.Command, ability.Id, ability.Sequence, 203);
        int firstEvent = battle.Events.Count;
        battle.SubmitDiagnosticEnemyAbility(enemy.Id, selection, native.MagicId);
        BattleModelOwner playerModel = battle.ReadPresentationModelOwner(player.Id);
        BattleModelOwner enemyModel = battle.ReadPresentationModelOwner(enemy.Id);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        for (int callback = 0; callback < 480 && !HasCounterHeader(process); callback++)
        {
            battle.Tick();
        }

        Assert.Contains(battle.Events.Skip(firstEvent), entry => entry is { Kind: "EffectContact" } && entry.ActorId == enemy.Id && entry.TargetId == player.Id);
        Assert.Equal(enemy.Id, player.LastAttackerId);
        Assert.Equal<byte?>(0, player.LastNativeAction?.AttackType);
        Assert.Equal(4u, player.NativePlayerStatRules!.NativeStatusPassiveFlags!.Value & 4);
        Assert.Null(player.PreviousAiTarget);
        AssertCounterHeader(process);
        BattleSession fork = battle.ForkForContinuation();
        AssertCounterHeader(fork.Presentation!);
        Assert.Null(fork.Combatants.Single(actor => actor.Id == player.Id).PreviousAiTarget);

        bool HasCounterHeader(BattlePresentationProcess active)
        {
            return active.ActionPackets.PublishedHeaders().Any(entry => entry.Slot.Source == playerModel && entry.Slot.Command == 1);
        }

        void AssertCounterHeader(BattlePresentationProcess active)
        {
            NativeBattleActionPacketsState packets = active.ActionPackets;
            var header = Assert.Single(packets.PublishedHeaders(), entry => entry.Slot.Source == playerModel && entry.Slot.Command == 1);
            Assert.Equal(enemyModel, packets.ReadContact(packets.ResolveContact(header.Reference, 0)).Primary.Recipient);
        }
    }

    private static (EnemyAttackSetup Attacks, bool UsesFire)? ActorReferenceSetup(int effect) => effect switch
    {
        13 => (new(21, 14, 1, 5, HigherEnemyBand), false),
        120 => (new(41, 6, 0, 0, HigherEnemyBand), true),
        235 => (new(615, 41, 3, 0), false),
        _ => null
    };
}
