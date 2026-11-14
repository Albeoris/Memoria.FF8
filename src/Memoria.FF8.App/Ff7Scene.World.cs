using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7WorldRuntime? worldPresentationOwner;
    private Ff7WorldPresentation? worldPresentation;
    private readonly CommittedPresentationHistory<(Ff7WorldRuntime Owner, long Tick, long RestoreEpoch, long CameraEpoch), Ff7WorldRenderFrame> worldHistory = new(
        (first, second) => first == second,
        (first, second) => ReferenceEquals(first.Owner, second.Owner) && first.RestoreEpoch == second.RestoreEpoch && first.CameraEpoch == second.CameraEpoch && second.Tick == first.Tick + 1,
        Ff7WorldRenderInterpolation.Continuous);

    private static (Ff7WorldRuntime Owner, long Tick, long RestoreEpoch, long CameraEpoch) WorldHistoryState(Ff7WorldRuntime world)
    {
        return (world, world.PresentationTick, world.PresentationRestoreEpoch, world.PresentationCameraEpoch);
    }

    private void CommitWorldPresentation(Ff7WorldRuntime world)
    {
        worldHistory.Commit(WorldHistoryState(world), () => WorldRenderFrame(world.Process, world.PresentationArea));
    }

    private void ResetWorldPresentation()
    {
        worldPresentationOwner = null;
        worldPresentation = null;
        worldHistory.Reset();
    }

    internal static Ff7WorldRenderFrame WorldRenderFrame(Ff7WorldProcess state, int? resourceArea = null)
    {
        Ff7WorldCameraState camera = state.Camera ?? new();
        Ff7WorldEffectsState? effects = state.Effects;
        Ff7WorldEffectParticle[] particles = effects?.DisplayParticles ?? effects?.Particles ?? [];
        byte[]? widths = effects?.DisplayWidths ?? effects?.Widths;
        byte[]? heights = effects?.DisplayHeights ?? effects?.Heights;
        HashSet<int> detached = state.Vm.Models.Where(model => model.Detached).Select(model => model.Model).ToHashSet();
        Ff7WorldEntity? controlled = state.Entities.SingleOrDefault(entity => entity.Model == state.ControlledModel);
        Ff7WorldMarsh marsh = state.Marsh;
        Ff7WorldMarshSegment[] marshSegments = marsh.Initialized && marsh.Points is { Length: 48 } && controlled is { X: >= 0x30000 and < 0x40000, Z: >= 0x1C000 and < 0x2C000 }
            ? Enumerable.Range(0, 12).Select(index =>
            {
                Ff7WorldMarshPoint point = marsh.Points[(marsh.Head + 3 + index * 4) % 48];
                return new Ff7WorldMarshSegment(index, new(point.X + 0x34000, unchecked((short)point.Height), point.Z + 0x20000), point.Direction & 4095);
            }).ToArray()
            : [];
        return new(state.ControlledModel, state.Initialized, state.MainState, state.ViewMode, state.RadarMode, state.WorldMapState, state.PresentationCounter, state.Sky?.CloudPhase ?? 0,
            new(camera.Heading, camera.Pitch, camera.Distance, camera.GroundHeight, camera.Roll, camera.Focal),
            state.Entities.Where(entity => !detached.Contains(entity.Model) && entity.Model != state.Chocobo?.RiderModel).Select(entity => new Ff7WorldVisual(entity.Model, new(entity.X, entity.Y, entity.Z), (entity.Direction + entity.TurnOffset) & 4095, entity.PoseAnimation, entity.PoseFrame, entity.Flags)
            {
                GroundHeight = entity.GroundHeight,
                Terrain = entity.Terrain,
                NativeAnimation = entity.Animation,
                Elevation = entity.Elevation,
                PresentationEpoch = entity.PresentationEpoch,
                PresentationAnimationEpoch = entity.PresentationAnimationEpoch,
                TiltX = entity.TiltX,
                TiltY = entity.TiltY,
                TiltZ = entity.TiltZ
            }).ToArray(),
            state.Lights.Select(light => new Memoria.FF7.Resources.Ff7WorldSkyZone(light.Index, light.X, light.Z, light.Enabled, light.Radius, light.Radius2, new(light.Red, light.Green, light.Blue), new(light.Red2, light.Green2, light.Blue2))).ToArray())
        {
            BridgeDeformation = state.Input?.Bridge?.Deformation,
            ResourceArea = resourceArea,
            ChocoboTint = state.ChocoboTint,
            MeteorVisible = state.MeteorVisible,
            MarshSegments = marshSegments,
            SnowLayers = (state.Snow?.Layers ?? []).Where(layer => layer.Displayed).Select(layer => new Ff7WorldSnowLayer(layer.Layer, layer.DisplayedBrightness, layer.DisplayedU, layer.DisplayedV)).ToArray(),
            HighwindPropellerFrame = state.Vehicle?.HighwindPropellerPoseFrame ?? state.Vehicle?.HighwindPropellerFrame ?? 0,
            Effects = particles.Select(particle => new Ff7WorldEffectVisual(particle.Id, new(particle.X, particle.Y, particle.Z), particle.Sprite, particle.Angle, particle.GroundAligned, widths![particle.Sprite], heights![particle.Sprite], new((particle.Color >> 16 & 255) / 255f, (particle.Color >> 8 & 255) / 255f, (particle.Color & 255) / 255f, (particle.Color >> 24) / 255f))).ToArray(),
            CurvatureThreshold = 5000 - ((state.Vehicle?.FlightProgress ?? 0) * 2500 >> 8),
            HeightSmoothVelocity = state.Input?.HeightSmoothVelocity ?? 0,
            ApproachBubble = state.ApproachBubble is { } bubble ? new(new(bubble.X, bubble.Y, bubble.Z), bubble.Age) : null,
            SubmarineTint = state.SubmarineTint,
            ShakeX = state.ScreenShake?.X ?? 0,
            ShakeY = state.ScreenShake?.Y ?? 0
        };
    }

    private void RenderWorld(Ff7WorldRuntime world, Rectangle bounds)
    {
        if (!ReferenceEquals(worldPresentationOwner, world))
        {
            worldPresentation = new(installation, world.Resources, appearance);
            worldPresentationOwner = world;
        }
        float fraction = PresentationFraction(PresentationPreferences.WorldFps);
        var (previous, current) = worldHistory.Sample(WorldHistoryState(world), fraction, () => WorldRenderFrame(world.Process, world.PresentationArea));
        Ff7WorldRenderFrame frame = previous is null ? current : Ff7WorldRenderInterpolation.Interpolate(previous, current, fraction);
        worldPresentation!.Render(triangles, bounds.Width, bounds.Height, frame);
    }
}

internal static class Ff7WorldRenderInterpolation
{
    public static bool Continuous(Ff7WorldRenderFrame previous, Ff7WorldRenderFrame current)
    {
        return previous.Initialized && current.Initialized && previous.ControlledModel == current.ControlledModel && previous.ViewMode == current.ViewMode && previous.WorldMapState == current.WorldMapState && previous.ResourceArea == current.ResourceArea && previous.Models.SingleOrDefault(model => model.Model == previous.ControlledModel) is { } before && current.Models.SingleOrDefault(model => model.Model == current.ControlledModel) is { } after && Compatible(before, after);
    }

    public static Ff7WorldRenderFrame Interpolate(Ff7WorldRenderFrame previous, Ff7WorldRenderFrame current, float fraction)
    {
        if (!float.IsFinite(fraction))
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction == 1)
        {
            return current;
        }
        Dictionary<int, Ff7WorldVisual> oldModels = previous.Models.ToDictionary(model => model.Model);
        Ff7WorldVisual[] models = current.Models.Select(model => oldModels.TryGetValue(model.Model, out Ff7WorldVisual? old) && Compatible(old, model) ? InterpolateModel(old, model, fraction) : model).ToArray();
        Ff7WorldCameraView first = previous.Camera;
        Ff7WorldCameraView second = current.Camera;
        Ff7WorldCameraView camera = new(
            InterpolateAngle(first.Heading, second.Heading, fraction),
            Lerp(first.Pitch, second.Pitch, fraction),
            Lerp(first.Distance, second.Distance, fraction),
            Lerp(first.GroundHeight, second.GroundHeight, fraction),
            InterpolateAngle(first.Roll, second.Roll, fraction),
            Lerp(first.Focal, second.Focal, fraction));
        Dictionary<long, Ff7WorldEffectVisual> oldEffects = previous.Effects.ToDictionary(effect => effect.Id);
        Ff7WorldEffectVisual[] effects = current.Effects.Select(effect => oldEffects.TryGetValue(effect.Id, out Ff7WorldEffectVisual? old) && old.GroundAligned == effect.GroundAligned ? effect with { Position = System.Numerics.Vector3.Lerp(old.Position, effect.Position, fraction), Angle = InterpolateAngle(old.Angle, effect.Angle, fraction), Color = System.Numerics.Vector4.Lerp(old.Color, effect.Color, fraction), Width = old.Sprite == effect.Sprite ? old.Width + (effect.Width - old.Width) * fraction : effect.Width, Height = old.Sprite == effect.Sprite ? old.Height + (effect.Height - old.Height) * fraction : effect.Height } : effect).ToArray();
        Dictionary<int, Ff7WorldMarshSegment> oldSegments = previous.MarshSegments.ToDictionary(segment => segment.Index);
        Ff7WorldMarshSegment[] segments = current.MarshSegments.Select(segment => oldSegments.TryGetValue(segment.Index, out Ff7WorldMarshSegment? old) ? segment with { Position = System.Numerics.Vector3.Lerp(old.Position, segment.Position, fraction), Direction = InterpolateTilt(old.Direction, segment.Direction, fraction) } : segment).ToArray();
        Dictionary<int, Ff7WorldSnowLayer> oldLayers = previous.SnowLayers.ToDictionary(layer => layer.Layer);
        Ff7WorldSnowLayer[] layers = current.SnowLayers.Select(layer => oldLayers.TryGetValue(layer.Layer, out Ff7WorldSnowLayer? old) ? layer with { Brightness = old.Brightness + (layer.Brightness - old.Brightness) * fraction, U = InterpolateWrapping(old.U, layer.U, 256, fraction), V = InterpolateWrapping(old.V, layer.V, 256, fraction) } : layer).ToArray();
        Memoria.FF7.Resources.Ff7WorldBridgeDeformation? bridge = current.BridgeDeformation is { } after && previous.BridgeDeformation is { } before && before.Index == after.Index ? after with { Tilt = Lerp(before.Tilt, after.Tilt, fraction) } : current.BridgeDeformation;
        return current with { Camera = camera, Models = models, Effects = effects, MarshSegments = segments, SnowLayers = layers, BridgeDeformation = bridge, CloudPhase = InterpolatePeriodic(previous.CloudPhase, current.CloudPhase, 256, fraction), ShakeX = Lerp(previous.ShakeX, current.ShakeX, fraction), ShakeY = Lerp(previous.ShakeY, current.ShakeY, fraction), CurvatureThreshold = Lerp(previous.CurvatureThreshold, current.CurvatureThreshold, fraction), HighwindPropellerBlend = new(previous.HighwindPropellerFrame, current.HighwindPropellerFrame, fraction) };
    }

    private static bool Compatible(Ff7WorldVisual previous, Ff7WorldVisual current)
    {
        const int VisibilityFlags = 2 | 8;
        return previous.PresentationEpoch == current.PresentationEpoch && (previous.Flags & VisibilityFlags) == (current.Flags & VisibilityFlags);
    }

    private static Ff7WorldVisual InterpolateModel(Ff7WorldVisual previous, Ff7WorldVisual current, float fraction)
    {
        return current with
        {
            Position = new System.Numerics.Vector3(
                Ff7WorldRuntime.InterpolateWorldCoordinate((int)previous.Position.X, (int)current.Position.X, 0x48000, fraction),
                previous.Position.Y + (current.Position.Y - previous.Position.Y) * fraction,
                Ff7WorldRuntime.InterpolateWorldCoordinate((int)previous.Position.Z, (int)current.Position.Z, 0x38000, fraction)),
            Direction = InterpolateAngle(previous.Direction, current.Direction, fraction),
            TiltX = current.Model == 6 ? InterpolateTilt(previous.TiltX, current.TiltX, fraction) : previous.TiltX + (current.TiltX - previous.TiltX) * fraction,
            TiltY = InterpolateTilt(previous.TiltY, current.TiltY, fraction),
            TiltZ = InterpolateTilt(previous.TiltZ, current.TiltZ, fraction),
            Elevation = Lerp(previous.Elevation, current.Elevation, fraction),
            GroundHeight = previous.GroundHeight is int before && current.GroundHeight is int after ? Lerp(before, after, fraction) : current.GroundHeight,
            AnimationBlend = previous.Animation == current.Animation && previous.PresentationAnimationEpoch == current.PresentationAnimationEpoch ? new(previous.Frame, current.Frame, fraction) : null
        };
    }

    private static int Lerp(int previous, int current, float fraction)
    {
        return (int)MathF.Round(previous + (current - previous) * fraction);
    }

    private static float InterpolateTilt(float previous, float current, float fraction)
    {
        return InterpolateWrapping(previous, current, 4096, fraction);
    }

    private static float InterpolateWrapping(float previous, float current, float period, float fraction)
    {
        float difference = (current - previous) % period;
        if (difference > period / 2)
        {
            difference -= period;
        }
        else if (difference < -period / 2)
        {
            difference += period;
        }
        return previous + difference * fraction;
    }

    private static int InterpolateAngle(int previous, int current, float fraction)
    {
        int difference = current - previous;
        if (difference > 2048)
        {
            difference -= 4096;
        }
        else if (difference < -2048)
        {
            difference += 4096;
        }
        return Lerp(previous, previous + difference, fraction) & 4095;
    }

    private static int InterpolatePeriodic(int previous, int current, int period, float fraction)
    {
        int difference = (current - previous) % period;
        if (difference > period / 2)
        {
            difference -= period;
        }
        else if (difference < -period / 2)
        {
            difference += period;
        }
        int value = Lerp(previous, previous + difference, fraction) % period;
        return value < 0 ? value + period : value;
    }
}
