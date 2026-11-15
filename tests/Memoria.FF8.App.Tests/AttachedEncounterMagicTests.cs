using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AttachedEncounterMagicTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void Compiled_normal_magic_publishes_one_native_header_group(bool doubleActive, int casts)
    {
        string directory = TestResources.GameDirectory;
        BattleRun original = Run(directory, attached: false, doubleActive, casts);
        BattleRun hosted = Run(directory, attached: true, doubleActive, casts);
        Assert.Equal(original.Headers, hosted.Headers);
        Assert.Equal(original.Damage, hosted.Damage);
        Assert.Equal(original.Random, hosted.Random);
    }

    private sealed record BattleRun((byte Command, ushort Action, byte Sequence, ushort Effect, int Contacts, byte Repeats)[] Headers,
        (string? Target, int Amount)[] Damage, byte[] Random);

    private static BattleRun Run(string directory, bool attached, bool doubleActive, int casts)
    {
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => BindFirePolicy(definition)) { UseAttachedBattlePresentation = attached };
        ExpandedBattleInstalledEntry.Prepare(world, session, _ => { }, 94, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == battle.ReadDefinition().EncounterMod!.OriginalActorId);
        BattleCombatant readyParty = battle.Combatants.First(actor => actor.IsPlayer);
        foreach (BattleCombatant actor in battle.Combatants)
        {
            actor.Atb = actor == readyParty ? BattleSession.AtbMaximum : 0;
        }
        TickUntil(world, () => battle.NativeEntryReady && battle.QueuedActions.Count == 0 && battle.ActiveAction is null && battle.CanChoosePlayerCommand(readyParty.Id));
        if (battle.SupportsPlayerLimitSelection(readyParty.Id) && !battle.HasNativeLimitSelection(readyParty.Id))
        {
            battle.SelectPlayerCommandActor(readyParty.Id);
        }
        if (doubleActive)
        {
            caster.NativeStatus1 |= 0x20000;
        }
        int start = battle.Events.Count;
        caster.Atb = BattleSession.AtbMaximum;
        NativeBattleActionHeaderSlot[] headers = [];
        HashSet<long> tokens = [];
        BattleModelOwner source = battle.ReadPresentationModelOwner(caster.Id);
        TickUntil(world, () =>
        {
            NativeBattleActionPacketsState packets = battle.ExpandedPresentationProcess?.ActionPackets ?? battle.NativeActionPackets;
            NativeBattleActionHeaderSlot[] current = packets.PublishedHeaders().Where(entry => entry.Slot.Source == source && entry.Slot.Command == 2).Select(entry => entry.Slot).ToArray();
            if (current.Length == casts)
            {
                headers = current;
            }
            if (current.Length != 0 && battle.ExpandedPresentationProcess?.ActiveActionToken is long token)
            {
                tokens.Add(token);
            }
            return headers.Length == casts && battle.Events.Skip(start).Count(entry => entry.Kind == "Damage" && entry.ActorId == caster.Id) >= casts &&
                battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.ExpandedPresentationProcess?.HasActiveAction != true;
        });
        Assert.Equal(casts - 1, headers[0].ReadRepeatModeMinusOne());
        Assert.All(headers, header => Assert.Equal(1, header.ReadContactCount()));
        Assert.Equal(1ul, battle.EncounterPolicies!.Cursors[caster.Id].CommittedTurns);
        Assert.Empty(caster.MagicStocks);
        var damage = battle.Events.Skip(start).Where(entry => entry.Kind == "Damage" && entry.ActorId == caster.Id).Select(entry => (entry.TargetId, entry.Amount)).ToArray();
        Assert.Equal(casts, damage.Length);
        if (attached)
        {
            Assert.Single(tokens);
            Assert.Equal(casts, battle.Events.Skip(start).Count(entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id));
        }
        return new(headers.Select(header => (header.Command, header.Action, header.Sequence, header.Effect, header.ReadContactCount(), header.ReadRepeatModeMinusOne())).ToArray(), damage, battle.Capture().Random!.Indexes.ToArray());
    }

    private static BattleDefinition BindFirePolicy(BattleDefinition definition)
    {
        const string name = "fixture.encounter";
        const string policyId = "fire";
        const byte castingSequence = 11;
        BattleCombatantDefinition actor = Assert.Single(definition.Combatants, member => !member.IsPlayer && member.InitialActive && member.ModelId == 61);
        EncounterPatchComposition composition = EncounterPatchComposer.Compose(new(name, "Classic", ["original"]), []);
        EncounterCompiledDefinition mod = EncounterPolicyRuntime.Compile(composition, definition, new(name, actor.Id, castingSequence, castingSequence, [], true));
        BattleNativeAction fire = definition.NativeActions.Single(action => action.Command == 2 && action.ActionId == 1);
        EncounterCompiledAction cast = new(EncounterCompiledActionKind.Cast, [new("Fire", fire, 0, true, false)], EncounterCompiledTargetKind.RandomOpponent, null, true, false, null, []);
        EncounterCompiledPolicy policy = new(policyId, [new("normal", 1, 1, 0, cast)]);
        return definition with { EncounterMod = mod with { Policies = mod.Policies.Add(policyId, policy), NormalActionPolicies = mod.NormalActionPolicies.Add("original", policyId) } };
    }

    private static void TickUntil(GameWorld world, Func<bool> condition)
    {
        for (int callback = 0; callback < 1200 && !condition(); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(condition(), string.Join(", ", world.Battle!.Events.TakeLast(10).Select(entry => $"{entry.Kind}/{entry.ActorId}")));
    }
}
