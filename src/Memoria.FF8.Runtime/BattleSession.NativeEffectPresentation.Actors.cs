using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static bool IsBoundPresentationRow(BattleModelRoster roster, int row)
    {
        return (uint)row < (uint)roster.ActorCount && roster.Rows[row] is not null;
    }

    private static void ValidateHostedPresentationBindings(NativeBattleEffectPresentationInputs inputs, BattleModelRoster roster)
    {
        roster.Validate();
        int source = inputs.Source.Index;
        if (!IsBoundPresentationRow(roster, source) || inputs.Parameters.Actor78 != source || (inputs.SourceIsParty is bool side && roster[source].IsParty != side))
        {
            throw new InvalidDataException("The hosted effect source disagrees with its presentation roster row.");
        }
        if (inputs.NativeTargets.IsDefault || inputs.ForeignActors.IsDefault)
        {
            throw new InvalidDataException("The hosted effect requires explicit model bindings.");
        }

        BattleModelTargets occupied = default(BattleModelTargets).Add(source);
        foreach (NativeBattleEffectModelBinding target in inputs.NativeTargets)
        {
            if (target?.Model is not { } model || !IsBoundPresentationRow(roster, model.Index) || occupied.Contains(model.Index))
            {
                throw new InvalidDataException("A hosted native effect target requires a unique bound roster row.");
            }
            BattleModelOwner actor = roster[model.Index];
            if (!string.Equals(target.ActorId, actor.ActorId, StringComparison.Ordinal) || (target.IsParty is bool targetSide && targetSide != actor.IsParty))
            {
                throw new InvalidDataException("A hosted native effect target disagrees with its presentation actor identity or side.");
            }
            occupied = occupied.Add(model.Index);
        }
        foreach (NativeBattleForeignActor foreign in inputs.ForeignActors)
        {
            if (foreign is null || !IsBoundPresentationRow(roster, foreign.Index) || occupied.Contains(foreign.Index) ||
                roster[foreign.Index].IsParty != foreign.IsParty || (foreign.NativeModel is { } native && native.Index != foreign.Index))
            {
                throw new InvalidDataException("A hosted foreign effect actor requires a unique matching presentation row and side.");
            }
            occupied = occupied.Add(foreign.Index);
        }
        if (occupied.Count != roster.Rows.Count(actor => actor is not null))
        {
            throw new InvalidDataException("A hosted effect lacks a model binding for a presentation actor.");
        }
    }

    private void BindHostedCinematicActors()
    {
        if (NativeGraphics is not { PresentationRoster: { } roster } graphics || NativeEffectMemory?.ScriptedEffects is not { } core)
        {
            return;
        }
        NativeBattleCinematicActorContext context;
        if (core.PresentationActors is { } retained)
        {
            if (!retained.Roster.Rows.SequenceEqual(roster.Rows))
            {
                throw new InvalidDataException("The retained cinematic actor context belongs to a different presentation roster.");
            }
            context = retained.RefreshCache(graphics.Models);
        }
        else
        {
            context = SeedHostedCinematicActors(core.ReadHeader(), NativeBattleCinematicActorContext.FromGraphics(roster, graphics.Models));
        }
        NativeEffectMemory = NativeEffectMemory! with { ScriptedEffects = core with { PresentationActors = context } };
    }

    private static NativeBattleCinematicActorContext SeedHostedCinematicActors(NativeBattleWaterHeader header, NativeBattleCinematicActorContext context)
    {
        BattleModelRoster roster = context.Roster;
        ImmutableArray<byte?>.Builder ordinals = context.ActorOrdinals.ToBuilder();
        for (int row = 0; row < roster.ActorCount; row++)
        {
            if (roster.Rows[row] is null)
            {
                continue;
            }
            if (row >= NativeBattleGraphicsModelAddress.NativeRowCount)
            {
                // Extension rows have no retained original ordinal bytes.
                ordinals[row] = 0;
            }
            else if (header.Known[0xCC + row] && header.Bytes[0xCC + row] < roster.ActorCount)
            {
                ordinals[row] = header.Bytes[0xCC + row];
            }
        }
        context = context with { ActorOrdinals = ordinals.MoveToImmutable() };

        if (!header.Known[0x17A] || header.Bytes[0x17A] > 6)
        {
            return context;
        }
        int savedCount = header.Bytes[0x17A];
        for (int index = 0; index < savedCount; index++)
        {
            byte actor = header.Bytes[0x174 + index];
            if (!header.Known[0x174 + index] || actor >= NativeBattleGraphicsModelAddress.NativeRowCount || !IsBoundPresentationRow(roster, actor))
            {
                return context;
            }
        }
        return context.WithSavedActors(header.Bytes.Slice(0x174, savedCount));
    }
}
