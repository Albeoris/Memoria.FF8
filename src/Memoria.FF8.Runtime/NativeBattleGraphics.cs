using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGraphicsBinding(string ActorId, byte ModelId, NativeBattleGraphicsResource Resource)
{
    public NativeBattleVector? InitialPosition { get; init; }
}
public sealed record NativeBattlePrimitiveCapabilities([property: JsonRequired] bool GouraudAlpha)
{
    public NativeBattlePcDirectTextureFormat? DirectTextureFormat { get; init; }
    public bool? HardwareEnabled { get; init; }
    public uint? SpriteFilterStartupControl { get; init; }
    public NativeBattlePcFramebufferCaptureProfile? PcFramebufferCaptureProfile { get; init; }
    //The current PC RGBA renderer interpolates both diffuse RGB and alpha; this selects464220's4000-capable branch.
    //Its direct RGBA8 texture target preserves the original five-bit RGB channels through the explicit RGB555 conversion branch.
    public static NativeBattlePrimitiveCapabilities PrimaryRgba8() => new(true) { DirectTextureFormat = NativeBattlePcDirectTextureFormat.Rgb555, HardwareEnabled = true, SpriteFilterStartupControl = 2, PcFramebufferCaptureProfile = NativeBattlePcFramebufferCaptureProfile.PrimaryRgba8() };
}

public sealed partial record NativeBattleGraphicsDefinition(NativeBattleGraphicsRules Rules, ImmutableArray<NativeBattleGraphicsBinding> Bindings, ImmutableArray<string> InitialLoadOrder)
{
    public NativeBattlePrimitiveCapabilities? PrimitiveCapabilities { get; init; }
}
public sealed record NativeBattleGraphicsRequest(ushort Command, BattleModelOwner? ModelOwner, byte State = 0, bool Entry = false);
public sealed partial record NativeBattleGraphicsState([property: JsonRequired] BattleActorModels ActorModels, [property: JsonRequired] NativeBattleModelBoundary NativeBindings, [property: JsonRequired] uint Flags, [property: JsonRequired] long Callback, [property: JsonRequired] NativeBattleEffectsState Effects)
{
    [JsonRequired]
    public ImmutableArray<NativeBattleContactPacket> PendingContacts { get; init; } = [];
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
    public NativeBattleUiServices? UiServices { get; init; }
    [JsonRequired]
    public NativeBattleTriggerWidget? TriggerWidget { get; init; }
    [JsonRequired]
    public NativeBattleTriggerReceipt? PendingTrigger { get; init; }
    public BattleModelOwner? PendingTriggerSoundActor { get; init; }
    [JsonIgnore]
    public bool? NativeMenuControl942Bit8 => UiServices is null ? null : (UiServices.Flags942 & 8) != 0;
    [JsonIgnore]
    public ImmutableArray<NativeBattleGraphicsRequest> Requests => Descriptors.Queue.Select(slot => new NativeBattleGraphicsRequest(Descriptors.Packets[slot].Command, null, Descriptors.Packets[slot].PriorityState == 255 ? (byte)255 : (byte)(Descriptors.Packets[slot].PriorityState & 15), slot == EntryBarrierPacket)).ToImmutableArray();
    [JsonRequired]
    public string? ActionActorId { get; init; }
    [JsonRequired]
    public bool ActionCallbackReady { get; init; }
    public NativeBattleActionGeometry? ActionContext { get; init; }
    [JsonRequired]
    public NativeBattleActionGraphicsState? ActionGraphics { get; init; }
    [JsonRequired]
    public NativeBattleOriginalEffectState? OriginalEffect { get; init; }
    [JsonRequired]
    public byte? StageResourceIndex98990 { get; init; }
    [JsonRequired]
    public long PresentationSubticks { get; init; }

    public bool PresentationHalfSubtick { get; init; }

    public void Validate()
    {
        if (ActorModels is null || NativeBindings is null || Effects is null || Callback < 0 || PresentationSubticks is < 0 or >= TimeSpan.TicksPerSecond || ModelLoadCount < 0 || EntryGatePhase > 2 || EntryBarrierCompletion is not (0 or 255) || EntryLifecycle > 4 || EntryBarrierPacket is < 0 or >= 30 || Descriptors is null || PendingContacts.IsDefault)
        {
            throw new InvalidDataException("Native graphics state requires its model owners, native bindings and valid callback gates.");
        }
        NativeBindings.ValidateBindings(ActorModels);
        BattleModelRoster roster = ActorModels.Roster;
        GuardianFormPresentation?.Validate();
        if (GuardianAnimationSkip is { } skip && (!NativeBattleGuardianAnimationFinale.IsSupported(skip.Effect) || OriginalEffect?.Binding.Effect != skip.Effect))
        {
            throw new InvalidDataException("Saved summon skip request differs from its selected supported effect.");
        }
        if (GuardianFormHumanFocalToRestore is <= 0)
        {
            throw new InvalidDataException("Saved GF camera restoration requires a positive focal length.");
        }
        ActionContext?.Validate();
        if (OriginalEffect is { ReturnedDescriptor: 0 } completedEffect)
        {
            completedEffect.Validate();
        }
        else
        {
            OriginalEffect?.Validate(roster);
        }
        UiServices?.Validate();
        TriggerWidget?.Validate(roster);
        if ((Flags & 8) != 0 && UiServices is null)
        {
            throw new InvalidDataException("Native506CF0 stage has no initialized4A94D0 UI service owner; migrate the standalone snapshot explicitly.");
        }
        PendingTrigger?.Validate(roster);
        if (PendingTriggerSoundActor is { } soundActor)
        {
            RequireBoundOwner(roster, soundActor);
        }
        if (ActionGraphics is { } action)
        {
            ValidateActionOwners(roster, action);
        }
        foreach (BattleModelEntry<BattleModelOwner> entry in ActorModels.Entries)
        {
            NativeBattleGraphicsModel model = entry.State;
            if ((model.Flags0 & 2) != 0 && (model.SequenceState is null || model.ResourceFlags44 is null || model.Flags8 is null || model.Flags2 is null))
            {
                throw new InvalidDataException("Loaded native graphics model lacks its resource or model fields.");
            }
            if (model.LinkedModel is not null)
            {
                RequireBoundOwner(roster, model.ReadLinkedModel());
            }
            model.ValidatePartialStatus();
            model.SequenceState?.Validate();
            if (model.TextureResourceIndex is int textureAlias && (uint)textureAlias >= NativeBattleModelResourceRowAddress.MaximumEncodedRowCount)
            {
                throw new InvalidDataException("Native model texture resource does not identify its physical52-byte cache row.");
            }
            if (model.StatusTimers.IsDefault || model.StatusTimers.Length != 32)
            {
                throw new InvalidDataException("Native model requires its32 status-effect timer fields.");
            }
        }
        Effects.Validate(roster);
        Descriptors.Validate();
        Auxiliary.Validate();
        ModuleEffects.Validate();
        if (ModuleDraws.IsDefault || ModuleDraws.Any(draw => draw is null || !Enum.IsDefined(draw.Kind)))
        {
            throw new InvalidDataException("Native effect module requires its current graphics callback draw receipts.");
        }
        foreach (NativeBattleContactPacket packet in PendingContacts)
        {
            if (packet is null)
            {
                throw new InvalidDataException("Pending graphics contacts require actual packets.");
            }
            packet.Validate();
            ValidateContactOwners(roster, packet.Primary);
            if (packet.Secondary is { } secondary)
            {
                ValidateContactOwners(roster, secondary);
            }
        }
    }

    private static void ValidateActionOwners(BattleModelRoster roster, NativeBattleActionGraphicsState action)
    {
        if (action.Actor is null || action.TargetActors is null || action.CoverActors is null)
        {
            throw new InvalidDataException("Action graphics requires retained source, target and cover selectors.");
        }
        if (action.HiddenPartyActors is { } hiddenParty)
        {
            RequireBoundPartyTargets(roster, hiddenParty);
        }
        if (action.Guardian is { } guardian)
        {
            RequireBoundPartyTargets(roster, guardian.HiddenPartyActors);
            if (guardian.Neighbor?.Owner is { } neighbor && !roster.Contains(neighbor))
            {
                throw new InvalidDataException("The guardian service neighbor must remain a bound party model.");
            }
        }
        if (action.FirstHeaderTargetCount is < 0)
        {
            throw new InvalidDataException("Action graphics requires a nonnegative first-header target count.");
        }
    }

    private static void ValidateContactOwners(BattleModelRoster roster, NativeBattleContactHalf contact)
    {
        RequireBoundOwner(roster, contact.Recipient);
        if (contact.CoverSource is { } coverSource)
        {
            RequireBoundOwner(roster, coverSource);
        }
    }

    private static void RequireBoundPartyTargets(BattleModelRoster roster, BattleModelTargets? targets)
    {
        if (targets is null || targets.Enumerate().Any(owner => owner is not BattlePartyModelOwner || !roster.Contains(owner)))
        {
            throw new InvalidDataException("Hidden party selection requires bound party model owners.");
        }
    }

    private static void RequireBoundOwner(BattleModelRoster roster, BattleModelOwner? owner)
    {
        if (owner is null || !roster.Contains(owner))
        {
            throw new InvalidDataException("Graphics state requires a bound model owner.");
        }
    }
}
