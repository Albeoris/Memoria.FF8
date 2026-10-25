using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DiagnosticLimitPreparation
{
    public bool BorrowBlueMagicForSquall { get; init; }
    public ushort LearnBlueMagic { get; init; }
    public ushort LearnDuel { get; init; }
    public byte LearnAngelo { get; init; }
    public byte LearnSpecialMagic { get; init; }
    public ushort? Options978 { get; init; }

    internal void Validate()
    {
        if ((LearnSpecialMagic & ~0x3F) != 0)
        {
            throw new InvalidDataException("Diagnostic special Limit learning accepts only the six native learned flags.");
        }
    }
}

internal sealed record DiagnosticLimitPreparationResult(NativeLimitProgress Progress, ImmutableArray<DiagnosticPreparationChange> Changes);

internal sealed partial class GameWorld
{
    internal BattleDiagnosticBorrowedLimitReceipt? DiagnosticCapabilities { get; private set; }

    private BattleDefinition PrepareDiagnosticCapabilities(BattleDefinition source)
    {
        DiagnosticCapabilities = null;
        if (diagnosticBattle?.Request.Fixture?.Limits?.BorrowBlueMagicForSquall != true)
        {
            return source;
        }
        BattleCombatantDefinition actor = source.Combatants.Single(candidate => candidate.IsPlayer && candidate.ModelId == 0);
        (BattleDefinition next, BattleDiagnosticBorrowedLimitReceipt receipt) = BattleSession.PrepareDiagnosticBlueMagic(source, actor.Id, BattleResources.ReadNativeLimitDefinition(3));
        DiagnosticCapabilities = receipt;
        return next;
    }

    // This preparation only supplies saved prerequisites before encounter construction.
    // It does not replace the actor's command owner, identity, skeleton or invocation process.
    internal static DiagnosticLimitPreparationResult PrepareDiagnosticLimits(NativeLimitProgress source, DiagnosticLimitPreparation recipe)
    {
        source.Validate();
        recipe.Validate();
        if (recipe.LearnSpecialMagic != 0 && source.SpecialMagicFlags is null || recipe.Options978 is not null && source.Options978 is null)
        {
            throw new NotSupportedException("Diagnostic Limit preparation cannot invent an uninitialized special-magic or options owner.");
        }
        NativeLimitProgress next = source with
        {
            BlueMagicFlags = (ushort)(source.BlueMagicFlags | recipe.LearnBlueMagic),
            DuelFlags = (ushort)(source.DuelFlags | recipe.LearnDuel),
            AngeloFlags = (byte)(source.AngeloFlags | recipe.LearnAngelo),
            SpecialMagicFlags = source.SpecialMagicFlags is byte special ? (byte)(special | recipe.LearnSpecialMagic) : null,
            Options978 = recipe.Options978 ?? source.Options978
        };
        next.Validate();
        JsonElement before = JsonSerializer.SerializeToElement(source);
        JsonElement after = JsonSerializer.SerializeToElement(next);
        ImmutableArray<DiagnosticPreparationChange> changes = JsonElement.DeepEquals(before, after) ? [] : [new("limit-progress", 0, before, after)];
        return new(next, changes);
    }
}
