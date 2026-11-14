using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal NativeBattleModelLifecycleServices CaptureExpandedModelLifecycleServices(NativeBattleGraphicsRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        NativeBattleModelActorFacts?[] facts = new NativeBattleModelActorFacts?[roster.ActorCount];
        foreach (BattleCombatant actor in combatants.Where(actor => actor.Slot is not null))
        {
            int row = RequireExpandedPresentationRow(roster, actor);
            if (facts[row] is not null || roster[row].IsParty != actor.IsPlayer)
            {
                throw new InvalidDataException("Expanded model lifecycle actor has a conflicting roster owner.");
            }
            facts[row] = new(actor.Id, actor.IsPlayer, actor.ModelId, actor.NativeStatusTimers[12], actor.NativeStatusTimers[10])
            {
                RawBattleStatuses = NativeBattleStatusBits(actor),
                CurrentHp = actor.CurrentHp,
                MaximumHp = actor.MaximumHp
            };
        }
        for (int row = 0; row < roster.ActorCount; row++)
        {
            if (roster.ContainsRow(row) && facts[row] is null)
            {
                throw new InvalidDataException("Expanded model lifecycle roster contains an actor without a current allocation.");
            }
        }
        return new(facts.ToImmutableArray()) { ActorOpacities = ReadPreparedActorOpacities() ?? ImmutableDictionary<string, float>.Empty };
    }

    private NativeBattleGraphicsState PublishHostedModelLifecycleCue(NativeBattleGraphicsState graphics, NativeBattleGraphicsModel model, NativeBattleModelActorFacts actor, NativeBattleSequenceCue cue, NativeBattleModelLifecycleServices services)
    {
        int row = model.Index;
        switch (cue.Kind)
        {
            case NativeBattleSequenceCueKind.Text:
                NativeBattleGraphicsResource resource = ReadHostedPresentationModelResource(row) ?? throw new InvalidDataException("Hosted Native91 lost its original actor resource binding.");
                services.Emit(new NativeBattleModelTextPublished(row, actor.ActorId, resource.ReadText91(cue.First), cue.Second, actor.IsParty ? null : BattleTextIdentity.Enemy(actor.OriginalModelId, cue.First)));
                break;
            case NativeBattleSequenceCueKind.SuspendParticipant:
                NativeBattleActionHeaderSlot? header = graphics.ActionGraphics is { } action && action.ActorIndex == row && nativeActionPackets.ActiveHeaderIndex == action.HeaderIndex
                    ? nativeActionPackets.Headers[action.HeaderIndex]
                    : null;
                services.Emit(new NativeBattleModelParticipantSuspended(row, actor.ActorId, checked((byte)cue.First)) { ActiveHeader = header });
                break;
            case NativeBattleSequenceCueKind.ActionCallback:
                throw new InvalidDataException("Native AA must publish its active header contacts inline.");
            case NativeBattleSequenceCueKind.TriggerWindow:
                graphics = PrepareNativeTriggerWidget(graphics with { Models = graphics.Models.SetItem(row, model) }, row, cue);
                services.Emit(new NativeBattleModelTriggerRegistered(row, actor.ActorId, graphics.TriggerWidget!));
                break;
        }
        return graphics;
    }
}
