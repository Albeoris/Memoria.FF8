using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeBattlePreparedPresentation? PreparedPresentation { get; private set; }
    private long preparedPresentationVersion;

    /// <summary>Called at completed host/command boundaries. This composes presentation data, never a battle save.</summary>
    public void RefreshPreparedPresentation()
    {
        if (NativeGraphics is not { PrimitiveFrame: { } frame } graphics)
        {
            PreparedPresentation = null;
            return;
        }
        NativeBattlePreparedPrimitiveFrame primitives = NativeBattlePreparedPrimitiveFrame.From(frame);
        if (PreparedPresentation?.Primitives == primitives)
        {
            primitives = PreparedPresentation.Primitives;
        }
        ImmutableArray<NativeBattlePreparedActor>.Builder actors = ImmutableArray.CreateBuilder<NativeBattlePreparedActor>();
        for (int index = 0; index < graphics.ActorIds.Length; index++)
        {
            if (graphics.ActorIds[index] is string actorId && graphics.Models[index].BodyTextureSlots is { } body)
            {
                BattleCombatantDefinition actor = GetCombatantDefinition(actorId);
                actors.Add(new(actor.Id, actor.Name, actor.IsPlayer, actor.ModelId, actor.Costume, actor.WeaponId, actor.SceneSlot, GetGuardianFormVisual(actorId), body, graphics.Models[index].LinkedTextureSlots));
            }
        }
        NativeBattlePreparedScan? scan = PrepareScanPresentation(graphics);
        ushort? pilotEffect = graphics.OriginalEffect?.Binding.Effect is ushort effect && effect is 2 or 3 or 144 ? effect : null;
        PreparedPresentation = new(++preparedPresentationVersion, graphics.Callback, Frame, GuardianAnimationSkipPending, primitives, NativeEffectMemory?.Textures, NativeEffectMemory?.PrimitiveProcess?.TextureWindowCache, NativeEffectMemory?.UiImmediateFrame, NativeCamera?.CurrentPose, graphics.Effects.TextDraws, NativeStage?.ResourceStageId, NativeStage?.TextureAnimationStartCallback, actors.ToImmutable(), scan) { PilotEffect = pilotEffect, PilotGeneration = pilotEffect.HasValue && graphics.OriginalEffect?.Pools is { Length: > 0 } pilotPools ? NativeBattlePilotVisualLifetimes.EpochFor(pilotPools[0].RequireOwner()) : 0, PilotCamera = nativePilotSourceCamera, PilotProjection = nativePilotSourceProjection };
    }

    private NativeBattlePreparedScan? PrepareScanPresentation(NativeBattleGraphicsState graphics)
    {
        if (NativeEffectMemory?.Scan is not { Text.Initialized: true } scan || graphics.UiServices is not { } services)
        {
            return null;
        }
        ImmutableDictionary<int, NativeBattlePreparedScanExtra>.Builder extras = ImmutableDictionary.CreateBuilder<int, NativeBattlePreparedScanExtra>();
        foreach (BattleCombatant actor in combatants.Where(actor => actor.NativeIndex is >= 3 and < 7))
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
            extras.Add(actor.NativeIndex, new(failure == NativeBattlePreparedScanExtraFailure.None ? row : null, failure));
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
        BattleCombatant? selected = combatants.FirstOrDefault(actor => actor.Id == actorId);
        NativeBattlePreparedScanActor? identity = selected is null ? null : new(selected.ModelId, selected.Name, selected.IsPlayer, selected.HasNativeDisplayNameOverride);
        return new(scan.Text.ReadFrame(services.Flags942), scan.Results, scan.Presentation.Name269AA78, extras.ToImmutable(), definition.NativeScanNames.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.ToImmutableArray()), effect?.Binding.ScanTextResources, empty, identity);
    }
}
