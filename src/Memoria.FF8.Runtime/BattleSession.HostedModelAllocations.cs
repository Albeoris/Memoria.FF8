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
        var unloadChecks = new List<(HostedDispatchControl.LoadModel Load, ImmutableHashSet<BattleModelOwner> LaterOwners)>();
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
                case ExpandedHostedDescriptor { Control: HostedDispatchControl.LoadModel load }:
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

    private HostedModelLoadAdmission PrepareHostedModelLoadAdmission(BattlePresentationProcess process, HostedDispatchControl.LoadModel load, ImmutableHashSet<BattleModelOwner> protectedOwners)
    {
        BattleCombatant actor = GetActor(RequireCanonicalGraphicsActorId(load.ModelOwner));
        if (load.ModelOwner != CreatePresentationModelOwner(actor) || actor.Slot != load.Slot ||
            load.OriginalSlot is { } original && actor.NativeActorIndex != original.Offset)
        {
            throw new InvalidDataException("A staged model load requires its current typed slot and actual original alias.");
        }
        bool usesOriginalSceneResource = false;
        BattleSourcePlacementBinding? source = null;
        if (load is HostedDispatchControl.LoadEnemy enemy)
        {
            NativeBattleEnemyAllocation allocation = nativeAllocations.Single(candidate => candidate.Id == actor.Id);
            if (load.Slot.Side != BattleSide.Enemies || actor.SceneSlot != allocation.SceneSlot || actor.SourceDefinition.SceneSlot != allocation.SceneSlot ||
                enemy.SceneSlot != allocation.SceneSlot || enemy.ModelId != actor.ModelId)
            {
                throw new InvalidDataException("A staged enemy model load requires its exact native allocation and original scene prototype.");
            }
            usesOriginalSceneResource = allocation.ResolvedDefinition is null && allocation.DonorSource is null;
            if (allocation.DonorSource is { Group: { } group } donor)
            {
                source = new(group, donor.SceneSlot, donor.OriginalEnemyOrdinal);
            }
            else if (allocation.EncounterRole is not null)
            {
                source = allocation.TemplateSource ?? throw new InvalidDataException("A staged encounter copy requires its admitted source placement.");
            }
        }
        NativeBattleGraphicsBinding binding = PrepareModelBinding(load.ModelOwner, load.Slot);
        if (binding.ActorId != actor.Id || binding.InitialPosition is null || binding.ModelId != checked((byte)(actor.ModelId + (actor.IsPlayer ? 0 : 16))) ||
            binding.Resource.AnimationFrames.IsDefaultOrEmpty || binding.Resource.ModelLoadSources is not { IsDefaultOrEmpty: false })
        {
            throw new InvalidDataException("The installed model binding does not match its staged allocation.");
        }
        binding.Resource.Validate();
        return new(load, binding, protectedOwners, usesOriginalSceneResource) { Source = source };
    }
}
