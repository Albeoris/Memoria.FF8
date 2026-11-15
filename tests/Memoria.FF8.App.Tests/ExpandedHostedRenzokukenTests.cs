using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedHostedRenzokukenTests
{
    private const string Squall = "party-0";
    private const int CallbackBudget = 3200;

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 7)]
    public void Installed_Renzokuken_uses_its_attached_widget_and_retires_every_trigger_once(bool automatic, byte seed)
    {
        RunWorld(automatic, seed, (world, battle, process, caster, target) =>
        {
            int first = battle.Events.Count;
            BattleActionPresentation? mirror = null;
            List<NativeBattlePresentationContactReceived> contacts = [];
            List<NativeBattlePresentationCompletionRequested> completions = [];
            HashSet<long> observedFrames = [];
            Dictionary<long, NativeBattleActionHeaderSlot> headers = [];
            BattleNativeRenzokukenState? admitted = null;
            NativeBattleTriggerWidget? lastWidget = null;
            int highestIndex = 0;
            int manualPresses = 0;
            NativeBattleUiRandomState? beforeUi = battle.Capture().NativeBattleUiRandom;
            byte crisis = PrepareSelection(battle, caster);
            NativeBattleRandomState random = Assert.IsType<NativeBattleRandomState>(battle.Capture().Random);
            NativeBattleRandomState forecast = random.Draw(out byte magicRoll).Draw(out byte finisherRoll);
            ushort expectedMagic = target.NativeRenzokukenMagicIds[crisis + magicRoll % 2 + 1];
            byte? expectedFinisher = null;
            if (NativeBattleRenzokukenRules.HasFinisher(crisis, finisherRoll))
            {
                _ = forecast.Draw(out byte selection);
                expectedFinisher = NativeBattleRenzokukenRules.Finisher(caster.NativePlayerStatRules!.WeaponRecord[2], selection);
            }
            Queue(world, target);
            for (int callback = 0; callback < CallbackBudget; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                Assert.Same(process, battle.ExpandedPresentationProcess);
                if (process.ActiveActionToken is long token && process.ActionPackets.ActiveHeader is { } header)
                {
                    headers.TryAdd(token, process.ActionPackets.ReadHeader(header));
                }
                if (observedFrames.Add(process.Graphics.Callback) && process.LastFrameServices is { } frame)
                {
                    contacts.AddRange(frame.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>());
                    completions.AddRange(frame.ReadOrderedReceipts().OfType<NativeBattlePresentationCompletionRequested>());
                }
                if (battle.GetNativeRenzokuken(Squall) is { } current)
                {
                    admitted ??= current;
                    mirror ??= Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
                    Assert.Same(mirror, battle.ActiveAction);
                    Assert.Equal(expectedMagic, current.MagicId);
                    Assert.Equal(expectedFinisher, current.FinisherActionId);
                    highestIndex = Math.Max(highestIndex, current.Input.ContactIndex);
                    if (process.TriggerWidget is { } widget)
                    {
                        lastWidget = widget;
                        Assert.Equal(widget.Input, current.Input);
                        Assert.Equal(widget.PendingTrigger, current.PendingTrigger);
                        Assert.Equal(expectedMagic, widget.SourceMagicId);
                        battle.AdvanceNativeRenzokukenPresentation(TimeSpan.FromSeconds(1));
                        Assert.Same(current, battle.GetNativeRenzokuken(Squall));
                        Assert.Same(widget, process.TriggerWidget);
                        if (!automatic && battle.NativeRenzokukenTriggerWindowOpen)
                        {
                            Assert.True(battle.TriggerNativeRenzokuken());
                            Assert.False(battle.TriggerNativeRenzokuken());
                            manualPresses++;
                        }
                    }
                }
                else if (admitted is not null && battle.ActiveAction is null && process.ActiveActionToken is null)
                {
                    break;
                }
            }
            Assert.NotNull(admitted);
            Assert.NotNull(lastWidget);
            Assert.True(battle.ActiveAction is null && process.ActiveActionToken is null && battle.GetNativeRenzokuken(Squall) is null,
                $"Renzokuken {expectedMagic} finisher {expectedFinisher} did not drain at callback {process.Graphics.Callback}, trigger {highestIndex}.");
            NativeBattlePresentationContactReceived[] triggers = contacts.Where(contact => contact.Renzokuken is not null).ToArray();
            int count = lastWidget.Block.Timeline.InputThresholds.Length;
            Assert.Equal(count, highestIndex);
            Assert.Equal(Enumerable.Range(1, count), triggers.Select(contact => (int)contact.Renzokuken!.Index));
            BattleModelOwner recipient = battle.ReadPresentationModelOwner(target.Id);
            BattleModelOwner owner = battle.ReadPresentationModelOwner(caster.Id);
            Assert.All(triggers, contact =>
            {
                Assert.Equal(recipient, contact.Packet.Primary.Recipient);
                Assert.Equal(owner, contact.Renzokuken!.Owner.Actor);
            });
            long actionToken = Assert.Single(triggers.Select(contact => contact.Renzokuken!.Token).Distinct());
            Assert.All(triggers, contact => Assert.Equal(triggers[0].Packet.Occurrence, contact.Packet.Occurrence));
            Assert.Equal((byte)5, headers[actionToken].Command);
            Assert.Equal((byte)21, headers[actionToken].Sequence);
            Assert.Equal(expectedFinisher.HasValue ? (ushort)65534 : (ushort)65533, headers[actionToken].Action);
            Assert.Equal(expectedFinisher.HasValue, headers.Values.Any(header => header.Command == 250));
            Assert.Equal(automatic, !Equals(beforeUi, battle.Capture().NativeBattleUiRandom));
            Assert.Equal(!automatic, manualPresses > 0);
            Assert.Single(completions, receipt => receipt.Callback == 0x47DE70 && receipt.ActorId == Squall);
            Assert.Single(battle.Events.Skip(first), entry => entry.Kind == "ActionComplete" && entry.ActorId == Squall);
            Assert.Null(battle.Capture().NativeTimedAction);
            Assert.Null(process.TriggerWidget);
            Assert.False(caster.IsBusy);

            if (battle.Combatants.FirstOrDefault(IsAvailableEnemy) is { } survivor)
            {
                caster.Atb = BattleSession.AtbMaximum;
                int start = battle.Events.Count;
                world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: Squall, TargetId: survivor.Id));
                for (int frame = 0; frame < CallbackBudget && !battle.Events.Skip(start).Any(entry => entry is { Kind: "ActionComplete", ActorId: Squall }); frame++)
                {
                    world.ApplyBattleInput(new(BattleInputKind.Tick));
                }
                Assert.Single(battle.Events.Skip(start), entry => entry is { Kind: "ActionComplete", ActorId: Squall });
                Assert.Same(process, battle.ExpandedPresentationProcess);
            }
            else
            {
                for (int frame = 0; frame < CallbackBudget && battle.Outcome == BattleOutcome.Active; frame++)
                {
                    world.ApplyBattleInput(new(BattleInputKind.Tick));
                }
                Assert.Equal(BattleOutcome.Victory, battle.Outcome);
            }
        });
    }

    [Fact]
    public void Initial_Renzokuken_resource_failure_preserves_live_admission()
    {
        RunWorld(false, 4, (world, battle, process, caster, target) =>
        {
            _ = PrepareSelection(battle, caster);
            Queue(world, target);
            BattleAttackCommand head = Assert.Single(battle.QueuedActions);
            BattleSnapshot before = battle.Capture();
            NativeBattleActionPacketsState packets = process.ActionPackets;
            NativeBattleDescriptorState descriptors = process.Graphics.Descriptors;
            int[] hp = battle.Combatants.Select(actor => actor.CurrentHp).ToArray();
            const string reason = "Installed Renzokuken resource could not be opened.";
            battle.AttachNativeEffectResourceResolver(_ => throw new InvalidDataException(reason));

            InvalidDataException failure = Assert.Throws<InvalidDataException>(() => world.ApplyBattleInput(new(BattleInputKind.Tick)));

            Assert.Equal(reason, failure.Message);
            Assert.Same(head, Assert.Single(battle.QueuedActions));
            Assert.True(caster.IsBusy);
            Assert.Null(battle.ActiveAction);
            Assert.Null(battle.GetNativeRenzokuken(Squall));
            Assert.Null(battle.Capture().NativeTimedAction);
            Assert.Equal(before.Random, battle.Capture().Random);
            Assert.Equal(hp, battle.Combatants.Select(actor => actor.CurrentHp));
            Assert.Same(descriptors, process.Graphics.Descriptors);
            Assert.Same(packets, process.ActionPackets);
            Assert.Null(process.ActiveActionToken);
        });
    }

    private static byte PrepareSelection(BattleSession battle, BattleCombatant caster)
    {
        caster.Atb = BattleSession.AtbMaximum;
        byte crisis = battle.RefreshNativeLimit(Squall);
        Assert.InRange(crisis, 1, 4);
        caster.CurrentHp = caster.MaximumHp;
        return crisis;
    }

    private static void Queue(GameWorld world, BattleCombatant target)
    {
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: Squall, TargetId: target.Id, Ability: BattleAbility.LimitBreak, ActionId: 5 << 16));
    }

    private static bool IsAvailableEnemy(BattleCombatant actor) => actor is { IsPlayer: false, IsActive: true, IsAlive: true, Untargetable: false };

    private static void RunWorld(bool automatic, byte seed, Action<GameWorld, BattleSession, ExpandedBattlePresentationProcess, BattleCombatant, BattleCombatant> run)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()) with { GlobalRandom = new(917) });
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        GameWorld world = new(session, directory, sharedAssets: game);
        try
        {
            ExpandedBattleInstalledEntry.Prepare(world, session, current =>
            {
                NativeCharacterVitals vitals = (current.State.CharacterVitals! with { CombatParty = [1, 2, 3] }).WithGuestBattleRecords().Rest();
                NativeCharacterVital[] records = vitals.Records.ToArray();
                foreach (int ally in new[] { 1, 2, 3 })
                {
                    records[ally] = records[ally] with { MemberFlags = (ushort)(records[ally].MemberFlags | 1), HitPoints = new(0, 0), Status = 1 };
                }
                records[0] = records[0] with { MemberFlags = (ushort)(records[0].MemberFlags | 1), HitPoints = new(1, 1), Status = 0 };
                current.SetCharacterVitals((vitals with { Records = records }).WithBattleParty([1, 2, 3, 0], 4));
                NativeLimitProgress limits = current.State.LimitProgress!;
                ushort options = Assert.IsType<ushort>(limits.Options978);
                current.SetLimitProgress(limits with { Options978 = (ushort)((options & ~0x8001) | (automatic ? 1 : 0)) });
            });
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: seed));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            ExpandedBattlePresentationProcess process = Assert.IsType<ExpandedBattlePresentationProcess>(battle.ExpandedPresentationProcess);
            for (int frame = 0; frame < CallbackBudget && (!process.EntryReady || process.ActiveActionToken is not null || battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
            }
            Assert.True(process.EntryReady);
            Assert.Null(battle.ActiveAction);
            Assert.Empty(battle.QueuedActions);
            BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == Squall);
            BattleCombatant target = battle.Combatants.Last(actor => IsAvailableEnemy(actor) && actor.Slot is { Position: >= 4 });
            Assert.False(process.ActionPackets.ModelAliases.TryEncodeExact(battle.ReadPresentationModelOwner(caster.Id), out _));
            Assert.False(process.ActionPackets.ModelAliases.TryEncodeExact(battle.ReadPresentationModelOwner(target.Id), out _));
            run(world, battle, process, caster, target);
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
}
