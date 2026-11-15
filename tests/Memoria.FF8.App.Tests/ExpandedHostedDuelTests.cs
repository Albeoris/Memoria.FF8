using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedHostedDuelTests
{
    private const string ZellId = "party-1";
    private const int CallbackBudget = 1600;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_Duel_keeps_one_attached_event_across_moves_and_closing(bool timeout)
    {
        RunInstalledScenario(world => RunDuel(world, timeout));
    }

    [Fact]
    public void Initial_Duel_resource_preflight_failure_preserves_the_fifo_and_live_battle()
    {
        RunInstalledScenario(VerifyRejectedOpening);
    }

    private static void RunInstalledScenario(Action<GameWorld> run)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, assets.ReadColdProcessNativeBattleMemory()) with { GlobalRandom = new(917) });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        GameWorld world = new(session, directory, sharedAssets: assets);
        try
        {
            ExpandedBattleInstalledEntry.Prepare(world, session, current =>
            {
                NativeCharacterVitals vitals = (current.State.CharacterVitals! with { CombatParty = [0, 2, 3] }).WithGuestBattleRecords().Rest();
                NativeCharacterVital[] records = vitals.Records.ToArray();
                foreach (int ally in new[] { 0, 2, 3 })
                {
                    records[ally] = records[ally] with { MemberFlags = (ushort)(records[ally].MemberFlags | 1), HitPoints = new(0, 0), Status = 1 };
                }
                records[1] = records[1] with { MemberFlags = (ushort)(records[1].MemberFlags | 1), HitPoints = new(1, 1), Status = 0 };
                current.SetCharacterVitals((vitals with { Records = records }).WithBattleParty([0, 2, 3, 1], 4));
            });
            run(world);
        }
        catch (Exception failure)
        {
            try
            {
                world.Dispose();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(failure, cleanup);
            }
            throw;
        }
        world.Dispose();
    }

    private static void VerifyRejectedOpening(GameWorld world)
    {
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        ExpandedBattlePresentationProcess process = Assert.IsType<ExpandedBattlePresentationProcess>(battle.ExpandedPresentationProcess);
        BattleSession child = Assert.IsType<BattleSession>(typeof(ExpandedBattlePresentationProcess).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(process));
        for (int frame = 0; frame < CallbackBudget && (battle.QueuedActions.Count != 0 || battle.ActiveAction is not null || process.ActiveActionToken is not null); frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.Empty(battle.QueuedActions);
        Assert.Null(battle.ActiveAction);
        Assert.Null(process.ActiveActionToken);
        BattleCombatant zell = Assert.Single(battle.Combatants, actor => actor.Id == ZellId);
        BattleCombatant target = battle.Combatants.Where(IsAvailableEnemy).MaxBy(actor => actor.CurrentHp)!;
        zell.Atb = BattleSession.AtbMaximum;
        Assert.NotEqual((byte)0, battle.RefreshNativeLimit(ZellId));
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: ZellId, TargetId: target.Id, Ability: BattleAbility.LimitBreak, ActionId: 11 << 16));
        BattleAttackCommand head = Assert.Single(battle.QueuedActions);
        Assert.NotNull(head.NativeDuel);
        Assert.True(zell.IsBusy);
        Assert.Equal(0, zell.Atb);
        int[] hp = battle.Combatants.Select(actor => actor.CurrentHp).ToArray();
        BattleSnapshot before = battle.Capture();
        NativeBattleActionPacketsState packets = child.NativeActionPackets;
        NativeBattleDescriptorState descriptors = child.NativeGraphics!.Descriptors;
        const string reason = "Injected failure while reading the original Duel opening resource.";
        List<ushort> requests = [];
        battle.AttachNativeEffectResourceResolver(effect =>
        {
            requests.Add(effect);
            throw new InvalidDataException(reason);
        });

        InvalidDataException failure = Assert.Throws<InvalidDataException>(() => world.ApplyBattleInput(new(BattleInputKind.Tick)));

        Assert.Equal(reason, failure.Message);
        Assert.Equal((ushort)274, Assert.Single(requests));
        Assert.Same(head, Assert.Single(battle.QueuedActions));
        Assert.True(zell.IsBusy);
        Assert.Equal(0, zell.Atb);
        Assert.Null(battle.GetNativeDuel(ZellId));
        Assert.Null(battle.ActiveAction);
        Assert.Equal(hp, battle.Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(before.Random, battle.Capture().Random);
        Assert.Same(process, battle.ExpandedPresentationProcess);
        Assert.Null(process.ActiveActionToken);
        Assert.Same(packets, child.NativeActionPackets);
        Assert.Same(descriptors, child.NativeGraphics!.Descriptors);
    }

    private static void RunDuel(GameWorld world, bool timeout)
    {
        BattleSession battle = ExpandedBattleInstalledEntry.Start(world);
        ExpandedBattlePresentationProcess process = Assert.IsType<ExpandedBattlePresentationProcess>(battle.ExpandedPresentationProcess);
        BattleSession child = Assert.IsType<BattleSession>(typeof(ExpandedBattlePresentationProcess).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(process));
        BattleCombatant zell = Assert.Single(battle.Combatants, actor => actor.Id == ZellId);
        BattleModelOwner owner = battle.ReadPresentationModelOwner(ZellId);
        Assert.False(child.NativeActionPackets.ModelAliases.TryEncodeExact(owner, out _));
        Assert.Equal(4, battle.Combatants.Count(actor => actor.IsPlayer));
        Assert.Equal(5, battle.Combatants.Count(actor => !actor.IsPlayer));
        BattleCombatant target = battle.Combatants.Where(IsAvailableEnemy).MaxBy(actor => actor.CurrentHp)!;
        zell.Atb = BattleSession.AtbMaximum;
        Assert.NotEqual((byte)0, battle.RefreshNativeLimit(ZellId));
        zell.CurrentHp = zell.MaximumHp;
        zell.NativeStatus0 |= 2;
        int start = battle.Events.Count;
        List<(long Token, byte Command, ushort Action)> headers = [];
        List<NativeBattlePresentationReceipt> receipts = [];
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: ZellId, TargetId: target.Id, Ability: BattleAbility.LimitBreak, ActionId: 11 << 16));
        AdvanceUntil(() => ReadyForChoice() && headers.Any(header => header.Command == 241));
        Assert.Equal((byte)11, headers[0].Command);
        Assert.Equal((ushort)65531, headers[0].Action);
        Assert.Single(receipts.OfType<NativeBattleDuelReadinessReached>(), receipt => receipt.Callback == 0x48E5A0);
        BattleActionPresentation mirror = Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
        Assert.Equal((byte?)11, mirror.NativeQueuedCommand);
        AssertNoAftercare();

        if (timeout)
        {
            BattleNativeDuelState widget = battle.GetNativeDuel(ZellId)!;
            battle.AdvanceNativeDuelUi(widget.RemainingTimer);
        }
        else
        {
            BattleNativeDuelChoice move = battle.GetNativeDuelChoices(ZellId).First(choice => !choice.Finisher);
            PostChoice(move);
            AdvanceUntil(ReadyForChoice);
            Assert.Same(mirror, battle.ActiveAction);
            Assert.Contains(receipts.OfType<NativeBattleDuelReadinessReached>(), receipt => receipt.Callback == 0x48E5E0);
            AssertNoAftercare();
            BattleNativeDuelChoice[] route = FindGroupFinisher(battle.ReadDefinition(), zell.NativeLimit!, battle.GetNativeDuel(ZellId)!.MoveSetId);
            foreach (BattleNativeDuelChoice choice in route)
            {
                PostChoice(choice);
                if (!choice.Finisher)
                {
                    AdvanceUntil(ReadyForChoice);
                    Assert.Same(mirror, battle.ActiveAction);
                    AssertNoAftercare();
                }
            }
        }

        AdvanceUntil(() => battle.GetNativeDuel(ZellId) is null && battle.ActiveAction is null && process.ActiveActionToken is null);
        Assert.Contains(headers, header => header.Command == 241 && header.Action == (timeout ? 65532 : 65530));
        Assert.Equal(!timeout, headers.Any(header => header.Command == 239));
        NativeBattleDuelSelectionReached[] selections = receipts.OfType<NativeBattleDuelSelectionReached>().ToArray();
        Assert.NotEmpty(selections);
        Assert.Equal(selections.Length, selections.Select(receipt => receipt.Frame.Poll).Distinct().Count());
        Assert.Single(receipts.OfType<NativeBattlePresentationCompletionRequested>(), receipt => receipt.Callback == 0x47DE70 && receipt.ActorId == ZellId);
        Assert.Single(battle.Events.Skip(start), entry => entry is { Kind: "ActionComplete", ActorId: ZellId });
        Assert.Single(battle.Events.Skip(start), entry => entry is { Kind: "EffectDamage", TargetId: ZellId } && entry.Message.Contains("Poison", StringComparison.OrdinalIgnoreCase));
        Assert.False(zell.IsBusy);
        BattleCombatant survivor = battle.Combatants.First(IsAvailableEnemy);
        zell.Atb = BattleSession.AtbMaximum;
        int attack = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: ZellId, TargetId: survivor.Id));
        AdvanceUntil(() => battle.Events.Skip(attack).Any(entry => entry is { Kind: "ActionComplete", ActorId: ZellId }));

        bool ReadyForChoice() => battle.GetNativeDuel(ZellId) is { Phase: BattleNativeDuelPhase.Ready, Selection1D28DEF: 254 };

        void PostChoice(BattleNativeDuelChoice choice)
        {
            Assert.Contains(battle.GetNativeDuelChoices(ZellId), current => current.MoveSetId == choice.MoveSetId);
            foreach (ushort button in choice.Buttons)
            {
                battle.AdvanceNativeDuelPresentation(TimeSpan.FromTicks(166667), button);
                battle.AdvanceNativeDuelPresentation(TimeSpan.FromTicks(166667), 0);
            }
            Assert.Equal(choice.MoveSetId, battle.GetNativeDuel(ZellId)!.Selection1D28DEF);
            Assert.DoesNotContain(battle.QueuedActions, command => command.ActorId == ZellId);
        }

        void AssertNoAftercare()
        {
            Assert.DoesNotContain(receipts.OfType<NativeBattlePresentationCompletionRequested>(), receipt => receipt.ActorId == ZellId);
            Assert.DoesNotContain(battle.Events.Skip(start), entry => entry is { Kind: "ActionComplete", ActorId: ZellId });
        }

        void AdvanceUntil(Func<bool> expected)
        {
            for (int callback = 0; callback < CallbackBudget && !expected(); callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                Assert.Same(process, battle.ExpandedPresentationProcess);
                if (process.LastStepActionToken is long token && headers.All(header => header.Token != token) &&
                    child.NativeGraphics is { ActionGraphics: { Header: { } reference } action } graphics && action.ResolveActor(graphics) == owner)
                {
                    NativeBattleActionHeaderSlot header = child.NativeActionPackets.ReadHeader(reference);
                    headers.Add((token, header.Command, header.Action));
                }
                if (process.LastFrameServices is { } frame)
                {
                    receipts.AddRange(frame.ReadOrderedReceipts());
                }
            }
            if (!expected())
            {
                NativeBattleGraphicsState graphics = child.NativeGraphics!;
                NativeBattleGraphicsModel model = graphics.ActorModels.Get(owner);
                string queue = string.Join(", ", graphics.Descriptors.Queue.Select(slot => $"{graphics.Descriptors.Packets[slot].Command}/{graphics.Descriptors.Packets[slot].PriorityState:X2}"));
                string effect = graphics.OriginalEffect is { } current ? $"{current.Binding.Effect}/{current.ReturnedDescriptor:X}" : "none";
                Assert.Fail($"Duel did not reach the expected boundary. Widget={battle.GetNativeDuel(ZellId)?.Phase}; token={process.ActiveActionToken}; descriptor queue={queue}; callback={graphics.ActionGraphics?.Callback:X}; effect={effect}; module tasks={graphics.ModuleEffects.PublishedCount}; caster flags={model.Flags0:X}/{model.ResourceFlags44:X}; sequence={model.Sequence}, frame={model.SequenceFrame}, offset={model.SequenceState?.Offset46:X}; headers={string.Join(",", headers)}.");
            }
        }
    }

    private static BattleNativeDuelChoice[] FindGroupFinisher(BattleDefinition battle, BattleNativeLimitDefinition rules, byte initial)
    {
        Queue<(byte Set, BattleNativeDuelChoice[] Choices)> pending = new();
        pending.Enqueue((initial, []));
        HashSet<byte> visited = [];
        while (pending.TryDequeue(out var branch))
        {
            if (!visited.Add(branch.Set))
            {
                continue;
            }
            foreach (BattleNativeDuelChoice choice in NativeBattleDuelRules.Choices(rules, branch.Set, 0, battle.NativeActions))
            {
                if (choice.Finisher)
                {
                    BattleNativeAction native = Assert.Single(battle.NativeActions, action => action.Command == 239 && action.ActionId == choice.ActionId);
                    if ((native.TargetFlags!.Value & 0x30) == 0)
                    {
                        return [.. branch.Choices, choice];
                    }
                }
                else
                {
                    pending.Enqueue((choice.MoveSetId, [.. branch.Choices, choice]));
                }
            }
        }
        throw new InvalidDataException("The installed Duel moves do not reach a group finisher from this input set.");
    }

    private static bool IsAvailableEnemy(BattleCombatant actor) => !actor.IsPlayer && actor.IsActive && actor.IsAlive && !actor.Untargetable && !actor.IsEjected;
}
