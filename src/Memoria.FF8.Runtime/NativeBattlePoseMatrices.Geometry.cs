using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattlePoseMatrices
{
    /// <summary>502D40/5095B0 camera-compose each original508C90 bone matrix before509B30 publishes GTE registers.</summary>
    public static ImmutableArray<NativeBattleActionMatrix> GeometryMatrices(NativeBattleGraphicsModel model, NativeBattlePoseSource source, NativeBattleActionMatrix camera)
    {
        camera.Validate();
        ushort flags2 = model.Flags2 ?? throw new NotSupportedException("Native502D40 requires its actual model flagsWORD2.");
        if ((flags2 & 1) != 0)
        {
            throw new NotSupportedException("Native502D40 billboard geometry requires its original50CDC0 angle owner.");
        }
        return ModelGroupMatrices50F190(model, source, camera);
    }

    public static ImmutableArray<NativeBattleActionMatrix> ModelGroupMatrices50F190(NativeBattleGraphicsModel model, NativeBattlePoseSource source, NativeBattleActionMatrix camera)
    {
        return NativeBattleBodyBoneMatrices.Require(ComposeGeometryMatrices5095B0(model, source, camera), source);
    }

    public static NativeBattleGraphicsModel ComposeGeometryMatrices5095B0(NativeBattleGraphicsModel model, NativeBattlePoseSource source, NativeBattleActionMatrix camera)
    {
        camera.Validate();
        NativeBattleBodyBoneMatrices.Require(model, source);
        NativeBattleActionMatrix previous = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native502D40 requires its prior502AB0 model matrix; reconstructing it after the current service is unsupported.");
        Matrix world = new(previous.Rotation.ToArray(), previous.X, previous.Y, previous.Z);
        Matrix view = new(camera.Rotation.ToArray(), camera.X, camera.Y, camera.Z);
        Matrix basis = Compose(view, world);
        return NativeBattleBodyBoneMatrices.Compose5095B0(model, new(basis.Rotation.ToImmutableArray(), basis.X, basis.Y, basis.Z, 4096));
    }

    public static NativeBattleActionMatrix PublishGeometryWorldMatrix502AB0(NativeBattleGraphicsModel model, NativeBattlePoseSource source)
    {
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native502AB0 requires its model transform WORDs.");
        NativeBattleActionMatrix root = NativeBattleBodyBoneMatrices.Require(model, source)[0];
        Matrix matrix = ModelMatrix(model, transform, new(root.Rotation.ToArray(), root.X, root.Y, root.Z));
        return new(matrix.Rotation.ToImmutableArray(), matrix.X, matrix.Y, matrix.Z, 4096);
    }

    public static ImmutableArray<NativeBattleActionMatrix> LinkedGeometryMatrices502D40(NativeBattleGraphicsModel model, NativeBattlePoseSource source, NativeBattleActionMatrix camera)
    {
        NativeBattleActionMatrix previous = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native linked geometry requires its retained parent model matrix.");
        NativeBattleLinkedModelTransform local = model.LinkedTransform ?? throw new NotSupportedException("Native linked geometry requires its actual local WORDs20..32.");
        NativeBattleSequenceState state = model.SequenceState ?? throw new NotSupportedException("Native linked geometry requires its independent pose owner.");
        NativeBattleDecodedPose pose = ReadLinkedDecodedPose(state, source);
        Matrix world = new(previous.Rotation.ToArray(), previous.X, previous.Y, previous.Z);
        Matrix view = new(camera.Rotation.ToArray(), camera.X, camera.Y, camera.Z);
        Matrix localMatrix = Rotation(local.Rotation) with { X = local.Position.X, Y = local.Position.Y, Z = local.Position.Z };
        Matrix combined = Compose(Compose(view, world), localMatrix);
        return BoneMatrices(source, pose).Select(bone => Compose(combined, bone)).Select(matrix => new NativeBattleActionMatrix(matrix.Rotation.ToImmutableArray(), matrix.X, matrix.Y, matrix.Z, 4096)).ToImmutableArray();
    }
}
