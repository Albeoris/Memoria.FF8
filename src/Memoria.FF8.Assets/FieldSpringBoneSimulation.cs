using System.Numerics;

namespace Memoria.FF8.Assets;

/// <summary>A simulated joint with a bone-local tail offset, spring stiffness in inverse seconds squared, damping in inverse seconds, and world-space acceleration.</summary>
public readonly record struct FieldSpringBoneJoint(int Bone, Vector3 LocalTail, float Stiffness, float Damping, Vector3 WorldGravity);

/// <summary>Presentation state for one actor's spring joints. The caller supplies the animation samples and simulation clock.</summary>
public sealed class FieldSpringBoneSimulation
{
    private readonly int[] parents;
    private readonly int[] evaluationOrder;
    private readonly FieldSpringBoneJoint[] joints;
    private readonly int[] jointByBone;
    private readonly TailState[] tails;

    public FieldSpringBoneSimulation(IReadOnlyList<int> parents, IReadOnlyList<FieldSpringBoneJoint> joints)
    {
        ArgumentNullException.ThrowIfNull(parents);
        ArgumentNullException.ThrowIfNull(joints);
        this.parents = parents.ToArray();
        this.joints = joints.ToArray();
        evaluationOrder = OrderBones(this.parents);
        jointByBone = new int[parents.Count];
        Array.Fill(jointByBone, -1);
        tails = new TailState[joints.Count];
        for (int i = 0; i < this.joints.Length; i++)
        {
            FieldSpringBoneJoint joint = this.joints[i];
            if ((uint)joint.Bone >= parents.Count || jointByBone[joint.Bone] >= 0)
            {
                throw new ArgumentException($"Spring joint {joint.Bone} must identify a unique bone in the rig.", nameof(joints));
            }
            if (!TryDirection(joint.LocalTail, out _, out _) || !float.IsFinite(joint.Stiffness) || joint.Stiffness < 0 || !float.IsFinite(joint.Damping) || joint.Damping < 0 || !IsFinite(joint.WorldGravity))
            {
                throw new ArgumentException($"Spring joint {joint.Bone} requires a finite, nonzero tail, nonnegative stiffness and damping, and finite gravity.", nameof(joints));
            }
            jointByBone[joint.Bone] = i;
        }
    }

    /// <summary>Advance the tails by one simulation timestep and return a pose in the animated transforms' model space.</summary>
    public Matrix4x4[] Step(IReadOnlyList<Matrix4x4> animatedGlobals, Matrix4x4 modelToWorld, float deltaSeconds)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        }
        return Evaluate(animatedGlobals, modelToWorld, deltaSeconds);
    }

    /// <summary>Apply the current tails to an animation sample without advancing the simulation.</summary>
    public Matrix4x4[] Pose(IReadOnlyList<Matrix4x4> animatedGlobals, Matrix4x4 modelToWorld)
    {
        return Evaluate(animatedGlobals, modelToWorld, 0);
    }

    /// <summary>Apply interpolated world-space tails to an animation sample without advancing the simulation.</summary>
    public Matrix4x4[] Pose(IReadOnlyList<Matrix4x4> animatedGlobals, Matrix4x4 modelToWorld, FieldSpringBonePose previous, FieldSpringBonePose current, float fraction)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        if (previous.Owner != this || current.Owner != this)
        {
            throw new ArgumentException("Spring bone pose samples must belong to this actor's simulation.");
        }
        if (!float.IsFinite(fraction) || fraction < 0 || fraction > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }
        return Evaluate(animatedGlobals, modelToWorld, 0, previous, current, fraction);
    }

    public FieldSpringBonePose Capture()
    {
        Vector3?[] positions = new Vector3?[tails.Length];
        for (int i = 0; i < tails.Length; i++)
        {
            if (tails[i].Initialized)
            {
                positions[i] = tails[i].Position;
            }
        }
        return new FieldSpringBonePose(this, positions);
    }

    public void Reset()
    {
        Array.Clear(tails);
    }

    private Matrix4x4[] Evaluate(IReadOnlyList<Matrix4x4> animatedGlobals, Matrix4x4 modelToWorld, float deltaSeconds, FieldSpringBonePose? previous = null, FieldSpringBonePose? current = null, float fraction = 0)
    {
        ArgumentNullException.ThrowIfNull(animatedGlobals);
        if (animatedGlobals.Count != parents.Length)
        {
            throw new ArgumentException("Spring bone animation samples must contain every bone in the rig.", nameof(animatedGlobals));
        }
        if (modelToWorld.M14 != 0 || modelToWorld.M24 != 0 || modelToWorld.M34 != 0 || modelToWorld.M44 != 1 || !IsFiniteTransform(modelToWorld) || !Matrix4x4.Invert(modelToWorld, out Matrix4x4 worldToModel) || !IsFiniteTransform(worldToModel))
        {
            throw new ArgumentException("A finite and invertible affine transform is required for field spring bones.", nameof(modelToWorld));
        }

        Matrix4x4[] results = new Matrix4x4[parents.Length];
        Matrix4x4[] corrections = new Matrix4x4[parents.Length];
        foreach (int bone in evaluationOrder)
        {
            int parent = parents[bone];
            // A rotation around an ancestor's pivot applies equally to the ancestor and every descendant's animated global transform.
            Matrix4x4 inherited = parent < 0 ? Matrix4x4.Identity : corrections[parent];
            Matrix4x4 transform = animatedGlobals[bone] * inherited;
            int jointIndex = jointByBone[bone];
            if (jointIndex >= 0)
            {
                Matrix4x4 rotation = EvaluateJoint(jointIndex, transform, modelToWorld, worldToModel, deltaSeconds, previous, current, fraction);
                transform *= rotation;
                inherited *= rotation;
            }
            results[bone] = transform;
            corrections[bone] = inherited;
        }
        return results;
    }

    private Matrix4x4 EvaluateJoint(int index, Matrix4x4 boneTransform, Matrix4x4 modelToWorld, Matrix4x4 worldToModel, float deltaSeconds, FieldSpringBonePose? previous, FieldSpringBonePose? current, float fraction)
    {
        FieldSpringBoneJoint joint = joints[index];
        Vector3 origin = boneTransform.Translation;
        Vector3 restOffset = Vector3.TransformNormal(joint.LocalTail, boneTransform);
        ref TailState tail = ref tails[index];
        bool advance = deltaSeconds > 0;
        if (!TryDirection(restOffset, out Vector3 restDirection, out float length))
        {
            if (advance)
            {
                tail = default;
            }
            return Matrix4x4.Identity;
        }

        Vector3 restWorld = Vector3.Transform(origin + restOffset, modelToWorld);
        Vector3 start = restWorld;
        if (previous is not null && current is not null)
        {
            start = Vector3.Lerp(previous.ReadPosition(index) ?? restWorld, current.ReadPosition(index) ?? restWorld, fraction);
        }
        else if (tail.Initialized)
        {
            start = tail.Position;
        }
        Vector3 prediction = start;
        if (advance)
        {
            // The implicit spring term keeps a stiff joint stable at the field tick rate.
            float springStep = joint.Stiffness * deltaSeconds * deltaSeconds;
            float retention = 1 / (1 + springStep);
            float attraction = float.IsPositiveInfinity(springStep) ? 1 : springStep * retention;
            float dampedSeconds = MathF.Exp(-joint.Damping * deltaSeconds) * deltaSeconds * retention;
            prediction += tail.Velocity * dampedSeconds + (restWorld - start) * attraction + joint.WorldGravity * (deltaSeconds * retention * deltaSeconds);
        }

        // Project in model space so the constraint preserves the animated bone length even with actor scaling.
        Vector3 direction = restDirection;
        if (prediction != restWorld)
        {
            Vector3 originWorld = Vector3.Transform(origin, modelToWorld);
            Vector3 predictedOffset = Vector3.TransformNormal(prediction - originWorld, worldToModel);
            if (TryDirection(predictedOffset, out Vector3 projectedDirection, out _))
            {
                direction = projectedDirection;
            }
        }
        if (advance)
        {
            Vector3 position = Vector3.Transform(origin + direction * length, modelToWorld);
            Vector3 velocity = (position - start) / deltaSeconds;
            tail = IsFinite(position) && IsFinite(velocity) ? new TailState(true, position, velocity) : default;
        }
        return RotationAtPivot(restDirection, direction, origin);
    }

    private static Matrix4x4 RotationAtPivot(Vector3 from, Vector3 to, Vector3 pivot)
    {
        Vector3 cross = Vector3.Cross(from, to);
        float cosine = Vector3.Dot(from, to);
        float angle;
        Vector3 axis;
        float sine = cross.Length();
        if (sine > 1e-6f)
        {
            axis = cross / sine;
            angle = MathF.Atan2(sine, cosine);
        }
        else if (cosine < 0)
        {
            Vector3 perpendicular = MathF.Abs(from.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
            axis = Vector3.Normalize(Vector3.Cross(from, perpendicular));
            angle = MathF.PI;
        }
        else
        {
            return Matrix4x4.Identity;
        }
        Matrix4x4 rotation = Matrix4x4.CreateFromAxisAngle(axis, angle);
        rotation.Translation = pivot - Vector3.TransformNormal(pivot, rotation);
        return rotation;
    }

    private static int[] OrderBones(int[] parents)
    {
        int[] order = new int[parents.Length];
        List<int>?[] children = new List<int>?[parents.Length];
        int next = 0;
        for (int bone = 0; bone < parents.Length; bone++)
        {
            int parent = parents[bone];
            if (parent == -1)
            {
                order[next++] = bone;
            }
            else if ((uint)parent >= parents.Length)
            {
                throw new ArgumentException($"Bone {bone} has an invalid parent {parent}.", nameof(parents));
            }
            else
            {
                (children[parent] ??= []).Add(bone);
            }
        }
        for (int i = 0; i < next; i++)
        {
            if (children[order[i]] is { } descendants)
            {
                descendants.CopyTo(order, next);
                next += descendants.Count;
            }
        }
        if (next != parents.Length)
        {
            throw new ArgumentException("Spring bone rig parents must form an acyclic hierarchy.", nameof(parents));
        }
        return order;
    }

    private static bool TryDirection(Vector3 offset, out Vector3 direction, out float length)
    {
        length = offset.Length();
        if (length > 0 && float.IsFinite(length))
        {
            direction = offset / length;
            return true;
        }
        direction = default;
        return false;
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private static bool IsFiniteTransform(Matrix4x4 value)
    {
        return IsFinite(value.Translation) && IsFinite(new Vector3(value.M11, value.M12, value.M13)) && IsFinite(new Vector3(value.M21, value.M22, value.M23)) && IsFinite(new Vector3(value.M31, value.M32, value.M33));
    }

    private readonly record struct TailState(bool Initialized, Vector3 Position, Vector3 Velocity);

    public sealed class FieldSpringBonePose
    {
        private readonly Vector3?[] positions;

        internal FieldSpringBonePose(FieldSpringBoneSimulation owner, Vector3?[] positions)
        {
            Owner = owner;
            this.positions = positions;
        }

        internal FieldSpringBoneSimulation Owner { get; }

        internal Vector3? ReadPosition(int joint)
        {
            return positions[joint];
        }
    }
}
