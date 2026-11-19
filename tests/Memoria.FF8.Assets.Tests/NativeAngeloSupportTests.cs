using System.Collections.Immutable;
using System.Text.Json;
using System.Reflection;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeAngeloSupportTests
{
    [Theory]
    [InlineData(0, 23)]
    [InlineData(6, 41)]
    public void Foreign_automatic_Recover_uses_actual_Combine_scope_through_contact_and_restore(int recipient, int bones)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, 3, false, expanded: true, holderCharacter: recipient);
        BattleModelOwner owner = battle.ReadPresentationModelOwner("rinoa");
        Assert.Null(ReadFamily(battle, owner));
        for (int callback = 0; callback < 2400 && CurrentInvocation(battle) is null; callback++)
        {
            battle.Tick();
        }
        BattleSession.ExpandedHostedInvocation ordinary = Assert.IsType<BattleSession.ExpandedHostedInvocation>(CurrentInvocation(battle));
        Assert.Null(ordinary.Command.NativePlayerReaction);
        Assert.Null(ReadFamily(battle, owner));
        AdvanceUntilSupport(battle);
        SupportInvocation support = ReadSupport(battle);
        Assert.Equal((byte)240, support.NativeQueuedCommand);
        Assert.Equal((ushort)12, support.NativeAction.ActionId);
        Assert.Equal(BattleNativePlayerReaction.AngeloRecover, support.NativePlayerReaction);
        Assert.Equal(BattleLimitFamily.Combine, ReadFamily(battle, owner));
        Assert.Null(ReadFamily(battle, battle.ReadPresentationModelOwner("ally-0")));
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        NativeBattleLimitPresentationBinding binding = Assert.Single(Capture(battle).PresentationContinuation!.Resources.NativeGraphics!.LimitPresentations, candidate => candidate.ActorId == "rinoa" && candidate.Family == BattleLimitFamily.Combine);
        Assert.Equal(NativeBattleEquipmentPresentationRole.FreeHands, binding.Resource.EquipmentPresentationRole);
        string expectedBank = Convert.ToHexString(SHA256.HashData(binding.Resource.PoseSource!.Animations.AsSpan()));
        string progress = JsonSerializer.Serialize(battle.ReadDefinition().LimitProgress);
        BattleSession restored = RoundTrip(battle);
        BattlePresentationProcess restoredProcess = Assert.IsType<BattlePresentationProcess>(restored.Presentation);
        bool contact = false;
        for (int callback = 0; callback < 2400 && !contact; callback++)
        {
            NativeBattleGraphicsModel before = process.Graphics.ActorModels.Get(owner);
            NativeBattleGraphicsResource resource = (NativeBattleGraphicsResource)typeof(BattlePresentationProcess).GetMethod("NativeGraphicsPoseResource", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(process, [process.Graphics, owner])!;
            _ = typeof(BattlePresentationProcess).GetMethod("NativeGraphicsPoseResource", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(restoredProcess, [restoredProcess.Graphics, owner]);
            int events = battle.Events.Count;
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(Capture(battle)), JsonSerializer.Serialize(Capture(restored)));
            contact = battle.Events.Skip(events).Any(entry => entry.Kind == "EffectContact" && entry.Message == support.NativeAction.Name && entry.TargetId == "rinoa");
            if (contact)
            {
                Assert.Equal(NativeBattleEquipmentPresentationRole.FreeHands, before.EquipmentPresentationRole);
                Assert.Equal((byte?)checked((byte)recipient), before.ModelId4);
                Assert.Equal(bones, resource.PoseSource!.BoneCount);
                Assert.Equal(expectedBank, Convert.ToHexString(SHA256.HashData(resource.PoseSource.Animations.AsSpan())));
                Assert.Equal(binding.Resource.PoseSource!.ActionBoneAliases.ToArray(), resource.PoseSource.ActionBoneAliases.ToArray());
            }
        }
        Assert.True(contact);
        Assert.Equal(recipient, battle.Combatants.Single(actor => actor.Id == "rinoa").ModelId);
        Assert.Equal(recipient, battle.GetCombatantDefinition("rinoa").NativePlayerStatRules!.CharacterId);
        Assert.Equal(progress, JsonSerializer.Serialize(battle.ReadDefinition().LimitProgress));
        Assert.Equal(JsonSerializer.Serialize(Capture(battle)), JsonSerializer.Serialize(Capture(restored)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Duplicate_Combine_holders_share_one_periodic_chance_and_select_first_eligible_search_owner(bool firstBlocked)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition source = Create(game, assets, 8, false, expanded: true, periodic: true).Capture().Definition;
        BattleNativeLimitDefinition combine = assets.ReadNativeLimitDefinition(4);
        BattleCombatantDefinition[] actors = source.Combatants.ToArray();
        for (int index = 0; index < 2; index++)
        {
            actors[index] = actors[index] with { NativeLimit = assets.ReadNativeLimitDefinition(index), NativeLimitFamilies = [new(BattleLimitFamily.Combine, combine)], InitialUntargetable = index == 0 && firstBlocked };
        }
        int nativeRinoa = Array.FindIndex(actors, actor => actor.Id == "rinoa");
        actors[nativeRinoa] = actors[nativeRinoa] with { NativeLimitFamilies = [] };
        BattleSession initial = new(source with { Combatants = actors }, BattleAtbMode.Normal);
        typeof(BattleSession).GetMethod("InitializeNativeSupportCallbacks", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(initial, null);
        BattleSnapshot before = Capture(initial);
        BattleSession battle = BattleSession.Restore(before with { NativeSupport = before.NativeSupport! with { Cooldown = 0 } });
        NativeBattleRandomState expected = before.Random!.Draw(out _);

        typeof(BattleSession).GetMethod("AdvanceNativeSupportCallbacks", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, null);

        BattleAttackCommand queued = Assert.Single(battle.QueuedActions, action => action.NativePlayerReaction == BattleNativePlayerReaction.AngeloPeriodic);
        Assert.Equal(firstBlocked ? "ally-1" : "ally-0", queued.TargetId);
        Assert.Equal((ushort)14, queued.NativeAction!.ActionId);
        Assert.Equal(expected.Indexes, Capture(battle).Random!.Indexes);
        Assert.Equal(JsonSerializer.Serialize(before.Definition.LimitProgress), JsonSerializer.Serialize(Capture(battle).Definition.LimitProgress));
        Assert.False(battle.HasNativeLimitFamily("rinoa", BattleLimitFamily.Combine));
    }

    [Theory]
    [InlineData(1, false, 11, 91, "enemy")]
    [InlineData(3, false, 12, 93, "rinoa")]
    [InlineData(4, true, 13, 94, "rinoa")]
    public void ExpandedDamageQueuesOriginalAngeloReactionAndRestoresBeforeItsContact(byte learned, bool lethal, ushort row, ushort effect, string recipient)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, learned, lethal, expanded: true);
        AdvanceUntilSupport(battle);
        SupportInvocation queued = ReadSupport(battle);
        Assert.Equal("rinoa", queued.ActorId);
        Assert.Equal(recipient, queued.TargetId);
        Assert.Equal((byte)240, queued.NativeQueuedCommand);
        Assert.Equal(row, queued.NativeAction!.ActionId);
        Assert.Equal(effect, queued.NativeAction.MagicId);
        var targets = Assert.IsType<BattleTargetSelection.Explicit>(queued.TargetRequest.Selection);
        Assert.Equal(recipient, Assert.Single(targets.Actors).ActorId);
        int preparedRevivalHp = 0;
        if (lethal)
        {
            Assert.True(battle.Combatants[0].IsAlive);
            Assert.Equal(BattleOutcome.Active, battle.Outcome);
            preparedRevivalHp = battle.Combatants[0].CurrentHp;
            BattleEvent healing = Assert.Single(battle.Events, entry => entry.Kind == "Healing");
            Assert.Equal("rinoa", healing.TargetId);
            Assert.Equal(preparedRevivalHp, healing.Amount);
        }
        AdvanceRestoredSupport(battle, queued.NativeAction.Name!, recipient);
        Assert.True(battle.Combatants[0].IsAlive);
        if (lethal)
        {
            Assert.Equal(preparedRevivalHp, battle.Combatants[0].CurrentHp);
            Assert.Single(battle.Events, entry => entry.Kind == "Healing");
        }
        if (row == 12)
        {
            Assert.Equal(9 + 10 * battle.Combatants[0].MaximumHp / 16, battle.Combatants[0].CurrentHp);
        }
    }

    [Fact]
    public void ExpandedSearchUsesOriginalClockAndBothRandomOwnersToGrantOneItem()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession initial = Create(game, assets, 8, false, expanded: true, periodic: true);
        BattleSnapshot boundary = Capture(initial);
        BattleSession battle = Restore(boundary with { NativeSupport = boundary.NativeSupport! with { Cooldown = 0 } });
        battle.Tick();
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions, command => command.NativePlayerReaction is not null);
        Assert.Equal(BattleNativePlayerReaction.AngeloPeriodic, queued.NativePlayerReaction);
        Assert.Equal("ally-0", queued.ActorId);
        Assert.Equal("rinoa", queued.TargetId);
        Assert.Equal(92, queued.NativeAction!.MagicId);
        NativeBattleRandomState expected = boundary.Random!.Draw(out _);
        Assert.Equal(expected.Indexes, Capture(battle).Random!.Indexes);
        Assert.Equal(boundary.NativeCrtRandom, Capture(battle).NativeCrtRandom);
        bool rewardObserved = false;
        AdvanceRestoredSupport(battle, queued.NativeAction.Name!, "rinoa", () =>
        {
            if (!rewardObserved && battle.Items.Any(item => item.Id == 1 && item.Count == 1))
            {
                Assert.Equal(boundary.NativeCrtRandom!.Draw(out _), Capture(battle).NativeCrtRandom);
                rewardObserved = true;
            }
        }, () => battle.Events.Any(entry => entry.Kind == "NativeText" && entry.TargetId == "rinoa"));
        BattleSnapshot completed = Capture(battle);
        Assert.True(rewardObserved);
        Assert.Equal(expected.Draw(out _).Indexes, completed.Random!.Indexes);
        Assert.Contains(completed.NativeItemInventory!, item => item is { ItemId: 1, Count: 1 });
        Assert.Single(battle.Events, entry => entry.Kind == "NativeText" && entry.TargetId == "rinoa");
    }

    [Theory]
    [InlineData(2, 1, 12, "ally-0")]
    [InlineData(4, 41, 13, "ally-1")]
    public void ExpandedPeriodicRecoverySelectsAnEligibleAllyWithOriginalRejectionSampling(byte learned, byte phase, ushort actionId, string recipient)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = Create(game, assets, learned, false, expanded: true, periodic: true).Capture().Definition;
        int slot = Array.FindIndex(definition.Combatants, actor => actor.Id == recipient);
        definition.Combatants[slot] = definition.Combatants[slot] with { CurrentHp = actionId == 12 ? 1 : 0, NativeStatus0 = actionId == 12 ? (ushort)0x300 : (ushort)1 };
        BattleSession initial = new(definition with { InitialRandom = NativeBattleRandomState.ForecastPhase(phase) }, BattleAtbMode.Normal);
        AttachAndEnter(initial, game, assets);
        BattleSnapshot boundary = Capture(initial);
        BattleSession battle = Restore(boundary with { NativeSupport = boundary.NativeSupport! with { Cooldown = 0 } });
        NativeBattleRandomState expected = boundary.Random!.Draw(out _);
        byte roll;
        do
        {
            expected = expected.Draw(out roll);
        }
        while (roll % 4 != slot && (actionId != 12 || roll % 4 != 3));
        battle.Tick();
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions, command => command.NativePlayerReaction is not null);
        Assert.Equal(BattleNativePlayerReaction.AngeloPeriodic, queued.NativePlayerReaction);
        Assert.Equal("ally-0", queued.ActorId);
        Assert.Equal(recipient, queued.TargetId);
        Assert.Equal(actionId, queued.NativeAction!.ActionId);
        Assert.Equal(expected.Indexes, Capture(battle).Random!.Indexes);
        AdvanceRestoredSupport(battle, queued.NativeAction.Name!, recipient);
        Assert.Equal(actionId == 12 ? 63 : 12, battle.Combatants[slot].CurrentHp);
    }

    [Theory]
    [InlineData(3, false, false)]
    [InlineData(4, true, false)]
    [InlineData(8, false, true)]
    public void ExpandedAngeloWithNoAcquiredGuardianForceDoesNotQueueOrConsumeChance(byte learned, bool lethal, bool periodic)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, learned, lethal, expanded: true, periodic: periodic, acquired: false);
        BattleSnapshot initial = Capture(battle);
        if (periodic)
        {
            battle = Restore(initial with { NativeSupport = initial.NativeSupport! with { Cooldown = 0 } });
            battle.Tick();
        }
        else
        {
            for (int frame = 0; frame < 128 && !battle.Events.Any(entry => entry is { Kind: "Damage", ActorId: "enemy" }); frame++)
            {
                battle.Tick();
            }
            Assert.Contains(battle.Events, entry => entry is { Kind: "Damage", ActorId: "enemy" });
        }
        Assert.DoesNotContain(battle.QueuedActions, command => command.NativePlayerReaction is not null);
        Assert.Equal(initial.Random!.Indexes, Capture(battle).Random!.Indexes);
    }

    private static void AdvanceRestoredSupport(BattleSession battle, string name, string recipient, Action? afterTick = null, Func<bool>? additionalCompletion = null)
    {
        BattleSession restored = RoundTrip(battle);
        bool Complete() => battle.Events.Any(entry => entry.Kind == "EffectContact" && entry.Message == name) && (additionalCompletion?.Invoke() ?? true);
        for (int frame = 0; frame < 2400 && !Complete(); frame++)
        {
            battle.Tick();
            restored.Tick();
            afterTick?.Invoke();
        }
        Assert.True(Complete());
        Assert.Equal(recipient, Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.Message == name).TargetId);
        Assert.Equal(JsonSerializer.Serialize(Capture(battle)), JsonSerializer.Serialize(Capture(restored)));
    }

    private static BattleSession RoundTrip(BattleSession battle)
    {
        BattleSnapshot captured = Capture(battle);
        string snapshot = JsonSerializer.Serialize(captured);
        // JSON owns the public logical document; command targeting and active host identities are ignored private continuation owners.
        BattleSnapshot document = JsonSerializer.Deserialize<BattleSnapshot>(snapshot)!;
        Assert.Equal(snapshot, JsonSerializer.Serialize(document));
        BattleSnapshot decoded = document with { QueuedActions = captured.QueuedActions, ActiveAction = captured.ActiveAction, PresentationContinuation = captured.PresentationContinuation, LogicalContinuation = captured.LogicalContinuation };
        BattleSession restored = Restore(decoded);
        Assert.Equal(snapshot, JsonSerializer.Serialize(Capture(restored)));
        return restored;
    }

    [Theory]
    [InlineData(1, BattleNativePlayerReaction.AngeloRush, 11, 8)]
    [InlineData(3, BattleNativePlayerReaction.AngeloRecover, 12, 1)]
    public void DamageCallbackUsesLearnedPriorityAndTheOriginalAttackerOrSelfMask(byte learned, BattleNativePlayerReaction reaction, int id, ushort mask)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, learned, false);
        AdvanceUntilSupport(battle);
        SupportInvocation queued = ReadSupport(battle);
        Assert.Equal(reaction, queued.NativePlayerReaction);
        Assert.Equal((byte)240, queued.NativeQueuedCommand);
        Assert.Equal(id, queued.ActionId);
        Assert.Equal(mask, NativeBattleTargetRequestAdapter.ProjectOriginalWord(queued.TargetRequest, target => battle.Combatants.Single(actor => actor.Id == target.ActorId).Slot));
        Assert.True(battle.Combatants[0].Atb > 0);
        string snapshot = JsonSerializer.Serialize(Capture(battle));
        Assert.Equal(snapshot, JsonSerializer.Serialize(Restore(Capture(battle)).Capture()));
    }

    [Fact]
    public void LethalReverseCommitsRevivalBeforeItsPreparedPresentationAndDoesNotReviveTwiceAfterRestore()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, 4, true);
        AdvanceUntilSupport(battle);
        //483400 prepares48D200/48E830 synchronously; the queued command mirrors facts already committed by the death callback.
        Assert.True(battle.Combatants[0].IsAlive);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        SupportInvocation queued = ReadSupport(battle);
        Assert.Equal(BattleNativePlayerReaction.AngeloReverse, queued.NativePlayerReaction);
        Assert.Equal((ushort)0x4001, NativeBattleTargetRequestAdapter.ProjectOriginalWord(queued.TargetRequest, target => battle.Combatants.Single(actor => actor.Id == target.ActorId).Slot));
        BattleSnapshot prepared = Capture(battle);
        NativeBattleActionHeaderSlot header = prepared.ReadFixturePresentation().NativeActionPackets!.ReadHeader(prepared.ReadFixturePresentation().NativeActionPackets!.ActiveHeader!.Value);
        Assert.Equal((byte)240, header.Command);
        Assert.Equal((ushort)13, header.Action);
        NativeBattleContactPacket contact = prepared.ReadFixturePresentation().NativeActionPackets!.ReadContact(header.FirstContact);
        Assert.Equal(battle.ReadPresentationModelOwner("rinoa"), contact.Primary.Recipient);
        Assert.Null(contact.Secondary);
        int revivedHp = battle.Combatants[0].CurrentHp;
        Assert.Equal(checked((ushort)revivedHp), contact.Primary.Amount6);
        Assert.Equal(1, contact.Primary.HitFlags & 1);
        BattleEvent healing = Assert.Single(battle.Events, item => item.Kind == "Healing");
        Assert.Equal("rinoa", healing.TargetId);
        Assert.Equal(revivedHp, healing.Amount);
        string json = JsonSerializer.Serialize(prepared);
        BattleSession restored = Restore(prepared);
        Assert.Equal(json, JsonSerializer.Serialize(Capture(restored)));
        NativeBattleActionPacketsState restoredPackets = Capture(restored).ReadFixturePresentation().NativeActionPackets!;
        Assert.Equal(contact, restoredPackets.ReadContact(header.FirstContact));
        Assert.Equal(
            NativeBattleContactPacketCodec.Encode(prepared.ReadFixturePresentation().NativeActionPackets!.ExportRetained().Contacts[header.FirstContact.Ordinal]!),
            NativeBattleContactPacketCodec.Encode(restoredPackets.ExportRetained().Contacts[header.FirstContact.Ordinal]!));
        for (int frame = 0; frame < 2400 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(revivedHp, battle.Combatants[0].CurrentHp);
            Assert.Equal(BattleOutcome.Active, battle.Outcome);
            Assert.Single(battle.Events, item => item.Kind == "Healing");
            Assert.Equal(JsonSerializer.Serialize(Capture(battle)), JsonSerializer.Serialize(Capture(restored)));
        }
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.True(battle.Combatants[0].IsAlive);
        Assert.Equal(revivedHp, battle.Combatants[0].CurrentHp);
        Assert.Single(battle.Events, item => item.Kind == "Healing");
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(JsonSerializer.Serialize(Capture(battle)), JsonSerializer.Serialize(Capture(restored)));
    }

    [Fact]
    public void SavedAngeloProhibitionDoesNotConsumeAnInvocationRoll()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, 3, false, 0x10);
        NativeBattleRandomState random = Capture(battle).Random!;
        for (int frame = 0; frame < 2400 && (frame == 0 || battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
        }
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(random, Capture(battle).Random);
    }

    private static void AdvanceUntilSupport(BattleSession battle)
    {
        for (int frame = 0; frame < 2400 && !HasSupport(battle); frame++)
        {
            battle.Tick();
        }
        Assert.True(HasSupport(battle), string.Join("; ", battle.Events.TakeLast(12).Select(entry => entry.Kind + ":" + entry.Message)));
        SupportInvocation support = ReadSupport(battle);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "EffectContact" && entry.Message == support.NativeAction.Name);
        Assert.Contains(battle.Events, item => item.Kind == "Damage" && item.ActorId == "enemy");
    }

    private sealed record SupportInvocation(string ActorId, string TargetId, BattleTargetRequest TargetRequest, BattleNativePlayerReaction? NativePlayerReaction, byte? NativeQueuedCommand, BattleNativeAction NativeAction, int ActionId);

    private static bool HasSupport(BattleSession battle) => battle.QueuedActions.Any(command => command.NativePlayerReaction is not null) || CurrentSupportAction(battle) is not null;

    private static BattleActionPresentation? CurrentSupportAction(BattleSession battle)
    {
        if (battle.ActiveAction is { NativePlayerReaction: not null } active)
        {
            return active;
        }
        BattleSession.ExpandedHostedInvocation? invocation = CurrentInvocation(battle);
        return invocation?.Command.NativePlayerReaction is not null ? invocation.Command : null;
    }

    private static BattleSession.ExpandedHostedInvocation? CurrentInvocation(BattleSession battle) => (BattleSession.ExpandedHostedInvocation?)typeof(BattleSession).GetProperty("CurrentExpandedHostedInvocation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle);

    private static BattleLimitFamily? ReadFamily(BattleSession battle, BattleModelOwner owner) => (BattleLimitFamily?)typeof(BattleSession).GetMethod("ReadActivePresentationLimitFamily", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [owner]);

    private static SupportInvocation ReadSupport(BattleSession battle)
    {
        IEnumerable<SupportInvocation> queued = battle.QueuedActions.Where(command => command.NativePlayerReaction is not null).Select(command => new SupportInvocation(command.ActorId, command.TargetId, command.TargetRequest, command.NativePlayerReaction, command.NativeQueuedCommand, command.NativeAction!, command.ActionId));
        if (CurrentSupportAction(battle) is { } active)
        {
            queued = queued.Append(new(active.ActorId, active.TargetId, active.TargetRequest, active.NativePlayerReaction, active.NativeQueuedCommand, active.NativeAction!, active.NativeAction!.ActionId));
        }
        return Assert.Single(queued);
    }

    private static BattleSession Create(GameAssets game, BattleAssets assets, byte learned, bool lethal, byte savedFlags = 0, bool expanded = false, bool periodic = false, bool acquired = true, int holderCharacter = 4)
    {
        var native = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, expanded ? 4 : 1, expanded ? 6 : 1, expanded ? 4 : 1, expanded);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals().WithExtendedRoster(true);
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, holderCharacter) with { NativeStatusPassiveFlags = 0 };
        BattleNativeCharacterGrowth growth = new(0, ImmutableArray.Create<byte>(0, 0, 0, 0, 0, 0));
        ushort maximumHp = rules.Project(vitals.Records[holderCharacter].MagicInventory, growth).MaximumHp;
        // Explicit fixed-damage fixture isolates the callback's RNG draw from physical hit/critical/formula draws.
        BattleNativeAction attack = assets.NativeActions.First(action => action.Command == 8) with { ActionId = 1000, AttackType = lethal ? (byte)27 : (byte)35, Power = 1, Hit = 0, HitCount = 1, AttackFlags = 0, Statuses0 = 0, Statuses1 = 0, Element = 0 };
        BattleCombatantDefinition rinoa = new() { Id = "rinoa", Name = "Rinoa", ModelId = holderCharacter, IsPlayer = true, MaximumHp = maximumHp, CurrentHp = 10, InitialAtb = 1234, NativePlayerStatRules = rules, NativeGrowth = growth, NativeDeathCount = 0, Speed = 1, NativeStatusResistances = new byte[40], NativeLimit = holderCharacter == 4 ? null : assets.ReadNativeLimitDefinition(holderCharacter), NativeLimitFamilies = holderCharacter == 4 ? null : [assets.ReadNativeLimitFamily(BattleLimitFamily.Combine)] };
        BattleCombatantDefinition Enemy(BattleCombatantDefinition source, string id, bool suspended)
        {
            NativeBattleEnemyTemplate template = assets.ReadEnemyTemplate(source.ModelId, checked((byte)source.Level)) with { NativeDrawTables = null };
            NativeBattleEnemyLevel stats = template.Levels[source.Level];
            return source with
            {
                Id = id,
                MaximumHp = stats.MaximumHp,
                CurrentHp = stats.MaximumHp,
                Attack = stats.Strength,
                Defense = stats.Vitality,
                Magic = stats.Magic,
                Spirit = stats.Spirit,
                Evasion = stats.Evasion,
                ElementDefenses = stats.ElementDefenses,
                DrawSpells = stats.DrawSpells,
                DrawGuardianForces = stats.DrawGuardianForces,
                Speed = 1,
                NativeDeathCount = 0,
                NativeTemplate = template,
                NativeStatusResistances = template.NativeStatusAllocation!.Resistances.ToArray(),
                InitialHidden = false,
                InitialSuspended = suspended,
                NativeAi = assets.ReadEnemyAi(source.ModelId, source.Level) with { Scripts = [[0], [0], [0], [0], [0]] }
            };
        }
        BattleCombatantDefinition enemy = Enemy(native.Selected.Combatants.First(actor => !actor.IsPlayer), "enemy", periodic);
        BattleCombatantDefinition[] actors = [rinoa, enemy];
        if (expanded)
        {
            BattleCombatantDefinition[] allies = Enumerable.Range(0, 3).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = $"Ally {index}", IsPlayer = true, ModelId = index, Speed = 1, NativeDeathCount = 0 }).ToArray();
            BattleCombatantDefinition[] extraEnemies = native.Selected.Combatants.Where(actor => !actor.IsPlayer).Skip(1).Select((source, index) => Enemy(source, $"enemy-{index}", true)).ToArray();
            actors = periodic ? [.. allies, rinoa, enemy, .. extraEnemies] : [rinoa, .. allies, enemy, .. extraEnemies];
        }
        actors = actors.Select(actor =>
        {
            if (!actor.IsPlayer)
            {
                return actor;
            }
            int weapon = vitals.GetBattleWeapon(actor.ModelId);
            return actor with { WeaponId = weapon, WeaponProfile = assets.ReadWeaponProfile(weapon), WeaponName = assets.WeaponDefinitions[weapon].Name, NativeAttack = assets.ReadPlayerNativeAttackData(vitals, actor.ModelId, weapon) };
        }).ToArray();
        BattleSession battle = new(native.Selected with
        {
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeFieldBattleRules = 0,
            NativeActions = [.. assets.NativeActions, attack],
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects(new ushort[] { 91, 92, 93, 94, attack.MagicId }.Where(effect => effect != 0)),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            Items = [],
            GuardianForces = [new(0, 100, 100, acquired) { KillCount = 0, DeathCount = 0 }],
            NativeSavedBattleFlags = savedFlags,
            NativeSavedBattleFlagsKnownMask = 255,
            LimitProgress = game.ReadNewGameLimitProgress() with { AngeloFlags = learned },
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            NativeSupportRules = periodic ? assets.ReadNativeSupportRules() : null,
            NativeCrtRandom = NativeCrtRandomState.FromColdProcess(),
            NativeItemNames = periodic ? Enumerable.Range(0, 256).Select(index => $"Item {index}").ToArray() : [],
            NativeGlobalMessages = periodic ? assets.ReadNativeGlobalBattleMessages() : [],
            NativeMessageSpeed = game.ReadNewGameInputConfiguration().BattleMessageSpeed,
            InitialRandom = expanded ? NativeBattleRandomState.ForecastPhase(1) : null,
            Combatants = actors
        }, periodic ? BattleAtbMode.Normal : BattleAtbMode.TurnBased);
        AttachAndEnter(battle, game, assets);
        if (periodic)
        {
            return battle;
        }
        BattleTargetRequest targets = expanded
            ? new(new BattleTargetSelection.Explicit([new BattlePartyActorReference("rinoa")]), BattleTargetEligibility.AiAvailable, false, BattleTargetReplacement.None)
            : NativeBattleTargetRequestAdapter.Decode(1, BattleTargetEligibility.SavedStatus, BattleTargetReplacement.UseCurrentBattlePolicy, samplesOriginalSlots: true);
        BattleAttackCommand command = new("enemy", "rinoa", targets, BattleAbility.EnemyAbility, attack.ActionId) { NativeQueuedCommand = 8, NativeAction = attack };
        NativeBattleRandomState random = new(Enumerable.Repeat((byte)1, 8).ToImmutableArray(), 0);
        return Restore(Capture(battle) with { QueuedActions = [command], Random = random });
    }

    private static void AttachAndEnter(BattleSession battle, GameAssets game, BattleAssets assets)
    {
        BattleDefinition definition = battle.ReadDefinition();
        int party = definition.Combatants.Count(actor => actor.IsPlayer);
        int enemies = definition.Combatants.Length - party;
        var native = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, party, enemies, Math.Min(4, enemies), party > 3 || enemies > 4);
        Dictionary<string, string> names = native.Selected.Combatants.Select((actor, index) => (actor.Id, Replacement: definition.Combatants[index].Id)).ToDictionary(pair => pair.Id, pair => pair.Replacement);
        NativeBattleGraphicsDefinition graphics = native.Graphics with
        {
            Bindings = native.Graphics.Bindings.Select(binding =>
            {
                BattleCombatantDefinition actor = definition.Combatants.Single(actor => actor.Id == names[binding.ActorId]);
                return actor.IsPlayer ? binding with { ActorId = actor.Id, ModelId = checked((byte)actor.ModelId), Resource = assets.ReadNativeGraphicsResource(actor) } : binding with { ActorId = actor.Id };
            }).ToImmutableArray(),
            InitialLoadOrder = native.Graphics.InitialLoadOrder.Select(id => names[id]).ToImmutableArray()
        };
        graphics = graphics with { LimitPresentations = assets.ReadNativeLimitPresentations(definition.Combatants, graphics.Bindings) };
        battle.AttachPresentation(new(definition, graphics));
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
        for (int callback = 0; callback < 1200 && !process.EntryReady; callback++)
        {
            process.Advance(static () => { }, static (_, _) => { });
        }
        Assert.True(process.EntryReady);
    }

    private static BattleSession Restore(BattleSnapshot snapshot)
    {
        BattlePresentationSnapshot presentation = Assert.IsType<BattlePresentationSnapshot>(snapshot.PresentationContinuation);
        Assert.NotNull(snapshot.LogicalContinuation);
        BattleSession restored = BattleSession.Restore(snapshot);
        restored.AttachRestoredPresentation(presentation.Resources, snapshot);
        Assert.NotNull(restored.Presentation);
        Assert.True(restored.NativeEntryReady);
        return restored;
    }

    private static BattleSnapshot Capture(BattleSession battle) => battle.Presentation is null ? battle.Capture() : battle.CaptureForContinuation();
}
