using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattlePoseMatrices
{
    /// <summary>502D40/5095B0 camera-compose each original508C90 bone matrix before509B30 publishes GTE registers.</summary>
    public static ImmutableArray<NativeBattleActionMatrix> GeometryMatrices(NativeBattleGraphicsModel model, NativeBattlePoseSource source, NativeBattleActionMatrix camera, NativeBattleVector? actualCameraEye = null)
    {
        return NativeBattleBodyBoneMatrices.Require(ComposeGeometryMatrices502D40(model, source, camera, actualCameraEye), source);
    }

    public static NativeBattleGraphicsModel ComposeGeometryMatrices502D40(NativeBattleGraphicsModel model, NativeBattlePoseSource source, NativeBattleActionMatrix camera, NativeBattleVector? actualCameraEye = null)
    {
        NativeBattleBodyBoneMatrices.Require(model, source);
        return NativeBattleBodyBoneMatrices.Compose5095B0(model, GeometryBasis502D40(model, camera, actualCameraEye));
    }

    private static NativeBattleActionMatrix GeometryBasis502D40(NativeBattleGraphicsModel model, NativeBattleActionMatrix camera, NativeBattleVector? actualCameraEye)
    {
        camera.Validate();
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native502D40 requires its retained model matrix.");
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Compose56C2F0(camera, world);
        ushort flags2 = model.Flags2 ?? throw new NotSupportedException("Native502D40 requires its actual model flagsWORD2.");
        if ((flags2 & 1) != 0)
        {
            NativeBattleVector eye = actualCameraEye ?? throw new NotSupportedException("Native502D40 billboard geometry requires its current camera eye.");
            short rotation = model.Rotation14 ?? throw new NotSupportedException("Native502D40 billboard geometry requires its signed model rotationWORD14.");
            int x = unchecked((short)world.X) - eye.X;
            int z = unchecked((short)world.Z) - eye.Z;
            int angle = NativePrecision53.RoundInteger(Math.Atan2(x, z) * (double)BitConverter.Int32BitsToSingle(0x4422F983));
            return NativeBattleGfMatrices.RotateY56D020(basis, unchecked(angle - rotation - 1024));
        }
        return basis;
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

    public static NativeBattleActionMatrix PublishGeometryWorldMatrix502AB0(NativeBattleGraphicsModel model, NativeBattlePoseSource source, NativeBattleModelScaleVector? pointedScale = null)
    {
        uint pointer = model.ScaleVectorAddress48 ?? throw new NotSupportedException("Native502AB0 requires the retained model DWORD30 scale pointer; migrate the named older snapshot explicitly.");
        if (pointer == 0 && pointedScale is not null || pointer != 0 && pointedScale is null)
        {
            throw new NotSupportedException("Native502AB0 requires the actual three DWORD scale values exactly when its model pointer is nonnull.");
        }
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native502AB0 requires its model transform WORDs.");
        NativeBattleActionMatrix root = NativeBattleBodyBoneMatrices.Require(model, source)[0];
        Matrix matrix = ModelMatrix(model, transform, new(root.Rotation.ToArray(), root.X, root.Y, root.Z), pointedScale);
        return new(matrix.Rotation.ToImmutableArray(), matrix.X, matrix.Y, matrix.Z, 4096);
    }

    public static ImmutableArray<NativeBattleActionMatrix> LinkedGeometryMatrices502D40(NativeBattleGraphicsModel model, NativeBattlePoseSource source, NativeBattleActionMatrix camera)
    {
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native linked geometry requires its retained parent model matrix.");
        return ComposeLinkedGeometry(model, source, NativeBattleGfMatrices.Compose56C2F0(camera, world));
    }

    public static ImmutableArray<NativeBattleActionMatrix> LinkedDrawGeometryMatrices502D40(NativeBattleGraphicsModel model, NativeBattlePoseSource source, NativeBattleActionMatrix camera, NativeBattleVector? actualCameraEye = null)
    {
        return ComposeLinkedGeometry(model, source, GeometryBasis502D40(model, camera, actualCameraEye));
    }

    private static ImmutableArray<NativeBattleActionMatrix> ComposeLinkedGeometry(NativeBattleGraphicsModel model, NativeBattlePoseSource source, NativeBattleActionMatrix basis)
    {
        NativeBattleLinkedModelTransform local = model.LinkedTransform ?? throw new NotSupportedException("Native linked geometry requires its actual local WORDs20..32.");
        NativeBattleSequenceState state = model.SequenceState ?? throw new NotSupportedException("Native linked geometry requires its independent pose owner.");
        NativeBattleDecodedPose pose = ReadLinkedDecodedPose(state, source);
        Matrix parent = new(basis.Rotation.ToArray(), basis.X, basis.Y, basis.Z);
        Matrix localMatrix = Rotation(local.Rotation) with { X = local.Position.X, Y = local.Position.Y, Z = local.Position.Z };
        Matrix combined = Compose(parent, localMatrix);
        return BoneMatrices(source, pose).Select(bone => Compose(combined, bone)).Select(matrix => new NativeBattleActionMatrix(matrix.Rotation.ToImmutableArray(), matrix.X, matrix.Y, matrix.Z, 4096)).ToImmutableArray();
    }
}
