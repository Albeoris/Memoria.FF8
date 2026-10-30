using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticSlotsDeclaration([property: JsonRequired] byte MagicAction, [property: JsonRequired] int MaximumRolls, [property: JsonRequired] double MaximumSeconds)
{
    public const string ReadySelfBatch2 = "native-ready-self-after-two-misses";

    public string? Reselection { get; init; }

    [JsonIgnore]
    public bool ReselectsAfterTwoMisses => Reselection == ReadySelfBatch2;

    [JsonIgnore]
    public ushort Effect => MagicAction switch
    {
        51 => 280,
        52 => 189,
        53 => 288,
        56 => 85,
        _ => throw new InvalidDataException("Slots diagnostics require one declared original special magic row.")
    };

    public void Validate(BattleInput input, ushort effect, int? action, byte? command)
    {
        if (Reselection is not null && Reselection != ReadySelfBatch2 || MaximumRolls is < 1 or > 256 || !double.IsFinite(MaximumSeconds) || MaximumSeconds <= 0 || MaximumSeconds > 5 || effect != Effect || input.Kind != BattleInputKind.Action || input.Ability != BattleAbility.LimitBreak || input.ActionId != (16 << 16 | MagicAction) || action != MagicAction || command != 16 || string.IsNullOrWhiteSpace(input.ActorId) || string.IsNullOrWhiteSpace(input.TargetId))
        {
            throw new InvalidDataException("Slots diagnostics require bounded ordinary rolls and a matching command16 Limit input.");
        }
    }
}

/// <summary>Original resource bytes observed once at battle initialization, separate from admission results.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticSlotsSource([property: JsonRequired] Guid BattleInstanceId, [property: JsonRequired] Guid LifecycleId, [property: JsonRequired] string ActorId, [property: JsonRequired] byte ActorIndex, [property: JsonRequired] byte MagicAction, [property: JsonRequired] byte InitialLearnedFlags, [property: JsonRequired] BattleNativeLimitDefinition Rules, [property: JsonRequired] ImmutableArray<byte> CharacterRecord, [property: JsonRequired] ImmutableArray<byte> SelectionTable, [property: JsonRequired] ImmutableArray<byte> SpellTable, [property: JsonRequired] ImmutableArray<byte> TimerRecord, [property: JsonRequired] ImmutableArray<byte> ActionRecord, [property: JsonRequired] string Fingerprint)
{
    public string ComputeFingerprint()
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (ImmutableArray<byte> bytes in new[] { CharacterRecord, SelectionTable, SpellTable, TimerRecord, ActionRecord })
        {
            hash.AppendData(bytes.AsSpan());
        }
        hash.AppendData(new[] { MagicAction });
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public void Validate()
    {
        if (BattleInstanceId == Guid.Empty || LifecycleId == Guid.Empty || string.IsNullOrWhiteSpace(ActorId) || ActorIndex > 2 || CharacterRecord.IsDefault || CharacterRecord.Length != 36 || SelectionTable.IsDefault || SelectionTable.Length != 60 || SpellTable.IsDefault || SpellTable.Length != 256 || TimerRecord.IsDefault || TimerRecord.Length != 60 || ActionRecord.IsDefault || ActionRecord.Length != 60 || Rules is null || Fingerprint != ComputeFingerprint())
        {
            throw new InvalidDataException("Slots source lost its initialized resource or physical owner fingerprint.");
        }
        Rules.Validate();
        ushort effect = new DiagnosticSlotsDeclaration(MagicAction, 1, 1).Effect;
        if (Rules.CharacterId != 5 || Rules.Command != 16 || CharacterRecord[4] != Rules.Command || CharacterRecord[5] != Rules.Parameter || CharacterRecord[2] != Rules.HitPointCoefficient || !Rules.StatusWeights.AsSpan().SequenceEqual(TimerRecord.AsSpan().Slice(16, 32)) || !Rules.SlotsSelectionBytes.AsSpan().SequenceEqual(SelectionTable.Concat(SpellTable).ToArray()) || !Rules.SlotsSpellBytes.AsSpan().SequenceEqual(SpellTable.AsSpan()) || (ushort)(ActionRecord[4] | ActionRecord[5] << 8) != effect || ActionRecord[10] != (MagicAction is 53 or 56 ? 0x48 : 0x08))
        {
            throw new InvalidDataException("Slots rules differ from the initialized original character, tables or selected action row.");
        }
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticSlotsObservation([property: JsonRequired] Guid BattleInstanceId, [property: JsonRequired] Guid LifecycleId, [property: JsonRequired] long RestoreCount, [property: JsonRequired] long ResetCount, [property: JsonRequired] long Callback, [property: JsonRequired] long SimulationFrame, [property: JsonRequired] double Seconds, [property: JsonRequired] string ActorId, [property: JsonRequired] byte ActorIndex, [property: JsonRequired] int ModelId, [property: JsonRequired] int CommandOwner, [property: JsonRequired] byte Command, [property: JsonRequired] int Level, [property: JsonRequired] int HitPoints, [property: JsonRequired] int MaximumHitPoints, [property: JsonRequired] ushort Statuses0, [property: JsonRequired] uint Statuses1, [property: JsonRequired] byte Crisis, [property: JsonRequired] byte LearnedFlags, [property: JsonRequired] int DeadParty, [property: JsonRequired] bool LimitsDisabled, [property: JsonRequired] bool LimitOncePerTurn, [property: JsonRequired] byte? RetainedCrisis, [property: JsonRequired] bool Ready, [property: JsonRequired] bool Active, [property: JsonRequired] bool CardSlots, [property: JsonRequired] bool PendingDeal, [property: JsonRequired] NativeBattleRandomState Random, [property: JsonRequired] BattleNativeSlotsRoll? OwnedRoll);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticSlotsRollReceipt([property: JsonRequired] int Index, [property: JsonRequired] DiagnosticSlotsObservation Before, [property: JsonRequired] DiagnosticSlotsObservation After, [property: JsonRequired] BattleNativeSlotsRoll Result);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticSlotsReselectionReceipt([property: JsonRequired] int AfterRollIndex, [property: JsonRequired] DiagnosticSlotsObservation Before, [property: JsonRequired] BattleDiagnosticLimitSelectionReceipt Selection, [property: JsonRequired] DiagnosticSlotsObservation After);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticSlotsDoReceipt([property: JsonRequired] DiagnosticSlotsObservation Before, [property: JsonRequired] DiagnosticSlotsObservation After, [property: JsonRequired] string ActorId, [property: JsonRequired] string TargetId, [property: JsonRequired] byte TargetIndex, [property: JsonRequired] byte Command, [property: JsonRequired] ushort Action, [property: JsonRequired] ushort Effect, [property: JsonRequired] ushort TargetMask, [property: JsonRequired] BattleNativeSlotsRoll UsedRoll);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticSlotsAdmissionReceipt([property: JsonRequired] int Execution, [property: JsonRequired] string SourceFingerprint, [property: JsonRequired] DiagnosticSlotsDeclaration Declaration, [property: JsonRequired] BattleInput Input, [property: JsonRequired] DiagnosticSlotsObservation Start, [property: JsonRequired] BattleDiagnosticLimitSelection Policy, [property: JsonRequired] BattleDiagnosticLimitSelectionReceipt Selection)
{
    public DiagnosticSlotsRollReceipt[] Rolls { get; init; } = [];
    public DiagnosticSlotsReselectionReceipt[] Reselections { get; init; } = [];
    public DiagnosticSlotsDoReceipt? Do { get; init; }
}

/// <summary>Validates observations of existing native roll and Do APIs; never mutates a battle or chooses a result.</summary>
public sealed class DiagnosticSlotsAdmission
{
    private readonly DiagnosticSlotsSource source;
    private readonly byte? selectedRetainedCrisis;
    private int selectionCount;
    private int lastSelectionAfterRollIndex;
    private DiagnosticSlotsObservation? reselectionAnchor;
    public DiagnosticSlotsAdmissionReceipt Receipt { get; private set; }
    public bool Matched => Receipt.Rolls.LastOrDefault()?.Result.MagicId == Receipt.Declaration.MagicAction;
    public bool RequiresReselection => Receipt.Declaration.ReselectsAfterTwoMisses && Receipt.Do is null && !Matched && Receipt.Rolls.Length - lastSelectionAfterRollIndex == 2;
    public int RemainingSelections => Receipt.Policy.MaximumSelections - selectionCount;

    public DiagnosticSlotsAdmission(DiagnosticSlotsSource source, DiagnosticSlotsAdmissionReceipt receipt)
    {
        source.Validate();
        receipt.Declaration.Validate(receipt.Input, receipt.Declaration.Effect, receipt.Declaration.MagicAction, 16);
        if (receipt.Execution is < 1 or > 2 || receipt.SourceFingerprint != source.Fingerprint || source.MagicAction != receipt.Declaration.MagicAction || receipt.Input.ActorId != source.ActorId || receipt.Rolls is null || receipt.Reselections is null || receipt.Rolls.Length != 0 || receipt.Reselections.Length != 0 || receipt.Do is not null)
        {
            throw new InvalidDataException("Slots admission requires a new initialized source-owned execution.");
        }
        this.source = source;
        Receipt = receipt;
        ValidatePoint(receipt.Start, false);
        receipt.Policy.Validate();
        BattleDiagnosticLimitSelectionReceipt selection = receipt.Selection;
        if (receipt.Declaration.ReselectsAfterTwoMisses && receipt.Start.LimitOncePerTurn)
        {
            throw new InvalidDataException("Ready-owner reselection requires the original repeatable crisis-selection policy.");
        }
        selectedRetainedCrisis = ValidateSelection(receipt.Start, selection, true);
        selectionCount = selection.CrisisSelections.Length;
    }

    private byte? ValidateSelection(DiagnosticSlotsObservation before, BattleDiagnosticLimitSelectionReceipt selection, bool initial)
    {
        if (selection is null || !selection.Accepted || selection.ActorId != source.ActorId || selection.CommandOwner != 5 || selection.Command != 16 || selection.CrisisSelections.IsDefaultOrEmpty || selection.CrisisSelections.Length > 256 || selection.CrisisSelections.Any(level => level > 4) || selection.CrisisSelections[^1] == 0 || selection.RandomBefore is null || selection.RandomAfter is null || !RandomEqual(before.Random, selection.RandomBefore) || initial && before.OwnedRoll is not null || !before.Ready)
        {
            throw new InvalidDataException("Slots admission does not follow its independent start and real Limit selection.");
        }
        selection.RandomBefore.Validate();
        selection.RandomAfter.Validate();
        NativeBattleRandomState selectionRandom = before.Random;
        byte? retained = before.RetainedCrisis;
        for (int index = 0; index < selection.CrisisSelections.Length; index++)
        {
            byte crisis;
            if (before.LimitOncePerTurn && retained is byte saved)
            {
                crisis = saved;
            }
            else
            {
                byte draw = 0;
                if (!before.LimitsDisabled && (before.Statuses1 & 0x200) == 0)
                {
                    selectionRandom = selectionRandom.Draw(out draw);
                }
                crisis = NativeBattleLimitRules.CrisisLevel(source.Rules, before.HitPoints, before.MaximumHitPoints, before.Statuses0, before.Statuses1, before.DeadParty, draw, before.LimitsDisabled);
                if (before.LimitOncePerTurn)
                {
                    retained = crisis;
                }
            }
            bool matched = crisis >= Receipt.Policy.MinimumCrisisLevel && crisis <= Receipt.Policy.MaximumCrisisLevel;
            if (selection.CrisisSelections[index] != crisis || index + 1 < selection.CrisisSelections.Length && (matched || retained is not null && before.LimitOncePerTurn))
            {
                throw new InvalidDataException("Slots crisis receipt differs from native selection or its first accepted result.");
            }
        }
        if (selection.CrisisSelections.Length > Receipt.Policy.MaximumSelections || selection.CrisisSelections[^1] < Receipt.Policy.MinimumCrisisLevel || selection.CrisisSelections[^1] > Receipt.Policy.MaximumCrisisLevel || !RandomEqual(selectionRandom, selection.RandomAfter))
        {
            throw new InvalidDataException("Slots crisis selection changed its source HP/status inputs, random stream or bounds.");
        }
        return retained;
    }

    public void Rolled(DiagnosticSlotsRollReceipt roll)
    {
        if (Receipt.Do is not null || Matched || RequiresReselection || roll.Index != Receipt.Rolls.Length + 1 || roll.Index > Receipt.Declaration.MaximumRolls || roll.Result is null)
        {
            throw new InvalidDataException("Slots roll exceeded its bound, index or matched-result latch.");
        }
        ValidatePoint(roll.Before, true);
        ValidatePoint(roll.After, true);
        if (!SameState(roll.Before, roll.After) || roll.After.Seconds < roll.Before.Seconds || roll.After.OwnedRoll != roll.Result || roll.Result.Repetitions is < 1 or > 3)
        {
            throw new InvalidDataException("Slots roll changed native state or lost its currently owned result.");
        }
        if (Receipt.Rolls.Length == 0)
        {
            if (!RandomEqual(roll.Before.Random, Receipt.Selection.RandomAfter!) || roll.Before.OwnedRoll is not null || roll.Before.Crisis != Receipt.Selection.CrisisSelections[^1] || roll.Before.RetainedCrisis != selectedRetainedCrisis || !SameState(Receipt.Start with { Crisis = roll.Before.Crisis, RetainedCrisis = roll.Before.RetainedCrisis }, roll.Before))
            {
                throw new InvalidDataException("First Slots roll is not linked to its original selection/start.");
            }
        }
        else
        {
            RequireSameObservation(reselectionAnchor is not null && lastSelectionAfterRollIndex == Receipt.Rolls.Length ? reselectionAnchor : Receipt.Rolls[^1].After, roll.Before);
        }
        NativeBattleRandomState next = roll.Before.Random.Draw(out byte level).Draw(out byte bucket).Draw(out byte spell).Draw(out byte repetitions);
        BattleNativeSlotsRoll expected = NativeBattleSlotsRules.Roll(source.Rules, roll.Before.Level, roll.Before.Crisis, level, bucket, spell, repetitions);
        if (!RandomEqual(next, roll.After.Random) || expected != roll.Result)
        {
            throw new InvalidDataException("Slots result or random cursor differs from the four actual native draws.");
        }
        Receipt = Receipt with { Rolls = [.. Receipt.Rolls, roll] };
    }

    public void Reselected(DiagnosticSlotsReselectionReceipt transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (!RequiresReselection || RemainingSelections <= 0 || transition.AfterRollIndex != Receipt.Rolls.Length)
        {
            throw new InvalidDataException("Ready-owner reselection is allowed only after exactly two unmatched ordinary rolls.");
        }
        ValidatePoint(transition.Before, true);
        ValidatePoint(transition.After, true);
        RequireSameObservation(Receipt.Rolls[^1].After, transition.Before);
        byte? retained = ValidateSelection(transition.Before, transition.Selection, false);
        if (transition.Selection.CrisisSelections.Length > RemainingSelections)
        {
            throw new InvalidDataException("Slots reselections exceeded the global native crisis-selection bound.");
        }
        DiagnosticSlotsObservation expected = transition.Before with { Crisis = transition.Selection.CrisisSelections[^1], RetainedCrisis = retained, Random = transition.Selection.RandomAfter!, OwnedRoll = null };
        RequireSameObservation(expected, transition.After);
        selectionCount += transition.Selection.CrisisSelections.Length;
        lastSelectionAfterRollIndex = Receipt.Rolls.Length;
        reselectionAnchor = transition.After;
        Receipt = Receipt with { Reselections = [.. Receipt.Reselections, transition] };
    }

    public void Did(DiagnosticSlotsDoReceipt accepted)
    {
        if (Receipt.Do is not null || !Matched || accepted.UsedRoll != Receipt.Rolls[^1].Result || accepted.ActorId != source.ActorId || string.IsNullOrWhiteSpace(accepted.TargetId) || accepted.TargetIndex > 6 || (accepted.TargetMask & (1 << accepted.TargetIndex)) == 0 || accepted.Command != 16 || accepted.Action != source.MagicAction || accepted.Effect != Receipt.Declaration.Effect || accepted.TargetMask != (source.MagicAction is 53 or 56 ? 0x80F8 : 0x8007))
        {
            throw new InvalidDataException("Slots Do did not accept exactly its latched original result and command.");
        }
        RequireSameObservation(Receipt.Rolls[^1].After, accepted.Before);
        ValidatePoint(accepted.Before, true);
        ValidatePoint(accepted.After, false);
        if (accepted.After.OwnedRoll is not null || accepted.After.Crisis != 0 || accepted.After.RetainedCrisis is not null || accepted.After.Ready || !RandomEqual(accepted.Before.Random, accepted.After.Random) || !SameState(accepted.Before with { Crisis = 0, RetainedCrisis = null, Ready = false, Statuses1 = accepted.Before.Statuses1 & 0xFF77FFFF }, accepted.After) || accepted.After.Seconds < accepted.Before.Seconds)
        {
            throw new InvalidDataException("Slots Do lost command consumption or changed native owner/RNG state.");
        }
        Receipt = Receipt with { Do = accepted };
    }

    private void ValidatePoint(DiagnosticSlotsObservation point, bool selected)
    {
        if (point is null || point.Random is null || point.BattleInstanceId != source.BattleInstanceId || point.LifecycleId != source.LifecycleId || point.RestoreCount != 0 || point.ResetCount != 0 || point.ActorId != source.ActorId || point.ActorIndex != source.ActorIndex || point.ModelId != 5 || point.CommandOwner != 5 || point.Command != 16 || point.Level is < 1 or > 100 || point.DeadParty is < 0 or > 3 || point.RetainedCrisis > 4 || point.Crisis > 4 || point.HitPoints <= 0 || point.MaximumHitPoints < point.HitPoints || !point.Active || point.CardSlots || point.PendingDeal || selected && (!point.Ready || point.Crisis is < 1 or > 4) || point.Callback < 0 || point.SimulationFrame < 0 || !double.IsFinite(point.Seconds) || point.Seconds < Receipt.Start.Seconds || point.Seconds - Receipt.Start.Seconds > Receipt.Declaration.MaximumSeconds)
        {
            throw new InvalidDataException("Slots observation lost native lifecycle, living Selphie owner or admission deadline.");
        }
        point.Random.Validate();
    }

    private static bool SameState(DiagnosticSlotsObservation left, DiagnosticSlotsObservation right)
    {
        return left with { Seconds = right.Seconds, Random = right.Random, OwnedRoll = right.OwnedRoll } == right;
    }

    private static void RequireSameObservation(DiagnosticSlotsObservation left, DiagnosticSlotsObservation right)
    {
        if (!SameState(left, right) || !RandomEqual(left.Random, right.Random) || left.OwnedRoll != right.OwnedRoll || right.Seconds < left.Seconds)
        {
            throw new InvalidDataException("Slots observations changed between ordinary operations.");
        }
    }

    public static bool RandomEqual(NativeBattleRandomState left, NativeBattleRandomState right)
    {
        return left.Lane == right.Lane && left.Indexes.AsSpan().SequenceEqual(right.Indexes.AsSpan());
    }
}

public static class DiagnosticSlotsAdmissionValidation
{
    public static void ValidateReceipt(DiagnosticSlotsAdmissionReceipt receipt, DiagnosticSlotsSource initializedSource, DiagnosticSlotsObservation independentStart, BattleDiagnosticLimitSelectionReceipt actualSelection, BattleDiagnosticLimitSelection policy, BattleInput input, IReadOnlyList<BattleDiagnosticLimitSelectionReceipt>? actualReselections = null)
    {
        if (receipt.Policy != policy)
        {
            throw new InvalidDataException("Slots Limit policy changed from the initialized request.");
        }
        if (receipt.Start != independentStart || receipt.Input != input || receipt.Selection != actualSelection)
        {
            // Immutable array fields are compared by content below after deserialization.
            if (System.Text.Json.JsonSerializer.Serialize(receipt.Start) != System.Text.Json.JsonSerializer.Serialize(independentStart) || System.Text.Json.JsonSerializer.Serialize(receipt.Input) != System.Text.Json.JsonSerializer.Serialize(input) || System.Text.Json.JsonSerializer.Serialize(receipt.Selection) != System.Text.Json.JsonSerializer.Serialize(actualSelection))
            {
                throw new InvalidDataException("Slots receipt differs from its independent live start, semantic input or Limit selection.");
            }
        }
        if (receipt.Reselections is null || receipt.Reselections.Length != (actualReselections?.Count ?? 0))
        {
            throw new InvalidDataException("Slots reselections differ from their independent actual native selection log.");
        }
        DiagnosticSlotsAdmission replay = new(initializedSource, receipt with { Rolls = [], Reselections = [], Do = null });
        int nextReselection = 0;
        foreach (DiagnosticSlotsRollReceipt roll in receipt.Rolls)
        {
            if (nextReselection < receipt.Reselections.Length && receipt.Reselections[nextReselection].AfterRollIndex == roll.Index - 1)
            {
                DiagnosticSlotsReselectionReceipt transition = receipt.Reselections[nextReselection];
                if (System.Text.Json.JsonSerializer.Serialize(transition.Selection) != System.Text.Json.JsonSerializer.Serialize(actualReselections![nextReselection]))
                {
                    throw new InvalidDataException("Slots reselection changed its actual native selection receipt.");
                }
                replay.Reselected(transition);
                nextReselection++;
            }
            replay.Rolled(roll);
        }
        if (nextReselection != receipt.Reselections.Length)
        {
            throw new InvalidDataException("Slots reselection log is extra, duplicated or out of order.");
        }
        replay.Did(receipt.Do ?? throw new InvalidDataException("Slots receipt has no real Do admission."));
    }
}
