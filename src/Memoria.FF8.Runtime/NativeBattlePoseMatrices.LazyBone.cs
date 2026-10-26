namespace Memoria.FF8.Runtime;

public static partial class NativeBattlePoseMatrices
{
    public static NativeBattleVector ReadBoneLazy(NativeBattleGraphicsModel model, Func<NativeBattlePoseSource> readSource, byte selector, short fraction)
    {
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native502170 requires its model transform owner.");
        if ((model.Flags0 & 2) == 0)
        {
            return transform.Position28;
        }
        return ReadBone(model, readSource(), selector, fraction);
    }
}
