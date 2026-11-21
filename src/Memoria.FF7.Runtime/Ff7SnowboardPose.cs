using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed class Ff7SnowboardPose
{
    private readonly Ff7SnowboardPresentationData data;
    private Vector3[] current;
    private Vector3[] start;
    private Vector3[] target;
    private Vector3 root;
    private Vector3 startRoot;
    private Vector3 targetRoot;
    private int duration;
    private int remaining;
    private byte easing;

    public Ff7SnowboardPose(Ff7SnowboardPresentationData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Joints.Count == 0 || data.Poses.Count == 0 || data.Poses.Any(pose => pose.Rotations.Count != data.Joints.Count))
        {
            throw new InvalidDataException("Snowboard pose data has inconsistent joint or pose allocations.");
        }
        this.data = data;
        current = new Vector3[data.Joints.Count];
        start = new Vector3[data.Joints.Count];
        target = new Vector3[data.Joints.Count];
    }

    public Ff7SnowboardPose(Ff7SnowboardPresentationData data, int initialPose) : this(data)
    {
        Ff7SnowboardPoseFrame initial = ReadPose(initialPose);
        current = initial.Rotations.ToArray();
        start = current.ToArray();
        target = current.ToArray();
        root = startRoot = targetRoot = initial.RootOffset;
    }

    public int Remaining => remaining;

    public static Matrix4x4 BuildActorPlacement(Vector3 positionQ12, float heading, Vector3 visualAngles)
    {
        Matrix4x4 placement = Matrix4x4.CreateRotationY(MathF.PI * 0.5f) * Matrix4x4.CreateRotationY(heading) * Matrix4x4.CreateRotationZ(visualAngles.Z) * Matrix4x4.CreateRotationX(visualAngles.X) * Matrix4x4.CreateRotationY(visualAngles.Y);
        placement.Translation = positionQ12 / 4096f;
        return placement;
    }

    public void Begin(Ff7SnowboardActionFrame frame)
    {
        Ff7SnowboardPoseFrame pose = ReadPose(frame.Model);
        if (frame.Easing > 3)
        {
            throw new NotSupportedException("Unsupported native snowboard pose easing selector.");
        }
        start = current.ToArray();
        target = pose.Rotations.ToArray();
        startRoot = root;
        targetRoot = pose.RootOffset;
        duration = remaining = frame.Duration;
        easing = frame.Easing;
    }

    public void SetReference(int pose)
    {
        start = ReadPose(pose).Rotations.ToArray();
        startRoot = root;
    }

    public void Blend(int first, int second, float fraction)
    {
        if (first < -1 || !float.IsFinite(fraction))
        {
            throw new ArgumentOutOfRangeException(nameof(first));
        }
        Ff7SnowboardPoseFrame destination = ReadPose(second);
        IReadOnlyList<Vector3> source = first == -1 ? start : ReadPose(first).Rotations;
        Vector3 sourceRoot = first == -1 ? startRoot : ReadPose(first).RootOffset;
        for (int joint = 0; joint < current.Length; joint++)
        {
            current[joint] = source[joint] + (destination.Rotations[joint] - source[joint]) * fraction;
        }
        root = sourceRoot + (destination.RootOffset - sourceRoot) * fraction;
    }

    public void Advance()
    {
        if (remaining == 0)
        {
            return;
        }
        remaining--;
        float progress = (duration - remaining) / (float)duration;
        float fraction = Math.Clamp(Ease(progress, easing), 0, 1);
        for (int joint = 0; joint < current.Length; joint++)
        {
            current[joint] = start[joint] + (target[joint] - start[joint]) * fraction;
        }
        root = startRoot + (targetRoot - startRoot) * fraction;
    }

    /// <summary>Placement is the native actor-placement matrix expressed in System.Numerics row-vector convention.</summary>
    public IReadOnlyList<Ff7SnowboardModelInstance> Sample(Matrix4x4 placement)
    {
        Matrix4x4[] rotations = new Matrix4x4[data.Joints.Count];
        Vector3[] endpoints = new Vector3[data.Joints.Count];
        for (int joint = 0; joint < rotations.Length; joint++)
        {
            Ff7SnowboardJointBinding binding = data.Joints[joint];
            Vector3 angle = current[joint];
            Matrix4x4 euler = Matrix4x4.CreateRotationX(angle.X) * Matrix4x4.CreateRotationZ(angle.Z) * Matrix4x4.CreateRotationY(angle.Y);
            rotations[joint] = binding.RotationSuffix * euler * binding.RotationBasis;
            if (binding.RotationParent >= 0 && binding.RotationParent < joint)
            {
                rotations[joint] *= rotations[binding.RotationParent];
            }
            if (!binding.SkipEndpoint)
            {
                endpoints[joint] = Vector3.TransformNormal(new(0, 0, binding.Length * 0.125f), binding.EndpointBasis * rotations[joint]);
            }
        }
        for (int joint = 0; joint < endpoints.Length; joint++)
        {
            int parent = data.Joints[joint].PositionParent;
            if (parent >= 0 && parent < joint)
            {
                endpoints[joint] += endpoints[parent];
            }
        }
        Matrix4x4 basis = placement with { M41 = 0, M42 = 0, M43 = 0 };
        Vector3 origin = Vector3.TransformNormal(root, basis);
        origin.Y -= 1031;
        origin = origin * 0.0625f + placement.Translation;
        Matrix4x4 endpointBasis = Matrix4x4.CreateScale(1, -1, -1) * basis;
        Matrix4x4 halfTurn = Matrix4x4.CreateRotationX(MathF.PI);
        List<Ff7SnowboardModelInstance> instances = [];
        foreach (Ff7SnowboardDrawBinding binding in data.DrawBindings)
        {
            if (binding.ModelIndex < 0)
            {
                continue;
            }
            if ((uint)binding.RotationJoint >= rotations.Length || binding.PositionJoint >= endpoints.Length)
            {
                throw new InvalidDataException("Snowboard draw binding references an absent native joint.");
            }
            Matrix4x4 transform = halfTurn * rotations[binding.RotationJoint] * halfTurn * basis;
            transform.Translation = origin + (binding.PositionJoint < 0 ? Vector3.Zero : Vector3.TransformNormal(endpoints[binding.PositionJoint], endpointBasis));
            instances.Add(new(binding.ModelIndex, transform) { NativePrimitiveKind = binding.NativePrimitiveKind });
        }
        return instances;
    }

    private Ff7SnowboardPoseFrame ReadPose(int index)
    {
        if ((uint)index >= data.Poses.Count)
        {
            throw new InvalidDataException("Snowboard action references an absent native pose.");
        }
        return data.Poses[index];
    }

    internal static float Ease(float progress, byte kind)
    {
        return kind switch
        {
            0 => progress,
            1 => (MathF.Sin((progress - 0.5f) * MathF.PI) + 1) * 0.5f,
            2 => 1 - MathF.Sin((progress + 1) * 0.5f * MathF.PI),
            3 => -MathF.Sin((float)((progress + 2) * (Math.PI * 0.5))),
            _ => throw new NotSupportedException("Unsupported native snowboard pose easing selector.")
        };
    }
}
