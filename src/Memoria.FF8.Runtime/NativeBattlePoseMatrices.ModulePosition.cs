namespace Memoria.FF8.Runtime;

public static partial class NativeBattlePoseMatrices
{
    /// <summary>570CC0 placement4 rotates with56C4F0, then adds the current model matrix translation through WORD stores.</summary>
    public static NativeBattleVector TransformModulePosition570CC0(NativeBattleGraphicsModel model, NativeBattleVector local)
    {
        NativeBattleActionMatrix current = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native570CC0 placement4 requires the retained502AB0 matrix; rebuilding it after the model callback is unsupported.");
        current.Validate();
        Matrix matrix = new(current.Rotation.ToArray(), current.X, current.Y, current.Z);
        (int x, int y, int z) = ApplyRounded(matrix, local.X, local.Y, local.Z);
        return new(unchecked((short)(x + current.X)), unchecked((short)(y + current.Y)), unchecked((short)(z + current.Z)));
    }
}
