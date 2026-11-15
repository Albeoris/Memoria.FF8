using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedHostedRenzokukenTests
{
    private const string Squall = "party-0";
    private const int CallbackBudget = 3200;
    private const int CrisisSelectionBudget = 64;

    [Theory]
    [InlineData(159, 92, 9)]
    [InlineData(161, 131, 738)]
    [InlineData(335, 77, 63)]
    [InlineData(337, 117, 813)]
    [InlineData(339, 112, 796)]
    [InlineData(340, 126, 982)]
    public void Installed_special_target_Renzokuken_rebinds_and_releases_its_actual_models(ushort magic, int targetModel, ushort formation)
    {
        RunWorld(false, ExpandedBattleInstalledEntry.Seed, (world, battle, process, caster, target) =>
        {
            BattleModelOwner source = battle.ReadPresentationModelOwner(caster.Id);
            BattleModelOwner recipient = battle.ReadPresentationModelOwner(target.Id);
            BattleModelOwner? companion = null;
            if (magic == 339)
            {
                Assert.True(process.ActionPackets.ModelAliases.TryEncodeExact(recipient, out byte original));
                companion = process.ActionPackets.ModelAliases.DecodeLiteral(checked((byte)(original + 1)));
                Assert.NotEqual(recipient, companion);
                Assert.Contains(battle.ReadDefinition().Combatants, actor => actor.Id == ((BattleEnemyActorModel)companion).Actor.ActorId && actor.InitialActive);
            }
            if (magic == 337)
            {
                NativeBattleEnemyAllocation generation = Assert.Single(battle.Capture().NativeAllocations, allocation => allocation.Id == target.Id);
                Assert.Equal(target.SceneSlot, generation.SceneSlot);
                Assert.Equal(recipient, new BattleEnemyActorModel(new(generation.Id)));
            }
            (int? Source, int? Target) originalResources = (process.Graphics.ActorModels.Get(source).OriginalResourceRow104, process.Graphics.ActorModels.Get(recipient).OriginalResourceRow104);
            Assert.Equal(magic, PrepareSelection(battle, caster, target, null, magic));
            int initialEvent = battle.Events.Count;
            bool boundSource = false;
            bool boundTarget = false;
            bool boundCompanion = false;
            bool targetSelectedBeforeBinding = false;
            long observedCallback = process.Graphics.Callback;
            List<NativeBattlePresentationContactReceived> contacts = [];
            List<NativeBattlePresentationCompletionRequested> completions = [];
            Queue(world, target);
            for (int tick = 0; tick < CallbackBudget; tick++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                Assert.Same(process, battle.ExpandedPresentationProcess);
                NativeBattleGraphicsModel targetGraphics = process.Graphics.ActorModels.Get(recipient);
                boundSource |= process.Graphics.ActorModels.Get(source) is { ResourceRenzokukenTargetBank: null } actor && actor.ResourceMagicId == magic;
                if (targetGraphics.ResourceMagicId == magic)
                {
                    Assert.Equal((byte?)0, targetGraphics.ResourceRenzokukenTargetBank);
                    boundTarget |= targetGraphics is { Sequence: 1, SequenceState: not null };
                }
                else if (magic is 339 or 340 && !boundTarget && targetGraphics.Sequence == 1 &&
                    process.Graphics.OriginalEffect is { Parameters: { } parameters } selected && selected.Binding.Effect == magic &&
                    parameters.Source == source && selected.EntryFirstRecipient == recipient &&
                    process.ActionPackets.ReadHeader(parameters.Header) is { Command: 5 } admitted && admitted.Effect == magic)
                {
                    uint controller = magic == 339 ? NativeBattleRenzokukenSpecialProfiles.Renzokuken339.ControllerCallback : NativeBattleRenzokukenSpecialProfiles.Renzokuken340.ControllerCallback;
                    targetSelectedBeforeBinding |= selected.Pools.SelectMany(pool => pool.Slots).Any(task => (task.Flags & 1) != 0 && task.Callback == controller && task.ReadWord(12) == 2);
                }
                if (companion is not null && process.Graphics.ActorModels.Get(companion) is { } other && other.ResourceMagicId == magic)
                {
                    Assert.Equal((byte?)1, other.ResourceRenzokukenTargetBank);
                    boundCompanion = true;
                }
                if (process.Graphics.Callback != observedCallback && process.LastFrameServices is { } frame)
                {
                    observedCallback = process.Graphics.Callback;
                    contacts.AddRange(frame.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>());
                    completions.AddRange(frame.ReadOrderedReceipts().OfType<NativeBattlePresentationCompletionRequested>());
                }
                if (battle.GetNativeRenzokuken(caster.Id) is { } widget)
                {
                    Assert.Equal(magic, widget.MagicId);
                    Assert.Null(widget.FinisherActionId);
                }
                if (boundSource && IsIdle(battle, process))
                {
                    break;
                }
            }
            Assert.True(boundSource && boundTarget && IsIdle(battle, process), $"Renzokuken {magic} did not bind and retire both owners at callback {process.Graphics.Callback}.");
            if (magic is 339 or 340)
            {
                Assert.True(targetSelectedBeforeBinding);
            }
            Assert.Equal(companion is not null, boundCompanion);
            int count = battle.ReadDefinition().NativeRenzokukenResources.Single(resource => resource.MagicId == magic).Timeline.InputThresholds.Length;
            NativeBattlePresentationContactReceived[] triggers = contacts.Where(contact => contact.Renzokuken is not null).ToArray();
            Assert.Equal(Enumerable.Range(1, count), triggers.Select(contact => (int)contact.Renzokuken!.Index));
            Assert.All(triggers, contact =>
            {
                Assert.Equal(source, contact.Renzokuken!.Owner.Actor);
                Assert.Equal(recipient, contact.Packet.Primary.Recipient);
            });
            Assert.Single(completions, receipt => receipt.Callback == 0x47DE70 && receipt.Owner == source);
            Assert.Single(battle.Events.Skip(initialEvent), entry => entry is { Kind: "ActionComplete", ActorId: Squall });
            Assert.Null(battle.Capture().NativeTimedAction);
            Assert.Null(battle.GetNativeRenzokuken(caster.Id));
            Assert.Null(process.TriggerWidget);
            VerifyRestoredModel(source, originalResources.Source);
            VerifyRestoredModel(recipient, originalResources.Target);
            if (companion is not null)
            {
                Assert.Null(process.Graphics.ActorModels.Get(companion).ResourceRenzokukenTargetBank);
            }
            if (magic == 337)
            {
                VerifyDefeatContinuation(world, battle, process, caster, target, initialEvent);
            }
            else
            {
                BattleCombatant next = battle.Combatants.First(IsAvailableEnemy);
                AttackAndDrain(world, battle, process, caster, next);
                Assert.Same(process, battle.ExpandedPresentationProcess);
            }

            void VerifyRestoredModel(BattleModelOwner owner, int? original)
            {
                NativeBattleGraphicsModel actual = process.Graphics.ActorModels.Get(owner);
                Assert.Null(actual.ResourceMagicId);
                Assert.Null(actual.ResourceRenzokukenTargetBank);
                Assert.Equal(original, actual.ActiveResourceRow100);
            }
        }, formation: formation, targetModel: targetModel);
    }

    [Theory]
    [InlineData(false, -1, true)]
    [InlineData(true, 0, false)]
    [InlineData(false, 1, false)]
    [InlineData(true, 3, false)]
    public void Installed_Renzokuken_uses_its_attached_widget_and_retires_every_trigger_once(bool automatic, int finisherIndex, bool verifyApproachAndPoison)
    {
        byte? requiredFinisher = finisherIndex < 0 ? null : checked((byte)finisherIndex);
        RunWorld(automatic, 4, (world, battle, process, caster, target) =>
        {
            BattleModelOwner owner = battle.ReadPresentationModelOwner(caster.Id);
            BattleModelOwner recipient = battle.ReadPresentationModelOwner(target.Id);
            if (verifyApproachAndPoison)
            {
                BattleCombatant attackTarget = battle.Combatants.Where(enemy => IsAvailableEnemy(enemy) && enemy != target).MaxBy(enemy => enemy.CurrentHp)!;
                VerifyApproachingAttack(world, battle, process, caster, attackTarget);
                caster.NativeStatus0 |= 2;
            }

            int first = battle.Events.Count;
            BattleActionPresentation? mirror = null;
            List<NativeBattlePresentationReceipt> ordered = [];
            HashSet<long> observedFrames = [];
            BattleNativeRenzokukenState? admitted = null;
            NativeBattleActionHeaderSlot? openingHeader = null;
            NativeBattleRenzokukenWidgetStepped? unregisterReplay = null;
            (NativeBattleTriggerWidget Widget, HostedRenzokukenFrame Frame)? closing = null;
            int highestIndex = 0;
            int manualPresses = 0;
            bool replayedContact = false;
            bool approached = false;
            double initialDistance = verifyApproachAndPoison ? DistanceTo(process, owner, recipient) : 0;
            HashSet<BattleModelOwner> livingEnemies = battle.Combatants.Where(IsAvailableEnemy).Select(enemy => battle.ReadPresentationModelOwner(enemy.Id)).ToHashSet();
            NativeBattleUiRandomState? beforeUi = battle.Capture().NativeBattleUiRandom;
            ushort expectedMagic = PrepareSelection(battle, caster, target, requiredFinisher);
            Queue(world, target);
            for (int callback = 0; callback < CallbackBudget; callback++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                Assert.Same(process, battle.ExpandedPresentationProcess);
                if (openingHeader is null && process.ActionPackets.ActiveHeader is { } active)
                {
                    NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(active);
                    if (header is { Command: 5 } && header.Source == owner)
                    {
                        openingHeader = header;
                    }
                }
                NativeBattleModelLifecycleServices? frame = process.LastFrameServices;
                if (observedFrames.Add(process.Graphics.Callback) && frame is not null)
                {
                    ordered.AddRange(frame.ReadOrderedReceipts());
                    if (frame.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().FirstOrDefault(receipt => receipt.Renzokuken is not null) is { } trigger)
                    {
                        if (verifyApproachAndPoison && !replayedContact)
                        {
                            Assert.True(approached && DistanceTo(process, owner, recipient) < initialDistance, "Renzokuken must approach its selected target before its first contact.");
                            AssertRejectedContactReplay(battle, frame, trigger);
                            replayedContact = true;
                        }
                    }
                }
                if (battle.GetNativeRenzokuken(Squall) is { } current)
                {
                    admitted ??= current;
                    mirror ??= Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
                    Assert.Same(mirror, battle.ActiveAction);
                    Assert.Equal(expectedMagic, current.MagicId);
                    Assert.Equal(requiredFinisher, current.FinisherActionId);
                    highestIndex = Math.Max(highestIndex, current.Input.ContactIndex);
                    if (verifyApproachAndPoison && HasDisplacement(process.Graphics.ActorModels.Get(owner)))
                    {
                        approached |= DistanceTo(process, owner, recipient) < initialDistance;
                    }
                    if (process.TriggerWidget is { SourceMagicId: ushort widgetMagic } widget)
                    {
                        Assert.Equal(owner, widget.Actor);
                        Assert.Equal(expectedMagic, widgetMagic);
                        Assert.Equal(widget.Input, current.Input);
                        Assert.Equal(widget.PendingTrigger, current.PendingTrigger);
                        battle.AdvanceNativeRenzokukenPresentation(TimeSpan.FromSeconds(1));
                        Assert.Same(current, battle.GetNativeRenzokuken(Squall));
                        Assert.Same(widget, process.TriggerWidget);
                        if (verifyApproachAndPoison && widget.ClosingPhase is 10 or 12 && process.ActiveActionToken is long token &&
                            ordered.OfType<NativeBattlePresentationContactReceived>().LastOrDefault(receipt => receipt.Renzokuken is not null)?.Renzokuken is { } hit)
                        {
                            closing = (widget, new(hit.Owner, token, current, battle.Capture().NativeTimedAction!.Contacts));
                        }
                        if (!automatic && battle.NativeRenzokukenTriggerWindowOpen)
                        {
                            Assert.True(battle.TriggerNativeRenzokuken());
                            Assert.False(battle.TriggerNativeRenzokuken());
                            manualPresses++;
                        }
                    }
                    else if (process.TriggerWidget is not null)
                    {
                        Assert.False(battle.CanTriggerNativeRenzokuken);
                        Assert.False(battle.NativeRenzokukenBarVisible);
                    }
                    else if (closing is { } last && unregisterReplay is null && process.ActiveActionToken == last.Frame.ActionToken)
                    {
                        NativeBattleTriggerWidget final = ForecastUnregister(last.Widget, process.Graphics.ActorModels.Roster);
                        Assert.Equal(final.Input, current.Input);
                        unregisterReplay = new(last.Frame, final, null, true, 0, 0);
                        Assert.Throws<InvalidDataException>(() => battle.ApplyExpandedHostedRenzokukenWidgetSteps([unregisterReplay], 0));
                        Assert.Same(current, battle.GetNativeRenzokuken(Squall));
                    }
                }
                else if (admitted is not null && battle.ActiveAction is null && process.ActiveActionToken is null)
                {
                    break;
                }
            }
            Assert.NotNull(admitted);
            Assert.NotNull(openingHeader);
            Assert.True(battle.ActiveAction is null && process.ActiveActionToken is null && battle.GetNativeRenzokuken(Squall) is null,
                $"Renzokuken {expectedMagic} finisher {requiredFinisher} did not drain at callback {process.Graphics.Callback}, trigger {highestIndex}.");
            NativeBattlePresentationContactReceived[] triggers = ordered.OfType<NativeBattlePresentationContactReceived>().Where(contact => contact.Renzokuken is not null).ToArray();
            int count = Assert.Single(battle.ReadDefinition().NativeRenzokukenResources, resource => resource.MagicId == expectedMagic).Timeline.InputThresholds.Length;
            Assert.Equal(count, highestIndex);
            Assert.Equal(Enumerable.Range(1, count), triggers.Select(contact => (int)contact.Renzokuken!.Index));
            Assert.All(triggers, contact =>
            {
                Assert.Equal(recipient, contact.Packet.Primary.Recipient);
                Assert.Equal(owner, contact.Renzokuken!.Owner.Actor);
            });
            Assert.Single(triggers.Select(contact => contact.Renzokuken!.Token).Distinct());
            Assert.All(triggers, contact => Assert.Equal(triggers[0].Packet.Occurrence, contact.Packet.Occurrence));
            Assert.Equal((byte)5, openingHeader.Command);
            Assert.Equal((byte)21, openingHeader.Sequence);
            Assert.Equal(requiredFinisher.HasValue ? (ushort)65534 : (ushort)65533, openingHeader.Action);
            AssertFinisher(battle, process, ordered, triggers[^1], requiredFinisher, recipient, livingEnemies);
            if (automatic)
            {
                NativeBattleUiRandomState expected = Assert.IsType<NativeBattleUiRandomState>(beforeUi);
                for (int i = 0; i < count; i++)
                {
                    expected = expected.Draw(out _);
                }
                Assert.Equal(expected, battle.Capture().NativeBattleUiRandom);
            }
            else
            {
                Assert.Equal(beforeUi, battle.Capture().NativeBattleUiRandom);
            }
            Assert.Equal(!automatic, manualPresses > 0);
            uint[] expectedCallbacks = verifyApproachAndPoison ? [0x47DE70, 0x47E120] : [0x47DE70];
            Assert.Equal(expectedCallbacks, ordered.OfType<NativeBattlePresentationCompletionRequested>().Where(receipt => receipt.ActorId == Squall).Select(receipt => receipt.Callback));
            if (verifyApproachAndPoison)
            {
                Assert.True(replayedContact);
                Assert.NotNull(unregisterReplay);
                Assert.False(HasDisplacement(process.Graphics.ActorModels.Get(owner)));
                Assert.Equal(initialDistance, DistanceTo(process, owner, recipient));
                Assert.Single(battle.Events.Skip(first), entry => entry is { Kind: "EffectDamage", TargetId: Squall } && entry.Message.Contains("Poison", StringComparison.OrdinalIgnoreCase));
            }
            Assert.Single(battle.Events.Skip(first), entry => entry.Kind == "ActionComplete" && entry.ActorId == Squall);
            Assert.Null(battle.Capture().NativeTimedAction);
            Assert.Null(process.TriggerWidget);
            Assert.False(caster.IsBusy);

            if (battle.Combatants.FirstOrDefault(IsAvailableEnemy) is { } survivor)
            {
                caster.Atb = BattleSession.AtbMaximum;
                int start = battle.Events.Count;
                world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: Squall, TargetId: survivor.Id));
                if (unregisterReplay is not null)
                {
                    Assert.Throws<InvalidDataException>(() => battle.ApplyExpandedHostedRenzokukenWidgetSteps([unregisterReplay], 0));
                }
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
        }, requiredFinisher);
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

    private static ushort PrepareSelection(BattleSession battle, BattleCombatant caster, BattleCombatant target, byte? requiredFinisher, ushort? requiredMagic = null)
    {
        caster.Atb = BattleSession.AtbMaximum;
        for (int attempt = 0; attempt < CrisisSelectionBudget; attempt++)
        {
            byte crisis = battle.RefreshNativeLimit(Squall);
            if (crisis == 0)
            {
                continue;
            }
            NativeBattleRandomState random = Assert.IsType<NativeBattleRandomState>(battle.Capture().Random);
            NativeBattleRandomState forecast = random.Draw(out byte magic).Draw(out byte gate);
            byte? finisher = null;
            if (NativeBattleRenzokukenRules.HasFinisher(crisis, gate))
            {
                _ = forecast.Draw(out byte selection);
                finisher = NativeBattleRenzokukenRules.Finisher(caster.NativePlayerStatRules!.WeaponRecord[2], selection);
            }
            ushort selected = target.NativeRenzokukenMagicIds[crisis + magic % 2 + 1];
            if (finisher == requiredFinisher && (requiredMagic is null || selected == requiredMagic))
            {
                caster.CurrentHp = caster.MaximumHp;
                return selected;
            }
        }
        throw new InvalidDataException($"The installed weapon {caster.WeaponId} did not select resource {requiredMagic?.ToString() ?? "any"} with finisher {requiredFinisher?.ToString() ?? "none"} within {CrisisSelectionBudget} native Limit selections.");
    }

    private static void AssertFinisher(BattleSession battle, ExpandedBattlePresentationProcess process, List<NativeBattlePresentationReceipt> ordered,
        NativeBattlePresentationContactReceived lastTrigger, byte? expectedFinisher, BattleModelOwner target, HashSet<BattleModelOwner> livingEnemies)
    {
        NativeBattleRenzokukenFinisherReached[] reached = ordered.OfType<NativeBattleRenzokukenFinisherReached>().ToArray();
        if (expectedFinisher is not byte index)
        {
            Assert.Empty(reached);
            return;
        }
        NativeBattleRenzokukenFinisherReached formula = Assert.Single(reached);
        Assert.Same(lastTrigger.Renzokuken!.Owner, formula.EventOwner);
        Assert.Equal(index == 3 ? 0x48F350u : 0x48F3F0u, formula.EventOwner.FinisherCallback);
        Assert.True(ordered.IndexOf(lastTrigger) < ordered.IndexOf(formula));
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(formula.Header);
        Assert.Equal((byte)250, header.Command);
        Assert.Equal(ushort.MaxValue, header.Action);
        Assert.Equal((ushort)index, header.Data!.Argument);
        BattleNativeAction native = Assert.Single(battle.ReadDefinition().NativeActions, action => action.Command == 249 && action.ActionId == index);
        Assert.Equal(native.MagicId, header.Effect);
        bool group = (native.TargetFlags!.Value & 0x10) == 0;
        Assert.Equal(index == 1, group);
        NativeBattleContactPacket[] expected = process.ActionPackets.ReadHeaderContacts(formula.Header).ToArray();
        Assert.Equal(native.HitCount * (group ? livingEnemies.Count : 1), expected.Length);
        Assert.True(expected.Select(packet => packet.Primary.Recipient).ToHashSet().SetEquals(group ? livingEnemies : [target]));
        NativeBattlePresentationContactReceived[] delivered = ordered.Skip(ordered.IndexOf(formula) + 1).OfType<NativeBattlePresentationContactReceived>()
            .Where(receipt => receipt.Renzokuken is null && expected.Any(packet => packet.Occurrence == receipt.Packet.Occurrence)).ToArray();
        Assert.Equal(expected.Length, delivered.Length);
        Assert.Equal(expected.Length, delivered.Select(receipt => receipt.Packet.Occurrence).Distinct().Count());
        Assert.All(delivered, receipt => Assert.Equal(expected.Single(packet => packet.Occurrence == receipt.Packet.Occurrence).Primary.Recipient, receipt.Packet.Primary.Recipient));
    }

    private static void VerifyApproachingAttack(GameWorld world, BattleSession battle, ExpandedBattlePresentationProcess process, BattleCombatant caster, BattleCombatant target)
    {
        BattleModelOwner source = battle.ReadPresentationModelOwner(caster.Id);
        BattleModelOwner recipient = battle.ReadPresentationModelOwner(target.Id);
        caster.Atb = BattleSession.AtbMaximum;
        double before = DistanceTo(process, source, recipient);
        int begin = battle.Events.Count;
        bool reached = false;
        bool approaching = false;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: caster.Id, TargetId: target.Id));
        for (int frame = 0; frame < CallbackBudget; frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            NativeBattleGraphicsModel model = process.Graphics.ActorModels.Get(source);
            double distance = DistanceTo(process, source, recipient);
            if (HasDisplacement(model) && distance < before)
            {
                approaching = true;
            }
            if (!reached && process.LastFrameServices?.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().Any(contact => contact.Packet.Primary.Recipient == recipient) == true)
            {
                Assert.True(approaching && distance < before, "The physical Attack must approach its selected target at impact.");
                reached = true;
            }
            if (battle.Events.Skip(begin).Any(entry => entry is { Kind: "ActionComplete", ActorId: Squall }) && !HasDisplacement(model) && distance == before)
            {
                Assert.True(reached);
                Assert.Same(process, battle.ExpandedPresentationProcess);
                return;
            }
        }
        Assert.Fail("The preceding installed physical Attack did not return through its original displacement sequence.");
    }

    private static bool HasDisplacement(NativeBattleGraphicsModel model)
    {
        return model.SequenceState is { Header36: not 0 } or { Header38: not 0 } or { Header40: not 0 };
    }

    private static double DistanceTo(ExpandedBattlePresentationProcess process, BattleModelOwner source, BattleModelOwner recipient)
    {
        NativeBattleActionMatrix matrix = Assert.IsType<NativeBattleActionMatrix>(process.Graphics.ActorModels.Get(source).GeometryWorldMatrix);
        NativeBattleVector destination = process.Graphics.ActorModels.Get(recipient).Transform!.Position28;
        return Math.Pow(matrix.X - destination.X, 2) + Math.Pow(matrix.Z - destination.Z, 2);
    }

    private static NativeBattleTriggerWidget ForecastUnregister(NativeBattleTriggerWidget widget, BattleModelRoster roster)
    {
        // The last visible closing phases no longer consume input time or RNG.
        for (int tick = 0; tick < 28; tick++)
        {
            NativeBattleTriggerWidgetStep step = NativeBattleTriggerWidgetRules.Advance(widget, frame33: (tick & 3) == 0, automatic: false, random: null, roster);
            if (step.Unregistered)
            {
                return step.Widget;
            }
            widget = step.Widget;
        }
        throw new InvalidDataException("The original closing widget did not reach unregister.");
    }

    private static void AssertRejectedContactReplay(BattleSession battle, NativeBattleModelLifecycleServices actual, NativeBattlePresentationContactReceived trigger)
    {
        int before = battle.Events.Count;
        NativeBattleRandomState? random = battle.Capture().Random;
        NativeBattleModelLifecycleServices replay = new(actual.Facts);
        replay.EmitContact(trigger.Packet, trigger.Renzokuken);
        MethodInfo callback = typeof(BattleSession).GetMethod("ApplyExpandedHostedFrameReceipts", BindingFlags.Instance | BindingFlags.NonPublic)!;
        TargetInvocationException rejection = Assert.Throws<TargetInvocationException>(() => callback.Invoke(battle, [replay, trigger.Renzokuken!.Token]));
        Assert.True(rejection.InnerException is InvalidDataException or NotSupportedException);
        Assert.Equal(before, battle.Events.Count);
        Assert.Equal(random, battle.Capture().Random);
    }

    private static void Queue(GameWorld world, BattleCombatant target)
    {
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: Squall, TargetId: target.Id, Ability: BattleAbility.LimitBreak, ActionId: 5 << 16));
    }

    private static bool IsAvailableEnemy(BattleCombatant actor) => actor is { IsPlayer: false, IsActive: true, IsAlive: true, Untargetable: false };

    private static bool IsIdle(BattleSession battle, ExpandedBattlePresentationProcess process)
    {
        return battle.ActiveAction is null && battle.QueuedActions.Count == 0 && process.ActiveActionToken is null && process.IsControlPrefixRetired;
    }

    private static void VerifyDefeatContinuation(GameWorld world, BattleSession battle, ExpandedBattlePresentationProcess process, BattleCombatant caster, BattleCombatant attacker, int initialEvent)
    {
        Assert.Equal("native-allocation-10", attacker.Id);
        BattleEvent damage = Assert.Single(battle.Events.Skip(initialEvent), entry => entry.Kind == "Damage" && entry.ActorId == attacker.Id && entry.TargetId == caster.Id);
        Assert.True(damage.Amount > 0);
        Assert.Single(battle.Events.Skip(initialEvent), entry => entry.Kind == "Defeated" && entry.ActorId == attacker.Id && entry.TargetId == caster.Id);
        Assert.Equal(0, caster.CurrentHp);
        Assert.False(caster.IsAlive);

        bool sawFade = false;
        for (int callback = 0; callback < CallbackBudget && world.Battle is not null; callback++)
        {
            Assert.Same(process, battle.ExpandedPresentationProcess);
            sawFade |= process.Graphics.NativeOutcome?.FadeDurationB0C is > 0 and < 255;
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(sawFade);
        Assert.Equal(BattleOutcome.Defeat, battle.Outcome);
        Assert.Equal((byte)1, process.Graphics.NativeOutcome?.ResultByte6E7);
        Assert.Equal((byte)0, process.Graphics.NativeOutcome?.FadeDurationB0C);
        Assert.Single(battle.Events.Skip(initialEvent), entry => entry.Kind == "Outcome");
        Assert.Null(world.Battle);
        Assert.Equal(75, world.Field?.Id);
        BattleRewardSummary rewards = Assert.IsType<BattleRewardSummary>(world.LastBattleRewards);
        Assert.Empty(rewards.CharacterExperience);
        Assert.Empty(rewards.GuardianForceExperience);
        Assert.Empty(rewards.GuardianForceAbilityPoints);
        Assert.Empty(rewards.Items);
        Assert.Empty(rewards.Cards);
    }

    private static void AttackAndDrain(GameWorld world, BattleSession battle, ExpandedBattlePresentationProcess process, BattleCombatant caster, BattleCombatant target)
    {
        caster.Atb = BattleSession.AtbMaximum;
        int first = battle.Events.Count;
        world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: caster.Id, TargetId: target.Id));
        for (int callback = 0; callback < CallbackBudget && !IsIdle(battle, process); callback++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
        }
        Assert.True(IsIdle(battle, process));
        Assert.Single(battle.Events.Skip(first), entry => entry is { Kind: "ActionComplete", ActorId: Squall });
    }

    private static BattleCombatant SelectSpecialTarget(GameWorld world, BattleSession battle, ExpandedBattlePresentationProcess process, BattleCombatant caster, ushort formation, int targetModel)
    {
        if (formation == 63)
        {
            BattleCombatant carrier = Assert.Single(battle.Combatants, actor => IsAvailableEnemy(actor) && actor.ModelId == 68);
            carrier.CurrentHp = 1;
            AttackAndDrain(world, battle, process, caster, carrier);
        }
        else if (formation == 813)
        {
            HashSet<string> defeated = [];
            for (int allocation = 0; allocation < 10; allocation++)
            {
                BattleCombatant current = Assert.Single(battle.Combatants, actor => IsAvailableEnemy(actor) && actor.ModelId != targetModel &&
                    process.ActionPackets.ModelAliases.TryEncodeExact(battle.ReadPresentationModelOwner(actor.Id), out _));
                Assert.True(defeated.Add(current.Id));
                current.CurrentHp = 1;
                AttackAndDrain(world, battle, process, caster, current);
                Assert.False(current.IsAlive);
            }
        }
        return Assert.Single(battle.Combatants, actor => IsAvailableEnemy(actor) && actor.ModelId == targetModel &&
            process.ActionPackets.ModelAliases.TryEncodeExact(battle.ReadPresentationModelOwner(actor.Id), out _));
    }

    private static void RunWorld(bool automatic, byte seed, Action<GameWorld, BattleSession, ExpandedBattlePresentationProcess, BattleCombatant, BattleCombatant> run, byte? finisher = null,
        ushort formation = ExpandedBattleInstalledEntry.Formation, int? targetModel = null)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
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
                if (finisher is byte selected)
                {
                    NativeCharacterVital squall = records[0];
                    records[0] = assets.WeaponDefinitions.Where(candidate => candidate.CharacterId == 0).Select(candidate => squall with { Weapon = checked((byte)candidate.Id) })
                        .First(candidate =>
                        {
                            records[0] = candidate;
                            return (assets.ReadPlayerNativeStatRules(vitals with { Records = records }, 0).WeaponRecord[2] & (1 << selected)) != 0;
                        });
                }
                current.SetCharacterVitals((vitals with { Records = records }).WithBattleParty([1, 2, 3, 0], 4));
                NativeLimitProgress limits = current.State.LimitProgress!;
                ushort options = Assert.IsType<ushort>(limits.Options978);
                current.SetLimitProgress(limits with { Options978 = (ushort)((options & ~0x8001) | (automatic ? 1 : 0)) });
            }, formation: formation, additionalEnemies: targetModel is null ? 4 : 1);
            world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { LimitOncePerTurn = false });
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
            Assert.False(process.ActionPackets.ModelAliases.TryEncodeExact(battle.ReadPresentationModelOwner(caster.Id), out _));
            BattleCombatant target;
            if (targetModel is int model)
            {
                caster.CurrentHp = caster.MaximumHp;
                target = SelectSpecialTarget(world, battle, process, caster, formation, model);
                caster.CurrentHp = 1;
            }
            else
            {
                target = battle.Combatants.Last(actor => IsAvailableEnemy(actor) && actor.Slot is { Position: >= 4 });
                Assert.False(process.ActionPackets.ModelAliases.TryEncodeExact(battle.ReadPresentationModelOwner(target.Id), out _));
            }
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
