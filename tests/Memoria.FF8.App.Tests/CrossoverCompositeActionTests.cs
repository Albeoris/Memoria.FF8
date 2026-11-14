using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverCompositeActionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Meteor", (byte)0, (byte)0, 10)]
    [InlineData("Blood Pain", (byte)9, (byte)4, 6)]
    [InlineData("Tonberry", (byte)0, (byte)0, 1)]
    public void Original_composite_action_settles_every_occurrence_releases_resources_and_allows_following_Fire(string name, byte character, byte crisis, int expectedContacts)
    {
        using Fixture fixture = new(name, character, crisis);
        Result result = fixture.Execute(queuedRoundTrip: true);
        Assert.Equal(expectedContacts, result.Contacts.Length);
        if (expectedContacts > 1)
        {
            Assert.Contains(result.Contacts.GroupBy(contact => contact.HitDescription.PreparedContact!.Invocation.TargetSlot), contacts => contacts.Count() > 1);
        }
        if (name == "Blood Pain")
        {
            Assert.All(result.Contacts, contact => Assert.Equal(4, contact.HitDescription.PreparedContact!.Invocation.TargetSlot));
        }
        fixture.FollowWithFire();
        output.WriteLine($"{name}: {result.Contacts.Length} individually settled native occurrences, {result.VisualCallbacks} visual callbacks, queued JSON continuation and following Fire completed.");
    }

    [Theory]
    [InlineData("Meteor", (byte)0, (byte)0)]
    [InlineData("Blood Pain", (byte)9, (byte)4)]
    [InlineData("Tonberry", (byte)0, (byte)0)]
    public void Original_composite_target_draws_receipts_and_final_random_state_repeat_from_the_same_queued_boundary(string name, byte character, byte crisis)
    {
        using Fixture first = new(name, character, crisis);
        Result original = first.Execute();
        using Fixture second = new(name, character, crisis);
        Result restored = second.Execute(queuedRoundTrip: true);
        Assert.Equal(JsonSerializer.Serialize(original.Contacts), JsonSerializer.Serialize(restored.Contacts));
        Assert.Equal(original.Random, restored.Random);
        Assert.Equal(original.Actors, restored.Actors);
    }

    [Theory]
    [InlineData("Meteor", (byte)0, (byte)0)]
    [InlineData("Blood Pain", (byte)9, (byte)4)]
    [InlineData("Tonberry", (byte)0, (byte)0)]
    public void Dead_target_is_unavailable_and_refusal_does_not_mutate_queue_stock_or_random_owners(string name, byte character, byte crisis)
    {
        using Fixture fixture = new(name, character, crisis, deadTarget: 4);
        fixture.AssertRejected(1 << 4);
        Result result = fixture.Execute();
        Assert.DoesNotContain(result.Contacts, contact => contact.HitDescription.PreparedContact!.Invocation.TargetSlot == 4);
    }

    [Fact]
    public void Original_selected_repeated_action_rejects_a_group_without_mutating_the_queued_boundary()
    {
        using Fixture fixture = new("Blood Pain", 9, 4);
        fixture.AssertSelectedGroupRejected();
    }

    [Theory]
    [InlineData("Meteor", (byte)0, (byte)0)]
    [InlineData("Blood Pain", (byte)9, (byte)4)]
    [InlineData("Tonberry", (byte)0, (byte)0)]
    public void Original_source_catalog_requires_stock_owned_limit_selection_or_a_living_assigned_guardian(string name, byte character, byte crisis)
    {
        using Fixture fixture = new(name, character, crisis);
        fixture.AssertSourceAvailability();
    }

    internal sealed record Result(ImmutableArray<Ff7BattleGuestCompositeContact> Contacts, string Random, string Actors, int VisualCallbacks);

    internal sealed class Fixture : IDisposable
    {
        private readonly GameAssets game;
        private readonly BattleAssets assets;
        private readonly Ff7Session original;
        private readonly LgpArchive archive;
        private readonly LgpArchive magic;
        private readonly SceneFormation formation;
        private readonly IReadOnlyList<KernelSection> kernel;
        private readonly Ff7PcExecutableData executable;
        private readonly Ff7BattleAnimationResources animations;
        private readonly Ff7Progress progress;
        private readonly NativeCharacterVitals origin;
        private readonly CrossoverFf8BattleKit kit;
        private GameplaySaveLimitProgress learned;
        private readonly CrossoverBattleRoster roster;
        private readonly CrossoverGuestAbility ability;
        private readonly BattleNativeAction action;
        private readonly BattleNativeAction fire;
        private readonly Ff7BattleRenderer renderer;
        private readonly int row;
        private readonly byte crisis;
        private readonly bool separateLinkedRig;
        private readonly bool nativeProcess;
        private readonly CrossoverArenaSurfaceCapture? arenaCapture;
        private Ff7BattleSession host;
        private CrossoverGunbladeHostAttack attack = null!;
        private CrossoverGuestHostActions provider = null!;

        public int SourceMaximumHp => kit.MaximumHitPoints;
        public int NativeHitCount => action.HitCount;

        public Fixture(string name, byte character, byte crisis, int? deadTarget = null, CrossoverArenaSurfaceCapture? arenaCapture = null, bool soloGuestParty = false, bool damagedAllies = false, int? sourceHP = null, bool nativeProcess = false)
        {
            string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
            string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
            Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff8) && !string.IsNullOrWhiteSpace(ff7), "Select both original game installations.");
            this.crisis = crisis;
            this.nativeProcess = nativeProcess;
            this.arenaCapture = arenaCapture;
            game = new(ff8!);
            assets = new(ff8!);
            if (character >= 6)
            {
                BattleNativeAction source = Assert.Single(assets.NativeActions, item => item.Name == name && item.CharacterId is >= 6 and <= 10 && NativeBattleLimitRules.GuestActionId(item.CharacterId.Value) == item.ActionId);
                Assert.Equal(character, source.CharacterId);
                character = checked((byte)source.CharacterId!.Value);
            }
            origin = game.ReadNewGameCharacterVitals().DeepCopy();
            if (character >= 6)
            {
                origin = origin.WithExtendedRoster(true);
            }
            row = CrossoverGuestRecord.FindOriginalRow(origin, character);
            int magicCommand = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2) + 20;
            ushort sourceHitPoints = checked((ushort)(sourceHP ?? 9000));
            origin.Records[row] = origin.Records[row] with { BonusSources = null, HitPointBonus = 9999, HitPoints = new(sourceHitPoints, sourceHitPoints), Status = 0, Junction = origin.Records[row].Junction with { Commands = (uint)magicCommand } };
            origin = origin with { MagicSystem = origin.MagicSystem with { SharedMagicStock = true } };
            fire = Assert.Single(assets.NativeActions, item => item.Command == 2 && item.Name == "Fire");
            origin = origin.ChangeMagicStock(row, fire.ActionId, 4, MagicOperationReason.ScriptReward, out _);
            BattleNativeAction? spell = assets.NativeActions.SingleOrDefault(item => item.Command == 2 && item.Name == name);
            if (spell is not null)
            {
                origin = origin.ChangeMagicStock(row, spell.ActionId, 4, MagicOperationReason.ScriptReward, out _);
            }
            BattleActionDefinition? guardian = assets.ActionDefinitions.SingleOrDefault(item => item.Ability == BattleAbility.GuardianForce && (item.DisplayName ?? item.Name) == name);
            if (guardian is not null)
            {
                int guardianCommand = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)3) + 20;
                origin.Records[row] = origin.Records[row] with { Junction = origin.Records[row].Junction with { Commands = (uint)(magicCommand | guardianCommand << 8), GuardianForces = checked((ushort)(1 << guardian.Id)) } };
                NativeGuardianForceVital acquired = origin.GuardianForces[guardian.Id] with { Flags = (byte)(origin.GuardianForces[guardian.Id].Flags | 1) };
                origin.GuardianForces[guardian.Id] = acquired with { HitPoints = origin.Definitions.GuardianForceMaximum(guardian.Id, acquired) };
            }
            kit = CrossoverFf8BattleKit.Read(origin, assets, character);
            separateLinkedRig = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = kit.CharacterId, WeaponId = kit.Weapon }).LinkedPoseSource is not null;
            learned = GameplaySaveLimitProgress.Capture(game.ReadNewGameLimitProgress()) with { BlueMagicFlags = ushort.MaxValue, DuelFlags = ushort.MaxValue, AngeloFlags = 255 };
            ability = Assert.Single(CrossoverGuestAbilityCatalog.Read(assets, origin, kit, learned, crisis), item => item.Name == name);
            action = nativeProcess ? fire : Assert.IsType<BattleNativeAction>(ability.NativeAction);
            Ff7Installation installation = Ff7Installation.Discover(ff7!);
            original = Ff7Session.NewGame(installation);
            archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
            magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
            formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
            executable = Ff7PcExecutableData.Load(installation);
            kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
            animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
            progress = original.Progress with { Party = soloGuestParty ? [0, 255, 255] : [0, 1, 255] };
            host = Create();
            roster = CrossoverBattleRoster.Create(Guid.Parse("4024f7ec-2c66-4ecf-8d4c-38ed9513e82b"), host.Capture(), [new(0, 0, character)], []);
            Ff7BattleGuestProjection projection = CrossoverGuestHostActions.Projection(kit);
            host.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, character, row, kit.HitPoints, (ushort)kit.MaximumHitPoints, projection.Level, projection.Luck, projection.Defense, projection.MagicDefense));
            Ff7BattleSnapshot fixture = host.Capture();
            fixture = fixture with
            {
                Actors = fixture.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Gauge = ushort.MaxValue, AwaitingTurn = true } : actor with
                {
                    Gauge = 0,
                    AwaitingTurn = false,
                    Actor = actor.Actor with { HitPoints = actor.Actor.Slot == deadTarget ? 0u : damagedAllies && actor.Actor.Slot < 3 ? 30000u : 60000u, MaxHitPoints = 60000u, Statuses = actor.Actor.Slot == deadTarget ? 1u : 0x400u },
                    Timers = Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, actor.Actor.Slot == deadTarget ? 1u : 0x400u)
                }).ToArray()
            };
            host = Create(fixture);
            renderer = new(archive, formation.LocationId);
            Bind();
            Assert.Equal(separateLinkedRig, provider.Visual!.Model.LinkedTransform is not null);
            Ready();
        }

        public void AssertRejected(ushort targets)
        {
            Ff7BattleCommandOption option = Option();
            Assert.Equal(0, option.ValidTargets & targets);
            Assert.NotNull(host.GuestAbilityTargetUnsupportedReason(0, Ff7BattleCommand.Magic, ability.Key, 4));
            string before = JsonSerializer.Serialize(host.Capture());
            string stock = JsonSerializer.Serialize(provider.Capture());
            Assert.False(host.Submit(0, Ff7BattleCommand.Magic, ability.Key, targets));
            Assert.Equal(before, JsonSerializer.Serialize(host.Capture()));
            Assert.Equal(stock, JsonSerializer.Serialize(provider.Capture()));
            Assert.Null(provider.Invocation);
        }

        public void AssertSelectedGroupRejected()
        {
            Ff7BattleCommandOption option = Option();
            Assert.True(option.SelectSingle);
            Assert.Equal(0, option.GroupUses);
            string before = JsonSerializer.Serialize(host.Capture());
            string source = JsonSerializer.Serialize(provider.Capture());
            Assert.False(host.Submit(0, Ff7BattleCommand.Magic, ability.Key, 0x30));
            Assert.Equal(before, JsonSerializer.Serialize(host.Capture()));
            Assert.Equal(source, JsonSerializer.Serialize(provider.Capture()));
        }

        public void AssertSourceAvailability()
        {
            NativeCharacterVitals unavailable = origin.DeepCopy();
            GameplaySaveLimitProgress progress = learned;
            byte selectedCrisis = crisis;
            if (action.Command == 2)
            {
                unavailable = unavailable.ChangeMagicStock(row, action.ActionId, -4, MagicOperationReason.ScriptReward, out _);
            }
            else if (action.Command == 15)
            {
                progress = learned with { BlueMagicFlags = (ushort)(learned.BlueMagicFlags & ~(1 << action.ActionId)) };
            }
            else if (action.Command == 254)
            {
                int guardian = Assert.Single(unavailable.Records[row].Junction.GuardianForceAssignments);
                unavailable.GuardianForces[guardian] = unavailable.GuardianForces[guardian] with { HitPoints = 0 };
            }
            else
            {
                Assert.Equal(NativeBattleLimitRules.GuestActionId(kit.CharacterId), action.ActionId);
                selectedCrisis = 0;
            }
            CrossoverFf8BattleKit current = CrossoverFf8BattleKit.Read(unavailable, assets, kit.CharacterId);
            Assert.DoesNotContain(CrossoverGuestAbilityCatalog.Read(assets, unavailable, current, progress, selectedCrisis), choice => choice.Key == ability.Key);
            Assert.Contains(CrossoverGuestAbilityCatalog.Read(assets, origin, kit, learned, crisis), choice => choice.Key == ability.Key);
        }

        public Result Execute(bool queuedRoundTrip = false, Action<Ff7BattleRenderer, Ff7BattleSession, CrossoverGuestHostActions>? observe = null, int? targetSlot = null)
        {
            Ff7BattleCommandOption option = Option();
            Assert.True(option.UnsupportedReason is null, option.UnsupportedReason);
            ushort targets = (ushort)((option.CandidateTargets ?? option.ValidTargets) & (option.DefaultsToEnemies ? 0x3F0 : 7));
            if (targetSlot is int selected)
            {
                ushort selectedMask = checked((ushort)(1 << selected));
                Assert.NotEqual(0, (option.CandidateTargets ?? option.ValidTargets) & selectedMask);
                targets = option.SelectSingle && option.GroupUses == 0 ? selectedMask : (ushort)((option.CandidateTargets ?? option.ValidTargets) & (selected < 3 ? 7 : 0x3F0));
            }
            if (option.SelectSingle && option.GroupUses == 0)
            {
                targets = (ushort)(targets & -targets);
            }
            Assert.NotEqual((ushort)0, targets);
            int targetsPerHit = (action.TargetFlags.GetValueOrDefault() & 2) == 0 ? System.Numerics.BitOperations.PopCount((uint)targets) : 1;
            int expectedContacts = checked(action.HitCount * targetsPerHit);
            string stock = JsonSerializer.Serialize(provider.WorkingStock);
            Assert.True(host.Submit(0, Ff7BattleCommand.Magic, ability.Key, targets));
            Assert.Equal(targets, Assert.Single(host.Capture().Queue.Ready.OfType<Ff7BattleQueuedCommand>()).TargetMask);
            Assert.Equal(stock, JsonSerializer.Serialize(provider.WorkingStock));
            if (queuedRoundTrip)
            {
                Ff7BattleSnapshot saved = RoundTrip(host.Capture());
                CrossoverGunbladeHostSnapshot caster = RoundTrip(attack.Capture());
                CrossoverGuestHostActionsSnapshot savedProvider = RoundTrip(provider.Capture());
                host = Create(saved);
                Bind(caster, savedProvider);
            }
            ImmutableArray<Ff7BattleGuestCompositeContact> contacts = [];
            int settled = 0;
            int visualCallbacks = 0;
            bool began = false;
            NativeBattleEffectPresentation? effect = null;
            Dictionary<int, Ff7BattleActionVitals> projected = [];
            for (int tick = 0; tick < 10000; tick++)
            {
                host.Advance(waitForCommand: true);
                Assert.True(host.GuestAbilityFailure is null, host.GuestAbilityFailure?.Reason);
                observe?.Invoke(renderer, host, provider);
                began |= provider.Invocation is not null;
                if (host.Action?.GuestComposite is { } composite)
                {
                    if (contacts.IsEmpty)
                    {
                        contacts = composite.Contacts;
                        Assert.Equal(Enumerable.Range(0, contacts.Length), contacts.Select(contact => contact.Ordinal));
                        Assert.All(contacts, contact => Assert.Equal(0, contact.Phase));
                        Assert.Equal(Enumerable.Range(0, action.HitCount).SelectMany(hit => Enumerable.Repeat(hit, targetsPerHit)), contacts.Select(contact => contact.Hit));
                        foreach (Ff7BattleGuestCompositeContact contact in contacts)
                        {
                            Ff7BattleGuestPreparedContact receipt = Assert.IsType<Ff7BattleGuestPreparedContact>(contact.HitDescription.PreparedContact);
                            int target = receipt.Invocation.TargetSlot;
                            if (projected.TryGetValue(target, out Ff7BattleActionVitals previous))
                            {
                                Assert.Equal(previous with { OriginSavedStatuses = Ff7GuestBattleStatuses.ToOrigin(previous.Statuses, previous.OriginSavedStatuses) }, receipt.TargetBefore);
                            }
                            projected[target] = receipt.Receipt.Target;
                        }
                    }
                    Assert.InRange(composite.NextContact, settled, contacts.Length);
                }
                foreach (IGrouping<int, Ff7BattleGuestCompositeContact> sameTarget in host.PresentedGuestContacts.GroupBy(contact => contact.HitDescription.PreparedContact!.Invocation.TargetSlot))
                {
                    Ff7BattleGuestPreparedContact receipt = sameTarget.Last().HitDescription.PreparedContact!;
                    Ff7BattleAiActor actual = host.Actors.Single(actor => actor.Actor.Slot == sameTarget.Key).Actor;
                    Assert.Equal(receipt.Receipt.Target.Hp, actual.HitPoints);
                    Assert.Equal(receipt.Receipt.Target.Statuses, actual.Statuses);
                    if (receipt.Receipt.AddedOriginBattleStatuses != 0)
                    {
                        Assert.Equal(receipt.Receipt.AddedOriginBattleStatuses, host.GetOriginBattleStatuses(sameTarget.Key) & receipt.Receipt.AddedOriginBattleStatuses);
                    }
                }
                settled += host.PresentedGuestContacts.Length;
                if (provider.ActiveAbility is { } active)
                {
                    effect = active.Presentation.Effect ?? effect;
                    visualCallbacks += !active.Presentation.Draws.IsEmpty || active.Presentation.Ordering is { Packets.Count: > 0 } ? 1 : 0;
                }
                if (began && provider.Invocation is null && host.Action is null)
                {
                    break;
                }
            }
            Assert.True(began);
            Assert.False(contacts.IsEmpty);
            Assert.Equal(expectedContacts, contacts.Length);
            Assert.Equal(contacts.Length, settled);
            Assert.True(visualCallbacks > 0);
            Assert.Null(provider.Invocation);
            Assert.Null(host.Action);
            Assert.NotNull(effect);
            Assert.True(effect.Complete);
            Assert.Equal(action.MagicId, effect.Graphics.OriginalEffect!.RequestedEffect);
            Assert.Empty(effect.ArenaReplacements);
            Assert.Null(provider.EffectVisual);
            Assert.Equal((byte)1, provider.Visual!.Model.Sequence);
            Assert.Null(provider.Visual.Model.ResourceMagicId);
            Assert.Null(provider.Visual.Model.ActiveStagedResourceRow100);
            Assert.Equal(provider.Visual.Model.OriginalResourceRow104, provider.Visual.Model.ActiveResourceRow100);
            Assert.Equal(separateLinkedRig, provider.Visual.Model.LinkedTransform is not null);
            if (action.Command == 2)
            {
                Assert.Equal(3, provider.WorkingStock.GetMagicCount(row, action.ActionId));
                Assert.Single(provider.Capture().CommittedSpells!.Value);
            }
            else
            {
                Assert.Equal(stock, JsonSerializer.Serialize(provider.WorkingStock));
            }
            Assert.Equal(4, provider.WorkingStock.GetMagicCount(row, fire.ActionId));
            return new(contacts, JsonSerializer.Serialize(host.Capture().Random), JsonSerializer.Serialize(host.Actors.Select(actor => actor.Actor)), visualCallbacks);
        }

        public void FollowWithFire()
        {
            Ready();
            Ff7BattleCommandOption option = Assert.Single(host.Commands(0), choice => choice.Command == Ff7BattleCommand.Magic && choice.Argument == fire.ActionId);
            Assert.Null(option.UnsupportedReason);
            int target = host.Actors.First(actor => actor.Actor.Slot >= 4 && (option.ValidTargets & 1 << actor.Actor.Slot) != 0).Actor.Slot;
            Assert.True(host.SubmitGuestMagicSequence(0, fire.ActionId, [(ushort)(1 << target)]));
            bool began = false;
            int contacts = 0;
            for (int tick = 0; tick < 4000; tick++)
            {
                host.Advance(waitForCommand: true);
                Assert.True(host.GuestAbilityFailure is null, host.GuestAbilityFailure?.Reason);
                began |= provider.Invocation is not null;
                if (host.Action?.GuestComposite is { } composite)
                {
                    Assert.Single(composite.Contacts);
                    contacts = Math.Max(contacts, composite.NextContact);
                }
                if (began && provider.Invocation is null && host.Action is null)
                {
                    break;
                }
            }
            Assert.True(began);
            Assert.Null(provider.Invocation);
            Assert.Null(host.Action);
            Assert.Equal(1, contacts);
            Assert.Equal(3, provider.WorkingStock.GetMagicCount(row, fire.ActionId));
        }

        public Ff7BattleSession ProcessHost => host;
        public CrossoverGuestHostActions ProcessProvider => provider;
        private CrossoverGuestItemInventory ProcessItems => ((CrossoverGuestEncounter)typeof(CrossoverGuestHostActions).GetField("encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(provider)!).Items!;
        public void OpenNativeSelection(ushort? selection = null)
        {
            Ff7BattleCommandOption invocation = Option(selection);
            Assert.Null(invocation.UnsupportedReason);
            provider.ExecuteOperation(invocation.Argument, 4, () => throw new InvalidOperationException("Limit selection must use its source random owner."));
        }

        public void AdvanceToNativeInput()
        {
            for (int tick = 0; tick < 12000; tick++)
            {
                if (provider.DuelProcess?.Phase == BattleNativeDuelPhase.Ready || provider.ShotProcess?.Phase == BattleNativeShotPhase.Ready || provider.SlotsChoosing)
                {
                    return;
                }
                host.Advance(waitForCommand: true);
                Assert.True(host.GuestAbilityFailure is null, host.GuestAbilityFailure?.Reason);
            }
            throw new InvalidDataException("The original Limit process did not reach its native input acknowledgement.");
        }

        public void SelectSlotsSpell(string name)
        {
            BattleNativeAction selected = assets.NativeActions.Single(action => action.Command == 16 && action.Name == name);
            typeof(CrossoverGuestHostActions).GetField("mixedSlots", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(provider, new BattleNativeSlotsRoll(checked((byte)selected.ActionId), 1));
        }

        public void ExecuteNativeProcess(byte command, ushort? selection = null, bool automaticDuel = false, byte? angeloFlags = null, byte? duelAction = null)
        {
            if (automaticDuel)
            {
                typeof(CrossoverGuestHostActions).GetField("limitOptions", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(provider, (ushort)2);
            }
            if (angeloFlags is byte learnedAngelo)
            {
                learned = learned with { AngeloFlags = learnedAngelo };
                provider.BindLimitProgress(learned);
            }
            Ff7BattleCommandOption invocation = Option(selection);
            Assert.Null(invocation.UnsupportedReason);
            Assert.True(host.Submit(0, Ff7BattleCommand.Magic, invocation.Argument, command is 16 or 19 ? (ushort)1 : (ushort)(1 << 4)));
            bool began = false;
            bool inputSeen = false;
            bool queuedMove = false;
            int pulse = 0;
            int completedPhases = 0;
            ushort? previousPhase = null;
            HashSet<ushort> seenPhases = [];
            HashSet<(ushort Ability, long StartedAt)> ordinaryDuelInvocations = [];
            NativeBattleEffectPresentation? nativeProcessOwner = null;
            byte? recipeMoveSet = null;
            bool desiredRecipeSubmitted = false;
            int submittedRecipes = 0;
            short? initialDuelTimer = null;
            short? remainingDuelTimer = null;
            for (int tick = 0; tick < 16000; tick++)
            {
                if (queuedMove && !automaticDuel)
                {
                    provider.SetProcessButtons(0);
                }
                if (provider.DuelProcess is { Phase: BattleNativeDuelPhase.Ready })
                {
                    inputSeen = true;
                    BattleNativeDuelChoice move = duelAction is byte desired ? DuelRoute(provider.DuelProcess!.MoveSetId, desired) : provider.DuelProcessChoices.First(choice => choice.Learned);
                    if (recipeMoveSet != provider.DuelProcess!.MoveSetId && !desiredRecipeSubmitted)
                    {
                        queuedMove = false;
                        pulse = 0;
                        recipeMoveSet = provider.DuelProcess.MoveSetId;
                    }
                    if (!queuedMove && !automaticDuel && !desiredRecipeSubmitted)
                    {
                        provider.SetProcessButtons((pulse & 1) == 0 ? move.Buttons[pulse / 2] : (ushort)0);
                        pulse++;
                        if (pulse >= 2 * move.Buttons.Length - 1)
                        {
                            submittedRecipes++;
                            queuedMove = true;
                            desiredRecipeSubmitted = duelAction is null || duelAction == move.ActionId;
                        }
                    }
                }
                if (provider.SlotsChoosing)
                {
                    inputSeen = true;
                    provider.RerollProcessSlots();
                    Assert.NotNull(provider.SlotsProcess);
                    if (provider.SlotsCastUnsupportedReason is null)
                    {
                        provider.AcceptProcessSlots();
                    }
                }
                if (provider.ShotProcess is { Phase: BattleNativeShotPhase.Ready })
                {
                    inputSeen = true;
                    if (!queuedMove)
                    {
                        provider.TriggerProcessShot();
                        queuedMove = true;
                    }
                    else
                    {
                        provider.FinishProcessShot();
                    }
                }
                host.Advance(waitForCommand: true);
                Assert.True(host.GuestAbilityFailure is null, host.GuestAbilityFailure?.Reason);
                Assert.True(provider.ProcessFailure is null, provider.ProcessFailure);
                if (provider.Invocation is { GuestAbility: >= 0xF100 and <= 0xF105 } duelInvocation)
                {
                    ordinaryDuelInvocations.Add((duelInvocation.GuestAbility, duelInvocation.StartedAt));
                }
                began |= provider.ProcessActive;
                if (provider.DuelProcess is { } timer)
                {
                    initialDuelTimer ??= timer.InitialTimer;
                    Assert.Equal(initialDuelTimer.Value, timer.InitialTimer);
                    Assert.True(remainingDuelTimer is null || timer.RemainingTimer <= remainingDuelTimer.Value, $"The original Duel timer restarted at host tick {host.Tick}: {remainingDuelTimer} -> {timer.RemainingTimer} in {timer.Phase}.");
                    remainingDuelTimer = timer.RemainingTimer;
                }
                CrossoverGuestFireHostState? presented = (CrossoverGuestFireHostState?)typeof(CrossoverGuestHostActions).GetProperty("PresentedFire", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(provider);
                if ((command is 11 or 14) && presented?.Presentation.Effect is { } nativeEffect && presented.Presentation.Command != 239)
                {
                    nativeProcessOwner ??= nativeEffect;
                    Assert.Same(nativeProcessOwner, nativeEffect);
                }
                if (provider.Invocation?.GuestAbility is ushort phase && previousPhase != phase)
                {
                    previousPhase = phase;
                    seenPhases.Add(phase);
                    completedPhases++;
                }
                if (began && !provider.ProcessActive && provider.Invocation is null && host.Action is null)
                {
                    break;
                }
            }
            Assert.True(began);
            if (duelAction is byte expectedMove)
            {
                ushort expectedAbility = (ushort)(((expectedMove >= 6 ? 239 : 241) << 8) | expectedMove);
                Assert.Contains(expectedAbility, seenPhases);
                if (expectedMove == NativeBattleDuelRules.ActionId(kit.Limit, NativeBattleDuelRules.InitialMoveSet(kit.Limit, crisis)))
                {
                    Assert.True(ordinaryDuelInvocations.Count(invocation => invocation.Ability == expectedAbility) >= 2, "The requested input move must execute after its independent native opening phase.");
                }
            }
            Assert.True(inputSeen || command == 19);
            if (command == 11 && !automaticDuel && duelAction is null)
            {
                Assert.Equal(1, submittedRecipes);
                Assert.Equal(2, ordinaryDuelInvocations.Count);
            }
            Assert.True(completedPhases > 0);
            Assert.False(provider.ProcessActive, $"Native Limit still owns its turn: Duel {provider.DuelProcess?.Phase} timer {provider.DuelProcess?.RemainingTimer}/{initialDuelTimer}; Shot {provider.ShotProcess?.Phase}; invocation {provider.Invocation}.");
            Assert.Null(provider.Invocation);
            Assert.Null(host.Action);
        }

        public void AssertProcessCancellation(bool acceptedShot)
        {
            OpenNativeSelection();
            AdvanceToNativeInput();
            Assert.IsType<NativeBattleEffectPresentation>(PresentedProcess()?.Presentation.Effect);
            int beforeAmmo = ProcessItems.Slots.Single(item => item.ItemId == 101).Count;
            if (acceptedShot)
            {
                provider.TriggerProcessShot();
                Assert.Equal(beforeAmmo - 1, ProcessItems.Slots.Single(item => item.ItemId == 101).Count);
            }
            Ff7BattleSnapshot state = host.Capture();
            state = state with
            {
                Actors = state.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with
                {
                    Actor = actor.Actor with { HitPoints = 0, Statuses = actor.Actor.Statuses | 1 },
                    Timers = Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, actor.Actor.Statuses | 1)
                } : actor).ToArray()
            };
            typeof(Ff7BattleSession).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, state);
            bool cancellationSeen = false;
            HashSet<ushort> phases = [];
            for (int tick = 0; tick < 16000; tick++)
            {
                host.Advance(waitForCommand: true);
                Assert.True(host.GuestAbilityFailure is null, host.GuestAbilityFailure?.Reason);
                cancellationSeen |= provider.ProcessCancelling;
                if (provider.Invocation is { } invocation)
                {
                    phases.Add(invocation.GuestAbility);
                }
                if (!provider.ProcessActive && provider.Invocation is null && host.Action is null)
                {
                    break;
                }
            }
            Assert.True(cancellationSeen);
            Assert.False(provider.ProcessActive);
            Assert.Null(provider.Invocation);
            Assert.Null(host.Action);
            Assert.False(provider.HasProcessBackground);
            Assert.DoesNotContain(host.Capture().Queue.Slots, entry => entry?.Command.ActorSlot == 0);
            Assert.DoesNotContain(host.Capture().Queue.Ready, command => command?.ActorSlot == 0);
            if (acceptedShot)
            {
                Assert.DoesNotContain((ushort)0xED00, phases);
                Assert.Equal(beforeAmmo - 1, ProcessItems.Slots.Single(item => item.ItemId == 101).Count);
                Assert.Single(ProcessItems.Ledger, debit => !debit.Refunded && debit.Selection.ItemId == 101);
            }

            CrossoverGuestFireHostState? PresentedProcess() => (CrossoverGuestFireHostState?)typeof(CrossoverGuestHostActions).GetProperty("PresentedFire", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(provider);
        }

        public void AssertRetainedPreflightIsolation()
        {
            OpenNativeSelection();
            AdvanceToNativeInput();
            CrossoverGuestFireHostState? retained = null;
            for (int tick = 0; tick < 10000; tick++)
            {
                retained = (CrossoverGuestFireHostState?)typeof(CrossoverGuestHostActions).GetProperty("PresentedFire", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(provider);
                if (retained?.Presentation.Effect is { ServiceComplete: true })
                {
                    break;
                }
                host.Advance(waitForCommand: true);
                Assert.True(host.GuestAbilityFailure is null, host.GuestAbilityFailure?.Reason);
            }
            NativeBattleEffectPresentation effect = Assert.IsType<NativeBattleEffectPresentation>(retained?.Presentation.Effect);
            string before = CaptureNative();
            string working = JsonSerializer.Serialize(provider.WorkingStock);
            BattleNativeDuelChoice choice = provider.DuelProcessChoices.First(choice => !choice.Finisher);
            Assert.Null(provider.DuelProcessChoiceUnsupportedReason(choice));
            Assert.Equal(before, CaptureNative());
            Assert.Equal(working, JsonSerializer.Serialize(provider.WorkingStock));

            string CaptureNative() => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(new BattleSnapshot { NativeGraphics = effect.Graphics, NativeEffectMemory = effect.Memory })) + JsonSerializer.Serialize(effect.Random) + JsonSerializer.Serialize(effect.Source);
        }

        public void ExecuteAngelWingTurns(uint repetitionStatus = 0, int expectedCasts = 1)
        {
            if (repetitionStatus != 0)
            {
                Ff7BattleSnapshot state = host.Capture() with { GuestOriginStatuses = [new(0, repetitionStatus)] };
                typeof(Ff7BattleSession).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, state);
            }
            Ff7BattleCommandOption invocation = Option(0x1304);
            Assert.Null(invocation.UnsupportedReason);
            int fireStock = provider.WorkingStock.GetMagicCount(row, fire.ActionId);
            Assert.True(host.Submit(0, Ff7BattleCommand.Magic, invocation.Argument, 1));
            bool activated = false;
            bool forced = false;
            bool forcedContact = false;
            HashSet<long> forcedInvocations = [];
            for (int tick = 0; tick < 20000; tick++)
            {
                host.Advance();
                Assert.True(host.GuestAbilityFailure is null, host.GuestAbilityFailure?.Reason);
                activated |= provider.ProcessOwnsReadyTurn;
                if (activated)
                {
                    Assert.Null(host.ReadyPlayer);
                    Ff7BattleGuestPhysicalHit query = new(CrossoverGunbladeHostAttack.ProjectPhysicalSource(kit), new(0, 0, 255), 0);
                    Assert.Equal(Ff7GuestBattleStatuses.ToHostStatuses(0x30, 0x4000), host.GuestContactContext(0, 0, query, false).TargetVitals.AbsoluteAddStatusImmunities);
                }
                if (host.Action is { Command.GuestAbility: var ability, GuestComposite: { } contacts } && ability == fire.ActionId)
                {
                    forced = true;
                    forcedInvocations.Add(host.Action.StartedAt);
                    Assert.All(contacts.Contacts, contact =>
                    {
                        Assert.Equal(5, contact.HitDescription.Attack.OriginDamageMultiplier);
                        Assert.Equal(expectedCasts == 3 ? 75 : 100, contact.HitDescription.Attack.DamageScalePercent);
                    });
                    forcedContact |= contacts.NextContact > 0;
                }
                if (forced && forcedContact && provider.Invocation is null && host.Action is null && !provider.ProcessActive)
                {
                    break;
                }
            }
            Assert.True(activated);
            Assert.True(forced);
            Assert.True(forcedContact);
            Assert.Equal(expectedCasts, forcedInvocations.Count);
            Assert.Empty(host.Capture().GuestMagicSequences);
            Assert.Equal(fireStock, provider.WorkingStock.GetMagicCount(row, fire.ActionId));
            provider.AdvanceIdle(host.Tick + 1, 1, null);
            Assert.False(provider.ProcessOwnsReadyTurn);
            Ff7BattleGuestPhysicalHit released = new(CrossoverGunbladeHostAttack.ProjectPhysicalSource(kit), new(0, 0, 255), 0);
            Assert.Equal(0u, host.GuestContactContext(0, 0, released, false).TargetVitals.AbsoluteAddStatusImmunities);
        }

        private BattleNativeDuelChoice DuelRoute(byte initial, byte desired)
        {
            Queue<(byte MoveSet, BattleNativeDuelChoice? First)> pending = new();
            HashSet<byte> visited = [];
            pending.Enqueue((initial, null));
            while (pending.TryDequeue(out var state))
            {
                if (!visited.Add(state.MoveSet))
                {
                    continue;
                }
                IReadOnlyList<BattleNativeDuelChoice> choices = NativeBattleDuelRules.Choices(kit.Limit, state.MoveSet, learned.DuelFlags, assets.NativeActions);
                foreach (BattleNativeDuelChoice choice in choices.Where(choice => DuelRecipeIsSelectable(choices, choice)))
                {
                    BattleNativeDuelChoice first = state.First ?? choice;
                    if (choice.ActionId == desired)
                    {
                        return first;
                    }
                    if (!choice.Finisher)
                    {
                        pending.Enqueue((choice.MoveSetId, first));
                    }
                }
            }
            throw new InvalidDataException($"No native Duel recipe route reaches source action {desired}.");
        }

        private static bool DuelRecipeIsSelectable(IReadOnlyList<BattleNativeDuelChoice> choices, BattleNativeDuelChoice selected)
        {
            ImmutableArray<ushort> ring = ImmutableArray.Create<ushort>(0, 0, 0, 0, 0, 0, 0, 0);
            byte cursor = 0;
            for (int index = 0; index < selected.Buttons.Length; index++)
            {
                cursor = (byte)((cursor + 1) & 7);
                ring = ring.SetItem(cursor, selected.Buttons[index]);
                BattleNativeDuelChoice? matched = choices.FirstOrDefault(choice => NativeBattleDuelRules.Matches(choice, ring, cursor));
                if (matched is not null)
                {
                    return index == selected.Buttons.Length - 1 && matched.MoveSetId == selected.MoveSetId;
                }
            }
            return false;
        }

        private Ff7BattleSession Create(Ff7BattleSnapshot? saved = null) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: saved, animations: animations);

        private void Bind(CrossoverGunbladeHostSnapshot? caster = null, CrossoverGuestHostActionsSnapshot? saved = null)
        {
            attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.RenderCameraOutput, _ => 128, caster, host.GuestEffectTargetAnchor, host.GuestEffectTargetBone);
            if (caster is null)
            {
                attack.PrepareStanding(host.VisualPlacement(0));
            }
            GameplaySaveEconomy? economy = nativeProcess ? GameplaySaveEconomy.Capture(game.ReadNewGameEconomy()) : null;
            if (economy is not null)
            {
                NativeItemStock[] items = economy.Items.ToArray();
                for (int item = 101; item <= 108; item++)
                {
                    NativeBattleItemStockRules.Add(items, item, 3);
                }
                economy = economy with { Items = items };
            }
            provider = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets, saved, originEconomy: economy, originLimitOptions: nativeProcess ? (ushort)0 : null, originLimitUiRandom: nativeProcess ? NativePlaytimeState.FromColdProcess().CreateBattleUiRandom() : null);
            provider.BindLimitProgress(learned);
            if (nativeProcess)
            {
                provider.BindLimitProcessOwners(255, 0x20);
            }
            MethodInfo actorVisual = typeof(Ff7Scene).GetMethod("BattleActorVisual", BindingFlags.Static | BindingFlags.NonPublic)!;
            provider.BindForeignGeometry(slot => renderer.CaptureActorGeometry((Ff7BattleActorVisual)actorVisual.Invoke(null, [host, host.Actors.Single(actor => actor.Actor.Slot == slot)])!));
            provider.BindArena(renderer.CaptureArenaGeometry, arenaCapture is null ? null : arenaCapture.Resolve);
            host.BindGuestAttack(provider);
        }

        private Ff7BattleCommandOption Option(ushort? selection = null)
        {
            typeof(CrossoverGuestHostActions).GetField("limitSelection", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(provider, new CrossoverNativeRenzokukenSelection(NativeBattleRandomState.Initialize(0), host.Tick, crisis));
            return Assert.Single(host.Commands(0), choice => choice.Command == Ff7BattleCommand.Magic && choice.Argument == (selection ?? ability.Key));
        }

        private void Ready()
        {
            for (int tick = 0; tick < 10000 && host.ReadyPlayer != 0; tick++)
            {
                host.Advance();
            }
            Assert.Equal(0, host.ReadyPlayer);
        }

        private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

        public void Dispose()
        {
            if (nativeProcess)
            {
                provider.CancelAtTerminal();
            }
            magic.Dispose();
            archive.Dispose();
            original.Dispose();
            assets.Dispose();
            game.Dispose();
        }
    }
}
