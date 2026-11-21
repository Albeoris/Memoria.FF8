using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;
using Vector2N = System.Numerics.Vector2;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private readonly Dictionary<int, (Ff7NativeWaterEffectSample Sample, IFf7ActorAppearance Appearance, Ff7NativePartVisibilityState? Visibility, Ff7ModelRenderOverrides Effects)> waterAppearanceEffects = [];

    private IReadOnlyDictionary<int, Ff7ModelRenderOverrides> PrepareWaterAppearanceEffects(Ff7Session session, Ff7FieldProcess process)
    {
        Dictionary<int, Ff7ModelRenderOverrides> prepared = [];
        if (appearance is null || !appearance.SupportsVertexColorBindings)
        {
            waterAppearanceEffects.Clear();
            return prepared;
        }
        foreach (Ff7Actor actor in process.Actors.Where(actor => actor.Visible && actor.NativeWaterEffect is { OwnsColors: true, DisplayedSample: not null }))
        {
            Ff7NativeWaterEffectState water = actor.NativeWaterEffect!;
            Ff7NativeWaterEffectSample sample = water.DisplayedSample!;
            if (waterAppearanceEffects.TryGetValue(actor.Slot, out var cached) && ReferenceEquals(cached.Sample, sample) && ReferenceEquals(cached.Appearance, appearance) && ReferenceEquals(cached.Visibility, actor.PartVisibility))
            {
                prepared.Add(actor.Slot, cached.Effects);
                continue;
            }
            Ff7Actor presented = session.PrepareActorPresentation(actor);
            Ff7NativeWaterEffectPose pose = sample.SamplingPose ?? new(actor.Model, actor.Animation, (actor.AnimationFrame16 >> 4) << 4, checked((byte)actor.Direction), presented.X4096 / 4096f, presented.Y4096 / 4096f, presented.Z4096 / 4096f);
            Ff7FieldWaterColorRegion region = sample.ColorRegion ?? (session.Host.NativeWaterGeometryCapture ?? throw new InvalidOperationException("Water appearance restoration requires the selected native geometry owner."))(actor, session.Field.Cameras[0], sample.Parameters).ColorRegion ?? throw new InvalidDataException("Water appearance restoration requires its sampled native color region.");
            Ff7ActorVisual visual = new(actor.Entity, pose.ModelIndex, checked((int)(pose.X * 4096)), checked((int)(pose.Y * 4096)), checked((int)(pose.Z * 4096)), pose.Direction, pose.AnimationIndex, pose.AnimationFrame16)
            {
                PartyCharacter = actor.PartyCharacter
            };
            Ff7ModelRenderOverrides effects = CreateWaterAppearanceEffects(water, appearance, visual, session.Field.Models.Models[pose.ModelIndex], session.Models[pose.ModelIndex], region, actor.PartVisibility);
            waterAppearanceEffects[actor.Slot] = (sample, appearance, actor.PartVisibility, effects);
            prepared.Add(actor.Slot, effects);
        }
        foreach (int slot in waterAppearanceEffects.Keys.Where(slot => !prepared.ContainsKey(slot)).ToArray())
        {
            waterAppearanceEffects.Remove(slot);
        }
        return prepared;
    }

    internal static Ff7ModelRenderOverrides CreateWaterAppearanceEffects(Ff7NativeWaterEffectState water, IFf7ActorAppearance appearance, Ff7ActorVisual pose, Memoria.FF7.Formats.PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, Ff7FieldWaterColorRegion region, Ff7NativePartVisibilityState? visibility = null)
    {
        Ff7ModelRenderOverrides effects = CreateWaterModelEffects(water, visibility);
        if ((pose.AnimationFrame16 & 15) != 0 || pose.AnimationBlend is not null)
        {
            throw new InvalidDataException("Water appearance colors require their captured native pose.");
        }
        if (!appearance.SupportsVertexColorBindings || !appearance.TrySampleVertices(pose, descriptor, original, region.LocalVertexScale, out IReadOnlyList<System.Numerics.Vector3> vertices))
        {
            return effects;
        }
        region.Validate();
        bool[] modified = Ff7FieldRenderer.CaptureAppearanceVertexColorMask(vertices, region);
        Ff7FieldWaterParameters parameters = water.DisplayedSample!.Parameters;
        return effects with
        {
            AppearanceVertexColor = (vertex, color) =>
            {
                if ((uint)vertex >= modified.Length)
                {
                    throw new InvalidDataException("Water appearance colors require the captured target vertex identity.");
                }
                short blue = region.ProfileSix ? Ff7FieldWaterGeometryMath.ProfileSixBlueOffset(parameters.Blue, vertex) : parameters.Blue;
                return modified[vertex] ? Ff7NativeColorOffset.Shade(color, parameters.Red, parameters.Green, blue) : color;
            }
        };
    }

    internal static Ff7ModelRenderOverrides CreateWaterModelEffects(Ff7NativeWaterEffectState water, Ff7NativePartVisibilityState? visibility = null)
    {
        water.Validate();
        visibility?.Validate();
        Ff7NativeWaterEffectSample sample = water is { OwnsColors: true, DisplayedSample: { } displayed } ? displayed : throw new InvalidDataException("Native water colors require their captured material owner.");
        Dictionary<int, Ff7FieldWaterPartMask> parts = sample.Parts.ToDictionary(part => part.Part);
        return new(part => visibility is null || Ff7NativePartVisibility.PartVisible(visibility, part), null, false)
        {
            SourceVertexColor = (part, vertex, original) =>
            {
                if (!parts.TryGetValue(part, out Ff7FieldWaterPartMask? sampled))
                {
                    return original;
                }
                if ((uint)vertex >= sampled.ModifiedVertices.Length)
                {
                    throw new InvalidDataException("Native water colors require their original vertex topology.");
                }
                short blue = sampled.BlueOffsets?[vertex] ?? sample.Parameters.Blue;
                return sampled.ModifiedVertices[vertex] ? Ff7NativeColorOffset.Shade(original, sample.Parameters.Red, sample.Parameters.Green, blue) : original;
            }
        };
    }

    private static Ff7FieldWaterSplashVisual[] WaterSplashes(Ff7FieldProcess process)
    {
        return process.NativeWaterSplashes.OrderBy(splash => splash.TextureIndex).Select(splash => new Ff7FieldWaterSplashVisual(splash.TextureIndex, new Vector2N(splash.X, splash.Y), splash.Depth, splash.HalfSize)).ToArray();
    }
}
