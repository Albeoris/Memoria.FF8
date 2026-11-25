using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    public NativeBattlePreparedPresentation? PreparedPresentation { get; private set; }
    private long preparedPresentationVersion;

    private void PublishPreparedPresentation(NativeBattleModelLifecycleServices frame, BattleModelRoster? rosterAtReceipts = null)
    {
        if (NativeGraphics is not { PrimitiveFrame: { } primitiveFrame } graphics)
        {
            throw new InvalidOperationException("Battle frame publication requires its primitive frame.");
        }
        BattlePresentationServices host = services ?? throw new InvalidOperationException("Battle frame publication requires its bound scene facts.");
        BattleModelRoster roster = HostedPlannedModelRoster;
        NativeBattleModelLifecycleServices current = rosterAtReceipts is not null && !ReferenceEquals(rosterAtReceipts, roster)
            ? host.CaptureModels(roster, hostedModelRegistry)
            : frame;
        current.ValidateCurrentModels(roster, graphics);
        NativeBattlePreparedPrimitiveFrame primitives = PrepareNativePrimitiveFrame(graphics, primitiveFrame, frame);
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
            actors.Add(host.ReadPreparedActor(entry.Owner, body, entry.State.LinkedTextureSlots));
        }
        NativeBattlePreparedScan? scan = PrepareScanPresentation(graphics, host);
        ushort? pilotEffect = graphics.OriginalEffect?.Binding.Effect is ushort effect && NativeBattleSmoothPresentation.Supports(effect) ? effect : null;
        var (frameNumber, skippingGuardian) = host.ReadDisplayFrame();
        PreparedPresentation = new(++preparedPresentationVersion, graphics.Callback, frameNumber, skippingGuardian, primitives, NativeEffectMemory?.Textures, NativeEffectMemory?.PrimitiveProcess?.TextureWindowCache, NativeEffectMemory?.UiImmediateFrame, EncounterPresentationCamera, graphics.Effects.TextDraws, NativeStage?.ResourceStageId, NativeStage?.TextureAnimationStartCallback, actors.ToImmutable(), scan) { PrimitiveCapabilities = this.resources.NativeGraphics?.PrimitiveCapabilities, SceneMotion = graphics.OriginalEffect is null, PilotEffect = pilotEffect, PilotGeneration = pilotEffect.HasValue && graphics.OriginalEffect?.Pools is { Length: > 0 } pilotPools ? NativeBattlePilotVisualLifetimes.EpochFor(pilotPools[0].RequireOwner()) : 0, PilotCamera = nativePilotSourceCamera, PilotProjection = nativePilotSourceProjection };
    }

    private NativeBattlePreparedPrimitiveFrame PrepareNativePrimitiveFrame(NativeBattleGraphicsState graphics, NativeBattlePrimitiveFrame frame, NativeBattleModelLifecycleServices? hostedServices = null)
    {
        NativeBattlePreparedPrimitiveFrame primitives = NativeBattlePreparedPrimitiveFrame.From(frame);
        ImmutableDictionary<string, float>? opacities = HasBattleEntry
            ? (hostedServices ?? hostedFrameServices)?.ActorOpacities ?? throw new InvalidOperationException("Hosted primitive publication requires the current frame's actor opacities.")
            : null;
        if (opacities is not null)
        {
            primitives = primitives with { ActorOpacities = ReadEncounterArrivalOpacities(opacities) };
        }
        ImmutableHashSet<string>.Builder? petrifiedEnemies = null;
        foreach (BattleModelEntry<BattleEnemyModelOwner> entry in graphics.ActorModels.Enemies.Entries)
        {
            if (entry.Owner is BattleEnemyActorModel && (entry.State.Flags8.GetValueOrDefault() & 0x10) != 0)
            {
                (petrifiedEnemies ??= ImmutableHashSet.CreateBuilder<string>()).Add(ReadGraphicsPresentationId(entry.Owner));
            }
        }
        return petrifiedEnemies is null ? primitives : primitives with { PetrifiedEnemyActors = petrifiedEnemies.ToImmutable() };
    }

    private NativeBattlePreparedScan? PrepareScanPresentation(NativeBattleGraphicsState graphics, BattlePresentationServices host)
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
                originalExtras.Add(operand, host.ReadPreparedScanExtra(owner));
            }
        }
        var boundExtras = ImmutableDictionary.CreateBuilder<uint, NativeBattlePreparedScanExtra>();
        foreach (NativeBattleScanExtraTextBinding binding in scan.Results.ExtraTextBindings)
        {
            NativeBattlePreparedScanExtra captured = host.ReadPreparedScanExtra(binding.Owner);
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
        NativeBattlePreparedScanActor? identity = selectedOwner is null ? null : host.ReadPreparedScanActor(selectedOwner);
        if (effect?.Binding.Effect != 40 && identity is not null)
        {
            identity = identity with { TargetData = null };
        }
        return new(scan.Text.ReadFrame(services.Flags942), scan.Results, scan.Presentation.Name269AA78, originalExtras.ToImmutable(), this.resources.NativeScanNames.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.ToImmutableArray()), effect?.Binding.ScanTextResources, empty, identity)
        {
            BoundEnemyExtra = boundExtras.ToImmutable()
        };
    }


}
