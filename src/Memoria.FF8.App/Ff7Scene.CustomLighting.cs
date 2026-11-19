using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private readonly Dictionary<int, (Ff7NativeCustomLightingSample Sample, Ff7FieldModelResources Model, Ff7NativePartVisibilityState? Visibility, Ff7ModelRenderOverrides Effects)> customLightingEffects = [];

    private Ff7ModelRenderOverrides CustomLightingEffects(Ff7Actor actor, Ff7NativeCustomLightingSample sample)
    {
        Ff7Session session = Session!;
        Ff7NativeCustomLightingPose pose = sample.SamplingPose ?? throw new InvalidDataException("Displayed custom lighting requires its restored sampled native pose.");
        if (pose.ModelIndex != actor.Model || (uint)pose.ModelIndex >= session.Models.Count)
        {
            throw new NotSupportedException("Custom lighting requires the original sampled model's vertex identities.");
        }
        Ff7FieldModelResources model = session.Models[pose.ModelIndex];
        if (customLightingEffects.TryGetValue(actor.Slot, out var cached) && ReferenceEquals(cached.Sample, sample) && ReferenceEquals(cached.Model, model) && ReferenceEquals(cached.Visibility, actor.PartVisibility))
        {
            return cached.Effects;
        }
        Ff7ModelRenderOverrides effects = CreateCustomLightingEffects(sample, session.Field.Models.Models[pose.ModelIndex], model, actor.PartVisibility);
        customLightingEffects[actor.Slot] = (sample, model, actor.PartVisibility, effects);
        return effects;
    }

    internal static Ff7ModelRenderOverrides CreateCustomLightingEffects(Ff7NativeCustomLightingSample sample, PcFieldModelDescriptor descriptor, Ff7FieldModelResources resources, Ff7NativePartVisibilityState? visibility = null)
    {
        sample.Validate();
        visibility?.Validate();
        Ff7NativeCustomLightingPose pose = sample.SamplingPose ?? throw new InvalidDataException("Custom lighting requires its sampled native pose.");
        if ((uint)pose.AnimationIndex >= descriptor.Animations.Count)
        {
            throw new InvalidDataException("Custom lighting references an absent sampled animation.");
        }
        string animationName = Path.GetFileNameWithoutExtension(descriptor.Animations[pose.AnimationIndex]) + ".a";
        Ff7FieldPosePhase phase = new(pose.AnimationFrame16, pose.FirstAnimationFrame, pose.LastAnimationFrame, pose.AnimationLoop);
        IReadOnlyList<Matrix4x4> bones = Ff7FieldPoseSampler.Sample(resources, resources.Animations[animationName], phase, Ff7NativeFieldPoseMath.ModelBasis(descriptor).M11).Bones;
        Matrix4x4 actorRotation = Matrix4x4.CreateRotationX(-MathF.PI / 2) * Matrix4x4.CreateRotationZ(pose.Direction * MathF.Tau / 256);
        Dictionary<(int Part, int Vertex), uint> colors = [];
        for (int partIndex = 0; partIndex < resources.Geometry.Count; partIndex++)
        {
            Ff7BoneGeometry part = resources.Geometry[partIndex];
            Ff7FieldLighting lighting = new(sample.Colors, sample.Directions, bones[part.BoneIndex] * actorRotation);
            PcPolygonModel model = part.Polygon;
            foreach (PcModelGroup group in model.Groups)
            {
                int[] bindings = Ff7FieldLighting.BindNormals(model, group);
                for (int local = 0; local < bindings.Length; local++)
                {
                    if (bindings[local] < 0)
                    {
                        continue;
                    }
                    int vertex = group.VertexStart + local;
                    uint original = vertex < model.VertexColors.Count ? model.VertexColors[vertex] : uint.MaxValue;
                    Vector4 color = lighting.Shade(model.Normals[bindings[local]], original);
                    uint packed = Pack(color);
                    if (colors.TryGetValue((partIndex, vertex), out uint prior) && prior != packed)
                    {
                        throw new NotSupportedException("Custom lighting requires distinct group vertex identities for conflicting native normal bindings.");
                    }
                    colors[(partIndex, vertex)] = packed;
                }
            }
        }
        return new(part => visibility is null || Ff7NativePartVisibility.PartVisible(visibility, part), null, true)
        {
            SourceVertexColor = (part, vertex, original) => colors.GetValueOrDefault((part, vertex), original)
        };

        static uint Pack(Vector4 color)
        {
            uint Channel(float value) => checked((uint)MathF.Round(value * 255, MidpointRounding.ToEven));
            return Channel(color.W) << 24 | Channel(color.X) << 16 | Channel(color.Y) << 8 | Channel(color.Z);
        }
    }
}
