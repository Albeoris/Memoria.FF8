using System.Numerics;
using System.Text.Json;

namespace Memoria.FF8.Assets;

public static partial class ModelExchange
{
    /// <summary>Converts retained editable positions to upright Y-up authoring/viewer space.</summary>
    public static Vector3 ToAuthoringSpace(Vector3 position, string kind) => Vector3.Transform(position, AuthoringTransform(kind));

    /// <summary>Converts a complete pose, respecting each linked rig's resource kind.</summary>
    public static IReadOnlyList<Vector3[]> ToAuthoringSpace(EditableModel model, IReadOnlyList<Vector3[]> pose)
    {
        if (pose.Count != model.Meshes.Count)
        {
            throw new ArgumentException("Pose primitives do not match the model.", nameof(pose));
        }
        Dictionary<string, string> kinds = AuthoringKinds(model);
        return pose.Select((vertices, index) =>
        {
            string? rig = model.Meshes[index].RigId;
            Matrix4x4 basis = AuthoringTransform(rig is not null && kinds.TryGetValue(rig, out string? kind) ? kind : model.Kind);
            return vertices.Select(position => Vector3.Transform(position, basis)).ToArray();
        }).ToArray();
    }

    private static Matrix4x4 AuthoringTransform(string kind) => kind is "battle" or "stage"
        ? new Matrix4x4(-1, 0, 0, 0, 0, 0, -1, 0, 0, -1, 0, 0, 0, 0, 0, 1)
        : Matrix4x4.CreateScale(1, -1, -1);

    private static Dictionary<string, string> AuthoringKinds(EditableModel model)
    {
        Dictionary<string, string> kinds = [];
        if (model.Kind == "assembly" && model.ExchangeManifest is not null)
        {
            using JsonDocument manifest = JsonDocument.Parse(model.ExchangeManifest);
            foreach (JsonElement item in manifest.RootElement.GetProperty("Objects").EnumerateArray())
            {
                kinds[item.GetProperty("RigId").GetString()!] = item.GetProperty("Kind").GetString()!;
            }
        }
        return kinds;
    }

    private static EditableModel ChangeAuthoringBasis(EditableModel model, bool importing = false)
    {
        Dictionary<string, string> kinds = AuthoringKinds(model);
        Matrix4x4 Basis(string? rig)
        {
            Matrix4x4 result = AuthoringTransform(rig is not null && kinds.TryGetValue(rig, out string? kind) ? kind : model.Kind);
            return importing ? Matrix4x4.Transpose(result) : result;
        }
        Matrix4x4 Transform(Matrix4x4 value, Matrix4x4 basis) => Matrix4x4.Transpose(basis) * value * basis;
        EditableBone Bone(EditableBone bone)
        {
            Matrix4x4 basis = Basis(bone.RigId);
            Quaternion rotation = Quaternion.CreateFromRotationMatrix(Transform(Matrix4x4.CreateFromQuaternion(bone.Rotation), basis));
            return bone with { Translation = Vector3.Transform(bone.Translation, basis), Rotation = Quaternion.Normalize(rotation), Scale = Vector3.TransformNormal(bone.Scale, Absolute(basis)) };
        }
        EditableAnimationChannel Channel(EditableAnimationChannel channel)
        {
            Matrix4x4 basis = Basis(model.Bones[channel.Bone].RigId);
            return channel with { Values = channel.Values.Select(value =>
            {
                if (channel.Path == "rotation")
                {
                    // CUBICSPLINE tangents are quaternion derivatives, not unit rotations.
                    Vector3 converted = Vector3.TransformNormal(new Vector3(value.X, value.Y, value.Z), basis);
                    return new Vector4(converted, value.W);
                }
                return new Vector4(Vector3.Transform(new Vector3(value.X, value.Y, value.Z), channel.Path == "scale" ? Absolute(basis) : basis), value.W);
            }).ToArray() };
        }
        return model with
        {
            Bones = model.Bones.Select(Bone).ToArray(),
            Meshes = model.Meshes.Select(mesh =>
            {
                Matrix4x4 basis = Basis(mesh.RigId);
                return mesh with
                {
                    Vertices = mesh.Vertices.Select(vertex => vertex with { Position = Vector3.Transform(vertex.Position, basis), Normal = Vector3.TransformNormal(vertex.Normal, basis) }).ToArray(),
                    Transform = Transform(mesh.Transform, basis),
                    InverseBindMatrices = mesh.InverseBindMatrices?.ToDictionary(pair => pair.Key, pair => Transform(pair.Value, basis))
                };
            }).ToArray(),
            Animations = model.Animations.Select(animation => animation with { Channels = animation.Channels.Select(Channel).ToArray() }).ToArray()
        };
    }

    private static Matrix4x4 Absolute(Matrix4x4 value) => new(MathF.Abs(value.M11), MathF.Abs(value.M12), MathF.Abs(value.M13), 0, MathF.Abs(value.M21), MathF.Abs(value.M22), MathF.Abs(value.M23), 0, MathF.Abs(value.M31), MathF.Abs(value.M32), MathF.Abs(value.M33), 0, 0, 0, 0, 1);
}
