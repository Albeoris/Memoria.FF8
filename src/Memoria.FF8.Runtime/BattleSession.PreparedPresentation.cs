using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeBattlePreparedPresentation? PreparedPresentation { get; private set; }
    private long preparedPresentationVersion;

    /// <summary>Called at completed host/command boundaries. This composes presentation data, never a battle save.</summary>
    public void RefreshPreparedPresentation()
    {
        if (expandedPresentationProcess is { } process)
        {
            PreparedPresentation = process.PreparedPresentation;
            return;
        }
        RefreshPreparedPresentation(this, null);
    }

    internal void RefreshHostedPreparedPresentation(BattleSession logicalOwner, NativeBattleModelLifecycleServices services, BattleModelRoster? rosterAtReceipts = null)
    {
        ArgumentNullException.ThrowIfNull(logicalOwner);
        ArgumentNullException.ThrowIfNull(services);
        if (!hostedBattlePresentation || ReferenceEquals(this, logicalOwner) || !logicalOwner.UsesExpandedRoster ||
            NativeGraphics is not { PrimitiveFrame: not null } graphics)
        {
            throw new InvalidOperationException("Hosted frame publication requires the current presentation process and expanded logical owner.");
        }
        BattleModelRoster roster = HostedPlannedModelRoster;
        NativeBattleModelLifecycleServices currentFacts = rosterAtReceipts is not null && !ReferenceEquals(rosterAtReceipts, roster)
            ? logicalOwner.CaptureHostedModelLifecycleServices(roster, hostedModelRegistry)
            : services;
        currentFacts.ValidateCurrentModels(roster, graphics);
        RefreshPreparedPresentation(logicalOwner, services);
    }

    private void RefreshPreparedPresentation(BattleSession actorOwner, NativeBattleModelLifecycleServices? hostedServices)
    {
        if (NativeGraphics is not { PrimitiveFrame: { } frame } graphics)
        {
            PreparedPresentation = null;
            return;
        }
        NativeBattlePreparedPrimitiveFrame primitives = PrepareNativePrimitiveFrame(frame, hostedServices);
        if (PreparedPresentation?.Primitives == primitives)
        {
            primitives = PreparedPresentation.Primitives;
        }
        ImmutableArray<NativeBattlePreparedActor>.Builder actors = ImmutableArray.CreateBuilder<NativeBattlePreparedActor>();
        foreach (BattleModelEntry<BattleModelOwner> entry in graphics.ActorModels.Entries)
        {
            if (entry.Owner is not (BattlePartyActorModel or BattleEnemyActorModel) || entry.State.BodyTextureSlots is not { } body)
            {
                continue;
            }
            string actorId = RequireCanonicalGraphicsActorId(entry.Owner);
            if (hostedServices is not null)
            {
                BattleCombatant actor = actorOwner.GetActor(actorId);
                actors.Add(new(actor.Id, actor.Name, actor.IsPlayer, actor.ModelId, actor.Costume, actor.WeaponId, actor.SceneSlot, actorOwner.GetGuardianFormVisual(actorId), body, entry.State.LinkedTextureSlots));
            }
            else
            {
                BattleCombatantDefinition actor = GetCombatantDefinition(actorId);
                actors.Add(new(actor.Id, actor.Name, actor.IsPlayer, actor.ModelId, actor.Costume, actor.WeaponId, actor.SceneSlot, GetGuardianFormVisual(actorId), body, entry.State.LinkedTextureSlots));
            }
        }
        NativeBattlePreparedScan? scan = PrepareScanPresentation(graphics, actorOwner);
        ushort? pilotEffect = graphics.OriginalEffect?.Binding.Effect is ushort effect && NativeBattleSmoothPresentation.Supports(effect) ? effect : null;
        PreparedPresentation = new(++preparedPresentationVersion, graphics.Callback, actorOwner.Frame, actorOwner.GuardianAnimationSkipPending, primitives, NativeEffectMemory?.Textures, NativeEffectMemory?.PrimitiveProcess?.TextureWindowCache, NativeEffectMemory?.UiImmediateFrame, EncounterPresentationCamera, graphics.Effects.TextDraws, NativeStage?.ResourceStageId, NativeStage?.TextureAnimationStartCallback, actors.ToImmutable(), scan) { PrimitiveCapabilities = definition.NativeGraphics?.PrimitiveCapabilities, SceneMotion = graphics.OriginalEffect is null, PilotEffect = pilotEffect, PilotGeneration = pilotEffect.HasValue && graphics.OriginalEffect?.Pools is { Length: > 0 } pilotPools ? NativeBattlePilotVisualLifetimes.EpochFor(pilotPools[0].RequireOwner()) : 0, PilotCamera = nativePilotSourceCamera, PilotProjection = nativePilotSourceProjection };
    }

    private NativeBattlePreparedPrimitiveFrame PrepareNativePrimitiveFrame(NativeBattlePrimitiveFrame frame, NativeBattleModelLifecycleServices? hostedServices = null)
    {
        NativeBattlePreparedPrimitiveFrame primitives = NativeBattlePreparedPrimitiveFrame.From(frame);
        ImmutableDictionary<string, float>? opacities = hostedBattlePresentation
            ? (hostedServices ?? hostedFrameServices)?.ActorOpacities ?? throw new InvalidOperationException("Hosted primitive publication requires the current frame's actor opacities.")
            : ReadPreparedActorOpacities();
        return opacities is null ? primitives : primitives with { ActorOpacities = opacities };
    }

    private ImmutableDictionary<string, float>? ReadPreparedActorOpacities()
    {
        if (encounterArrivals.IsEmpty && encounterAllocation is not { Phase: BattleEncounterAllocationPhase.Loading or BattleEncounterAllocationPhase.Ready })
        {
            return null;
        }
        ImmutableDictionary<string, float> opacities = encounterArrivals.Where(state => !state.Complete || state.Track.OpacityTo != 1).ToImmutableDictionary(state => state.Track.ActorId, state => state.Sample.Opacity, StringComparer.Ordinal);
        if (encounterAllocation is { Phase: BattleEncounterAllocationPhase.Loading or BattleEncounterAllocationPhase.Ready } batch)
        {
            foreach (string id in batch.ActorIds)
            {
                opacities = opacities.SetItem(id, 0);
            }
        }
        return opacities;
    }

    private NativeBattlePreparedScan? PrepareScanPresentation(NativeBattleGraphicsState graphics, BattleSession actorOwner)
    {
        if (NativeEffectMemory?.Scan is not { Text.Initialized: true } scan || graphics.UiServices is not { } services)
        {
            return null;
        }
        NativeBattleModelAliases aliases = graphics.NativeBindings.CaptureAliases(graphics.ActorModels);
        var originalExtras = ImmutableDictionary.CreateBuilder<int, NativeBattlePreparedScanExtra>();
        foreach (BattleEnemyActorModel owner in graphics.ActorModels.Roster.Enemies.OfType<BattleEnemyActorModel>())
        {
            if (aliases.TryEncodeExact(owner, out byte operand))
            {
                originalExtras.Add(operand, CaptureScanExtra(actorOwner.GetActor(owner.Actor.ActorId)));
            }
        }
        var boundExtras = ImmutableDictionary.CreateBuilder<uint, NativeBattlePreparedScanExtra>();
        foreach (NativeBattleScanExtraTextBinding binding in scan.Results.ExtraTextBindings)
        {
            NativeBattlePreparedScanExtra captured = actorOwner.byId.TryGetValue(RequireCanonicalGraphicsActorId(binding.Owner), out BattleCombatant? actor)
                ? CaptureScanExtra(actor)
                : new(null, NativeBattlePreparedScanExtraFailure.MissingOwner);
            boundExtras.Add(binding.Address, captured);
        }
        NativeBattleScanByteOwner? empty = null;
        if (NativeEffectMemory.Effect68 is { } effect68)
        {
            var bytes = effect68.ReadView(NativeBattleEffect68Profile.ResultEmptyAddress, 1).ReadSnapshot(0, 1);
            empty = new(bytes.Data.ToImmutableArray(), bytes.Known.ToImmutableArray());
        }
        NativeBattleOriginalEffectState? effect = graphics.OriginalEffect;
        BattleModelOwner? selectedOwner = effect?.ScanSelectedModel;
        BattleCombatant? selected = selectedOwner is null ? null : actorOwner.byId.GetValueOrDefault(RequireCanonicalGraphicsActorId(selectedOwner));
        NativeBattlePreparedScanActor? identity = selected is null ? null : new(selected.ModelId, selected.Name, selected.IsPlayer, selected.HasNativeDisplayNameOverride);
        return new(scan.Text.ReadFrame(services.Flags942), scan.Results, scan.Presentation.Name269AA78, originalExtras.ToImmutable(), definition.NativeScanNames.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.ToImmutableArray()), effect?.Binding.ScanTextResources, empty, identity)
        {
            BoundEnemyExtra = boundExtras.ToImmutable()
        };
    }

    private static NativeBattlePreparedScanExtra CaptureScanExtra(BattleCombatant actor)
    {
        NativeBattleScanByteOwner row = new(new byte[71].ToImmutableArray(), new bool[71].ToImmutableArray());
        if (actor.NativeDisplayNameIndex is not int index)
        {
            return new(row.WriteByte(0, 0), NativeBattlePreparedScanExtraFailure.None);
        }
        if (actor.NativeAi is null)
        {
            return new(null, NativeBattlePreparedScanExtraFailure.MissingName);
        }
        if ((uint)index >= actor.NativeAi.NameBuffers.Length)
        {
            return new(null, NativeBattlePreparedScanExtraFailure.InvalidIndex);
        }
        if (actor.NativeAi.NameBuffers[index] is not { } name)
        {
            return new(null, NativeBattlePreparedScanExtraFailure.MissingName);
        }
        return name.Length > 71
            ? new(null, NativeBattlePreparedScanExtraFailure.InvalidLength)
            : new(row.Write(0, name), NativeBattlePreparedScanExtraFailure.None);
    }
}
