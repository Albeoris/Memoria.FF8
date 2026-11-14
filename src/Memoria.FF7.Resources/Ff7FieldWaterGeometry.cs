using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7FieldWaterParameters(short Level, short Red, short Green, short Blue, byte Flags, int EnvironmentProfile);
public sealed record Ff7FieldWaterPartMask(int Part, bool[] ModifiedVertices);
public sealed record Ff7FieldWaterSplashCandidate(int BoneIndex, float X, float Y, float Depth, float HalfSize);
public sealed record Ff7FieldWaterGeometry(IReadOnlyList<Ff7FieldWaterPartMask> Parts, IReadOnlyList<Ff7FieldWaterSplashCandidate> Splashes)
{
    public Ff7FieldWaterColorRegion? ColorRegion { get; init; }
}

public sealed record Ff7FieldWaterColorRegion(bool HasThreshold, double Height, bool UseY, bool All = false)
{
    public void Validate()
    {
        if (!double.IsFinite(Height) || All && !HasThreshold)
        {
            throw new InvalidDataException("Native water color regions require a finite captured threshold or an explicit ordered-comparison result.");
        }
    }

    public bool Contains(Vector3 position)
    {
        return HasThreshold && (All || (UseY ? position.Y : position.Z) > Height);
    }
}

public static class Ff7FieldWaterGeometryMath
{
    public static Ff7FieldWaterGeometry Capture(Ff7FieldModelResources model, PcFieldModelDescriptor descriptor, PcFieldAnimation animation, int frame, Vector3 position, byte direction, PcFieldCamera camera, Vector2 cameraOffset, float nearPlane, float farPlane, Ff7FieldWaterParameters parameters, bool emitSplashes)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z) || !float.IsFinite(cameraOffset.X) || !float.IsFinite(cameraOffset.Y))
        {
            throw new InvalidDataException("Native water sampling requires finite model and camera coordinates.");
        }
        if (parameters.EnvironmentProfile is not (0 or 3 or 4))
        {
            throw new NotSupportedException("This native water environment requires its distinct color and splash geometry contract.");
        }
        if (parameters.EnvironmentProfile == 0)
        {
            return new(model.Geometry.Select((part, index) => new Ff7FieldWaterPartMask(index, new bool[part.Polygon.Vertices.Count])).ToArray(), []) { ColorRegion = new(false, 0, (parameters.Flags & 4) != 0) };
        }
        if (model.KnownPoseBinding is not null)
        {
            throw new NotSupportedException("Native water sampling requires the original model's shared bound pose channels.");
        }
        Matrix4x4 basis = Ff7NativeFieldPoseMath.ModelBasis(descriptor);
        Ff7NativeFieldPose pose = Ff7NativeFieldPoseMath.Sample(model, animation, frame, basis.M11);
        Matrix4x4 actor = basis * Matrix4x4.CreateRotationZ(direction * MathF.Tau / 256) * Matrix4x4.CreateTranslation(position);
        List<Ff7FieldWaterPartMask> masks = [];
        double? colorPlane = null;
        for (int partIndex = 0; partIndex < model.Geometry.Count; partIndex++)
        {
            Ff7BoneGeometry part = model.Geometry[partIndex];
            Matrix4x4 transform = pose.Bones[part.BoneIndex] * actor;
            bool[] modified = new bool[part.Polygon.Vertices.Count];
            if (parameters.EnvironmentProfile != 0)
            {
                foreach (PcModelGroup group in part.Polygon.Groups)
                {
                    for (int local = 0; local < group.VertexCount; local++)
                    {
                        int vertex = group.VertexStart + local;
                        Vector3 world = Vector3.Transform(part.Polygon.Vertices[vertex], transform);
                        if (!float.IsFinite(world.X) || !float.IsFinite(world.Y) || !float.IsFinite(world.Z))
                        {
                            throw new InvalidDataException("Native water colors require finite original vertices and pose transforms.");
                        }
                        float height = (parameters.Flags & 4) != 0 ? world.Y : world.Z;
                        colorPlane ??= PlaneHeight(height, world.Y, parameters);
                        modified[vertex] = height > colorPlane.Value;
                    }
                }
            }
            masks.Add(new(partIndex, modified));
        }
        List<Ff7FieldWaterSplashCandidate> splashes = [];
        if (!emitSplashes || parameters.EnvironmentProfile == 0)
        {
            return new(masks.AsReadOnly(), splashes.AsReadOnly()) { ColorRegion = Region(colorPlane, parameters.Flags) };
        }
        double? splashPlane = null;
        HashSet<int> drawableBones = model.Geometry.Select(part => part.BoneIndex).ToHashSet();
        foreach (HrcBone bone in model.Hierarchy.Bones)
        {
            if (!drawableBones.Contains(bone.Index))
            {
                continue;
            }
            Matrix4x4 transform = pose.Bones[bone.Index] * actor;
            Vector3 start = Vector3.Transform(Vector3.Zero, transform);
            Vector3 end = Vector3.Transform(new Vector3(0, 0, -(bone.Length + 1 / basis.M11)), transform);
            splashPlane ??= PlaneHeight(start.Z, start.Y, parameters);
            double plane = splashPlane.Value;
            if (!(start.Z > plane) || end.Z == start.Z)
            {
                continue;
            }
            float fraction = (float)((plane - start.Z) / (end.Z - start.Z));
            if (!(fraction >= 0 && fraction <= 1))
            {
                continue;
            }
            Vector3 first = Project(start);
            Vector3 second = Project(end);
            Vector3 projected = first + (second - first) * fraction;
            if (!float.IsFinite(projected.X) || !float.IsFinite(projected.Y) || !float.IsFinite(projected.Z))
            {
                throw new InvalidDataException("Native water splashes require a finite original camera projection.");
            }
            short x = unchecked((short)(int)projected.X);
            short y = unchecked((short)(int)(projected.Y - 8));
            splashes.Add(new(bone.Index, x, y + 8, 1 - projected.Z, 12));
        }
        return new(masks.AsReadOnly(), splashes.AsReadOnly()) { ColorRegion = Region(colorPlane, parameters.Flags) };

        Vector3 Project(Vector3 world)
        {
            Vector3 projected = PcFieldProjection.Project(world, camera, new(160 + cameraOffset.X, 120 + cameraOffset.Y));
            return new(projected.X, projected.Y, Ff7FieldProjectionMath.ProjectDepth(projected.Z, nearPlane, farPlane));
        }
    }

    private static Ff7FieldWaterColorRegion Region(double? plane, byte flags)
    {
        bool useY = (flags & 4) != 0;
        if (plane is null || double.IsNaN(plane.Value) || double.IsPositiveInfinity(plane.Value))
        {
            return new(false, 0, useY);
        }
        return double.IsNegativeInfinity(plane.Value) ? new(true, 0, useY, All: true) : new(true, plane.Value, useY);
    }

    private static double PlaneHeight(float height, float firstY, Ff7FieldWaterParameters parameters)
    {
        if (parameters.EnvironmentProfile == 4)
        {
            float scalar = height <= -229 ? 33 : height < 0 ? (float)(((double)height + 229) * -1.121f + 33) : (float)((double)height * -1.11f);
            float factor = (float)(200d + scalar);
            if (firstY > -1350)
            {
                return double.NaN;
            }
            if (parameters.Level != 1)
            {
                factor = 1000;
            }
            return -(double)parameters.Level * factor;
        }
        float profileThreeScalar = height > -165 ? 70 : (float)(((double)height + 286) * 0.84f - 60);
        float profileThreeFactor = (float)((double)profileThreeScalar * 0.11f - 32);
        profileThreeFactor = (float)(7d * profileThreeFactor / Math.Abs((int)parameters.Level));
        return -(double)parameters.Level * profileThreeFactor;
    }
}

public static class Ff7FieldProjectionMath
{
    public static float ProjectDepth(float cameraZ, float nearPlane, float farPlane)
    {
        if (!float.IsFinite(nearPlane) || !float.IsFinite(farPlane) || nearPlane <= 0 || farPlane <= nearPlane)
        {
            throw new InvalidDataException("FF7 rendering requires a finite admitted near/far projection contract.");
        }
        return farPlane / (farPlane - nearPlane) - farPlane * nearPlane / ((farPlane - nearPlane) * cameraZ);
    }
}
