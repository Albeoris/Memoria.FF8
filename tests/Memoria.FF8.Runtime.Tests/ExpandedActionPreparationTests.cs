using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedActionPreparationTests
{
    private const int Card = (29 << 16) | 10;
    private const int MiniMog = (38 << 16) | 255;
    private const byte CardReward = 4;
    private static readonly MethodInfo AdvanceExpandedAction = typeof(BattleSession).GetMethod("AdvanceExpandedAction", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Theory]
    [InlineData(BattleAbility.Magic, 1, false, 1)]
    [InlineData(BattleAbility.Item, 1, false, 50)]
    [InlineData(BattleAbility.Draw, 1, false, 15)]
    [InlineData(BattleAbility.Draw, 1, true, 1)]
    [InlineData(BattleAbility.Draw, 64, false, 15)]
    public void FactoryFailureLeavesTheWholeLiveCommitBoundaryUnchanged(BattleAbility ability, int id, bool drawCast, int effect)
    {
        BattleSession live = Create(potionItem: ability == BattleAbility.Item);
        BattleCombatant[] actors = live.Combatants.ToArray();
        string? before = null;
        int calls = 0;
        InvalidOperationException failure = new("Injected presentation resource failure.");
        live.AttachExpandedEffectFactory((owner, caster, native, _, contacts, _) =>
        {
            calls++;
            Assert.NotSame(live, owner);
            Assert.NotSame(actors[0], caster);
            Assert.Same(owner.Combatants[0], caster);
            Assert.Equal(effect, native.MagicId);
            Assert.NotEmpty(contacts);
            Assert.Equal(before, Snapshot(live));
            throw failure;
        });
        live.QueueAction("caster", ability == BattleAbility.Item ? "caster" : "enemy", ability, id, drawCast, drawCastTargetId: drawCast ? "enemy" : null);
        ReachCommit(live);
        before = Snapshot(live);
        long resets = live.DiagnosticResetCount;

        Assert.Same(failure, Assert.Throws<TargetInvocationException>(() => Commit(live)).InnerException);
        Assert.Equal(1, calls);
        Assert.Equal(before, Snapshot(live));
        Assert.Null(live.ExpandedEffect);
        Assert.Equal(resets, live.DiagnosticResetCount);
        AssertActorIdentity(actors, live);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Hosted_candidate_retains_successful_draw_context_and_header(bool cast)
    {
        BattleSession candidate = Create();
        candidate.QueueAction("caster", "enemy", BattleAbility.Draw, 1, cast, drawCastTargetId: cast ? "enemy" : null);
        ReachCommit(candidate);
        (BattleActionPresentation command, NativeBattleActionHeaderPreparation header) = StageHostedInvocation(candidate);

        Assert.Equal(cast, command.DrawCast);
        Assert.Equal(cast ? "enemy" : null, command.DrawCastTargetId);
        Assert.False(command.NativeDraw!.Failed);
        Assert.Equal((byte)1, command.NativeDraw.Selection);
        Assert.Equal((byte)6, header.Command);
        Assert.Equal(NativeBattleCommandSequenceFixture.Values[cast ? 9 : 10], header.Sequence);
        Assert.Equal(cast ? (ushort)1 : (ushort)15, header.Effect);
    }

    [Fact]
    public void Hosted_candidate_preserves_queued_attack_header_when_Gunblade_uses_its_formula_clone()
    {
        BattleSession candidate = Create(gunblade: true);
        candidate.SetGunbladeMode(BattleGunbladeMode.Automatic);
        candidate.QueueAttack("caster", "enemy");
        ReachCommit(candidate);
        (BattleActionPresentation command, NativeBattleActionHeaderPreparation header) = StageHostedInvocation(candidate);

        Assert.Equal((byte)1, command.NativeQueuedCommand);
        Assert.Equal((byte)253, command.NativeAction!.Command);
        Assert.Equal((byte)1, header.Command);
        Assert.Equal((ushort)0, header.Effect);
        Assert.Equal(NativeBattleCommandSequenceFixture.Values[1], header.Sequence);
    }

    [Theory]
    [InlineData(BattleAbility.Draw, 1, 0x47EBD0u, 6)]
    [InlineData(BattleAbility.Magic, 1, 0x47E970u, 1)]
    [InlineData(BattleAbility.NativeCommand, Card, 0x47EC70u, 50)]
    [InlineData(BattleAbility.NativeCommand, MiniMog, 0x47EBD0u, 38)]
    public void Failed_hosted_control_executes_formula_provenance_without_contact_or_stock_debit(BattleAbility ability, int id, uint titleCallback, ushort titleIndex)
    {
        string target = id == MiniMog ? "caster" : "enemy";
        BattleSession candidate = Create(card: id == Card, miniMog: id == MiniMog, failSpecial: id is Card or MiniMog);
        candidate.QueueAction("caster", target, ability, id);
        ReachCommit(candidate);
        BattleCombatant caster = candidate.Combatants[0];
        BattleCombatant recipient = candidate.Combatants.Single(actor => actor.Id == target);
        if (ability is BattleAbility.Draw or BattleAbility.Magic)
        {
            caster.NativeStatus0 |= 0x10;
        }
        var before = candidate.Capture();
        (BattleActionPresentation command, NativeBattleActionHeaderPreparation header) = StageHostedInvocation(candidate);
        Assert.Equal((byte)0, header.Command);
        Assert.Equal((byte)10, header.Sequence);
        Assert.Equal((ushort)9, header.Action);
        Assert.Equal((ushort)0, header.Effect);
        Assert.Equal(titleCallback, header.Data!.ProducerAddress);
        Assert.Equal(titleIndex, header.Data.Argument);
        Assert.Equal(ability, command.Ability);
        Assert.Equal((byte)3, recipient.LastNativeAction!.AttackType);
        Assert.Equal(before.Combatants.Select(actor => actor.CurrentHp), candidate.Capture().Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(before.MagicVitals!.GetMagicCount(1, 1), candidate.MagicVitals!.GetMagicCount(1, 1));
        Assert.Equal(before.NativeGil, candidate.Capture().NativeGil);
        Assert.Equal(BattleActionPhase.Recovery, candidate.ActiveAction!.Phase);
        Assert.DoesNotContain(candidate.Events, item => item.Kind is "EffectContact" or "ActionComplete");

        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        object invocation = ReadHostedInvocation(candidate);
        Assert.Empty((ImmutableArray<ExpandedBattleContact>)invocation.GetType().GetProperty("Contacts")!.GetValue(invocation)!);
        NativeBattleGraphicsRoster roster = candidate.ReadExpandedPresentationRoster();
        object? bridge = typeof(BattleSession).GetMethod("PrepareExpandedHostedContactBridge", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [invocation, roster]);
        Assert.Null(bridge);
        typeof(BattleSession).GetMethod("BindExpandedHostedContactTracking", privateInstance)!.Invoke(candidate, [invocation, roster, bridge]);
        typeof(BattleSession).GetMethod("ValidateExpandedEffectContactCompletion", privateInstance)!.Invoke(candidate, []);
        typeof(BattleSession).GetField("expandedHostedActionToken", privateInstance)!.SetValue(candidate, 1L);
        NativeBattleModelLifecycleServices services = candidate.CaptureExpandedModelLifecycleServices(roster);
        services.EmitContact(new(new(candidate.ReadExpandedPresentationRow(target), 0, 0, 0, 0, 0, 0), new(byte.MaxValue, 0, 0, 0, 0, 0, 0)));
        MethodInfo acknowledge = typeof(BattleSession).GetMethod("ApplyExpandedHostedFrameReceipts", privateInstance)!;
        Assert.Contains("unexpected contact", Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => acknowledge.Invoke(candidate, [services, 1L])).InnerException).Message);
    }

    [Fact]
    public void A_missing_reservation_still_cancels_silenced_magic_before_hosted_admission()
    {
        BattleSession candidate = Create();
        candidate.QueueAction("caster", "enemy", BattleAbility.Magic, 1);
        ReachCommit(candidate);
        candidate.Combatants[0].NativeStatus0 |= 0x10;
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        string reservation = Assert.IsType<string>(candidate.ActiveAction!.MagicReservationId);
        typeof(BattleSession).GetMethod("ReleaseMagicReservation", privateInstance)!.Invoke(candidate, [reservation]);
        typeof(BattleSession).GetField("preparingExpandedHostedAction", privateInstance)!.SetValue(candidate, true);
        typeof(BattleSession).GetMethod("CommitExpandedAction", privateInstance)!.Invoke(candidate, [candidate.ActiveAction]);
        Assert.Null(candidate.ActiveAction);
        Assert.Empty(((System.Collections.IEnumerable)typeof(BattleSession).GetField("expandedHostedInvocations", privateInstance)!.GetValue(candidate)!).Cast<object>());
        Assert.Equal(3, candidate.GetAvailableMagicCasts("caster", 1));
    }

    [Fact]
    public void Ordered_hosted_contact_debits_its_own_enemy_draw_once_and_keeps_newer_pending_draw()
    {
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        BattleSession owner = Create();
        NativeBattleGraphicsRoster roster = owner.ReadExpandedPresentationRoster();
        byte row = owner.ReadExpandedPresentationRow("caster");
        BattleActionPresentation command = new("enemy", "caster", BattleActionPhase.Recovery, 1, BattleAbility.Magic, 1);
        NativeBattleContactPacket packet = new(new(row, 0, 0, 0, 0, 0, 0), new(byte.MaxValue, 0, 0, 0, 0, 0, 0));
        BattlePendingEnemyDraw original = new("enemy", 1, 1);
        BattlePendingEnemyDraw newer = new("enemy", 1, 2);
        FieldInfo pending = typeof(BattleSession).GetField("pendingEnemyDraw", privateInstance)!;
        pending.SetValue(owner, original);
        typeof(BattleSession).GetField("preparingExpandedHostedAction", privateInstance)!.SetValue(owner, true);
        object? captured = typeof(BattleSession).GetMethod("TakePendingHostedEnemyDraw", privateInstance)!.Invoke(owner, []);
        Assert.Same(original, captured);
        Assert.Null(pending.GetValue(owner));
        typeof(BattleSession).GetMethod("StageExpandedHostedInvocation", privateInstance)!.Invoke(owner, [command, new NativeBattleActionHeaderPreparation(3, 2, 5, -1, 1, 1, null), ImmutableArray.Create(new ExpandedBattleContact("caster", packet)), captured]);
        object invocation = Assert.Single(((System.Collections.IEnumerable)typeof(BattleSession).GetField("expandedHostedInvocations", privateInstance)!.GetValue(owner)!).Cast<object>());
        object bridge = typeof(BattleSession).GetMethod("PrepareExpandedHostedContactBridge", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [invocation, roster])!;
        typeof(BattleSession).GetMethod("BindExpandedHostedContactTracking", privateInstance)!.Invoke(owner, [invocation, roster, bridge]);
        typeof(BattleSession).GetMethod("InitializeExpandedPartyPresentation", privateInstance)!.Invoke(owner, []);
        typeof(BattleSession).GetField("expandedHostedActionToken", privateInstance)!.SetValue(owner, 1L);
        pending.SetValue(owner, newer);
        NativeBattleModelLifecycleServices services = owner.CaptureExpandedModelLifecycleServices(roster);
        services.EmitContact(packet);
        MethodInfo acknowledger = typeof(BattleSession).GetMethod("ApplyExpandedHostedFrameReceipts", privateInstance)!;
        int before = owner.GetAvailableMagicCasts("caster", 1);

        acknowledger.Invoke(owner, [services, 1L]);

        Assert.Equal(before - 1, owner.GetAvailableMagicCasts("caster", 1));
        Assert.Same(newer, pending.GetValue(owner));
        Assert.Single(owner.Events, item => item.Kind == "EnemyDrawStock");
        Assert.Throws<TargetInvocationException>(() => acknowledger.Invoke(owner, [services, 1L]));
        Assert.Equal(before - 1, owner.GetAvailableMagicCasts("caster", 1));
        Assert.Same(newer, pending.GetValue(owner));
        Assert.Single(owner.Events, item => item.Kind == "EnemyDrawStock");
    }

    private static (BattleActionPresentation Command, NativeBattleActionHeaderPreparation Header) StageHostedInvocation(BattleSession candidate)
    {
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(BattleSession).GetField("preparingExpandedHostedAction", privateInstance)!.SetValue(candidate, true);
        typeof(BattleSession).GetMethod("CommitExpandedAction", privateInstance)!.Invoke(candidate, [candidate.ActiveAction!]);
        object invocation = ReadHostedInvocation(candidate);
        BattleActionPresentation command = Assert.IsType<BattleActionPresentation>(invocation.GetType().GetProperty("Command")!.GetValue(invocation));
        NativeBattleActionHeaderPreparation header = Assert.IsType<NativeBattleActionHeaderPreparation>(invocation.GetType().GetProperty("Header")!.GetValue(invocation));
        return (command, header);
    }

    private static object ReadHostedInvocation(BattleSession candidate)
    {
        return Assert.Single(((System.Collections.IEnumerable)typeof(BattleSession).GetField("expandedHostedInvocations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(candidate)!).Cast<object>());
    }

    [Theory]
    [InlineData(BattleAbility.Attack, 0, "enemy")]
    [InlineData(BattleAbility.NativeCommand, Card, "enemy")]
    [InlineData(BattleAbility.NativeCommand, MiniMog, "caster")]
    public void CommandsWithoutPresentersPublishTheSameStateInPlace(BattleAbility ability, int id, string target)
    {
        BattleSession live = Create(id == Card, id == MiniMog);
        BattleCombatant[] actors = live.Combatants.ToArray();
        live.AttachExpandedEffectFactory((_, _, _, _, _, _) => throw new InvalidOperationException("This command does not own a presenter."));
        live.QueueAction("caster", target, ability, id);
        ReachCommit(live);
        long resets = live.DiagnosticResetCount;
        BattleSnapshot before = live.Capture();
        BattleSession control = BattleSession.Restore(before);
        Assert.Equal(Snapshot(control), Snapshot(live));

        Commit(control);
        Commit(live);

        Assert.Equal(Snapshot(control), Snapshot(live));
        AssertActorIdentity(actors, live);
        Assert.Equal(resets, live.DiagnosticResetCount);
        Assert.Null(live.ActiveAction);
        Assert.Single(live.Events, entry => entry.Kind == "ActionComplete");
        Assert.Equal(before.MagicVitals!.GetMagicCount(1, 1), live.MagicVitals!.GetMagicCount(1, 1));
        if (id == Card)
        {
            Assert.Equal(Math.Max(0, before.NativeCards!.AvailableCount(CardReward)) + 1, live.Capture().NativeCards!.AvailableCount(CardReward));
        }
        else if (id == MiniMog)
        {
            Assert.Equal(before.NativeGil!.Gil - 1000, live.Capture().NativeGil!.Gil);
            Assert.Equal(21, Assert.Single(live.GuardianForces).HitPoints);
        }
        else
        {
            Assert.True(actors[1].CurrentHp < before.Combatants[1].CurrentHp);
        }
    }

    private static void AssertActorIdentity(BattleCombatant[] actors, BattleSession live)
    {
        Assert.Equal(actors.Length, live.Combatants.Count);
        for (int index = 0; index < actors.Length; index++)
        {
            Assert.Same(actors[index], live.Combatants[index]);
        }
    }

    private static void ReachCommit(BattleSession battle)
    {
        battle.Tick();
        while (battle.ActiveAction is { RemainingFrames: > 1, Phase: BattleActionPhase.Windup })
        {
            battle.Tick();
        }
        Assert.Equal(BattleActionPhase.Windup, battle.ActiveAction?.Phase);
        Assert.Equal(1, battle.ActiveAction?.RemainingFrames);
    }

    private static void Commit(BattleSession battle) => AdvanceExpandedAction.Invoke(battle, [battle.ActiveAction!]);

    private static string Snapshot(BattleSession battle) => JsonSerializer.Serialize(battle.Capture());

    private static BattleSession Create(bool card = false, bool miniMog = false, bool potionItem = false, bool gunblade = false, bool failSpecial = false)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Stock(MagicSystemFixture.Create(), 1, 1, 3);
        BattleNativeAction fire = new(2, 1, 1, 1)
        {
            Name = "Fire", MagicId = 1, AttackType = 2, Power = 20, HitCount = 1, TargetFlags = 0x50, AttackFlags = 1,
            DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0,
            Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [.. new byte[60]], SupplementalRecord = []
        };
        BattleNativeAction potion = fire with { Name = "Potion", Command = 4, KernelSection = 7, MagicId = 50, AttackType = 4, Power = 20, TargetFlags = 0x10, AttackFlags = 2, RawRecord = [.. new byte[24]] };
        BattleNativeAction cardAction = fire with { Name = "Card", Command = 29, ActionId = 10, KernelSection = 10, KernelIndex = 10, MagicId = 1, AttackType = 17, Power = 0, AttackFlags = 3, RawRecord = [1] };
        BattleNativeAction dance = fire with { Name = "MiniMog", Command = 240, ActionId = 6, KernelSection = 9, KernelIndex = 6, MagicId = 96, AttackType = 24, Power = 0, TargetFlags = null, RawRecord = [1] };
        string?[] messages = new string?[119];
        messages[9] = "Converted";
        messages[11] = " 0123456789";
        messages[16] = "card";
        messages[19] = "Drew magic";
        messages[50] = "Card failed";
        messages[53] = "Not enough gil";
        messages[54] = "MiniMog restored GF HP";
        messages[71] = "";
        messages[100] = "Silence";
        NativePocketStationState pocket = new(NativeInputConfiguration.OriginalInitHash, ImmutableArray.Create(new byte[64]).SetItem(0, 0x0D).SetItem(1, 20));
        ImmutableArray<BattleNativeCommandBinding> commands = card ? [new(29, "Card", 0x50, 10)] : miniMog ? [new(38, "MiniMog", 0xA0, 255)] : [];
        BattleCombatantDefinition caster = new() { Id = "caster", Name = "Caster", IsPlayer = true, ModelId = 1, MagicRecordId = 1, MaximumHp = 2000, CurrentHp = 500, Level = 10, Magic = 100, Attack = 40, Speed = 1, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Attack, BattleAbility.Draw, BattleAbility.Item, BattleAbility.Magic, BattleAbility.NativeCommand], NativeCommands = commands, EquipmentIds = miniMog ? [0] : [] };
        if (potionItem)
        {
            caster = WithProjectedItemUser(caster, vitals);
        }
        if (gunblade)
        {
            caster = caster with { ModelId = 0, WeaponId = 0, NativeAttack = fire with { Command = 1, ActionId = 0, AttackType = 1, DamagePresentation = 4 } };
        }
        string[] commandNames = Enumerable.Repeat(string.Empty, 39).ToArray();
        commandNames[6] = "Draw";
        commandNames[9] = "Cast";
        commandNames[38] = "MiniMog";
        return new(new BattleDefinition
        {
            UsesExpandedRoster = true,
            DrawOncePerSpell = true,
            MagicVitals = vitals,
            NativeKnownMagic = new(NativeInputConfiguration.OriginalInitHash, [0, 0]),
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeCommandNames = commandNames,
            NativeCards = NativeCardInventory.CreateNewGame(),
            NativeGil = new(failSpecial && miniMog ? 0u : 10000u, 0),
            NativePocketStation = pocket,
            NativeSavedBattleFlags = 0,
            NativeSavedBattleFlagsKnownMask = 1,
            NativeMessageSpeed = 1,
            NativeGlobalMessages = messages,
            NativeCardNames = Enumerable.Range(0, 110).Select(index => (string?)$"Card {index}").ToArray(),
            GuardianForces = [new(0, 1, 100, miniMog)],
            Items = [new(1, 2)],
            NativeActions = [fire, potion, cardAction, dance],
            ActionDefinitions =
            [
                new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Native, fire.Power, false) { NativeAction = fire },
                new(BattleAbility.Item, 1, "Potion", BattleActionEffect.Native, potion.Power, true) { NativeAction = potion }
            ],
            Combatants =
            [
                caster,
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 60000, CurrentHp = card ? 1 : 60000, Level = 1, Speed = 1, NativeDrawSlots = [new(1, 0), new(64, 0), new(0, 0), new(0, 0)], NativeSpecial = failSpecial && card ? new(255, 255, [0, 0, 0], 10) : new(CardReward, CardReward, [0, 0, 0], 10) }
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static BattleCombatantDefinition WithProjectedItemUser(BattleCombatantDefinition actor, NativeCharacterVitals vitals)
    {
        byte[] character = new byte[36];
        for (int offset = 12; offset < character.Length; offset += 4)
        {
            character[offset + 1] = 1;
            character[offset + 2] = 40;
            character[offset + 3] = 1;
        }
        var spells = Enumerable.Repeat(ImmutableArray.Create(new byte[60]), 57).ToImmutableArray();
        var passives = Enumerable.Repeat(ImmutableArray.Create(new byte[8]), 19).ToImmutableArray();
        BattleNativePlayerStatRules rules = new(1, 10, character.ToImmutableArray(), [.. new byte[12]], spells, passives, [.. new byte[20]], 0, vitals.Definitions) { NativeStatusPassiveFlags = 0 };
        BattleNativeCharacterGrowth growth = new(2000, [.. new byte[6]]);
        IReadOnlyList<NativeMagicStock> stock = vitals.GetMagicInventory(1);
        BattleNativePlayerStatistics projected = rules.Project(stock, growth);
        return actor with
        {
            NativePlayerStatRules = rules,
            NativeGrowth = growth,
            NativeStatusResistances = rules.ProjectStatusResistances(stock).ToArray(),
            MaximumHp = projected.MaximumHp,
            CurrentHp = projected.MaximumHp / 2,
            Attack = projected.Strength,
            Defense = projected.Vitality,
            Magic = projected.Magic,
            Spirit = projected.Spirit,
            Speed = Math.Max(1, projected.Speed),
            Luck = projected.Luck,
            Hit = projected.Hit,
            Evasion = projected.Evasion,
            ElementDefenses = projected.ElementDefenses.ToArray(),
            ElementAttack = projected.ElementAttack,
            ElementAttackPercent = projected.ElementAttackPercent
        };
    }
}
