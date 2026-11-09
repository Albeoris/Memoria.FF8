using System.Numerics;

namespace Memoria.FF8.Rendering;

/// <summary>Transfers a global bind-relative rotation into the current target parent's local basis.</summary>
public static class SemanticJointRotation
{
    public static Matrix4x4 Transfer(Matrix4x4 targetRestRotation, Matrix4x4 sourceRestRotation, Matrix4x4 sourceCurrentRotation, Matrix4x4 targetParentCurrentRotation, Matrix4x4 calibratedAxes, Matrix4x4 inverseCalibratedAxes)
    {
        Matrix4x4 restTargetRotation = targetRestRotation * calibratedAxes;
        Matrix4x4 inverseSourceRest = Matrix4x4.Transpose(sourceRestRotation);
        Matrix4x4 desiredGlobal = restTargetRotation * inverseSourceRest * sourceCurrentRotation * inverseCalibratedAxes;
        Matrix4x4 inverseParentRotation = Matrix4x4.Transpose(targetParentCurrentRotation);
        return desiredGlobal * inverseParentRotation;
    }
}
