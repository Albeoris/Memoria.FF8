using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGraphicsBinding(string ActorId, byte ModelId, NativeBattleGraphicsResource Resource)
{
    public NativeBattleVector? InitialPosition { get; init; }
}
public sealed record NativeBattleGraphicsDefinition(NativeBattleGraphicsRules Rules, ImmutableArray<NativeBattleGraphicsBinding> Bindings, ImmutableArray<string> InitialLoadOrder);
public sealed record NativeBattleGraphicsRequest(ushort Command, byte? ModelIndex, byte State = 0, bool Entry = false);
public sealed record NativeBattleGraphicsState([property: JsonRequired] ImmutableArray<NativeBattleGraphicsModel> Models, [property: JsonRequired] ImmutableArray<string?> ActorIds, [property: JsonRequired] uint Flags, [property: JsonRequired] long Callback)
{
    [JsonRequired]
    public ImmutableArray<NativeBattleContactPacket> PendingContacts { get; init; } = [];
    [JsonRequired]
    public NativeBattleEffectsState Effects { get; init; } = NativeBattleEffectsState.Clear();
    [JsonRequired]
    public NativeBattleAuxiliaryState Auxiliary { get; init; } = NativeBattleAuxiliaryState.Clear();
    [JsonRequired]
    public NativeBattleModuleState ModuleEffects { get; init; } = NativeBattleModuleState.Clear();
    [JsonRequired]
    public ImmutableArray<NativeBattleModuleDraw> ModuleDraws { get; init; } = [];
    [JsonRequired]
    public byte EntryGatePhase { get; init; }
    [JsonRequired]
    public byte EntryBarrierCompletion { get; init; }
    [JsonRequired]
    public int ModelLoadCount { get; init; }
    [JsonRequired]
    public bool EntryReady { get; init; }
    [JsonRequired]
    public NativeBattleDescriptorState Descriptors { get; init; } = NativeBattleDescriptorState.Clear();
    [JsonRequired]
    public byte EntryLifecycle { get; init; }
    [JsonRequired]
    public int? EntryBarrierPacket { get; init; }
    [JsonRequired]
    public bool? NativeMenuControl942Bit8 { get; init; }
    [JsonIgnore]
    public ImmutableArray<NativeBattleGraphicsRequest> Requests => Descriptors.Queue.Select(slot => new NativeBattleGraphicsRequest(Descriptors.Packets[slot].Command, Descriptors.Packets[slot].Index is >= 0 and < 7 ? (byte)Descriptors.Packets[slot].Index : null, Descriptors.Packets[slot].PriorityState == 255 ? (byte)255 : (byte)(Descriptors.Packets[slot].PriorityState & 15), slot == EntryBarrierPacket)).ToImmutableArray();
    [JsonRequired]
    public string? ActionActorId { get; init; }
    [JsonRequired]
    public bool ActionCallbackReady { get; init; }
    public NativeBattleActionGeometry? ActionContext { get; init; }
    [JsonRequired]
    public long PresentationSubticks { get; init; }

    public void Validate()
    {
        ActionContext?.Validate();
        if (Models.IsDefault || Models.Length != 7 || ActorIds.IsDefault || ActorIds.Length != 7 || Callback < 0 || PresentationSubticks is < 0 or >= TimeSpan.TicksPerSecond || ModelLoadCount < 0 || EntryGatePhase > 2 || EntryBarrierCompletion is not (0 or 255) || EntryLifecycle > 3 || EntryBarrierPacket is < 0 or >= 30 || Descriptors is null || PendingContacts.IsDefault || Models.Where((model, index) => model is null || model.Index != index).Any())
        {
            throw new InvalidDataException("Native graphics state requires seven physical model owners and valid callback gates.");
        }
        foreach (NativeBattleGraphicsModel model in Models)
        {
            if ((model.Flags0 & 2) != 0 && (ActorIds[model.Index] is null || model.SequenceState is null || model.ResourceFlags44 is null || model.Flags8 is null || model.Flags2 is null))
            {
                throw new InvalidDataException("Loaded native graphics row lacks its actor, resource or model fields.");
            }
            model.SequenceState?.Validate();
            if (model.StatusTimers.IsDefault || model.StatusTimers.Length != 32)
            {
                throw new InvalidDataException("Native model requires its32 status-effect timer fields.");
            }
        }
        Effects.Validate();
        Descriptors.Validate();
        Auxiliary.Validate();
        ModuleEffects.Validate();
        if (ModuleDraws.IsDefault || ModuleDraws.Any(draw => draw is null || !Enum.IsDefined(draw.Kind)))
        {
            throw new InvalidDataException("Native effect module requires its current graphics callback draw receipts.");
        }
        foreach (NativeBattleContactPacket packet in PendingContacts)
        {
            packet.Validate();
        }
    }
}
