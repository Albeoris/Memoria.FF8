using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicPacketScope(string CommonBank, byte CommonBuffer, int CommonStart, int CommonEnd, uint PrivateStart, uint PrivateEnd);
public sealed record NativeBattleCastingSequenceProvenance(NativeBattlePilotVisualIdentity Identity, uint ScriptAddress, uint FileAddress, int ScriptOffset, byte Actor, int Sequence, long ContextBirth);
public sealed record NativeBattleCastingTaskProvenance(NativeBattlePilotVisualIdentity Identity, NativeBattlePilotVisualIdentity Parent, NativeBattleCastingSequenceProvenance Source, NativeBattleModuleKind Kind, uint? ResourceAddress = null);
public sealed record NativeBattleCastingPresentationState(ImmutableDictionary<int, NativeBattleCastingSequenceProvenance> Sequences, ImmutableDictionary<int, NativeBattleCastingTaskProvenance> Tasks, long NextBirth = 1, long? Cinematic18Birth = null);

public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore]
    public NativeBattleCinematicPacketScope? PresentationPacketScope { get; init; }
}

public sealed partial record NativeBattleModuleState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCastingPresentationState? CastingPresentation { get; init; }

    internal NativeBattleModuleState EnterCinematic18Presentation()
    {
        NativeBattleCastingPresentationState presentation = CastingPresentation ?? new(ImmutableDictionary<int, NativeBattleCastingSequenceProvenance>.Empty, ImmutableDictionary<int, NativeBattleCastingTaskProvenance>.Empty);
        return this with { CastingPresentation = presentation with { Cinematic18Birth = presentation.NextBirth, NextBirth = checked(presentation.NextBirth + 1) } };
    }

    internal NativeBattleModuleState BindCastingSequence(int actor, NativeBattleCastingSequenceProvenance? source)
    {
        if (CastingPresentation is null && source is null)
        {
            return this;
        }
        NativeBattleCastingPresentationState presentation = CastingPresentation ?? new(ImmutableDictionary<int, NativeBattleCastingSequenceProvenance>.Empty, ImmutableDictionary<int, NativeBattleCastingTaskProvenance>.Empty);
        if (source is null)
        {
            return this with { CastingPresentation = presentation with { Sequences = presentation.Sequences.Remove(actor) } };
        }
        source = source with { Identity = source.Identity with { Generation = presentation.NextBirth } };
        return this with { CastingPresentation = presentation with { Sequences = presentation.Sequences.SetItem(actor, source), NextBirth = checked(presentation.NextBirth + 1) } };
    }

    internal NativeBattleModuleState ForgetCastingSlot(int slot)
    {
        return CastingPresentation is { } presentation ? this with { CastingPresentation = presentation with { Tasks = presentation.Tasks.Remove(slot) } } : this;
    }

    internal NativeBattleModuleState RecordCastingTask(int slot, NativeBattleCastingSequenceProvenance source, NativeBattlePilotVisualIdentity parent)
    {
        NativeBattleModuleTask task = Slots[slot];
        NativeBattleCastingPresentationState presentation = CastingPresentation ?? new(ImmutableDictionary<int, NativeBattleCastingSequenceProvenance>.Empty, ImmutableDictionary<int, NativeBattleCastingTaskProvenance>.Empty);
        NativeBattlePilotVisualIdentity identity = new(18, DescriptorAddress, slot, presentation.NextBirth, parent.Primitive) { ResourceGeneration = unchecked(parent.Generation * 397 ^ parent.Bank ^ parent.Slot ^ parent.ResourceGeneration) };
        NativeBattleCastingTaskProvenance entry = new(identity, parent, source, task.Kind);
        return this with { CastingPresentation = presentation with { Tasks = presentation.Tasks.SetItem(slot, entry), NextBirth = checked(presentation.NextBirth + 1) }, Slots = Slots.SetItem(slot, task with { PilotIdentity = identity, PresentationOwnerEffect = 18 }) };
    }

    internal NativeBattleModuleState RestoreCastingPresentation(Func<int, byte?> sequence)
    {
        if (CastingPresentation is not { } presentation)
        {
            return this;
        }
        if (presentation.Sequences.Count > 7 || presentation.Tasks.Count > TaskCapacity || presentation.NextBirth <= 0 || presentation.Cinematic18Birth is not > 0 || presentation.Cinematic18Birth >= presentation.NextBirth)
        {
            throw new InvalidDataException("Casting presentation provenance exceeds its actual model/task owners.");
        }
        foreach ((int actor, NativeBattleCastingSequenceProvenance source) in presentation.Sequences)
        {
            NativeBattleControl18Presentation.Validate(source);
            ValidateOrdinals(source);
            if (actor != source.Actor)
            {
                throw new InvalidDataException("Casting sequence source disagrees with its physical model key.");
            }
            if (sequence(actor) != source.Sequence)
            {
                presentation = presentation with { Sequences = presentation.Sequences.Remove(actor) };
            }
        }
        NativeBattleModuleState state = this;
        foreach ((int slot, NativeBattleCastingTaskProvenance entry) in presentation.Tasks)
        {
            NativeBattleControl18Presentation.Validate(entry.Source);
            ValidateOrdinals(entry.Source);
            if ((uint)slot >= Slots.Length || entry.Identity.Effect != 18 || entry.Identity.Bank != DescriptorAddress || entry.Identity.Slot != slot || entry.Identity.Generation <= entry.Parent.Generation || entry.Identity.Generation >= presentation.NextBirth || entry.Parent.Effect != 18 || entry.Parent.Generation < entry.Source.Identity.Generation || entry.Parent.Generation >= presentation.NextBirth || entry.Identity.ResourceGeneration != unchecked(entry.Parent.Generation * 397 ^ entry.Parent.Bank ^ entry.Parent.Slot ^ entry.Parent.ResourceGeneration) || (entry.Parent != entry.Source.Identity && (entry.Parent.Bank != DescriptorAddress || entry.Parent.Slot < 0 || entry.Parent.Slot >= TaskCapacity)) || !NativeBattleControl18Presentation.IsCasting(entry.Kind) || Slots[slot].Kind != entry.Kind)
            {
                throw new InvalidDataException("Casting presentation provenance disagrees with its actual native task allocation.");
            }
            NativeBattleModuleTask task = Slots[slot];
            if ((task.Flags & 1) == 0)
            {
                presentation = presentation with { Tasks = presentation.Tasks.Remove(slot) };
            }
            else
            {
                state = state with { Slots = state.Slots.SetItem(slot, task with { PilotIdentity = entry.Identity, PresentationOwnerEffect = 18 }) };
            }
        }
        return state with { CastingPresentation = presentation };

        void ValidateOrdinals(NativeBattleCastingSequenceProvenance source)
        {
            if (source.ContextBirth >= source.Identity.Generation || source.ContextBirth > presentation.Cinematic18Birth || source.Identity.Generation >= presentation.NextBirth)
            {
                throw new InvalidDataException("Casting presentation birth ordinals disagree with their retained context and next allocation.");
            }
        }
    }

    internal NativeBattleModuleState BindCastingResource(int slot, NativeBattleCastingTaskProvenance receipt, uint resource)
    {
        NativeBattleCastingPresentationState presentation = CastingPresentation ?? throw new InvalidDataException("Casting draw lost its explicit source provenance.");
        if (receipt.ResourceAddress is uint previous && previous != resource)
        {
            throw new InvalidDataException("Casting resource changed within its actual native task lifetime.");
        }
        if (!presentation.Tasks.TryGetValue(slot, out NativeBattleCastingTaskProvenance? entry) || entry.Identity != receipt.Identity)
        {
            return this;
        }
        return this with { CastingPresentation = presentation with { Tasks = presentation.Tasks.SetItem(slot, entry with { ResourceAddress = resource }) } };
    }
}

public sealed partial record NativeBattleModuleTask
{
    [JsonIgnore]
    public ushort? PresentationOwnerEffect { get; init; }
}

public sealed partial record NativeBattleModuleDraw
{
    [JsonIgnore]
    public ushort? PresentationOwnerEffect { get; init; }
    [JsonIgnore]
    public int? PresentationSourceSlot { get; init; }
    [JsonIgnore]
    public NativeBattleModuleKind? PresentationSourceKind { get; init; }
    [JsonIgnore]
    public NativeBattleCastingTaskProvenance? PresentationSource { get; init; }
}

internal static class NativeBattleControl18Presentation
{
    internal static bool IsCasting(NativeBattleModuleKind kind) => kind is NativeBattleModuleKind.CastingSeed574C80 or NativeBattleModuleKind.CastingRing5753A0 or NativeBattleModuleKind.CastingGround575250 or NativeBattleModuleKind.CastingParticle574F50 or NativeBattleModuleKind.CastingTrail5751A0;

    internal static NativeBattleOriginalEffectState? RecordCurrentPackets(NativeBattleOriginalEffectState? effect, long? contextBirth, NativeBattleCastingTaskProvenance receipt, string bank, byte buffer, int start, NativeBattleModulePacketBatch batch)
    {
        if (effect?.Binding.Effect != 18 || contextBirth != receipt.Source.ContextBirth)
        {
            return effect;
        }
        ImmutableArray<NativeBattlePrimitiveReference> references = batch.CapturedWrites is { } writes ? NativeBattleEffectPresentationPackets.Record(effect.PresentationPackets, bank, buffer, start, writes) : effect.PresentationPackets.AddRange(batch.Primitives.Select(item => new NativeBattlePrimitiveReference(bank, buffer, checked(start + item.ByteOffset))));
        return effect with { PresentationPackets = references };
    }

    internal static void Validate(NativeBattleCastingSequenceProvenance source)
    {
        if (source.Identity.Effect != 18 || source.Identity.Generation <= 0 || source.Identity.Bank < NativeBattleEffectNodeMemory.ArenaAddress || (long)source.Identity.Bank - NativeBattleEffectNodeMemory.ArenaAddress >= NativeBattleEffectArena.Length || source.Identity.Slot < 0 || source.Identity.Slot >= 128 || source.ContextBirth <= 0 || source.Actor >= 7 || source.Sequence < 0 || source.Sequence > 255 || source.ScriptOffset < 0 || source.ScriptAddress != unchecked(source.FileAddress + (uint)source.ScriptOffset))
        {
            throw new InvalidDataException("Casting presentation requires its explicit executed cinematic sequence source.");
        }
    }
}
