using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeBattlePreparedPresentation? PreparedPresentation { get; private set; }
    private long preparedPresentationVersion;

    /// <summary>Called at completed host/command boundaries. This composes presentation data, never a battle save.</summary>
    public void RefreshPreparedPresentation()
    {
        RefreshPreparedPresentation(this, null);
    }

    internal void RefreshHostedPreparedPresentation(BattleSession logicalOwner, NativeBattleModelLifecycleServices services)
    {
        ArgumentNullException.ThrowIfNull(logicalOwner);
        ArgumentNullException.ThrowIfNull(services);
        if (!hostedBattlePresentation || ReferenceEquals(this, logicalOwner) || !logicalOwner.UsesExpandedRoster ||
            NativeGraphics is not { PrimitiveFrame: not null, PresentationRoster: { } roster } graphics)
        {
            throw new InvalidOperationException("Hosted frame publication requires the current presentation process and expanded logical owner.");
        }
        if (!roster.Rows.SequenceEqual(logicalOwner.ReadExpandedPresentationRoster().Rows))
        {
            throw new InvalidDataException("Hosted frame publication roster differs from the current logical allocations.");
        }
        services.Validate(roster, graphics);
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
        for (int index = 0; index < graphics.ActorIds.Length; index++)
        {
            if (graphics.ActorIds[index] is string actorId && graphics.Models[index].BodyTextureSlots is { } body)
            {
                if (hostedServices is not null)
                {
                    BattleCombatant actor = actorOwner.GetActor(actorId);
                    actors.Add(new(actor.Id, actor.Name, actor.IsPlayer, actor.ModelId, actor.Costume, actor.WeaponId, actor.SceneSlot, actorOwner.GetGuardianFormVisual(actorId), body, graphics.Models[index].LinkedTextureSlots));
                }
                else
                {
                    BattleCombatantDefinition actor = GetCombatantDefinition(actorId);
                    actors.Add(new(actor.Id, actor.Name, actor.IsPlayer, actor.ModelId, actor.Costume, actor.WeaponId, actor.SceneSlot, GetGuardianFormVisual(actorId), body, graphics.Models[index].LinkedTextureSlots));
                }
            }
        }
        NativeBattlePreparedScan? scan = PrepareScanPresentation(graphics, actorOwner, hostedServices is null ? null : graphics.PresentationRoster);
        ushort? pilotEffect = graphics.OriginalEffect?.Binding.Effect is ushort effect && NativeBattleSmoothPresentation.Supports(effect) ? effect : null;
        PreparedPresentation = new(++preparedPresentationVersion, graphics.Callback, actorOwner.Frame, actorOwner.GuardianAnimationSkipPending, primitives, NativeEffectMemory?.Textures, NativeEffectMemory?.PrimitiveProcess?.TextureWindowCache, NativeEffectMemory?.UiImmediateFrame, EncounterPresentationCamera, graphics.Effects.TextDraws, NativeStage?.ResourceStageId, NativeStage?.TextureAnimationStartCallback, actors.ToImmutable(), scan) { SceneMotion = graphics.OriginalEffect is null, PilotEffect = pilotEffect, PilotGeneration = pilotEffect.HasValue && graphics.OriginalEffect?.Pools is { Length: > 0 } pilotPools ? NativeBattlePilotVisualLifetimes.EpochFor(pilotPools[0].RequireOwner()) : 0, PilotCamera = nativePilotSourceCamera, PilotProjection = nativePilotSourceProjection };
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

    private NativeBattlePreparedScan? PrepareScanPresentation(NativeBattleGraphicsState graphics, BattleSession actorOwner, NativeBattleGraphicsRoster? roster)
    {
        if (NativeEffectMemory?.Scan is not { Text.Initialized: true } scan || graphics.UiServices is not { } services)
        {
            return null;
        }
        ImmutableDictionary<int, NativeBattlePreparedScanExtra>.Builder extras = ImmutableDictionary.CreateBuilder<int, NativeBattlePreparedScanExtra>();
        IEnumerable<(int Index, BattleCombatant Actor)> enemies = roster is null
            ? actorOwner.combatants.Where(actor => actor.EncodeNativeSlot() is >= 3 and < 7).Select(actor => (actor.EncodeNativeSlot(), actor))
            : roster.EnemyRows.Select(row => (row, actorOwner.GetActor(roster[row].ActorId)));
        foreach ((int index, BattleCombatant actor) in enemies)
        {
            NativeBattlePreparedScanExtraFailure failure = NativeBattlePreparedScanExtraFailure.None;
            NativeBattleScanByteOwner row = new(new byte[71].ToImmutableArray(), new bool[71].ToImmutableArray());
            if (actor.NativeDisplayNameIndex is not int selectedName)
            {
                row = row.WriteByte(0, 0);
            }
            else if (actor.NativeAi is null)
            {
                failure = NativeBattlePreparedScanExtraFailure.MissingName;
            }
            else if ((uint)selectedName >= actor.NativeAi.NameBuffers.Length)
            {
                failure = NativeBattlePreparedScanExtraFailure.InvalidIndex;
            }
            else if (actor.NativeAi.NameBuffers[selectedName] is not { } name)
            {
                failure = NativeBattlePreparedScanExtraFailure.MissingName;
            }
            else if (name.Length > 71)
            {
                failure = NativeBattlePreparedScanExtraFailure.InvalidLength;
            }
            else
            {
                row = row.Write(0, name);
            }
            extras.Add(index, new(failure == NativeBattlePreparedScanExtraFailure.None ? row : null, failure));
        }
        NativeBattleScanByteOwner? empty = null;
        if (NativeEffectMemory.Effect68 is { } effect68)
        {
            var bytes = effect68.ReadView(NativeBattleEffect68Profile.ResultEmptyAddress, 1).ReadSnapshot(0, 1);
            empty = new(bytes.Data.ToImmutableArray(), bytes.Known.ToImmutableArray());
        }
        NativeBattleOriginalEffectState? effect = graphics.OriginalEffect;
        NativeBattleModulePoolTask? viewer = effect?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => task.Callback == 0x84F860);
        string? actorId = viewer is null ? null : graphics.ActorIds.ElementAtOrDefault(viewer.ReadByte(0x2D));
        BattleCombatant? selected = actorOwner.combatants.FirstOrDefault(actor => actor.Id == actorId);
        NativeBattlePreparedScanActor? identity = selected is null ? null : new(selected.ModelId, selected.Name, selected.IsPlayer, selected.HasNativeDisplayNameOverride);
        return new(scan.Text.ReadFrame(services.Flags942), scan.Results, scan.Presentation.Name269AA78, extras.ToImmutable(), definition.NativeScanNames.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.ToImmutableArray()), effect?.Binding.ScanTextResources, empty, identity);
    }
}
