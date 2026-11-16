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

    [Fact]
    public void Installed_Edea_compiled_turn_publishes_the_original_opening_taunt_before_its_spell()
    {
        string directory = TestResources.GameDirectory;
        BattleRun original = Run(directory, attached: false, doubleActive: false, casts: 1, nativeTaunt: true);
        BattleRun hosted = Run(directory, attached: true, doubleActive: false, casts: 1, nativeTaunt: true);
        Assert.Equal(original.Opening, hosted.Opening);
        Assert.Equal(original.Headers, hosted.Headers);
        Assert.Equal(original.Damage, hosted.Damage);
        Assert.Equal(original.Random, hosted.Random);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_Edea_reaches_its_own_terminal_after_party_incapacity(bool petrified)
    {
        RunTerminal(attached: false);
        RunTerminal(attached: true);

        void RunTerminal(bool attached)
        {
            string directory = TestResources.GameDirectory;
            using GameAssets game = new(directory);
            using BattleAssets assets = new(directory);
            NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(88);
            GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
            session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
            using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => BindFirePolicy(definition, resource)) { UseAttachedBattlePresentation = attached };
            ExpandedBattleInstalledEntry.Prepare(world, session, PrepareEdeaParty, 136, allies: 3, additionalEnemies: 0);
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            battle.ConfigureDiagnosticScheduling(holdEnemyCommands: true);
            TickUntil(world, () => battle.NativeEntryReady && battle.QueuedActions.Count == 0 && battle.ActiveAction is null);
            BattleCombatant[] party = battle.Combatants.Where(actor => actor.IsPlayer).ToArray();
            int[] initialHp = party.Select(actor => actor.CurrentHp).ToArray();
            foreach (BattleCombatant actor in party)
            {
                if (!petrified)
                {
                    actor.CurrentHp = 0;
                    actor.Atb = 0;
                }
                actor.NativeStatus0 |= petrified ? (ushort)4 : (ushort)1;
            }
            string casterId = battle.ReadDefinition().EncounterMod!.OriginalActorId;
            BattleModelOwner source = battle.ReadPresentationModelOwner(casterId);
            TickUntil(world, () => battle.EncounterPolicies?.TerminalStarted == true);
            Assert.Equal(BattleOutcome.Active, battle.Outcome);
            Assert.False(battle.EncounterPolicies!.TerminalReached);
            bool sawTerminal = false;
            TickUntil(world, () =>
            {
                NativeBattleActionPacketsState packets = battle.ExpandedPresentationProcess?.ActionPackets ?? battle.NativeActionPackets;
                sawTerminal |= packets.PublishedHeaders().Any(entry => entry.Slot is { Command: 8, Action: 207, Sequence: 15 } && entry.Slot.Source == source);
                return battle.Outcome != BattleOutcome.Active;
            });
            Assert.True(sawTerminal);
            Assert.Equal(BattleOutcome.Victory, battle.Outcome);
            Assert.Equal((byte)4, battle.NativeResult);
            Assert.True(battle.EncounterPolicies.TerminalReached);
            Assert.True(battle.EncounterPolicies.CleanedUp);
            Assert.Equal(petrified ? initialHp : new int[party.Length], party.Select(actor => actor.CurrentHp).ToArray());
            TickUntil(world, () => battle.CanReturnToField && (battle.ExpandedPresentationProcess?.TerminalFinished ?? true));
        }
    }

    private sealed record BattleRun((byte Command, ushort Action, byte Sequence, ushort Effect, int Contacts, byte Repeats)[] Headers,
        (string? Target, int Amount)[] Damage, byte[] Random, EncounterNativeEventState? Opening);

    private static BattleRun Run(string directory, bool attached, bool doubleActive, int casts, bool nativeTaunt = false)
    {
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleGraphicsResource? tauntResource = nativeTaunt ? assets.ReadEnemyNativeGraphicsResource(88) : null;
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => BindFirePolicy(definition, tauntResource)) { UseAttachedBattlePresentation = attached };
        ExpandedBattleInstalledEntry.Prepare(world, session, current =>
        {
            if (nativeTaunt)
            {
                PrepareEdeaParty(current);
            }
        }, nativeTaunt ? (ushort)136 : (ushort)94, allies: 3, additionalEnemies: 0);
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
        long? tauntCallback = null;
        long? spellCallback = null;
        ushort fireEffect = battle.ReadDefinition().NativeActions.Single(action => action.Command == 2 && action.ActionId == 1).MagicId;
        BattleModelOwner source = battle.ReadPresentationModelOwner(caster.Id);
        TickUntil(world, () =>
        {
            NativeBattleGraphicsState graphics = battle.ExpandedPresentationProcess?.Graphics ?? battle.NativeGraphics!;
            if (nativeTaunt && battle.Events.Skip(start).Any(entry => entry.Kind == "NativeText" && entry.ActorId == caster.Id))
            {
                tauntCallback ??= graphics.Callback;
            }
            if (nativeTaunt && graphics.OriginalEffect is { Parameters: not null } effect && effect.RequestedEffect == fireEffect)
            {
                spellCallback ??= graphics.Callback;
            }
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
        EncounterNativeEventState? opening = null;
        if (tauntResource is not null)
        {
            EncounterNativeTextCue cue = Assert.Single(battle.ReadDefinition().EncounterMod!.NativeEvents!.Opening);
            var callbackEvents = battle.Events.Skip(start).ToArray();
            BattleEvent message = Assert.Single(callbackEvents, entry => entry.Kind == "NativeText" && entry.NativeTextIdentity == BattleTextIdentity.Enemy(caster.ModelId, cue.Text));
            Assert.Equal(caster.Id, message.ActorId);
            Assert.Equal((byte)3, message.NativeMessageMode);
            Assert.Equal(cue.Duration, message.Amount);
            Assert.Equal(tauntResource.ReadText91(cue.Text), message.Message);
            Assert.True(tauntCallback <= spellCallback, $"Taunt callback {tauntCallback}, spell callback {spellCallback}.");
            opening = battle.CaptureEncounterNativeEvents()[caster.Id];
            Assert.Equal(new EncounterNativeEventState(LastOwnTurn: 1, OpeningPending: false, OpeningCursor: 1), opening);
        }
        return new(headers.Select(header => (header.Command, header.Action, header.Sequence, header.Effect, header.ReadContactCount(), header.ReadRepeatModeMinusOne())).ToArray(), damage, battle.Capture().Random!.Indexes.ToArray(), opening);
    }

    private static BattleDefinition BindFirePolicy(BattleDefinition definition, NativeBattleGraphicsResource? tauntResource)
    {
        const string policyId = "fire";
        string name = tauntResource is null ? "fixture.encounter" : EncounterBindingResolver.Disc1Edea;
        EncounterBindingResult binding;
        if (tauntResource is null)
        {
            BattleCombatantDefinition actor = Assert.Single(definition.Combatants, member => !member.IsPlayer && member.InitialActive && member.ModelId == 61);
            binding = new(name, actor.Id, 11, 11, [], true);
        }
        else
        {
            binding = EncounterBindingResolver.Resolve(name, definition, new("Classic"));
        }
        EncounterPatchComposition composition = EncounterPatchComposer.Compose(new(name, "Classic", ["original"]), []);
        EncounterCompiledDefinition mod = EncounterPolicyRuntime.Compile(composition, definition, binding);
        if (tauntResource is not null)
        {
            mod = mod with { NativeEvents = EncounterNativeEventResourceReader.Bind(tauntResource), EventGates = mod.EventGates.Add("opening", new("original", "nativeTaunts", 1)) };
        }
        BattleNativeAction fire = definition.NativeActions.Single(action => action.Command == 2 && action.ActionId == 1);
        EncounterCompiledAction cast = new(EncounterCompiledActionKind.Cast, [new("Fire", fire, 0, true, false)], EncounterCompiledTargetKind.RandomOpponent, null, true, false, null, []);
        EncounterCompiledPolicy policy = new(policyId, [new("normal", 1, 1, 0, cast)]);
        return definition with { EncounterMod = mod with { Policies = mod.Policies.Add(policyId, policy), NormalActionPolicies = mod.NormalActionPolicies.Add("original", policyId) } };
    }

    private static void PrepareEdeaParty(GameSession session)
    {
        NativeCharacterVitals vitals = session.CharacterVitals!.WithGuestBattleRecords();
        NativeCharacterVital[] records = vitals.Records.Select(record => record with { MemberFlags = (ushort)(record.MemberFlags | 1), Status = 0 }).ToArray();
        session.SetCharacterVitals((vitals with { Records = records }).Rest().WithBattleParty([0, 2, 4], 3));
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
