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
internal sealed record DiagnosticCommandPreparation([property: JsonRequired] int Slot, [property: JsonRequired] int Ability);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticMagicJunctionPreparation([property: JsonRequired] int Slot, [property: JsonRequired] int Action);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticItemPreparation([property: JsonRequired] int Id, [property: JsonRequired] int Count);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticGuardianForcePreparation([property: JsonRequired] int Id, [property: JsonRequired] bool Acquire)
{
    public int? LearningAbility { get; init; }
    public ushort? AbilityPoints { get; init; }
}

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
    public ImmutableArray<DiagnosticCommandPreparation> Commands { get; init; } = [];
    public ImmutableArray<DiagnosticMagicJunctionPreparation> MagicJunctions { get; init; } = [];
    public ushort? HitPoints { get; init; }
    public int? Level { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticBattlePreparation([property: JsonRequired] ImmutableArray<DiagnosticGuardianForcePreparation> GuardianForces, [property: JsonRequired] ImmutableArray<DiagnosticCharacterPreparation> Characters, [property: JsonRequired] bool HoldEnemyCommands)
{
    public ImmutableArray<DiagnosticItemPreparation> Items { get; init; } = [];
    public DiagnosticLimitPreparation? Limits { get; init; }
    public ImmutableArray<byte> CombatParty { get; init; } = [];

    internal void Validate()
    {
        Limits?.Validate();
        if (CombatParty.IsDefault || !CombatParty.IsEmpty && (CombatParty.Length != 3 || CombatParty.All(row => row == 255) || CombatParty.Any(row => row != 255 && row >= 8) || CombatParty.Where(row => row != 255).Distinct().Count() != CombatParty.Count(row => row != 255)))
        {
            throw new InvalidDataException("Diagnostic combat party requires three native saved-row selectors, at least one character and no duplicate selected rows.");
        }
        if (GuardianForces.IsDefault || Characters.IsDefault || GuardianForces.Length > 16 || Characters.Length > 3 || GuardianForces.Any(entry => entry is null || entry.Id is < 0 or >= 16) || GuardianForces.Select(entry => entry.Id).Distinct().Count() != GuardianForces.Length || Characters.Any(entry => entry is null || entry.Row is < 0 or >= 8 || entry.CharacterId > 10) || Characters.Select(entry => entry.Row).Distinct().Count() != Characters.Length)
        {
            throw new InvalidDataException("Diagnostic preparation requires distinct owned GF and character rows.");
        }
        if (Items.IsDefault || Items.Length > NativeEconomy.ItemSlotCount || Items.Any(item => item is null || item.Id is < 1 or > NativeEconomy.ItemSlotCount || item.Count is < 2 or > 100) || Items.Select(item => item.Id).Distinct().Count() != Items.Length)
        {
            throw new InvalidDataException("Diagnostic inventory preparation requires distinct native item identities and exact quantities of2..100.");
        }
        foreach (DiagnosticGuardianForcePreparation gf in GuardianForces)
        {
            if (gf.LearningAbility is < 1 or >= 116 || gf.AbilityPoints == 0 || gf.AbilityPoints.HasValue && !gf.LearningAbility.HasValue)
            {
                throw new InvalidDataException("Diagnostic GF progress requires an explicit available native learning ability and positive optional AP award.");
            }
        }
        foreach (DiagnosticCharacterPreparation character in Characters)
        {
            if (character.Stocks.IsDefault || character.GuardianForces.IsDefault || character.Stocks.Length > 32 || character.Stocks.Any(stock => stock is null || stock.Action is < 1 or >= 64 || stock.Count is < 2 or > 100) || character.Stocks.Select(stock => stock.Action).Distinct().Count() != character.Stocks.Length || character.GuardianForces.Any(id => id is < 0 or >= 16) || character.GuardianForces.Distinct().Count() != character.GuardianForces.Length || character.HitPoints == 0)
            {
                throw new InvalidDataException("Diagnostic preparation requires native magic stock identities, at most32 distinct stocks of2..100, distinct GF assignments and positive HP.");
            }
            if (character.MagicCommand is { } magicCommand && (magicCommand.Slot is < 0 or >= 3 || magicCommand.Ability is < 20 or >= 39))
            {
                throw new InvalidDataException("Diagnostic Magic capability requires a native command slot and kernel command ability.");
            }
            if (character.Commands.IsDefault || character.Commands.Length > 3 || character.Commands.Any(command => command is null || command.Slot is < 0 or >= 3 || command.Ability is < 20 or >= 39) || character.Commands.Select(command => command.Slot).Distinct().Count() != character.Commands.Length || character.MagicCommand is { } magic && character.Commands.Any(command => command.Slot == magic.Slot))
            {
                throw new InvalidDataException("Diagnostic commands require distinct native slots and learned kernel command abilities; a legacy Magic slot cannot be assigned twice.");
            }
            if (character.Level is < 1 or > 100)
            {
                throw new InvalidDataException("Diagnostic character level must be within1..100.");
            }
            if (character.MagicJunctions.IsDefault || character.MagicJunctions.Length > 19 || character.MagicJunctions.Any(junction => junction is null || junction.Slot is < 0 or >= 19 || junction.Action is < 1 or >= 57) || character.MagicJunctions.Select(junction => junction.Slot).Distinct().Count() != character.MagicJunctions.Length || character.MagicJunctions.Select(junction => junction.Action).Distinct().Count() != character.MagicJunctions.Length)
            {
                throw new InvalidDataException("Diagnostic magic junctions require distinct native slots and stocked kernel magic identities.");
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
    internal static DiagnosticPreparationResult PrepareDiagnosticCharacters(NativeCharacterVitals source, DiagnosticBattlePreparation recipe, NativeBattleExperienceDefinition? experienceDefinition = null)
    {
        recipe.Validate();
        source.Validate();
        if (source.MagicSystem.SharedMagicStock || source.MagicSystem.InfiniteMagic || source.MagicSystem.SmartAttack)
        {
            throw new NotSupportedException("Diagnostic stock preparation requires ordinary personal finite magic rules.");
        }
        NativeCharacterVitals next = source.DeepCopy();
        if (!recipe.CombatParty.IsEmpty)
        {
            next = next with { CombatParty = recipe.CombatParty.ToArray() };
        }
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
            if (gf.LearningAbility is int learning)
            {
                next = next.SelectGuardianForceLearning(gf.Id, learning);
            }
            if (gf.AbilityPoints is ushort points)
            {
                next = next.AwardGuardianForceAbilityPoints(gf.Id, points);
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
                if (stock.Action >= next.Definitions.MagicHitPoints.Length)
                {
                    throw new InvalidDataException("Diagnostic stock identity is absent from the installed magic kernel.");
                }
                int delta = stock.Count - next.GetMagicCount(character.Row, stock.Action);
                next = next.ChangeMagicStock(character.Row, stock.Action, delta, delta >= 0 ? MagicOperationReason.ScriptReward : MagicOperationReason.ScriptRemoval, out MagicStockMutation mutation);
                if (mutation.Rejected != 0 || next.GetMagicCount(character.Row, stock.Action) != stock.Count)
                {
                    throw new InvalidOperationException("Diagnostic stock preparation could not satisfy the exact declared quantity.");
                }
            }
            if (character.MagicCommand is { } magicCommand)
            {
                if (next.MenuDefinitions.CommandAbilityCommands[magicCommand.Ability - 20] != 2)
                {
                    throw new InvalidOperationException("The declared kernel ability does not provide the Magic command.");
                }
                next = next.SetJunctionAbility(character.Row, magicCommand.Slot, magicCommand.Ability, command: true);
            }
            foreach (DiagnosticCommandPreparation command in character.Commands)
            {
                if (command.Ability - 20 >= next.MenuDefinitions.CommandAbilityCommands.Length)
                {
                    throw new InvalidDataException("The declared command ability is absent from the installed kernel.");
                }
                next = next.SetJunctionAbility(character.Row, command.Slot, command.Ability, command: true);
            }
            foreach (DiagnosticMagicJunctionPreparation junction in character.MagicJunctions)
            {
                next = next.SetJunctionMagic(character.Row, junction.Slot, junction.Action);
            }
            if (character.Level is int level)
            {
                next = PrepareDiagnosticCharacterLevel(next, character.Row, level, experienceDefinition ?? throw new NotSupportedException("Diagnostic level preparation requires the actual installed battle experience definition."));
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
        Record("combat-party", 0, source.CombatParty, next.CombatParty);
        return new(next, changes.ToImmutable());
    }
}
