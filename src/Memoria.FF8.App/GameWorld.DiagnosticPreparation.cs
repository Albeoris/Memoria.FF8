using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticMagicStockPreparation([property: JsonRequired] int Action, [property: JsonRequired] int Count);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticMagicCommandPreparation([property: JsonRequired] int Slot, [property: JsonRequired] int Ability);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticGuardianForcePreparation([property: JsonRequired] int Id, [property: JsonRequired] bool Acquire);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticTargetExpectation([property: JsonRequired] string ActorId, [property: JsonRequired] int Stage, [property: JsonRequired] int Model, [property: JsonRequired] int SceneSlot, [property: JsonRequired] int PhysicalIndex, [property: JsonRequired] uint FlagsMask, [property: JsonRequired] uint FlagsValue, [property: JsonRequired] int? OpeningAction, [property: JsonRequired] ushort? OpeningEffect)
{
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ActorId) || ActorId.Length > 128 || Stage is < 0 or > 255 || Model is < 0 or > 238 || SceneSlot is < 0 or > 7 || PhysicalIndex is < 3 or > 6 || (FlagsMask & 0xC3) != 0xC3 || (FlagsValue & 0xC3) != 1 || (FlagsValue & ~FlagsMask) != 0 || OpeningAction is <= 0 || OpeningEffect is 0 || OpeningAction.HasValue != OpeningEffect.HasValue)
        {
            throw new InvalidDataException("Diagnostic target requires an explicit original stage/model/scene/physical identity and active visible targetable unsuspended flags; opening action/effect must be a positive pair or explicitly null.");
        }
    }
}

internal sealed record DiagnosticTargetReceipt(BattleDiagnosticTarget Target, BattleDiagnosticEffectDispatch? OpeningDispatch, long? OpeningCompletedCallback, string Scope);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticCharacterPreparation([property: JsonRequired] int Row, [property: JsonRequired] byte CharacterId, [property: JsonRequired] ImmutableArray<DiagnosticMagicStockPreparation> Stocks, [property: JsonRequired] ImmutableArray<int> GuardianForces)
{
    public DiagnosticMagicCommandPreparation? MagicCommand { get; init; }
    public ushort? HitPoints { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticBattlePreparation([property: JsonRequired] ImmutableArray<DiagnosticGuardianForcePreparation> GuardianForces, [property: JsonRequired] ImmutableArray<DiagnosticCharacterPreparation> Characters, [property: JsonRequired] bool HoldEnemyCommands)
{
    internal void Validate()
    {
        if (GuardianForces.IsDefault || Characters.IsDefault || GuardianForces.Length > 16 || Characters.Length > 3 || GuardianForces.Any(entry => entry is null || entry.Id is < 0 or >= 16) || GuardianForces.Select(entry => entry.Id).Distinct().Count() != GuardianForces.Length || Characters.Any(entry => entry is null || entry.Row is < 0 or >= 8 || entry.CharacterId > 10) || Characters.Select(entry => entry.Row).Distinct().Count() != Characters.Length)
        {
            throw new InvalidDataException("Diagnostic preparation requires distinct owned GF and character rows.");
        }
        foreach (DiagnosticCharacterPreparation character in Characters)
        {
            if (character.Stocks.IsDefault || character.GuardianForces.IsDefault || character.Stocks.Length > 10 || character.Stocks.Any(stock => stock is null || stock.Action is not (>= 1 and <= 9 or 21) || stock.Count is < 2 or > 100) || character.Stocks.Select(stock => stock.Action).Distinct().Count() != character.Stocks.Length || character.GuardianForces.Any(id => id is < 0 or >= 16) || character.GuardianForces.Distinct().Count() != character.GuardianForces.Length || character.HitPoints == 0)
            {
                throw new InvalidDataException("Diagnostic preparation supports explicit action1..9 and21 stocks of 2..100, distinct GF assignments and positive HP only.");
            }
            if (character.MagicCommand is { } command && (command.Slot is < 0 or >= 3 || command.Ability is < 20 or >= 39))
            {
                throw new InvalidDataException("Diagnostic Magic capability requires a native command slot and kernel command ability.");
            }
        }
    }
}

internal sealed record DiagnosticPreparationChange(string Owner, int Row, JsonElement Before, JsonElement After);
internal sealed record DiagnosticPreparationResult(NativeCharacterVitals Vitals, ImmutableArray<DiagnosticPreparationChange> Changes);

internal sealed partial class GameWorld
{
    private BattleDiagnosticEffectDispatch? diagnosticOpeningDispatch;
    private long? diagnosticOpeningCompleted;
    private readonly List<BattleDiagnosticEffectDispatch> diagnosticObservedOpeningDispatches = [];

    internal object ObserveDiagnosticTargetPreparation()
    {
        return new { Expected = diagnosticBattle?.Request.Target, OpeningDispatch = diagnosticOpeningDispatch, OpeningCompletedCallback = diagnosticOpeningCompleted, ObservedDispatches = diagnosticObservedOpeningDispatches };
    }

    private void ObserveDiagnosticOpening(BattleDiagnosticEffectDispatch dispatch)
    {
        if (diagnosticObservedOpeningDispatches.Count < 32)
        {
            diagnosticObservedOpeningDispatches.Add(dispatch);
        }
        DiagnosticTargetExpectation? expected = diagnosticBattle?.Request.Target;
        if (expected?.OpeningAction is not null && dispatch.ActorId == expected.ActorId && dispatch.ActionId == expected.OpeningAction && dispatch.RequestedEffect == expected.OpeningEffect && dispatch.ExecutedEffect == expected.OpeningEffect)
        {
            diagnosticOpeningDispatch = dispatch;
        }
    }

    private void ObserveDiagnosticOpeningCompletion(BattleEvent value)
    {
        if (value.Kind == "ActionComplete" && diagnosticOpeningDispatch is { } opening && value.ActorId == opening.ActorId)
        {
            diagnosticOpeningCompleted = battle?.NativeHostCallbacks;
        }
    }

    internal DiagnosticTargetReceipt? VerifyDiagnosticTarget()
    {
        DiagnosticTargetExpectation? expected = diagnosticBattle?.Request.Target;
        if (expected is null)
        {
            return null;
        }
        BattleSession active = battle ?? throw new InvalidOperationException("Diagnostic target has no live encounter.");
        if (!active.NativeEntryReady || active.ObserveDiagnosticDrainBlockers().Count != 0 || expected.OpeningAction is not null && (diagnosticOpeningDispatch is null || diagnosticOpeningCompleted is null))
        {
            return null;
        }
        BattleDiagnosticTarget target = active.ObserveDiagnosticTarget(expected.ActorId);
        if (target.Stage != expected.Stage || target.Model != expected.Model || target.SceneSlot != expected.SceneSlot || target.PhysicalIndex != expected.PhysicalIndex || (target.Flags & expected.FlagsMask) != expected.FlagsValue || !target.Alive || target.HitPoints <= 0 || !target.StatisticsReady)
        {
            throw new InvalidOperationException("The initialized live target does not satisfy the declared original participant contract.");
        }
        DiagnosticTargetReceipt receipt = new(target, diagnosticOpeningDispatch, diagnosticOpeningCompleted, expected.OpeningAction is null ? "Direct initialized participant identity; no opening action was declared. Campaign progression requires separate provenance." : "Direct initialized participant and completed original opening; campaign progression requires separate provenance.");
        if (DiagnosticReceipt is { Target: null } initializer)
        {
            DiagnosticReceipt = initializer with { Target = receipt };
        }
        return receipt;
    }

    // Preparation precedes encounter allocation. This adapter never receives a live battle,
    // effect allocator or random owner and is never used between the two commands.
    internal static DiagnosticPreparationResult PrepareDiagnosticCharacters(NativeCharacterVitals source, DiagnosticBattlePreparation recipe)
    {
        recipe.Validate();
        source.Validate();
        if (source.MagicSystem.SharedMagicStock || source.MagicSystem.InfiniteMagic || source.MagicSystem.SmartAttack)
        {
            throw new NotSupportedException("Diagnostic stock preparation requires ordinary personal finite magic rules.");
        }
        NativeCharacterVitals next = source.DeepCopy();
        foreach (DiagnosticGuardianForcePreparation gf in recipe.GuardianForces)
        {
            if (gf.Acquire)
            {
                next = next.AcquireGuardianForce(gf.Id);
            }
            else if ((next.GuardianForces[gf.Id].Flags & 1) == 0)
            {
                throw new InvalidOperationException("The declared GF prerequisite is not acquired.");
            }
        }
        foreach (DiagnosticCharacterPreparation character in recipe.Characters)
        {
            if (!next.CombatParty.Contains((byte)character.Row) || next.Records[character.Row].CharacterId != character.CharacterId)
            {
                throw new InvalidOperationException("Diagnostic preparation must name the actual combat-party physical row and saved identity.");
            }
            foreach (int gf in character.GuardianForces)
            {
                if ((next.Records[character.Row].Junction.GuardianForces & (1 << gf)) == 0)
                {
                    next = next.ToggleGuardianForceJunction(character.Row, gf);
                }
            }
            foreach (DiagnosticMagicStockPreparation stock in character.Stocks)
            {
                int delta = stock.Count - next.GetMagicCount(character.Row, stock.Action);
                next = next.ChangeMagicStock(character.Row, stock.Action, delta, delta >= 0 ? MagicOperationReason.ScriptReward : MagicOperationReason.ScriptRemoval, out MagicStockMutation mutation);
                if (mutation.Rejected != 0 || next.GetMagicCount(character.Row, stock.Action) != stock.Count)
                {
                    throw new InvalidOperationException("Diagnostic stock preparation could not satisfy the exact declared quantity.");
                }
            }
            if (character.MagicCommand is { } command)
            {
                if (next.MenuDefinitions.CommandAbilityCommands[command.Ability - 20] != 2)
                {
                    throw new InvalidOperationException("The declared kernel ability does not provide the Magic command.");
                }
                next = next.SetJunctionAbility(character.Row, command.Slot, command.Ability, command: true);
            }
            if (character.HitPoints is ushort hitPoints)
            {
                if (hitPoints > next.GetMagicMaximumHitPoints(character.Row))
                {
                    throw new InvalidOperationException("Diagnostic HP exceeds the character's actual derived maximum.");
                }
                next = next.WithSavedHitPoints(character.Row, hitPoints);
            }
        }
        next = next.RebuildParty();
        next.Validate();
        ImmutableArray<DiagnosticPreparationChange>.Builder changes = ImmutableArray.CreateBuilder<DiagnosticPreparationChange>();
        void Record(string owner, int row, object before, object after)
        {
            JsonElement beforeJson = JsonSerializer.SerializeToElement(before);
            JsonElement afterJson = JsonSerializer.SerializeToElement(after);
            if (!JsonElement.DeepEquals(beforeJson, afterJson))
            {
                changes.Add(new(owner, row, beforeJson, afterJson));
            }
        }
        for (int row = 0; row < source.Records.Length; row++)
        {
            Record("character", row, source.Records[row], next.Records[row]);
        }
        for (int id = 0; id < source.GuardianForces.Length; id++)
        {
            Record("guardian-force", id, source.GuardianForces[id], next.GuardianForces[id]);
        }
        Record("magic-system", 0, source.MagicSystem, next.MagicSystem);
        return new(next, changes.ToImmutable());
    }
}
