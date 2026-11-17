using static Memoria.FF8.Runtime.BattlePresentationProcess;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{

    private void StageHostedNativeAllocation(BattleEnemyActorModel owner, BattleRosterSlot slot, NativeBattleModelSlot? originalSlot, int sceneSlot, int modelId, bool activate)
    {
        StageExpandedHostedControl(new HostedDispatchControl.LoadEnemy(owner, slot, originalSlot, sceneSlot, modelId));
        StageExpandedHostedControl(new HostedDispatchControl.AllocationStatistics());
        if (activate)
        {
            StageExpandedHostedControl(originalSlot is { } original
                ? new HostedDispatchControl.ActivateOriginal(original)
                : new HostedDispatchControl.Model(113, owner));
        }
        StageExpandedHostedControl(new HostedDispatchControl.AllocationGate());
    }

    private void StageHostedNativeActivation(BattlePreviousTarget? target)
    {
        switch (target)
        {
            case NativeBattleTargetRequestAdapter.OriginalPreviousOperand original when NativeBattleSlots.Contains(original.Operand):
                StageExpandedHostedControl(new HostedDispatchControl.ActivateOriginal(NativeBattleModelSlot.Decode(checked((byte)original.Operand))));
                break;
            case BattlePreviousTarget.Actor captured when FindTargetActor(captured.Reference) is { } actor:
                StageExpandedHostedControl(new HostedDispatchControl.Model(113, CreatePresentationModelOwner(actor)));
                break;
            default:
                throw new NotSupportedException("Hosted native activation requires its original slot or captured actor.");
        }
    }

    private ImmutableArray<HostedModelLoadAdmission> PrepareExpandedModelLoadAdmissions(BattlePresentationProcess process, int firstOperation = 0)
    {
        var unloadChecks = new List<(HostedDispatchControl.LoadEnemy Load, ImmutableHashSet<BattleModelOwner> LaterOwners)>();
        HashSet<BattleModelOwner> laterOwners = [];
        for (int i = expandedOperationCursor.Operations.Length - 1; i >= firstOperation; i--)
        {
            switch (expandedOperationCursor.Operations[i])
            {
                case ExpandedHostedInvocation invocation:
                    laterOwners.Add(invocation.Header.Source);
                    foreach (ExpandedBattleContact contact in invocation.Contacts)
                    {
                        laterOwners.Add(contact.Packet.Primary.Recipient);
                        AddOptional(contact.Packet.Primary.CoverSource);
                        AddOptional(contact.Packet.Secondary?.Recipient);
                        AddOptional(contact.Packet.Secondary?.CoverSource);
                    }
                    break;
                case ExpandedHostedDescriptor { Control: HostedDispatchControl.Model model }:
                    laterOwners.Add(model.Owner);
                    break;
                case ExpandedHostedDescriptor { Control: HostedDispatchControl.LoadEnemy load }:
                    unloadChecks.Add((load, laterOwners.ToImmutableHashSet()));
                    break;
            }
        }
        unloadChecks.Reverse();
        return unloadChecks.Select(admission => PrepareHostedModelLoadAdmission(process, admission.Load, admission.LaterOwners)).ToImmutableArray();

        void AddOptional(BattleModelOwner? owner)
        {
            if (owner is not null)
            {
                laterOwners.Add(owner);
            }
        }
    }

    private HostedModelLoadAdmission PrepareHostedModelLoadAdmission(BattlePresentationProcess process, HostedDispatchControl.LoadEnemy load, ImmutableHashSet<BattleModelOwner> protectedOwners)
    {
        BattleCombatant actor = GetActor(load.Owner.Actor.ActorId);
        NativeBattleEnemyAllocation allocation = nativeAllocations.Single(candidate => candidate.Id == actor.Id);
        if (load.Owner != CreatePresentationModelOwner(actor) || load.Slot.Side != BattleSide.Enemies || actor.Slot != load.Slot ||
            (load.OriginalSlot is { } original && actor.NativeActorIndex != original.Offset) ||
            actor.SceneSlot != allocation.SceneSlot || actor.SourceDefinition.SceneSlot != allocation.SceneSlot ||
            load.SceneSlot != allocation.SceneSlot || load.ModelId != actor.ModelId)
        {
            throw new InvalidDataException("A staged model load requires its exact native allocation and original scene prototype.");
        }
        NativeBattleGraphicsBinding binding = PrepareModelBinding(load.Owner, load.Slot);
        if (binding.ActorId != actor.Id || binding.InitialPosition is null || binding.ModelId != checked((byte)(actor.ModelId + 16)) ||
            binding.Resource.AnimationFrames.IsDefaultOrEmpty || binding.Resource.ModelLoadSources is not { IsDefaultOrEmpty: false })
        {
            throw new InvalidDataException("The installed model binding does not match its staged allocation.");
        }
        binding.Resource.Validate();
        return new(load, binding, protectedOwners, allocation.ResolvedDefinition is null);
    }
}
