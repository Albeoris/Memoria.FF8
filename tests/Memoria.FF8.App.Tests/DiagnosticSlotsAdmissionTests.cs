using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticSlotsAdmissionTests
{
    [Theory]
    [InlineData(51, 280)]
    [InlineData(52, 189)]
    [InlineData(53, 288)]
    [InlineData(56, 85)]
    public void Original_special_rows_accept_only_matching_command16_Limit_declarations(byte action, ushort effect)
    {
        DiagnosticSlotsDeclaration declaration = new(action, 256, 5);
        BattleInput input = Input(action);
        declaration.Validate(input, effect, action, 16);
        Assert.Throws<InvalidDataException>(() => declaration.Validate(input with { Ability = BattleAbility.Magic }, effect, action, 2));
        Assert.Throws<InvalidDataException>(() => (declaration with { MaximumRolls = 257 }).Validate(input, effect, action, 16));
        Assert.Throws<InvalidDataException>(() => (declaration with { MaximumSeconds = double.NaN }).Validate(input, effect, action, 16));
        Assert.Throws<InvalidDataException>(() => (declaration with { MaximumSeconds = 6 }).Validate(input, effect, action, 16));
    }

    [Fact]
    public void Pure_validation_uses_existing_native_roll_rules_and_preserves_independent_start_and_learning()
    {
        var fixture = Fixture();
        DiagnosticSlotsAdmission admission = new(fixture.Source, fixture.Empty);
        string before = JsonSerializer.Serialize(fixture.Empty);
        admission.Rolled(fixture.Roll);
        Assert.True(admission.Matched);
        admission.Did(fixture.Do);
        DiagnosticSlotsAdmissionValidation.ValidateReceipt(admission.Receipt, fixture.Source, fixture.Empty.Start, fixture.Empty.Selection, fixture.Empty.Policy, fixture.Empty.Input);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Empty));
        Assert.Equal(fixture.Roll.After.Random.Indexes, admission.Receipt.Do!.After.Random.Indexes);
        Assert.Equal(fixture.Empty.Start.LearnedFlags, admission.Receipt.Do.After.LearnedFlags);
        Assert.Throws<InvalidDataException>(() => admission.Rolled(fixture.Roll with { Index = 2 }));
        Assert.Throws<InvalidDataException>(() => admission.Did(fixture.Do));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("model")]
    [InlineData("physical")]
    [InlineData("lifecycle")]
    [InlineData("restore")]
    [InlineData("hp")]
    [InlineData("learned")]
    [InlineData("callback")]
    [InlineData("deadline")]
    [InlineData("crisis")]
    [InlineData("card")]
    [InlineData("pending")]
    [InlineData("result")]
    [InlineData("owned")]
    [InlineData("random")]
    [InlineData("index")]
    public void Roll_receipts_reject_unowned_result_state_random_or_lifecycle(string fault)
    {
        var fixture = Fixture();
        DiagnosticSlotsRollReceipt roll = fixture.Roll;
        roll = fault switch
        {
            "owner" => roll with { After = roll.After with { ActorId = "squall" } },
            "model" => roll with { After = roll.After with { ModelId = 0 } },
            "physical" => roll with { After = roll.After with { ActorIndex = 2 } },
            "lifecycle" => roll with { After = roll.After with { LifecycleId = Guid.NewGuid() } },
            "restore" => roll with { After = roll.After with { RestoreCount = 1 } },
            "hp" => roll with { After = roll.After with { HitPoints = 2 } },
            "learned" => roll with { After = roll.After with { LearnedFlags = 1 } },
            "callback" => roll with { After = roll.After with { Callback = 11 } },
            "deadline" => roll with { After = roll.After with { Seconds = 106 } },
            "crisis" => roll with { After = roll.After with { Crisis = 3 } },
            "card" => roll with { After = roll.After with { CardSlots = true } },
            "pending" => roll with { After = roll.After with { PendingDeal = true } },
            "result" => roll with { Result = new(52, 1), After = roll.After with { OwnedRoll = new(52, 1) } },
            "owned" => roll with { After = roll.After with { OwnedRoll = null } },
            "random" => roll with { After = roll.After with { Random = roll.Before.Random } },
            "index" => roll with { Index = 2 },
            _ => throw new InvalidDataException()
        };
        DiagnosticSlotsAdmission admission = new(fixture.Source, fixture.Empty);
        Assert.Throws<InvalidDataException>(() => admission.Rolled(roll));
    }

    [Theory]
    [InlineData("used")]
    [InlineData("action")]
    [InlineData("effect")]
    [InlineData("owner")]
    [InlineData("target")]
    [InlineData("mask")]
    [InlineData("command")]
    [InlineData("unconsumed")]
    [InlineData("ready")]
    [InlineData("random")]
    public void Do_requires_current_latched_result_and_actual_native_command_consumption(string fault)
    {
        var fixture = Fixture();
        DiagnosticSlotsAdmission admission = new(fixture.Source, fixture.Empty);
        admission.Rolled(fixture.Roll);
        DiagnosticSlotsDoReceipt accepted = fault switch
        {
            "used" => fixture.Do with { UsedRoll = new(52, 1) },
            "action" => fixture.Do with { Action = 52 },
            "effect" => fixture.Do with { Effect = 189 },
            "owner" => fixture.Do with { ActorId = "squall" },
            "target" => fixture.Do with { TargetIndex = 6 },
            "mask" => fixture.Do with { TargetMask = 2 },
            "command" => fixture.Do with { Command = 2 },
            "unconsumed" => fixture.Do with { After = fixture.Do.After with { OwnedRoll = fixture.Roll.Result } },
            "ready" => fixture.Do with { After = fixture.Do.After with { Ready = true } },
            "random" => fixture.Do with { After = fixture.Do.After with { Random = fixture.Roll.Before.Random } },
            _ => throw new InvalidDataException()
        };
        Assert.Throws<InvalidDataException>(() => admission.Did(accepted));
    }

    [Fact]
    public void Coherent_altered_rule_or_random_receipts_cannot_replace_the_initialized_source_or_independent_anchor()
    {
        var fixture = Fixture();
        DiagnosticSlotsAdmission valid = new(fixture.Source, fixture.Empty);
        valid.Rolled(fixture.Roll);
        valid.Did(fixture.Do);
        DiagnosticSlotsAdmissionReceipt receipt = valid.Receipt;
        DiagnosticSlotsSource altered = fixture.Source with { SpellTable = fixture.Source.SpellTable.SetItem(0, 52), Rules = fixture.Source.Rules with { SlotsSpellBytes = fixture.Source.SpellTable.SetItem(0, 52), SlotsSelectionBytes = fixture.Source.SelectionTable.Concat(fixture.Source.SpellTable.SetItem(0, 52)).ToImmutableArray() } };
        altered = altered with { Fingerprint = altered.ComputeFingerprint() };
        altered.Validate();
        Assert.Throws<InvalidDataException>(() => DiagnosticSlotsAdmissionValidation.ValidateReceipt(receipt with { SourceFingerprint = altered.Fingerprint }, fixture.Source, fixture.Empty.Start, fixture.Empty.Selection, fixture.Empty.Policy, fixture.Empty.Input));
        NativeBattleRandomState fakeRandom = NativeBattleRandomState.Initialize(99);
        DiagnosticSlotsAdmissionReceipt fake = receipt with { Start = receipt.Start with { Random = fakeRandom }, Selection = receipt.Selection with { RandomBefore = fakeRandom, RandomAfter = fakeRandom } };
        Assert.Throws<InvalidDataException>(() => DiagnosticSlotsAdmissionValidation.ValidateReceipt(fake, fixture.Source, fixture.Empty.Start, fixture.Empty.Selection, fixture.Empty.Policy, fixture.Empty.Input));
        Assert.Throws<InvalidDataException>(() => DiagnosticSlotsAdmissionValidation.ValidateReceipt(receipt with { Input = receipt.Input with { ActorId = "squall" } }, fixture.Source, fixture.Empty.Start, fixture.Empty.Selection, fixture.Empty.Policy, fixture.Empty.Input));
    }

    [Fact]
    public void Ordinary_unmatched_rolls_preserve_every_cursor_step_and_cannot_Do_or_exceed_the_bound()
    {
        var fixture = Fixture(1);
        DiagnosticSlotsAdmission admission = new(fixture.Source, fixture.Empty with { Declaration = fixture.Empty.Declaration with { MaximumRolls = 1 } });
        admission.Rolled(fixture.Roll);
        Assert.False(admission.Matched);
        Assert.Single(admission.Receipt.Rolls);
        Assert.Throws<InvalidDataException>(() => admission.Did(fixture.Do));
        Assert.Throws<InvalidDataException>(() => admission.Rolled(fixture.Roll with { Index = 2, Before = fixture.Roll.After }));
    }

    [Fact]
    public void Driver_optin_preserves_ordinary_paths_and_rejects_protocol_or_repair_substitution()
    {
        DiagnosticBattleDriverRecipe ordinary = new(new(BattleInputKind.Action, ActorId: "p", TargetId: "e", Ability: BattleAbility.Magic, ActionId: 1), 2);
        ordinary.Validate();
        DiagnosticBattleDriverRecipe slots = new(Input(51), 280) { SlotsAdmission = new(51, 256, 5), LimitSelection = new(4, 4, 256), ExpectedNativeAction = 51, ExpectedNativeCommand = 16 };
        slots.Validate();
        Assert.Throws<InvalidDataException>(() => (slots with { LimitSelection = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (slots with { Repairs = [new("selphie", 1)] }).Validate());
        Assert.Throws<InvalidDataException>(() => (slots with { SupportingEffects = [2] }).Validate());
    }

    [Fact]
    public void Native_crisis_replay_uses_actual_HP_status_inputs_and_each_real_selection_draw()
    {
        var fixture = Fixture();
        DiagnosticSlotsObservation start = fixture.Empty.Start with { LimitOncePerTurn = false, RetainedCrisis = null };
        NativeBattleRandomState random = start.Random;
        List<byte> choices = [];
        for (int index = 0; index < 256; index++)
        {
            random = random.Draw(out byte draw);
            byte crisis = NativeBattleLimitRules.CrisisLevel(fixture.Source.Rules, start.HitPoints, start.MaximumHitPoints, start.Statuses0, start.Statuses1, start.DeadParty, draw, false);
            choices.Add(crisis);
            if (crisis > 0)
            {
                break;
            }
        }
        Assert.True(choices[^1] > 0);
        BattleDiagnosticLimitSelectionReceipt selected = fixture.Empty.Selection with { CrisisSelections = choices.ToImmutableArray(), RandomAfter = random };
        DiagnosticSlotsAdmissionReceipt empty = fixture.Empty with { Start = start, Policy = new(1, 4, 256), Selection = selected };
        _ = new DiagnosticSlotsAdmission(fixture.Source, empty);
        Assert.Throws<InvalidDataException>(() => new DiagnosticSlotsAdmission(fixture.Source, empty with { Selection = selected with { RandomAfter = start.Random } }));
        Assert.Throws<InvalidDataException>(() => new DiagnosticSlotsAdmission(fixture.Source, empty with { Start = start with { HitPoints = start.MaximumHitPoints } }));
    }

    [Fact]
    public void Consecutive_Do_over_rolls_cannot_skip_the_retained_cursor_or_row_result()
    {
        var fixture = Fixture(1);
        DiagnosticSlotsAdmission admission = new(fixture.Source, fixture.Empty with { Declaration = fixture.Empty.Declaration with { MaximumRolls = 2 } });
        admission.Rolled(fixture.Roll);
        DiagnosticSlotsObservation before = fixture.Roll.After with { Seconds = 100.03 };
        NativeBattleRandomState random = before.Random.Draw(out byte a).Draw(out byte b).Draw(out byte c).Draw(out byte d);
        BattleNativeSlotsRoll result = NativeBattleSlotsRules.Roll(fixture.Source.Rules, before.Level, before.Crisis, a, b, c, d);
        DiagnosticSlotsRollReceipt second = new(2, before, before with { Seconds = 100.04, Random = random, OwnedRoll = result }, result);
        Assert.Throws<InvalidDataException>(() => admission.Rolled(second with { Before = fixture.Roll.Before }));
        admission.Rolled(second);
        Assert.Equal(2, admission.Receipt.Rolls.Length);
        Assert.False(admission.Matched);
    }

    [Fact]
    public void Batch_two_reselection_replays_each_native_epoch_and_binds_the_independent_selection_log()
    {
        var fixture = BatchFixture();
        string sourceBefore = JsonSerializer.Serialize(fixture.Source);
        string receiptBefore = JsonSerializer.Serialize(fixture.Receipt);
        DiagnosticSlotsAdmission admission = ReplayBatch(fixture.Source, fixture.Receipt);
        Assert.True(admission.Matched);
        Assert.True(fixture.Receipt.Reselections.Length >= 2);
        Assert.Equal(fixture.Receipt.Reselections.Length, admission.Receipt.Reselections.Length);
        Assert.All(admission.Receipt.Reselections, transition =>
        {
            Assert.Equal(0, transition.AfterRollIndex % 2);
            Assert.NotNull(transition.Before.OwnedRoll);
            Assert.Null(transition.After.OwnedRoll);
            Assert.True(transition.Before.Ready);
            Assert.True(transition.After.Ready);
            Assert.Equal(transition.Before.HitPoints, transition.After.HitPoints);
            Assert.Equal(transition.Before.LearnedFlags, transition.After.LearnedFlags);
        });
        int draws = fixture.Receipt.Selection.CrisisSelections.Length + fixture.Receipt.Reselections.Sum(value => value.Selection.CrisisSelections.Length);
        Assert.Equal(fixture.Receipt.Policy.MaximumSelections - draws, admission.RemainingSelections);
        DiagnosticSlotsAdmissionValidation.ValidateReceipt(fixture.Receipt, fixture.Source, fixture.Receipt.Start, fixture.Receipt.Selection, fixture.Receipt.Policy, fixture.Receipt.Input, fixture.Log);
        Assert.Equal(sourceBefore, JsonSerializer.Serialize(fixture.Source));
        Assert.Equal(receiptBefore, JsonSerializer.Serialize(fixture.Receipt));
        Assert.Throws<InvalidDataException>(() => admission.Reselected(fixture.Receipt.Reselections[^1]));
        Assert.Throws<InvalidDataException>(() => admission.Did(fixture.Receipt.Do!));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("order")]
    [InlineData("independent-missing")]
    [InlineData("independent-random")]
    [InlineData("independent-owner")]
    public void Batch_receipt_cannot_substitute_or_omit_ordered_independent_reselection_evidence(string fault)
    {
        var fixture = BatchFixture();
        DiagnosticSlotsAdmissionReceipt receipt = fixture.Receipt;
        BattleDiagnosticLimitSelectionReceipt[] log = fixture.Log;
        if (fault == "missing")
        {
            receipt = receipt with { Reselections = receipt.Reselections.Skip(1).ToArray() };
            log = log.Skip(1).ToArray();
        }
        else if (fault == "extra")
        {
            receipt = receipt with { Reselections = [.. receipt.Reselections, receipt.Reselections[^1]] };
            log = [.. log, log[^1]];
        }
        else if (fault == "order")
        {
            receipt = receipt with { Reselections = receipt.Reselections.Reverse().ToArray() };
            log = log.Reverse().ToArray();
        }
        else if (fault == "independent-missing")
        {
            log = [];
        }
        else if (fault == "independent-random")
        {
            log = [log[0] with { RandomAfter = log[0].RandomBefore }, .. log.Skip(1)];
        }
        else
        {
            log = [log[0] with { ActorId = "squall" }, .. log.Skip(1)];
        }
        Assert.Throws<InvalidDataException>(() => DiagnosticSlotsAdmissionValidation.ValidateReceipt(receipt, fixture.Source, fixture.Receipt.Start, fixture.Receipt.Selection, fixture.Receipt.Policy, fixture.Receipt.Input, log));
    }

    [Theory]
    [InlineData("early")]
    [InlineData("late")]
    [InlineData("actor")]
    [InlineData("physical")]
    [InlineData("lifecycle")]
    [InlineData("restore")]
    [InlineData("hp")]
    [InlineData("learned")]
    [InlineData("callback")]
    [InlineData("deadline")]
    [InlineData("random-before")]
    [InlineData("random-after")]
    [InlineData("crisis")]
    [InlineData("failed-clear")]
    [InlineData("ready")]
    [InlineData("rejected")]
    [InlineData("past-first-selection")]
    public void Reselection_requires_exact_two_misses_same_ready_owner_and_actual_native_refresh(string fault)
    {
        var fixture = BatchFixture();
        DiagnosticSlotsAdmission admission = new(fixture.Source, fixture.Receipt with { Rolls = [], Reselections = [], Do = null });
        admission.Rolled(fixture.Receipt.Rolls[0]);
        DiagnosticSlotsReselectionReceipt transition = fixture.Receipt.Reselections[0];
        if (fault != "early")
        {
            admission.Rolled(fixture.Receipt.Rolls[1]);
            Assert.True(admission.RequiresReselection);
        }
        transition = fault switch
        {
            "early" => transition with { AfterRollIndex = 1, Before = fixture.Receipt.Rolls[0].After },
            "late" => transition with { AfterRollIndex = 3 },
            "actor" => transition with { After = transition.After with { ActorId = "squall" } },
            "physical" => transition with { After = transition.After with { ActorIndex = 0 } },
            "lifecycle" => transition with { After = transition.After with { LifecycleId = Guid.NewGuid() } },
            "restore" => transition with { After = transition.After with { RestoreCount = 1 } },
            "hp" => transition with { After = transition.After with { HitPoints = 2 } },
            "learned" => transition with { After = transition.After with { LearnedFlags = 1 } },
            "callback" => transition with { After = transition.After with { Callback = 11 } },
            "deadline" => transition with { After = transition.After with { Seconds = 106 } },
            "random-before" => transition with { Selection = transition.Selection with { RandomBefore = fixture.Receipt.Start.Random } },
            "random-after" => transition with { Selection = transition.Selection with { RandomAfter = transition.Selection.RandomBefore } },
            "crisis" => transition with { After = transition.After with { Crisis = 3 } },
            "failed-clear" => transition with { After = transition.After with { OwnedRoll = transition.Before.OwnedRoll } },
            "ready" => transition with { After = transition.After with { Ready = false } },
            "rejected" => transition with { Selection = transition.Selection with { Accepted = false } },
            "past-first-selection" => transition with { Selection = transition.Selection with { CrisisSelections = transition.Selection.CrisisSelections.Add(4) } },
            _ => throw new InvalidDataException()
        };
        Assert.Throws<InvalidDataException>(() => admission.Reselected(transition));
        Assert.Empty(admission.Receipt.Reselections);
    }

    [Fact]
    public void Batch_boundary_forbids_a_third_roll_without_refresh_and_forbids_the_previous_owned_roll_after_refresh()
    {
        var fixture = BatchFixture();
        DiagnosticSlotsAdmission admission = new(fixture.Source, fixture.Receipt with { Rolls = [], Reselections = [], Do = null });
        admission.Rolled(fixture.Receipt.Rolls[0]);
        admission.Rolled(fixture.Receipt.Rolls[1]);
        Assert.True(admission.RequiresReselection);
        Assert.Throws<InvalidDataException>(() => admission.Rolled(fixture.Receipt.Rolls[2]));
        admission.Reselected(fixture.Receipt.Reselections[0]);
        Assert.False(admission.RequiresReselection);
        Assert.Throws<InvalidDataException>(() => admission.Reselected(fixture.Receipt.Reselections[0]));
        Assert.Throws<InvalidDataException>(() => admission.Rolled(fixture.Receipt.Rolls[2] with { Before = fixture.Receipt.Rolls[1].After }));
        admission.Rolled(fixture.Receipt.Rolls[2]);
        Assert.Equal(3, admission.Receipt.Rolls.Length);
    }

    [Fact]
    public void Selection_budget_is_global_across_initial_selection_and_all_reselection_epochs()
    {
        var fixture = BatchFixture();
        int first = fixture.Receipt.Selection.CrisisSelections.Length;
        DiagnosticSlotsAdmissionReceipt empty = fixture.Receipt with { Rolls = [], Reselections = [], Do = null, Policy = fixture.Receipt.Policy with { MaximumSelections = first } };
        DiagnosticSlotsAdmission admission = new(fixture.Source, empty);
        admission.Rolled(fixture.Receipt.Rolls[0]);
        admission.Rolled(fixture.Receipt.Rolls[1]);
        Assert.Equal(0, admission.RemainingSelections);
        Assert.Throws<InvalidDataException>(() => admission.Reselected(fixture.Receipt.Reselections[0]));
        int total = first + fixture.Log.Sum(value => value.CrisisSelections.Length);
        DiagnosticSlotsAdmissionReceipt shortened = fixture.Receipt with { Policy = fixture.Receipt.Policy with { MaximumSelections = total - 1 } };
        Assert.Throws<InvalidDataException>(() => DiagnosticSlotsAdmissionValidation.ValidateReceipt(shortened, fixture.Source, shortened.Start, shortened.Selection, shortened.Policy, shortened.Input, fixture.Log));
        DiagnosticSlotsAdmission rollLimited = new(fixture.Source, fixture.Receipt with { Rolls = [], Reselections = [], Do = null, Declaration = fixture.Receipt.Declaration with { MaximumRolls = 2 } });
        rollLimited.Rolled(fixture.Receipt.Rolls[0]);
        rollLimited.Rolled(fixture.Receipt.Rolls[1]);
        rollLimited.Reselected(fixture.Receipt.Reselections[0]);
        Assert.Throws<InvalidDataException>(() => rollLimited.Rolled(fixture.Receipt.Rolls[2]));
    }

    [Fact]
    public void Reselection_is_closed_optin_and_cannot_reuse_a_once_per_turn_crisis_latch()
    {
        var fixture = Fixture(1);
        DiagnosticSlotsDeclaration batch = fixture.Empty.Declaration with { Reselection = DiagnosticSlotsDeclaration.ReadySelfBatch2 };
        batch.Validate(fixture.Empty.Input, batch.Effect, batch.MagicAction, 16);
        Assert.Throws<InvalidDataException>(() => (batch with { Reselection = "arbitrary-refresh" }).Validate(fixture.Empty.Input, batch.Effect, batch.MagicAction, 16));
        Assert.Throws<InvalidDataException>(() => new DiagnosticSlotsAdmission(fixture.Source, fixture.Empty with { Declaration = batch }));
        DiagnosticSlotsAdmission ordinary = new(fixture.Source, fixture.Empty);
        Assert.False(ordinary.Receipt.Declaration.ReselectsAfterTwoMisses);
        Assert.Null(ordinary.Receipt.Declaration.Reselection);
        Assert.Empty(ordinary.Receipt.Reselections);
        Assert.False(ordinary.RequiresReselection);
        Assert.Throws<InvalidDataException>(() => ordinary.Reselected(BatchFixture().Receipt.Reselections[0]));
    }

    [Fact]
    public void First_matching_roll_after_multiple_epochs_is_latched_until_its_own_Do_consumes_it()
    {
        var fixture = BatchFixture();
        DiagnosticSlotsAdmission admission = ReplayBatch(fixture.Source, fixture.Receipt, false);
        Assert.True(admission.Matched);
        Assert.False(admission.RequiresReselection);
        Assert.Null(admission.Receipt.Do);
        Assert.Throws<InvalidDataException>(() => admission.Reselected(fixture.Receipt.Reselections[^1]));
        Assert.Throws<InvalidDataException>(() => admission.Rolled(fixture.Receipt.Rolls[^1] with { Index = fixture.Receipt.Rolls.Length + 1 }));
        Assert.Throws<InvalidDataException>(() => admission.Did(fixture.Receipt.Do! with { UsedRoll = fixture.Receipt.Rolls[0].Result }));
        Assert.Throws<InvalidDataException>(() => admission.Did(fixture.Receipt.Do! with { Before = fixture.Receipt.Rolls[0].After }));
        admission.Did(fixture.Receipt.Do!);
        Assert.Null(admission.Receipt.Do!.After.OwnedRoll);
        Assert.False(admission.Receipt.Do.After.Ready);
        Assert.Equal((byte)0, admission.Receipt.Do.After.Crisis);
    }

    private static DiagnosticSlotsAdmission ReplayBatch(DiagnosticSlotsSource source, DiagnosticSlotsAdmissionReceipt receipt, bool doCommand = true)
    {
        DiagnosticSlotsAdmission admission = new(source, receipt with { Rolls = [], Reselections = [], Do = null });
        int next = 0;
        foreach (DiagnosticSlotsRollReceipt roll in receipt.Rolls)
        {
            if (next < receipt.Reselections.Length && receipt.Reselections[next].AfterRollIndex == roll.Index - 1)
            {
                admission.Reselected(receipt.Reselections[next++]);
            }
            admission.Rolled(roll);
        }
        if (doCommand)
        {
            admission.Did(receipt.Do!);
        }
        return admission;
    }

    private static (DiagnosticSlotsSource Source, DiagnosticSlotsAdmissionReceipt Receipt, BattleDiagnosticLimitSelectionReceipt[] Log) BatchFixture()
    {
        var original = Fixture(1);
        byte[] spells = original.Source.SpellTable.ToArray();
        //Synthetic pair1 first matches after multiple epochs; all observations below use the native rule functions.
        spells[2] = 51;
        DiagnosticSlotsSource source = original.Source with { SpellTable = spells.ToImmutableArray(), Rules = original.Source.Rules with { SlotsSpellBytes = spells.ToImmutableArray(), SlotsSelectionBytes = original.Source.SelectionTable.Concat(spells).ToImmutableArray() } };
        source = source with { Fingerprint = source.ComputeFingerprint() };
        source.Validate();
        DiagnosticSlotsObservation start = original.Empty.Start with { LimitOncePerTurn = false, RetainedCrisis = null };
        BattleDiagnosticLimitSelection policy = new(4, 4, 256);
        (BattleDiagnosticLimitSelectionReceipt selection, DiagnosticSlotsObservation current) = SelectBatch(source, start, policy);
        List<DiagnosticSlotsRollReceipt> rolls = [];
        List<DiagnosticSlotsReselectionReceipt> transitions = [];
        int epochRolls = 0;
        int selections = selection.CrisisSelections.Length;
        while (rolls.Count < 256)
        {
            if (epochRolls == 2)
            {
                (BattleDiagnosticLimitSelectionReceipt next, DiagnosticSlotsObservation after) = SelectBatch(source, current, policy with { MaximumSelections = 256 - selections });
                transitions.Add(new(rolls.Count, current, next, after));
                selections += next.CrisisSelections.Length;
                current = after;
                epochRolls = 0;
            }
            NativeBattleRandomState random = current.Random.Draw(out byte a).Draw(out byte b).Draw(out byte c).Draw(out byte d);
            BattleNativeSlotsRoll result = NativeBattleSlotsRules.Roll(source.Rules, current.Level, current.Crisis, a, b, c, d);
            DiagnosticSlotsObservation afterRoll = current with { Seconds = current.Seconds + 0.001, Random = random, OwnedRoll = result };
            rolls.Add(new(rolls.Count + 1, current, afterRoll, result));
            current = afterRoll;
            epochRolls++;
            if (result.MagicId == source.MagicAction)
            {
                break;
            }
        }
        Assert.Equal(source.MagicAction, rolls[^1].Result.MagicId);
        Assert.Equal(12, rolls.Count);
        Assert.Equal(5, transitions.Count);
        Assert.Equal(36, selections);
        DiagnosticSlotsDoReceipt accepted = new(current, current with { Seconds = current.Seconds + 0.001, OwnedRoll = null, Crisis = 0, RetainedCrisis = null, Ready = false }, source.ActorId, source.ActorId, source.ActorIndex, 16, source.MagicAction, 280, 0x8007, rolls[^1].Result);
        DiagnosticSlotsAdmissionReceipt receipt = original.Empty with { SourceFingerprint = source.Fingerprint, Declaration = original.Empty.Declaration with { Reselection = DiagnosticSlotsDeclaration.ReadySelfBatch2 }, Start = start, Policy = policy, Selection = selection, Rolls = rolls.ToArray(), Reselections = transitions.ToArray(), Do = accepted };
        return (source, receipt, transitions.Select(value => value.Selection).ToArray());
    }

    private static (BattleDiagnosticLimitSelectionReceipt Selection, DiagnosticSlotsObservation After) SelectBatch(DiagnosticSlotsSource source, DiagnosticSlotsObservation before, BattleDiagnosticLimitSelection policy)
    {
        NativeBattleRandomState random = before.Random;
        List<byte> choices = [];
        for (int index = 0; index < policy.MaximumSelections; index++)
        {
            random = random.Draw(out byte draw);
            byte crisis = NativeBattleLimitRules.CrisisLevel(source.Rules, before.HitPoints, before.MaximumHitPoints, before.Statuses0, before.Statuses1, before.DeadParty, draw, before.LimitsDisabled);
            choices.Add(crisis);
            if (crisis >= policy.MinimumCrisisLevel && crisis <= policy.MaximumCrisisLevel)
            {
                break;
            }
        }
        Assert.InRange(choices[^1], policy.MinimumCrisisLevel, policy.MaximumCrisisLevel);
        BattleDiagnosticLimitSelectionReceipt selection = new(source.ActorId, 5, 16, true, choices.ToImmutableArray(), before.Random, random);
        return (selection, before with { Seconds = before.Seconds + 0.001, Crisis = choices[^1], Random = random, OwnedRoll = null });
    }

    private static BattleInput Input(byte action) => new(BattleInputKind.Action, ActorId: "selphie", TargetId: "selphie", Ability: BattleAbility.LimitBreak, ActionId: 16 << 16 | action);

    private static (DiagnosticSlotsSource Source, DiagnosticSlotsAdmissionReceipt Empty, DiagnosticSlotsRollReceipt Roll, DiagnosticSlotsDoReceipt Do) Fixture(byte rolledMagic = 51)
    {
        byte[] character = new byte[36];
        character[2] = 100;
        character[4] = 16;
        byte[] selection = new byte[60];
        byte[] spells = new byte[256];
        for (int index = 0; index < spells.Length; index += 2)
        {
            spells[index] = rolledMagic;
            spells[index + 1] = 1;
        }
        byte[] timers = new byte[60];
        byte[] action = new byte[60];
        action[4] = 24;
        action[5] = 1;
        action[10] = 8;
        BattleNativeLimitDefinition rules = new(5, 16, 0, 100, new byte[32].ToImmutableArray()) { SlotsSelectionBytes = selection.Concat(spells).ToImmutableArray(), SlotsSpellBytes = spells.ToImmutableArray() };
        Guid battle = Guid.NewGuid();
        Guid lifecycle = Guid.NewGuid();
        DiagnosticSlotsSource source = new(battle, lifecycle, "selphie", 1, 51, 0, rules, character.ToImmutableArray(), selection.ToImmutableArray(), spells.ToImmutableArray(), timers.ToImmutableArray(), action.ToImmutableArray(), "");
        source = source with { Fingerprint = source.ComputeFingerprint() };
        NativeBattleRandomState random = NativeBattleRandomState.Initialize(17);
        DiagnosticSlotsObservation start = new(battle, lifecycle, 0, 0, 10, 10, 100, "selphie", 1, 5, 5, 16, 8, 1, 1000, 0, 0, 0, 0, 0, false, true, 4, true, true, false, false, random, null);
        BattleDiagnosticLimitSelectionReceipt selected = new("selphie", 5, 16, true, [4], random, random);
        DiagnosticSlotsAdmissionReceipt empty = new(1, source.Fingerprint, new(51, 256, 5), Input(51), start, new(4, 4, 256), selected);
        DiagnosticSlotsObservation before = start with { Seconds = 100.01, Crisis = 4 };
        NativeBattleRandomState afterRandom = random.Draw(out byte level).Draw(out byte bucket).Draw(out byte spell).Draw(out byte repetitions);
        BattleNativeSlotsRoll result = NativeBattleSlotsRules.Roll(rules, 8, 4, level, bucket, spell, repetitions);
        DiagnosticSlotsObservation after = before with { Seconds = 100.02, Random = afterRandom, OwnedRoll = result };
        DiagnosticSlotsRollReceipt roll = new(1, before, after, result);
        DiagnosticSlotsObservation beforeDo = after with { Seconds = 100.03 };
        DiagnosticSlotsObservation afterDo = beforeDo with { Seconds = 100.04, OwnedRoll = null, Crisis = 0, RetainedCrisis = null, Ready = false };
        DiagnosticSlotsDoReceipt accepted = new(beforeDo, afterDo, "selphie", "selphie", 1, 16, 51, 280, 0x8007, result);
        return (source, empty, roll, accepted);
    }
}
