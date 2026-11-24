using System.Numerics;

namespace Memoria.FF8.Assets;

/// <summary>A VRM 1.0 spring segment whose tail position is expressed in the head bone's bind space.</summary>
public readonly record struct FieldSpringBoneJoint(int Bone, Vector3 LocalTail, float Stiffness, float DragForce, Vector3 WorldGravity)
{
    public int? Center { get; init; }
    public float HitRadius { get; init; }
    public IReadOnlyList<FieldSpringBoneCollider> Colliders { get; init; } = [];
}

/// <summary>A collider with bone-local endpoints and a world-space radius. A sphere has no tail endpoint.</summary>
public readonly record struct FieldSpringBoneCollider(int Bone, Vector3 Offset, float Radius, Vector3? Tail = null);

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
            if (!TryDirection(joint.LocalTail, out _, out _) || !IsNonnegative(joint.Stiffness) || !IsNonnegative(joint.DragForce) || joint.DragForce > 1 || !IsFinite(joint.WorldGravity) || !IsNonnegative(joint.HitRadius))
            {
                throw new ArgumentException($"Spring joint {joint.Bone} has invalid tail, force or radius values.", nameof(joints));
            }
            if (joint.Center is int center && !IsAncestorOrSelf(center, joint.Bone))
            {
                throw new ArgumentException($"Spring joint {joint.Bone} requires its center to be an ancestor or itself.", nameof(joints));
            }
            FieldSpringBoneCollider[] colliders = joint.Colliders.ToArray();
            foreach (FieldSpringBoneCollider collider in colliders)
            {
                if ((uint)collider.Bone >= parents.Count || !IsFinite(collider.Offset) || !IsNonnegative(collider.Radius) || (collider.Tail is Vector3 end && !IsFinite(end)))
                {
                    throw new ArgumentException($"Spring joint {joint.Bone} references an invalid sphere or capsule collider.", nameof(joints));
                }
            }
            this.joints[i] = joint with { Colliders = colliders };
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

    /// <summary>Apply interpolated tail samples in world or center space without advancing the simulation.</summary>
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
                positions[i] = tails[i].Current;
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
        if (modelToWorld.M14 != 0 || modelToWorld.M24 != 0 || modelToWorld.M34 != 0 || modelToWorld.M44 != 1 || !IsFiniteTransform(modelToWorld) || !Matrix4x4.Invert(modelToWorld, out Matrix4x4 inverse) || !IsFiniteTransform(inverse))
        {
            throw new ArgumentException("A finite and invertible affine transform is required for field spring bones.", nameof(modelToWorld));
        }

        Matrix4x4[] results = new Matrix4x4[parents.Length];
        Matrix4x4?[] corrections = new Matrix4x4?[parents.Length];
        foreach (int bone in evaluationOrder)
        {
            int parent = parents[bone];
            // A rotation around an ancestor's pivot applies equally to the ancestor and every descendant's animated global transform.
            Matrix4x4 inherited = parent < 0 ? Matrix4x4.Identity : corrections[parent]!.Value;
            Matrix4x4 transform = animatedGlobals[bone] * inherited;
            int jointIndex = jointByBone[bone];
            if (jointIndex >= 0 && Matrix4x4.Invert(animatedGlobals[bone], out Matrix4x4 inverseAnimated))
            {
                Matrix4x4 posedParent = parent < 0 ? Matrix4x4.Identity : results[parent];
                if (Matrix4x4.Invert(posedParent, out Matrix4x4 inverseParent))
                {
                    transform = EvaluateJoint(jointIndex, transform, posedParent, inverseParent, modelToWorld, animatedGlobals, corrections, deltaSeconds, previous, current, fraction);
                    inherited = inverseAnimated * transform;
                }
            }
            else if (jointIndex >= 0 && deltaSeconds > 0)
            {
                tails[jointIndex] = default;
            }
            results[bone] = transform;
            corrections[bone] = inherited;
        }
        return results;
    }

    private Matrix4x4 EvaluateJoint(int index, Matrix4x4 animated, Matrix4x4 posedParent, Matrix4x4 inverseParent, Matrix4x4 modelToWorld, IReadOnlyList<Matrix4x4> animatedGlobals, Matrix4x4?[] corrections, float deltaSeconds, FieldSpringBonePose? previous, FieldSpringBonePose? current, float fraction)
    {
        FieldSpringBoneJoint joint = joints[index];
        ref TailState tail = ref tails[index];
        bool advance = deltaSeconds > 0;
        if (!advance && (previous is null ? !tail.Initialized : previous.ReadPosition(index) is null && current?.ReadPosition(index) is null))
        {
            return animated;
        }

        // The bind tail defines the segment, and the animation supplies its reference orientation for this sample.
        Matrix4x4 local = animated * inverseParent;
        if (!Matrix4x4.Decompose(local, out Vector3 scale, out Quaternion rotation, out Vector3 translation))
        {
            if (advance)
            {
                tail = default;
            }
            return animated;
        }
        Matrix4x4 rest = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation) * posedParent;
        Matrix4x4 restToWorld = rest * modelToWorld;
        if (!Matrix4x4.Invert(restToWorld, out Matrix4x4 worldToRest))
        {
            if (advance)
            {
                tail = default;
            }
            return animated;
        }
        Matrix4x4 localScale = Matrix4x4.CreateScale(scale);
        Vector3 scaledTail = joint.LocalTail * scale;
        Vector3 worldOffset = Vector3.TransformNormal(scaledTail, restToWorld);
        if (!TryDirection(worldOffset, out Vector3 worldRestDirection, out float length))
        {
            if (advance)
            {
                tail = default;
            }
            return animated;
        }
        Vector3 origin = restToWorld.Translation;
        Vector3 restWorld = origin + worldOffset;
        Matrix4x4 centerToWorld = joint.Center switch
        {
            int center when center == joint.Bone => localScale * restToWorld,
            int center => ReadCorrectedTransform(center, animatedGlobals, corrections) * modelToWorld,
            _ => Matrix4x4.Identity,
        };
        if (!Matrix4x4.Invert(centerToWorld, out Matrix4x4 worldToCenter))
        {
            if (advance)
            {
                tail = default;
            }
            return animated;
        }

        Vector3 restCenter = Vector3.Transform(restWorld, worldToCenter);
        Vector3 start = tail.Initialized ? tail.Current : restCenter;
        if (previous is not null && current is not null)
        {
            start = Vector3.Lerp(previous.ReadPosition(index) ?? restCenter, current.ReadPosition(index) ?? restCenter, fraction);
        }
        Vector3 prediction = Vector3.Transform(start, centerToWorld);
        if (advance)
        {
            // VRM drag retains a fraction of the previous displacement per field tick.
            // Stiffness and gravity add a world-space displacement scaled by the tick duration.
            Vector3 inertia = tail.Initialized ? (tail.Current - tail.Previous) * (1 - joint.DragForce) : Vector3.Zero;
            prediction += Vector3.TransformNormal(inertia, centerToWorld) + (worldRestDirection * joint.Stiffness + joint.WorldGravity) * deltaSeconds;
            prediction = ConstrainLength(prediction, origin, worldRestDirection, length);
            foreach (FieldSpringBoneCollider collider in joint.Colliders)
            {
                Matrix4x4 colliderToWorld = ReadCorrectedTransform(collider.Bone, animatedGlobals, corrections) * modelToWorld;
                prediction = ResolveCollision(prediction, origin, worldRestDirection, length, joint.HitRadius, collider, colliderToWorld);
            }
            Vector3 next = Vector3.Transform(prediction, worldToCenter);
            tail = IsFinite(next) ? new TailState(true, next, start) : default;
        }

        Vector3 to = Vector3.Transform(prediction, worldToRest);
        if (!TryDirection(scaledTail, out Vector3 fromDirection, out _) || !TryDirection(to, out Vector3 toDirection, out _))
        {
            return localScale * rest;
        }
        return localScale * SwingRotation(fromDirection, toDirection) * rest;
    }

    private Matrix4x4 ReadCorrectedTransform(int bone, IReadOnlyList<Matrix4x4> animatedGlobals, Matrix4x4?[] corrections)
    {
        // A collider can be attached to a node outside the portion of the rig already visited.
        for (int ancestor = bone; ancestor >= 0; ancestor = parents[ancestor])
        {
            if (corrections[ancestor] is Matrix4x4 correction)
            {
                return animatedGlobals[bone] * correction;
            }
        }
        return animatedGlobals[bone];
    }

    private static Vector3 ResolveCollision(Vector3 prediction, Vector3 origin, Vector3 restDirection, float length, float jointRadius, FieldSpringBoneCollider collider, Matrix4x4 colliderToWorld)
    {
        Vector3 closest = Vector3.Transform(collider.Offset, colliderToWorld);
        Vector3 capsuleAxis = Vector3.Zero;
        if (collider.Tail is Vector3 end)
        {
            capsuleAxis = Vector3.TransformNormal(end - collider.Offset, colliderToWorld);
            float squaredLength = capsuleAxis.LengthSquared();
            if (squaredLength > 0)
            {
                float fraction = Math.Clamp(Vector3.Dot(prediction - closest, capsuleAxis) / squaredLength, 0, 1);
                closest += capsuleAxis * fraction;
            }
        }
        bool hasNormal = TryDirection(prediction - closest, out Vector3 normal, out float distance);
        float penetration = collider.Radius + jointRadius - distance;
        if (penetration <= 0 || !float.IsFinite(penetration))
        {
            return prediction;
        }
        if (!hasNormal && !TryDirection(Vector3.Cross(capsuleAxis, prediction - origin), out normal, out _))
        {
            normal = Perpendicular(restDirection);
        }
        return ConstrainLength(prediction + normal * penetration, origin, restDirection, length);
    }

    private static Vector3 ConstrainLength(Vector3 tail, Vector3 origin, Vector3 restDirection, float length)
    {
        return origin + (TryDirection(tail - origin, out Vector3 direction, out _) ? direction : restDirection) * length;
    }

    private static Matrix4x4 SwingRotation(Vector3 from, Vector3 to)
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
            axis = Perpendicular(from);
            angle = MathF.PI;
        }
        else
        {
            return Matrix4x4.Identity;
        }
        return Matrix4x4.CreateFromAxisAngle(axis, angle);
    }

    private static Vector3 Perpendicular(Vector3 direction)
    {
        Vector3 axis = MathF.Abs(direction.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
        return Vector3.Normalize(Vector3.Cross(direction, axis));
    }

    private bool IsAncestorOrSelf(int ancestor, int bone)
    {
        for (; bone >= 0; bone = parents[bone])
        {
            if (bone == ancestor)
            {
                return true;
            }
        }
        return false;
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

    private static bool IsNonnegative(float value)
    {
        return float.IsFinite(value) && value >= 0;
    }

    private static bool IsFiniteTransform(Matrix4x4 value)
    {
        return IsFinite(value.Translation) && IsFinite(new Vector3(value.M11, value.M12, value.M13)) && IsFinite(new Vector3(value.M21, value.M22, value.M23)) && IsFinite(new Vector3(value.M31, value.M32, value.M33));
    }

    private readonly record struct TailState(bool Initialized, Vector3 Current, Vector3 Previous);

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
