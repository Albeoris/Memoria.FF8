using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

/// <summary>Interpolates presentation samples without advancing simulation or editing native state.</summary>
public static class WorldRenderInterpolation
{
    public static WorldRenderFrame Interpolate(WorldRenderFrame previous, WorldRenderFrame current, float alpha)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        if (!float.IsFinite(alpha) || alpha < 0 || alpha > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(alpha));
        }
        if (alpha == 1)
        {
            return current;
        }
        bool sameAvatar = previous.AvatarVisible == current.AvatarVisible && ReferenceEquals(previous.AvatarModel, current.AvatarModel);
        bool sameAnimation = sameAvatar && previous.AnimationId == current.AnimationId;
        Vector3 position = Position(previous.Position, current.Position, alpha);
        return current with
        {
            Position = position,
            Heading = Angle(previous.Heading, current.Heading, alpha),
            AnimationFrame = sameAnimation && alpha == 0 ? previous.AnimationFrame : current.AnimationFrame,
            AvatarAnimationBlend = sameAnimation && alpha > 0 ? new WorldAnimationBlend((int)previous.AnimationFrame, (int)current.AnimationFrame, alpha) : null,
            Camera = Camera(previous, current, position, alpha),
            Sky = Sky(previous.Sky, current.Sky, alpha),
            StaticModels = StaticModels(previous.StaticModels, current.StaticModels, position, alpha),
            AnimatedModels = AnimatedModels(previous.AnimatedModels, current.AnimatedModels, position, alpha),
            ForestParticles = Forest(previous.Forest, current.Forest, position, alpha)
        };
    }

    private static float Lerp(float previous, float current, float alpha) => previous + (current - previous) * alpha;

    private static float Angle(float previous, float current, float alpha) => previous + MathF.IEEERemainder(current - previous, MathF.Tau) * alpha;

    private static Vector3 Position(Vector3 previous, Vector3 current, float alpha)
    {
        if (alpha == 0)
        {
            return previous;
        }
        return new Vector3(WorldAssets.Wrap(previous.X + WorldAssets.Wrap(current.X - previous.X, 262144) * alpha, 262144), Lerp(previous.Y, current.Y, alpha), WorldAssets.Wrap(previous.Z + WorldAssets.Wrap(current.Z - previous.Z, 196608) * alpha, 196608));
    }

    private static Vector3 Near(Vector3 value, Vector3 anchor) => new(anchor.X + WorldAssets.Wrap(value.X - anchor.X, 262144), value.Y, anchor.Z + WorldAssets.Wrap(value.Z - anchor.Z, 196608));

    private static Matrix4x4 Rotation(Matrix4x4 previous, Matrix4x4 current, float alpha)
    {
        return Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(previous)), Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(current)), alpha));
    }

    private static WorldCamera Camera(WorldRenderFrame previous, WorldRenderFrame current, Vector3 position, float alpha)
    {
        WorldCamera before = previous.Camera;
        WorldCamera after = current.Camera;
        if (before.NativeProjection != after.NativeProjection)
        {
            return after;
        }
        if (alpha == 0)
        {
            return before;
        }
        WorldCamera result = after with
        {
            Heading = Angle(before.Heading, after.Heading, alpha),
            Distance = Lerp(before.Distance, after.Distance, alpha),
            Height = Lerp(before.Height, after.Height, alpha),
            TargetHeight = Lerp(before.TargetHeight, after.TargetHeight, alpha),
            FieldOfView = Lerp(before.FieldOfView, after.FieldOfView, alpha),
            FarDistance = Lerp(before.FarDistance, after.FarDistance, alpha),
            NativeOffsetZ = before.NativeProjection && after.NativeProjection ? (int)MathF.Round(Lerp(before.NativeOffsetZ ?? WorldSkyProjection.FootOffsetZ, after.NativeOffsetZ ?? WorldSkyProjection.FootOffsetZ, alpha)) : after.NativeOffsetZ,
            TargetPosition = before.TargetPosition is Vector3 previousTarget && after.TargetPosition is Vector3 currentTarget ? Near(Position(previousTarget, currentTarget, alpha), position) : after.TargetPosition
        };
        if (!after.NativeProjection)
        {
            return result;
        }
        WorldCameraFrame first = before.ScriptedFrame ?? before.FootFrame(previous.Position);
        WorldCameraFrame second = after.ScriptedFrame ?? after.FootFrame(current.Position);
        Matrix4x4 basis = Rotation(Basis(first), Basis(second), alpha);
        WorldCameraFrame frame = new(Near(Position(first.Eye, second.Eye, alpha), position), new Vector3(basis.M31, basis.M32, basis.M33), new Vector3(basis.M21, basis.M22, basis.M23));
        return result with { ScriptedFrame = frame };
    }

    private static Matrix4x4 Basis(WorldCameraFrame frame)
    {
        Vector3 forward = Vector3.Normalize(frame.Forward);
        Vector3 right = Vector3.Normalize(Vector3.Cross(frame.Up, forward));
        Vector3 up = Vector3.Cross(forward, right);
        return new Matrix4x4(right.X, right.Y, right.Z, 0, up.X, up.Y, up.Z, 0, forward.X, forward.Y, forward.Z, 0, 0, 0, 0, 1);
    }

    private static WorldSky Sky(WorldSky previous, WorldSky current, float alpha)
    {
        byte Channel(byte first, byte second) => (byte)Math.Clamp(MathF.Round(Lerp(first, second, alpha)), 0, 255);
        Rgba32 Color(Rgba32 first, Rgba32 second) => new(Channel(first.R, second.R), Channel(first.G, second.G), Channel(first.B, second.B), Channel(first.A, second.A));
        return current with { Position = Position(previous.Position, current.Position, alpha), Top = Color(previous.Top, current.Top), Center = Color(previous.Center, current.Center), Bottom = Color(previous.Bottom, current.Bottom), Radius = (int)MathF.Round(Lerp(previous.Radius, current.Radius, alpha)) };
    }

    private static IReadOnlyList<NativeWorldStaticModelDraw>? StaticModels(IReadOnlyList<NativeWorldStaticModelDraw>? previous, IReadOnlyList<NativeWorldStaticModelDraw>? current, Vector3 position, float alpha)
    {
        if (current is null)
        {
            return null;
        }
        NativeWorldStaticModelDraw[] result = current.ToArray();
        for (int index = 0; index < result.Length; index++)
        {
            NativeWorldStaticModelDraw draw = result[index];
            NativeWorldStaticModelDraw[] matches = previous?.Where(item => draw.ObjectId.HasValue && item.ObjectId == draw.ObjectId && ReferenceEquals(item.Model, draw.Model)).ToArray() ?? [];
            if (matches.Length != 1)
            {
                if (alpha > 0)
                {
                    result[index] = draw with { Pose = draw.Pose with { Anchor = Near(draw.Pose.Anchor, position) } };
                }
                continue;
            }
            NativeWorldStaticModelPose before = matches[0].Pose;
            if (alpha == 0)
            {
                result[index] = draw with { Pose = before };
                continue;
            }
            Matrix4x4 first = before.Rotation ?? WorldStaticModelTransform.CreateRotation(draw.Model.ShapeId, before.Angles);
            Matrix4x4 second = draw.Pose.Rotation ?? WorldStaticModelTransform.CreateRotation(draw.Model.ShapeId, draw.Pose.Angles);
            result[index] = draw with { Pose = draw.Pose with { Anchor = Near(Position(before.Anchor, draw.Pose.Anchor, alpha), position), Rotation = Rotation(first, second, alpha) } };
        }
        return Array.AsReadOnly(result);
    }

    private static IReadOnlyList<NativeWorldAnimatedModelDraw>? AnimatedModels(IReadOnlyList<NativeWorldAnimatedModelDraw>? previous, IReadOnlyList<NativeWorldAnimatedModelDraw>? current, Vector3 position, float alpha)
    {
        if (current is null)
        {
            return null;
        }
        NativeWorldAnimatedModelDraw[] result = current.ToArray();
        for (int index = 0; index < result.Length; index++)
        {
            NativeWorldAnimatedModelDraw draw = result[index];
            NativeWorldAnimatedModelDraw[] matches = previous?.Where(item => draw.ObjectId.HasValue && item.ObjectId == draw.ObjectId && ReferenceEquals(item.Model, draw.Model)).ToArray() ?? [];
            if (matches.Length != 1)
            {
                if (alpha > 0)
                {
                    result[index] = draw with { Position = Near(draw.Position, position) };
                }
                continue;
            }
            NativeWorldAnimatedModelDraw before = matches[0];
            bool sameAnimation = before.AnimationId == draw.AnimationId;
            result[index] = sameAnimation && alpha == 0 ? before : draw with
            {
                Position = alpha == 0 ? before.Position : Near(Position(before.Position, draw.Position, alpha), position),
                Heading = Angle(before.Heading, draw.Heading, alpha),
                AnimationBlend = sameAnimation && alpha > 0 ? new WorldAnimationBlend((int)before.AnimationFrame, (int)draw.AnimationFrame, alpha) : null
            };
        }
        return Array.AsReadOnly(result);
    }

    private static IReadOnlyList<WorldForestParticleFrame>? Forest(NativeWorldForestState? previous, NativeWorldForestState? current, Vector3 position, float alpha)
    {
        if (current is null)
        {
            return null;
        }
        List<WorldForestParticleFrame> particles = [];
        foreach (NativeWorldForestParticle particle in current.Particles.Where(item => item.Visible))
        {
            NativeWorldForestParticle? before = previous?.Particles.SingleOrDefault(item => item.Slot == particle.Slot && item.Type == particle.Type && item.Visible && item.Age + 1 == particle.Age && item.Lifetime == particle.Lifetime);
            Vector3 anchor = new(particle.X, particle.NativeY, -particle.GraphicsZ);
            Matrix4x4 rotation = ForestRotation(particle);
            float scale = particle.Scale;
            NativeWorldForestParticle selected = particle;
            if (before is not null)
            {
                anchor = Position(new Vector3(before.X, before.NativeY, -before.GraphicsZ), anchor, alpha);
                rotation = alpha == 0 ? ForestRotation(before) : Rotation(ForestRotation(before), rotation, alpha);
                scale = Lerp(before.Scale, particle.Scale, alpha);
                if (alpha == 0)
                {
                    selected = before;
                }
            }
            particles.Add(new WorldForestParticleFrame(selected, Near(anchor, position), rotation, scale, before is not null && alpha > 0) { PreviousScale = before?.Scale ?? particle.Scale, Blend = alpha });
        }
        return particles.AsReadOnly();
    }

    private static Matrix4x4 ForestRotation(NativeWorldForestParticle particle)
    {
        const float radiansPerUnit = MathF.Tau / 4096;
        return Matrix4x4.CreateRotationZ(particle.Roll * radiansPerUnit) * Matrix4x4.CreateRotationY(particle.Yaw * radiansPerUnit) * Matrix4x4.CreateRotationX(particle.RotationX * radiansPerUnit);
    }
}
