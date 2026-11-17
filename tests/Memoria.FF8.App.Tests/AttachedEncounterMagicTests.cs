using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class AttachedEncounterMagicTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void Compiled_normal_magic_publishes_one_native_header_group(bool doubleActive, int casts)
    {
        string directory = TestResources.GameDirectory;
        BattleRun observed = Run(directory, doubleActive, casts);
        Assert.Equal(casts, observed.Damage.Length);
        Assert.NotEmpty(observed.Headers);
    }

    [Fact]
    public void Installed_Edea_compiled_turn_publishes_the_original_opening_taunt_before_its_spell()
    {
        string directory = TestResources.GameDirectory;
        BattleRun observed = Run(directory, doubleActive: false, casts: 1, nativeTaunt: true);
        Assert.NotNull(observed.Opening);
        Assert.Single(observed.Damage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_Edea_reaches_its_own_terminal_after_party_incapacity(bool petrified)
    {
        RunTerminal();

        void RunTerminal()
        {
            string directory = TestResources.GameDirectory;
            using GameAssets game = new(directory);
            using BattleAssets assets = new(directory);
            NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(88);
            GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
            session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
            using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => BindFirePolicy(definition, resource));
            ExpandedBattleInstalledEntry.Prepare(world, session, PrepareEdeaParty, 136, allies: 3, additionalEnemies: 0);
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            DriveEdeaTerminal(world, battle, petrified);
        }
    }

    [Fact]
    public void Installed_Edea_attached_terminal_returns_to_the_suspended_field_request()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(88);
        GameSession session = new(GameSession.Load(TestResources.FixturePath("story-battle-136")));
        using GameWorld world = new(session, directory, sharedAssets: game, expandedBattleAllyCount: 3, applyEncounterMods: (definition, context) => BindFirePolicy(definition, resource, context));
        world.EnsureField();

        NativeBattleCompletion request = Assert.IsType<NativeBattleCompletion>(world.Events.PendingInteractiveBattle);
        Assert.Equal((768, 0, 4, 93, (ushort)136, (ushort)18), (request.FieldId, request.ObjectId, request.ScriptId, request.Operation, request.Encounter, request.Flags));
        Assert.Null(world.Battle);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        Assert.NotNull(battle.Presentation);
        BattleEncounterOccurrence occurrence = Assert.IsType<BattleEncounterOccurrence>(session.State.PendingBattleEncounter);
        Assert.Equal(new ScriptedBattleOwner(request.FieldId, request.ObjectId, request.ScriptId, request.Operation, request.Flags, request.ProgramHash), occurrence.ScriptedOwner);

        DriveEdeaTerminal(world, battle, petrified: true);
        Assert.Equal(request.Frame, world.Events.Frame);
        Assert.Equal(request, world.Events.PendingInteractiveBattle);
        int callbacks = checked((int)battle.NativeHostCallbacks!.Value);
        world.ApplyBattleInput(new(BattleInputKind.Return));

        Assert.Null(world.Battle);
        Assert.Null(world.Events.PendingInteractiveBattle);
        Assert.Null(session.State.PendingBattleEncounter);
        Assert.Single(session.State.AppliedBattleResultIds, id => id == occurrence.OccurrenceId);
        NativeBattleCompletion completion = Assert.Single(world.Events.Scene.NativeBattleCompletions!, entry => entry.Frame == request.Frame && entry.ObjectId == request.ObjectId && entry.ScriptId == request.ScriptId && entry.Operation == request.Operation);
        Assert.Equal((byte)4, completion.Result);
        Assert.Equal(callbacks, completion.EmulatedCombatCallbacks);
        Assert.NotNull(completion.ReturnFrame);
        Assert.Null(completion.Abandonment);
        NativeBattleFieldReturnReceipt receipt = Assert.IsType<NativeBattleFieldReturnReceipt>(world.Events.Scene.NativeBattleFieldReturn);
        Assert.Equal((request.FieldId, request.ProgramHash, request.Frame, (byte)4), (receipt.Reload.FieldId, receipt.Reload.ProgramHash, receipt.Reload.BattleEntryFrame, receipt.Reload.NativeResult));
    }

    private static void DriveEdeaTerminal(GameWorld world, BattleSession battle, bool petrified)
    {
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
            NativeBattleActionPacketsState packets = battle.Presentation!.ActionPackets;
            sawTerminal |= packets.PublishedHeaders().Any(entry => entry.Slot is { Command: 8, Action: 207, Sequence: 15 } && entry.Slot.Source == source);
            return battle.Outcome != BattleOutcome.Active;
        });
        Assert.True(sawTerminal);
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal((byte)4, battle.NativeResult);
        Assert.True(battle.EncounterPolicies.TerminalReached);
        Assert.True(battle.EncounterPolicies.CleanedUp);
        Assert.Equal(petrified ? initialHp : new int[party.Length], party.Select(actor => actor.CurrentHp).ToArray());
        TickUntil(world, () => battle.CanReturnToField && (battle.Presentation?.TerminalFinished ?? true));
    }

    [Fact]
    public void Installed_Edea_summon_loads_the_full_batch_and_releases_a_defeated_copy()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(88);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => BindSummonPolicy(definition, resource));
        ExpandedBattleInstalledEntry.Prepare(world, session, PrepareEdeaParty, 136, allies: 3, additionalEnemies: 0);
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == battle.ReadDefinition().EncounterMod!.OriginalActorId);
        BattleCombatant readyParty = battle.Combatants.First(actor => actor.IsPlayer);
        foreach (BattleCombatant actor in battle.Combatants)
        {
            actor.Atb = actor == readyParty ? BattleSession.AtbMaximum : 0;
        }
        TickUntil(world, () => battle.NativeEntryReady && battle.QueuedActions.Count == 0 && battle.ActiveAction is null && battle.CanChoosePlayerCommand(readyParty.Id));
        caster.Atb = BattleSession.AtbMaximum;
        int eventStart = battle.Events.Count;
        HashSet<string> loaded = [];
        bool originalControl = false;
        bool fullArrival = false;
        TickUntil(world, () =>
        {
            NativeBattleActionPacketsState packets = battle.Presentation!.ActionPackets;
            originalControl |= packets.PublishedHeaders().Any(entry => entry.Slot is { Command: 0, Action: 9, Sequence: 11, Effect: 0 } && entry.Slot.Source == battle.ReadPresentationModelOwner(caster.Id));
            if (battle.EncounterAllocation is not { } batch)
            {
                return false;
            }
            NativeBattleGraphicsState graphics = battle.Presentation!.Graphics;
            foreach (string id in batch.ActorIds)
            {
                BattleCombatant actor = battle.Combatants.Single(actor => actor.Id == id);
                if (graphics.ActorModels.TryGet(battle.ReadPresentationModelOwner(id), out NativeBattleGraphicsModel? model) && model.EffectCounter97 == 2)
                {
                    loaded.Add(id);
                }
                if (batch.Phase is BattleEncounterAllocationPhase.Loading or BattleEncounterAllocationPhase.Ready or BattleEncounterAllocationPhase.Arriving)
                {
                    Assert.False(actor.IsActive);
                }
            }
            if (batch.Phase == BattleEncounterAllocationPhase.Arriving)
            {
                Assert.Equal(batch.ActorIds.Length, loaded.Count);
                Assert.Equal(batch.ActorIds.Length, battle.CaptureEncounterArrivals().Length);
                fullArrival = true;
            }
            return batch.Phase == BattleEncounterAllocationPhase.Complete;
        });
        Assert.True(originalControl);
        Assert.True(fullArrival);
        Assert.Equal(1ul, battle.EncounterPolicies!.Cursors[caster.Id].CommittedTurns);
        Assert.Null(battle.EncounterPolicies.PendingSummon);
        Assert.Equal(2, Assert.Single(battle.Events.Skip(eventStart), entry => entry.Kind == "EncounterSpawn").Amount);
        Assert.All(battle.EncounterAllocation!.ActorIds, id =>
        {
            BattleCombatant actor = battle.Combatants.Single(actor => actor.Id == id);
            Assert.True(actor.IsActive);
            Assert.False(actor.Hidden || actor.Untargetable || actor.Suspended);
            Assert.Equal(caster.Level, actor.Level);
            Assert.Equal(caster.MaximumHp, actor.CurrentHp);
        });
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Assert.Equal(battle.EncounterAllocation, restored.EncounterAllocation);
        Assert.Equal(battle.EncounterAllocation.ActorIds.Select(id => battle.Combatants.Single(actor => actor.Id == id).NativeActorIndex),
            restored.EncounterAllocation!.ActorIds.Select(id => restored.Combatants.Single(actor => actor.Id == id).NativeActorIndex));

        string defeatedId = battle.EncounterAllocation.ActorIds[0];
        string livingId = battle.EncounterAllocation.ActorIds[1];
        BattleCombatant defeated = battle.Combatants.Single(actor => actor.Id == defeatedId);
        BattleModelOwner defeatedOwner = battle.ReadPresentationModelOwner(defeatedId);
        BattleModelOwner livingOwner = battle.ReadPresentationModelOwner(livingId);
        int defeatedRow = ReadGraphics().ActorModels.Get(defeatedOwner).TextureResourceIndex!.Value;
        int livingRow = ReadGraphics().ActorModels.Get(livingOwner).TextureResourceIndex!.Value;
        NativeBattleModelCpuResources before = ReadCpu();
        NativeBattleModelCpuResourceRow extent = before.Rows[defeatedRow];
        bool shared = extent.Require(16) == before.Rows[livingRow].Require(16);
        int reclaimed = extent.PrivateLength4!.Value + (shared ? 0 : extent.SharedLength8!.Value);
        defeated.CurrentHp = 1;
        readyParty.Atb = BattleSession.AtbMaximum;
        TickUntil(world, () => battle.CanChoosePlayerCommand(readyParty.Id));
        int attackStart = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: readyParty.Id, TargetId: defeatedId) { ForcePhysical = true });
        bool sawRemoval = false;
        TickUntil(world, () =>
        {
            NativeBattleGraphicsState graphics = ReadGraphics();
            sawRemoval |= graphics.Descriptors.Packets.Any(packet => packet.Command == 105 && packet.ModelSubject is NativeBattleDescriptorModelSubject.ExplicitModel model && model.Owner == defeatedOwner);
            return battle.Capture().NativeAllocations.Single(allocation => allocation.Id == defeatedId).Retirement is { ResourcesReleased: true };
        });
        Assert.True(sawRemoval);
        Assert.False(defeated.IsActive);
        Assert.True(battle.Combatants.Single(actor => actor.Id == livingId).IsActive);
        Assert.Equal(0, ReadGraphics().ActorModels.Get(defeatedOwner).Flags0 & 3);
        NativeBattleModelTextureCache cache = battle.Presentation!.ModelTextureCache;
        Assert.Equal((byte)0, cache.Rows[defeatedRow].Kind);
        Assert.Equal((byte)3, cache.Rows[livingRow].Kind);
        Assert.Equal(before.EnemyCursor - reclaimed, ReadCpu().EnemyCursor);
        Assert.Single(battle.Events.Skip(attackStart), entry => entry.Kind == "NativeModelRemoved" && entry.ActorId == defeatedId);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);

        NativeBattleGraphicsState ReadGraphics() => battle.Presentation!.Graphics;
        NativeBattleModelCpuResources ReadCpu() => battle.Presentation!.ModelCpuResources;
    }

    private sealed record BattleRun((byte Command, ushort Action, byte Sequence, ushort Effect, int Contacts, byte Repeats)[] Headers,
        (string? Target, int Amount)[] Damage, byte[] Random, EncounterNativeEventState? Opening);

    private static BattleRun Run(string directory, bool doubleActive, int casts, bool nativeTaunt = false)
    {
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleGraphicsResource? tauntResource = nativeTaunt ? assets.ReadEnemyNativeGraphicsResource(88) : null;
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => BindFirePolicy(definition, tauntResource));
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
            NativeBattleGraphicsState graphics = battle.Presentation!.Graphics;
            if (nativeTaunt && battle.Events.Skip(start).Any(entry => entry.Kind == "NativeText" && entry.ActorId == caster.Id))
            {
                tauntCallback ??= graphics.Callback;
            }
            if (nativeTaunt && graphics.OriginalEffect is { Parameters: not null } effect && effect.RequestedEffect == fireEffect)
            {
                spellCallback ??= graphics.Callback;
            }
            NativeBattleActionPacketsState packets = battle.Presentation!.ActionPackets;
            NativeBattleActionHeaderSlot[] current = packets.PublishedHeaders().Where(entry => entry.Slot.Source == source && entry.Slot.Command == 2).Select(entry => entry.Slot).ToArray();
            if (current.Length == casts)
            {
                headers = current;
            }
            if (current.Length != 0 && battle.Presentation?.ActiveActionToken is long token)
            {
                tokens.Add(token);
            }
            return headers.Length == casts && battle.Events.Skip(start).Count(entry => entry.Kind == "Damage" && entry.ActorId == caster.Id) >= casts &&
                battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.Presentation?.HasActiveAction != true;
        });
        Assert.Equal(casts - 1, headers[0].ReadRepeatModeMinusOne());
        Assert.All(headers, header => Assert.Equal(1, header.ReadContactCount()));
        Assert.Equal(1ul, battle.EncounterPolicies!.Cursors[caster.Id].CommittedTurns);
        Assert.Empty(caster.MagicStocks);
        var damage = battle.Events.Skip(start).Where(entry => entry.Kind == "Damage" && entry.ActorId == caster.Id).Select(entry => (entry.TargetId, entry.Amount)).ToArray();
        Assert.Equal(casts, damage.Length);
        Assert.Single(tokens);
        Assert.Equal(casts, battle.Events.Skip(start).Count(entry => entry.Kind == "EffectContact" && entry.ActorId == caster.Id));
        EncounterNativeEventState? opening = null;
        if (tauntResource is not null)
        {
            EncounterNativeTextCue cue = Assert.Single(battle.ReadDefinition().EncounterMod!.NativeEvents!.Resources.Opening);
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

    private static BattleDefinition BindFirePolicy(BattleDefinition definition, NativeBattleGraphicsResource? tauntResource, EncounterBindingContext? context = null)
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
            binding = EncounterBindingResolver.Resolve(name, definition, context ?? new("Classic"));
        }
        EncounterPatchComposition composition = EncounterPatchComposer.Compose(new(name, "Classic", ["original"]), []);
        EncounterCompiledDefinition mod = EncounterPolicyRuntime.Compile(composition, definition, binding);
        if (tauntResource is not null)
        {
            mod = mod with { NativeEvents = EncounterNativeEventBinding.Bind(binding, definition.Combatants.Single(actor => actor.Id == binding.OriginalActorId), tauntResource), EventGates = mod.EventGates.Add("opening", new("original", "nativeTaunts", 1)) };
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

    private static BattleDefinition BindSummonPolicy(BattleDefinition definition, NativeBattleGraphicsResource resource)
    {
        definition = BindFirePolicy(definition, resource);
        EncounterCompiledDefinition mod = definition.EncounterMod!;
        EncounterCompiledAction summon = new(EncounterCompiledActionKind.SummonGroup, [], EncounterCompiledTargetKind.Self, null, false, false, null, [new("first-copy", "left"), new("second-copy", "right")]);
        EncounterCompiledPolicy policy = new("summon", [new("first-turn", 1, 1, 0, summon)]);
        return definition with
        {
            EncounterMod = mod with
            {
                Policies = mod.Policies.Add(policy.Id, policy),
                NormalActionPolicies = mod.NormalActionPolicies.SetItem("original", policy.Id),
                Templates = mod.Templates.Add("first-copy", new("first-copy", "original", "copies", "fire", "arrival")).Add("second-copy", new("second-copy", "original", "copies", "fire", "arrival")),
                Presentations = mod.Presentations.Add("arrival", new("arrival", "002", 8, 28, 0, 1)),
                Placements = mod.Placements.Add("left", new("left", -1500, 0, 0)).Add("right", new("right", 1500, 0, 0))
            }
        };
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
